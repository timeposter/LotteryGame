using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LotteryModels
{
    public class UsdtAccount
    {
        public int Id { get; set; }

        /// <summary>收款账户名称（前台展示）</summary>
        [MaxLength(50)]
        public string AccountName { get; set; } = string.Empty;

        /// <summary>USDT收款地址</summary>
        [MaxLength(200)]
        public string UsdtAddress { get; set; } = string.Empty;

        /// <summary>链类型：TRC20 / ERC20</summary>
        [MaxLength(20)]
        public string ChainType { get; set; } = "TRC20";

        /// <summary>排序</summary>
        public int Sort { get; set; } = 0;

        /// <summary>是否启用</summary>
        public bool IsEnable { get; set; } = true;

        /// <summary>备注说明</summary>
        [MaxLength(500)]
        public string Remark { get; set; } = string.Empty;

        /// <summary>创建时间</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}

