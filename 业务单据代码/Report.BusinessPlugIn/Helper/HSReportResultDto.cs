using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.DJJX.Report.BusinessPlugIn.Helper
{
    /// <summary>
    /// 存货收发存汇总表数据
    /// </summary>
    public class HSReportResultDto
    {
        /// <summary>
        /// 物料内码
        /// </summary>
        public long MaterialId { get; set; }

        /// <summary>
        /// 仓库
        /// </summary>
        public long StockId { get; set; }

        /// <summary>
        /// 期末结存单价
        /// </summary>
        public decimal EndPrice { get; set; }

        /// <summary>
        /// 本期发出单价
        /// </summary>
        public decimal SEndPrice { get; set; }

        /// <summary>
        /// 本期收入单价
        /// </summary>
        public decimal ReceivePrice { get; set; }

        /// <summary>
        /// 期初结存单价
        /// </summary>
        public decimal InitPrice { get; set; }
    }
}
