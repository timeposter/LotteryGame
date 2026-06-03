using LotteryModels;
using LotteryPlay.Data;
using LotteryPlay.Models;
using LotteryPlay.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LotteryPlay.Pages
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _dbContext;
        public string UserName { get; set; } = string.Empty;
        public decimal Balance { get; set; }

        public IndexModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        #region 页面加载 - 登录校验 + 用户信息
        public async Task<IActionResult> OnGetAsync()
        {
            var userName = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(userName))
            {
                return RedirectToPage("/Account/Login");
            }

            UserName = userName;
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == userName);
            Balance = user?.Balance ?? 0;

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

        #region 原有：期号、开奖记录（保留不变）
        public async Task<IActionResult> OnGetCurrentPeriod(int lotId)
        {
            // 1. 读取彩种配置
            var lotteryConfig = await _dbContext.Lottery.FindAsync(lotId);
            if (lotteryConfig == null || !lotteryConfig.IsEnable)
            {
                return new JsonResult(new { code = 0, msg = "彩种已禁用" });
            }

            int totalSecond = lotteryConfig.PeriodSecond;
            int stopSecond = lotteryConfig.StopBetSecond;

            // 2. 查询当前未开奖的期号
            var current = await _dbContext.LotteryDatas
                .Where(m => m.LotteryId == lotId && m.IsOpen == 0)
                .OrderByDescending(m => m.CreateTime)
                .FirstOrDefaultAsync();

            // 3. 如果没有当期，或者当期已过期，生成新期号
            if (current == null || current.EndTime < DateTime.Now)
            {
                //1.随机开奖5位号码
                Random rd = new Random();
                List<int> open = new List<int>();
                for (int i = 0; i < 5; i++) open.Add(rd.Next(0, 10));
                current.OpenNumber = string.Join(",", open);
                current.OpenTime = DateTime.Now;
                current.IsOpen = 1;
                _dbContext.Update(current);

                //2.当期所有未派奖订单
                var allBets = await _dbContext.UserBets
                    .Where(w => w.LotteryId == lotId && w.PeriodNo == current.PeriodNo && !w.IsWin)
                    .Include(x => x.Play)
                    .ToListAsync();

                var playCfg = await _dbContext.PlayConfig.ToListAsync();
                foreach (var bet in allBets)
                {
                    var p = playCfg.FirstOrDefault(w => w.Id == bet.PlayId);
                    bool win = false;
                    decimal prize = 0;
                    var openNumArr = current.OpenNumber.Split(',').Select(int.Parse).ToList();

                    if (p.PlayName.Contains("直选"))
                    {
                        //直选：拆分前端分位号码，判断是否存在开奖对位
                        var userPos = bet.BetNumber.Split('|');
                        bool ok = true;
                        for (int i = 0; i < 5; i++)
                        {
                            if (!userPos[i].Contains(openNumArr[i].ToString())) { ok = false; break; }
                        }
                        win = ok;
                    }
                    else if (p.PlayName.Contains("组选三"))
                    {
                        //开奖号码取后三位，判断AAB形态
                        var last3 = openNumArr.Skip(2).Take(3).ToList();
                        var userSel = bet.BetNumber.Split(',').Select(int.Parse).ToList();
                        var g = last3.GroupBy(x => x).Select(g => g.Count()).OrderByDescending(x => x).ToList();
                        bool isZu3 = g[0] == 2 && g[1] == 1;
                        if (isZu3 && last3.All(x => userSel.Contains(x))) win = true;
                    }
                    else if (p.PlayName.Contains("组选六"))
                    {
                        var last3 = openNumArr.Skip(2).Take(3).ToList();
                        var userSel = bet.BetNumber.Split(',').Select(int.Parse).ToList();
                        bool isZu6 = last3.Distinct().Count() == 3;
                        if (isZu6 && last3.All(x => userSel.Contains(x))) win = true;
                    }

                    //中奖派奖
                    if (win)
                    {
                        prize = bet.BetMoney * p.BonusAmount;
                        var u = await _dbContext.Users.FindAsync(bet.UserId);
                        u.Balance += prize;
                        bet.IsWin = true;
                        bet.WinMoney = prize;
                    }
                }
                // 把过期的期号标记为已开奖（可选，也可以不处理）
                if (current != null)
                {
                    current.IsOpen = 1;
                    _dbContext.LotteryDatas.Update(current);
                }

                // 生成新期号
                string periodNo = DateTime.Now.ToString("yyyyMMddHHmmss");
                DateTime createTime = DateTime.Now;
                DateTime endTime = createTime.AddSeconds(totalSecond);

                current = new LotteryData
                {
                    LotteryId = lotId,
                    PeriodNo = periodNo,
                    OpenNumber = "",
                    IsOpen = 0,
                    CreateTime = createTime,
                    EndTime = endTime
                };
                _dbContext.LotteryDatas.Add(current);
                var traceList = await _dbContext.UserTrace
    .Where(w => w.LotteryId == lotId && w.LeftCount > 0 && w.Status == 0)
    .ToListAsync();

                foreach (var trace in traceList)
                {
                    UserBet bet = new UserBet()
                    {
                        UserId = trace.UserId,
                        LotteryId = trace.LotteryId,
                        PlayId = trace.PlayId,
                        PeriodNo = current.PeriodNo,
                        BetNumber = trace.BetNumber,
                        Multiple = trace.Multiple,
                        BetMoney = trace.PerMoney,
                        SourceType = 1,
                        TraceId = trace.Id
                    };
                    _dbContext.UserBets.Add(bet);
                    trace.LeftCount--;
                    if (trace.LeftCount <= 0) trace.Status = 1;
                }
                await _dbContext.SaveChangesAsync();
            }

            // 4. 计算剩余时间和投注状态
            TimeSpan leftTime = current.EndTime - DateTime.Now;
            string countTime = leftTime.TotalSeconds > 0
                ? $"{(int)leftTime.TotalMinutes:D2}:{leftTime.Seconds:D2}"
                : "00:00";
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
        public async Task<IActionResult> OnGetLotteryHistory(int lotId)
        {
            var history = await _dbContext.LotteryDatas
                .Where(m => m.LotteryId == lotId && m.IsOpen == 1)
                .OrderByDescending(m => m.CreateTime)
                .Take(10)
                .Select(m => new
                {
                    period = m.PeriodNo,
                    openTime = m.OpenTime.Value.ToString("HH:mm:ss"),
                    openNumber = m.OpenNumber
                })
                .ToListAsync();

            return new JsonResult(new { code = 1, data = history });
        }
        #endregion

        #region 投注接口（改用数据库玩法ID，移除枚举）
        /// <summary>投注提交</summary>
        public async Task<IActionResult> OnPostBet(int lotteryId, int playId, string period, string betNum, int multiple)
        {
            //1.获取登录用户（示例固定用户Id=1，正式换登录获取）
            int uid = 1;
            var user = await _dbContext.Users.FindAsync(uid);
            if (user == null) return new JsonResult(new { code = 0, msg = "用户不存在" });

            //2.校验彩种、玩法、当期
            var lot = await _dbContext.Lottery.FindAsync(lotteryId);
            var play = await _dbContext.PlayConfig.FindAsync(playId);
            var nowPeriod = await _dbContext.LotteryDatas
                .FirstOrDefaultAsync(w => w.LotteryId == lotteryId && w.PeriodNo == period && w.IsOpen == 0);

            if (lot == null || !lot.IsEnable) return new JsonResult(new { code = 0, msg = "彩种异常" });
            if (play == null || !play.IsEnable) return new JsonResult(new { code = 0, msg = "玩法禁用" });
            if (nowPeriod == null || DateTime.Now >= nowPeriod.EndTime.AddSeconds(-lot.StopBetSecond))
                return new JsonResult(new { code = 0, msg = "已截止投注" });

            //3.拆分勾选号码数组
            List<int> nums = betNum.Split(',').Select(int.Parse).ToList();
            int zhuShu = 0;

            //【核心：按玩法计算注数】
            if (play.PlayName.Contains("直选"))
            {
                //直选：5个位置各选若干，排列组合
                //格式规范前端：万,千,百,十,个 分段|分隔 如 01|23|5|6|9
                var posArr = betNum.Split('|');
                int wan = posArr[0].Length;
                int qian = posArr[1].Length;
                int bai = posArr[2].Length;
                int shi = posArr[3].Length;
                int ge = posArr[4].Length;
                zhuShu = wan * qian * bai * shi * ge;
            }
            else if (play.PlayName.Contains("组选三"))
            {
                //组三：从所选N个号码中选3个，其中2同1异 C(n,2)*(n-2)
                int n = nums.Count;
                zhuShu = n * (n - 1) / 2 * (n - 2);
            }
            else if (play.PlayName.Contains("组选六"))
            {
                //组六：C(n,3)=n*(n-1)*(n-2)/6
                int n = nums.Count;
                zhuShu = n * (n - 1) * (n - 2) / 6;
            }

            if (zhuShu <= 0) return new JsonResult(new { code = 0, msg = "号码不足无法投注" });

            //总投注金额
            decimal totalMoney = zhuShu * multiple;
            if (user.Balance < totalMoney) return new JsonResult(new { code = 0, msg = "余额不足" });

            //4.扣余额、新增投注订单
            user.Balance -= totalMoney;
            UserBet bet = new UserBet()
            {
                UserId = uid,
                LotteryId = lotteryId,
                PlayId = playId,
                PeriodNo = period,
                BetNumber = betNum,
                Multiple = multiple,
                BetMoney = totalMoney,
                SourceType = 0, //手动下单
                TraceId = 0
            };
            _dbContext.UserBets.Add(bet);
            await _dbContext.SaveChangesAsync();

            return new JsonResult(new { code = 1, msg = $"投注成功！{zhuShu}注，共{totalMoney}元" });
        }
        #endregion
        /// <summary>添加追号</summary>
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

            //1、预扣全部追号金额
            user.Balance -= totalNeed;

            //2、新增追号计划
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
            _dbContext.SaveChanges(); //先保存拿到Trace.Id

            //3、【关键】当期直接生成第1条投注单（追号来源）
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

            //4、追号剩余期数 -1（已经用掉1期）
            trace.LeftCount -= 1;

            _dbContext.SaveChanges();

            return new JsonResult(new { code = 1, msg = $"追号成功！首期已下单，剩余{trace.LeftCount}期自动追投" });
        }
        //共用算注数方法（和投注共用）
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
                if (playName.Contains("组三"))
                    zhu = n * (n - 1) / 2 * (n - 2);
                else if (playName.Contains("组六"))
                    zhu = n * (n - 1) * (n - 2) / 6;
            }
            return zhu;
        }
        public async Task<JsonResult> OnGetGetHistoryPeriod(int lid)
        {
            var list = await _dbContext.LotteryDatas
                .Where(w => w.LotteryId == lid && w.IsOpen == 1)
                .OrderByDescending(o => o.PeriodNo)
                .Take(20)
                .Select(x => new { x.PeriodNo })
                .ToListAsync();
            return new JsonResult(list);
        }

        public async Task<IActionResult> OnPostAddHistoryTrace(int lid, int pid, string startPer, string betNum, int mul, int traceCount)
        {
            int uid = int.Parse(HttpContext.Session.GetString("UserId"));
            var user = await _dbContext.Users.FindAsync(uid);
            var play = await _dbContext.PlayConfig.FindAsync(pid);
            if (user == null) return new JsonResult(new { code = 0, msg = "未登录" });

            //计算单期金额
            int zhu = CalcZhuShu(play.PlayName, betNum);
            decimal perMoney = zhu * mul;
            decimal totalCost = perMoney * traceCount;

            if (user.Balance < totalCost)
                return new JsonResult(new { code = 0, msg = $"余额不足，总共需要{totalCost}元" });

            //一次性预扣全款
            user.Balance -= totalCost;

            //生成追号计划：起始期=勾选历史期，剩余总期数
            UserTrace trace = new UserTrace
            {
                UserId = uid,
                LotteryId = lid,
                PlayId = pid,
                StartPeriod = startPer,
                TotalCount = traceCount,
                LeftCount = traceCount,
                BetNumber = betNum,
                Multiple = mul,
                PerMoney = perMoney,
                Status = 0
            };
            _dbContext.UserTrace.Add(trace);
            await _dbContext.SaveChangesAsync();

            return new JsonResult(new { code = 1, msg = $"追号创建成功！起始期：{startPer}，连续{traceCount}期自动追投" });
        }
    }
}