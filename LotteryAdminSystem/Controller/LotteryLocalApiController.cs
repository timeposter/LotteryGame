using LotteryAdminSystem.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LotteryAdminSystem.Controller
{
    [Route("api/lottery")]
    [ApiController]
    public class LotteryLocalApiController : ControllerBase
    {
        private readonly AppDbContext _db;
        public LotteryLocalApiController(AppDbContext db)
        {
            _db = db;
        }

        [HttpGet("awards")]
        public async Task<IActionResult> GetAwardData([FromQuery] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return Ok(new { code = -1, msg = "彩种编码不能为空" });

            // 当前待开奖期
            var nowItem = await _db.LotteryIssueRecords
                .FirstOrDefaultAsync(x => x.LotteryCode == code && x.IssueStatus == 0);

            if (nowItem == null)
                return Ok(new { code = -1, msg = "暂无开奖数据" });

            // 已开奖历史
            var awardList = await _db.LotteryIssueRecords
                .Where(x => x.LotteryCode == code && x.IssueStatus == 1)
                .OrderByDescending(x => x.IssueNo)
                .ToListAsync();

            var res = new
            {
                code = 0,
                msg = "success",
                data = new
                {
                    lottery = new
                    {
                        status = nowItem.NextOpenRemaining > 0 ? "open" : "close",
                        last_issue = nowItem.LastIssueNo,
                        now_issue = nowItem.IssueNo,
                        next_issue = nowItem.NextIssueNo,
                        next_open_remaining = nowItem.NextOpenRemaining,
                        next_opendate = nowItem.NextOpenTime?.ToString("yyyy-MM-dd HH:mm:ss")
                    },
                    awards = awardList.Select(x => new
                    {
                        issue = x.IssueNo,
                        code = x.AwardCode
                    }).ToList()
                }
            };
            return Ok(res);
        }
    }
}
