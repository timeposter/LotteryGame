using LotteryAdminSystem.Data;
using LotteryModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LotteryAdminSystem.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly AppDbContext _db;
        public IndexModel(AppDbContext db)
        {
            _db = db;
        }

        public List<User> UserList { get; set; } = new();

        // µÇÂ¼À¹½Ø
        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminName")))
                return RedirectToPage("/Admin/Login");

            UserList = _db.Users.ToList();
            return Page();
        }

        // É¾³ýÓÃ»§
        public IActionResult OnPostDelete(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminName")))
                return RedirectToPage("/Admin/Login");

            var user = _db.Users.Find(id);
            if (user != null)
            {
                _db.Users.Remove(user);
                _db.SaveChanges();
            }
            return RedirectToPage();
        }
    }
}