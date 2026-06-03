using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LotteryPlay.Data;
using LotteryPlay.Models;
using LotteryModels;

namespace LotteryPlay.Pages.Account
{
    public class RechargeModel : PageModel
    {
        private readonly AppDbContext _db;
        public RechargeModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public decimal Money { get; set; }
        public decimal Balance { get; set; }
        public string Msg { get; set; } = string.Empty;
        public string MsgColor { get; set; } = "#333";

        public async Task<IActionResult> OnGetAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToPage("/Account/Login");

            var user = await _db.Users.FindAsync(userId);
            Balance = user?.Balance ?? 0;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if (!userId.HasValue || string.IsNullOrEmpty(userName))
                return RedirectToPage("/Account/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                Msg = "账号异常";
                MsgColor = "red";
                return Page();
            }

            if (Money <= 0)
            {
                Msg = "充值金额必须大于0";
                MsgColor = "red";
                Balance = user.Balance;
                return Page();
            }

            // 执行充值
            var oldBal = user.Balance;
            user.Balance += Money;

            // 写入流水
            _db.UserFundLogs.Add(new UserFundLog
            {
                UserId = userId.Value,
                UserName = userName,
                Type = 1,
                Money = Money,
                BeforeBalance = oldBal,
                AfterBalance = user.Balance,
                Remark = "账户充值"
            });

            await _db.SaveChangesAsync();
            Msg = "充值成功！";
            MsgColor = "green";
            Balance = user.Balance;
            return Page();
        }
    }
}