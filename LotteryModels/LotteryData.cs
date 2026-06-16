

using System.ComponentModel.DataAnnotations.Schema;

namespace LotteryModels
{
    /// <summary>开奖期号数据</summary>
    public class LotteryData
    {
        public int Id { get; set; }

        /// <summary>所属彩种</summary>
        public int LotteryId { get; set; }

        /// <summary>期号</summary>
        public string PeriodNo { get; set; } = string.Empty;

        /// <summary>开奖号码</summary>
        public string? OpenNumber { get; set; }

        /// <summary>投注截止时间</summary>
        public DateTime EndTime { get; set; }

        /// <summary>开奖时间</summary>
        public DateTime? OpenTime { get; set; }

        /// <summary>0=待开奖 1=已开奖</summary>
        public int IsOpen { get; set; } = 0;
        public DateTime? CreateTime { get; set; }
        #region 导航关联（EF联表查询必备）
        /// <summary>所属彩种实体</summary>
        [ForeignKey(nameof(LotteryId))]
        public Lottery Lottery { get; set; } = null!;
        #endregion
    }
}