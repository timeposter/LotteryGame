using LotteryAdminSystem.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

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
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("AdminName")))
                return RedirectToPage("/Users/Index");
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            // 异步查询数据库
            var admin = await _db.Admins.FirstOrDefaultAsync(a => a.AdminName == Input.AdminName);
            if (admin == null)
            {
                ModelState.AddModelError("", "账号或密码错误");
                return Page();
            }

            var hasher = new PasswordHasher<LotteryModels.Admins>();
            var verify = hasher.VerifyHashedPassword(admin, admin.PasswordHash, Input.Password);
            if (verify != PasswordVerificationResult.Success)
            {
                ModelState.AddModelError("", "账号或密码错误");
                return Page();
            }

            // 可选：保留Session存储
            HttpContext.Session.SetString("AdminName", admin.AdminName);

            // 异步签发登录Cookie，适配 [Authorize] 授权校验
            List<Claim> claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, admin.AdminName)
    };
            ClaimsIdentity identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            await HttpContext.SignInAsync(principal);

            return RedirectToPage("/Index");
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