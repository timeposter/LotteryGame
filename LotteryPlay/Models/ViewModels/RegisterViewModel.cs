using System.ComponentModel.DataAnnotations;

namespace LotteryPlay.Models.ViewModels
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "请输入用户名")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入登录密码")]
        [MinLength(6, ErrorMessage = "密码至少6位")]
        [MaxLength(16, ErrorMessage = "密码不能超过16位")]
        public string LoginPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "请再次输入登录密码")]
        public string LoginPasswordConfirm { get; set; } = string.Empty;

        [Required(ErrorMessage = "请输入提款密码")]
        [MinLength(6)]
        [MaxLength(16)]
        public string WithdrawPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "请再次输入提款密码")]
        public string WithdrawPasswordConfirm { get; set; } = string.Empty;

        [Required(ErrorMessage = "请填写真实姓名")]
        public string RealName { get; set; } = string.Empty;

        [Required(ErrorMessage = "请选择收款方式")]
        public string WithdrawMethod { get; set; } = string.Empty;

        [Required(ErrorMessage = "请填写收款账号")]
        public string WithdrawAccount { get; set; } = string.Empty;

        // 省市必填
        [Required(ErrorMessage = "请选择省份")]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "请选择城市")]
        public string City { get; set; } = string.Empty;

        public string QQ { get; set; } = string.Empty;

        [Required(ErrorMessage = "请填写验证码结果")]
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