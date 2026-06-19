using LotteryCore;
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

            var user = await _db.Users.FindAsync(userId);
            Balance = user?.Balance ?? 0;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userName = HttpContext.Session.GetString("Username");
            if (!userId.HasValue || string.IsNullOrEmpty(userName))
                return RedirectToPage("/Account/Login");

            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                Msg = "账号异常";
                MsgColor = "red";
                return Page();
            }

            // 基础校验
            if (Money <= 0)
            {
                Msg = "充值金额必须大于0";
                MsgColor = "red";
                Balance = user.Balance;
                return Page();
            }
            if (string.IsNullOrWhiteSpace(ChainType))
            {
                Msg = "请选择转账链类型";
                MsgColor = "red";
                Balance = user.Balance;
                return Page();
            }

            // 1. 生成唯一订单号
            OrderNo = $"RE{DateTime.Now:yyyyMMddHHmmss}{new Random().Next(1000, 9999)}";

            // 2. 分配对应链收款地址
            ReceiveAddress = ChainType == "TRC20" ? Trc20Addr : Erc20Addr;

            // 3. QRCoder 生成二维码 Base64
            QrCodeBase64 = GenerateQrCodeBase64(ReceiveAddress);

            // 4. 写入充值订单表
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

            // 页面提示
            Msg = "请使用对应链钱包转账 USDT，转账完成后系统自动到账";
            MsgColor = "green";
            Balance = user.Balance;

            return Page();
        }

        /// <summary>
        /// 前端轮询接口：查询订单状态，支付成功则更新余额+流水
        /// </summary>
        public async Task<IActionResult> OnGetCheckPayStatus(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return new JsonResult(new { code = 0, msg = "订单号不能为空" });

            // 查询订单
            var order = await _db.RechargeOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

            if (order == null)
                return new JsonResult(new { code = -1, msg = "订单不存在" });

            // 判断订单是否超时（示例：15分钟超时）
            if (DateTime.Now - order.CreateTime > TimeSpan.FromMinutes(15))
            {
                order.Status = 2;
                await _db.SaveChangesAsync();
                return new JsonResult(new { code = -2, msg = "订单已超时，请重新充值" });
            }

            // 已完成直接返回成功
            if (order.Status == 1)
            {
                return new JsonResult(new { code = 1, msg = "支付成功" });
            }

            // ========== 此处对接链上查询/第三方回调 判断是否到账 ==========
            // 模拟：实际项目替换为 链上区块查询 / 支付平台回调校验
            bool paySuccess = false;

            if (paySuccess)
            {
                // 标记订单已完成
                order.Status = 1;
                order.PayTime = DateTime.Now;

                // 更新用户余额 + 新增资金流水（沿用你原有逻辑）
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

            // 待支付
            return new JsonResult(new { code = 0, msg = "等待区块确认，请稍候..." });
        }
        /// <summary>
        /// QRCoder 生成二维码 Base64
        /// </summary>
        private string GenerateQrCodeBase64(string content)
        {
            using var qrGen = new QRCodeGenerator();
            using var qrData = qrGen.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
            using var pngQr = new PngByteQRCode(qrData);

            byte[] imgBytes = pngQr.GetGraphic(20); // 20=像素密度
            return $"data:image/png;base64,{Convert.ToBase64String(imgBytes)}";
        }
    }
}