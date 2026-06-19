using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LotteryCore.Enetities;
namespace LotteryAdminSystem.Pages.Users
{
    public class EditModel : PageModel
    {
        private readonly AppDBContext _db;
        public EditModel(AppDBContext db)
        {
            _db = db;
        }

        [BindProperty]
        public User Input { get; set; } = new();

        public IActionResult OnGet(int id)
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminName")))
                return RedirectToPage("/Admin/Login");

            var user = _db.Users.Find(id);
            if (user == null) return NotFound();

            Input = user;
            return Page();
        }

        public IActionResult OnPost()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("AdminName")))
                return RedirectToPage("/Admins/Login");

            if (!ModelState.IsValid)
                return Page();

            _db.Users.Update(Input);
            _db.SaveChanges();
            return RedirectToPage("Index");
        }
    }
}