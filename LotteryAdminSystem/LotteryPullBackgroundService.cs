using LotteryAdminSystem.Converter;
using LotteryCore.Data;
using LotteryCore.Enetities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LotteryAdminSystem
{
    /// <summary>后台定时拉取远程开奖API数据，写入LotteryDatas表（防停滞/跨天卡死/请求优化）</summary>
    public class LotteryPullBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LotteryPullBackgroundService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        // 全局配置常量
        // 全局配置常量
        private const int GlobalLoopTimeoutSec = 35;
        private const int SingleLotteryTaskTimeoutMs = 15000;
        private const int MaxSyncHistoryAwardCount = 8;
        private const int BatchSaveDbRow = 5;
        // HTTP独立网络超时（必须小于HttpClient总超时）
        private const int HttpSingleTimeoutSec = 12;

        public LotteryPullBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<LotteryPullBackgroundService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }
        public override Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("拉取服务初始化完成，后台线程启动");
            return base.StartAsync(cancellationToken);
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 延迟200ms释放主线程，避免占用服务启动窗口
            await Task.Delay(200, stoppingToken);
            _logger.LogInformation("开奖数据后台拉取常驻服务已启动，全局超时{0}s，单彩种熔断{1}ms", GlobalLoopTimeoutSec, SingleLotteryTaskTimeoutMs);
            Random random = new Random();

            while (!stoppingToken.IsCancellationRequested)
            {
                long loopStartTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                try
                {
                    using var globalCts = new CancellationTokenSource(TimeSpan.FromSeconds(GlobalLoopTimeoutSec));
                    using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token, stoppingToken);
                    await RunPullJobLoopAsync(combinedToken.Token);
                }
                catch (OperationCanceledException ex)
                {
                    if (stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("程序收到停止信号，终止本轮拉取任务");
                    }
                    else
                    {
                        _logger.LogWarning("本轮拉取任务全局超时终止，跳过本轮，异常：{0}", ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "单轮拉取任务全局异常，本轮跳过，等待下一轮重试");
                }

                long loopCostMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - loopStartTime;
                int sleepMs = 4000 + random.Next(0, 2000);
                _logger.LogDebug("本轮拉取耗时{0}ms，休眠{1}ms后执行下一轮", loopCostMs, sleepMs);
                await Task.Delay(sleepMs, stoppingToken);
            }

            _logger.LogInformation("开奖数据后台拉取服务正常停止");
        }

        /// <summary>遍历所有启用同步的彩种批量执行抓取</summary>
        private async Task RunPullJobLoopAsync(CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDBContext>();

            var enableLotteryList = await db.Lottery
                .Where(l => l.IsEnable && l.Id == 1)
                .ToListAsync(token);

            if (enableLotteryList.Count == 0)
                return;

            foreach (var lotteryItem in enableLotteryList)
            {
                using var singleCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(SingleLotteryTaskTimeoutMs));
                using var singleCombineToken = CancellationTokenSource.CreateLinkedTokenSource(singleCts.Token, token);
                try
                {
                    string apiCode = "hn300";
                    await ProcessSingleLotteryRemotePullAsync(lotteryItem, apiCode, db, singleCombineToken.Token);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("彩种[{0}]拉取超时熔断，跳过该彩种", lotteryItem.LotteryName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "彩种[{0}]拉取业务异常，跳过该彩种", lotteryItem.LotteryName);
                }
            }
        }

        /// <summary>单个彩种：请求远程API + 解析JSON + 入库LotteryData</summary>
        private async Task ProcessSingleLotteryRemotePullAsync(
            Lottery lottery, string lotteryApiCode, AppDBContext db, CancellationToken token)
        {
            var apiResult = await RequestRemoteLotteryApiAsync(lotteryApiCode, token);
            if (apiResult == null || apiResult.code != 0)
            {
                _logger.LogWarning("彩种[{0}]API返回异常，code={1},msg={2}",
                    lottery.LotteryName, apiResult?.code, apiResult?.msg);
                return;
            }

            var apiData = apiResult.data;
            var lotteryInfo = apiData.lottery;

            if (lotteryInfo.status != "open")
            {
                _logger.LogInformation("彩种[{0}]接口状态非open，暂时跳过", lottery.LotteryName);
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
                _logger.LogInformation("【{0}】新增待开奖期号：{1}", lottery.LotteryName, currentIssueNo);
            }
            else
            {
                currentIssueEntity.EndTime = endBetTime;
                currentIssueEntity.OpenTime = currentOpenTime;
            }

            // 分批同步历史开奖，避免一次性锁表超时
            var syncAwardList = apiData.awards.OrderByDescending(o=>o.opendate).Take(MaxSyncHistoryAwardCount).ToList();
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
                    _logger.LogInformation("【{0}】期号{1}开奖完成，开奖号码：{2}",
                        lottery.LotteryName, currentIssueNo, matchAward.code);
                }
            }
        }

        /// <summary>请求远程开奖接口，隔离HTTP超时与业务超时，精准区分取消类型</summary>
        private async Task<LotteryApiResult?> RequestRemoteLotteryApiAsync(string code, CancellationToken businessToken)
        {
            var client = _httpClientFactory.CreateClient("LotteryApiClient");
            string apiUrl = $"https://www.manycailm.com/api/lottery/awards?code={code}";

            // 1. 独立HTTP超时令牌，仅控制网络请求
            using var httpTimeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(HttpSingleTimeoutSec));
            // 2. 组合令牌：网络超时 或 上层业务超时 任一触发都取消请求
            using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(httpTimeoutCts.Token, businessToken);

            try
            {
                var response = await client.GetAsync(apiUrl, combinedToken.Token);
                response.EnsureSuccessStatusCode();

                string jsonText = await response.Content.ReadAsStringAsync(combinedToken.Token);
                _logger.LogDebug("第三方开奖接口返回原始JSON：{0}", jsonText);

                var opt = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringConverter(), new JsonNullableDateTimeConverter() }
                };
                var result = JsonSerializer.Deserialize<LotteryApiResult>(jsonText, opt);
                return result;
            }
            catch (OperationCanceledException)
            {
                // 精准判断是哪种取消触发
                if (httpTimeoutCts.IsCancellationRequested)
                {
                    _logger.LogWarning("【网络超时】API {0} 请求超过{1}秒无响应，自动放弃本次拉取", code, HttpSingleTimeoutSec);
                }
                else if (businessToken.IsCancellationRequested)
                {
                    _logger.LogWarning("【业务超时】彩种处理总时长超限，终止API请求 {0}", code);
                }
                return null;
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "【网络异常】请求开奖API {0} 连接失败/403拦截", code);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "【未知异常】解析开奖API {0} 数据失败", code);
                return null;
            }
        }
    }

    #region 接口返回DTO
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

        [JsonIgnore]
        public string NowIssueStr => now_issue?.ToString() ?? string.Empty;
        [JsonIgnore]
        public string LastIssueStr => last_issue?.ToString() ?? string.Empty;
        [JsonIgnore]
        public string NextIssueStr => next_issue?.ToString() ?? string.Empty;
    }

    public class LotteryAwardItem
    {
        public string issue { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string opendate { get; set; } = string.Empty;
    }
    #endregion
}