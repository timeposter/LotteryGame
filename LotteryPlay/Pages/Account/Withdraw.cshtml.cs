using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LotteryCore.Enetities;
using LotteryCore.Data;

namespace LotteryPlay.Pages.Account
{
    public class WithdrawModel : PageModel
    {
        private readonly AppDBContext _db;
        public WithdrawModel(AppDBContext db)
        {
            _db = db;
        }

        #region 全局配置（可自行修改）
        /// <summary>最低提款金额</summary>
        private const decimal MinWithdrawMoney = 5.00m;
        #endregion

        // 页面展示字段
        public decimal Balance { get; set; }
        public string Msg { get; set; } = string.Empty;
        public string MsgColor { get; set; } = "#333";

        // 表单绑定字段
        [BindProperty]
        public decimal Money { get; set; }

        [BindProperty]
        public string ChainType { get; set; } = "TRC20";

        [BindProperty]
        public string UserReceiveAddr { get; set; } = string.Empty;

        /// <summary>页面加载</summary>
        public async Task<IActionResult> OnGetAsync()
        {
            // 登录校验
            // ========== 1. 登录校验 ==========
            var userIdStr = HttpContext.Session.GetString("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId) || userId <= 0)
            {
                // 未登录，跳转到登录页
                return RedirectToPage("/Account/Login");
            }

            // 读取用户余额
            var user = await _db.Users.FindAsync(userId);
            Balance = user?.Balance ?? 0;
            return Page();
        }

        /// <summary>提交提款申请</summary>
        public async Task<IActionResult> OnPostAsync()
        {
            // ========== 1. 登录校验 ==========
            var userIdStr = HttpContext.Session.GetString("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId) || userId <= 0)
            {
                // 未登录，跳转到登录页
                return RedirectToPage("/Account/Login");
            }

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                Msg = "账号异常，请重新登录";
                MsgColor = "red";
                return Page();
            }
            Balance = user.Balance;

            // 2. 基础表单校验
            if (Money <= 0)
            {
                Msg = "提款金额必须大于0";
                MsgColor = "red";
                return Page();
            }
            if (Money < MinWithdrawMoney)
            {
                Msg = $"最低提款金额为 {MinWithdrawMoney} USDT";
                MsgColor = "red";
                return Page();
            }
            if (string.IsNullOrWhiteSpace(ChainType))
            {
                Msg = "请选择转账链类型";
                MsgColor = "red";
                return Page();
            }
            if (string.IsNullOrWhiteSpace(UserReceiveAddr))
            {
                Msg = "请填写你的USDT收款地址";
                MsgColor = "red";
                return Page();
            }

            // 3. 余额校验
            if (Money > user.Balance)
            {
                Msg = "账户余额不足，无法提款";
                MsgColor = "red";
                return Page();
            }

            try
            {
                // 4. 生成提款订单号（WD = Withdraw）
                string orderNo = $"WD{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";

                // 5. 新增提款订单
                var withdrawOrder = new WithdrawOrder
                {
                    OrderNo = orderNo,
                    UserId = userId,
                    UserName = userName,
                    Money = Money,
                    ChainType = ChainType,
                    UserReceiveAddr = UserReceiveAddr,
                    Status = 1, // 1=已完成（简易即时版，如需人工审核改为0）
                    CreateTime = DateTime.Now,
                    DealTime = DateTime.Now,
                    Remark = "用户自助提款"
                };
                _db.WithdrawOrders.Add(withdrawOrder);

                // 6. 扣减用户余额
                decimal oldBalance = user.Balance;
                user.Balance -= Money;
                decimal newBalance = user.Balance;

                // 7. 写入资金流水（Type=2 代表提款）
                _db.UserFundLogs.Add(new UserFundLog
                {
                    UserId = userId,
                    UserName = userName,
                    Type = 2,
                    Money = Money,
                    BeforeBalance = oldBalance,
                    AfterBalance = newBalance,
                    Remark = $"USDT提款({ChainType})，订单号：{orderNo}"
                });

                // 保存数据库
                await _db.SaveChangesAsync();

                // 成功提示
                Msg = "提款申请提交成功，金额已扣除！";
                MsgColor = "green";
                Balance = newBalance;
            }
            catch (Exception ex)
            {
                Msg = $"提交失败：{ex.Message}";
                MsgColor = "red";
            }

            return Page();
        }
    }
}