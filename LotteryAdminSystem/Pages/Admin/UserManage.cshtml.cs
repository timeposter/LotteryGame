using LotteryCore.Enetities;
using Microsoft.AspNetCore.Authorization;
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
    }
}