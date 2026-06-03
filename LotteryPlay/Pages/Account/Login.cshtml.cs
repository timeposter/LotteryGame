using LotteryPlay.Data;
using LotteryPlay.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LotteryPlay.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _context;

        public LoginModel(AppDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public LoginViewModel Input { get; set; } = new();

        public IActionResult OnGet()
        {
            // 已登录直接跳首页
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
            {
                return RedirectToPage("/Index");
            }
            return Page();
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == Input.Username);

            // 检查用户是否存在 + 密码是否匹配
            if (user == null || !LotteryPlay.PasswordHelper.Verify(Input.Password, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "用户名或密码错误");
                return Page();
            }

            // 写入 Session，登录态生效
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetInt32("UserId", user.Id);

            // 登录成功，强制跳转到首页
            return RedirectToPage("/Index");
        }
    }
}