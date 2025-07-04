using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Orm.DataEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.DJJX.Report.BusinessPlugIn.Helper
{
    public static class SqlHelper
    {
        /// <summary>
        /// 获取供应商上的币别
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="supplierId">供应商内码</param>
        /// <returns></returns>
        public static long GetCurrencyIdBySupplierId(Context context, object supplierId)
        {
            var sqlStr = $"select FPAYCURRENCYID from t_BD_SupplierFinance where FSUPPLIERID ={supplierId}";
            return DBUtils.ExecuteScalar(context, sqlStr, 0L);
        }

        /// <summary>
        /// 根据物料和供应商获取采购订单和采购入库单相关数据
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="supplierId">供应商内码</param>
        /// <param name="materialId">物料内码</param>
        /// <param name="orgId">组织内码</param>
        /// <param name="startTime">开始时间</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetHistoryData(Context context, object supplierId, object materialId, long orgId, string startTime)
        {
            var filterStr = string.Empty;
            if (supplierId != null)
            {
                filterStr = $"and t1.FSUPPLIERID = {supplierId}";
            }
            var sqlQuery = $@"select * from (
select t1.FBILLNO,t1.FBILLTYPEID,t1.FSUPPLIERID,t1.FDATE,--单据编号，单据类型，供应商，日期
t2.FMATERIALID,t2.FUNITID,t2.FQTY,--物料，采购单位，采购数量
t3.FPRICE,t3.FTAXPRICE,t2.fseq,t1.fid,'PUR_PurchaseOrder' as billFlag --单价，含税单价，单据内码，单据标识
from t_PUR_POOrder t1 --采购订单
join t_PUR_POOrderEntry t2 on t1.FID = t2.FID --采购订单明细
join t_PUR_POOrderEntry_F t3 on t2.FENTRYID = t3.FENTRYID --采购订单明细财务
where t2.FMATERIALID = {materialId} {filterStr} and t1.FPURCHASEORGID = {orgId}
and t1.fdate <='{DateTime.Now.Date}' and t1.fdate >= '{startTime}'
and t1.FDOCUMENTSTATUS = 'C'
union all
select top 50 t1.FBILLNO,t1.FBILLTYPEID,t1.FSUPPLIERID,t1.FDATE,
t2.FMATERIALID,t2.FUNITID,t2.FREALQTY as FQTY,
t3.FPRICE,t3.FTAXPRICE,t2.fseq,t1.fid,'STK_InStock' as billFlag
from t_STK_InStock t1 --采购入库单
join T_STK_INSTOCKENTRY t2 on t2.FID = t1.FID
join T_STK_INSTOCKENTRY_F t3 on t2.FENTRYID = t3.FENTRYID
where t2.FMATERIALID = {materialId} {filterStr} and t1.FSTOCKORGID = {orgId}
and t1.fdate >= '{startTime}' and t1.fdate <='{DateTime.Now.Date}'
and t1.FDOCUMENTSTATUS = 'C'
) t order by t.fdate desc";
            return DBUtils.ExecuteDynamicObject(context, sqlQuery);
        }

        /// <summary>
        /// 百宝箱
        /// 获取在途量，请购量，未批请购量，未领数量
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="materialId">物料内码</param>
        /// <param name="orgId">组织内码</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetQty(Context context, object materialId, long orgId)
        {
            var querySql = $@"--在途量
select sum(t2.FREMAINSTOCKINQTY) as qty,t1.F_BOA_CKMC as fstockid,'在途量' as type --剩余入库数量，仓库内码
from t_PUR_POOrderEntry t1 --采购订单分录
join t_PUR_POOrderEntry_R t2 on t1.FENTRYID = t2.FENTRYID --采购订单分录_关联信息
join t_PUR_POOrder t3 on t1.FID = t3.FID --采购订单单据头
where t1.FMATERIALID = {materialId} and t3.FPURCHASEORGID = {orgId}
and t3.FCLOSESTATUS <> 'B'
and t3.FDOCUMENTSTATUS = 'C'
group by t1.FMATERIALID,t1.F_BOA_CKMC
union all
--请购量
select sum(t1.FREQQTY) as qty,t4.FSTOCKID as fstockid,'请购量' as type --申请数量，仓库内码
from T_PUR_ReqEntry t1 --采购申请单分录
join T_PUR_Requisition t2 on t1.FID = t2.FID --采购申请单单据头
join T_PUR_ReqEntry_R t3 on t1.FENTRYID = t3.FENTRYID --采购申请单分录_关联关系
join T_PUR_ReqEntry_S t4 on t1.FENTRYID = t4.FENTRYID --采购申请单分录_货源
where t2.FDOCUMENTSTATUS = 'C' and t3.FORDERJNBASEQTY=0 and t2.FCLOSESTATUS <> 'B'
and t1.FMATERIALID = {materialId} and t2.FAPPLICATIONORGID = {orgId}
group by t1.FMATERIALID,t4.FSTOCKID
union all
--未批请购量
select sum(t1.FREQQTY) as qty,t4.FSTOCKID as fstockid,'未批请购量' as type --申请数量，仓库内码
from T_PUR_ReqEntry t1 --采购申请单分录
join T_PUR_Requisition t2 on t1.FID = t2.FID --采购申请单单据头
join T_PUR_ReqEntry_S t4 on t1.FENTRYID = t4.FENTRYID --采购申请单分录_货源
where t2.FDOCUMENTSTATUS <> 'C' and t2.FCLOSESTATUS <> 'B'
and t1.FMATERIALID = {materialId} and t2.FAPPLICATIONORGID = {orgId}
group by t1.FMATERIALID,t4.FSTOCKID
union all
--未领数量 --该数量未涉及仓库
select sum(t2.FNOPICKEDQTY) as qty,max(t1.FID) as fstockid,'未领数量' as type --未领数量,单据内码
from T_PRD_PPBOMENTRY t1 --生产用料清单分录
join T_PRD_PPBOMENTRY_Q t2 on t1.FENTRYID = t2.FENTRYID --生产用料清单分录_表体数量字段
join T_PRD_PPBOM t3 on t1.FID = t3.FID --生产用料清单单据头
join T_PRD_MOENTRY t4 on t3.FMOID = t4.FID and t3.FMOENTRYID = t4.FENTRYID --生产订单分录
join T_PRD_MOENTRY_A t5 on t4.FENTRYID = t5.FENTRYID --生产订单分录_生产执行数据
where t5.FSTATUS not in (6,7)
and t1.FMATERIALID = {materialId} and t3.FPRDORGID = {orgId}
and t3.FDOCUMENTSTATUS = 'C'
group by t1.FMATERIALID";
            return DBUtils.ExecuteDynamicObject(context, querySql);
        }

        /// <summary>
        /// 获取最近出库日期，最近入库日期
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> GetDate(Context context, object materialId, long orgId,long stockId)
        {
            var querySql = $@"/*dialect*/
select max(t.FDate) as FDate
from (
select max(t1.FDATE) as FDate,t2.FMATERIALID,t2.FSTOCKID
from t_STK_InStock t1 --采购入库单
join T_STK_INSTOCKENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID = {materialId}
and t1.FSTOCKORGID = {orgId}
and t2.FSTOCKID = {stockId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select max(t1.FDATE) as FDate,t2.FMATERIALID,t2.FSTOCKID
from T_PRD_INSTOCK t1 --生产入库单
join T_PRD_INSTOCKENTRY t2 on t1.FID = t2.FID 
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID = {materialId}
and t1.FSTOCKORGID = {orgId}
and t2.FSTOCKID = {stockId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select max(t1.FDATE) as FDate,t2.FMATERIALID,t2.FSTOCKID
from T_STK_MISCELLANEOUS t1 --其他入库单
join T_STK_MISCELLANEOUSENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID = {materialId}
and t1.FSTOCKORGID = {orgId}
and t2.FSTOCKID = {stockId}
group by t2.FMATERIALID,t2.FSTOCKID ) t group by t.FMATERIALID,t.FSTOCKID
";
            var querySql1 = $@"/*dialect*/select top 1 * from (
select * from (
select top 1 t1.FDATE
from T_PRD_PICKMTRL t1 --生产领料
join T_PRD_PICKMTRLDATA t2 on t1.FID = t2.FID
where t2.FMATERIALID = {materialId} and t1.FSTOCKORGID = {orgId} and t1.FDOCUMENTSTATUS = 'C'
and t2.FSTOCKID = {stockId}
order by t1.FDATE desc ) t
union all
select * from (
select top 1 t1.FDATE
from T_SAL_OUTSTOCK t1 --销售出库单
join T_SAL_OUTSTOCKENTRY t2 on t1.FID = t2.FID
where t2.FMATERIALID = {materialId} and t1.FSTOCKORGID = {orgId} and t1.FDOCUMENTSTATUS = 'C'
and t2.FSTOCKID = {stockId}
order by t1.FDATE desc ) t
union all
select * from (
select top 1 t1.FDATE
from T_STK_MISDELIVERY t1 --其他出库单
join T_STK_MISDELIVERYENTRY t2 on t1.FID = t2.FID
where t2.FMATERIALID = {materialId} and t1.FSTOCKORGID = {orgId} and t1.FDOCUMENTSTATUS = 'C'
and t2.FSTOCKID = {stockId}
order by t1.FDATE desc ) t
) t order by t.FDATE desc";
            var inStockDate = DBUtils.ExecuteDynamicObject(context, querySql);//最近入库日期
            var outStockDate = DBUtils.ExecuteDynamicObject(context, querySql1);//最近出库日期
            var returnDate = new Dictionary<string, string>();
            if (inStockDate.Count > 0)
            {
                returnDate.Add("inStockDate", inStockDate[0]["FDATE"].ToString());
            }
            else
            {
                returnDate.Add("inStockDate", "");
            }
            if (outStockDate.Count > 0)
            {
                returnDate.Add("outStockDate", outStockDate[0]["FDATE"].ToString());
            }
            else
            {
                returnDate.Add("outStockDate", "");
            }
            return returnDate;
        }

        /// <summary>
        /// 获取物料内码和物料编码
        /// </summary>
        /// <param name="context"></param>
        /// <param name="filter"></param>
        /// <returns></returns>
        public static DynamicObjectCollection GetMaterialNumber(Context context, string filter)
        {
            var sqlQuery = $@"select t1.FMATERIALID,t1.FNUMBER
from T_BD_MATERIAL t1 --编码
join t_BD_MaterialBase t2 on t1.FMATERIALID = t2.FMATERIALID --存货类别
join T_BD_MATERIAL_L t3 on t1.FMATERIALID = t3.FMATERIALID and t3.FLOCALEID = 2052 --名称，规格型号
where {filter}";
            return DBUtils.ExecuteDynamicObject(context, sqlQuery);
        }

        /// <summary>
        /// 获取入库单价和入库含税单价
        /// </summary>
        /// <param name="context"></param>
        /// <param name="materialId"></param>
        /// <returns></returns>
        public static DynamicObjectCollection GetInstockPrice(Context context, long materialId, string orgId)
        {
            var sqlQuery = $@"select top 1 t2.FMATERIALID,t3.FPRICE,t3.FTAXPRICE
from t_STK_InStock t1 --采购入库单
join T_STK_INSTOCKENTRY t2 on t2.FID = t1.FID
join T_STK_INSTOCKENTRY_F t3 on t2.FENTRYID = t3.FENTRYID
where t2.FMATERIALID in ({materialId}) and t1.FSTOCKORGID = {orgId}
and t1.FDOCUMENTSTATUS = 'C'
order by t1.FDATE desc";
            return DBUtils.ExecuteDynamicObject(context, sqlQuery);
        }

        /// <summary>
        /// 货品分仓存量明细表
        /// 获取在途量，已收料未入库数量，请购量，未批请购量，未领数量，出库未审数量，入库未审数量
        /// </summary>
        /// <param name="context"></param>
        /// <param name="materialId"></param>
        /// <param name="orgId"></param>
        /// <returns></returns>
        public static DynamicObjectCollection GetOnWayQty(Context context, string materialId, string orgId)
        {
            var sqlQuery = $@"--在途量
select sum(t2.FREMAINSTOCKINQTY) as qty,t1.FMATERIALID,'在途量' as type,t1.F_BOA_CKMC as fstockid --剩余入库数量
from t_PUR_POOrderEntry t1 --采购订单分录
join t_PUR_POOrderEntry_R t2 on t1.FENTRYID = t2.FENTRYID --采购订单分录_关联信息
join t_PUR_POOrder t3 on t1.FID = t3.FID --采购订单单据头
where t1.FMATERIALID in ({materialId}) and t3.FPURCHASEORGID = {orgId} and t2.FREMAINSTOCKINQTY>0
and t3.FCLOSESTATUS <> 'B'
and t3.FDOCUMENTSTATUS = 'C'
group by t1.FMATERIALID,t1.F_BOA_CKMC
union all
--已收料未入库数量
select sum(t1.FACTRECEIVEQTY-t2.FINSTOCKQTY) as qty,t1.FMATERIALID,'已收料未入库数量' as type,t1.FSTOCKID as fstockid
from T_PUR_ReceiveEntry t1 --收料通知单分录
join T_PUR_ReceiveEntry_S t2 on t1.FENTRYID = t2.FENTRYID
join T_PUR_Receive t3 on t1.FID = t3.FID --收料通知单
where t3.FSTOCKORGID = {orgId} and t1.FMATERIALID in ({materialId})
and t3.FDOCUMENTSTATUS = 'C'
group by t1.FMATERIALID,t1.FSTOCKID
union all
--请购量
select sum(t1.FREQQTY) as qty,t1.FMATERIALID,'请购量' as type,t4.FSTOCKID as fstockid --申请数量
from T_PUR_ReqEntry t1 --采购申请单分录
join T_PUR_Requisition t2 on t1.FID = t2.FID --采购申请单单据头
join T_PUR_ReqEntry_R t3 on t1.FENTRYID = t3.FENTRYID --采购申请单分录_关联关系
join T_PUR_ReqEntry_S t4 on t1.FENTRYID = t4.FENTRYID --采购申请单分录_货源
where t2.FDOCUMENTSTATUS = 'C' and t3.FORDERJNBASEQTY=0 
and t1.FMATERIALID in ({materialId}) and t2.FAPPLICATIONORGID = {orgId}
group by t1.FMATERIALID,t4.FSTOCKID
union all
--未批请购量
select sum(t1.FREQQTY) as qty,t1.FMATERIALID,'未批请购量' as type,t4.FSTOCKID as fstockid --申请数量
from T_PUR_ReqEntry t1 --采购申请单分录
join T_PUR_Requisition t2 on t1.FID = t2.FID --采购申请单单据头
join T_PUR_ReqEntry_S t4 on t1.FENTRYID = t4.FENTRYID --采购申请单分录_货源
where t2.FDOCUMENTSTATUS <> 'C'
and t1.FMATERIALID in ({materialId}) and t2.FAPPLICATIONORGID = {orgId}
group by t1.FMATERIALID,t4.FSTOCKID
union all
--未领数量 --该数量未涉及仓库
select sum(t2.FNOPICKEDQTY) as qty,t1.FMATERIALID,'未领数量' as type,max(t1.FID) as fstockid --未领数量
from T_PRD_PPBOMENTRY t1 --生产用料清单分录
join T_PRD_PPBOMENTRY_Q t2 on t1.FENTRYID = t2.FENTRYID --生产用料清单分录_表体数量字段
join T_PRD_PPBOM t3 on t1.FID = t3.FID --生产用料清单单据头
join T_PRD_MOENTRY t4 on t3.FMOID = t4.FID and t3.FMOENTRYID = t4.FENTRYID --生产订单分录
join T_PRD_MOENTRY_A t5 on t4.FENTRYID = t5.FENTRYID --生产订单分录_生产执行数据
where t5.FSTATUS not in (6,7)
and t1.FMATERIALID in ({materialId}) and t3.FPRDORGID = {orgId}
and t3.FDOCUMENTSTATUS = 'C'
group by t1.FMATERIALID
union all
--组织间在途量 --该数量未涉及仓库
select sum(r.FREMAINBASEQTY) qty,r.FMATERIALID,'组织间在途量' as type,max(r.FID) as fstockid
from T_PLN_REQUIREMENTORDER r
where r.FREMAINBASEQTY>0 and r.fdocumentstatus='C'
and r.fmaterialid  in ({materialId})
group by r.fmaterialid
union all
--出库未审数量
select SUM(t.qty) as qty,t.FMATERIALID,'出库未审数量' as type,t.FSTOCKID as fstockid
from (
select sum(t2.FAPPQTY) as qty,t2.FMATERIALID,t2.FSTOCKID
from T_PRD_PICKMTRL t1 --生产领料单
join T_PRD_PICKMTRLDATA t2 on t2.FID = t1.FID --生产领料单明细
where t1.FDOCUMENTSTATUS not in ('C','Z')
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select sum(t2.FQTY) as qty,t2.FMATERIALID,t2.FSTOCKID
from T_STK_MISDELIVERY t1 --其他出库单
join T_STK_MISDELIVERYENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS not in ('C','Z')
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select sum(t2.FMUSTQTY) as qty,t2.FMATERIALID,t2.FSTOCKID
from T_SAL_OUTSTOCK t1 --销售出库单
join T_SAL_OUTSTOCKENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS not in ('C','Z')
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
) t group by t.FMATERIALID,t.FSTOCKID
union all
--入库未审数量
select sum(qty) as qty,t.FMATERIALID,'入库未审数量' as type,t.FSTOCKID as fstockid
from (
select sum(t2.FMUSTQTY) as qty,t2.FMATERIALID,t2.FSTOCKID
from t_STK_InStock t1 --采购入库单
join T_STK_INSTOCKENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS not in ('C','Z')
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select sum(t2.FMUSTQTY) as qty,t2.FMATERIALID,t2.FSTOCKID
from T_PRD_INSTOCK t1 --生产入库单
join T_PRD_INSTOCKENTRY t2 on t1.FID = t2.FID 
where t1.FDOCUMENTSTATUS not in ('C','Z')
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select sum(t2.FQTY) as qty,t2.FMATERIALID,t2.FSTOCKID
from T_STK_MISCELLANEOUS t1 --其他入库单
join T_STK_MISCELLANEOUSENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS not in ('C','Z')
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID ) t group by t.FMATERIALID,t.FSTOCKID";
            return DBUtils.ExecuteDynamicObject(context, sqlQuery);
        }

        /// <summary>
        /// 货品分仓存量明细表
        /// 最近领料日期，最近出库日期，最近入库日期
        /// </summary>
        /// <param name="context"></param>
        /// <param name="materialId"></param>
        /// <param name="orgId"></param>
        /// <returns></returns>
        public static DynamicObjectCollection GetDateInfo(Context context, string materialId, string orgId)
        {
            var sqlQuery = $@"--最近领料日期
select max(t1.FDATE) as FDate,t2.FMATERIALID,'最近领料日期' as type,t2.FSTOCKID
from T_PRD_PICKMTRL t1 --生产领料
join T_PRD_PICKMTRLDATA t2 on t1.FID = t2.FID
where t2.FMATERIALID in ({materialId}) and t1.FSTOCKORGID = {orgId} and t1.FDOCUMENTSTATUS = 'C'
group by t2.FMATERIALID,t2.FSTOCKID
union all
--最近出库日期
select max(t.FDate) as FDate,t.FMATERIALID,'最近出库日期' as type,t.FSTOCKID
from (
select max(t1.FDATE) as FDate,t2.FMATERIALID,t2.FSTOCKID
from T_PRD_PICKMTRL t1 --生产领料单
join T_PRD_PICKMTRLDATA t2 on t2.FID = t1.FID --生产领料单明细
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select max(t1.FDATE) as FDate,t2.FMATERIALID,t2.FSTOCKID
from T_STK_MISDELIVERY t1 --其他出库单
join T_STK_MISDELIVERYENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select max(t1.FDATE) as FDate,t2.FMATERIALID,t2.FSTOCKID
from T_SAL_OUTSTOCK t1 --销售出库单
join T_SAL_OUTSTOCKENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
) t group by t.FMATERIALID,t.FSTOCKID
union all
--最近入库日期
select max(t.FDate) as FDate,t.FMATERIALID,'最近入库日期' as type,t.FSTOCKID
from (
select max(t1.FDATE) as FDate,t2.FMATERIALID,t2.FSTOCKID
from t_STK_InStock t1 --采购入库单
join T_STK_INSTOCKENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select max(t1.FDATE) as FDate,t2.FMATERIALID,t2.FSTOCKID
from T_PRD_INSTOCK t1 --生产入库单
join T_PRD_INSTOCKENTRY t2 on t1.FID = t2.FID 
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID
union all
select max(t1.FDATE) as FDate,t2.FMATERIALID,t2.FSTOCKID
from T_STK_MISCELLANEOUS t1 --其他入库单
join T_STK_MISCELLANEOUSENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID in ({materialId})
and t1.FSTOCKORGID = {orgId}
group by t2.FMATERIALID,t2.FSTOCKID ) t group by t.FMATERIALID,t.FSTOCKID";
            return DBUtils.ExecuteDynamicObject(context, sqlQuery);
        }

        /// <summary>
        /// 根据物料编码获取物料内码。（解决即时库存获取的物料内码为masterid的问题）
        /// </summary>
        /// <param name="context"></param>
        /// <param name="materialNumber"></param>
        /// <param name="orgId">库存组织</param>
        /// <returns></returns>
        public static string GetMaterialIdStrbyMasterId(Context context, string materialNumber, object orgId)
        {
            var sqlStr = $@"select FMATERIALID
from T_BD_MATERIAL
where FNUMBER in ({materialNumber}) and FUSEORGID = {orgId}";
            var result = DBUtils.ExecuteDynamicObject(context, sqlStr);
            return string.Join(",", result.Select(t => t["FMATERIALID"]));
        }

        /// <summary>
        /// 获取参与mrp计算的仓库
        /// </summary>
        /// <param name="context">上下文</param>
        /// <returns></returns>
        public static DynamicObjectCollection GetStockNumberAndId(Context context)
        {
            var sqlStr = $"select FSTOCKID,FNUMBER from t_BD_Stock where FALLOWMRPPLAN = 1";
            return DBUtils.ExecuteDynamicObject(context,sqlStr);
        }

        /// <summary>
        /// 判断仓库是否参与mrp计算
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="stockId">仓库内码</param>
        /// <returns>false = 不参与mrp计算，true = 参与mrp计算</returns>
        public static bool IsMrpStock(Context context,long stockId)
        {
            var sqlStr = $"select FALLOWMRPPLAN from t_BD_Stock where FSTOCKID = {stockId}";
            var result = DBUtils.ExecuteScalar(context,sqlStr,0L);
            return result == 1;
        }

        /// <summary>
        /// 判断仓库是否禁用
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="stockId">仓库内码</param>
        /// <returns>true = 禁用，false = 未禁用</returns>
        public static bool IsStockForbidStatus(Context context, long stockId)
        {
            var sqlStr = $"select FFORBIDSTATUS from T_BD_STOCK where FSTOCKID = {stockId}";
            var ForbidStatus = DBUtils.ExecuteScalar(context,sqlStr,"Z");
            return ForbidStatus == "B";
        }
    }
}
