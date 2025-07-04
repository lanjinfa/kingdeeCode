using BOA.YD.JYFX.PlugIns.Dtos;
using BOA.YD.JYFX.PlugIns.Helpers;
using Kingdee.BOS;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.DynamicForm.PlugIn.ControlModel;
using Kingdee.BOS.Core.Enums;
using Kingdee.BOS.Core.List;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.ControlElement;
using Kingdee.BOS.Core.Metadata.EntityElement;
using Kingdee.BOS.Core.Metadata.FieldElement;
using Kingdee.BOS.KDThread;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Orm.Metadata.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace BOA.YD.JYFX.PlugIns.Plugins
{
    [HotUpdate]
    [Description("单据生成工作台")]
    public class BillCreatePlatformPlugin : AbstractDynamicFormPlugIn
    {
        private const string srcEntityKey = "F_BOA_SrcEntity";//源数据单据体标识
        private const string targetEntityKey = "F_BOA_TargetEntity";//目标数据单据体标识
        private const string linkSrcEntityKey = "F_BOA_Entity";//关联源数据单据体和目标数据单据体的单据体标识
        private BusinessInfo _currInfo = null;//当前界面元数据
        private LayoutInfo _currLayout = null;//当前界面布局元数据
        private string billTypeKey = string.Empty;//单据类型标识
        private string billTypeName = string.Empty;//单据类型名称
        private DynamicObjectCollection fieldMapList = null;//单据生成配置信息
        private FormMetadata srcFormMetaData;//源单元数据
        private DynamicObject[] selectQueryList;//需要同步的数据
        private DynamicObjectCollection orgConfig;//法人组织配置表
        private DynamicObjectCollection stockConfig;//法人仓库配置表
        private DynamicObjectCollection departmentConfig;//法人部门配置表
        private bool isFastBusiness = true;//当前组织是否快消事业部
        private DateTime period = DateTime.MinValue;//生成期间
        private ListSelectedRowCollection selectedData;//选单数据
        private DynamicObject apiConfig;//api配置信息
        private InventoryDto inventoryList;//法人即时库存数据记录
        private List<string> updateSyncStatus = new List<string>();//记录更新永不传输标记sql语句
        private DynamicObjectCollection rEFUNDBILLSrcBillInfo;//付款退款单关联付款单信息
        //private string _errorMaterialMsg = string.Empty;//法人物料为空记录
        //private LoginStatus loginStatus1;

        /// <summary> 
        /// 动态设置表单业务元数据
        /// </summary>     
        /// <param name = "e" ></ param >
        public override void OnSetBusinessInfo(SetBusinessInfoArgs e)
        {
            base.OnSetBusinessInfo(e);
            var currMeta = (FormMetadata)ObjectUtils.CreateCopy(this.View.OpenParameter.FormMetaData);
            _currInfo = currMeta.BusinessInfo;
            _currLayout = currMeta.GetLayoutInfo();
            e.BusinessInfo = _currInfo;
            e.BillBusinessInfo = _currInfo;
        }

        /// <summary> 
        /// 动态设置表单布局元数据   
        /// </summary>      
        /// <param name = "e" ></ param >
        public override void OnSetLayoutInfo(SetLayoutInfoArgs e)
        {
            base.OnSetLayoutInfo(e);
            e.LayoutInfo = _currLayout;
            e.BillLayoutInfo = _currLayout;
        }

        public override void AfterBindData(EventArgs e)
        {
            base.AfterBindData(e);
            var orgInfo = this.View.Model.GetValue("F_BOA_OrgId") as DynamicObject;
            var orgName = orgInfo["Name"].ToString();
            isFastBusiness = orgName == "快消事业部";
            var billInfo = this.View.Model.GetValue("F_BOA_BillSelect");
            billTypeKey = (billInfo as DynamicObject)["Id"].ToString();
            billTypeName = (billInfo as DynamicObject)["Name"].ToString();
            fieldMapList = SqlHelper.GetSyncFieldMapList(this.Context, billTypeKey);
            if (fieldMapList.Count == 0)
            {
                this.View.ShowErrMessage($"[{billTypeName}]未配置同步字段信息！");
                return;
            }
            ReBuildFields();
            SetFieldsShow();
            SetDisCountFieldsShow();
        }

        public override void AfterButtonClick(AfterButtonClickEventArgs e)
        {
            base.AfterButtonClick(e);
            if (e.Key.EqualsIgnoreCase("F_BOA_SelectData"))
            {
                var orgInfo = this.View.Model.GetValue("F_BOA_OrgId");
                if (orgInfo == null)
                {
                    this.View.ShowErrMessage("请选择组织！");
                    return;
                }
                var billInfo = this.View.Model.GetValue("F_BOA_BillSelect");
                if (billInfo == null)
                {
                    this.View.ShowErrMessage("请选择单据类型！");
                    return;
                }
                var orgId = Convert.ToInt64((orgInfo as DynamicObject)["Id"]);
                var billType = (billInfo as DynamicObject)["Id"].ToString();
                OpenListByBillType(billType, orgId);
            }
            else if (e.Key.EqualsIgnoreCase("F_BOA_Sync"))
            {
                SyncData();
            }
            else if (e.Key.EqualsIgnoreCase("F_BOA_UpdateSync"))
            {
                if (updateSyncStatus.Count == 0)
                {
                    this.View.ShowWarnningMessage("请先勾选或取消对应的永不传输字段");
                    return;
                }
                SqlHelper.UpdateBillSyncStatus(this.Context, updateSyncStatus);
                updateSyncStatus = new List<string>();
                FillEntryData();
                this.View.ShowMessage("更新成功！");
            }
        }

        public override void EntityRowClick(EntityRowClickEventArgs e)
        {
            //base.EntityRowClick(e);
            if (e.Key.EqualsIgnoreCase(srcEntityKey))
            {
                this.View.SetEntityFocusRow(linkSrcEntityKey, e.Row);
            }
        }

        public override void EntryBarItemClick(BarItemClickEventArgs e)
        {
            base.EntryBarItemClick(e);
            if (e.BarItemKey.EqualsIgnoreCase("BOA_ClearData"))//清空
            {
                if (this.View.Model.GetEntryRowCount(srcEntityKey) > 0)
                {
                    this.View.Model.SetValue("F_BOA_SrcAmount", 0);
                    this.View.Model.SetValue("F_BOA_TargetSrcAmount", 0);
                    this.View.Model.DeleteEntryData(srcEntityKey);
                    //this.View.Model.DeleteEntryData(targetEntityKey);
                    this.View.Model.DeleteEntryData(linkSrcEntityKey);
                }
            }
            else if (e.BarItemKey.EqualsIgnoreCase("BOA_deleteRow"))//目标单据删除行判断
            {
                if (this.View.Model.GetEntryRowCount(targetEntityKey) == 1)
                {
                    e.Cancel = true;
                    this.View.ShowWarnningMessage("必须要有一行数据才能同步！");
                    return;
                }
            }
            else if (e.BarItemKey.EqualsIgnoreCase("BOA_Import"))//引入
            {
                OpenExcelImportForm();
            }
            else if (e.BarItemKey.EqualsIgnoreCase("BOA_Delete"))//源单删除行
            {
                var currentRow = this.View.Model.GetEntryCurrentRowIndex(srcEntityKey);
                this.View.Model.DeleteEntryRow(srcEntityKey, currentRow);
                this.View.Model.DeleteEntryRow(linkSrcEntityKey, currentRow);
                this.View.UpdateView(targetEntityKey);
                ComputeAmountFieldValue();
                ComputeAmountFieldValue1();
            }
        }

        public override void BeforeUpdateValue(BeforeUpdateValueEventArgs e)
        {
            base.BeforeUpdateValue(e);
            if (e.Key.EqualsIgnoreCase("F_BOA_BillSelect"))
            {
                if (e.Value == null)
                {
                    e.Cancel = true;
                    this.View.ShowErrMessage("单据类型不能为空！");
                    return;
                }
                var newBillTypeKey = (e.Value as DynamicObject)["Id"].ToString();
                var newBillTypeName = (e.Value as DynamicObject)["Name"].ToString();
                if (newBillTypeKey == "ER_ExpReimbursement_Travel")
                {
                    newBillTypeKey = "ER_ExpReimbursement";
                    newBillTypeName = "费用报销单";
                }
                var newFieldMapList = SqlHelper.GetSyncFieldMapList(this.Context, newBillTypeKey);
                if (newFieldMapList.Count == 0)
                {
                    e.Cancel = true;
                    this.View.ShowErrMessage($"[{newBillTypeName}]未配置同步字段信息！");
                    return;
                }
                billTypeKey = newBillTypeKey;
                fieldMapList = newFieldMapList;
                billTypeName = newBillTypeName;
                ReBuildFields();
                if (billTypeName == "费用报销单" || billTypeName == "付款单")
                {
                    this.View.Model.SetValue("F_BOA_CbBillStatus", 3);
                }
                SetFieldsShow();
                SetDisCountFieldsShow();
            }
            else if (e.Key.EqualsIgnoreCase("F_BOA_OrgId"))//组织
            {
                if (e.Value == null)
                {
                    e.Cancel = true;
                    this.View.ShowErrMessage("组织不能为空！");
                    return;
                }
                isFastBusiness = (e.Value as DynamicObject)["Name"].ToString() == "事业部";
                if (this.View.Model.GetEntryRowCount(srcEntityKey) > 0)
                {
                    this.View.Model.DeleteEntryData(srcEntityKey);
                    this.View.Model.DeleteEntryData(targetEntityKey);
                    this.View.Model.DeleteEntryData(linkSrcEntityKey);
                }
                SetDisCountFieldsShow();
            }
            else if (e.Key.EqualsIgnoreCase("F_BOA_Period"))//生成期间
            {
                if (e.Value.IsNullOrEmptyOrWhiteSpace())
                {
                    e.Cancel = true;
                    this.View.ShowErrMessage("生成期间不能为空！");
                    return;
                }
                else
                {
                    period = Convert.ToDateTime(e.Value);
                }
            }
        }

        public override void AfterBarItemClick(AfterBarItemClickEventArgs e)
        {
            base.AfterBarItemClick(e);
            if (e.BarItemKey.EqualsIgnoreCase("BOA_SyncLogSearch"))//同步日志查询
            {
                var listShowParameter = new ListShowParameter
                {
                    FormId = "BOA_SyncLog",
                    IsShowApproved = false,
                    IsLookUp = false,
                    ListType = Convert.ToInt32(BOSEnums.Enu_ListType.List)
                    //UseOrgId = Convert.ToInt64(reportFilterDto.OrgId)
                };
                listShowParameter.OpenStyle.ShowType = ShowType.MainNewTabPage;
                //listShowParameter.ListFilterParameter.Filter = filterStr;
                this.View.ShowForm(listShowParameter);
            }
        }

        public override void DataChanged(DataChangedEventArgs e)
        {
            base.DataChanged(e);
            if (billTypeName == "采购入库单")
            {
                if (e.Field.Key.EqualsIgnoreCase("target_FTaxPrice") || e.Field.Key.EqualsIgnoreCase("target_FRealQty")
                    || e.Field.Key.EqualsIgnoreCase("target_FEntryTaxRate"))
                {
                    var realQty = Convert.ToDecimal(this.View.Model.GetValue("target_FRealQty", e.Row));
                    var taxPrice = Convert.ToDecimal(this.View.Model.GetValue("target_FTaxPrice", e.Row));
                    var taxRate = Convert.ToDecimal(this.View.Model.GetValue("target_FEntryTaxRate", e.Row)) * 0.01m;
                    var allAmount = realQty * taxPrice;
                    var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                    this.View.Model.SetValue("target_FEntryTaxAmount", taxAmount, e.Row);//税额
                    this.View.Model.SetValue("target_FAllAmount", allAmount, e.Row);//价税合计
                    this.View.Model.SetValue("target_FBaseUnitQty", realQty, e.Row);//库存基本数量
                    this.View.Model.SetValue("target_FPriceUnitQty", realQty, e.Row);//计价数量
                    this.View.Model.SetValue("target_FAmount", allAmount - taxAmount, e.Row);//金额
                    ComputeAmountFieldValue1();
                }
                if (e.Field.Key.EqualsIgnoreCase("target_FMaterialId"))
                {
                    if (e.NewValue == null)
                    {
                        return;
                    }
                    var date = this.View.Model.GetValue("target_FCorpDateTime", 0);
                    var org = this.View.Model.GetValue("target_FCorpOrgNumber", 0);
                    var stock = this.View.Model.GetValue("target_FCorpStockId", 0);
                    var materialInfo = SqlHelper.GetMaterialInfoById(this.Context, e.NewValue.ToString());
                    this.View.Model.SetValue("target_FCorpDateTime", date, e.Row);
                    this.View.Model.SetValue("target_FCorpOrgNumber", org, e.Row);
                    this.View.Model.SetValue("target_FCorpStockId", stock, e.Row);
                    this.View.Model.SetValue("target_FUnitID", materialInfo["FSTOREUNITID"], e.Row);//库存单位
                    this.View.Model.SetValue("target_FBaseUnitID", materialInfo["FBASEUNITID"], e.Row);//基本单位
                    this.View.Model.SetValue("target_FPriceUnitID", materialInfo["FPURCHASEPRICEUNITID"], e.Row);//采购计价单位
                    var materialName = materialInfo["FName"].ToString();
                    var materialNumber = materialInfo["FNUMBER"].ToString();
                    if (isFastBusiness)
                    {
                        materialNumber = materialInfo["F_BOA_BOXCODE"].ToString().Trim();
                    }
                    else
                    {
                        var cwNumber = materialInfo["F_BOA_CWFNUMBER"].ToString().Trim();
                        if (cwNumber != "")
                        {
                            materialNumber = cwNumber;
                        }
                        var cwName = materialInfo["F_BOA_CWFNAME"].ToString().Trim();
                        if (cwName != "")
                        {
                            materialName = cwName;
                        }
                    }
                    this.View.Model.SetValue("target_FCorpMaterialId", materialNumber, e.Row);//物料编码
                    this.View.Model.SetValue("target_MaterialName", materialName, e.Row);//物料名称
                }
            }
            else if (billTypeName == "销售出库单")
            {
                if (e.Field.Key.EqualsIgnoreCase("target_FRealQty") || e.Field.Key.EqualsIgnoreCase("target_FAllAmount")
                    || e.Field.Key.EqualsIgnoreCase("target_FEntryTaxRate"))
                {
                    var realQty = Convert.ToDecimal(this.View.Model.GetValue("target_FRealQty", e.Row));//数量
                    var allAmount = Convert.ToDecimal(this.View.Model.GetValue("target_FAllAmount", e.Row));//价税合计
                    var taxRate = Convert.ToDecimal(this.View.Model.GetValue("target_FEntryTaxRate", e.Row)) * 0.01m;//税率
                    if (this.View.Model.GetValue("target_FUnitID", e.Row) == null)
                    {
                        return;
                    }
                    var stockUnitId = (this.View.Model.GetValue("target_FUnitID", e.Row) as DynamicObject)["Id"].ToString();//库存单位
                    var priceUnitId = (this.View.Model.GetValue("target_FPriceUnitId", e.Row) as DynamicObject)["Id"].ToString();//计价单位
                    if (stockUnitId == priceUnitId)//单位一样不需要单位换算
                    {
                        var taxPrice = allAmount / realQty;
                        var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                        this.View.Model.SetValue("target_FEntryTaxAmount", taxAmount, e.Row);//税额
                        this.View.Model.SetValue("target_FTaxPrice", taxPrice, e.Row);//含税单价
                        this.View.Model.SetValue("target_FPriceUnitQty", realQty, e.Row);//计价数量
                        this.View.Model.SetValue("target_FAmount", allAmount - taxAmount, e.Row);//金额
                    }
                    else//单位不一样需要单位换算
                    {
                        var materialId = (this.View.Model.GetValue("target_FMaterialId", e.Row) as DynamicObject)["Id"].ToString();
                        var unitInfoList = SqlHelper.GetUnitInfo(this.Context, materialId);
                        var unitItem1 = unitInfoList.FirstOrDefault(t => t["FCURRENTUNITID"].ToString() == stockUnitId
                                               && t["FDESTUNITID"].ToString() == priceUnitId);
                        if (unitItem1 != null)
                        {
                            var num1 = Convert.ToDecimal(unitItem1["FCONVERTDENOMINATOR"]);//换算比例1
                            var num2 = Convert.ToDecimal(unitItem1["FCONVERTNUMERATOR"]);//换算比例2
                            var realQty1 = realQty * (num2 / num1);
                            var taxPrice = allAmount / realQty1;
                            var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                            this.View.Model.SetValue("target_FEntryTaxAmount", taxAmount, e.Row);//税额
                            this.View.Model.SetValue("target_FTaxPrice", taxPrice, e.Row);//含税单价
                            this.View.Model.SetValue("target_FPriceUnitQty", realQty1, e.Row);//计价数量
                            this.View.Model.SetValue("target_FAmount", allAmount - taxAmount, e.Row);//金额
                        }
                        else
                        {
                            var unitItem2 = unitInfoList.FirstOrDefault(t => t["FCURRENTUNITID"].ToString() == priceUnitId
                                               && t["FDESTUNITID"].ToString() == stockUnitId);
                            var num1 = Convert.ToDecimal(unitItem2["FCONVERTDENOMINATOR"]);//换算比例1
                            var num2 = Convert.ToDecimal(unitItem2["FCONVERTNUMERATOR"]);//换算比例2
                            var realQty1 = realQty * (num1 / num2);
                            var taxPrice = allAmount / realQty1;
                            var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                            this.View.Model.SetValue("target_FEntryTaxAmount", taxAmount, e.Row);//税额
                            this.View.Model.SetValue("target_FTaxPrice", taxPrice, e.Row);//含税单价
                            this.View.Model.SetValue("target_FPriceUnitQty", realQty1, e.Row);//计价数量
                            this.View.Model.SetValue("target_FAmount", allAmount - taxAmount, e.Row);//金额
                        }
                    }
                    if (e.Field.Key.EqualsIgnoreCase("target_FRealQty"))
                    {
                        var materialNumber = this.View.Model.GetValue("target_FCorpMaterialId", e.Row).ToString();
                        var stockNumber = this.View.Model.GetValue("target_FCorpStockId", e.Row).ToString();
                        var orgNumber = this.View.Model.GetValue("target_FCorpOrgNumber", e.Row).ToString();
                        var oldQty = Convert.ToDecimal(e.OldValue);
                        var dataobj = this.View.Model.DataObject;
                        var linkData = dataobj[linkSrcEntityKey] as DynamicObjectCollection;
                        var inventoryMsg = InventoryHelper.OutStockBillInventory1(materialNumber, orgNumber, stockNumber,
                             inventoryList, linkData);
                        if (inventoryMsg != string.Empty)
                        {
                            this.View.ShowWarnningMessage(inventoryMsg);
                            return;
                        }
                    }
                    ComputeAmountFieldValue1();
                }
                else if (e.Field.Key.EqualsIgnoreCase("target_FMaterialId"))
                {
                    if (e.NewValue == null)
                    {
                        return;
                    }
                    var materialInfo = SqlHelper.GetMaterialInfoById(this.Context, e.NewValue.ToString());
                    var date = this.View.Model.GetValue("target_FCorpDateTime", 0);
                    var org = this.View.Model.GetValue("target_FCorpOrgNumber", 0);
                    var stock = this.View.Model.GetValue("target_FCorpStockId", 0);
                    var inventoryQty = inventoryList.Data
                                   .Where(t => t.FMaterialNumber == materialInfo["FNUMBER"].ToString()
                                   && t.FStockNumber == stock.ToString()
                                   && t.FStockOrgNumber == org.ToString())
                                  .Sum(t => t.FBaseQty);
                    this.View.Model.SetValue("target_FCorpDateTime", date, e.Row);
                    this.View.Model.SetValue("target_FCorpOrgNumber", org, e.Row);
                    this.View.Model.SetValue("target_FCorpStockId", stock, e.Row);
                    this.View.Model.SetValue("target_FInventory", inventoryQty, e.Row);//即时库存
                    this.View.Model.SetValue("target_FUnitID", materialInfo["FSTOREUNITID"], e.Row);//库存单位
                    //this.View.Model.SetValue("target_FBaseUnitID", materialInfo["FBASEUNITID"], e.Row);//基本单位
                    this.View.Model.SetValue("target_FPriceUnitID", materialInfo["FSALEPRICEUNITID"], e.Row);//销售计价单位
                    var materialName = materialInfo["FName"].ToString();
                    var materialNumber = materialInfo["FNUMBER"].ToString();
                    if (isFastBusiness)
                    {
                        materialNumber = materialInfo["F_BOA_BOXCODE"].ToString().Trim();
                    }
                    else
                    {
                        var cwNumber = materialInfo["F_BOA_CWFNUMBER"].ToString().Trim();
                        if (cwNumber != "")
                        {
                            materialNumber = cwNumber;
                        }
                        var cwName = materialInfo["F_BOA_CWFNAME"].ToString().Trim();
                        if (cwName != "")
                        {
                            materialName = cwName;
                        }
                    }
                    this.View.Model.SetValue("target_FCorpMaterialId", materialNumber, e.Row);//物料编码
                    this.View.Model.SetValue("target_MaterialName", materialName, e.Row);//物料名称
                    //this.View.UpdateView();
                }
            }
            else if (billTypeName == "采购退料单")
            {
                if (e.Field.Key.EqualsIgnoreCase("target_FTAXPRICE") || e.Field.Key.EqualsIgnoreCase("target_FRMREALQTY"))
                {
                    var realQty = Convert.ToDecimal(this.View.Model.GetValue("target_FRMREALQTY", e.Row));
                    var taxPrice = Convert.ToDecimal(this.View.Model.GetValue("target_FTAXPRICE", e.Row));
                    var taxRate = Convert.ToDecimal(this.View.Model.GetValue("target_FENTRYTAXRATE", e.Row)) * 0.01m;
                    var allAmount = realQty * taxPrice;
                    var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                    this.View.Model.SetValue("target_FENTRYTAXAMOUNT", taxAmount, e.Row);//税额
                    this.View.Model.SetValue("target_FALLAMOUNT", allAmount, e.Row);//价税合计
                    this.View.Model.SetValue("target_FBASEUNITQTY", realQty, e.Row);//库存基本数量
                    this.View.Model.SetValue("target_FPRICEUNITQTY", realQty, e.Row);//计价数量
                    this.View.Model.SetValue("target_FAmount", allAmount - taxAmount, e.Row);//金额
                    ComputeAmountFieldValue1();
                }
                if (e.Field.Key.EqualsIgnoreCase("target_FMATERIALID"))
                {
                    if (e.NewValue == null)
                    {
                        return;
                    }
                    var date = this.View.Model.GetValue("target_FCorpDateTime", 0);
                    var org = this.View.Model.GetValue("target_FCorpOrgNumber", 0);
                    var stock = this.View.Model.GetValue("target_FCorpStockId", 0);
                    var materialInfo = SqlHelper.GetMaterialInfoById(this.Context, e.NewValue.ToString());
                    this.View.Model.SetValue("target_FCorpDateTime", date, e.Row);
                    this.View.Model.SetValue("target_FCorpOrgNumber", org, e.Row);
                    this.View.Model.SetValue("target_FCorpStockId", stock, e.Row);
                    this.View.Model.SetValue("target_FUnitID", materialInfo["FSTOREUNITID"], e.Row);//库存单位
                    this.View.Model.SetValue("target_FBASEUNITID", materialInfo["FBASEUNITID"], e.Row);//基本单位
                    this.View.Model.SetValue("target_FPRICEUNITID", materialInfo["FPURCHASEPRICEUNITID"], e.Row);//采购计价单位
                    var materialName = materialInfo["FName"].ToString();
                    var materialNumber = materialInfo["FNUMBER"].ToString();
                    if (isFastBusiness)
                    {
                        materialNumber = materialInfo["F_BOA_BOXCODE"].ToString().Trim();
                    }
                    else
                    {
                        var cwNumber = materialInfo["F_BOA_CWFNUMBER"].ToString().Trim();
                        if (cwNumber != "")
                        {
                            materialNumber = cwNumber;
                        }
                        var cwName = materialInfo["F_BOA_CWFNAME"].ToString().Trim();
                        if (cwName != "")
                        {
                            materialName = cwName;
                        }
                    }
                    this.View.Model.SetValue("target_FCorpMaterialId", materialNumber, e.Row);//物料编码
                    this.View.Model.SetValue("target_MaterialName", materialName, e.Row);//物料名称
                }
            }
            else if (billTypeName == "销售退货单")
            {
                if (e.Field.Key.EqualsIgnoreCase("target_FRealQty") || e.Field.Key.EqualsIgnoreCase("target_FTaxPrice"))
                {
                    var realQty = Convert.ToDecimal(this.View.Model.GetValue("target_FRealQty", e.Row));//数量
                    var taxPrice = Convert.ToDecimal(this.View.Model.GetValue("target_FTaxPrice", e.Row));//含税单价
                    //var allAmount = Convert.ToDecimal(this.View.Model.GetValue("target_FAllAmount", e.Row));//价税合计
                    var taxRate = Convert.ToDecimal(this.View.Model.GetValue("target_FEntryTaxRate", e.Row)) * 0.01m;//税率
                    if (this.View.Model.GetValue("target_FUnitID", e.Row) == null)
                    {
                        return;
                    }
                    var stockUnitId = (this.View.Model.GetValue("target_FUnitID", e.Row) as DynamicObject)["Id"].ToString();//库存单位
                    var priceUnitId = (this.View.Model.GetValue("target_FPriceUnitId", e.Row) as DynamicObject)["Id"].ToString();//计价单位
                    if (stockUnitId == priceUnitId)//单位一样不需要单位换算
                    {
                        //var taxPrice = allAmount / realQty;
                        var allAmount = realQty * taxPrice;
                        var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                        this.View.Model.SetValue("target_FEntryTaxAmount", taxAmount, e.Row);//税额
                        this.View.Model.SetValue("target_FAllAmount", allAmount, e.Row);//价税合计
                        this.View.Model.SetValue("target_FPriceUnitQty", realQty, e.Row);//计价数量
                        this.View.Model.SetValue("target_FAmount", allAmount - taxAmount, e.Row);//金额
                    }
                    else//单位不一样需要单位换算
                    {
                        var materialId = (this.View.Model.GetValue("target_FMaterialId", e.Row) as DynamicObject)["Id"].ToString();
                        var unitInfoList = SqlHelper.GetUnitInfo(this.Context, materialId);
                        var unitItem1 = unitInfoList.FirstOrDefault(t => t["FCURRENTUNITID"].ToString() == stockUnitId
                                        && t["FDESTUNITID"].ToString() == priceUnitId);
                        if (unitItem1 != null)
                        {
                            var num1 = Convert.ToDecimal(unitItem1["FCONVERTDENOMINATOR"]);//换算比例1
                            var num2 = Convert.ToDecimal(unitItem1["FCONVERTNUMERATOR"]);//换算比例2
                            var realQty1 = realQty * (num2 / num1);
                            var allAmount = realQty1 * taxPrice;
                            var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                            this.View.Model.SetValue("target_FEntryTaxAmount", taxAmount, e.Row);//税额
                            this.View.Model.SetValue("target_FAllAmount", allAmount, e.Row);//价税合计
                            this.View.Model.SetValue("target_FPriceUnitQty", realQty1, e.Row);//计价数量
                            this.View.Model.SetValue("target_FAmount", allAmount - taxAmount, e.Row);//金额
                        }
                        else
                        {
                            var unitItem2 = unitInfoList.FirstOrDefault(t => t["FCURRENTUNITID"].ToString() == priceUnitId
                                               && t["FDESTUNITID"].ToString() == stockUnitId);
                            var num1 = Convert.ToDecimal(unitItem2["FCONVERTDENOMINATOR"]);//换算比例1
                            var num2 = Convert.ToDecimal(unitItem2["FCONVERTNUMERATOR"]);//换算比例2
                            var realQty1 = realQty * (num1 / num2);
                            var allAmount = realQty1 * taxPrice;
                            var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                            this.View.Model.SetValue("target_FEntryTaxAmount", taxAmount, e.Row);//税额
                            this.View.Model.SetValue("target_FAllAmount", allAmount, e.Row);//价税合计
                            this.View.Model.SetValue("target_FPriceUnitQty", realQty1, e.Row);//计价数量
                            this.View.Model.SetValue("target_FAmount", allAmount - taxAmount, e.Row);//金额
                        }
                    }
                    ComputeAmountFieldValue1();
                }
                else if (e.Field.Key.EqualsIgnoreCase("target_FMaterialId"))
                {
                    if (e.NewValue == null)
                    {
                        return;
                    }
                    var materialInfo = SqlHelper.GetMaterialInfoById(this.Context, e.NewValue.ToString());
                    var date = this.View.Model.GetValue("target_FCorpDateTime", 0);
                    var org = this.View.Model.GetValue("target_FCorpOrgNumber", 0);
                    var stock = this.View.Model.GetValue("target_FCorpStockId", 0);
                    //var inventoryQty = inventoryList.Data
                    //               .Where(t => t.FMaterialNumber == materialInfo["FNUMBER"].ToString()
                    //               && t.FStockNumber == stock.ToString()
                    //               && t.FStockOrgNumber == org.ToString())
                    //              .Sum(t => t.FBaseQty);
                    this.View.Model.SetValue("target_FCorpDateTime", date, e.Row);
                    this.View.Model.SetValue("target_FCorpOrgNumber", org, e.Row);
                    this.View.Model.SetValue("target_FCorpStockId", stock, e.Row);
                    //this.View.Model.SetValue("target_FInventory", inventoryQty, e.Row);//即时库存
                    this.View.Model.SetValue("target_FUnitID", materialInfo["FSTOREUNITID"], e.Row);//库存单位
                    //this.View.Model.SetValue("target_FBaseunitId", materialInfo["FBASEUNITID"], e.Row);//基本单位
                    this.View.Model.SetValue("target_FPriceUnitID", materialInfo["FSALEPRICEUNITID"], e.Row);//销售计价单位
                    var materialName = materialInfo["FName"].ToString();
                    var materialNumber = materialInfo["FNUMBER"].ToString();
                    if (isFastBusiness)
                    {
                        materialNumber = materialInfo["F_BOA_BOXCODE"].ToString().Trim();
                    }
                    else
                    {
                        var cwNumber = materialInfo["F_BOA_CWFNUMBER"].ToString().Trim();
                        if (cwNumber != "")
                        {
                            materialNumber = cwNumber;
                        }
                        var cwName = materialInfo["F_BOA_CWFNAME"].ToString().Trim();
                        if (cwName != "")
                        {
                            materialName = cwName;
                        }
                    }
                    this.View.Model.SetValue("target_FCorpMaterialId", materialNumber, e.Row);//物料编码
                    this.View.Model.SetValue("target_MaterialName", materialName, e.Row);//物料名称
                }
            }
            if (e.Field.Key.EqualsIgnoreCase("src_F_BOA_NoSync"))
            {
                UpdateNoSyncFlag(e.Row);
            }
        }

        /// <summary>
        /// 根据源单类型打开对应单据列表
        /// </summary>
        /// <param name="BillType">单据类型</param>
        private void OpenListByBillType(string BillType, long orgId)
        {
            var listShowParameter = new ListShowParameter
            {
                FormId = BillType,
                IsShowApproved = true,
                IsLookUp = true,
                ListType = Convert.ToInt32(BOSEnums.Enu_ListType.List),
                UseOrgId = orgId
            };
            listShowParameter.OpenStyle.ShowType = ShowType.Modal;
            this.View.ShowForm(listShowParameter, delegate (FormResult result)
             {
                 var returnData = result.ReturnData;
                 if (!returnData.IsNullOrEmpty() && returnData is ListSelectedRowCollection)
                 {
                     selectedData = returnData as ListSelectedRowCollection;
                     FillEntryData();
                 }
             });
        }

        /// <summary>
        /// 动态构造列字段
        /// </summary>
        /// <param name="fieldList">需要构造的字段列表</param>
        private void ReBuildFields()
        {
            var srcBillKey = fieldMapList.First()["F_BOA_SYNCBILL"].ToString();//源单单据标识
            srcFormMetaData = MetaDataServiceHelper.GetFormMetaData(this.Context, srcBillKey);
            this.View.Model.DeleteEntryData(srcEntityKey);
            this.View.Model.DeleteEntryData(targetEntityKey);
            this.View.Model.DeleteEntryData(linkSrcEntityKey);
            //oldData = (DynamicObject)ObjectUtils.CreateCopy(this.View.Model.DataObject);
            // 获取单据体表格的元数据及外观
            Entity entity = _currInfo.GetEntity(srcEntityKey);
            var srcFieldCount = entity.Fields.Count;
            //删除原有的字段
            for (var i = 0; i < srcFieldCount; i++)
            {
                Field fld1 = entity.Fields[0];
                _currInfo.Remove(fld1);
                Appearance fldApp1 = _currLayout.GetAppearance(fld1.Key);
                _currLayout.Remove(fldApp1);
            }
            Entity targetEntity = _currInfo.GetEntity(targetEntityKey);
            var targetFieldCount = targetEntity.Fields.Count;
            for (var i = 0; i < targetFieldCount; i++)
            {
                Field fld = targetEntity.Fields[0];
                _currInfo.Remove(fld);
                Appearance fldApp3 = _currLayout.GetAppearance(fld.Key);
                _currLayout.Remove(fldApp3);
            }
            //构造源数据单据体字段和目标数据单据体字段
            //添加法人业务日期字段
            var corpDateTimeFieldApp = FieldCreateHelper.CreateCorpDateTimeField(this.Context, targetEntityKey, "target");
            if (corpDateTimeFieldApp != null)
            {
                _currInfo.Add(corpDateTimeFieldApp.Field);
                _currLayout.Add(corpDateTimeFieldApp);
            }
            //添加法人组织字段
            if (billTypeName != "直接调拨单")
            {
                var corpOrgFieldApp = FieldCreateHelper.CreateCorpOrgNumberField(this.Context, targetEntityKey, "target");
                if (corpOrgFieldApp != null)
                {
                    _currInfo.Add(corpOrgFieldApp.Field);
                    _currLayout.Add(corpOrgFieldApp);
                }
            }
            //添加法人仓库字段
            if (billTypeName != "应付单" && billTypeName != "费用报销单" && billTypeName != "其他应收单"
                && billTypeName != "其他应付单" && billTypeName != "付款单" && billTypeName != "收款单"
                && billTypeName != "付款退款单" && billTypeName != "收款退款单" && billTypeName != "现金存取单"
                && billTypeName != "银行转账单" && billTypeName != "应收单" && billTypeName != "直接调拨单")
            {
                var corpStockFieldApp = FieldCreateHelper.CreateCorpStockField(this.Context, targetEntityKey, "target");
                if (corpStockFieldApp != null)
                {
                    _currInfo.Add(corpStockFieldApp.Field);
                    _currLayout.Add(corpStockFieldApp);
                }
            }
            //添加法人物料字段
            if (billTypeName != "费用报销单" && billTypeName != "其他应收单" && billTypeName != "其他应付单"
                && billTypeName != "付款单" && billTypeName != "收款单" && billTypeName != "付款退款单"
                && billTypeName != "收款退款单" && billTypeName != "现金存取单" && billTypeName != "银行转账单")
            {
                var corpMaterialFieldApp = FieldCreateHelper.CreateCorpMaterialField(this.Context, targetEntityKey, "target");
                if (corpMaterialFieldApp != null)
                {
                    _currInfo.Add(corpMaterialFieldApp.Field);
                    _currLayout.Add(corpMaterialFieldApp);
                }
                var corpMaterialNumberFieldApp = FieldCreateHelper.CreateTextField(this.Context, targetEntityKey,
                    "target_MaterialName", "物料名称");
                if (corpMaterialNumberFieldApp != null)
                {
                    _currInfo.Add(corpMaterialNumberFieldApp.Field);
                    _currLayout.Add(corpMaterialNumberFieldApp);
                }
                var materialModelFieldApp = FieldCreateHelper.CreateTextField(this.Context, targetEntityKey,
                    "target_MaterialModel", "规格型号");
                if (materialModelFieldApp != null)
                {
                    _currInfo.Add(materialModelFieldApp.Field);
                    _currLayout.Add(materialModelFieldApp);
                }
            }
            //添加即时库存字段
            if (billTypeName == "销售出库单")
            {
                var corpInventoryFieldApp = FieldCreateHelper.CreateCorpInventoryQtyField(this.Context, targetEntityKey);
                if (corpInventoryFieldApp != null)
                {
                    _currInfo.Add(corpInventoryFieldApp.Field);
                    _currLayout.Add(corpInventoryFieldApp);
                }
                var syncedQtyFieldApp = FieldCreateHelper.CreateQtyField(this.Context, targetEntityKey, "target_SyncedQty", "已同步数量");
                if (syncedQtyFieldApp != null)
                {
                    _currInfo.Add(syncedQtyFieldApp.Field);
                    _currLayout.Add(syncedQtyFieldApp);
                }
            }
            if (billTypeName == "费用报销单")//创建法人部门字段
            {
                var corpDepartmentFieldApp = FieldCreateHelper.CreateTextField(this.Context, targetEntityKey,
                            "target_Department", "部门字段");
                if (corpDepartmentFieldApp != null)
                {
                    _currInfo.Add(corpDepartmentFieldApp.Field);
                    _currLayout.Add(corpDepartmentFieldApp);
                }
            }
            //添加配置表里的字段
            foreach (var field in fieldMapList)
            {
                var srcFieldKey = field["F_BOA_SRCFIELDKEY"].ToString();//源单同步字段标识
                var srcFieldLock = Convert.ToInt32(field["F_BOA_ISLOCK"]);//目标字段是否锁定
                var srcFieldShow = field["F_BOA_ISSHOW"].ToString();//目标字段是否显示
                var isUpdate = Convert.ToInt32(field["F_BOA_IsUpdate"]);//目标字段是否触发值更新
                var isLock = Convert.ToInt32(field["F_BOA_IsSrcLocked"]);//源字段是否锁定
                var srcField = srcFormMetaData.BusinessInfo.GetField(srcFieldKey).Clone() as Field;
                var srcFieldApp = FieldCreateHelper.CreateField(this.Context, srcEntityKey, srcField, "src", isLock, 1);
                if (srcFieldApp != null)
                {
                    _currInfo.Add(srcFieldApp.Field);
                    _currLayout.Add(srcFieldApp);
                }
                if (srcFieldShow == "1")
                {
                    var targetFieldApp = FieldCreateHelper.CreateField(this.Context, targetEntityKey, srcField, "target",
                        srcFieldLock, isUpdate);
                    if (targetFieldApp != null)
                    {
                        _currInfo.Add(targetFieldApp.Field);
                        _currLayout.Add(targetFieldApp);
                    }
                }
            }
            if (billTypeName == "直接调拨单")//入仓库，出仓库
            {
                var inStockFieldApp = FieldCreateHelper.CreateTextField(this.Context, targetEntityKey,
                    "target_InStockNo", "调入仓库编码");
                if (inStockFieldApp != null)
                {
                    _currInfo.Add(inStockFieldApp.Field);
                    _currLayout.Add(inStockFieldApp);
                }
                var outStockFieldApp = FieldCreateHelper.CreateTextField(this.Context, targetEntityKey,
                    "target_OutStockNo", "调出仓库编码");
                if (outStockFieldApp != null)
                {
                    _currInfo.Add(outStockFieldApp.Field);
                    _currLayout.Add(outStockFieldApp);
                }
            }
            //源数据单据体
            EntryGrid grid = (EntryGrid)this.View.GetControl(srcEntityKey);
            grid.SetAllowLayoutSetting(false); // 列按照索引显示
            EntityAppearance listAppearance = _currLayout.GetEntityAppearance(srcEntityKey);
            grid.CreateDyanmicList(listAppearance);
            //目标数据单据体
            EntryGrid grid1 = (EntryGrid)this.View.GetControl(targetEntityKey);
            grid1.SetAllowLayoutSetting(false); // 列按照索引显示
            EntityAppearance listAppearance1 = _currLayout.GetEntityAppearance(targetEntityKey);
            grid1.CreateDyanmicList(listAppearance1);
            _currInfo.GetDynamicObjectType(true);
            //this.Model.CreateNewData();
            //this.View.UpdateViewState();
        }

        /// <summary>
        /// 填充单据体数据/刷新
        /// </summary>
        private void FillEntryData()
        {
            //_currInfo.GetDynamicObjectType(true);
            //this.Model.CreateNewData();
            this.View.UpdateViewState();
            if (this.View.Model.GetEntryRowCount(srcEntityKey) > 0)
            {
                this.View.Model.DeleteEntryData(srcEntityKey);
                this.View.Model.DeleteEntryData(targetEntityKey);
                this.View.Model.DeleteEntryData(linkSrcEntityKey);
            }
            var srcEntity = _currInfo.GetEntity(srcEntityKey);
            var srcEntityDy = this.View.Model.GetEntityDataObject(srcEntity);
            var linkEntity = _currInfo.GetEntity(linkSrcEntityKey);
            var linkEntityDy = this.View.Model.GetEntityDataObject(linkEntity);
            var targetEntity = _currInfo.GetEntity(targetEntityKey);
            //var targetEntityDy = this.View.Model.GetEntityDataObject(targetEntity);
            var selectList = selectedData.Select(t => new SelectDto { BillId = t.PrimaryKeyValue, EntryId = t.EntryPrimaryKeyValue }).ToList();
            var billIdList = selectList.Select(t => t.BillId).Distinct().ToArray();
            var billEntryIdList = selectList.Select(t => t.EntryId).Distinct().ToList();
            selectQueryList = BillQueryHelper.GetBillListByIdList(this.Context, billTypeKey, billIdList);
            var index = 0;
            var headProp = fieldMapList.First()["F_BOA_HeadProp"].ToString();//源单单据头属性
            var entityProp = fieldMapList.First()["F_BOA_EntityProp"].ToString();//源单单据体属性
            var headFieldMapList = fieldMapList.Where(t => t["F_BOA_SRCENTITYPROP"].ToString() == headProp).ToList();//单据头
            var entityFieldMapList = fieldMapList.Where(t => t["F_BOA_SRCENTITYPROP"].ToString() == entityProp).ToList();//单据体
            orgConfig = orgConfig ?? SqlHelper.GetOrgConfig(this.Context);
            if (billTypeName == "费用报销单")
            {
                departmentConfig = departmentConfig ?? SqlHelper.GetDepartmentConfig(this.Context);
            }
            DynamicObjectCollection syncedQtyInfoList = null;//销售出库单已同步数量集合
            if (billTypeName == "销售出库单")//即时库存获取
            {
                apiConfig = apiConfig ?? SqlHelper.GetSyncApiConfigure(this.Context);
                if (apiConfig == null)
                {
                    this.View.ShowErrMessage("未配置api连接配置，请配置！");
                    return;
                }
                inventoryList = InventoryHelper.GetInventoryInfo(apiConfig, orgConfig);
                syncedQtyInfoList = SqlHelper.GetSyncedQty(this.Context, billIdList, billEntryIdList);
            }
            foreach (var item in selectQueryList)
            {
                var corpDate = BillDate(item["DATE"]);
                var corpOrg = string.Empty;
                if (item.Contains("F_BOA_CORPORGID"))
                {
                    var corpOrgInfo = item["F_BOA_CORPORGID"] as DynamicObject;
                    if (corpOrgInfo != null)
                    {
                        corpOrg = corpOrgInfo["Number"].ToString();
                    }
                }
                else
                {
                    corpOrg = GetTransferOrgNumber(item);
                }
                if (billTypeName == "销售出库单")
                {
                    if (item["F_BOA_StockCorpOrgId"] != null)
                    {
                        corpOrg = (item["F_BOA_StockCorpOrgId"] as DynamicObject)["Number"].ToString();
                    }
                }
                var corpDepartment = "";
                if (billTypeKey == "ER_ExpReimbursement")
                {
                    corpDepartment = GetTransferDepartmentNumber(item);
                }
                var billId = item["Id"].ToString();//单据内码
                var billNo = item["BillNo"].ToString();//单据编号
                if (item.Contains(entityProp))//单据体数据
                {
                    var entryEntry = item[entityProp] as DynamicObjectCollection;
                    foreach (var entryItem in entryEntry)
                    {
                        var entryId = entryItem["Id"].ToString();//分录内码
                        if (billEntryIdList.FirstOrDefault(t => t == entryId) != null)
                        {
                            var seq = Convert.ToInt32(entryItem["Seq"]);//行号
                            var srcEntityRowDy = new DynamicObject(srcEntity.DynamicObjectType);
                            srcEntity.SeqDynamicProperty.SetValue(srcEntityRowDy, index + 1);
                            var linkEntityRowDy = new DynamicObject(linkEntity.DynamicObjectType);
                            linkEntity.SeqDynamicProperty.SetValue(linkEntityRowDy, index + 1);
                            var targetEntityRowDy = new DynamicObject(targetEntity.DynamicObjectType);
                            targetEntity.SeqDynamicProperty.SetValue(targetEntityRowDy, 1);
                            foreach (var entryFieldProp in entityFieldMapList)
                            {
                                var srcFieldShow = entryFieldProp["F_BOA_ISSHOW"].ToString();//源单字段是否显示
                                var srcFieldProp = entryFieldProp["F_BOA_SRCFIELDPROP"].ToString();//源单同步字段属性
                                foreach (var headFieldProp in headFieldMapList)
                                {
                                    var srcFieldShow1 = headFieldProp["F_BOA_ISSHOW"].ToString();//源单字段是否显示
                                    var srcFieldProp1 = headFieldProp["F_BOA_SRCFIELDPROP"].ToString();//源单同步字段属性
                                    if (item.Contains(srcFieldProp1))//单据头数据
                                    {
                                        var fieldValue1 = item[srcFieldProp1];
                                        srcEntityRowDy[$"src_{srcFieldProp1}"] = fieldValue1;
                                        if (srcFieldShow1 == "1")
                                        {
                                            targetEntityRowDy[$"target_{srcFieldProp1}"] = fieldValue1;
                                        }
                                    }
                                }
                                if (entryItem.Contains(srcFieldProp))
                                {
                                    var fieldValue = entryItem[srcFieldProp];
                                    srcEntityRowDy[$"src_{srcFieldProp}"] = fieldValue;
                                    linkEntityRowDy["F_BOA_SrcBillNo"] = billNo;
                                    linkEntityRowDy["F_BOA_SrcBillId"] = billId;
                                    linkEntityRowDy["F_BOA_SrcEntryId"] = entryId;
                                    linkEntityRowDy["F_BOA_SrcSeq"] = seq;
                                    if (srcFieldShow == "1")
                                    {
                                        targetEntityRowDy[$"target_{srcFieldProp}"] = fieldValue;
                                    }
                                }
                            }
                            if (billTypeName != "直接调拨单")
                            {
                                targetEntityRowDy["target_CorpOrgNumber"] = corpOrg;//组织赋值
                            }
                            targetEntityRowDy["target_CorpDateTime"] = corpDate;//业务日期赋值
                            if (billTypeKey == "STK_InStock" || billTypeKey == "SAL_OUTSTOCK" || billTypeKey == "PUR_MRB" || billTypeKey == "SAL_RETURNSTOCK"
                                || billTypeKey == "AP_Payable" || billTypeKey == "AR_receivable" || billTypeName == "直接调拨单")
                            {
                                var corpStockNumber = GetTransferStockNumber(entryItem);
                                var corpMaterial = GetTransferMaterialNumber(entryItem);
                                if (billTypeKey != "AP_Payable" && billTypeKey != "AR_receivable" && billTypeName != "直接调拨单")
                                {
                                    targetEntityRowDy["target_CorpStockId"] = corpStockNumber;//法人仓库赋值
                                }
                                if (billTypeName == "直接调拨单")//调入，调出仓库赋值
                                {
                                    GetInAndOutStockNumber(entryItem, out string outStock, out string inStock);
                                    targetEntityRowDy["target_InStockNo"] = inStock;//仓库赋值
                                    targetEntityRowDy["target_OutStockNo"] = outStock;//仓库赋值
                                }
                                targetEntityRowDy["target_CorpMaterialId"] = corpMaterial.Number;//物料编码赋值
                                targetEntityRowDy["target_MaterialName"] = corpMaterial.Name;//物料名称赋值
                                targetEntityRowDy["target_MaterialModel"] = corpMaterial.Model;//规格型号赋值
                                if (billTypeKey == "SAL_OUTSTOCK")
                                {
                                    if (inventoryList.Success && inventoryList.Data.Count > 0)
                                    {
                                        var inventoryQty = inventoryList.Data
                                            .Where(t => t.FMaterialNumber == corpMaterial.Number && t.FStockNumber == corpStockNumber && t.FStockOrgNumber == corpOrg)
                                            .Sum(t => t.FBaseQty);
                                        targetEntityRowDy["target_Inventory"] = inventoryQty;//即时库存
                                    }
                                    if (syncedQtyInfoList != null && syncedQtyInfoList.Count > 0)
                                    {
                                        var syncedQtyInfo = syncedQtyInfoList.FirstOrDefault(t => t["F_BOA_SRCBILLID"].ToString() == billId &&
                                        t["F_BOA_SRCENTRYID"].ToString() == entryId);
                                        if (syncedQtyInfo != null)
                                        {
                                            var syncedQty = Convert.ToDecimal(syncedQtyInfo["Qty"]);
                                            targetEntityRowDy["target_SyncedQty"] = syncedQty;//已同步数量
                                            var realQty = Convert.ToDecimal(targetEntityRowDy["target_RealQty"]);//单据分录原本数量
                                            targetEntityRowDy["target_RealQty"] = realQty - syncedQty;//实发数量
                                            if (syncedQty > 0)
                                            {
                                                ComputeAmount(targetEntityRowDy);
                                            }
                                        }
                                    }
                                }
                            }
                            if (billTypeKey == "ER_ExpReimbursement")//费用报销单
                            {
                                targetEntityRowDy["target_Department"] = corpDepartment;//部门字段赋值
                            }
                            (linkEntityRowDy["F_BOA_TargetEntity"] as DynamicObjectCollection).Add(targetEntityRowDy);
                            srcEntityDy.Add(srcEntityRowDy);
                            linkEntityDy.Add(linkEntityRowDy);
                            index++;
                        }
                    }
                }
            }
            this.View.UpdateView(srcEntityKey);
            this.View.UpdateView(linkSrcEntityKey);
            this.View.UpdateView(targetEntityKey);
            ComputeAmountFieldValue1(ComputeAmountFieldValue());
        }

        /// <summary>
        /// 获取组织编码
        /// </summary>
        /// <param name="billInfo"></param>
        /// <returns></returns>
        private string GetTransferOrgNumber(DynamicObject billInfo)
        {
            var orgId = "0";
            var departmentId = "0";
            if (billTypeKey == "STK_InStock")//采购入库单
            {
                orgId = billInfo["StockOrgId_Id"].ToString();
                departmentId = billInfo["PurchaseDeptId_Id"].ToString();
            }
            else if (billTypeKey == "SAL_OUTSTOCK")//销售出库单
            {
                orgId = billInfo["SaleOrgId_Id"].ToString();
                departmentId = billInfo["SaleDeptID_Id"].ToString();
            }
            else if (billTypeKey == "PUR_MRB")//采购退料单
            {
                orgId = billInfo["StockOrgId_Id"].ToString();
                departmentId = billInfo["MRDeptId_Id"].ToString();
            }
            else if (billTypeKey == "SAL_RETURNSTOCK")//销售退货单
            {
                orgId = billInfo["SaleOrgId_Id"].ToString();
                departmentId = billInfo["Sledeptid_Id"].ToString();
            }
            else if (billTypeKey == "AP_Payable")//应付单
            {
                orgId = billInfo["SETTLEORGID_Id"].ToString();
                //departmentId = billInfo["Sledeptid_Id"].ToString();
            }
            else if (billTypeKey == "ER_ExpReimbursement")//费用报销单
            {
                orgId = billInfo["ExpenseOrgId_Id"].ToString();
                departmentId = billInfo["ExpenseDeptID_Id"].ToString();
            }
            else if (billTypeKey == "AR_OtherRecAble" || billTypeKey == "AP_OtherPayable"
                || billTypeKey == "AP_PAYBILL" || billTypeKey == "AR_RECEIVEBILL"
                || billTypeKey == "AP_REFUNDBILL" || billTypeKey == "AR_REFUNDBILL")
            //其他应收单，其他应付单，付款单，收款单，付款退款单，收款退款单
            {
                orgId = billInfo["SETTLEORGID_Id"].ToString();
                //departmentId = billInfo["SALEDEPTID_Id"].ToString();
            }
            else if (billTypeKey == "CN_CASHACCESSBILL" || billTypeKey == "CN_BANKTRANSBILL")//现金存取单，银行转账单
            {
                orgId = billInfo["FPAYORGID_Id"].ToString();
            }
            else if (billTypeKey == "AR_receivable")//应收单
            {
                orgId = billInfo["SETTLEORGID_Id"].ToString();
                departmentId = billInfo["SALEDEPTID_Id"].ToString();
            }
            var corpOrg = orgConfig.FirstOrDefault(t => t["F_BOA_ORGID"].ToString() == orgId && t["F_BOA_DEPARTMENTID"].ToString() == departmentId);
            if (corpOrg != null)
            {
                return corpOrg["F_BOA_CORPORGNumber"].ToString();
            }
            else
            {
                var corpOrg1 = orgConfig.FirstOrDefault(t => t["F_BOA_ORGID"].ToString() == orgId);
                return corpOrg1 == null ? "" : corpOrg1["F_BOA_CORPORGNumber"].ToString();
            }
        }

        /// <summary>
        /// 获取仓库编码
        /// </summary>
        /// <param name="entryitem">分录信息</param>
        /// <returns></returns>
        private string GetTransferStockNumber(DynamicObject entryitem)
        {
            stockConfig = stockConfig ?? SqlHelper.GetStockConfig(this.Context);
            var stockId = 0L;
            if (billTypeKey == "STK_InStock")//采购入库单
            {
                stockId = Convert.ToInt64(entryitem["StockId_Id"]);
            }
            else if (billTypeKey == "SAL_OUTSTOCK")//销售出库单
            {
                stockId = Convert.ToInt64(entryitem["StockID_Id"]);
            }
            else if (billTypeKey == "PUR_MRB")//采购退料单
            {
                stockId = Convert.ToInt64(entryitem["STOCKID_Id"]);
            }
            else if (billTypeKey == "SAL_RETURNSTOCK")//销售退货单
            {
                stockId = Convert.ToInt64(entryitem["StockId_Id"]);
            }
            var stockInfo = stockConfig.FirstOrDefault(t => Convert.ToInt64(t["F_BOA_STOCKID"]) == stockId);
            if (stockInfo != null)
            {
                return stockInfo["F_BOA_LEGALSTOCKNUMBER"].ToString();
            }
            return "";
        }

        /// <summary>
        /// 直接调拨单仓库
        /// </summary>
        /// <param name="entryitem">分录数据</param>
        /// <param name="outStock">调出仓库编码</param>
        /// <param name="inStock">调入仓库编码</param>
        private void GetInAndOutStockNumber(DynamicObject entryitem, out string outStock, out string inStock)
        {
            stockConfig = stockConfig ?? SqlHelper.GetStockConfig(this.Context);
            var inStockId = entryitem["DestStockId_Id"].ToString();//调入仓库
            var outStockId = entryitem["SrcStockId_Id"].ToString();//调出仓库
            var stockInfoIn = stockConfig.FirstOrDefault(t => t["F_BOA_STOCKID"].ToString() == inStockId);
            inStock = "";
            if (stockInfoIn != null)
            {
                inStock = stockInfoIn["F_BOA_LEGALSTOCKNUMBER"].ToString();
            }
            var stockInfoOut = stockConfig.FirstOrDefault(t => t["F_BOA_STOCKID"].ToString() == outStockId);
            outStock = "";
            if (stockInfoOut != null)
            {
                outStock = stockInfoOut["F_BOA_LEGALSTOCKNUMBER"].ToString();
            }
        }

        /// <summary>
        /// 获取物料编码
        /// </summary>
        /// <param name="entryitem"></param>
        /// <returns></returns>
        private MaterialDto GetTransferMaterialNumber(DynamicObject entryitem)
        {
            if (entryitem["MaterialId"] == null)
            {
                return new MaterialDto();
            }
            var materialInfo = entryitem["MaterialId"] as DynamicObject;
            var model = string.Empty;
            if (materialInfo.Contains("Specification"))
            {
                model = materialInfo["Specification"].ToString();
            }
            var materialNumber = materialInfo["Number"].ToString();
            var materialName = materialInfo["Name"].ToString();
            var materialDto = new MaterialDto { Name = materialName, Number = materialNumber, Model = model };
            if (isFastBusiness)//当前组织为事业部
            {
                var boxCode = materialInfo["F_BOA_Boxcode"].ToString().Trim();
                if (boxCode != "")
                {
                    materialDto.Number = boxCode;
                    return materialDto;
                }
            }
            else
            {
                var materialId = materialInfo["Id"].ToString();
                var cwName = SqlHelper.GetMaterialNameById(this.Context, materialId).Trim();
                var cwFnumber = materialInfo["F_BOA_CWFNumber"].ToString().Trim();
                if (cwFnumber != "")
                {
                    materialDto.Number = cwFnumber;
                }
                if (cwName != "")
                {
                    materialDto.Name = cwName;
                }
                return materialDto;
            }
            return materialDto;
        }

        /// <summary>
        /// 获取部门
        /// </summary>
        /// <param name="billInfo"></param>
        /// <returns></returns>
        private string GetTransferDepartmentNumber(DynamicObject billInfo)
        {
            var departmentId = billInfo["ExpenseDeptID_Id"].ToString();
            var departmentInfo = billInfo["ExpenseDeptID"];
            var departmentNumber = string.Empty;
            if (departmentInfo != null)
            {
                departmentNumber = (departmentInfo as DynamicObject)["Number"].ToString();
            }
            var corpOrgInfo = billInfo["F_BOA_CORPORGID"];
            if (corpOrgInfo != null)
            {
                var corpOrgId = (corpOrgInfo as DynamicObject)["Id"].ToString();
                var corpDepartmentNumber1 = departmentConfig
                .FirstOrDefault(t => t["deptNumber"].ToString() == departmentNumber && t["F_BOA_CORPORGID"].ToString() == corpOrgId);
                if (corpDepartmentNumber1 != null)
                {
                    return corpDepartmentNumber1["F_BOA_LEGALDEPTNUMBER"].ToString();
                }
            }
            var corpDepartmentNumber = departmentConfig.FirstOrDefault(t => t["deptNumber"].ToString() == departmentNumber);
            if (corpDepartmentNumber != null)
            {
                return corpDepartmentNumber["F_BOA_LEGALDEPTNUMBER"].ToString();
            }
            return "";
        }

        /// <summary>
        /// 单据日期修改
        /// </summary>
        /// <param name="date">单据日期</param>
        /// <returns></returns>
        private string BillDate(object date)
        {
            period = period == DateTime.MinValue ? Convert.ToDateTime(this.View.Model.GetValue("F_BOA_Period")) : period;//生成期间
            var billDate = Convert.ToDateTime(date);//单据日期
            string dateStr;
            var billDays = DateTime.DaysInMonth(billDate.Year, billDate.Month);//单据月份的天数
            var currentDays = DateTime.DaysInMonth(period.Year, period.Month);//生成期间月份的天数
            if (currentDays >= billDays)//生成期间月份天数大于等于单据月份天数，日期不会溢出
            {
                dateStr = $"{period.Year}-{period.Month}-{billDate.Day}";
            }
            else//生成期间月份天数小于单据月份天数，日期会溢出
            {
                if (billDate.Day > currentDays)
                {
                    dateStr = $"{period.Year}-{period.Month}-{currentDays}";
                }
                else
                {
                    dateStr = $"{period.Year}-{period.Month}-{billDate.Day}";
                }
            }
            return dateStr;
        }

        #region 同步数据

        /// <summary>
        /// 同步数据之前的判断
        /// </summary>
        private void SyncData()
        {
            var dataobj = this.View.Model.DataObject;
            var linkData = dataobj[linkSrcEntityKey] as DynamicObjectCollection;
            if (linkData.Count == 0)
            {
                this.View.ShowWarnningMessage("请先查询数据！");
                return;
            }
            if (billTypeKey == "SAL_OUTSTOCK")//销售出库单，负库存判断
            {
                var inventoryMsg = InventoryHelper.OutStockBillInventory(linkData, inventoryList);
                if (inventoryMsg != string.Empty)
                {
                    this.View.ShowErrMessage(inventoryMsg);
                    return;
                }
            }
            if (billTypeName == "付款退款单")
            {
                var entryIdList = linkData.Select(t => t["F_BOA_SrcEntryId"].ToString()).ToList();
                rEFUNDBILLSrcBillInfo = SqlHelper.GetREFUNDBILLSrcBillInfo(this.Context, entryIdList);
                var unSyncInfo = rEFUNDBILLSrcBillInfo
                                .Where(t => Convert.ToInt32(t["corpBillId"]) == 0)
                                .Select(t => t["FBillNo"].ToString())
                                .Distinct()
                                .ToList();
                if (unSyncInfo.Count > 0)
                {
                    var billNoStr = string.Join(",", unSyncInfo);
                    this.View.ShowErrMessage($"以下单据：{billNoStr}。\n需先同步付款单！");
                    return;
                }
            }
            apiConfig = apiConfig ?? SqlHelper.GetSyncApiConfigure(this.Context);
            if (apiConfig == null)
            {
                this.View.ShowErrMessage("未配置api连接配置，请配置！");
                return;
            }
            var loginStatus = WebApiHelper.Login(apiConfig);
            if (loginStatus.ResultType != 1)
            {
                this.View.ShowErrMessage("登录授权失败，请联系管理员！");
                return;
            }
            var billStatus = Convert.ToInt32(this.View.Model.GetValue("F_BOA_CbBillStatus"));//同步后单据状态：1 = 创建，2 = 提交，3 = 审核
            var syncParameterSplitHelper = new SyncParameterSplitHelper(isFastBusiness, billStatus, loginStatus.Client, this.Context);
            if (billTypeKey == "AP_OtherPayable" || billTypeKey == "AR_OtherRecAble")
            {
                OtherPayOrRecSrc(linkData, syncParameterSplitHelper);
            }
            else
            {
                SyncDataAndShowProcessBar(linkData, syncParameterSplitHelper);
            }
        }

        /// <summary>
        /// 同步数据并显示进度条
        /// </summary>
        private void SyncDataAndShowProcessBar(DynamicObjectCollection linkData, SyncParameterSplitHelper syncParameterSplitHelper)
        {
            this.View.ShowProcessForm(formResult => { }, true, "正在同步");
            MainWorker.QuequeTask(this.View.Context, () =>
             {
                 try
                 {
                     var linkDataGroup = linkData.GroupBy(t => Convert.ToInt64(t["F_BOA_SrcBillId"])).Select(t => t.Key).ToList();
                     var syncBillCount = linkDataGroup.Count;//需要同步的单据数量汇总
                     var msgType = -1;//错误类型，0 = 同步接口报错，1 = 其他报错，-1 = 同步成功
                     var msgStr = string.Empty;//其他报错信息
                     List<DynamicObject> linkDataList = null;//同步的数据
                     SyncApiResult syncResult = null;//同步结果
                     DynamicObject billInfo = null;//同步单据
                     var currentIndex = 1m;//已执行同步的单据数量
                     var errorCount = 0;//失败数量
                     var successCount = 0;//成功数量
                     var updateStrList = new List<string>();//更新语句集合
                     if (billTypeKey == "STK_InStock")//采购入库单
                     {
                         foreach (var item in linkDataGroup)//单据
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.STKInStockParameter(billInfo, linkDataList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_STK_INSTOCKENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "采购入库单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "SAL_OUTSTOCK")//销售出库单
                     {
                         var outStockUnCheckInfoList = GetOutStockUnCheckInfo(linkData);
                         var linkDataList1 = GetCustomerLinkData(linkData);
                         var linkDataGroup1 = linkDataList1
                                             .GroupBy(t => new { t.BillId, t.BaseId })
                                             .Select(t => new { t.Key.BillId, t.Key.BaseId }).ToList();
                         var cusIdList = linkDataGroup1.Select(t => t.BaseId).ToList();
                         var customerCorpNumberList = SqlHelper.GetCustomerNumberById(this.Context, cusIdList);
                         syncBillCount = linkDataGroup1.Count;
                         foreach (var item in linkDataGroup1)
                         {
                             var entryIdList = linkDataList1.Where(t => t.BillId == item.BillId && t.BaseId == item.BaseId)
                                               .Select(t => t.EntryId).ToList();
                             linkDataList = linkData.Where(t => t["F_BOA_SrcBillId"].ToString() == item.BillId &&
                                     entryIdList.Contains(t["F_BOA_SrcEntryId"].ToString())).ToList();
                             var seqList = linkDataList1.Where(t => t.BillId == item.BillId && t.BaseId == item.BaseId).ToList();
                             var outStockUnCheckInfoList1 = outStockUnCheckInfoList.Where(t => t.Id == item.BillId &&
                                      entryIdList.Contains(t.EntryId)).ToList();
                             try
                             {
                                 billInfo = selectQueryList.FirstOrDefault(t => t["Id"].ToString() == item.BillId);
                                 syncResult = syncParameterSplitHelper.SALOUTSTOCKParameter(billInfo, linkDataList, customerCorpNumberList,
                                     seqList, outStockUnCheckInfoList1);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStrOutStock(this.Context, linkDataList, syncResult, billInfo);
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "销售出库单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "PUR_MRB")//采购退料单
                     {
                         var linkDataList1 = GetCustomerLinkData(linkData, "target_SUPPLIERID");
                         var linkDataGroup1 = linkDataList1
                                             .GroupBy(t => new { t.BillId, t.BaseId })
                                             .Select(t => new { t.Key.BillId, t.Key.BaseId }).ToList();
                         syncBillCount = linkDataGroup1.Count;
                         foreach (var item in linkDataGroup1)
                         {
                             var entryIdList = linkDataList1.Where(t => t.BillId == item.BillId && t.BaseId == item.BaseId)
                                               .Select(t => t.EntryId).ToList();
                             linkDataList = linkData.Where(t => t["F_BOA_SrcBillId"].ToString() == item.BillId &&
                                     entryIdList.Contains(t["F_BOA_SrcEntryId"].ToString())).ToList();
                             var seqList = linkDataList1.Where(t => t.BillId == item.BillId && t.BaseId == item.BaseId).ToList();
                             try
                             {
                                 billInfo = selectQueryList.FirstOrDefault(t => t["Id"].ToString() == item.BillId);
                                 syncResult = syncParameterSplitHelper.PURMRBParameter(billInfo, linkDataList, seqList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_PUR_MRBENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "采购退料单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "SAL_RETURNSTOCK")//销售退货单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.SALRETURNSTOCKParameter(billInfo, linkDataList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_SAL_RETURNSTOCKENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "销售退货单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "AP_Payable")//应付单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.APPayableParameter(billInfo, linkDataList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_AP_PAYABLEENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "应付单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "ER_ExpReimbursement")//费用报销单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.ERExpReimbursementParameter(billInfo, linkDataList, departmentConfig);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "t_ER_ExpenseReimbEntry");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "费用报销单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "AR_OtherRecAble")//其他应收单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.AROtherRecAbleParameter(billInfo, linkDataList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_AR_OtherRecAbleENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "其他应收单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "AP_OtherPayable")//其他应付单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.APOtherPayableParameter(billInfo, linkDataList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_AP_OTHERPAYABLEENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "其他应付单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "AP_PAYBILL")//付款单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.APPAYBILLParameter(billInfo, linkDataList, orgConfig, period);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_AP_PAYBILLENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "付款单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "AR_RECEIVEBILL")//收款单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.ARRECEIVEBILLParameter(billInfo, linkDataList, orgConfig);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_AR_RECEIVEBILLENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "收款单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "AP_REFUNDBILL")//付款退款单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.APREFUNDBILLParameter(billInfo, linkDataList, orgConfig, rEFUNDBILLSrcBillInfo, period);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_AP_REFUNDBILLENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "付款退款单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "AR_REFUNDBILL")//收款退款单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.ARREFUNDBILLParameter(billInfo, linkDataList, orgConfig);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_AR_REFUNDBILLENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "收款退款单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "CN_CASHACCESSBILL")//现金存取单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.CNCASHACCESSBILLParameter(billInfo, linkDataList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_CN_CASHACCESSBILLENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "现金存取单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "CN_BANKTRANSBILL")//银行转账单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.CNBANKTRANSBILLParameter(billInfo, linkDataList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_CN_BANKTRANSBILLENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "银行转账单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "AR_receivable")//应收单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.ARreceivableParameter(billInfo, linkDataList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "t_AR_receivableEntry");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "应收单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     else if (billTypeKey == "STK_TransferDirect")//直接调拨单
                     {
                         foreach (var item in linkDataGroup)
                         {
                             try
                             {
                                 linkDataList = linkData.Where(t => Convert.ToInt64(t["F_BOA_SrcBillId"]) == item).ToList();
                                 billInfo = selectQueryList.FirstOrDefault(t => Convert.ToInt64(t["Id"]) == item);
                                 syncResult = syncParameterSplitHelper.STKTransferDirectParameter(billInfo, linkDataList);
                                 if (syncResult.ResponseStatus.IsSuccess)
                                 {
                                     updateStrList = SqlHelper.CreateUpdateSqlStr(this.Context, linkDataList, "T_STK_STKTRANSFERINENTRY");
                                     successCount++;
                                     msgType = -1;
                                 }
                                 else
                                 {
                                     msgType = 0;
                                     errorCount++;
                                 }
                             }
                             catch (Exception ex)
                             {
                                 msgStr = ex.Message;
                                 msgType = 1;
                                 errorCount++;
                             }
                             finally
                             {
                                 syncParameterSplitHelper.SyncLogRecord(syncResult, billInfo, msgType, "直接调拨单", msgStr, linkDataList);
                                 SqlHelper.UpdateBillSyncStatus(this.Context, updateStrList);
                                 var rate = Convert.ToInt32(Math.Floor(currentIndex / syncBillCount.ToDecimal() * 100m));
                                 this.View.Session["ProcessRateValue"] = rate;
                                 this.View.Session["ProcessTips"] = $"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}";
                                 currentIndex++;
                             }
                         }
                     }
                     this.View.ShowMessage($"总数：{syncBillCount}，成功数：{successCount}，失败数：{errorCount}");
                 }
                 catch (Exception ex)
                 {
                     this.View.ShowErrMessage(ex.Message);
                 }
                 finally
                 {
                     this.View.Model.DeleteEntryData(srcEntityKey);
                     this.View.Model.DeleteEntryData(targetEntityKey);
                     this.View.Model.DeleteEntryData(linkSrcEntityKey);
                     this.View.Model.SetValue("F_BOA_SrcAmount", 0);
                     this.View.Model.SetValue("F_BOA_TargetSrcAmount", 0);
                     this.View.Model.SetValue("F_BOA_DisAmount", 0);
                     this.View.Session["ProcessRateValue"] = 100;
                     this.View.SendDynamicFormAction(this.View);
                 }
             }, null);
        }

        #endregion

        /// <summary>
        /// 打开excel引入窗口
        /// </summary>
        private void OpenExcelImportForm()
        {
            var count = this.View.Model.GetEntryRowCount(srcEntityKey);
            if (count == 0)
            {
                this.View.ShowWarnningMessage("请先查询数据！");
                return;
            }
            var form = new BillShowParameter();
            form.FormId = "BOA_ImportExcel";
            form.OpenStyle.ShowType = ShowType.Modal;
            form.Status = OperationStatus.VIEW;
            form.CustomComplexParams.Add("billTypeName", billTypeName);
            form.CustomComplexParams.Add("modelData", this.View.Model.DataObject);
            this.View.ShowForm(form, delegate (FormResult result)
            {
                if (result.ReturnData == null)
                {
                    return;
                }
                if (Convert.ToBoolean(result.ReturnData))
                {
                    this.View.ShowMessage("批改完成！");
                    FillEntryData();
                }
            });
        }

        /// <summary>
        /// 记录更新永不传输标记sql语句
        /// </summary>
        private void UpdateNoSyncFlag(int row)
        {
            var entryId = this.View.Model.GetValue("F_BOA_SrcEntryId", row).ToString();
            var updateValue = this.View.Model.GetValue("src_F_BOA_NoSync", row).ToString();
            var newValue = updateValue == "True" ? "1" : "0";
            string entryTableName;
            switch (billTypeName)
            {
                case "采购入库单":
                    entryTableName = "T_STK_INSTOCKENTRY";
                    break;
                case "销售出库单":
                    entryTableName = "T_SAL_OUTSTOCKENTRY";
                    break;
                case "采购退料单":
                    entryTableName = "T_PUR_MRBENTRY";
                    break;
                case "销售退货单":
                    entryTableName = "T_SAL_RETURNSTOCKENTRY";
                    break;
                case "应付单":
                    entryTableName = "T_AP_PAYABLEENTRY";
                    break;
                case "费用报销单":
                    entryTableName = "t_ER_ExpenseReimbEntry";
                    break;
                case "其他应收单":
                    entryTableName = "T_AR_OtherRecAbleENTRY";
                    break;
                case "其他应付单":
                    entryTableName = "T_AP_OTHERPAYABLEENTRY";
                    break;
                case "付款单":
                    entryTableName = "T_AP_PAYBILLENTRY";
                    break;
                case "收款单":
                    entryTableName = "T_AR_RECEIVEBILLENTRY";
                    break;
                case "付款退款单":
                    entryTableName = "T_AP_REFUNDBILLENTRY";
                    break;
                case "收款退款单":
                    entryTableName = "T_AR_REFUNDBILLENTRY";
                    break;
                case "现金存取单":
                    entryTableName = "T_CN_CASHACCESSBILLENTRY";
                    break;
                case "银行转账单":
                    entryTableName = "T_CN_BANKTRANSBILLENTRY";
                    break;
                case "应收单":
                    entryTableName = "t_AR_receivableEntry";
                    break;
                default:
                    entryTableName = "";
                    break;
            }
            var sqlStr = $@"update {entryTableName} set F_BOA_NoSync = {newValue} where FEntryId = {entryId}";
            updateSyncStatus.Add(sqlStr);
        }

        /// <summary>
        /// 销售出库单，修改客户，按客户分开同步
        /// 采购退料单，修改供应商，按供应商分开同步
        /// </summary>
        /// <param name="linkData">同步数据</param>
        /// <param name="key">客户或供应商标识</param>
        /// <returns></returns>
        private List<SyncRecordDto> GetCustomerLinkData(DynamicObjectCollection linkData, string key = "target_CustomerID")
        {
            var syncRecordDtoList = new List<SyncRecordDto>();
            foreach (var item in linkData)
            {
                var billId = item["F_BOA_SrcBillId"].ToString();
                var entryId = item["F_BOA_SrcEntryId"].ToString();
                var targetData = item["F_BOA_TargetEntity"] as DynamicObjectCollection;
                foreach (var targetItem in targetData)
                {
                    var seq = targetItem["Seq"].ToString();
                    var customerId = (targetItem[key] as DynamicObject)["Id"].ToString();
                    var syncRecordDto = new SyncRecordDto
                    {
                        BillId = billId,
                        EntryId = entryId,
                        BaseId = customerId,
                        Seq = seq
                    };
                    syncRecordDtoList.Add(syncRecordDto);
                }
            }
            return syncRecordDtoList;
        }

        /// <summary>
        /// 第一次加载数据时，销售出库单扣除已同步数量，计算相关金额字段
        /// </summary>
        /// <param name="targetEntityRowDy"></param>
        private void ComputeAmount(DynamicObject targetEntityRowDy)
        {
            var realQty = Convert.ToDecimal(targetEntityRowDy["target_RealQty"]);//数量
            var taxRate = Convert.ToDecimal(targetEntityRowDy["target_TaxRate"]) * 0.01m;//税率
            var taxPrice = Convert.ToDecimal(targetEntityRowDy["target_TaxPrice"]);//含税单价
            var stockUnitId = (targetEntityRowDy["target_UnitID"] as DynamicObject)["Id"].ToString();//库存单位
            var priceUnitId = (targetEntityRowDy["target_PriceUnitId"] as DynamicObject)["Id"].ToString();//计价单位
            if (stockUnitId == priceUnitId)//单位一样不需要单位换算
            {
                var allAmount = realQty * taxPrice;
                var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                targetEntityRowDy["target_AllAmount"] = allAmount;//价税合计
                targetEntityRowDy["target_TaxAmount"] = taxAmount;//税额
                targetEntityRowDy["target_PriceUnitQty"] = realQty;//计价数量
                targetEntityRowDy["target_Amount"] = allAmount - taxAmount;//金额
            }
            else//单位不一样需要单位换算
            {
                var materialId = (targetEntityRowDy["target_MaterialID"] as DynamicObject)["Id"].ToString();
                var unitInfoList = SqlHelper.GetUnitInfo(this.Context, materialId);
                var unitItem1 = unitInfoList.FirstOrDefault(t => t["FCURRENTUNITID"].ToString() == stockUnitId
                && t["FDESTUNITID"].ToString() == priceUnitId);
                if (unitItem1 != null)
                {
                    var num1 = Convert.ToDecimal(unitItem1["FCONVERTDENOMINATOR"]);//换算比例1
                    var num2 = Convert.ToDecimal(unitItem1["FCONVERTNUMERATOR"]);//换算比例2
                    var realQty1 = realQty * (num2 / num1);
                    var allAmount = realQty1 * taxPrice;
                    var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                    targetEntityRowDy["target_AllAmount"] = allAmount;//价税合计
                    targetEntityRowDy["target_TaxAmount"] = taxAmount;//税额
                    targetEntityRowDy["target_PriceUnitQty"] = realQty1;//计价数量
                    targetEntityRowDy["target_Amount"] = allAmount - taxAmount;//金额
                }
                else
                {
                    var unitItem2 = unitInfoList.FirstOrDefault(t => t["FCURRENTUNITID"].ToString() == priceUnitId
                                       && t["FDESTUNITID"].ToString() == stockUnitId);
                    var num1 = Convert.ToDecimal(unitItem2["FCONVERTDENOMINATOR"]);//换算比例1
                    var num2 = Convert.ToDecimal(unitItem2["FCONVERTNUMERATOR"]);//换算比例2
                    var realQty1 = realQty * (num1 / num2);
                    var allAmount = realQty1 * taxPrice;
                    var taxAmount = ConvertHelper.GetConvertNumValue(allAmount / (1m + taxRate) * taxRate);
                    targetEntityRowDy["target_AllAmount"] = allAmount;//价税合计
                    targetEntityRowDy["target_TaxAmount"] = taxAmount;//税额
                    targetEntityRowDy["target_PriceUnitQty"] = realQty1;//计价数量
                    targetEntityRowDy["target_Amount"] = allAmount - taxAmount;//金额
                }
            }
        }

        /// <summary>
        /// 其他应收单、其他应付单是否有源单判断
        /// </summary>
        private void OtherPayOrRecSrc(DynamicObjectCollection linkData, SyncParameterSplitHelper syncParameterSplitHelper)
        {
            var entryIdList = linkData.Select(t => t["F_BOA_SrcEntryId"].ToString()).ToList();
            var entryLinkTableName = "";
            if (billTypeKey == "AP_OtherPayable")//其他应付单
            {
                entryLinkTableName = "T_AP_OTHERPAYABLEENTRY_LK";
            }
            else//其他应收单
            {
                entryLinkTableName = "T_AR_OtherRecAbleENTRY_LK";
            }
            var srcData = SqlHelper.OtherPayOrRecSrc(this.Context, entryIdList, entryLinkTableName);
            if (srcData.Count > 0)
            {
                var msg = string.Empty;
                foreach (var item in srcData)
                {
                    var entryId = item["FEntryId"].ToString();
                    var linkItem = linkData.First(t => t["F_BOA_SrcEntryId"].ToString() == entryId);
                    msg += $"单据编号：{linkItem["F_BOA_SrcBillNo"]}，行号：{linkItem["F_BOA_SrcSeq"]}，存在源单\n";
                }
                this.View.ShowMessage(msg + "\n请确认，是否要同步数据！",
                            MessageBoxOptions.YesNo,
                            new Action<MessageBoxResult>((result1) =>
                            {
                                if (result1 == MessageBoxResult.Yes)
                                {
                                    SyncDataAndShowProcessBar(linkData, syncParameterSplitHelper);
                                }
                            }));
            }
            else
            {
                SyncDataAndShowProcessBar(linkData, syncParameterSplitHelper);
            }
        }

        /// <summary>
        /// 根据销售出库单内码获取应收收款核销记录，并计算金额
        /// </summary>
        private List<OutStockCheckDto> GetOutStockUnCheckInfo(DynamicObjectCollection linkData)
        {
            var orgInfo = this.View.Model.GetValue("F_BOA_OrgId");//当前组织
            var orgName = (orgInfo as DynamicObject)["Name"].ToString();//组织名称
            var outStockCheckDtoList = new List<OutStockCheckDto>();
            if (orgName != "事业部" && orgName != "事业部1")
            {
                return outStockCheckDtoList;
            }
            var idList = linkData.Select(t => t["F_BOA_SrcBillId"].ToString()).Distinct().ToList();//单据内码
            var currentOutStockDtoList = new List<CurrentOutStockDto>();
            foreach (var item in linkData)
            {
                var targetEntry = item["F_BOA_TargetEntity"] as DynamicObjectCollection;
                var entryId = item["F_BOA_SrcEntryId"].ToString();
                var id = item["F_BOA_SrcBillId"].ToString();
                foreach (var targetItem in targetEntry)
                {
                    var seq = targetItem["Seq"].ToString();
                    var priceQty = Convert.ToDecimal(targetItem["target_PriceUnitQty"]);
                    var allAmount = Convert.ToDecimal(targetItem["target_AllAmount"]);
                    var taxRate = Convert.ToDecimal(targetItem["target_TaxRate"]);
                    currentOutStockDtoList.Add(new CurrentOutStockDto
                    {
                        Id = id,
                        EntryId = entryId,
                        PriceQty = priceQty,
                        AllAmount = allAmount,
                        TaxRate = taxRate * 0.01m,
                        Seq = seq
                    });
                }
            }
            if (orgName == "事业部")
            {
                var totalAmount = currentOutStockDtoList.Sum(t => t.AllAmount);
                var discount = Convert.ToDecimal(this.View.Model.GetValue("F_BOA_DisAmount"));//票折金额
                if (discount == 0)
                {
                    return outStockCheckDtoList;
                }
                foreach (var item in idList)//按单据进行计算
                {
                    //var linkAllAmountSum = currentOutStockDtoList.Where(t => t.Id == item).Sum(t => t.AllAmount);//实际价税合计汇总
                    //var payCheckItem = payCheckInfo.FirstOrDefault(t => t["FSBillId"].ToString() == item);
                    //if (payCheckItem == null)
                    //{
                    //    continue;
                    //}
                    //var amount = Convert.ToDecimal(payCheckItem["amount"]);//总收款金额
                    //var checkAmount = Convert.ToDecimal(payCheckItem["currentAmount"]);//总核销金额
                    //var diffAmount = amount + checkAmount;//差额
                    var entryIdList = linkData.Where(t => t["F_BOA_SrcBillId"].ToString() == item)
                                     .Select(t => t["F_BOA_SrcEntryId"].ToString()).ToList();//分录内码集合
                    foreach (var entryItem in entryIdList)
                    {
                        var linkItemList = currentOutStockDtoList.Where(t => t.Id == item && t.EntryId == entryItem).ToList();
                        foreach (var linkItem in linkItemList)
                        {
                            var newDiscount = linkItem.AllAmount / totalAmount * discount;//折扣额
                            //var realAllAmount = diffAmount * (linkItem.AllAmount / linkAllAmountSum);//计算后的价税合计
                            var realAllAmount = linkItem.AllAmount - newDiscount;
                            var realTaxPrice = realAllAmount / linkItem.PriceQty;//计算后的含税单价
                            var realTaxAmount = realAllAmount / (1m + linkItem.TaxRate) * linkItem.TaxRate;//计算后的税额
                            var realAmount = realAllAmount - realTaxAmount;//计算后的金额
                            outStockCheckDtoList.Add(new OutStockCheckDto
                            {
                                OrgName = orgName,
                                Id = item,
                                EntryId = entryItem,
                                AllAmount = realAllAmount,
                                Amount = realAmount,
                                TaxAmount = realTaxAmount,
                                TaxPrice = realTaxPrice,
                                Seq = linkItem.Seq,
                                IsFree = realAllAmount == 0
                            });
                        }
                    }
                }
            }
            else if (orgName == "事业部1")
            {
                var payCheckInfo = SqlHelper.GetCheckInfo(this.Context, idList, orgName);
                if (payCheckInfo.Count == 0)
                {
                    return outStockCheckDtoList;
                }
                foreach (var item in idList)//按单据进行计算
                {
                    var payCheckItem = payCheckInfo.FirstOrDefault(t => t["FSBillId"].ToString() == item);
                    if (payCheckItem == null)
                    {
                        continue;
                    }
                    var amount = Convert.ToDecimal(payCheckItem["amount"]);//总收款金额
                    var checkAmount = Convert.ToDecimal(payCheckItem["currentAmount"]);//总核销金额
                    var diffAmount = amount + checkAmount;//差额
                    var entryIdList = linkData.Where(t => t["F_BOA_SrcBillId"].ToString() == item)
                                     .Select(t => t["F_BOA_SrcEntryId"].ToString()).ToList();//分录内码集合
                    var billInfo = selectQueryList.First(t => t["Id"].ToString() == item);
                    var entryInfo = billInfo["SAL_OUTSTOCKENTRY"] as DynamicObjectCollection;
                    foreach (var entryItem in entryIdList)
                    {
                        var entryInfoItem = entryInfo.First(t => t["Id"].ToString() == entryItem);
                        if (!entryInfoItem["F_BOA_Writedown"].ToBool())
                        {
                            continue;
                        }
                        var linkItemList = currentOutStockDtoList.Where(t => t.Id == item && t.EntryId == entryItem).ToList();
                        foreach (var linkItem in linkItemList)
                        {
                            if (diffAmount >= linkItem.AllAmount)
                            {
                                diffAmount -= linkItem.AllAmount;
                                outStockCheckDtoList.Add(new OutStockCheckDto
                                {
                                    OrgName = orgName,
                                    Id = item,
                                    EntryId = entryItem,
                                    Seq = linkItem.Seq,
                                    IsFree = true
                                });
                            }
                            else
                            {
                                var realAllAmount = linkItem.AllAmount - diffAmount;//计算后的价税合计
                                var realTaxPrice = realAllAmount / linkItem.PriceQty;//计算后的含税单价
                                var realTaxAmount = realAllAmount / (1m + linkItem.TaxRate) * linkItem.TaxRate;//计算后的税额
                                var realAmount = realAllAmount - realTaxAmount;//计算后的金额
                                outStockCheckDtoList.Add(new OutStockCheckDto
                                {
                                    OrgName = orgName,
                                    Id = item,
                                    EntryId = entryItem,
                                    AllAmount = realAllAmount,
                                    Amount = realAmount,
                                    TaxAmount = realTaxAmount,
                                    TaxPrice = realTaxPrice,
                                    Seq = linkItem.Seq,
                                    IsFree = false
                                });
                            }
                        }
                    }
                }
            }
            return outStockCheckDtoList;
        }

        /// <summary>
        /// 设置金额汇总字段可见性
        /// </summary>
        private void SetFieldsShow()
        {
            if (billTypeName == "采购入库单" || billTypeName == "销售出库单"
                || billTypeName == "采购退料单" || billTypeName == "销售退货单" || billTypeName == "收款单")
            {
                this.View.Model.SetValue("F_BOA_SrcAmount", 0);
                this.View.Model.SetValue("F_BOA_TargetSrcAmount", 0);
                this.View.GetControl("F_BOA_SrcAmount").Visible = true;
                this.View.GetControl("F_BOA_TargetSrcAmount").Visible = true;
            }
            else
            {
                this.View.Model.SetValue("F_BOA_SrcAmount", 0);
                this.View.Model.SetValue("F_BOA_TargetSrcAmount", 0);
                this.View.GetControl("F_BOA_SrcAmount").Visible = false;
                this.View.GetControl("F_BOA_TargetSrcAmount").Visible = false;
            }
        }

        /// <summary>
        /// 设置票折金额可见性
        /// </summary>
        private void SetDisCountFieldsShow()
        {
            if (billTypeName == "销售出库单" && isFastBusiness)
            {
                this.View.Model.SetValue("F_BOA_DisAmount", 0);
                this.View.GetControl("F_BOA_DisAmount").Visible = true;
            }
            else
            {
                this.View.Model.SetValue("F_BOA_DisAmount", 0);
                this.View.GetControl("F_BOA_DisAmount").Visible = false;
            }
        }

        /// <summary>
        /// 设置核算金额汇总
        /// </summary>
        private decimal ComputeAmountFieldValue()
        {
            var srcAmountSum = 0m;
            if (billTypeName == "采购入库单" || billTypeName == "销售出库单"
                || billTypeName == "采购退料单" || billTypeName == "销售退货单")
            {
                var dataobj = this.View.Model.DataObject;
                var srcData = dataobj[srcEntityKey] as DynamicObjectCollection;
                srcAmountSum = srcData.Sum(t => Convert.ToDecimal(t["src_AllAmount"]));
                this.View.Model.SetValue("F_BOA_SrcAmount", srcAmountSum);
            }
            if (billTypeName == "收款单")
            {
                var dataobj = this.View.Model.DataObject;
                var srcData = dataobj[srcEntityKey] as DynamicObjectCollection;
                srcAmountSum = srcData.Sum(t => Convert.ToDecimal(t["src_RECTOTALAMOUNTFOR"]));
                this.View.Model.SetValue("F_BOA_SrcAmount", srcAmountSum);
            }
            return srcAmountSum;
        }

        /// <summary>
        /// 设置核算金额汇总
        /// </summary>
        private void ComputeAmountFieldValue1(decimal amountSum = 0m)
        {
            if (billTypeName == "采购入库单" || billTypeName == "销售出库单"
                || billTypeName == "采购退料单" || billTypeName == "销售退货单")
            {
                if (amountSum == 0)
                {
                    var amount1 = 0m;
                    var dataobj = this.View.Model.DataObject;
                    var linkData = dataobj[linkSrcEntityKey] as DynamicObjectCollection;
                    foreach (var item in linkData)
                    {
                        var entry = item["F_BOA_TargetEntity"] as DynamicObjectCollection;
                        amount1 += entry.Sum(t => Convert.ToDecimal(t["target_AllAmount"]));
                    }
                    this.View.Model.SetValue("F_BOA_TargetSrcAmount", amount1);
                    return;
                }
                this.View.Model.SetValue("F_BOA_TargetSrcAmount", amountSum);
            }
            if (billTypeName == "收款单")
            {
                if (amountSum == 0)
                {
                    var amount1 = 0m;
                    var dataobj = this.View.Model.DataObject;
                    var linkData = dataobj[linkSrcEntityKey] as DynamicObjectCollection;
                    foreach (var item in linkData)
                    {
                        var entry = item["F_BOA_TargetEntity"] as DynamicObjectCollection;
                        amount1 += entry.Sum(t => Convert.ToDecimal(t["target_RECTOTALAMOUNTFOR"]));
                    }
                    this.View.Model.SetValue("F_BOA_TargetSrcAmount", amount1);
                    return;
                }
                this.View.Model.SetValue("F_BOA_TargetSrcAmount", amountSum);
            }
        }
    }
}
