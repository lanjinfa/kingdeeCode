using BOA.DJJX.Report.BusinessPlugIn.Helper;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Kingdee.K3.SCM.App.Core;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Kingdee.K3.SCM.ServiceHelper;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.Enums;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.Metadata;

namespace BOA.DJJX.Report.BusinessPlugIn.Plugins
{
    [HotUpdate]
    [Description("百宝箱")]
    public class TreasureChestPlugin : AbstractDynamicFormPlugIn
    {
        private bool isMaterialChange = false;//是否物料变更导致清空供应商字段值
        private string startTime = DateTime.Now.AddMonths(-12).ToString("yyyy-MM-dd");//记录开始时间(默认一年)

        public override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            var parameters = this.View.OpenParameter.GetCustomParameters();
            if (parameters.ContainsKey("supplierId") && parameters.ContainsKey("materialId")
                && parameters.ContainsKey("materialNumber") && parameters.ContainsKey("orgId"))
            {
                this.View.Model.SetValue("F_BOA_OrgId", parameters["orgId"]);
                var supplierId = parameters["supplierId"];
                var materialId = parameters["materialId"];
                var materailNumber = parameters["materialNumber"];
                var org = this.View.Model.GetValue("F_BOA_OrgId");
                var orgNumber = (org as DynamicObject)["Number"].ToString();
                var currencyId = SqlHelper.GetCurrencyIdBySupplierId(this.Context, supplierId);
                this.View.Model.SetValue("F_BOA_Supplier", supplierId);
                this.View.Model.SetValue("F_BOA_MaterialNumber", materialId);
                this.View.Model.SetValue("F_BOA_Currency", currencyId);
                this.View.UpdateView("F_BOA_Supplier");
                this.View.UpdateView("F_BOA_MaterialNumber");
                this.View.UpdateView("F_BOA_Currency");
                this.View.Model.SetValue("F_BOA_AbleStockQty", GetAbleQty(orgNumber, materailNumber.ToString()));
                this.View.UpdateView("F_BOA_AbleStockQty");
                var inventoryInfo = InventoryHelper.GetInventoryInfo(this.Context, orgNumber, materailNumber.ToString());
                GetHistoryAndInventoryData(inventoryInfo);
                if (inventoryInfo == null)
                {
                    //this.View.ShowErrMessage("查询即时库存失败，请联系管理员！");
                    return;
                }
                if (inventoryInfo.Data == null)
                {
                    //this.View.ShowErrMessage("未查询到即时库存，请重新选择条件！");
                    return;
                }
                var baseQty = inventoryInfo.Data.Sum(t => t.FBaseQty);
                var secQty = inventoryInfo.Data.Sum(t => t.FSecQty);
                this.View.Model.SetValue("F_BOA_StockQty", baseQty);
                this.View.Model.SetValue("F_BOA_SStockQty", secQty);
                this.View.UpdateView("F_BOA_StockQty");
                this.View.UpdateView("F_BOA_SStockQty");
            }
        }

        public override void AfterBindData(EventArgs e)
        {
            base.AfterBindData(e);
            this.View.GetControl("F_BOA_StartDate").Enabled = false;
        }

        public override void DataChanged(DataChangedEventArgs e)
        {
            base.DataChanged(e);
            if (e.Field.Key.EqualsIgnoreCase("F_BOA_Supplier"))//供应商
            {
                if (!isMaterialChange)
                {
                    var supplierId = e.NewValue;
                    var currencyId = SqlHelper.GetCurrencyIdBySupplierId(this.Context, supplierId ?? 0);
                    this.View.Model.SetValue("F_BOA_Currency", currencyId);
                    GetHistoryAndInventoryData();
                }
                isMaterialChange = false;//保证供应商可以正常值更新
            }
            else if (e.Field.Key.EqualsIgnoreCase("F_BOA_MaterialNumber"))//物料
            {
                if (e.NewValue == null)
                {
                    this.View.ShowWarnningMessage("物料不能为空！");
                    return;
                }
                var org = this.View.Model.GetValue("F_BOA_OrgId");
                if (org == null)
                {
                    this.View.ShowWarnningMessage("组织不能为空！");
                    return;
                }
                isMaterialChange = true;
                this.View.Model.SetValue("F_BOA_Supplier", 0);//清空供应商字段值

                var orgNumber = (org as DynamicObject)["Number"].ToString();
                var material = this.View.Model.GetValue("F_BOA_MaterialNumber");
                var materialNumber = (material as DynamicObject)["Number"].ToString();
                var inventoryInfo = InventoryHelper.GetInventoryInfo(this.Context, orgNumber, materialNumber);
                GetHistoryAndInventoryData(inventoryInfo);
                if (inventoryInfo == null)
                {
                    this.View.ShowErrMessage("查询即时库存失败，请联系管理员！");
                    return;
                }
                if (inventoryInfo.Data == null)
                {
                    //this.View.ShowErrMessage("未查询到即时库存，请重新选择条件！");
                    return;
                }
                var baseQty = inventoryInfo.Data.Sum(t => t.FBaseQty);
                var secQty = inventoryInfo.Data.Sum(t => t.FSecQty);
                this.View.Model.SetValue("F_BOA_StockQty", baseQty);
                this.View.Model.SetValue("F_BOA_SStockQty", secQty);
            }
            else if (e.Field.Key.EqualsIgnoreCase("F_BOA_OrgId"))
            {
                GetHistoryAndInventoryData();
            }
            else if (e.Field.Key.EqualsIgnoreCase("F_BOA_TimeZone"))//时间区间
            {
                //A = 一个月，B = 三个月，C = 六个月，D = 一年，E = 自定义起始日期
                var timeZone = e.NewValue;
                if (timeZone.ToString() == "A")
                {
                    startTime = DateTime.Now.AddMonths(-1).ToString("yyyy-MM-dd");
                    this.View.GetControl("F_BOA_StartDate").Enabled = false;
                    GetHistoryAndInventoryData();
                }
                else if (timeZone.ToString() == "B")
                {
                    startTime = DateTime.Now.AddMonths(-3).ToString("yyyy-MM-dd");
                    this.View.GetControl("F_BOA_StartDate").Enabled = false;
                    GetHistoryAndInventoryData();
                }
                else if (timeZone.ToString() == "C")
                {
                    startTime = DateTime.Now.AddMonths(-6).ToString("yyyy-MM-dd");
                    this.View.GetControl("F_BOA_StartDate").Enabled = false;
                    GetHistoryAndInventoryData();
                }
                else if (timeZone.ToString() == "D")
                {
                    startTime = DateTime.Now.AddMonths(-12).ToString("yyyy-MM-dd");
                    this.View.GetControl("F_BOA_StartDate").Enabled = false;
                    GetHistoryAndInventoryData();
                }
                else if (timeZone.ToString() == "E")
                {
                    this.View.GetControl("F_BOA_StartDate").Enabled = true;
                }
            }
            else if (e.Field.Key.EqualsIgnoreCase("F_BOA_StartDate"))//开始时间
            {
                startTime = e.NewValue.ToString();
                GetHistoryAndInventoryData();
            }
        }

        public override void BarItemClick(BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.BarItemKey.EqualsIgnoreCase("BOA_tbRefresh"))
            {
                GetHistoryAndInventoryData();
            }
        }

        public override void EntityRowDoubleClick(EntityRowClickEventArgs e)
        {
            base.EntityRowDoubleClick(e);
            if (e.Key.EqualsIgnoreCase("F_BOA_QEntity"))
            {
                var material = this.View.Model.GetValue("F_BOA_MaterialNumber");
                var org = this.View.Model.GetValue("F_BOA_OrgId");
                if (material == null)
                {
                    this.View.ShowWarnningMessage("请先选择物料！");
                    return;
                }
                if (org == null)
                {
                    this.View.ShowWarnningMessage("请先选择组织！");
                    return;
                }
                var stockName = this.View.Model.GetValue("F_BOA_StockName", e.Row);
                if (stockName == null || stockName.ToString().Equals("合计行："))
                {
                    this.View.ShowWarnningMessage("合计行，无明细！");
                    return;
                }
                var materialId = Convert.ToInt64((material as DynamicObject)["Id"]);
                var orgId = Convert.ToInt64((org as DynamicObject)["Id"]);
                var stockId = Convert.ToInt64((this.View.Model.GetValue("F_BOA_Stock", e.Row) as DynamicObject)["Id"]);
                var columnKey = e.ColKey;
                if (columnKey.EqualsIgnoreCase("F_BOA_WayQty"))//在途量
                {
                    GetWayQty(materialId, orgId, stockId);
                }
                else if (columnKey.EqualsIgnoreCase("F_BOA_PurQty"))//请购量
                {
                    GetPurQty(materialId, orgId, stockId);
                }
                else if (columnKey.EqualsIgnoreCase("F_BOA_NoPurQty"))//未批请购量
                {
                    GetUnPurQty(materialId, orgId, stockId);
                }
                else if (columnKey.EqualsIgnoreCase("F_BOA_UnclaimedQty"))//未领数量
                {
                    var qty = this.View.Model.GetValue("F_BOA_UnclaimedQty", e.Row);
                    if (Convert.ToDecimal(qty) <= 0)
                    {
                        this.View.ShowWarnningMessage("未查询到明细！");
                        return;
                    }
                    GetUnGetQty(materialId, orgId);
                }
                else if (columnKey.EqualsIgnoreCase("F_BOA_OutDate"))//最近出库日期
                {
                    GetOutDate(materialId, orgId, stockId);
                }
                else if (columnKey.EqualsIgnoreCase("F_BOA_InDate"))//最近入库日期
                {
                    GetInDate(materialId, orgId, stockId);
                }
                else
                {
                    this.View.ShowWarnningMessage("未配置查看明细功能！");
                }
            }
            else if (e.Key.EqualsIgnoreCase("F_BOA_HEntity"))
            {
                var formId = this.View.Model.GetValue("F_BOA_BillId", e.Row).ToString();
                var formFlag = this.View.Model.GetValue("F_BOA_BillFlag", e.Row).ToString();
                ShowFormInfo(formId, formFlag);
            }
        }

        /// <summary>
        /// 获取历史明细和即时库存数据
        /// </summary>
        private void GetHistoryAndInventoryData(InventoryDto inventoryDto = null)
        {
            var supplier = this.View.Model.GetValue("F_BOA_Supplier");
            var material = this.View.Model.GetValue("F_BOA_MaterialNumber");
            if (material == null)
            {
                this.View.ShowWarnningMessage("物料不能为空！");
                return;
            }
            var org = this.View.Model.GetValue("F_BOA_OrgId");
            if (org == null)
            {
                this.View.ShowWarnningMessage("组织不能为空！");
                return;
            }
            var hEntityRowCount = this.View.Model.GetEntryRowCount("F_BOA_HEntity");
            if (hEntityRowCount > 0)
            {
                ClearEntryRow("F_BOA_HEntity");
                hEntityRowCount = 0;
            }
            var qEntityRowCount = this.View.Model.GetEntryRowCount("F_BOA_QEntity");
            if (qEntityRowCount > 0)
            {
                ClearEntryRow("F_BOA_QEntity");
                qEntityRowCount = 0;
            }
            var supplierId = supplier != null ? (supplier as DynamicObject)["Id"] : supplier;
            var materialId = (material as DynamicObject)["Id"];
            var orgId = Convert.ToInt64((org as DynamicObject)["Id"]);
            var result = SqlHelper.GetHistoryData(this.Context, supplierId, materialId, orgId, startTime);
            //历史明细
            if (result.Count > 0)
            {
                foreach (var item in result)
                {
                    this.View.Model.InsertEntryRow("F_BOA_HEntity", hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_BillNo", item["FBILLNO"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_Date", item["FDATE"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_SupplierId", item["FSUPPLIERID"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_MaterialNo", item["FMATERIALID"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_UnitID", item["FUNITID"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_Qty", item["FQTY"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_Price", item["FPRICE"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_TaxPrice", item["FTAXPRICE"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_BillType", item["FBILLTYPEID"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_Seq", item["FSeq"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_BillId", item["fid"], hEntityRowCount);
                    this.View.Model.SetValue("F_BOA_BillFlag", item["billFlag"], hEntityRowCount);
                    hEntityRowCount++;
                }
                this.View.UpdateView("F_BOA_HEntity");
            }
            var orgNumber = (org as DynamicObject)["Number"].ToString();
            var materialNumber = (material as DynamicObject)["Number"].ToString();
            var result1 = inventoryDto ?? InventoryHelper.GetInventoryInfo(this.Context, orgNumber, materialNumber);
            if (result1 == null || !result1.Success || result1.Data == null)
            {
                return;
            }
            var inventoryData = result1.Data.
                                           GroupBy(t => new { t.FStockId, t.FStockLocId, t.FBaseUnitId, t.FSecUnitId }).
                                           Select(t => new InventoryData
                                           {
                                               FStockId = t.Key.FStockId,
                                               FStockLocId = t.Key.FStockLocId,
                                               FBaseUnitId = t.Key.FBaseUnitId,
                                               FSecUnitId = t.Key.FSecUnitId,
                                               FBaseAvbQty = t.Sum(m => m.FBaseAvbQty),
                                               FBaseQty = t.Sum(m => m.FBaseQty),
                                               FSecQty = t.Sum(m => m.FSecQty)
                                           }).ToList();
            //即使库存
            if (inventoryData.Count > 0)
            {
                var result2 = SqlHelper.GetQty(this.Context, materialId, orgId);
                var wayQtyInfo = result2.Where(t => t["type"].ToString() == "在途量").ToList();
                var purQtyInfo = result2.Where(t => t["type"].ToString() == "请购量").ToList();
                var noPurQtyInfo = result2.Where(t => t["type"].ToString() == "未批请购量").ToList();
                var unclaimedQtyInfo = result2.Where(t => t["type"].ToString() == "未领数量").ToList();
                foreach (var item in inventoryData)
                {
                    var wayQty = wayQtyInfo.Where(t => Convert.ToInt64(t["fstockid"]) == item.FStockId).Sum(t => Convert.ToDecimal(t["qty"]));//在途量
                    var purQty = purQtyInfo.Where(t => Convert.ToInt64(t["fstockid"]) == item.FStockId).Sum(t => Convert.ToDecimal(t["qty"]));//请购量
                    var noPurQty = noPurQtyInfo.Where(t => Convert.ToInt64(t["fstockid"]) == item.FStockId).Sum(t => Convert.ToDecimal(t["qty"]));//未批请购量
                    if (wayQty == 0 && purQty == 0 && noPurQty == 0 && item.FBaseQty == 0)
                    {
                        continue;
                    }
                    var unclaimedQty = unclaimedQtyInfo.Sum(t => Convert.ToDecimal(t["qty"]));//未领数量
                    var dateInfo = SqlHelper.GetDate(this.Context, materialId, orgId, item.FStockId);
                    this.View.Model.InsertEntryRow("F_BOA_QEntity", qEntityRowCount);
                    this.View.Model.SetValue("F_BOA_Stock", item.FStockId, qEntityRowCount);
                    this.View.InvokeFieldUpdateService("F_BOA_Stock", qEntityRowCount);//触发值更新
                    this.View.Model.SetValue("F_BOA_Position", item.FStockLocId, qEntityRowCount);
                    this.View.Model.SetValue("F_BOA_MianUnitID", item.FBaseUnitId, qEntityRowCount);
                    this.View.Model.SetValue("F_BOA_AbleQty", item.FBaseAvbQty, qEntityRowCount);
                    this.View.Model.SetValue("F_BOA_CurrentQty", item.FBaseQty, qEntityRowCount);
                    this.View.Model.SetValue("F_BOA_SuUnitID", item.FSecUnitId, qEntityRowCount);
                    this.View.Model.SetValue("F_BOA_SuQty", item.FSecQty, qEntityRowCount);
                    this.View.Model.SetValue("F_BOA_WayQty", wayQty, qEntityRowCount);//在途量
                    this.View.Model.SetValue("F_BOA_PurQty", purQty, qEntityRowCount);//请购量
                    this.View.Model.SetValue("F_BOA_NoPurQty", noPurQty, qEntityRowCount);//未批请购量
                    this.View.Model.SetValue("F_BOA_UnclaimedQty", SqlHelper.IsMrpStock(this.Context, item.FStockId) ? unclaimedQty : 0, qEntityRowCount);//未领数量
                    this.View.Model.SetValue("F_BOA_InDate", dateInfo["inStockDate"], qEntityRowCount);//最近入库日期
                    this.View.Model.SetValue("F_BOA_OutDate", dateInfo["outStockDate"], qEntityRowCount);//最近出库日期
                    qEntityRowCount++;
                }
                this.View.UpdateView("F_BOA_QEntity");
                var entity = this.View.BillBusinessInfo.GetEntity("F_BOA_QEntity");
                var entityData = this.View.Model.GetEntityDataObject(entity);
                this.View.Model.InsertEntryRow("F_BOA_QEntity", qEntityRowCount);
                this.View.Model.SetValue("F_BOA_StockName", "合计行：", qEntityRowCount);
                this.View.Model.SetValue("F_BOA_AbleQty", inventoryData.Sum(t => t.FBaseAvbQty), qEntityRowCount);
                this.View.Model.SetValue("F_BOA_CurrentQty", inventoryData.Sum(t => t.FBaseQty), qEntityRowCount);
                this.View.Model.SetValue("F_BOA_SuQty", inventoryData.Sum(t => t.FSecQty), qEntityRowCount);
                this.View.Model.SetValue("F_BOA_WayQty", entityData.Sum(t => Convert.ToDecimal(t["F_BOA_WayQty"])), qEntityRowCount);//在途量
                this.View.Model.SetValue("F_BOA_PurQty", entityData.Sum(t => Convert.ToDecimal(t["F_BOA_PurQty"])), qEntityRowCount);//请购量
                this.View.Model.SetValue("F_BOA_NoPurQty", entityData.Sum(t => Convert.ToDecimal(t["F_BOA_NoPurQty"])), qEntityRowCount);//未批请购量
                this.View.UpdateView("F_BOA_QEntity");
            }
        }

        /// <summary>
        /// 清除分录
        /// </summary>
        /// <param name="entryKey"></param>
        private void ClearEntryRow(string entryKey)
        {
            this.View.Model.DeleteEntryData(entryKey);
            //this.View.UpdateView(entryKey);
        }

        /// <summary>
        /// 打开列表
        /// </summary>
        /// <param name="filterStr">过滤参数</param>
        /// <param name="formId">单据标识</param>
        private void ShowListForm(string filterStr, string formId, long orgId)
        {
            var listShowParameter = new ListShowParameter
            {
                FormId = formId,
                IsShowApproved = false,
                IsLookUp = false,
                ListType = Convert.ToInt32(BOSEnums.Enu_ListType.List),
                UseOrgId = orgId
            };
            //listShowParameter.OpenStyle.ShowType = ShowType.MainNewTabPage;
            listShowParameter.ListFilterParameter.Filter = filterStr;
            this.View.ShowForm(listShowParameter);
        }

        /// <summary>
        /// 打开单据
        /// </summary>
        /// <param name="formId">单据内码</param>
        /// <param name="formFlag">单据标识</param>
        private void ShowFormInfo(string formId, string formFlag)
        {
            var showParameter = new BillShowParameter
            {
                FormId = formFlag,
                PKey = formId,
                Status = OperationStatus.VIEW
            };
            showParameter.OpenStyle.ShowType = ShowType.Modal;
            this.View.ShowForm(showParameter);
        }

        /// <summary>
        /// 获取可用即时库存
        /// </summary>
        /// <param name="orgNumber">组织编码</param>
        /// <param name="materialNumber">物料编码</param>
        /// <returns></returns>
        private decimal GetAbleQty(string orgNumber, string materialNumber)
        {
            var stockInfo = SqlHelper.GetStockNumberAndId(this.Context);
            var stockNmber = string.Join(",", stockInfo.Select(t => t["FNUMBER"].ToString()));
            var inventoryInfo = InventoryHelper.GetInventoryInfo(this.Context, orgNumber, materialNumber, stockNmber);
            if (inventoryInfo == null)
            {
                //this.View.ShowErrMessage("查询即时库存失败，请联系管理员！");
                return 0m;
            }
            if (inventoryInfo.Data == null)
            {
                //this.View.ShowErrMessage("未查询到即时库存，请重新选择条件！");
                return 0m;
            }
            return inventoryInfo.Data.Sum(t => t.FBaseQty);
        }

        /// <summary>
        /// 在途量
        /// </summary>
        private void GetWayQty(long materialId, long orgId, long stockId)
        {
            var sqlStr = $@"select t3.FID
from t_PUR_POOrderEntry t1 --采购订单分录
join t_PUR_POOrderEntry_R t2 on t1.FENTRYID = t2.FENTRYID --采购订单分录_关联信息
join t_PUR_POOrder t3 on t1.FID = t3.FID --采购订单单据头
where t1.FMATERIALID = {materialId} and t3.FPURCHASEORGID = {orgId}
and t2.FREMAINSTOCKINQTY>0 and t1.F_BOA_CKMC = {stockId}
and t3.FCLOSESTATUS <> 'B'
and t3.FDOCUMENTSTATUS = 'C'
group by t3.FID";
            var result = DBUtils.ExecuteDynamicObject(this.Context, sqlStr);
            if (result.Count > 0)
            {
                var idList = string.Join(",", result.Select(t => t["FID"]));
                var filterStr = $"fid in ({idList}) and FMATERIALID = {materialId}";
                ShowListForm(filterStr, "PUR_PurchaseOrder", orgId);
            }
            else
            {
                this.View.ShowWarnningMessage("未查询到明细！");
            }
        }

        /// <summary>
        /// 请购量
        /// </summary>
        private void GetPurQty(long materialId, long orgId, long stockId)
        {
            var sqlStr = $@"select t2.FID
from T_PUR_ReqEntry t1 --采购申请单分录
join T_PUR_Requisition t2 on t1.FID = t2.FID --采购申请单单据头
join T_PUR_ReqEntry_R t3 on t1.FENTRYID = t3.FENTRYID --采购申请单分录_关联关系
join T_PUR_ReqEntry_S t4 on t1.FENTRYID = t4.FENTRYID --采购申请单分录_货源
where t2.FDOCUMENTSTATUS = 'C' and t3.FORDERJNBASEQTY=0  and t1.FREQQTY>0 and t2.FCLOSESTATUS <> 'B'
and t1.FMATERIALID =  {materialId} and t2.FAPPLICATIONORGID = {orgId} and t4.FSTOCKID = {stockId}
group by t2.FID";
            var result = DBUtils.ExecuteDynamicObject(this.Context, sqlStr);
            if (result.Count > 0)
            {
                var idList = string.Join(",", result.Select(t => t["FID"]));
                var filterStr = $"fid in ({idList}) and FMATERIALID = {materialId}";
                ShowListForm(filterStr, "PUR_Requisition", orgId);
            }
            else
            {
                this.View.ShowWarnningMessage("未查询到明细！");
            }
        }

        /// <summary>
        /// 未批请购量
        /// </summary>
        private void GetUnPurQty(long materialId, long orgId, long stockId)
        {
            var sqlStr = $@"select t2.FID
from T_PUR_ReqEntry t1 --采购申请单分录
join T_PUR_Requisition t2 on t1.FID = t2.FID --采购申请单单据头
join T_PUR_ReqEntry_S t4 on t1.FENTRYID = t4.FENTRYID --采购申请单分录_货源
where t2.FDOCUMENTSTATUS <> 'C' and t1.FREQQTY>0 and t2.FCLOSESTATUS <> 'B'
and t1.FMATERIALID = {materialId} and t2.FAPPLICATIONORGID = {orgId} and t4.FSTOCKID = {stockId}
group by t2.FID";
            var result = DBUtils.ExecuteDynamicObject(this.Context, sqlStr);
            if (result.Count > 0)
            {
                var idList = string.Join(",", result.Select(t => t["FID"]));
                var filterStr = $"fid in ({idList}) and FMATERIALID = {materialId}";
                ShowListForm(filterStr, "PUR_Requisition", orgId);
            }
            else
            {
                this.View.ShowWarnningMessage("未查询到明细！");
            }
        }

        /// <summary>
        /// 未领数量
        /// </summary>
        private void GetUnGetQty(long materialId, long orgId)
        {
            var sqlStr = $@"select t3.FID
from T_PRD_PPBOMENTRY t1 --生产用料清单分录
join T_PRD_PPBOMENTRY_Q t2 on t1.FENTRYID = t2.FENTRYID --生产用料清单分录_表体数量字段
join T_PRD_PPBOM t3 on t1.FID = t3.FID --生产用料清单单据头
join T_PRD_MOENTRY t4 on t3.FMOID = t4.FID and t3.FMOENTRYID = t4.FENTRYID --生产订单分录
join T_PRD_MOENTRY_A t5 on t4.FENTRYID = t5.FENTRYID --生产订单分录_生产执行数据
where t5.FSTATUS not in (6,7) and t2.FNOPICKEDQTY>0
and t1.FMATERIALID = {materialId} and t3.FPRDORGID = {orgId}
and t3.FDOCUMENTSTATUS = 'C'
group by t3.FID";
            var result = DBUtils.ExecuteDynamicObject(this.Context, sqlStr);
            if (result.Count > 0)
            {
                var idList = string.Join(",", result.Select(t => t["FID"]));
                var filterStr = $"fid in ({idList}) and FMaterialID2 = {materialId}";
                ShowListForm(filterStr, "PRD_PPBOM", orgId);
            }
            else
            {
                this.View.ShowWarnningMessage("未查询到明细！");
            }
        }

        /// <summary>
        /// 最近出库日期
        /// </summary>
        private void GetOutDate(long materialId, long orgId, long stockId)
        {
            var sqlStr = $@"/*dialect*/select top 1 t.type,t.fid
from (
select top 1 t1.FDATE,'PRD_PickMtrl' as type,t1.fid
from T_PRD_PICKMTRL t1 --生产领料单
join T_PRD_PICKMTRLDATA t2 on t2.FID = t1.FID --生产领料单明细
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID =  {materialId}
and t1.FSTOCKORGID = {orgId}
and t2.FSTOCKID = {stockId}
order by t1.FDATE desc
union all
select top 1 t1.FDATE,'STK_MisDelivery' as type,t1.fid
from T_STK_MISDELIVERY t1 --其他出库单
join T_STK_MISDELIVERYENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID = {materialId}
and t1.FSTOCKORGID = {orgId}
and t2.FSTOCKID = {stockId}
order by t1.FDATE desc
union all
select top 1 t1.FDATE,'SAL_OUTSTOCK' as type,t1.fid
from T_SAL_OUTSTOCK t1 --销售出库单
join T_SAL_OUTSTOCKENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID = {materialId}
and t1.FSTOCKORGID = {orgId}
and t2.FSTOCKID = {stockId}
order by t1.FDATE desc
) t order by t.FDate desc";
            var result = DBUtils.ExecuteDynamicObject(this.Context, sqlStr);
            if (result.Count > 0)
            {
                var fid = result[0]["fid"];
                var formId = result[0]["type"];
                var filterStr = $"fid = {fid} and FMaterialId = {materialId}";
                ShowListForm(filterStr, formId.ToString(), orgId);
            }
            else
            {
                this.View.ShowWarnningMessage("未查询到明细！");
            }
        }

        /// <summary>
        /// 最近入库日期
        /// </summary>
        private void GetInDate(long materialId, long orgId, long stockId)
        {
            var sqlStr = $@"/*dialect*/select top 1 t.type,t.fid
from (
select top 1 t1.FDATE,'STK_InStock' as type,t1.fid
from t_STK_InStock t1 --采购入库单
join T_STK_INSTOCKENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID = {materialId}
and t1.FSTOCKORGID = {orgId}
and t2.FSTOCKID = {stockId}
order by t1.FDATE desc
union all
select top 1 t1.FDATE,'PRD_INSTOCK' as type,t1.fid
from T_PRD_INSTOCK t1 --生产入库单
join T_PRD_INSTOCKENTRY t2 on t1.FID = t2.FID 
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID = {materialId}
and t1.FSTOCKORGID = {orgId}
and t2.FSTOCKID = {stockId}
order by t1.FDATE desc
union all
select top 1 t1.FDATE,'STK_MISCELLANEOUS' as type,t1.fid
from T_STK_MISCELLANEOUS t1 --其他入库单
join T_STK_MISCELLANEOUSENTRY t2 on t1.FID = t2.FID
where t1.FDOCUMENTSTATUS = 'C'
and t2.FMATERIALID = {materialId}
and t1.FSTOCKORGID = {orgId}
and t2.FSTOCKID = {stockId}
order by t1.FDATE desc ) t order by t.fdate desc";
            var result = DBUtils.ExecuteDynamicObject(this.Context, sqlStr);
            if (result.Count > 0)
            {
                var fid = result[0]["fid"];
                var formId = result[0]["type"];
                var filterStr = $"fid = {fid} and FMaterialId = {materialId}";
                ShowListForm(filterStr, formId.ToString(), orgId);
            }
            else
            {
                this.View.ShowWarnningMessage("未查询到明细！");
            }
        }
    }
}
