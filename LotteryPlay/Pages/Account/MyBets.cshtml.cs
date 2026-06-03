using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using LotteryPlay.Data;
using LotteryPlay.Models;
using LotteryModels;

namespace LotteryPlay.Pages.Account
{
    public class MyBetsModel : PageModel
    {
        private readonly AppDbContext _db;
        public MyBetsModel(AppDbContext db)
        {
            _db = db;
        }

        public List<UserBet> Bets { get; set; } = new();
        public Dictionary<string, string> OpenNumbers { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToPage("/Account/Login");

            Bets = await _db.UserBets
                .Where(b => b.UserId == userId)
                .OrderByDescending(b => b.Id)
                .ToListAsync();

            // 取出所有已开奖的期号
            var periods = Bets.Select(b => b.Period).Distinct().ToList();
            var lotteries = await _db.LotteryDatas
                .Where(l => periods.Contains(l.PeriodNo) && l.IsOpen == 1)
                .ToListAsync();

            foreach (var l in lotteries)
            {
                OpenNumbers[l.PeriodNo] = l.OpenNumber ?? "-";
            }

            return Page();
        }
    }
}