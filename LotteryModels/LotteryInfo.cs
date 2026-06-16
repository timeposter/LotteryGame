using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotteryModels
{
    public class LotteryInfo
    {
        public long Id { get; set; }
        public string LotteryCode { get; set; }
        public string LotteryType { get; set; }
        public string RaceType { get; set; }
        public bool IsEnable { get; set; }
        public DateTime CreateTime { get; set; }
    }

    public class LotteryIssueRecord
    {
        public long Id { get; set; }
        public string LotteryCode { get; set; }
        public string IssueNo { get; set; }
        public string AwardCode { get; set; }
        public int IssueStatus { get; set; }
        public int? NextOpenRemaining { get; set; }
        public string LastIssueNo { get; set; }
        public string NextIssueNo { get; set; }
        public DateTime? NextOpenTime { get; set; }
        public DateTime UpdateTime { get; set; }
    }
}
