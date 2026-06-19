using System.ComponentModel.DataAnnotations;

namespace LotteryCore.Enetities
{
    public class Admins
    {
        public int Id { get; set; }

        [Required]
        [StringLength(30)]
        public string AdminName { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}
