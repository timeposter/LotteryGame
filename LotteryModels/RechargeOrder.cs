using System;

namespace LotteryModels
{
    /// <summary>
    /// USDT充值订单
    /// </summary>
    public class RechargeOrder
    {
        public int Id { get; set; }
        /// <summary>订单号</summary>
        public string OrderNo { get; set; } = string.Empty;
        /// <summary>用户ID</summary>
        public int UserId { get; set; }
        /// <summary>用户名</summary>
        public string UserName { get; set; } = string.Empty;
        /// <summary>充值金额(USDT)</summary>
        public decimal Money { get; set; }
        /// <summary>链类型 TRC20/ERC20</summary>
        public string ChainType { get; set; } = string.Empty;
        /// <summary>收款地址</summary>
        public string ReceiveAddress { get; set; } = string.Empty;
        /// <summary>状态 0=待支付 1=已完成 2=已超时</summary>
        public int Status { get; set; } = 0;
        /// <summary>创建时间</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
        /// <summary>完成时间</summary>
        public DateTime? PayTime { get; set; }
    }
}