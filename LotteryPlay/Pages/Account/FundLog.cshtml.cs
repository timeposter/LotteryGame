using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using LotteryCore.Enetities;
using LotteryCore.Data;

namespace LotteryPlay.Pages.Account
{
    public class FundLogModel : PageModel
    {
        private readonly AppDBContext _db;
        public FundLogModel(AppDBContext db)
        {
            _db = db;
        }

        public decimal Balance { get; set; }
        public List<UserFundLog> LogList { get; set; } = new List<UserFundLog>();

        public async Task<IActionResult> OnGetAsync()
        {
            // ========== 1. 登录校验 ==========
            var userIdStr = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if ((!userIdStr.HasValue) || userIdStr <= 0)
            {
                // 未登录，跳转到登录页
                return RedirectToPage("/Account/Login");
            }
            var user = await _db.Users.FindAsync(userIdStr);
            Balance = user?.Balance ?? 0;

            // 查询当前用户流水，倒序
            LogList = await _db.UserFundLogs
                .Where(x => x.UserId == userIdStr)
                .OrderByDescending(x => x.CreateTime)
                .ToListAsync();

            return Page();
        }
    }
}