using LotteryAdminSystem.Data;
using LotteryModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LotteryAdminSystem.Pages.Lotterys
{
    public class LotteryConfigModel : PageModel
    {
        private readonly AppDbContext _db;
        public List<Lottery> LotteryList { get; set; } = new();
        public List<PlayConfig> PlayList { get; set; } = new();

        public LotteryConfigModel(AppDbContext db)
        {
            _db = db;
        }

        public async Task OnGetAsync()
        {
            LotteryList = await _db.Lottery.OrderBy(x => x.Sort).ToListAsync();
            PlayList = await _db.PlayConfig.Include(x => x.Lottery).OrderBy(x => x.LotteryId).ThenBy(x => x.Sort).ToListAsync();
        }

        // 保存彩种
        public async Task<IActionResult> OnPostSaveLottery(int id, string name, int sort, bool enable)
        {
            var item = await _db.Lottery.FindAsync(id);
            if (item == null)
            {
                item = new Lottery();
                _db.Lottery.Add(item);
            }
            item.LotteryName = name;
            item.Sort = sort;
            item.IsEnable = enable;
            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        // 保存玩法
        public async Task<IActionResult> OnPostSavePlay(int id, int lotteryId, string playName, decimal bonus, int sort, bool enable)
        {
            var item = await _db.PlayConfig.FindAsync(id);
            if (item == null)
            {
                item = new PlayConfig();
                _db.PlayConfig.Add(item);
            }
            item.LotteryId = lotteryId;
            item.PlayName = playName;
            item.BonusAmount = bonus;
            item.Sort = sort;
            item.IsEnable = enable;
            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        // 删除
        public async Task<IActionResult> OnPostDelLottery(int id)
        {
            var item = await _db.Lottery.FindAsync(id);
            if (item != null)
            {
                _db.Lottery.Remove(item);
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostDelPlay(int id)
        {
            var item = await _db.PlayConfig.FindAsync(id);
            if (item != null)
            {
                _db.PlayConfig.Remove(item);
                await _db.SaveChangesAsync();
            }
            return RedirectToPage();
        }
    }
}