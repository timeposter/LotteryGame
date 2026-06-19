using System;

namespace LotteryCore.Enetities
{
    /// <summary>
    /// USDT 提款订单表
    /// </summary>
    public class WithdrawOrder
    {
        public int Id { get; set; }

        /// <summary>提款订单号</summary>
        public string OrderNo { get; set; } = string.Empty;

        /// <summary>用户ID</summary>
        public int UserId { get; set; }

        /// <summary>用户名</summary>
        public string UserName { get; set; } = string.Empty;

        /// <summary>提款金额</summary>
        public decimal Money { get; set; }

        /// <summary>链类型 TRC20/ERC20</summary>
        public string ChainType { get; set; } = string.Empty;

        /// <summary>用户USDT收款地址</summary>
        public string UserReceiveAddr { get; set; } = string.Empty;

        /// <summary>订单状态 0=待处理 1=已完成 2=已驳回</summary>
        public int Status { get; set; } = 0;

        /// <summary>申请时间</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>处理完成时间</summary>
        public DateTime? DealTime { get; set; }

        /// <summary>备注</summary>
        public string Remark { get; set; } = string.Empty;
    }
}