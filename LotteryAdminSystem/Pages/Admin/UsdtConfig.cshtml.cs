using LotteryCore.Data;
using LotteryCore.Enetities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LotteryAdminSystem.Pages.Admin
{
    [Authorize]
    public class UsdtConfigModel : PageModel
    {
        private readonly AppDBContext _db;
        public List<UsdtAccount> UsdtList { get; set; } = new();

        public UsdtConfigModel(AppDBContext db)
        {
            _db = db;
        }

        public async Task OnGetAsync()
        {
            ViewData["Menu"] = "usdt";
            UsdtList = await _db.UsdtAccounts.OrderBy(x => x.Sort).ToListAsync();
        }

        /// <summary>新增/保存收款账户</summary>
        public async Task<IActionResult> OnPostSave(int id, string accountName, string usdtAddress, string chainType, int sort, bool isEnable, string remark)
        {
            var item = await _db.UsdtAccounts.FindAsync(id);
            if (item == null)
            {
                item = new UsdtAccount();
                _db.UsdtAccounts.Add(item);
            }
            item.AccountName = accountName;
            item.UsdtAddress = usdtAddress;
            item.ChainType = chainType;
            item.Sort = sort;
            item.IsEnable = isEnable;
            item.Remark = remark;
            item.CreateTime = DateTime.Now;

            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        /// <summary>删除收款账户</summary>
        public async Task<IActionResult> OnPostDelete(int id)
        {
            var item = await _db.UsdtAccounts.FindAsync(id);
            if (item != null)
            {
                _db.UsdtAccounts.Remove(item);
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }

        /// <summary>切换启用/禁用状态</summary>
        public async Task<IActionResult> OnPostToggleEnable(int id)
        {
            var item = await _db.UsdtAccounts.FindAsync(id);
            if (item != null)
            {
                item.IsEnable = !item.IsEnable;
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}