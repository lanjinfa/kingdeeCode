using Kingdee.BOS.Orm.DataEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.FQGT.ProfitReport.BusinessPlugIn.Common
{
    /// <summary>
    /// 过滤条件
    /// </summary>
    public class FilterCondition
    {
        /// <summary>
        /// 过滤条件
        /// </summary>
        public string[] Filter { get; set; }

        /// <summary>
        /// 组织，用于显示
        /// </summary>
        public string[] OrgIds { get; set; }

        /// <summary>
        /// 期间，用于显示
        /// </summary>
        public string Period { get; set; }

        /// <summary>
        /// 单据名称类型
        /// 1=营业成本内代，2=营业成本外代，3=营业收入-下脚料收入
        /// 4=营业成本-下脚料收入，5=销售退回，6=包装皮
        /// </summary>
        public int BillType { get; set; }
    }
}
