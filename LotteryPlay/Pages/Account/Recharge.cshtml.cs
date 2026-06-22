using LotteryCore;
using LotteryCore.Data;
using LotteryCore.Enetities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using QRCoder;

namespace LotteryPlay.Pages.Account
{
    public class RechargeModel : PageModel
    {
        private readonly AppDBContext _db;
        public RechargeModel(AppDBContext db)
        {
            _db = db;
        }

        public decimal Balance { get; set; }
        public string MsgColor { get; set; } = "#333";

        // 表单绑定字段
        [BindProperty]
        public decimal Money { get; set; }

        [BindProperty]
        public string ChainType { get; set; } = string.Empty;

        // 页面展示字段
        public string Msg { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }

        // 支付订单相关
        public string OrderNo { get; set; } = string.Empty;
        public string ReceiveAddress { get; set; } = string.Empty;
        public string QrCodeUrl { get; set; } = string.Empty;
        public string QrCodeBase64 { get; set; } = string.Empty;

        #region 固定收款地址
        private const string Trc20Addr = "J3AZwFhYnyJoeyK2a25mrMMpPNtFwXgLp6G5J6QXKBbe";
        private const string Erc20Addr = "J3AZwFhYnyJoeyK2a25mrMMpPNtFwXgLp6G5J6QXKBbe";
        #endregion

        /// <summary>
        /// 统一加载用户余额
        /// </summary>
        private async Task LoadUserBalance()
        {
            var userIdStr = HttpContext.Session.GetInt32("UserId");
            if (userIdStr.HasValue)
            {
                var user = await _db.Users.FindAsync(userIdStr);
                Balance = user?.Balance ?? 0;
            }
            else
            {
                Balance = 0;
            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            // 登录校验，携带返回地址
            var userIdStr = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if ((!userIdStr.HasValue) || userIdStr <= 0)
            {
                return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("./Recharge") });
            }

            await LoadUserBalance();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // 模型校验失败直接返回页面，不跳转
            if (!ModelState.IsValid)
            {
                await LoadUserBalance();
                return Page();
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("Username");
            // 未登录携带返回地址
            if (!userId.HasValue || string.IsNullOrEmpty(userName))
                return RedirectToPage("/Account/Login", new { returnUrl = Url.Page("./Recharge") });

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                Msg = "账号异常，请重新登录";
                MsgColor = "red";
                await LoadUserBalance();
                return Page();
            }

            // 业务参数校验
            if (Money <= 0)
            {
                Msg = "充值金额必须大于0";
                MsgColor = "red";
                await LoadUserBalance();
                return Page();
            }
            if (string.IsNullOrWhiteSpace(ChainType) || (ChainType != "TRC20" && ChainType != "ERC20"))
            {
                Msg = "请选择正确的转账链类型";
                MsgColor = "red";
                await LoadUserBalance();
                return Page();
            }

            // 生成订单信息
            OrderNo = $"RE{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";
            ReceiveAddress = ChainType == "TRC20" ? Trc20Addr : Erc20Addr;
            QrCodeBase64 = GenerateQrCodeBase64(ReceiveAddress);

            // 新增充值订单入库
            var order = new RechargeOrder
            {
                OrderNo = OrderNo,
                UserId = userId.Value,
                UserName = userName,
                Money = Money,
                ChainType = ChainType,
                ReceiveAddress = ReceiveAddress,
                Status = 0,
                CreateTime = DateTime.Now
            };
            _db.RechargeOrders.Add(order);
            await _db.SaveChangesAsync();

            Msg = "请使用对应链钱包转账 USDT，转账完成后系统自动到账";
            MsgColor = "green";
            await LoadUserBalance();
            return Page();
        }

        /// <summary>
        /// 前端轮询接口：查询订单状态，支付成功则更新余额+流水
        /// </summary>
        public async Task<IActionResult> OnGetCheckPayStatus(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return new JsonResult(new { code = 0, msg = "订单号不能为空" });

            var order = await _db.RechargeOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

            if (order == null)
                return new JsonResult(new { code = -1, msg = "订单不存在" });

            // 15分钟订单超时
            if (DateTime.Now - order.CreateTime > TimeSpan.FromMinutes(15))
            {
                order.Status = 2;
                await _db.SaveChangesAsync();
                return new JsonResult(new { code = -2, msg = "订单已超时，请重新充值" });
            }

            // 已支付完成
            if (order.Status == 1)
            {
                return new JsonResult(new { code = 1, msg = "支付成功" });
            }

            // ========== 此处对接链上回调/区块查询，paySuccess为实际到账判断 ==========
            bool paySuccess = false;

            if (paySuccess)
            {
                order.Status = 1;
                order.PayTime = DateTime.Now;

                var user = await _db.Users.FindAsync(order.UserId);
                if (user != null)
                {
                    decimal oldBal = user.Balance;
                    user.Balance += order.Money;

                    _db.UserFundLogs.Add(new UserFundLog
                    {
                        UserId = order.UserId,
                        UserName = order.UserName,
                        Type = 1,
                        Money = order.Money,
                        BeforeBalance = oldBal,
                        AfterBalance = user.Balance,
                        Remark = $"USDT充值({order.ChainType})，订单号：{order.OrderNo}"
                    });
                }

                await _db.SaveChangesAsync();
                return new JsonResult(new { code = 1, msg = "支付成功，余额已到账" });
            }

            // 待转账确认
            return new JsonResult(new { code = 0, msg = "等待区块确认，请稍候..." });
        }

        /// <summary>
        /// 生成二维码Base64
        /// </summary>
        private string GenerateQrCodeBase64(string content)
        {
            using var qrGen = new QRCodeGenerator();
            using var qrData = qrGen.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            using var pngQr = new PngByteQRCode(qrData);

            byte[] imgBytes = pngQr.GetGraphic(20);
            return $"data:image/png;base64,{Convert.ToBase64String(imgBytes)}";
        }
    }
}