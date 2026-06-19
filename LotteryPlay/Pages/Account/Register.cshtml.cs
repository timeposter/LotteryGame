using LotteryCore.Data;
using LotteryCore.Enetities;
using LotteryPlay.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Cryptography;
using System.Text;

namespace LotteryPlay.Pages.Account
{
    public class RegisterModel : PageModel
    {
       
            private readonly AppDBContext _context;

            public RegisterModel(AppDBContext context)
            {
                _context = context;
            }

            [BindProperty]
            public RegisterViewModel Input { get; set; } = new();

            public int CaptchaNum1 { get; set; }
            public int CaptchaNum2 { get; set; }

            public IActionResult OnGet()
            {
                // 生成简单加法验证码
               CreateCaptcha();
                return Page();
            }
        /// <summary>
        /// 生成验证码并写入Session
        /// </summary>
        private void CreateCaptcha()
        {
            CaptchaNum1 = Random.Shared.Next(10, 50);
            CaptchaNum2 = Random.Shared.Next(10, 50);
            HttpContext.Session.SetInt32("CaptchaResult", CaptchaNum1 + CaptchaNum2);
        }
        public IActionResult OnPost()
            {
                // 验证验证码
                var correctResult = HttpContext.Session.GetInt32("CaptchaResult");
                if (!ModelState.IsValid || !correctResult.HasValue || Input.CaptchaResult != correctResult.ToString())
                {
                    ModelState.AddModelError(string.Empty, "验证码计算错误");
                    // 刷新验证码
                    CaptchaNum1 = Random.Shared.Next(10, 50);
                    CaptchaNum2 = Random.Shared.Next(10, 50);
                    HttpContext.Session.SetInt32("CaptchaResult", CaptchaNum1 + CaptchaNum2);
                    return Page();
                }

                // 检查用户名是否存在
                if (_context.Users.Any(u => u.Username == Input.Username))
                {
                    ModelState.AddModelError(string.Empty, "用户名已存在");
                    CaptchaNum1 = Random.Shared.Next(10, 50);
                    CaptchaNum2 = Random.Shared.Next(10, 50);
                    HttpContext.Session.SetInt32("CaptchaResult", CaptchaNum1 + CaptchaNum2);
                    return Page();
                }

                // 创建用户并保存密码哈希
                var user = new User
                {
                    Username = Input.Username,
                    PasswordHash = PasswordHelper.CreateHash(Input.LoginPassword),
                    WithdrawPasswordHash = PasswordHelper.CreateHash(Input.WithdrawPassword),
                    RealName = Input.RealName,
                    WithdrawMethod = Input.WithdrawMethod,
                    WithdrawAccount = Input.WithdrawAccount,
                    Province = Input.Province,
                    City = Input.City,
                    QQ = Input.QQ
                };

                _context.Users.Add(user);
                _context.SaveChanges();

                return RedirectToPage("./Login");
            }

            private string HashPassword(string password)
            {
                using var sha256 = SHA256.Create();
                var bytes = Encoding.UTF8.GetBytes(password);
                var hashBytes = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }