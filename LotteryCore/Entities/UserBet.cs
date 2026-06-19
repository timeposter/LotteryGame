using System.ComponentModel.DataAnnotations.Schema;

namespace LotteryCore.Enetities
{
    /// <summary>用户投注记录</summary>

    [Table("UserBets")]
    public class UserBet
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;

        /// <summary>期号</summary>
        public string Period { get; set; } = string.Empty;

        /// <summary>投注号码</summary>
        public string BetNumber { get; set; } = string.Empty;

        /// <summary>倍数</summary>
        public int Multiple { get; set; } = 1;

        /// <summary>单注金额</summary>
        public decimal BetMoney { get; set; }

        /// <summary>是否已结算</summary>
        public bool IsSettled { get; set; } = false;

        /// <summary>是否中奖</summary>
        public bool IsWin { get; set; } = false;

        /// <summary>中奖金额</summary>
        public decimal WinMoney { get; set; }

        /// <summary>投注时间</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
        public int LotteryId { get; set; }
        public int PlayId { get;  set; }
        public string PeriodNo { get;  set; }
        public PlayConfig Play { get; set; }
        /// <summary>来源：0普通手动投注 1追号自动生成</summary>
        public int SourceType { get; set; } = 0;
        /// <summary>关联追号ID，追号单存UserTrace.Id，普通=0</summary>
        public int TraceId { get; set; } = 0;
    }
}