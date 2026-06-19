using LotteryCore.Enetities;
using LotteryPlay.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LotteryPlay.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDBContext _dbContext;
        public string UserName { get; set; } = string.Empty;
        public decimal Balance { get; set; }

        public IndexModel(AppDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        #region 页面加载 - 登录校验 + 同步存储 UserId 到 Session
        public async Task<IActionResult> OnGetAsync()
        {
            var userName = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(userName))
            {
                return RedirectToPage("/Account/Login");
            }

            UserName = userName;
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == userName);
            if (user == null)
            {
                HttpContext.Session.Clear();
                return RedirectToPage("/Account/Login");
            }

            Balance = user.Balance;
            HttpContext.Session.SetString("UserId", user.Id.ToString());
            return Page();
        }
        #endregion

        #region 接口：获取所有启用的彩种
        public async Task<JsonResult> OnGetGetLotteryList()
        {
            var list = await _dbContext.Lottery
                .Where(x => x.IsEnable)
                .OrderBy(x => x.Sort)
                .Select(x => new LotteryVm
                {
                    Id = x.Id,
                    LotteryName = x.LotteryName
                })
                .ToListAsync();

            return new JsonResult(new { code = 1, data = list });
        }
        #endregion

        #region 接口：根据彩种ID 获取对应启用玩法
        public async Task<JsonResult> OnGetGetPlayList(int lotteryId)
        {
            var list = await _dbContext.PlayConfig
                .Where(x => x.LotteryId == lotteryId && x.IsEnable)
                .OrderBy(x => x.Sort)
                .Select(x => new PlayConfigVm
                {
                    Id = x.Id,
                    PlayName = x.PlayName,
                    BonusAmount = x.BonusAmount
                })
                .ToListAsync();

            return new JsonResult(new { code = 1, data = list });
        }
        #endregion

        #region 接口：获取当前期号、倒计时、投注状态（纯读库，不再自动开奖/建期）
        public async Task<IActionResult> OnGetCurrentPeriod(int lotId)
        {
            var lotteryConfig = await _dbContext.Lottery.FindAsync(lotId);
            if (lotteryConfig == null || !lotteryConfig.IsEnable)
            {
                return new JsonResult(new { code = 0, msg = "彩种已禁用" });
            }

            int stopSecond = lotteryConfig.StopBetSecond;

            // 只查询：当前最新【未开奖】期（IsOpen = 0）
            var current = await _dbContext.LotteryDatas
                .Where(m => m.LotteryId == lotId && m.IsOpen == 0)
                .OrderByDescending(m => m.CreateTime)
                .FirstOrDefaultAsync();

            // 数据库暂无待开奖期
            if (current == null)
            {
                return new JsonResult(new
                {
                    code = 0,
                    msg = "暂无待开奖期，请管理员维护期号数据"
                });
            }

            // 计算剩余时间、投注状态
            TimeSpan leftTime = current.OpenTime.Value - DateTime.Now;
            string countTime = leftTime.TotalSeconds > 0
                ? $"{(int)leftTime.TotalMinutes:D2}:{leftTime.Seconds:D2}"
                : "00:00";

            // 是否可投注：当前时间 < 截止秒数
            bool canBet = leftTime.TotalSeconds > stopSecond;
            string statusText = canBet ? "投注中" : "已截止";

            return new JsonResult(new
            {
                code = 1,
                period = current.PeriodNo,
                countTime = countTime,
                canBet = canBet,
                status = statusText
            });
        }
        #endregion
        #region 批量投注接口（适配前端投注列表）
        [HttpPost]
        public async Task<IActionResult> OnPostBet(int lotteryId, string period, string betItems)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(userIdStr, out int uid) || uid <= 0)
            {
                return new JsonResult(new { code = 0, msg = "请先登录" });
            }

            if (string.IsNullOrWhiteSpace(period) || string.IsNullOrWhiteSpace(betItems))
            {
                return new JsonResult(new { code = 0, msg = "请求参数不全" });
            }

            List<BetItemDto>? betList = null;
            try
            {
                betList = JsonSerializer.Deserialize<List<BetItemDto>>(betItems);
            }
            catch
            {
                return new JsonResult(new { code = 0, msg = "投注数据格式错误" });
            }

            if (betList == null || !betList.Any())
            {
                return new JsonResult(new { code = 0, msg = "投注列表为空" });
            }

            var lottery = await _dbContext.Lottery.FindAsync(lotteryId);
            var nowPeriod = await _dbContext.LotteryDatas
                .FirstOrDefaultAsync(w => w.LotteryId == lotteryId && w.PeriodNo == period && w.IsOpen == 0);

            if (lottery == null || !lottery.IsEnable)
                return new JsonResult(new { code = 0, msg = "彩种已禁用或不存在" });

            if (nowPeriod == null || DateTime.Now >= nowPeriod.EndTime.AddSeconds(-lottery.StopBetSecond))
                return new JsonResult(new { code = 0, msg = "本期投注已截止" });

            var user = await _dbContext.Users.FindAsync(uid);
            if (user == null)
                return new JsonResult(new { code = 0, msg = "用户信息异常" });

            decimal totalAllMoney = 0;
            int totalAllZhu = 0;
            var userBetList = new List<UserBet>();

            foreach (var item in betList)
            {
                var play = await _dbContext.PlayConfig.FindAsync(item.playId);
                if (play == null || !play.IsEnable)
                    continue;

                int zhuShu = CalcZhuShu(play.PlayName, item.betNum);
                if (zhuShu <= 0)
                    continue;

                decimal singleTotal = zhuShu * item.multiple;
                totalAllMoney += singleTotal;
                totalAllZhu += zhuShu;

                userBetList.Add(new UserBet
                {
                    UserId = uid,
                    LotteryId = lotteryId,
                    PlayId = item.playId,
                    PeriodNo = period,
                    BetNumber = item.betNum,
                    Multiple = item.multiple,
                    BetMoney = singleTotal,
                    SourceType = 0,
                    TraceId = 0
                });
            }

            if (!userBetList.Any())
            {
                return new JsonResult(new { code = 0, msg = "无有效投注号码" });
            }

            if (user.Balance < totalAllMoney)
            {
                return new JsonResult(new { code = 0, msg = $"账户余额不足！本次共需 {totalAllMoney} 元" });
            }

            user.Balance -= totalAllMoney;
            _dbContext.UserBets.AddRange(userBetList);
            await _dbContext.SaveChangesAsync();

            return new JsonResult(new
            {
                code = 1,
                msg = $"投注成功！共计 {totalAllZhu} 注，总金额 {totalAllMoney} 元"
            });
        }
        #endregion

        #region 追号相关接口
        /// <summary>普通追号（当前期开始追号）</summary>
        public async Task<IActionResult> OnPostAddTrace(int lotteryId, int playId, string period, string betNum, int multiple, int traceCount)
        {
            int uid = Convert.ToInt32(HttpContext.Session.GetString("UserId"));
            var user = await _dbContext.Users.FindAsync(uid);
            var lot = await _dbContext.Lottery.FindAsync(lotteryId);
            var play = await _dbContext.PlayConfig.FindAsync(playId);
            var nowPer = await _dbContext.LotteryDatas.FirstOrDefaultAsync(w => w.LotteryId == lotteryId && w.PeriodNo == period && w.IsOpen == 0);

            if (user == null) return new JsonResult(new { code = 0, msg = "未登录" });
            if (nowPer == null || DateTime.Now >= nowPer.EndTime.AddSeconds(-lot.StopBetSecond))
                return new JsonResult(new { code = 0, msg = "已截止无法追号" });

            int zhu = CalcZhuShu(play.PlayName, betNum);
            decimal perMoney = zhu * multiple;
            decimal totalNeed = perMoney * traceCount;

            if (user.Balance < totalNeed)
                return new JsonResult(new { code = 0, msg = $"余额不足，追号共需{totalNeed}元" });

            user.Balance -= totalNeed;

            UserTrace trace = new UserTrace()
            {
                UserId = uid,
                LotteryId = lotteryId,
                PlayId = playId,
                StartPeriod = period,
                LeftCount = traceCount,
                TotalCount = traceCount,
                BetNumber = betNum,
                Multiple = multiple,
                PerMoney = perMoney
            };
            _dbContext.UserTrace.Add(trace);
            await _dbContext.SaveChangesAsync();

            UserBet firstBet = new UserBet()
            {
                UserId = uid,
                LotteryId = lotteryId,
                PlayId = playId,
                PeriodNo = period,
                BetNumber = betNum,
                Multiple = multiple,
                BetMoney = perMoney,
                SourceType = 1,
                TraceId = trace.Id
            };
            _dbContext.UserBets.Add(firstBet);

            trace.LeftCount -= 1;
            await _dbContext.SaveChangesAsync();

            return new JsonResult(new { code = 1, msg = $"追号成功！首期已下单，剩余{trace.LeftCount}期自动追投" });
        }
        public async Task<IActionResult> OnGetLotteryHistory(int lotId)
        {
            var history = await _dbContext.LotteryDatas
                .Where(m => m.LotteryId == lotId && m.IsOpen == 1)
                .OrderByDescending(m => m.CreateTime)
                .Take(10)
                .Select(m => new
                {
                    period = m.PeriodNo,
                    openTime = m.OpenTime.HasValue ? m.OpenTime.Value.ToString("HH:mm:ss") : "",
                    openNumber = m.OpenNumber ?? ""
                })
                .ToListAsync();

            return new JsonResult(new { code = 1, data = history });
        }
        /// <summary>【改造后】获取历史期号 + 投注截止时间（前端列表展示用）</summary>
        public async Task<JsonResult> OnGetGetHistoryPeriod(int lid)
        {
            var lottery = await _dbContext.Lottery.FindAsync(lid);
            int stopSecond = lottery?.StopBetSecond ?? 0;

            var list = await _dbContext.LotteryDatas
                .Where(w => w.LotteryId == lid && w.IsOpen == 1)
                .OrderByDescending(o => o.PeriodNo)
                .Take(20)
                .Select(x => new
                {
                    x.PeriodNo,
                    // 计算真实投注截止时间：期结束时间 - 停售秒数
                    StopTime = x.OpenTime.Value.AddSeconds(-stopSecond)
                })
                .ToListAsync();

            return new JsonResult(list);
        }

        /// <summary>【重构】历史期追号：支持多期号 + 每期独立倍数</summary>
        public async Task<IActionResult> OnPostAddHistoryTrace(int lid, int pid, string betNum, int traceCount, List<string> periodList, List<int> mulList)
        {
            // 登录校验
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out int uid) || uid <= 0)
                return new JsonResult(new { code = 0, msg = "未登录" });

            var user = await _dbContext.Users.FindAsync(uid);
            var play = await _dbContext.PlayConfig.FindAsync(pid);
            var lottery = await _dbContext.Lottery.FindAsync(lid);

            if (user == null || play == null || lottery == null)
                return new JsonResult(new { code = 0, msg = "基础数据异常" });

            // 校验参数：期号、倍数数量必须一致
            if (periodList == null || mulList == null || periodList.Count != mulList.Count || !periodList.Any())
                return new JsonResult(new { code = 0, msg = "请至少选择一条历史期" });

            // 预计算总扣款金额
            decimal totalDeduct = 0;
            var traceAddList = new List<UserTrace>();

            foreach (var item in periodList.Zip(mulList, (p, m) => new { Period = p, Multiple = m }))
            {
                // 校验倍数
                if (item.Multiple < 1) continue;

                // 计算单期金额
                int zhu = CalcZhuShu(play.PlayName, betNum);
                decimal perMoney = zhu * item.Multiple;
                totalDeduct += perMoney * traceCount;

                // 构建追号计划
                traceAddList.Add(new UserTrace
                {
                    UserId = uid,
                    LotteryId = lid,
                    PlayId = pid,
                    StartPeriod = item.Period,
                    TotalCount = traceCount,
                    LeftCount = traceCount,
                    BetNumber = betNum,
                    Multiple = item.Multiple,
                    PerMoney = perMoney,
                    Status = 0
                });
            }

            // 无有效数据
            if (!traceAddList.Any())
                return new JsonResult(new { code = 0, msg = "无有效追号配置，请检查倍数" });

            // 余额校验
            if (user.Balance < totalDeduct)
                return new JsonResult(new { code = 0, msg = $"余额不足，全部追号共需 {totalDeduct:0.00} 元" });

            // 批量新增追号计划 + 扣余额
            user.Balance -= totalDeduct;
            _dbContext.UserTrace.AddRange(traceAddList);
            await _dbContext.SaveChangesAsync();

            return new JsonResult(new
            {
                code = 1,
                msg = $"追号创建成功！共创建 {traceAddList.Count} 条追号计划，连续 {traceCount} 期自动追投"
            });
        }
        #endregion

        #region 共用工具方法：计算注数（直选/组三/组六通用）
        private int CalcZhuShu(string playName, string betNum)
        {
            int zhu = 0;
            if (playName.Contains("直选"))
            {
                var arr = betNum.Split('|');
                zhu = arr[0].Length * arr[1].Length * arr[2].Length * arr[3].Length * arr[4].Length;
            }
            else
            {
                var nums = betNum.Split(',').Select(int.Parse).ToList();
                int n = nums.Count;
                if (playName.Contains("组三") || playName.Contains("组选三"))
                    zhu = n * (n - 1) / 2 * (n - 2);
                else if (playName.Contains("组六") || playName.Contains("组选六"))
                    zhu = n * (n - 1) * (n - 2) / 6;
            }
            return zhu;
        }
        #endregion
        #region 【新增】按规则生成新期号：yyyyMMdd + 3位流水号，每日0点重置001
        /// <summary>
        /// 生成新期号
        /// 规则：每日0点从001开始，每5分钟一期，格式 yyyyMMdd + 3位补零序号
        /// 每个彩种独立计数
        /// </summary>
        /// <param name="lotteryId">彩种ID</param>
        /// <returns>完整期号</returns>
        private async Task<string> GetNewPeriodNo(int lotteryId)
        {
            DateTime now = DateTime.Now;
            string todayDateStr = now.ToString("yyyyMMdd");
            DateTime todayStart = now.Date;

            // 1. 查询当前彩种【今日】所有期号记录
            var todayPeriodList = await _dbContext.LotteryDatas
                .Where(d => d.LotteryId == lotteryId && d.CreateTime >= todayStart)
                .ToListAsync();

            int maxIndex = 0;
            foreach (var item in todayPeriodList)
            {
                string period = item.PeriodNo;

                // ========== 改动1：适配新格式 12位(yyyyMMdd-001) ==========
                if (period.Length == 12)
                {
                    // 格式：20260611-001
                    // 下标：0~7=日期  8=-  9~11=3位流水号
                    string indexStr = period.Substring(9, 3);
                    if (int.TryParse(indexStr, out int idx))
                    {
                        if (idx > maxIndex)
                            maxIndex = idx;
                    }
                }
                // 兼容两种旧格式：时间戳期号、老版纯数字期号，不参与序号计算
            }

            // 2. 新序号 = 最大序号 + 1；今日无数据则从 1 开始
            int newIndex = maxIndex + 1;

            // ========== 改动2：拼接格式 日期-3位流水号 ==========
            string newPeriodNo = $"{todayDateStr}-{newIndex:D3}";

            return newPeriodNo;
        }
        #endregion

        /// <summary>
        /// 获取当前彩种 最新一期开奖结果
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> OnGetLatestOpen(int lotId)
        {
            // 1. 校验彩种ID
            if (lotId <= 0)
            {
                return new JsonResult(new { code = 0, msg = "彩种ID无效", data = (object)null });
            }

            // 2. 查询开奖表：按期号/开奖时间倒序，取最新1条（复用你现有开奖实体）
            var latest = await _dbContext.LotteryDatas
                .Where(x => x.LotteryId == lotId&&x.IsOpen==1)
                .OrderByDescending(x => x.PeriodNo) // 期号倒序（主流排序方式）
                .FirstOrDefaultAsync();

            // 3. 无开奖数据
            if (latest == null)
            {
                return new JsonResult(new { code = 1, msg = "暂无开奖数据", data = (object)null });
            }

            // 4. 组装返回数据（字段和历史开奖接口保持一致）
            var resData = new
            {
                period = latest.PeriodNo,
                openTime = latest.EndTime.ToString("yyyy-MM-dd HH:mm:ss"),
                openNumber = latest.OpenNumber
            };

            return new JsonResult(new { code = 1, msg = "获取成功", data = resData });
        }

        #region 前端投注列表对应实体类
        public class BetItemDto
        {
            public int playId { get; set; }
            public string playName { get; set; } = string.Empty;
            public string betNum { get; set; } = string.Empty;
            public int multiple { get; set; }
            public decimal singleMoney { get; set; }
            public string lineMoney { get; set; } = string.Empty;
        }
        #endregion
    }
}