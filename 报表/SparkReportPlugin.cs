using BOA.SBT.Report.PlugIns.Helper;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Contracts.Report;
using Kingdee.BOS.Core.Enums;
using Kingdee.BOS.Core.Report;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace BOA.SBT.Report.PlugIns
{
    [HotUpdate]
    [Description("spark_收汇情况表")]
    public class SparkReportPlugin : SysReportBaseService
    {
        public override void Initialize()
        {
            base.Initialize();
            this.ReportProperty.ReportType = ReportType.REPORTTYPE_NORMAL;
            this.ReportProperty.IsUIDesignerColumns = true;
            this.IsCreateTempTableByPlugin = true;
            var list = new List<DecimalControlField>();
            list.Add(new DecimalControlField
            {
                ByDecimalControlFieldName = "FAmount",
                DecimalControlFieldName = "FPRECISION"
            });
            list.Add(new DecimalControlField
            {
                ByDecimalControlFieldName = "F_BOA_QTY",
                DecimalControlFieldName = "FPRECISION"
            });
            this.ReportProperty.DecimalControlFieldList = list;
        }

        /// <summary>
        /// 设置报表头
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public override ReportTitles GetReportTitles(IRptParams filter)
        {
            ReportTitles titles = new ReportTitles();
            var customerFilter = filter.FilterParameter.CustomFilter;
            if (customerFilter != null)
            {
                var fABROADCUST1Info = customerFilter["FCustomer"] as DynamicObjectCollection;
                if (fABROADCUST1Info.Count > 0)//客户名称
                {
                    var str = string.Join(",", fABROADCUST1Info.Select(t => (t["FCustomer"] as DynamicObject)["Name"]));
                    titles.AddTitle("FCustomer", str);
                }


                //var billnos = customerFilter["FInvNo"] as DynamicObjectCollection;//发票号码
                //if (billnos.Count > 0)
                //{
                //    var billnoList = billnos.Select(t => (t["FInvNo"] as DynamicObject)["Name"]).ToList();
                //    var billnoSte = string.Join(",", billnoList);
                //    titles.AddTitle("F_BOA_InvNo", billnoSte);
                //}
                //var outContractNo = customerFilter["FoutContractNo"] as DynamicObjectCollection;//外销合同号
                //if (outContractNo.Count > 0)
                //{
                //    var billnoList = outContractNo.Select(t => (t["FoutContractNo"] as DynamicObject)["Number"]).ToList();
                //    var billnoSte = string.Join(",", billnoList);
                //    titles.AddTitle("FoutContractNo", billnoSte);
                //}

                if (customerFilter["FInvNoTxt"] != null)//发票号
                {
                    titles.AddTitle("F_BOA_InvNo", customerFilter["FInvNoTxt"].ToString());
                }
                if (customerFilter["FoutContractNo"] != null)//外销合同号
                {
                    titles.AddTitle("FoutContractNo", customerFilter["FoutContractNo"].ToString());
                }

                if (customerFilter["FDOCUMENTSTATUS"] != null && customerFilter["FDOCUMENTSTATUS"].ToString().Trim() != "")//单据状态
                {
                    titles.AddTitle("FDOCUMENTSTATUS", customerFilter["FDOCUMENTSTATUS"].ToString());
                }
                var fINANCIALCODE1Info = customerFilter["F_QIYI_FINANCIALCODE"] as DynamicObjectCollection;
                if (fINANCIALCODE1Info.Count > 0)//财务代码
                {
                    var str = string.Join(",", fINANCIALCODE1Info.Select(t => (t["F_QIYI_FINANCIALCODE"] as DynamicObject)["Name"]));
                    titles.AddTitle("F_QIYI_FINANCIALCODE", str);
                }
            }
            return titles;
        }

        public override void BuilderReportSqlAndTempTable(IRptParams filter, string tableName)
        {
            base.BuilderReportSqlAndTempTable(filter, tableName);

            var userFilter = string.Empty;
            var currentUserId = this.Context.UserId;
            var userAuthor = SqlHelper.GetUserAuthor(this.Context);
            var userGroup = userAuthor.Where(t => Convert.ToInt64(t["FUSERID"]) == currentUserId).ToList();
            if (userGroup.Count > 0)
            {
                var groupIdList = userGroup.Select(t => t["FOPERATORGROUPID"].ToString()).ToList();
                var groupIdStr = string.Join(",", groupIdList);
                userFilter = $" and t1.FSALEGROUPID in ({groupIdStr})";
            }

            var reqFilterStr = string.Empty;
            var customerFilter = filter.FilterParameter.CustomFilter;
            var F_BOA_ReQ = customerFilter["F_BOA_ReQ"];//收汇情况
            if (!F_BOA_ReQ.IsNullOrEmptyOrWhiteSpace())
            {
                if (F_BOA_ReQ.ToString() == "1")//已收汇
                {
                    reqFilterStr += $" and t.FWRITTENOFFSTATUS = 'C' ";
                }
                if (F_BOA_ReQ.ToString() == "2")//未收汇
                {
                    reqFilterStr += $" and t.FWRITTENOFFSTATUS = 'A' ";
                }
                if (F_BOA_ReQ.ToString() == "4")//部分收汇
                {
                    reqFilterStr += $" and t.FWRITTENOFFSTATUS = 'B' ";
                }
            }

            var filterStr = GetFilterStr(filter);
            this.KSQL_SEQ = string.Format(this.KSQL_SEQ, "t.fid");//设置临时表中一个自增字段FIDENTITYID
            var sqlQueryString = $@"/*dialect*/select *
,{this.KSQL_SEQ} into {tableName}
from  (
select t.fid,max(t.FDate) as FDate,max(t.FPRECISION) as FPRECISION
,max(t.FInvNo) as FInvNo,max(t.FSC) as FSC
,max(t.FDuedate) as FDuedate,max(t.FRemark) as FRemark,max(t.F_QIYI_Date) as F_QIYI_Date
,max(t.F_QIYI_FINANCIALCODE) as F_QIYI_FINANCIALCODE,max(t.FItemNo) as FItemNo
,max(t.FPONo) as FPONo,max(t.FConsignee) as FConsignee,max(t.FAmount) as FAmount
,max(t.FPaymentterm) as FPaymentterm,max(t.F_BOA_QTY) as F_BOA_QTY,max(t.F_BOA_ETD) as F_BOA_ETD
,max(t.F_CUSTOMERNUMBER) as F_BOA_CusNo
,max(isnull(treceive.FWRITTENOFFSTATUS,'A')) as FWRITTENOFFSTATUS
from (
select max(t1.FDate) as FDate,2 as FPRECISION,
t1.fid, max(t1.FDOCUMENTSTATUS) as FDOCUMENTSTATUS, max(t1.FSOURCEBILLNOES) as FInvNo,max(t1.FCONTRACTBILLNO) as FSC, --发票号,外销合同号
Convert(varchar(10),max(t1.F_QIYI_DATE1F_QIYI_YJSHDAY),120) as FDuedate,max(t1.FCONTENT) as FRemark,--预计收汇日期,备注
0 as FFare,Convert(varchar(10),max(t1.F_QIYI_Date),120) as F_QIYI_Date,max(t6.fmasterId) as F_QIYI_FINANCIALCODE,
stuff(
(select ','+tentry.FCUSNUMBER
 from TP_ES_ExchSettleEntry tentry
 where tentry.Fid = t1.fid and tentry.FCUSNUMBER <> ''
 group by tentry.FCUSNUMBER
 for xml path('')),1,1,'') as FItemNo,--客户货号
stuff(
(select ','+tentry.F_QIYI_TEXT
 from TP_ES_ExchSettleEntry tentry
 where tentry.Fid = t1.fid and tentry.F_QIYI_TEXT <> ''
 group by tentry.F_QIYI_TEXT
 for xml path('')),1,1,'') as FPONo,--客户合同号
case when max(t1.FABROADCUST) <> 0
then max(t4Hk.fname)
else max(t4l.fname) end as FConsignee, --客户名称
case when max(t1.FABROADCUST) <> 0
then max(t1.F_QIYI_HKFREBATEAMOUNT)
else max(t1.FBILLAMT) end as FAmount, --金额
case when max(t1.FABROADCUST) <> 0
then max(trlHk.fname)
else max(trl.fname) end as FPaymentterm, --付款条件
case when max(t1.FABROADCUST) <> 0
then max(t1.FABROADCUST)
else max(t1.FCUSID) end as FConsigneeId --客户内码
,max(t1subhead.FDECPIECESUM) as F_BOA_QTY --件数
,Convert(varchar(10),max(tst.FETDDATE),120) as F_BOA_ETD --ETD 
,max(t1.F_CUSTOMERNUMBER) as F_CUSTOMERNUMBER --客户发票号
from TP_ES_ExchSettle t1 --结汇单证
left join QIYI_t_Cust100055 t6 on t1.F_QIYI_FINANCIALCODE = t6.FID --财务代码
left join T_BD_CUSTOMER_L t4l on t1.FCUSID = t4l.FCUSTID and t4l.flocaleid = 2052 --客户
left join T_BD_CUSTOMER_L t4Hk on t1.FABROADCUST = t4Hk.FCUSTID and t4Hk.flocaleid = 2052--客户
left join T_BD_RecCondition_L trl on t1.F_QIYI_EXCHANGE = trl.fid and trl.flocaleid = 2052 --收款条件-多语言
left join T_BD_RecCondition_L trlHk on t1.F_QIYI_RECCONDITIONID = trlHk.fid and trlHk.flocaleid = 2052 --收款条件-多语言
left join TP_ES_ExchSettleSubHead t1subhead on t1.fid = t1subhead.fid --报关信息
left join TP_ES_StorageTrans tst on t1.FSOURCEBILLNOES = tst.FBILLNO --出运明细(储运托单)
where 1 = 1 {userFilter}
group by t1.fid
) t 

left join --收汇情况
 (
    select t3.FNAME as FInvNo,t2.fname --发票号，客户名称
    ,max(t1.FWRITTENOFFSTATUS) as FWRITTENOFFSTATUS --收款核销状态
    from t_AR_receivable t1 --应收单
    join T_BD_CUSTOMER_L t2 on t1.FCUSTOMERID = t2.FCUSTID and t2.flocaleid = 2052 --客户
    join V_TP_InvoiceView_L t3 on t1.F_QIYI_FINVOICEVIEW_H = t3.FID and t3.flocaleid = 2052--发票号
    group by t3.FNAME,t2.fname
 ) treceive on t.FInvNo = treceive.FInvNo and t.FConsignee = treceive.fname 
   and t.FInvNo <> '' and t.FConsignee <> ''

where 1=1 {filterStr}
group by t.fid

) t where 1=1 {reqFilterStr}";
            DBUtils.Execute(this.Context, sqlQueryString);
        }

        /// <summary>
        /// 过滤条件参数拼接
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        private string GetFilterStr(IRptParams filter)
        {
            var customerFilter = filter.FilterParameter.CustomFilter;
            //var custoemrFilterString = filter.FilterParameter.FilterString;
            var filterStr = string.Empty;
            var fABROADCUST1Info = customerFilter["FCustomer"] as DynamicObjectCollection;
            if (fABROADCUST1Info.Count > 0)//客户名称
            {
                var custIdList = fABROADCUST1Info.Select(t => (t["FCustomer"] as DynamicObject)["Name"]).ToList();
                var custIdListStr = string.Join("',N'", custIdList);
                filterStr += $" and t.FConsignee in (N'{custIdListStr}')";
            }

            //var billnos = customerFilter["FInvNo"] as DynamicObjectCollection;//发票号码
            //if (billnos.Count > 0)
            //{
            //    var billnoList = billnos.Select(t => (t["FInvNo"] as DynamicObject)["Name"]).ToList();
            //    var billnoSte = string.Join("','", billnoList);
            //    filterStr += $" and t.FInvNo in ('{billnoSte}')";
            //}
            //var outContractNo = customerFilter["FoutContractNo"] as DynamicObjectCollection;//外销合同号
            //if (outContractNo.Count > 0)
            //{
            //    var billnoList = outContractNo.Select(t => (t["FoutContractNo"] as DynamicObject)["Number"]).ToList();
            //    var billnoSte = string.Join("','", billnoList);
            //    filterStr += $" and t.FSC in ('{billnoSte}')";
            //}
            if (customerFilter["FInvNoTxt"] != null)//发票号
            {
                filterStr += $" and t.FInvNo like '%{customerFilter["FInvNoTxt"]}%'";
            }
            if (customerFilter["FoutContractNo"] != null)//外销合同号
            {
                filterStr += $" and t.FSC like '%{customerFilter["FoutContractNo"]}%'";
            }

            if (customerFilter["FDOCUMENTSTATUS"] != null && customerFilter["FDOCUMENTSTATUS"].ToString().Trim() != "")//单据状态
            {
                var strList = customerFilter["FDOCUMENTSTATUS"].ToString().Split(',');
                var strValue = string.Join("','", strList);
                filterStr += $" and t.FDOCUMENTSTATUS in ('{strValue}')";
            }
            var fINANCIALCODE = customerFilter["F_QIYI_FINANCIALCODE"] as DynamicObjectCollection;
            if (fINANCIALCODE.Count > 0)
            {
                var custIdList = fINANCIALCODE.Select(t => (t["F_QIYI_FINANCIALCODE"] as DynamicObject)["msterID"]);
                filterStr += $" and t.F_QIYI_FINANCIALCODE in ({string.Join(",", custIdList)})";
            }

            var startDate = customerFilter["F_BOA_StartDate"];
            if (startDate != null)
            {
                filterStr += $" and t.FDate >= '{startDate}'";
            }
            var endDate = customerFilter["F_BOA_EndDate"];
            if (endDate != null)
            {
                filterStr += $" and t.FDate <= '{endDate}'";
            }
           
            return filterStr;
        }

        /// <summary>
        /// 设置汇总行，只有显示财务信息时才需要汇总
        /// </summary>
        /// <param name="filter"></param>
        /// <returns></returns>
        public override List<SummaryField> GetSummaryColumnInfo(IRptParams filter)
        {
            List<SummaryField> summaryList = new List<SummaryField>();
            summaryList.Add(new SummaryField("FAmount", BOSEnums.Enu_SummaryType.SUM));
            summaryList.Add(new SummaryField("F_BOA_QTY", BOSEnums.Enu_SummaryType.SUM));
            return summaryList;
        }
    }
}
