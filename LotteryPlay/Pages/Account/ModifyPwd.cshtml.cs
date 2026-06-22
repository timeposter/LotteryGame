using LotteryCore.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace LotteryPlay.Pages.Account
{
    public class ModifyPwdModel : PageModel
    {
        private readonly AppDBContext _db;
        public ModifyPwdModel(AppDBContext db)
        {
            _db = db;
        }

        #region 登录密码字段
        [BindProperty]
        public string LoginOldPwd { get; set; } = string.Empty;
        [BindProperty]
        public string LoginNewPwd { get; set; } = string.Empty;
        [BindProperty]
        public string LoginRePwd { get; set; } = string.Empty;

        public string LoginMsg { get; set; } = string.Empty;
        public string LoginMsgColor { get; set; } = "#333";
        #endregion

        #region 提款密码字段
        [BindProperty]
        public string WithdrawOldPwd { get; set; } = string.Empty;
        [BindProperty]
        public string WithdrawNewPwd { get; set; } = string.Empty;
        [BindProperty]
        public string WithdrawRePwd { get; set; } = string.Empty;

        public string WithdrawMsg { get; set; } = string.Empty;
        public string WithdrawMsgColor { get; set; } = "#333";
        #endregion

        public IActionResult OnGet()
        { // ========== 1. 登录校验 ==========
            var userIdStr = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if ((!userIdStr.HasValue) || userIdStr <= 0)
            {
                // 未登录，跳转到登录页
                return RedirectToPage("/Account/Login");
            }
            return Page();
        }

        /// <summary>提交修改 登录密码</summary>
        public async Task<IActionResult> OnPostLoginPwdAsync()
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
            if (user == null)
            {
                LoginMsg = "账号异常，请重新登录";
                LoginMsgColor = "red";
                return Page();
            }

            // 校验原密码哈希
            if (!PasswordHelper.Verify(LoginOldPwd, user.PasswordHash))
            {
                LoginMsg = "原登录密码错误";
                LoginMsgColor = "red";
                return Page();
            }
            if (LoginNewPwd != LoginRePwd)
            {
                LoginMsg = "两次新密码输入不一致";
                LoginMsgColor = "red";
                return Page();
            }
            if (LoginNewPwd.Length < 6)
            {
                LoginMsg = "密码长度不能少于6位";
                LoginMsgColor = "red";
                return Page();
            }

            // 更新登录密码哈希
            user.PasswordHash = PasswordHelper.CreateHash(LoginNewPwd);
            await _db.SaveChangesAsync();

            LoginMsg = "登录密码修改成功！";
            LoginMsgColor = "green";
            return Page();
        }

        /// <summary>提交修改 提款密码</summary>
        public async Task<IActionResult> OnPostWithdrawPwdAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
                return RedirectToPage("/Account/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                WithdrawMsg = "账号异常，请重新登录";
                WithdrawMsgColor = "red";
                return Page();
            }

            // 校验原提款密码哈希
            if (!PasswordHelper.Verify(WithdrawOldPwd, user.WithdrawPasswordHash))
            {
                WithdrawMsg = "原提款密码错误";
                WithdrawMsgColor = "red";
                return Page();
            }
            if (WithdrawNewPwd != WithdrawRePwd)
            {
                WithdrawMsg = "两次新密码输入不一致";
                WithdrawMsgColor = "red";
                return Page();
            }
            if (WithdrawNewPwd.Length < 6)
            {
                WithdrawMsg = "密码长度不能少于6位";
                WithdrawMsgColor = "red";
                return Page();
            }

            // 更新提款密码哈希
            user.WithdrawPasswordHash = PasswordHelper.CreateHash(WithdrawNewPwd);
            await _db.SaveChangesAsync();

            WithdrawMsg = "提款密码修改成功！";
            WithdrawMsgColor = "green";
            return Page();
        }
    }
}