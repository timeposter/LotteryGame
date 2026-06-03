namespace LotteryPlay.Services
{
    public static class LotteryRuleService
    {
        // 单注奖金
        private static readonly decimal _unitBonus = 9.8m;
        // 示例奖金表（模拟），实际平台会有不同
        private static readonly Dictionary<int, decimal> _bonusMap = new()
        {
            { 1, 100000m },
            { 2, 3200m },
            { 3, 1600m },
            {4, 11.6m },
            { 5, 6.6m },
            { 6, 9.8m }
        };

        public static decimal GetBonus(int playId)
        {
            if (_bonusMap.TryGetValue(playId, out var bonus))
                return bonus;
            return 0;
        }

        // 核心：判断是否中奖
        public static bool IsWin(string openNum, string betNum, int playId)
        {
            if (string.IsNullOrWhiteSpace(openNum) || string.IsNullOrWhiteSpace(betNum))
                return false;

            openNum = openNum.Trim();
            betNum = betNum.Trim();

            return playId switch
            {
                1 => openNum == betNum,
                2 => CheckZuSan(openNum, betNum),
                3 => CheckZuLiu(openNum, betNum),
                4 => CheckDingWeiDan(openNum, betNum),
                5 => openNum.Contains(betNum),
                6 => CheckDaXiaoDanShuang(openNum, betNum),
                _ => false
            };
        }

        // 组选三：开奖号有一对相同号码，且你的号码包含这三个数（不要求顺序）
        private static bool CheckZuSan(string open, string bet)
        {
            if (open.Distinct().Count() != 2) return false;
            var openList = open.ToCharArray().OrderBy(x => x).ToList();
            var betList = bet.ToCharArray().OrderBy(x => x).ToList();
            return openList.SequenceEqual(betList);
        }

        // 组选六：开奖号五个数各不相同，你的号码包含这三个数（不要求顺序）
        private static bool CheckZuLiu(string open, string bet)
        {
            if (open.Distinct().Count() < 3) return false;
            var openList = open.ToCharArray().OrderBy(x => x).ToList();
            var betList = bet.ToCharArray().OrderBy(x => x).ToList();
            return openList.SequenceEqual(betList);
        }

        // 定位胆：你指定某一位数字，开奖号对应位置相同
        private static bool CheckDingWeiDan(string open, string bet)
        {
            // 格式示例：万位3千位5
            if (bet.Contains("万位"))
            {
                var pos = int.Parse(bet.Replace("万位", ""));
                return open[0] == pos.ToString()[0];
            }
            if (bet.Contains("千位"))
            {
                var pos = int.Parse(bet.Replace("千位", ""));
                return open[1] == pos.ToString()[0];
            }
            if (bet.Contains("百位"))
            {
                var pos = int.Parse(bet.Replace("百位", ""));
                return open[2] == pos.ToString()[0];
            }
            if (bet.Contains("十位"))
            {
                var pos = int.Parse(bet.Replace("十位", ""));
                return open[3] == pos.ToString()[0];
            }
            if (bet.Contains("个位"))
            {
                var pos = int.Parse(bet.Replace("个位", ""));
                return open[4] == pos.ToString()[0];
            }
            return false;
        }

        // 不定位：你选的数字出现在开奖号任意位置
        private static bool CheckBuDingWei(string open, string bet)
        {
            return open.Contains(bet);
        }

        // 大小单双：示例，这里简单处理为 0-4小/5-9大，13579单/02468双
        private static bool CheckDaXiaoShuangDan(string open, string bet)
        {
            int total = open.Sum(c => c - '0');
            if (bet == "大") return total >= 23;
            if (bet == "小") return total <= 22;
            if (bet == "单") return total % 2 == 1;
            if (bet == "双") return total % 2 == 0;
            return false;
        }
        /// <summary>获取单注奖金</summary>
        public static decimal GetUnitBonus(int playId)
        {
            return playId switch
            {
                1 => 100000m,//直选
                2 => 3200m,//组选三
                3 => 1600m,//组选六
                4 => 11.6m,//定位胆
                5 => 6.6m,//不定位
                6 => _unitBonus,//大小单双
                _ => 0
            };
        }

        /// <summary>判断是否中奖</summary>
        public static bool CheckWin(string openNum, string betNum, int playId)
        {
            if (string.IsNullOrWhiteSpace(openNum) || string.IsNullOrWhiteSpace(betNum))
                return false;

            openNum = openNum.Trim();
            betNum = betNum.Trim();

            return playId switch
            {
                1 => openNum == betNum,
                2 => CheckZuSan(openNum, betNum),
                3 => CheckZuLiu(openNum, betNum),
                4 => CheckDingWeiDan(openNum, betNum),
                5 => openNum.Contains(betNum),
                6 => CheckDaXiaoDanShuang(openNum, betNum),
                _ => false
            };
        }


        private static bool CheckDaXiaoDanShuang(string open, string bet)
        {
            int sum = open.Sum(c => c - '0');
            return bet switch
            {
                "大" => sum >= 23,
                "小" => sum <= 22,
                "单" => sum % 2 == 1,
                "双" => sum % 2 == 0,
                _ => false
            };
        }

        /// <summary>生成5位随机开奖号</summary>
        public static string GenerateOpenNumber()
        {
            var rnd = new Random();
            return $"{rnd.Next(0, 10)}{rnd.Next(0, 10)}{rnd.Next(0, 10)}{rnd.Next(0, 10)}{rnd.Next(0, 10)}";
        }
    }
}
