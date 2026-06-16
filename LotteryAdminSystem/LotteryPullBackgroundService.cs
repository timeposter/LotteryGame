using LotteryAdminSystem.Converter;
using LotteryAdminSystem.Data;
using LotteryModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace LotteryAdminSystem
{
    /// <summary>后台定时拉取远程开奖API数据，写入LotteryDatas表</summary>
    public class LotteryPullBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LotteryPullBackgroundService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public LotteryPullBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<LotteryPullBackgroundService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("开奖数据后台拉取常驻服务已启动");

            // 循环轮询，5秒执行一次一轮抓取
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await RunPullJobLoopAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "单轮拉取任务全局异常，本轮跳过，等待下一轮重试");
                }

                await Task.Delay(5000, stoppingToken);
            }

            _logger.LogInformation("开奖数据后台拉取服务正常停止");
        }

        /// <summary>遍历所有启用同步的彩种批量执行抓取</summary>
        private async Task RunPullJobLoopAsync(CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 只处理后台开启同步的彩种 IsEnable=true
            var enableLotteryList = await db.Lottery
                .Where(l => l.IsEnable&&l.Id==1)
                .ToListAsync(token);

            if (enableLotteryList.Count == 0)
                return;

            foreach (var lotteryItem in enableLotteryList)
            {
                // 示例：hn300，你后续可在Lottery实体增加LotteryCode字段动态传入
                string apiCode = "hn300";
                await ProcessSingleLotteryRemotePullAsync(lotteryItem, apiCode, db, token);
            }
        }

        /// <summary>单个彩种：请求远程API + 解析JSON + 入库LotteryData</summary>
        private async Task ProcessSingleLotteryRemotePullAsync(
            Lottery lottery, string lotteryApiCode, AppDbContext db, CancellationToken token)
        {
            // 1、请求远程开奖接口，复刻前端 ajax /api/lottery/awards?code=xxx
            var apiResult = await RequestRemoteLotteryApiAsync(lotteryApiCode, token);
            if (apiResult == null || apiResult.code != 0)
            {
                _logger.LogWarning("彩种[{0}]API返回异常，code={1},msg={2}",
                    lottery.LotteryName, apiResult?.code, apiResult?.msg);
                return;
            }

            var apiData = apiResult.data;
            var lotteryInfo = apiData.lottery;

            // 接口状态校验：status != open 不处理
            if (lotteryInfo.status != "open")
            {
                _logger.LogInformation("彩种[{0}]接口状态非open，暂时跳过", lottery.LotteryName);
                return;
            }

            DateTime now = DateTime.Now;
            string currentIssueNo = lotteryInfo.NextIssueStr;
            DateTime currentOpenTime =Convert.ToDateTime( lotteryInfo.next_opendate);
            string lastIssueNo = lotteryInfo.LastIssueStr;
            int remainSecond = lotteryInfo.next_open_remaining;

            // 计算开奖时间、投注截止时间
            DateTime openTime = now.AddSeconds(remainSecond);
            DateTime endBetTime = openTime.AddSeconds(-lottery.StopBetSecond);

            // 2、处理【当前待开奖期】
            var currentIssueEntity = await db.LotteryDatas
                .FirstOrDefaultAsync(d => d.LotteryId == lottery.Id && d.PeriodNo == currentIssueNo, token);

            if (currentIssueEntity == null)
            {
                // 新增待开奖期号
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
                _logger.LogInformation("【{0}】新增待开奖期号：{1}", lottery.LotteryName, currentIssueNo);
            }
            else
            {
                // 更新最新截止时间
                currentIssueEntity.EndTime = endBetTime;
            }

            // 3、批量同步历史已开奖记录
            foreach (var awardItem in apiData.awards)
            {
                var existAward = await db.LotteryDatas
                    .FirstOrDefaultAsync(d => d.LotteryId == lottery.Id && d.PeriodNo == awardItem.issue, token);

                if (existAward == null)
                {
                    existAward = new LotteryData
                    {
                        LotteryId = lottery.Id,
                        PeriodNo = awardItem.issue,
                        OpenNumber = awardItem.code,
                        EndTime = now,
                        OpenTime =Convert.ToDateTime( awardItem.opendate),
                        IsOpen = 1,
                        CreateTime = now
                    };
                    db.LotteryDatas.Add(existAward);
                }
                else
                {
                    existAward.OpenNumber = awardItem.code;
                    existAward.OpenTime = Convert.ToDateTime(awardItem.opendate);
                    existAward.IsOpen = 1;
                }
            }

            // 4、剩余倒计时≤0，标记当期正式开奖
            if (remainSecond <= 0 && currentIssueEntity.IsOpen == 0)
            {
                var matchAward = apiData.awards.Find(x => x.issue == currentIssueNo);
                if (matchAward != null)
                {
                    currentIssueEntity.OpenNumber = matchAward.code;
                    currentIssueEntity.OpenTime = now;
                    currentIssueEntity.IsOpen = 1;
                    _logger.LogInformation("【{0}】期号{1}开奖完成，开奖号码：{2}",
                        lottery.LotteryName, currentIssueNo, matchAward.code);

                    // 可在这里追加：开奖后自动结算投注订单、派奖、资金流水逻辑
                    // await SettleBetOrderAsync(lottery.Id, currentIssueEntity.Id, db, token);
                }
            }

            await db.SaveChangesAsync(token);
        }

        /// <summary>复刻前端AJAX，GET请求远程开奖接口</summary>
        private async Task<LotteryApiResult?> RequestRemoteLotteryApiAsync(string code, CancellationToken token)
        {
            try
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (msg, cert, chain, errors) => true,
                    UseCookies = true,
                    CookieContainer = new System.Net.CookieContainer()
                };
                var client = new HttpClient(handler);

                // 模拟浏览器头，规避CDN拦截
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                client.DefaultRequestHeaders.Referrer = new Uri("https://www.manycailm.com/");

                string apiUrl = $"https://www.manycailm.com/api/lottery/awards?code={code}";
                client.Timeout = TimeSpan.FromSeconds(10);

                var response = await client.GetAsync(apiUrl, token);
                response.EnsureSuccessStatusCode();

                string jsonText = await response.Content.ReadAsStringAsync(token);
                // 打印原始JSON调试
                _logger.LogDebug("第三方开奖接口返回原始JSON：{0}", jsonText);

                // 配置自动数字转字符串转换器
                var opt = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringConverter(),new JsonNullableDateTimeConverter() }
                };
                var result = JsonSerializer.Deserialize<LotteryApiResult>(jsonText, opt);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "请求第三方开奖API code={0} 异常", code);
                return null;
            }
        }
    }

    #region 接口返回DTO（和前端JSON结构一一对应，放在同一个文件里无需额外新建）
    /// <summary>API外层返回体</summary>
    public class LotteryApiResult
    {
        public int code { get; set; }
        public string msg { get; set; } = string.Empty;
        public LotteryApiData data { get; set; } = new LotteryApiData();
    }

    public class LotteryApiData
    {
        public LotteryInfoItem lottery { get; set; } = new LotteryInfoItem();
        public List<LotteryAwardItem> awards { get; set; } = new List<LotteryAwardItem>();
    }

    public class LotteryInfoItem
    {
        public object? now_issue { get; set; } = string.Empty;
        public object? last_issue { get; set; } = string.Empty;
        public object next_issue { get; set; } = string.Empty;
        public int next_open_remaining { get; set; }
        public string status { get; set; } = string.Empty;
        public string next_opendate { get; set; } = string.Empty;

        // 扩展只读属性，直接取出字符串
        [JsonIgnore]
        public string NowIssueStr => now_issue?.ToString() ?? string.Empty;
        [JsonIgnore]
        public string LastIssueStr => last_issue?.ToString() ?? string.Empty;
        [JsonIgnore]
        public string NextIssueStr => next_issue?.ToString() ?? string.Empty;
    }

    public class LotteryAwardItem
    {
        /// <summary>
        /// 期号
        /// </summary>
        public string issue { get; set; } = string.Empty;
        /// <summary>
        ///  开奖号码
        /// </summary>
        public string code { get; set; } = string.Empty;
        /// <summary>
        ///  开奖时间
        /// </summary>
        public string opendate { get; set; }=string.Empty;
    }
    #endregion
}