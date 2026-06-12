using System;

namespace LotteryPlay
{
    /// <summary>
    /// 5分钟一期 期号/时间计算工具类
    /// 规则：每日0点从001开始，每5分钟一期
    /// </summary>
    public static class PeriodHelper
    {
        /// <summary>
        /// 单期间隔（分钟）
        /// </summary>
        public const int PeriodMinute = 5;
        /// <summary>
        /// 单期间隔（秒）
        /// </summary>
        public const int PeriodSecond = PeriodMinute * 60;

        /// <summary>
        /// 根据服务器时间 计算【当前期号、本期截止时间、剩余秒数、是否可投注】
        /// </summary>
        /// <returns></returns>
        public static PeriodModel GetCurrentPeriodInfo()
        {
            DateTime now = DateTime.Now; // 服务器当前时间
            DateTime todayStart = now.Date; // 今日 00:00:00

            // 1. 计算今日零点到当前时间的总秒数
            double totalSecondsFromToday = (now - todayStart).TotalSeconds;

            // 2. 计算今日当前是第几期（从 1 开始）
            int periodIndex = (int)Math.Floor(totalSecondsFromToday / PeriodSecond) + 1;

            // 3. 拼接完整期号：yyyyMMdd + 3位补零序号
            string dateStr = todayStart.ToString("yyyyMMdd");
            string periodNo = $"{dateStr}{periodIndex:D3}";

            // 4. 计算本期 开始时间、截止时间
            long currentPeriodStartSec = (periodIndex - 1) * PeriodSecond;
            DateTime periodStartTime = todayStart.AddSeconds(currentPeriodStartSec);
            DateTime periodEndTime = periodStartTime.AddSeconds(PeriodSecond);

            // 5. 计算剩余倒计时秒数
            double remainSeconds = (periodEndTime - now).TotalSeconds;
            remainSeconds = Math.Round(remainSeconds);

            // 6. 是否允许投注：剩余时间 > 0 可投注
            bool canBet = remainSeconds > 0;

            // 处理极端情况：跨零点/时间偏差（防止负数）
            if (remainSeconds < 0)
            {
                // 本期已截止，自动跳到下一期
                periodIndex++;
                periodNo = $"{dateStr}{periodIndex:D3}";
                periodStartTime = periodEndTime;
                periodEndTime = periodStartTime.AddSeconds(PeriodSecond);
                remainSeconds = (periodEndTime - now).TotalSeconds;
                canBet = true;
            }

            return new PeriodModel
            {
                PeriodNo = periodNo,
                PeriodStartTime = periodStartTime,
                PeriodEndTime = periodEndTime,
                RemainSeconds = (int)remainSeconds,
                CanBet = canBet
            };
        }

        /// <summary>
        /// 秒数 转 时分秒字符串 00:00:00
        /// </summary>
        public static string SecondToHms(int totalSec)
        {
            if (totalSec <= 0) return "00:00:00";
            int h = totalSec / 3600;
            int m = totalSec % 3600 / 60;
            int s = totalSec % 60;
            return $"{h:D2}:{m:D2}:{s:D2}";
        }
    }

    /// <summary>
    /// 期号信息模型
    /// </summary>
    public class PeriodModel
    {
        /// <summary>
        /// 完整期号
        /// </summary>
        public string PeriodNo { get; set; }
        /// <summary>
        /// 本期开始时间
        /// </summary>
        public DateTime PeriodStartTime { get; set; }
        /// <summary>
        /// 本期截止时间
        /// </summary>
        public DateTime PeriodEndTime { get; set; }
        /// <summary>
        /// 剩余倒计时秒数
        /// </summary>
        public int RemainSeconds { get; set; }
        /// <summary>
        /// 是否可投注
        /// </summary>
        public bool CanBet { get; set; }
    }
}