using BOA.SR.ContractReportPlugin.Dtos;
using BOA.SR.ContractReportPlugin.Helper;
using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Contracts.Report;
using Kingdee.BOS.Core.Enums;
using Kingdee.BOS.Core.Report;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Kingdee.K3.Core.MFG.EntityHelper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;

namespace BOA.SR.ContractReportPlugin.Report
{
    [HotUpdate]
    [Description("总账账龄分析表(定制版)")]
    public class AgingScheduleReport : SysReportBaseService
    {
        /// <summary>
        /// 1122.01.01科目的下级科目余额
        /// </summary>
        private List<string> accountList1 = new List<string>();

        /// <summary>
        /// 1122.01.02科目的下级科目余额
        /// </summary>
        private List<string> accountList2 = new List<string>();

        public override void Initialize()
        {
            base.Initialize();
            this.ReportProperty.ReportType = ReportType.REPORTTYPE_NORMAL;
            this.ReportProperty.IsUIDesignerColumns = false;
            this.IsCreateTempTableByPlugin = true;
            //this.ReportProperty.IsGroupSummary = true;
        }

        public override ReportHeader GetReportHeaders(IRptParams filter)
        {
            ReportHeader header = new ReportHeader();
            header.AddChild("FCustmer", new LocaleValue("客户"));
            header.AddChild("FDept", new LocaleValue("部门"));
            header.AddChild("FSaler", new LocaleValue("销售员"));
            header.AddChild("FAdvance", new LocaleValue("预收"), SqlStorageType.SqlDecimal);
            header.AddChild("FBalance", new LocaleValue("余额"), SqlStorageType.SqlDecimal);
            header.AddChild("FInvoiced", new LocaleValue("已开票"), SqlStorageType.SqlDecimal);
            header.AddChild("FUninvoiced", new LocaleValue("未开票"), SqlStorageType.SqlDecimal);
            var days = filter.FilterParameter.CustomFilter["AgingGroupEntity"] as DynamicObjectCollection;
            foreach (var day in days)
            {
                var fieldName = day["Section"].ToString();
                var fieldKey = $"F{day["Days"]}";
                header.AddChild(fieldKey, new LocaleValue(fieldName), SqlStorageType.SqlDecimal);
            }
            return header;
        }

        public override ReportTitles GetReportTitles(IRptParams filter)
        {
            ReportTitles titles = new ReportTitles();
            var customerFilter = filter.FilterParameter.CustomFilter;
            if (customerFilter != null)
            {
                titles.AddTitle("F_BOA_BOOKTITLE", (customerFilter["Book"] as DynamicObject)["Name"].ToString());
                titles.AddTitle("F_BOA_DEADLINETITLE", customerFilter["DeadLine"].ToString().Substring(0, 10));
            }
            return titles;
        }

        /// <summary>
        /// 构造取数Sql，取数据填充到临时表：tableName
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="tableName"></param>
        public override void BuilderReportSqlAndTempTable(IRptParams filter, string tableName)
        {
            base.BuilderReportSqlAndTempTable(filter, tableName);
            var customerFilter = filter.FilterParameter.CustomFilter;
            var sysReportData = new SysReportHelper(this.Context).GetAgingScheduleReportData(customerFilter);
            if (sysReportData.Count == 0)//未查询到数据
            {
                var sqlStr1 = SqlStringNull(filter, tableName);
                DBUtils.Execute(this.Context, sqlStr1);
                return;
            }
            //根据部门和客户分组
            var groupList = sysReportData
                           .GroupBy(t => new
                           {
                               customerName = t.Flex6Name,
                               deptName = t.Flex5Name
                           })
                           .Select(t => new CusDeptDto
                           {
                               CustomerName = t.Key.customerName,
                               DeptName = t.Key.deptName
                           }).ToList();

            //var customers = sysReportData.Select(t => t.Flex6Name).Distinct().ToList();//客户名称集合
            var orgId = (customerFilter["Book"] as DynamicObject)["AccountOrgID_Id"].ToString();//组织内码
            var DeptAndSaleInfos = GetDeptAndSaleByCust(groupList, orgId);

            accountList1 = GetAccount01();
            accountList2 = GetAccount02();
            var sqlStr = string.Empty;
            for (var i = 0; i < groupList.Count; i++)
            {
                var groupItem = groupList[i];//客户和部门名称
                var customerName = groupItem.CustomerName;//客户
                var deptName = groupItem.DeptName;//部门
                var deptAndSaleInfo = DeptAndSaleInfos
                                     .FirstOrDefault(t => t["cusName"].ToString() == customerName
                                                && t["deptName"].ToString() == deptName);
                var saleName = "";//销售员
                if (deptAndSaleInfo != null)
                {
                    saleName = deptAndSaleInfo["saleName"].ToString();
                }
                var amountInfos = sysReportData
                                 .Where(t => t.Flex6Name == customerName
                                        && t.Flex5Name == deptName)
                                 .ToList();
                sqlStr += SqlStrSubstring(filter, i, customerName, deptName, saleName, amountInfos);
            }
            if (sqlStr != string.Empty)
            {
                sqlStr = sqlStr.Substring(0, sqlStr.Length - 10);
                sqlStr = $"select * into {tableName} from ({sqlStr}) t";
            }
            else
            {
                sqlStr = SqlStringNull(filter, tableName);
            }
            DBUtils.Execute(this.Context, sqlStr);
        }

        /// <summary>
        /// 设置汇总行
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public override List<SummaryField> GetSummaryColumnInfo(IRptParams filter)
        {
            List<SummaryField> summaryList = new List<SummaryField>
            {
                new SummaryField("FAdvance", BOSEnums.Enu_SummaryType.SUM),
                new SummaryField("FBalance", BOSEnums.Enu_SummaryType.SUM),
                new SummaryField("FInvoiced", BOSEnums.Enu_SummaryType.SUM),
                new SummaryField("FUninvoiced", BOSEnums.Enu_SummaryType.SUM)
            };
            var days = filter.FilterParameter.CustomFilter["AgingGroupEntity"] as DynamicObjectCollection;
            foreach (var day in days)
            {
                var fieldKey = $"F{day["Days"]}";
                summaryList.Add(new SummaryField(fieldKey, BOSEnums.Enu_SummaryType.SUM));
            }
            return summaryList;
        }

        /// <summary>
        /// 根据客户和部门，获取销售员
        /// </summary>
        /// <param name="groupList">客户和部门</param>
        /// <param name="orgId">组织内码</param>
        /// <returns></returns>
        private DynamicObjectCollection GetDeptAndSaleByCust(List<CusDeptDto> groupList, string orgId)
        {
            var filter = string.Empty;
            foreach (var item in groupList)
            {
                filter += $"(t2.fname = '{item.CustomerName}' and t4.fname = '{item.DeptName}') or";
            }
            if (filter != string.Empty)
            {
                filter = $" and ({filter.SubStr(0, filter.Length - 2)})";
            }

            var sqlStr = $@"/*dialect*/select t.cusName,t.saleName,t.deptName
from (
select t2.fname as cusName,t3.fname as saleName,t4.fname as deptName
,row_Number() Over(PARTITION BY t2.fname,t3.fname,t4.fname order by t1.FDATE desc) as seqRow
from T_SAL_ORDER t1 --销售订单
left join T_BD_CUSTOMER_L t2 on t1.FCUSTID = t2.FCUSTID and t2.FLOCALEID = 2052 --客户
left join V_BD_SALESMAN_L t3 on t1.FSALERID = t3.FID and t3.FLOCALEID = 2052 --销售员
left join T_BD_DEPARTMENT_L t4 on t1.FSALEDEPTID = t4.FDEPTID and t4.FLOCALEID = 2052 --部门
where t1.FSALEORGID = {orgId}{filter}
) t where t.seqRow = 1 group by t.cusName,t.saleName,t.deptName";
            return DBUtils.ExecuteDynamicObject(this.Context, sqlStr);
        }

        /// <summary>
        /// sql语句拼接
        /// </summary>
        /// <param name="i">FIDENTITYID 值</param>
        /// <param name="customerName">客户</param>
        /// <param name="deptName">部门</param>
        /// <param name="saleName">销售员</param>
        /// <returns></returns>
        private string SqlStrSubstring(IRptParams filter, int i, string customerName, string deptName, string saleName
            , List<AgingScheduleDto> amountDataInfos)
        {
            //var fAdvance = 0m;//预收
            //var fBalance = 0m;//余额
            //var fInvoiced = 0m;//已开票
            //var fUninvoiced = 0m;//未开票
            var amountDataInfosW = amountDataInfos.Where(t => t.FACCTNUMBER != "2203").ToList();
            var fAdvance = amountDataInfos.Where(t => t.FACCTNUMBER == "2203").Sum(t => t.FRESERVED);//预收
            var fBalance = amountDataInfos.Sum(t => t.FRESERVED) - fAdvance;//余额
            var fInvoiced = amountDataInfos.Where(t => accountList1.Contains(t.FACCTNUMBER)).Sum(t => t.FRESERVED);//已开票
            var fUninvoiced = amountDataInfos.Where(t => accountList2.Contains(t.FACCTNUMBER)).Sum(t => t.FRESERVED);//未开票
            var sqlStr = $@"select {i + 1} as FIDENTITYID";
            sqlStr += $",'{customerName}' as FCustmer";
            sqlStr += $",'{deptName}' as FDept";
            sqlStr += $",'{saleName}' as FSaler";
            sqlStr += $",{fAdvance} as FAdvance";
            sqlStr += $",{fBalance} as FBalance";
            sqlStr += $",{fInvoiced} as FInvoiced";
            sqlStr += $",{fUninvoiced} as FUninvoiced";
            var days = filter.FilterParameter.CustomFilter["AgingGroupEntity"] as DynamicObjectCollection;
            foreach (var day in days)
            {
                var fieldKey = $"F{day["Days"]}";
                var amount = 0m;//金额汇总
                foreach (var item in amountDataInfosW)
                {
                    var dic = item.Days;
                    amount += dic[fieldKey];
                }
                sqlStr += $",{amount} as {fieldKey}";
            }
            return sqlStr += " \nunion all\n";
        }

        /// <summary>
        /// 获取 1122.01.01科目的下级科目余额
        /// </summary>
        /// <returns></returns>
        private List<string> GetAccount01()
        {
            var sqlStr = "select FNUMBER from T_BD_ACCOUNT where FNUMBER like '1122.01.01.%'";
            var result = DBUtils.ExecuteDynamicObject(this.Context, sqlStr);
            return result.Select(t => t["FNUMBER"].ToString()).ToList();
        }

        /// <summary>
        /// 获取 1122.01.02科目的下级科目余额
        /// </summary>
        /// <returns></returns>
        private List<string> GetAccount02()
        {
            var sqlStr = "select FNUMBER from T_BD_ACCOUNT where FNUMBER like '1122.01.02.%'";
            var result = DBUtils.ExecuteDynamicObject(this.Context, sqlStr);
            return result.Select(t => t["FNUMBER"].ToString()).ToList();
        }

        /// <summary>
        /// 未查询到数据sql拼接
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        private string SqlStringNull(IRptParams filter, string tableName)
        {
            var sqlStr = $@"select * into #tempTable from ( select 1 as FIDENTITYID";
            sqlStr += ",'' as FCustmer";
            sqlStr += ",'' as FDept";
            sqlStr += ",'' as FSaler";
            sqlStr += ",0 as FAdvance";
            sqlStr += ",0 as FBalance";
            sqlStr += ",0 as FInvoiced";
            sqlStr += ",0 as FUninvoiced";
            var days = filter.FilterParameter.CustomFilter["AgingGroupEntity"] as DynamicObjectCollection;
            foreach (var day in days)
            {
                var fieldKey = $"F{day["Days"]}";
                sqlStr += $",0 as {fieldKey}";
            }
            sqlStr += ") t;\n";
            sqlStr += $@"delete #tempTable where FIDENTITYID = 1;
select * into {tableName} from #tempTable";
            return sqlStr;
        }
    }
}
