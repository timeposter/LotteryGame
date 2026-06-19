public class LotteryPullSetting
{
    /// <summary>第三方API域名</summary>
    public string ApiHost { get; set; }
    /// <summary>轮询间隔秒</summary>
    public int PullIntervalSec { get; set; }
    /// <summary>请求超时毫秒</summary>
    public int RequestTimeoutMs { get; set; }
    /// <summary>单接口最大重试次数</summary>
    public int MaxRetryTimes { get; set; }
}
/// <summary>第三方外层返回</summary>
public class ThirdPartyLotteryResp
{
    public int Code { get; set; }
    public string Msg { get; set; }
    public ThirdPartyData Data { get; set; }
}

public class ThirdPartyData
{
    public ThirdPartyLotteryInfo Lottery { get; set; }
    public List<ThirdPartyAwardItem> Awards { get; set; }
}

public class ThirdPartyLotteryInfo
{
    public string Status { get; set; }
    public string Last_issue { get; set; }
    public string Now_issue { get; set; }
    public string Next_issue { get; set; }
    public int Next_open_remaining { get; set; }
    public string Next_opendate { get; set; }
}

public class ThirdPartyAwardItem
{
    public string Issue { get; set; }
    public string Code { get; set; }
}