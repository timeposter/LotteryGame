namespace LotteryPlay.Models.ViewModels
{
    // 前端彩种视图
    public class LotteryVm
    {
        public int Id { get; set; }
        public string LotteryName { get; set; } = string.Empty;
    }

    // 前端玩法视图
    public class PlayConfigVm
    {
        public int Id { get; set; }
        public string PlayName { get; set; } = string.Empty;
        public decimal BonusAmount { get; set; }
    }
}