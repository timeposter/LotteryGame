using LotteryModels;
using LotteryPlay.Data;
using LotteryPlay.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace LotteryPlay.Pages.Account
{
    public class TraceRecordModel : PageModel
    {
        private readonly AppDbContext _db;
        public TraceRecordModel(AppDbContext db)
        {
            _db = db;
        }

        public List<UserTrace> List { get; set; }

        public async Task OnGetAsync()
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out int uid))
            {
                Response.Redirect("/Login");
                return;
            }
            List = await _db.UserTrace
                .Where(w => w.UserId == uid)
                .OrderByDescending(o => o.CreateTime)
                .ToListAsync();
        }

        /// <summary>终止追号接口</summary>
        public async Task<JsonResult> OnPostStopTrace(int traceId)
        {
            if (!int.TryParse(HttpContext.Session.GetString("UserId"), out int uid))
                return new JsonResult(new { code = 0, msg = "请登录" });

            var trace = await _db.UserTrace.FirstOrDefaultAsync(w => w.Id == traceId && w.UserId == uid);
            if (trace == null || trace.Status == 1)
                return new JsonResult(new { code = 0, msg = "数据不存在或已终止" });

            //剩余金额退回用户
            var user = await _db.Users.FindAsync(uid);
            decimal refundMoney = trace.LeftCount * trace.PerMoney;
            user.Balance += refundMoney;

            trace.Status = 1;
            trace.LeftCount = 0;
            await _db.SaveChangesAsync();

            return new JsonResult(new { code = 1, msg = $"终止成功，退回{refundMoney}元到余额" });
        }
    }
}