
using LotteryCore.Data;
using LotteryCore.DTO;
using LotteryCore.Enetities;
using LotteryCore.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace LotteryPullService
{
    internal class Program
    {
        static IConfiguration _config;
        static IServiceProvider _serviceProvider;
        static CancellationTokenSource _cts = new CancellationTokenSource();
        private const int MaxSyncHistoryAwardCount = 8;
        private const int BatchSaveDbRow = 5;

        static async Task Main(string[] args)
        {
            // 日志只输出文件
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(Path.Combine(AppContext.BaseDirectory, "logs/pull_service_.log"), rollingInterval: RollingInterval.Day)
                .CreateLogger();

            // 加载配置
            var cfgBuilder = new ConfigurationBuilder();
            cfgBuilder.AddJsonFile("appsettings.json", false, true);
            _config = cfgBuilder.Build();

            // 构建DI容器
            var services = new ServiceCollection();
            string connStr = _config.GetConnectionString("DefaultConnection")!;
            services.AddDbContext<AppDBContext>(opt =>
            {
                opt.UseMySql(connStr, ServerVersion.AutoDetect(connStr));
            });
            _serviceProvider = services.BuildServiceProvider();

            // 监听关闭信号
            Console.CancelKeyPress += (s, e) =>
            {
                _cts.Cancel();
                e.Cancel = true;
                Log.Information("收到关闭信号，停止拉取");
            };

            Log.Information("=== 独立拉取服务启动成功，主线程循环运行 ===");

            // 主线程永久循环，不会被系统判定空闲退出
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();

                    // ========== 每日23点生成次日期号逻辑 ==========
                    var now = DateTime.Now;
                    // 23点整执行生成，避免重复执行
                    if (now.Hour == 23 && now.Minute < 5)
                    {
                        await GenerateNextDayPeriod(db, _cts.Token);
                    }
                    var list = await db.Lottery.Where(x => x.IsEnable&&x.Id==1).ToListAsync(_cts.Token);
                    Log.Debug($"本轮读取启用彩种：{list.Count}个");

                    foreach (var lot in list)
                    {
                        await PullSingle(lot, db, _cts.Token);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "全局拉取异常");
                }

                int sleep = Random.Shared.Next(4000, 6000);
                Log.Debug($"本轮完成，休眠{sleep}ms");
                await Task.Delay(sleep, _cts.Token);
            }

            Log.Information("服务正常退出");
            Log.CloseAndFlush();
        }


        #region 

        #endregion
        /// 单彩种拉取逻辑（复制你原有逻辑）
        private static async Task PullSingle(Lottery lottery, AppDBContext db, CancellationToken token)
        {
            try
            {
                string url = $"https://www.manycailm.com/api/lottery/awards?code={lottery.ApiCode}";
                string json = await HttpHelper.SafeGetAsync(url);
                if (string.IsNullOrEmpty(json))
                {
                    Log.Warning($"{lottery.LotteryName} 接口无返回");
                    return;
                }

                var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.GetProperty("code").GetInt32() != 0)
                {
                    Log.Warning($"{lottery.LotteryName} 接口异常：{root.GetProperty("msg")}");
                    return;
                }
                var opt = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringConverter(), new JsonNullableDateTimeConverter() }
                };
                var result = JsonSerializer.Deserialize<LotteryApiResultDTO>(json, opt);
                if (result != null) {
                    var apiData = result.data;
                    var lotteryInfo = apiData.lottery;
                    if (lotteryInfo.status != "open")
                    {
                        //_logger.LogInformation("彩种[{0}]接口状态非open，暂时跳过", lottery.LotteryName);
                        return;
                    }

                    DateTime now = DateTime.Now;
                    string currentIssueNo = lotteryInfo.NextIssueStr;
                    int remainSecond = lotteryInfo.next_open_remaining;

                    // 安全转换开奖时间
                    DateTime? currentOpenTime = null;
                    if (!string.IsNullOrWhiteSpace(lotteryInfo.next_opendate) && DateTime.TryParse(lotteryInfo.next_opendate, out var dt))
                    {
                        currentOpenTime = dt;
                    }

                    DateTime openTime = now.AddSeconds(remainSecond);
                    DateTime endBetTime = openTime.AddSeconds(-lottery.StopBetSecond);

                    // 处理待开奖期号
                    var currentIssueEntity = await db.LotteryDatas
                        .FirstOrDefaultAsync(d => d.LotteryId == lottery.Id && d.PeriodNo == currentIssueNo, token);

                    if (currentIssueEntity == null)
                    {
                        currentIssueEntity = new LotteryData
                        {
                            LotteryId = lottery.Id,
                            PeriodNo = currentIssueNo,
                            EndTime = endBetTime,
                            OpenTime = currentOpenTime,
                            IsOpen = 0,
                            CreateTime = now
                        };
                        db.LotteryDatas.Add(currentIssueEntity);
                        //_logger.LogInformation("【{0}】新增待开奖期号：{1}", lottery.LotteryName, currentIssueNo);
                    }
                    else
                    {
                        currentIssueEntity.EndTime = endBetTime;
                        currentIssueEntity.OpenTime = currentOpenTime;
                    }

                    // 分批同步历史开奖，避免一次性锁表超时
                    var syncAwardList = apiData.awards.OrderBy(o => o.opendate).Take(MaxSyncHistoryAwardCount).ToList();
                    int batchCount = 0;
                    foreach (var awardItem in syncAwardList)
                    {
                        var existAward = await db.LotteryDatas
                            .FirstOrDefaultAsync(d => d.LotteryId == lottery.Id && d.PeriodNo == awardItem.issue, token);

                        DateTime? awardOpenDt = null;
                        if (!string.IsNullOrWhiteSpace(awardItem.opendate) && DateTime.TryParse(awardItem.opendate, out var awardDt))
                        {
                            awardOpenDt = awardDt;
                        }

                        if (existAward == null)
                        {
                            existAward = new LotteryData
                            {
                                LotteryId = lottery.Id,
                                PeriodNo = awardItem.issue,
                                OpenNumber = awardItem.code,
                                EndTime = now,
                                OpenTime = awardOpenDt,
                                IsOpen = 1,
                                CreateTime = now
                            };
                            db.LotteryDatas.Add(existAward);
                        }
                        else
                        {
                            existAward.OpenNumber = awardItem.code;
                            existAward.OpenTime = awardOpenDt;
                            existAward.IsOpen = 1;
                        }

                        batchCount++;
                        if (batchCount >= BatchSaveDbRow)
                        {
                            await db.SaveChangesAsync(token);
                            batchCount = 0;
                        }
                    }
                    if (batchCount > 0)
                    {
                        await db.SaveChangesAsync(token);
                    }

                    // 倒计时到期自动开奖标记
                    if (remainSecond <= 0 && currentIssueEntity.IsOpen == 0)
                    {
                        var matchAward = apiData.awards.Find(x => x.issue == currentIssueNo);
                        if (matchAward != null)
                        {
                            currentIssueEntity.OpenNumber = matchAward.code;
                            currentIssueEntity.OpenTime = now;
                            currentIssueEntity.IsOpen = 1;
                            await db.SaveChangesAsync(token);
                            //_logger.LogInformation("【{0}】期号{1}开奖完成，开奖号码：{2}",
                            //    lottery.LotteryName, currentIssueNo, matchAward.code);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"{lottery.LotteryName} 单彩种拉取失败");
            }
        }
        /// <summary>
        /// 生成次日所有5分钟一期期号 yyyyMMdd-0001 ~ yyyyMMdd-0288
        /// </summary>
        private static async Task GenerateNextDayPeriod(AppDBContext db, CancellationToken token)
        {
            var tomorrow = DateTime.Now.Date.AddDays(1);
            string dateStr = tomorrow.ToString("yyyyMMdd");

            // 查询该彩种是否已存在次日期号，避免重复生成
            var allLotteries = await db.Lottery.Where(x => x.IsEnable).ToListAsync(token);
            foreach (var lot in allLotteries)
            {
                bool exist = await db.LotteryDatas.AnyAsync(d =>
                    d.LotteryId == lot.Id && d.PeriodNo.StartsWith(dateStr), token);

                if (exist)
                {
                    Log.Debug($"彩种{lot.LotteryName}次日期号已存在，跳过生成");
                    continue;
                }

                // 0点0分开始，每5分钟一期，共288期
                DateTime currentTime = tomorrow;
                for (int i = 1; i <= 288; i++)
                {
                    string seq = i.ToString("D4"); // 0001、0002...0288
                    string period = $"{dateStr}-{seq}";

                    var data = new LotteryData
                    {
                        LotteryId = lot.Id,
                        PeriodNo = period,
                        OpenNumber = null,
                        IsOpen = 0, // 待开奖
                        OpenTime = currentTime,
                        EndTime = currentTime.AddSeconds(-10), // 提前10秒封盘
                        CreateTime = DateTime.Now
                    };
                    await db.LotteryDatas.AddAsync(data, token);

                    // 每次+5分钟
                    currentTime = currentTime.AddMinutes(5);
                }
                Log.Information($"成功生成{lot.LotteryName}次日{dateStr}全部288条期号");
            }
            await db.SaveChangesAsync(token);
        }
    }
}