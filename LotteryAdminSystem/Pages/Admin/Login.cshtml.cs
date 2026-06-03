using LotteryAdminSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace LotteryAdminSystem.Pages.Admin
{
    public class LoginModel : PageModel
    {
        private readonly AppDbContext _db;
        public LoginModel(AppDbContext db)
        {
            _db = db;
        }

        [BindProperty]
        public AdminLoginViewModel Input { get; set; } = new();

        public IActionResult OnGet()
        {
            // 已登录直接跳后台首页
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("AdminName")))
            {
                return RedirectToPage("/Users/Index");
            }
            return Page();
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
                return Page();

            var pwdHash = HashPwd(Input.Password);
            var admin = _db.Admins.FirstOrDefault(a =>
                a.AdminName == Input.AdminName && a.PasswordHash == pwdHash);

            if (admin == null)
            {
                ModelState.AddModelError("", "账号或密码错误");
                return Page();
            }

            // 写入Session 标记登录
            HttpContext.Session.SetString("AdminName", admin.AdminName);
            return RedirectToPage("/Users/Index");
        }

        private string HashPwd(string pwd)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(pwd);
            return Convert.ToBase64String(sha.ComputeHash(bytes));
        }
    }

    public class AdminLoginViewModel
    {
        [Required(ErrorMessage = "请输入管理员账号")]
        public string AdminName { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入密码")]
        public string Password { get; set; } = string.Empty;
    }
}