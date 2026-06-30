using LotteryCore.Data;
using LotteryCore.Enetities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LotteryAdminSystem.Pages.Admin
{
    [Authorize]
    public class UserManageModel : PageModel
    {
        private readonly AppDBContext _db;
        public UserManageModel(AppDBContext db)
        {
            _db = db;
        }

        public List<User> UserList { get; set; } = new();

        public async Task OnGetAsync()
        {
            UserList = await _db.Users
                .OrderByDescending(x => x.CreateTime)
                .ToListAsync();
        }

        /// <summary>
        /// 编辑用户接口，支持修改余额、姓名、QQ
        /// </summary>
        public async Task<IActionResult> OnPostEditUserAsync(int userId, string realName, decimal balance, string qq)
        {
            // 查找用户
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                return new JsonResult(new { code = 0, msg = "目标用户不存在！" });
            }

            // 校验余额不能为负数
            if (balance < 0)
            {
                return new JsonResult(new { code = 0, msg = "余额不能小于0！" });
            }

            // 赋值更新字段
            user.RealName = realName;
            user.Balance = balance;
            user.QQ = qq;

            // 保存数据库
            await _db.SaveChangesAsync();

            return new JsonResult(new { code = 1, msg = "用户信息（余额）修改成功！" });
        }
    }
}