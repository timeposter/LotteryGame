using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace LotteryCore.DTO
{
    public class LotteryApiResultDTO
    {
        public int code { get; set; }
        public string msg { get; set; } = string.Empty;
        public LotteryApiData data { get; set; } = new LotteryApiData();

    }
    
    public class LotteryApiData
    {
        public LotteryInfoItem lottery { get; set; } = new LotteryInfoItem();
        public List<LotteryAwardItem> awards { get; set; } = new List<LotteryAwardItem>();
    }

    public class LotteryInfoItem
    {
        public object? now_issue { get; set; } = string.Empty;
        public object? last_issue { get; set; } = string.Empty;
        public object next_issue { get; set; } = string.Empty;
        public int next_open_remaining { get; set; }
        public string status { get; set; } = string.Empty;
        public string next_opendate { get; set; } = string.Empty;

        [JsonIgnore]
        public string NowIssueStr => now_issue?.ToString() ?? string.Empty;
        [JsonIgnore]
        public string LastIssueStr => last_issue?.ToString() ?? string.Empty;
        [JsonIgnore]
        public string NextIssueStr => next_issue?.ToString() ?? string.Empty;
    }

    public class LotteryAwardItem
    {
        public string issue { get; set; } = string.Empty;
        public string code { get; set; } = string.Empty;
        public string opendate { get; set; } = string.Empty;
    }
}
