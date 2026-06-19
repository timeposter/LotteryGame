using LotteryCore.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LotteryPlay.Pages.Account
{
    public class InfoModel : PageModel
    {
        private readonly AppDBContext _db;
        public InfoModel(AppDBContext db)
        {
            _db = db;
        }

        public string UserName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public DateTime RegisterTime { get; set; }

        public IActionResult OnGet()
        {
            // µÇÂ¼À¹½Ø
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if (!userId.HasValue || string.IsNullOrEmpty(userName))
                return RedirectToPage("/Account/Login");

            var user = _db.Users.Find(userId);
            if (user == null)
                return RedirectToPage("/Account/Login");

            UserName = user.Username;
            Balance = user.Balance;
            RegisterTime = (DateTime)user.CreateTime;

            return Page();
        }
    }
}