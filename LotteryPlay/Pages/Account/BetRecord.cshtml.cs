using LotteryCore.Data;
using LotteryCore.Enetities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LotteryPlay.Pages.Account
{
    public class BetRecordModel : PageModel
    {
        private readonly AppDBContext _dbContext;

        #region 页面绑定属性
        // 当前页码
        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;

        // 筛选类型：0=全部 1=已中奖 2=未中奖
        [BindProperty(SupportsGet = true)]
        public int FilterType { get; set; } = 0;

        // 分页配置
        public int PageSize { get; set; } = 10; // 每页10条
        public int TotalCount { get; set; }     // 总记录数
        public int TotalPage { get; set; }      // 总页数

        // 当前用户投注记录列表
        public List<UserBet> BetRecordList { get; set; } = new List<UserBet>();
        #endregion

        public BetRecordModel(AppDBContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 页面加载：查询投注记录
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            // ========== 1. 登录校验 ==========
            var userIdStr = HttpContext.Session.GetString("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId) || userId <= 0)
            {
                // 未登录，跳转到登录页
                return RedirectToPage("/Account/Login");
            }

            // ========== 2. 构建查询语句（只查当前登录用户 + 联查玩法、彩种） ==========
            var query = _dbContext.UserBets
                .Include(b => b.Play)          // 联查玩法配置
                .ThenInclude(p => p.Lottery)   // 联查彩种
                .Where(b => b.UserId == userId); // 权限：只能查看自己的记录

            // 按 中奖状态 筛选
            switch (FilterType)
            {
                case 1: // 已中奖
                    query = query.Where(b => b.IsWin == true);
                    break;
                case 2: // 未中奖
                    query = query.Where(b => b.IsWin == false);
                    break;
            }

            // 总记录数（用于计算分页）
            TotalCount = await query.CountAsync();

            // 计算总页数
            TotalPage = (int)Math.Ceiling(TotalCount / (double)PageSize);

            // 页码边界修正
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > TotalPage && TotalPage > 0) CurrentPage = TotalPage;

            // ========== 3. 分页查询 + 按投注时间倒序（最新记录在前） ==========
            BetRecordList = await query
                .OrderByDescending(b => b.CreateTime)
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToListAsync();

            return Page();
        }
    }
}