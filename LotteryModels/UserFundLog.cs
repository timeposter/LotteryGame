using System;

namespace LotteryModels
{
    public class UserFundLog
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        /// <summary>1充值 2提款 3投注 4中奖</summary>
        public int Type { get; set; }
        public decimal Money { get; set; }
        public decimal BeforeBalance { get; set; }
        public decimal AfterBalance { get; set; }
        public string? Remark { get; set; }
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}
