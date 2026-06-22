using LotteryCore.Data;
using LotteryCore.Enetities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LotteryPlay.Pages.Account
{
    public class TraceRecordModel : PageModel
    {
        private readonly AppDBContext _db;
        public TraceRecordModel(AppDBContext db)
        {
            _db = db;
        }

        public List<UserTrace> List { get; set; } = new List<UserTrace>();

        public async Task OnGetAsync()
        {
            var UserIdStr = HttpContext.Session.GetInt32("UserId");
            if (!UserIdStr.HasValue)
            {
                Response.Redirect("/Account/Login");
                return;
            }
            List = await _db.UserTrace
                .Where(w => w.UserId == UserIdStr)
                .OrderByDescending(o => o.CreateTime)
                .ToListAsync();
        }

        /// <summary>终止追号接口</summary>
        public async Task<JsonResult> OnPostStopTrace(int traceId)
        {
            var UserIdStr = HttpContext.Session.GetInt32("UserId");
            if (!UserIdStr.HasValue||UserIdStr<=0)
                return new JsonResult(new { code = 0, msg = "请登录" });

            var trace = await _db.UserTrace.FirstOrDefaultAsync(w => w.Id == traceId && w.UserId == UserIdStr);
            if (trace == null || trace.Status == 1)
                return new JsonResult(new { code = 0, msg = "数据不存在或已终止" });

            //剩余金额退回用户
            var user = await _db.Users.FindAsync(UserIdStr);
            decimal refundMoney = trace.LeftCount * trace.PerMoney;
            user.Balance += refundMoney;

            trace.Status = 1;
            trace.LeftCount = 0;
            await _db.SaveChangesAsync();

            return new JsonResult(new { code = 1, msg = $"终止成功，退回{refundMoney}元到余额" });
        }
    }
}