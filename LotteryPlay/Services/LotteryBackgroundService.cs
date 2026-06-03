using LotteryModels;
using LotteryPlay.Data;
using LotteryPlay.Hubs;
using LotteryPlay.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;


namespace LotteryPlay.Services
{
    public class LotteryBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<LotteryHub> _hubContext;

        public LotteryBackgroundService(IServiceScopeFactory scopeFactory, IHubContext<LotteryHub> hubContext)
        {
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("彩票后台服务已启动...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var now = DateTime.Now;

                    var categories = await db.LotteryCategories
                        .Where(c => c.IsEnable)
                        .ToListAsync(stoppingToken);

                    foreach (var cat in categories)
                    {
                        var lotType = cat.LotteryId;
                        var periodData = await db.LotteryDatas
                            .FirstOrDefaultAsync(d => d.LotteryId == lotType && d.IsOpen == 0, stoppingToken);

                        // 无当期期号，自动创建新期
                        if (periodData == null)
                        {
                            await CreateNewPeriod(db, cat, stoppingToken);
                            continue;
                        }

                        // 计算剩余时间（毫秒 + 向上取整秒数，解决跳秒）
                        TimeSpan leftTime = periodData.EndTime - now;
                        double leftTotalMs = leftTime.TotalMilliseconds;
                        int leftSecond = (int)Math.Ceiling(leftTotalMs / 1000);
                        bool canBet = leftSecond > 0;

                        // 推送：彩种、期号、剩余毫秒、剩余秒数、可投注状态
                        await _hubContext.Clients.All.SendAsync(
                            "OnTick",
                            new
                            {
                                lotteryType = (int)lotType,
                                period = periodData.PeriodNo,
                                leftMs = leftTotalMs,
                                leftSecond = leftSecond < 0 ? 0 : leftSecond,
                                canBet = canBet
                            },
                            stoppingToken);

                        // 到达开奖时间，执行开奖、派奖、生成下期
                        if (now >= periodData.OpenTime && periodData.IsOpen == 0)
                        {
                            periodData.OpenNumber = LotteryRuleService.GenerateOpenNumber();
                            periodData.IsOpen = 1;
                            await db.SaveChangesAsync(stoppingToken);

                            // 推送开奖结果
                            await _hubContext.Clients.All.SendAsync(
                                "OnOpenResult",
                                new
                                {
                                    lotteryType = lotType,
                                    period = periodData.PeriodNo,
                                    openNumber = periodData.OpenNumber
                                },
                                stoppingToken);

                            // 结算所有投注
                            await SettleAllBet(db, lotType, periodData.PeriodNo, periodData.OpenNumber!, stoppingToken);
                            // 创建下一期
                            await CreateNewPeriod(db, cat, stoppingToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"后台服务异常：{ex.Message}");
                }

                // 1秒轮询一次
                await Task.Delay(1000, stoppingToken);
            }
        }

        /// <summary>
        /// 创建新期号
        /// </summary>
        private static async Task CreateNewPeriod(AppDbContext db, LotteryCategory cat, CancellationToken ct)
        {
            var lotteryId = cat.LotteryId;
            var last = await db.LotteryDatas
                .Where(d => d.LotteryId == lotteryId)
                .OrderByDescending(d => d.Id)
                .FirstOrDefaultAsync(ct);

            string newPeriod;
            string dateStr = DateTime.Now.ToString("yyyyMMdd");

            if (last == null || last.PeriodNo.Length < 8)
            {
                newPeriod = $"{dateStr}001";
            }
            else
            {
                string numStr = last.PeriodNo.Substring(8);
                if (int.TryParse(numStr, out int num))
                {
                    newPeriod = $"{dateStr}{(num + 1).ToString("D3")}";
                }
                else
                {
                    newPeriod = $"{dateStr}001";
                }
            }

            var endTime = DateTime.Now.AddSeconds(cat.PeriodSecond - cat.StopBetSecond);
            var openTime = DateTime.Now.AddSeconds(cat.PeriodSecond);

            var newData = new LotteryData
            {
                LotteryId = lotteryId,
                PeriodNo = newPeriod,
                EndTime = endTime,
                OpenTime = openTime,
                IsOpen = 0
            };

            db.LotteryDatas.Add(newData);
            await db.SaveChangesAsync(ct);
            Console.WriteLine($"新期号创建成功：{newPeriod}");
        }

        /// <summary>
        /// 结算当期所有投注
        /// </summary>
        private static async Task SettleAllBet(AppDbContext db, int lotteryId, string period, string openNum, CancellationToken ct)
        {
            var bets = await db.UserBets
                .Where(b => b.LotteryId == lotteryId && b.Period == period && !b.IsSettled)
                .ToListAsync(ct);

            foreach (var bet in bets)
            {
                bool win = LotteryRuleService.CheckWin(openNum, bet.BetNumber, bet.PlayId);
                decimal winMoney = 0;

                if (win)
                {
                    decimal unit = LotteryRuleService.GetUnitBonus(bet.PlayId);
                    winMoney = unit * bet.Multiple;

                    var user = await db.Users.FindAsync(new object[] { bet.UserId }, ct);
                    if (user != null)
                    {
                        var oldBal = user.Balance;
                        user.Balance += winMoney;
                        db.UserFundLogs.Add(new UserFundLog
                        {
                            UserId = user.Id,
                            UserName = user.Username,
                            Type = 4,
                            Money = winMoney,
                            BeforeBalance = oldBal,
                            AfterBalance = user.Balance,
                            Remark = $"[{lotteryId}]期号{period} 中奖派奖"
                        });
                    }
                }

                bet.IsWin = win;
                bet.WinMoney = winMoney;
                bet.IsSettled = true;
            }

            await db.SaveChangesAsync(ct);
        }
    }
}