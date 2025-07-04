using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.DJJX.Report.BusinessPlugIn.Helper
{
    /// <summary>
    /// 过滤字段
    /// </summary>
    public class ReportFilterDto
    {
        /// <summary>
        /// 物料内码
        /// </summary>
        public string MaterialId { get; set; }

        /// <summary>
        /// 物料编码
        /// </summary>
        public string MaterialNumber { get; set; }

        /// <summary>
        /// 仓库内码
        /// </summary>
        public string StockId { get; set; }

        /// <summary>
        /// 仓库编码
        /// </summary>
        public string StockNumber { get; set; }

        /// <summary>
        /// 组织内码
        /// </summary>
        public string OrgId { get; set; }

        /// <summary>
        ///组织编码
        /// </summary>
        public string OrgNumber { get; set; }
    }
}
