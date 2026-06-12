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
            // ========== 1. 登录校验 ==========
            var userIdStr = HttpContext.Session.GetString("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId) || userId <= 0)
            {
                // 未登录，跳转到登录页
                return RedirectToPage("/Account/Login");
            }
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