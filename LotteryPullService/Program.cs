
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
                    var list = await db.Lottery.Where(x => x.IsEnable&&!string.IsNullOrEmpty(x.ApiCode)).ToListAsync(_cts.Token);
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
                //var arr = root.GetProperty("data").GetProperty("awards").EnumerateArray();
                //foreach (var item in arr)
                //{
                //    string period = item.GetProperty("issue").GetString()!;
                //    string num = item.GetProperty("code").GetString()!;
                //    DateTime openTime = DateTime.Parse(item.GetProperty("opendate").GetString()!);

                //    var exist = await db.LotteryDatas
                //        .FirstOrDefaultAsync(d => d.LotteryId == lottery.Id && d.PeriodNo == period, token);
                //    if (exist == null)
                //    {
                //        await db.LotteryDatas.AddAsync(new LotteryData
                //        {
                //            LotteryId = lottery.Id,
                //            PeriodNo = period,
                //            OpenNumber = num,
                //            IsOpen = 1,
                //            OpenTime = openTime,
                //            EndTime = openTime.AddHours(24),
                //            CreateTime = DateTime.Now
                //        }, token);
                //        Log.Information($"新增 {lottery.LotteryName} {period} {num}");
                //    }
                //    else
                //    {
                //        exist.OpenNumber = num;
                //        exist.OpenTime = openTime;
                //        exist.EndTime = openTime.AddHours(24);
                //    }
                //}
                //await db.SaveChangesAsync(token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"{lottery.LotteryName} 单彩种拉取失败");
            }
        }
    }
}