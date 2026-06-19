namespace LotteryCore.Enetities
{
    public class Lottery
    {
        public int Id { get; set; }
        public string LotteryName { get; set; } = string.Empty;
        public int Sort { get; set; }
        public bool IsEnable { get; set; }
        /// <summary>每期时长(秒)</summary>
        public int PeriodSecond { get; set; }

        /// <summary>投注截止提前秒数</summary>
        public int StopBetSecond { get; set; }
        public string? ApiCode { get;set; } = string.Empty;
    }
}