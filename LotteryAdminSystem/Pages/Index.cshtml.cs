using LotteryCore.Enetities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LotteryAdminSystem.Pages
{
    [Authorize] // 必须登录才能访问后台
    public class IndexModel : PageModel
    {
        private readonly AppDBContext _db;
        public IndexModel(AppDBContext db)
        {
            _db = db;
        }

        public int TotalUserCount { get; set; }
        public int EnabledLotteryCount { get; set; }
        public int TodayBetCount { get; set; }
        public decimal TotalAmount { get; set; }

        public async Task OnGetAsync()
        {
            // 统计数据，按需自行调整SQL逻辑
            TotalUserCount = await _db.Users.CountAsync();
            EnabledLotteryCount = await _db.LotteryInfos.Where(x => x.IsEnable).CountAsync();
            var today = DateTime.Now.Date;
            TodayBetCount = await _db.BetRecords.Where(x => x.CreateTime.Date == today).CountAsync();
            TotalAmount = await _db.BetRecords.SumAsync(x => x.BetMoney);
        }
    }
}