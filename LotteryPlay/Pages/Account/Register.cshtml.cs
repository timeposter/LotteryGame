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

        // 打开页面初始化验证码
        public IActionResult OnGet()
        {
            CreateCaptcha();
            return Page();
        }

        /// <summary>
        /// 生成加法验证码，写入Session
        /// </summary>
        private void CreateCaptcha()
        {
            CaptchaNum1 = Random.Shared.Next(10, 50);
            CaptchaNum2 = Random.Shared.Next(10, 50);
            int total = CaptchaNum1 + CaptchaNum2;
            HttpContext.Session.SetInt32("CaptchaResult", total);
        }

        public IActionResult OnPost()
        {
            // 1. 基础模型校验（用户名、密码格式等DataAnnotation）
            if (!ModelState.IsValid)
            {
                CreateCaptcha();
                return Page();
            }

            // 2. 验证码校验
            var sessionCode = HttpContext.Session.GetInt32("CaptchaResult");
            // Session无值直接报错
            if (!sessionCode.HasValue)
            {
                ModelState.AddModelError(string.Empty, "验证码已过期，请刷新页面");
                CreateCaptcha();
                return Page();
            }

            // 前端输入转数字对比，避免字符串匹配问题
            if (!int.TryParse(Input.CaptchaResult, out int userInputCode) || userInputCode != sessionCode.Value)
            {
                ModelState.AddModelError(string.Empty, "验证码计算错误");
                CreateCaptcha();
                return Page();
            }

            // 3. 后端强制校验两次密码一致（防止前端JS被禁用绕过）
            if (Input.LoginPassword != Input.LoginPasswordConfirm)
            {
                ModelState.AddModelError(string.Empty, "两次登录密码输入不一致");
                CreateCaptcha();
                return Page();
            }
            if (Input.WithdrawPassword != Input.WithdrawPasswordConfirm)
            {
                ModelState.AddModelError(string.Empty, "两次提款密码输入不一致");
                CreateCaptcha();
                return Page();
            }

            // 4. 用户名唯一性校验
            if (_context.Users.Any(u => u.Username == Input.Username))
            {
                ModelState.AddModelError(string.Empty, "用户名已存在");
                CreateCaptcha();
                return Page();
            }

            // 5. 创建用户入库
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

            // 注册成功跳转登录
            return RedirectToPage("./Login");
        }

        // 备用哈希方法（你代码里没使用，可保留或删除）
        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hashBytes = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hashBytes);
        }
    }
}