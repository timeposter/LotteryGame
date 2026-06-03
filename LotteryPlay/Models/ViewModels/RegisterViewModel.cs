using System.ComponentModel.DataAnnotations;

namespace LotteryPlay.Models.ViewModels
{
    public class RegisterViewModel
    {

        [Required(ErrorMessage = "请输入用户名")]
        [StringLength(10, MinimumLength = 2, ErrorMessage = "用户名需2-10位字符")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入登录密码")]
        [StringLength(15, MinimumLength = 6, ErrorMessage = "密码需6-15位")]
        [DataType(DataType.Password)]
        public string LoginPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "请确认登录密码")]
        [Compare("LoginPassword", ErrorMessage = "两次密码不一致")]
        [DataType(DataType.Password)]
        public string ConfirmLoginPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入取款密码")]
        [StringLength(15, MinimumLength = 6, ErrorMessage = "密码需6-15位")]
        [DataType(DataType.Password)]
        public string WithdrawPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "请确认取款密码")]
        [Compare("WithdrawPassword", ErrorMessage = "两次密码不一致")]
        [DataType(DataType.Password)]
        public string ConfirmWithdrawPassword { get; set; } = string.Empty;

        public string? RealName { get; set; }

        public string? WithdrawMethod { get; set; }

        public string? WithdrawAccount { get; set; }

        public string? Province { get; set; }

        public string? City { get; set; }

        public string? QQ { get; set; }

        [Required(ErrorMessage = "请完成验证码计算")]
        public string CaptchaResult { get; set; } = string.Empty;
    }
    public class LoginViewModel
    {
        [Required(ErrorMessage = "请输入用户名")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入密码")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}