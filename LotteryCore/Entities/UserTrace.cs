using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotteryCore.Enetities
{
    public class UserTrace
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int LotteryId { get; set; }
        public int PlayId { get; set; }
        /// <summary>起始期号</summary>
        public string StartPeriod { get; set; }
        /// <summary>剩余追号期数</summary>
        public int LeftCount { get; set; }
        /// <summary>总追号期数</summary>
        public int TotalCount { get; set; }
        /// <summary>投注号码</summary>
        public string BetNumber { get; set; }
        /// <summary>倍数</summary>
        public int Multiple { get; set; }
        /// <summary>单期金额</summary>
        public decimal PerMoney { get; set; }
        /// <summary>状态：0正常 1已终止</summary>
        public int Status { get; set; } = 0;
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}