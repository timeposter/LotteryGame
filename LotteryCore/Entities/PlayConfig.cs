using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotteryCore.Enetities
{
    public class PlayConfig
    {
        public int Id { get; set; }
        public int LotteryId { get; set; }
        public string PlayName { get; set; } = string.Empty;
        public decimal BonusAmount { get; set; }
        public int Sort { get; set; }
        public bool IsEnable { get; set; }

        // 导航属性（可选）
        public Lottery Lottery { get; set; } = null!;
    }
}
