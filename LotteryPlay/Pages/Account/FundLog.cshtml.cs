using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using LotteryPlay.Data;
using LotteryPlay.Models;
using LotteryModels;

namespace LotteryPlay.Pages.Account
{
    public class FundLogModel : PageModel
    {
        private readonly AppDbContext _db;
        public FundLogModel(AppDbContext db)
        {
            _db = db;
        }

        public decimal Balance { get; set; }
        public List<UserFundLog> LogList { get; set; } = new List<UserFundLog>();

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if (!userId.HasValue || string.IsNullOrEmpty(userName))
                return RedirectToPage("/Account/Login");

            var user = await _db.Users.FindAsync(userId);
            Balance = user?.Balance ?? 0;

            // 查询当前用户流水，倒序
            LogList = await _db.UserFundLogs
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreateTime)
                .ToListAsync();

            return Page();
        }
    }
}