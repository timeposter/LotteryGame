using System.ComponentModel.DataAnnotations;

namespace LotteryModels
{

    public class User
    {
        public int Id { get; set; }

        [Required]
        [StringLength(10, MinimumLength = 2)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        public string WithdrawPasswordHash { get; set; } = string.Empty;

        public string? RealName { get; set; }

        public string? WithdrawMethod { get; set; }

        public string? WithdrawAccount { get; set; }

        public string? Province { get; set; }

        public string? City { get; set; }

        public string? QQ { get; set; }
        public decimal Balance { get; set; } = 0.00m;
        public DateTime? CreateTime { get; set; } = DateTime.Now;
    }
}