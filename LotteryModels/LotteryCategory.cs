namespace LotteryPlay.Models
{
    /// <summary>彩种配置表</summary>
    public class LotteryCategory
    {
        public int Id { get; set; }

        /// <summary>彩种类型ID</summary>
        public int LotteryId { get; set; }

        /// <summary>彩种名称</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>每期时长(秒)</summary>
        public int PeriodSecond { get; set; }

        /// <summary>投注截止提前秒数</summary>
        public int StopBetSecond { get; set; }

        /// <summary>是否启用</summary>
        public bool IsEnable { get; set; } = true;
    }
}