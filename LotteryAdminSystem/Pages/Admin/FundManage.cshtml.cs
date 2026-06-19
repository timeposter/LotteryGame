using LotteryCore.Data;
using LotteryCore.Enetities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LotteryAdminSystem.Pages.Admin
{
    [Authorize]
    public class FundManageModel : PageModel
    {
        private readonly AppDBContext _db;
        public FundManageModel(AppDBContext db)
        {
            _db = db;
        }

        public List<UserFundLog> FundList { get; set; } = new();

        public async Task OnGetAsync()
        {
            // 倒序展示最新50条流水
            FundList = await _db.UserFundLogs
                .OrderByDescending(x => x.CreateTime)
                .Take(50)
                .ToListAsync();
        }
    }
}