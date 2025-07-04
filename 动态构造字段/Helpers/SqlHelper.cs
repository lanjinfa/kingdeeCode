using BOA.YD.JYFX.PlugIns.Dtos;
using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.YD.JYFX.PlugIns.Helpers
{
    public static class SqlHelper
    {
        /// <summary>
        /// 获取收料通知单数据
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="filter">过滤条件</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetReceiveList(Context context, FilterDto filter)
        {
            var filterStr = "t1.FDOCUMENTSTATUS = 'C'";
            if (!filter.BussinessDate.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t1.FDATE>='{filter.BussinessDate}'";
            }
            if (!filter.ToDate.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t1.FDATE<='{filter.ToDate}'";
            }
            if (!filter.SupplierBillNo.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t2.F_BOA_SUPPLIERNO like '%{filter.SupplierBillNo}%'";
            }
            if (!filter.CheckStatus.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t2.F_BOA_CHECKSTATUS in ({filter.CheckStatus})";
            }
            if (!filter.SupplierId.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t1.FSUPPLIERID in ({filter.SupplierId})";
            }
            if (!filter.MaterialId.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t2.FMATERIALID in ({filter.MaterialId})";
            }
            if (!filter.OrgId.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t1.FSTOCKORGID = {filter.OrgId}";
            }
            if (!filter.ShipmentNo.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t1.F_BOA_SHIPMENTNO like '%{filter.ShipmentNo}%'";
            }
            var sqlStr = $@"select t1.FBILLNO,t1.FDATE,t1.FSUPPLIERID,--单据编号，收料日期，供应商
t2.FMATERIALID,t2.FACTRECEIVEQTY,t3.FTAXPRICE, --物料，交货数量,含税单价
t3.FAMOUNT,t3.FALLAMOUNT,t3.FTAXRATE,t3.FTAXAMOUNT, --金额，价税合计，税率，税额
t2.FGIVEAWAY,t2.F_BOA_SUPPLIERNO,t1.F_BOA_SHIPMENTNO, --是否赠品，供应商单号，发运单号
t2.fseq,t1.FID,t2.FENTRYID,t2.F_BOA_CHECKQTY,t2.F_BOA_CHECKSTATUS --序号，单据内码，分录内码，核销数量，核销状态
from T_PUR_Receive t1 --收料通知单
join T_PUR_ReceiveEntry t2 on t1.FID = t2.FID --收料通知单明细
join T_PUR_ReceiveEntry_F t3 on t2.FENTRYID = t3.FENTRYID --收料通知单明细-财务信息
where {filterStr}
order by t1.FDATE desc,t1.FBILLNO desc,t2.fseq asc";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 获取采购入库单数据
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="filter">过滤条件</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetInStockList(Context context, FilterDto filter)
        {
            var filterStr = "t1.FDOCUMENTSTATUS = 'C'";
            if (!filter.BussinessDate.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t1.FDATE>='{filter.BussinessDate}'";
            }
            if (!filter.ToDate.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t1.FDATE<='{filter.ToDate}'";
            }
            if (!filter.SupplierBillNo.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t2.F_BOA_TRACKENUMBER like '%{filter.SupplierBillNo}%'";
            }
            if (!filter.CheckStatus.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t2.F_BOA_CHECKSTATUS in ({filter.CheckStatus})";
            }
            if (!filter.SupplierId.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t1.FSUPPLIERID in ({filter.SupplierId})";
            }
            if (!filter.MaterialId.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t2.FMATERIALID in ({filter.MaterialId})";
            }
            if (!filter.OrgId.IsNullOrEmptyOrWhiteSpace())
            {
                filterStr += $" and t1.FSTOCKORGID = {filter.OrgId}";
            }
            var sqlStr = $@"select t1.FBILLNO,t1.FDATE,t1.FSUPPLIERID,--单据编号，入库日期，供应商
t2.FMATERIALID,t2.FREALQTY,t3.FTAXPRICE, --物料，实收数量,含税单价
t3.FAMOUNT,t3.FALLAMOUNT,t3.FTAXRATE,t3.FTAXAMOUNT, --金额，价税合计，税率，税额
t2.FGIVEAWAY,t2.F_BOA_TRACKENUMBER, --是否赠品，供应商单号
t2.fseq,t1.FID,t2.FENTRYID,t2.F_BOA_CHECKQTY,t2.F_BOA_CHECKSTATUS --行号，单据内码，分录内码，核销数量，核销状态
from t_STK_InStock t1 --采购入库单
join T_STK_INSTOCKENTRY t2 on t1.FID = t2.FID --采购入库单明细
join T_STK_INSTOCKENTRY_F t3 on t2.FENTRYID = t3.FENTRYID --采购入库单明细-财务信息
where {filterStr}
order by t1.FDATE desc,t1.FBILLNO desc,t2.fseq asc";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 更新对应单据分录上的核销信息
        /// </summary>
        /// <param name="receiveSqlList">收料通知单</param>
        /// <param name="inStockSqlList">采购入库单</param>
        public static void BillCheckLogRecord(Context context, List<string> receiveSqlList, List<string> inStockSqlList)
        {
            if (receiveSqlList.Count > 0)
            {
                DBUtils.ExecuteBatch(context, receiveSqlList, 100);
            }
            if (inStockSqlList.Count > 0)
            {
                DBUtils.ExecuteBatch(context, inStockSqlList, 100);
            }
        }

        /// <summary>
        /// 根据收料通知单内码和分录内码，获取与之对应核销的采购入库单内码和分录内码--核销记录
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="billId">收料通知单内码</param>
        /// <param name="entryId">收料通知单分录内码</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetCheckedInfoById(Context context, string billId, string entryId)
        {
            var sqlStr = $@"select F_BOA_INSTOCKBILLID,F_BOA_INSTOCKENTRYID from T_BOA_PurCheckLog 
where F_BOA_RECEIVEBILLID = {billId} and F_BOA_RECEIVEENTRYID = {entryId}";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 根据采购入库单内码和分录内码获取收料通知单内码和分录内码--核销记录
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="inStockBillInfo">采购入库单信息</param>
        /// <param name="receiveEntryId">需过滤的收料通知单分录内码</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetCheckReceiveInfoById(Context context, DynamicObjectCollection inStockBillInfo, List<string> receiveEntryId)
        {
            var entryId = string.Join(",", inStockBillInfo.Select(t => t["F_BOA_INSTOCKENTRYID"].ToString()));//采购入库单分录集合
            var receiveEntryIdStr = string.Join(",", receiveEntryId);
            var sqlStr = $@"select t1.F_BOA_RECEIVEBILLID,t1.F_BOA_RECEIVEENTRYID,--收料通知单内码，收料通知单分录内码
t1.F_BOA_CHECKQTY,t2.F_BOA_CHECKQTY as SumCheckQty,t2.FACTRECEIVEQTY, --核销数量，总核销数量，交货数量
t1.F_BOA_INSTOCKBILLID,t1.F_BOA_INSTOCKENTRYID --采购入库单内码,采购入库单分录内码
from T_BOA_PurCheckLog t1
join T_PUR_ReceiveEntry t2 on t1.F_BOA_RECEIVEENTRYID = t2.FENTRYID
where F_BOA_INSTOCKENTRYID in ({entryId}) and  t1.F_BOA_RECEIVEENTRYID not in ({receiveEntryIdStr})";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 删除核销记录语句
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="deleteSqlList">删除语句集合</param>
        public static void DeleteCheckLog(Context context, List<string> deleteSqlList)
        {
            if (deleteSqlList.Count > 0)
            {
                DBUtils.ExecuteBatch(context, deleteSqlList, 100);
            }
        }

        /// <summary>
        /// 根据单据类型获取单据生成配置信息
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="billType">单据类型</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetSyncFieldMapList(Context context, string billType)
        {
            var sqlStr = $@"select t1.F_BOA_SYNCBILL, t1.F_BOA_HeadProp,t1.F_BOA_EntityProp,t2.*
from T_BOA_BillCreateConfigure t1
join T_BOA_BillCreateConEntry t2 on t1.FID = t2.FID
where t1.F_BOA_SYNCBILL = '{billType}' and t1.FDOCUMENTSTATUS = 'C'
and t1.FFORBIDSTATUS = 'A'
order by t2.FSeq asc";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 获取api配置信息
        /// </summary>
        /// <param name="context">上下文</param>
        /// <returns></returns>
        public static DynamicObject GetSyncApiConfigure(Context context)
        {
            var sqlStr = @"select top 1 F_BOA_USERNAME,F_BOA_APIURL,F_BOA_ACCOUNTID,F_BOA_PASSWORD from T_BOA_ApiSyncConfig
where FDOCUMENTSTATUS = 'C' and FFORBIDSTATUS = 'A'";
            return DBUtils.ExecuteDynamicObject(context, sqlStr).FirstOrDefault();
        }

        /// <summary>
        /// 获取组织对应表
        /// </summary>
        /// <param name="context">上下文</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetOrgConfig(Context context)
        {
            var sqlStr = @"select t1.F_BOA_ORGID,t1.F_BOA_DEPARTMENTID,t1.F_BOA_CORPORGID,
isnull(t3.FNUMBER,'') as F_BOA_CORPORGNumber
from T_BOA_OrgConfigureEntry t1
join T_BOA_OrgConfigure t2 on t1.FID = t2.FID
left join BOA_t_Cust100005 t3 on t3.FID = t1.F_BOA_CORPORGID --发票抬头
where t2.FFORBIDSTATUS = 'A' and t2.FDOCUMENTSTATUS = 'C'
order by t1.FSeq asc";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 获取仓库对应表
        /// </summary>
        /// <param name="context">上下文</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetStockConfig(Context context)
        {
            var sqlStr = @"select t1.F_BOA_STOCKID,t1.F_BOA_LEGALSTOCKNUMBER
from T_BOA_StockConfigEntry t1
join T_BOA_StockConfigure t2 on t1.FID = t2.FID
where t2.FFORBIDSTATUS = 'A' and t2.FDOCUMENTSTATUS = 'C'";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 获取部门对应表
        /// </summary>
        /// <param name="context">上下文</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetDepartmentConfig(Context context)
        {
            var sqlStr = @"select t1.F_BOA_DEPARTMENTID,t1.F_BOA_LEGALDEPTNUMBER,t1.F_BOA_CORPORGID --部门，法人部门，法人组织内码
,isnull(t3.fnumber,'') as deptNumber,isnull(t4.fnumber,'') as corpOrgNumber --部门编码，法人组织编码
from T_BOA_DeptConfigEntry t1
join T_BOA_LegalDeptConfig t2 on t1.FID = t2.FID
left join T_BD_DEPARTMENT t3 on t1.F_BOA_DEPARTMENTID = t3.FDEPTID --部门
left join BOA_t_Cust100005 t4 on t1.F_BOA_CORPORGID = t4.fid --发票抬头
where t2.FFORBIDSTATUS = 'A' and t2.FDOCUMENTSTATUS = 'C'";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 反写同步单据上的同步信息/批量执行语句
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="sqlStrList">更新语句集合</param>
        public static void UpdateBillSyncStatus(Context context, List<string> sqlStrList)
        {
            if (sqlStrList.Count > 0)
            {
                DBUtils.ExecuteBatch(context, sqlStrList, 100);
            }
        }

        /// <summary>
        /// 构造更新同步信息语句
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entryInfo">分录信息</param>
        /// <param name="billEntryTableName">单据分录对应表名</param>
        /// <returns></returns>
        public static List<string> CreateUpdateSqlStr(Context context, List<DynamicObject> entryInfo, string billEntryTableName)
        {
            var sqlStrList = new List<string>();
            foreach (var item in entryInfo)
            {
                var entryId = item["F_BOA_SrcEntryId"].ToString();//单据分录内码
                var sqlStr = $@"update {billEntryTableName} 
set F_BOA_Synced = 1,F_BOA_SyncerId = {context.UserId},F_BOA_SyncDate = '{DateTime.Now}'
where fentryId = {entryId}";
                sqlStrList.Add(sqlStr);
            }
            return sqlStrList;
        }

        /// <summary>
        /// 构造更新同步信息语句(销售出库单)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entryInfo"></param>
        /// <returns></returns>
        public static List<string> CreateUpdateSqlStrOutStock(Context context, List<DynamicObject> entryInfo,
            SyncApiResult syncApiResult, DynamicObject billInfo)
        {
            var sqlStrList = new List<string>();
            var needReturnData = JsonConvert.DeserializeObject<List<SaleOutNeedReturnData>>(syncApiResult.NeedReturnData);
            var entryData = billInfo["SAL_OUTSTOCKENTRY"] as DynamicObjectCollection;
            foreach (var item in entryInfo)
            {
                var entryId = item["F_BOA_SrcEntryId"].ToString();//单据分录内码
                var qty = needReturnData.First().FEntity.Where(t => t.F_BOA_EntryId == entryId).Sum(t => t.FRealQty);
                var billQty = entryData.Where(t => t["Id"].ToString() == entryId).Sum(t => Convert.ToDecimal(t["RealQty"]));
                if (billQty <= qty)
                {
                    var sqlStr = $@"update T_SAL_OUTSTOCKENTRY 
set F_BOA_Synced = 1,F_BOA_SyncerId = {context.UserId},F_BOA_SyncDate = '{DateTime.Now}'
where fentryId = {entryId}";
                    sqlStrList.Add(sqlStr);
                }
            }
            return sqlStrList;
        }

        /// <summary>
        /// 构造更新同步信息语句--分摊单据--销售出库单
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entryInfo">分录信息</param>
        /// <param name="billEntryTableName">单据分录对应表名</param>
        /// <returns></returns>
        public static List<string> CreateUpdateSqlStrDiv(Context context, List<DynamicObject> entryInfo, string billEntryTableName)
        {
            var sqlStrList = new List<string>();
            foreach (var item in entryInfo)
            {
                var entryId = item["F_BOA_EntryId"].ToString();//单据分录内码
                var sqlStr = $@"update {billEntryTableName} 
set F_BOA_Synced = 1,F_BOA_SyncerId = {context.UserId},F_BOA_SyncDate = '{DateTime.Now}'
where fentryId = {entryId}";
                sqlStrList.Add(sqlStr);
            }
            return sqlStrList;
        }

        /// <summary>
        /// 构造更新同步信息语句--分摊单据
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entryInfo">分录信息</param>
        /// <param name="divBillTypeName">分摊单据类型名称</param>
        /// <returns></returns>
        public static List<string> CreateUpdateSqlStrDivOther(Context context, List<DivideDto> entryInfo, string divBillTypeName)
        {
            if (entryInfo.Count == 0)
            {
                return new List<string>();
            }
            var sqlStrList = new List<string>();
            var billEntryTableName = "T_SAL_OUTSTOCKENTRY";
            if (divBillTypeName == "其他出库单")
            {
                billEntryTableName = "T_STK_MISDELIVERYENTRY";
            }
            else if (divBillTypeName == "盘亏单")
            {
                billEntryTableName = "T_STK_STKCOUNTLOSSENTRY";
            }
            foreach (var item in entryInfo)
            {
                var entryId = item.EntryId;//单据分录内码
                var sqlStr = $@"update {billEntryTableName} 
set F_BOA_Synced = 1,F_BOA_SyncerId = {context.UserId},F_BOA_SyncDate = '{DateTime.Now}'
where fentryId = {entryId}";
                sqlStrList.Add(sqlStr);
            }
            return sqlStrList;
        }

        /// <summary>
        /// 根据客户内码获取集团客户编码
        /// </summary>
        /// <param name="context"></param>
        /// <param name="custId"></param>
        /// <returns></returns>
        public static string GetGroupCustNumberByCustId(Context context, long custId)
        {
            var sqlStr = $@"select t2.FNUMBER
from T_BD_CUSTOMER t1
left join T_BD_CUSTOMER t2 on t1.FGROUPCUSTID = t2.FCUSTID
where t1.FCUSTID = {custId}";
            return DBUtils.ExecuteScalar(context, sqlStr, "");
        }

        /// <summary>
        /// 根据物料内码获取物料相关单位信息
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="materialId">物料内码</param>
        /// <returns></returns>
        public static DynamicObject GetMaterialInfoById(Context context, string materialId)
        {
            var sqlStr = $@"select t1.FNUMBER,t5.FBASEUNITID,t2.FSTOREUNITID, --基本单位，库存单位
t3.FSALEPRICEUNITID,t4.FPURCHASEPRICEUNITID, --销售计价单位，采购计价单位
t1.F_BOA_BOXCODE,t1_l.FName,t1.F_BOA_CWFNUMBER,t1.F_BOA_CWFNAME --原箱代码，原箱名称，财务编码，财务名称
from T_BD_MATERIAL t1 
join T_BD_MATERIAL_L t1_l on t1.FMATERIALID = t1_l.FMATERIALID and t1_l.FLOCALEID = 2052
join t_BD_MaterialStock t2 on t1.FMATERIALID = t2.FMATERIALID
join t_BD_MaterialSale t3 on t1.FMATERIALID = t3.FMATERIALID
join t_bd_MaterialPurchase t4 on t1.FMATERIALID = t4.FMATERIALID
join t_BD_MaterialBase t5 on t1.FMATERIALID = t5.FMATERIALID
where t1.FMATERIALID = {materialId}";
            return DBUtils.ExecuteDynamicObject(context, sqlStr).FirstOrDefault();
        }

        /// <summary>
        /// 用于判断付款单上游单据是否已同步
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entryIdStr">付款单明细分录内码</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetExpReimbursementLinkInfo(Context context, string entryIdStr)
        {
            var sqlStr = $@"select t1.F_BOA_BUSINESSTYPE,isnull(t2_B.FNUMBER,'') as accountNumber,
isnull(t4.F_BOA_TARGETBILLID,0) as targetBillId,isnull(t2_C.FNUMBER,'') as cashNumber,
isnull(t4.F_BOA_TARGETENTRYID,0) as targetEntryId,t3.FSrcBillNo,t3.FSrcRowId,t3.FSrcBillId,
isnull(t3_aL.FName,'') as baccountNumber,t2.FHANDLINGCHARGEFOR --科目，手续费
,t2.FREALPAYAMOUNTFOR,t2.FPAYTOTALAMOUNTFOR --表体-实付金额,表体-应付金额
,t3.FREALPAYAMOUNT --本次付款金额
from T_AP_PAYBILL t1 --付款单
join T_AP_PAYBILLENTRY t2 on t1.FID = t2.FID --付款单明细
join T_AP_PAYBILLSRCENTRY t3 on t1.FID = t3.FID --付款单源单明细  and t2.FSeq = t3.FSeq
left join T_CN_BANKACNT t2_B on t2.FACCOUNTID = t2_B.FBANKACNTID --银行账号
left join T_CN_CASHACCOUNT t2_C on t2.FCASHACCOUNT = t2_C.FID --现金账号
left join T_BD_ACCOUNT t3_a on t3.F_BOA_Subject1 = t3_a.FACCTID --科目
left join T_BD_ACCOUNT_L t3_aL on t3_a.FACCTID = t3_aL.FACCTID and t3_aL.FLOCALEID = 2052 --科目多语言
left join (
 select t1.F_BOA_SRCBILLID,t2.F_BOA_SRCENTRYID,
 t1.F_BOA_TARGETBILLID,t2.F_BOA_TARGETENTRYID
 from T_BOA_SyncLog t1
 join T_BOA_SyncLogEntry t2 on t1.FID = t2.FID
 where t1.F_BOA_BILLTYPE = '费用报销单' and t1.F_BOA_ISSUCCESS = 1
 and t1.F_BOA_ISDISUSE = 0
) t4 on t3.FSrcBillId = t4.F_BOA_SRCBILLID and t3.FSrcRowId = t4.F_BOA_SRCENTRYID 
where t3.FSourceType = 'ER_ExpReimbursement' and t2.FEntryId in ({entryIdStr})";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 根据单据内码和分录内码获取销售出库单已同步的数量
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="billIdList">单据内码</param>
        /// <param name="entryIdList">分录内码</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetSyncedQty(Context context, string[] billIdList, List<string> entryIdList)
        {
            var billIdStr = string.Join(",", billIdList);
            var entryIdStr = string.Join(",", entryIdList);
            var sqlStr = $@"select t1.F_BOA_SRCBILLID,t2.F_BOA_SRCENTRYID,sum(t2.F_BOA_QTY) as Qty
from T_BOA_SyncLog t1
join T_BOA_SyncLogEntry t2 on t1.FID = t2.FID
where t1.F_BOA_SRCBILLID in ({billIdStr}) and t2.F_BOA_SRCENTRYID in ({entryIdStr})
and t1.F_BOA_ISSUCCESS = 1 and F_BOA_ISDISUSE = 0
group by t1.F_BOA_SRCBILLID,t2.F_BOA_SRCENTRYID";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 根据客户内码获取需要同步的客户编码
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="customerIdList">客户内码集合</param>
        /// <returns></returns>
        public static List<CustomerDto> GetCustomerNumberById(Context context, List<string> customerIdList)
        {
            var customerIdStr = string.Join(",", customerIdList);
            var sqlStr = $@"select isnull(t2.FNUMBER,'') as corpNumber,t1.F_BOA_CWZCUSTOMERNO as ticketNumber,
t1.FNUMBER,t1.FCUSTID
from T_BD_CUSTOMER t1
left join T_BD_CUSTOMER t2 on t1.FGROUPCUSTID = t2.FCUSTID
where t1.FCUSTID in ({customerIdStr})";
            var customerInfoList = DBUtils.ExecuteDynamicObject(context, sqlStr);
            var customerDtoList = new List<CustomerDto>();
            if (customerInfoList.Count > 0)
            {
                foreach (var item in customerInfoList)
                {
                    var cusId = item["FCUSTID"].ToString();
                    var corpNumber = item["corpNumber"].ToString();
                    var ticketNumber = item["ticketNumber"].ToString().Trim();
                    var number = item["FNUMBER"].ToString().Trim();
                    var customerDto = new CustomerDto
                    {
                        Id = cusId,
                        Number = number
                    };
                    if (corpNumber != "")
                    {
                        customerDto.Number = corpNumber;
                    }
                    else if (ticketNumber != "")
                    {
                        customerDto.Number = ticketNumber;
                    }
                    customerDtoList.Add(customerDto);
                }
            }
            return customerDtoList;
        }

        /// <summary>
        /// 获取单位换算信息
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="materialId">物料内码</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetUnitInfo(Context context, string materialId)
        {
            var sqlStr = $@"select FCONVERTDENOMINATOR,FCURRENTUNITID,--换算单位1
FCONVERTNUMERATOR,FDESTUNITID --换算单位2
from T_BD_UNITCONVERTRATE where FMATERIALID = {materialId}";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 获取物料财务名称
        /// </summary>
        /// <param name="context"></param>
        /// <param name="materialId"></param>
        /// <returns></returns>
        public static string GetMaterialNameById(Context context, string materialId)
        {
            var sqlStr = $"select F_BOA_CWFNAME from T_BD_MATERIAL where FMATERIALID = {materialId}";
            return DBUtils.ExecuteScalar(context, sqlStr, "");
        }

        /// <summary>
        /// 其他应付单/其他应收单是否有源单判断
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entryId">分录内码</param>
        /// <param name="entryLinkTableName">关联表表名</param>
        /// <returns></returns>
        public static DynamicObjectCollection OtherPayOrRecSrc(Context context, List<string> entryId, string entryLinkTableName)
        {
            var entryIdStr = string.Join(",", entryId);
            var sqlStr = $@"select FEntryId from {entryLinkTableName} where FEntryId in ({entryIdStr})";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 获取已作废或同步结果失败的单据
        /// </summary>
        /// <param name="context"></param>
        /// <param name="billIdListStr"></param>
        /// <returns></returns>
        public static int GetSyncLogById(Context context, string billIdListStr)
        {
            var sqlStr1 = $@"select FID from T_BOA_SyncLog 
where FID in ({billIdListStr}) and F_BOA_ISSUCCESS = 1 and F_BOA_ISDISUSE = 0";
            var count2 = DBUtils.ExecuteDynamicObject(context, sqlStr1).Count;
            return count2;
        }

        /// <summary>
        /// 判断付款单上游单据是否是费用申请单(差旅报销单)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="billId">付款单内码</param>
        /// <returns></returns>
        public static bool GetPaySrcBill(Context context, string billId)
        {
            var sqlStr = $@"select FId from T_AP_PAYBILLSRCENTRY where fid = {billId} 
and (FSourceType = 'ER_ExpenseRequest' or FSourceType = 'ER_ExpReimbursement_Travel')";
            var result = DBUtils.ExecuteDynamicObject(context, sqlStr);
            return result.Count > 0;
        }

        /// <summary>
        /// 根据销售出库单内码获取应收收款核销记录
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="billIdList">销售出库单据内码</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetCheckInfo(Context context, List<string> billIdList, string orgName)
        {
            var billIdStr = string.Join(",", billIdList);
            var sqlStr = string.Empty;
            if (orgName == "事业部")
            {
                sqlStr = $@"select sum(t1.FCURWRITTENOFFAMOUNTFOR) as currentAmount,--核销总金额
max(t2.allAmount) as amount,t2.FSBillId --应收总金额，销售出库单内码
from(
select t3.FBillNo,t1.FSBillId,max(t3.FALLAMOUNTFOR) as allAmount
from t_AR_receivableEntry_LK t1 --应收单关联明细
join t_AR_receivableEntry t2 on t1.FEntryId = t2.FEntryId --应收单明细
join t_AR_receivable t3 on t2.FID = t3.FID --应收单
where t1.FSTableName = 'T_SAL_OUTSTOCKENTRY' and t1.FSBillId in ({billIdStr})
group by t3.FBillNo,t1.FSBillId 
) t2 
join T_AR_RECMacthLogENTRY t1 on t1.FTARGETBILLNO = t2.FBillNo --应收收款核销记录
join(
select t1.FBillNo
from t_AR_receivable t1
join t_AR_receivableEntry t2 on t1.FID = t2.FID
join T_BD_MATERIAL t3 on t2.FMATERIALID = t3.FMATERIALID and t3.FNumber = '999'
group by t1.FBillNo
) t3 on t1.FSRCBILLNO = t3.FBillNo
where t1.FTARGETFROMID = 'AR_receivable' and t1.FSOURCEFROMID = 'AR_receivable'
group by t2.FSBillId";
            }
            else if (orgName == "事业部1")
            {
                sqlStr = $@"select sum(t1.FCURWRITTENOFFAMOUNTFOR) as currentAmount,--核销总金额
max(t2.allAmount) as amount,t2.FSBillId --应收总金额，销售出库单内码
from(
select t3.FBillNo,t1.FSBillId,max(t3.FALLAMOUNTFOR) as allAmount
from t_AR_receivableEntry_LK t1 --应收单关联明细
join t_AR_receivableEntry t2 on t1.FEntryId = t2.FEntryId --应收单明细
join t_AR_receivable t3 on t2.FID = t3.FID --应收单
where t1.FSTableName = 'T_SAL_OUTSTOCKENTRY' and t1.FSBillId in ({billIdStr})
group by t3.FBillNo,t1.FSBillId 
) t2 
join T_AR_RECMacthLogENTRY t1 on t1.FTARGETBILLNO = t2.FBillNo --应收收款核销记录
join T_BAS_BILLTYPE_L t3 on t1.FSOURCETYPE = t3.FBillTypeId and t3.FName = '费用应收单' and t3.flocaleid = 2052
where t1.FTARGETFROMID = 'AR_receivable' and t1.FSOURCEFROMID = 'AR_receivable'
group by t2.FSBillId";
            }
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 根据组织内码获取组织编码
        /// </summary>
        /// <param name="context"></param>
        /// <param name="orgNumber"></param>
        /// <returns></returns>
        public static string GetCorpOrgNumber(Context context, string orgId)
        {
            var sqlStr = $@"select top 1 t2.FNUMBER
from T_BOA_OrgConfigureEntry t1
join BOA_t_Cust100005 t2 on t1.F_BOA_CORPORGID = t2.FID
where t1.F_BOA_ORGID = {orgId}";
            return DBUtils.ExecuteScalar(context, sqlStr, "");
        }

        /// <summary>
        /// 查付款退款单的上游单据付款单
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entryIdList">付款退款单分录内码</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetREFUNDBILLSrcBillInfo(Context context, List<string> entryIdList)
        {
            var entryIdStr = string.Join(",", entryIdList);
            var sqlStr = $@"select isnull(t4.F_BOA_TARGETBILLID,0) as corpBillId,isnull(t4.F_BOA_TARGETENTRYID,0) as corpEntryId,
t1.frealrefundamount_sold,t1.frealrefundamount_s,t5.FBillNo,t3.FEntryId,t2.FREALREFUNDAMOUNT,t1.FRuleId,t5.F_BOA_Businesstype
from T_AP_REFUNDBILLSRCENTRY_LK t1 --付款退款单源单明细关联表
join T_AP_REFUNDBILLSRCENTRY t2 on t1.FEntryId = t2.FEntryId --付款退款单源单明细
join T_AP_REFUNDBILLENTRY t3 on t2.FId = t3.FId --付款退款单明细
join T_AP_REFUNDBILL t5 on t3.FID = t5.FID --付款退款单单据头
left join (
 select t1.F_BOA_SRCBILLID,t2.F_BOA_SRCENTRYID,
 t1.F_BOA_TARGETBILLID,t2.F_BOA_TARGETENTRYID
 from T_BOA_SyncLog t1
 join T_BOA_SyncLogEntry t2 on t1.FID = t2.FID
 where t1.F_BOA_BILLTYPE = '付款单' and t1.F_BOA_ISSUCCESS = 1
 and t1.F_BOA_ISDISUSE = 0
) t4 on t4.F_BOA_SRCBILLID = t1.FSBillId and t4.F_BOA_SRCENTRYID = t1.FSID
where t1.FSTableName = 'T_AP_PAYBILLENTRY' and t3.fentryid in ({entryIdStr})";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 取销售退货单成本价为0的数据
        /// </summary>
        /// <param name="context">上下文</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetReturnStockInfo(Context context)
        {
            var sqlStr = @"select t1.fbillno,tm.fnumber as materialNumber,t2.fentryid
from T_SAL_RETURNSTOCK t1 --销售退货单
join T_SAL_RETURNSTOCKENTRY t2 on t1.FID = t2.FID --销售退货单明细
join T_BD_MATERIAL tm on t2.FMaterialId = tm.FMaterialId 
join T_SAL_RETURNSTOCKENTRY_F t3 on t2.FEntryId = t3.FEntryId and t3.FCOSTPRICE <= 0 --销售退货单财务
GROUP BY t1.fbillno,tm.fnumber,t2.fentryid";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 根据物料编码获取销售出库单对应成本价
        /// </summary>
        /// <param name="context"></param>
        /// <param name="materialIdStr"></param>
        /// <returns></returns>
        public static DynamicObjectCollection GetOutStockPriceInfo(Context context, string materialIdStr)
        {
            var sqlStr = $@"select FSALCOSTPRICE,materialNumber from 
(
select ROW_NUMBER() OVER(PARTITION by t.materialNumber ORDER BY t.FDate DESC) as rn,
t.FSALCOSTPRICE,t.materialNumber
from (
select t1.FDate,t3.FSALCOSTPRICE,tm.fnumber as materialNumber
from T_SAL_OUTSTOCK t1 --销售出库单
join T_SAL_OUTSTOCKENTRY t2 on t1.FID = t2.FID --销售出库单明细
join T_BD_MATERIAL tm on t2.FMaterialId = tm.FMaterialId
join T_SAL_OUTSTOCKENTRY_F t3 on t2.FEntryId = t3.FEntryId and t3.FSALCOSTPRICE>0 --销售出库单财务
where tm.FNumber = '{materialIdStr}'
) t 
) t1 where rn=1";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 根据物料编码获取销售出库单对应成本价
        /// </summary>
        /// <param name="context"></param>
        /// <param name="materialIdStr"></param>
        /// <returns></returns>
        public static decimal GetOutStockPriceInfo1(Context context, string materialIdStr)
        {
            var sqlStr = $@"select top 1 t3.FSALCOSTPRICE/tm.F_BOA_Decimalxs as FSALCOSTPRICE
from T_SAL_OUTSTOCK t1 --销售出库单
join T_SAL_OUTSTOCKENTRY t2 on t1.FID = t2.FID --销售出库单明细
join T_BD_MATERIAL tm on t2.FMaterialId = tm.FMaterialId
join T_SAL_OUTSTOCKENTRY_F t3 on t2.FEntryId = t3.FEntryId and t3.FSALCOSTPRICE>0 --销售出库单财务
where tm.FNumber = '{materialIdStr}' and t1.FDOCUMENTSTATUS = 'C'
and (t1.FDATE <= '2022-06-30' or t1.FDATE >= '2022-10-01')
and tm.F_BOA_Decimalxs>0
order by t1.FDATE DESC";
            return DBUtils.ExecuteScalar(context, sqlStr, 0m);
        }
    }
}
