using BOA.XJS.Weigh.BusinessPlugIn.Helper;
using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.Const;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.DynamicForm.PlugIn.ControlModel;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.EntityElement;
using Kingdee.BOS.Core.NotePrint;
using Kingdee.BOS.DataEntity;
using Kingdee.BOS.JSON;
using Kingdee.BOS.KDThread;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using Kingdee.K3.Core.MFG.PLN.Reserved;
using Kingdee.K3.Core.SCM.Mobile;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BOA.XJS.Weigh.BusinessPlugIn.Plugins
{
    [HotUpdate]
    [Description("称重平台")]
    public class WeighPlatformPlugin : AbstractDynamicFormPlugIn
    {
        private DynamicObjectCollection portInfo;//端口信息
        private List<PurOrderInfo> purOrderInfos = new List<PurOrderInfo>();//订单信息
        private FormMetadata formMetadata;
        private long printId;//打印标签内码
        private Entity entity;//单据体实体
        private string sendGoodsBillNo = "";//送货单号
        private string uniqueNo = "";//入库卷号(每包唯一号)
        private string inStockBillNo = "";//采购入库单号
        private string realWeightQty = "0";//当前称重重量
        private string uniqueNoValue = "";//入库卷号(每包唯一号)重新过磅使用 
        private bool isResetWeight = false;//是否重新过磅操作
        private int resetCurrentRow = 0;//重新过磅对应的行索引
        private int lastRow = 0;//上一次点击的行索引
        private int _currentRow = 0;//当前 过磅/重新过磅 的行索引
        private decimal _currentPackage = 0;//包装重量
        private bool isCopyEntry = false;//是否复制行 或 是否子单据新增行
        private bool isUpdateField = true;//是否触发值更新

        /// <summary>
        /// 片数
        /// </summary>
        private string _pieces;

        /// <summary>
        /// 仓库编码
        /// </summary>
        private string _stockNumber;

        /// <summary>
        /// 整车标示值集合
        /// </summary>
        private List<string> _zcbs = new List<string>();

        /// <summary>
        /// 下拉列表控件对象
        /// </summary>
        private ComboFieldEditor _combo;

        /// <summary>
        /// 送货重量
        /// </summary>
        private decimal _sendQty;

        /// <summary>
        /// 过磅重量
        /// </summary>
        private decimal _weightQty;

        /// <summary>
        /// 磅差率
        /// </summary>
        private decimal _diffRata;

        /// <summary>
        /// 当前批次采购订单单据分录内码的最大值
        /// </summary>
        private string _maxEntryId;

        /// <summary>
        /// 采购订单 下推 采购入库单 具体行数量记录
        /// </summary>
        private List<InStockDto> _inStockDtos = new List<InStockDto>();

        public override void PreOpenForm(PreOpenFormEventArgs e)
        {
            if (e.Context.ClientType != ClientType.WPF)
            {
                e.CancelMessage = "不支持桌面应用的功能，请在客户端上使用该功能。";
                e.Cancel = true;
            }
            base.PreOpenForm(e);
        }

        public override void AfterButtonClick(AfterButtonClickEventArgs e)
        {
            base.AfterButtonClick(e);
            if (e.Key.EqualsIgnoreCase("F_BOA_Weigh"))//过磅
            {
                isResetWeight = false;

                ExcuteCustomerCtlNew();

                //var weinthValue = this.View.Model.GetValue("F_BOA_RealQty").ToString();
                //WeightOperate(weinthValue);
            }
            else if (e.Key.EqualsIgnoreCase("F_BOA_AllWeigh"))//整车过磅(该按钮用来测试自定义控件是否更新成功)
            {
                //Weight();

                //var args = new object[1];
                //args[0] = null;
                //this.View.GetControl("F_BOA_CustomCtl").InvokeControlMethod("DoCustomMethod", "WriteString", args);
            }
            else if (e.Key.EqualsIgnoreCase("F_BOA_Print"))//再次打印标签
            {
                var printTemp = this.View.Model.GetValue("F_BOA_Template");
                if (printTemp == null)
                {
                    this.View.ShowErrMessage("请选择打印模板！");
                    return;
                }
                var currentRow = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");
                var isInStock = Convert.ToBoolean(this.View.Model.GetValue("F_BOA_IsInStock", currentRow));
                if (!isInStock)
                {
                    this.View.ShowErrMessage("只有已入库的行可以进行再次打印的操作！");
                    return;
                }
                var uniqueNoStr = this.View.Model.GetValue("F_BOA_UniqueNo", currentRow);
                printId = SqlHelper.GetPrintBillByUniqueNo(this.Context, uniqueNoStr.ToString());
                DoPrint(printId.ToString(), printTemp);
            }
            else if (e.Key.EqualsIgnoreCase("F_BOA_Reset"))//重新过磅
            {
                var currentRow = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");
                resetCurrentRow = currentRow;
                var isInStock = Convert.ToBoolean(this.View.Model.GetValue("F_BOA_IsInStock", currentRow));
                if (!isInStock)
                {
                    this.View.ShowWarnningMessage("只有已生成入库单的数据行，可以执行重新过磅操作！");
                    return;
                }
                uniqueNoValue = this.View.Model.GetValue("F_BOA_UniqueNo", currentRow).ToString();
                var result = SqlHelper.GetInStockIdByUniqueNo(this.Context, uniqueNoValue);
                if (result.Count > 0)
                {
                    var inStockIdList = result.Where(t => t["flag"].ToString() == "采购入库单")
                                              .Select(t => t["fid"].ToString()).ToList();
                    if (inStockIdList.Count > 0)
                    {
                        var inStockIdStr = string.Join(",", inStockIdList);
                        WebApiHelper.UnAudit(this.Context, "STK_InStock", inStockIdStr);
                        WebApiHelper.Delete(this.Context, "STK_InStock", inStockIdStr);
                    }
                    var purOrderIdList = result.Where(t => t["flag"].ToString() == "采购订单")
                                              .Select(t => t["fid"].ToString()).ToList();
                    if (purOrderIdList.Count > 0)
                    {
                        var purOrderIdStr = string.Join(",", purOrderIdList);
                        WebApiHelper.UnAudit(this.Context, "PUR_PurchaseOrder", purOrderIdStr);
                        WebApiHelper.Delete(this.Context, "PUR_PurchaseOrder", purOrderIdStr);
                    }
                }
                RefreshPurOrderInfo(currentRow);
                isResetWeight = true;

                ExcuteCustomerCtlNew();

                //WeightOperate("10");
            }
            else if (e.Key.EqualsIgnoreCase("F_BOA_WeightEnd"))//整车过磅完毕
            {
                BtnWeightEnd();
            }
        }

        public override void DataChanged(DataChangedEventArgs e)
        {
            base.DataChanged(e);
            if (e.Field.Key.EqualsIgnoreCase("F_BOA_Model") || e.Field.Key.EqualsIgnoreCase("F_BOA_SupplierId"))
            {
                FillSubEntity(e.Row);//值更新
            }
            else if (e.Field.Key.EqualsIgnoreCase("F_BOA_WeighDataS"))
            {
                var id = e.NewValue;
                if (id != null)
                {
                    //noListRecord = new List<int>();
                    var dataInfo = SqlHelper.GetWeighDataById(this.Context, Convert.ToInt64(id));
                    if (dataInfo.Count > 0)
                    {
                        FillInEntry(dataInfo);
                    }
                }
            }
            else if (e.Field.Key.EqualsIgnoreCase("F_BOA_GoodsBill") && isUpdateField)//送货单号 单据头
            {
                var currentRow = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");
                var sendValue = this.View.Model.GetValue("F_BOA_SendNo", currentRow);
                if (sendValue.IsNullOrEmptyOrWhiteSpace() && !e.NewValue.IsNullOrEmptyOrWhiteSpace())
                {
                    this.View.Model.SetValue("F_BOA_SendNo", e.NewValue, currentRow);
                }
            }
            else if (e.Field.Key.EqualsIgnoreCase("F_BOA_Supplier") && isUpdateField)//供应商
            {
                var currentRow = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");
                var supplierValue = this.View.Model.GetValue("F_BOA_SupplierId", currentRow);
                if (supplierValue.IsNullOrEmptyOrWhiteSpace() && !e.NewValue.IsNullOrEmptyOrWhiteSpace())
                {
                    this.View.Model.SetValue("F_BOA_SupplierId", e.NewValue, currentRow);
                }
            }
            else if (e.Field.Key.EqualsIgnoreCase("F_BOA_SendNo"))//单据体-送货单号
            {
                var sendNoValue = e.NewValue;
                if (sendNoValue.IsNullOrEmptyOrWhiteSpace())
                {
                    return;
                }
                CreateEnumValue(sendNoValue.ToString().Trim());
            }
            else if (e.Field.Key.EqualsIgnoreCase("F_BOA_Type"))//过磅类型
            {
                var fieldValue = e.NewValue.ToString();
                if (fieldValue == "1")//单包磅差
                {
                    this.View.ShowWarnningMessage("已切换单包磅差！");
                }
                else if (fieldValue == "2")//整车磅差
                {
                    this.View.ShowWarnningMessage("已切换整车磅差！");
                }
            }
        }

        public override void AfterBarItemClick(AfterBarItemClickEventArgs e)
        {
            base.AfterBarItemClick(e);
            if (e.BarItemKey.EqualsIgnoreCase("BOA_tbButton_2"))//打开参数配置
            {
                var openPa = new BillShowParameter();
                openPa.FormId = "BOA_WeighingConfigure";
                openPa.Status = OperationStatus.EDIT;
                openPa.OpenStyle.ShowType = ShowType.Modal;
                openPa.PKey = "100002";
                this.View.ShowForm(openPa, delegate (FormResult result)
                {
                    portInfo = SqlHelper.GetPort(this.Context);
                });
            }
        }

        public override void AfterBindData(EventArgs e)
        {
            base.AfterBindData(e);
            this.View.Model.SetValue("F_BOA_EmpId", SqlHelper.GetEmpInfoIdByUserId(this.Context));
            this.View.UpdateView("F_BOA_EmpId");
            //portInfo = SqlHelper.GetPort(this.Context);
            //this.View.Model.SetValue("F_BOA_Weighbridge", portInfo[0]["F_BOA_WEIGHBRIDGE"]);
            //this.View.UpdateView("F_BOA_Weighbridge");
            OpenCom();
        }

        public override void CustomEvents(CustomEventsArgs e)
        {
            base.CustomEvents(e);
            if (e.Key.EqualsIgnoreCase("F_BOA_CustomCtl"))
            {
                var _data = e.EventArgs;
                if (_data.Length == 0)
                {
                    this.View.ShowErrMessage("无称重数据！");
                    return;
                }
                if (_data.StartsWith("v"))
                {
                    this.View.ShowMessage(_data);
                    return;
                }
                if (_data.StartsWith("msg"))
                {
                    this.View.ShowErrMessage(_data);
                    return;
                }

                this.View.Model.SetValue("F_BOA_QtyText", _data);

                WeightOperate(_data);
            }
        }

        public override void OnPrepareNotePrintData(PreparePrintDataEventArgs e)
        {
            base.OnPrepareNotePrintData(e);
            if (e.DataSourceId.EqualsIgnoreCase("FBillHead"))
            {
                formMetadata = formMetadata ?? MetaDataServiceHelper.Load(this.Context, "BOA_PrintBill") as FormMetadata;
                var data = BusinessDataServiceHelper.Load(this.Context, new object[] { printId }, formMetadata.BusinessInfo.GetDynamicObjectType(true));
                var list = new DynamicObjectCollection(e.DynamicObjectType);
                var row = new DynamicObject(e.DynamicObjectType);
                foreach (var item in e.Fields)
                {
                    var key = item.ToString();
                    var dataKey = key;
                    if (!data[0].Contains(key))
                    {
                        dataKey = key.SubStr(1, key.Length - 1);
                    }
                    row[key] = data[0][dataKey];
                }
                list.Add(row);
                e.DataObjects = list.ToArray();
            }
        }

        public override void BeforeClosed(BeforeClosedEventArgs e)
        {
            base.BeforeClosed(e);
            CloseCom();
        }

        public override void AfterEntryBarItemClick(AfterBarItemClickEventArgs e)
        {
            base.AfterEntryBarItemClick(e);
            if (e.BarItemKey.EqualsIgnoreCase("BOA_copy"))//复制行
            {
                isCopyEntry = true;
                var currentRow = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");
                var materialInfo = this.View.Model.GetValue("F_BOA_Model", currentRow);
                var sendNo = this.View.Model.GetValue("F_BOA_SendNo", currentRow);
                var supplier = this.View.Model.GetValue("F_BOA_SupplierId", currentRow);
                var rowCount = this.View.Model.GetEntryRowCount("F_BOA_Entity");
                this.View.Model.InsertEntryRow("F_BOA_Entity", rowCount);
                this.View.Model.SetValue("F_BOA_Model", materialInfo, rowCount);
                this.View.Model.SetValue("F_BOA_SendNo", sendNo, rowCount);
                this.View.Model.SetValue("F_BOA_SupplierId", supplier, rowCount);
                this.View.InvokeFieldUpdateService("F_BOA_Model", rowCount);
            }
            else if (e.BarItemKey.EqualsIgnoreCase("BOA_subRefresh"))//子单据体刷新
            {
                var currentRow = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");
                FillSubEntity(currentRow);//刷新
            }
        }

        public override void EntityRowClick(EntityRowClickEventArgs e)
        {
            base.EntityRowClick(e);
            if (e.Key.EqualsIgnoreCase("F_BOA_Entity"))//单据体行切换
            {
                isUpdateField = false;
                var currentRow = e.Row;
                //FillSubEntity(currentRow);
                RefreshPurOrderInfo(currentRow);
                if (lastRow == currentRow)
                {
                    this.View.Model.SetValue("F_BOA_Select", true, currentRow);
                }
                else
                {
                    this.View.Model.SetValue("F_BOA_Select", false, lastRow);
                    lastRow = currentRow;
                    this.View.Model.SetValue("F_BOA_Select", true, currentRow);
                }
                var sendNo = this.View.Model.GetValue("F_BOA_SendNo", currentRow);
                var supplier = this.View.Model.GetValue("F_BOA_SupplierId", currentRow);
                this.View.Model.SetValue("F_BOA_GoodsBill", sendNo);
                this.View.Model.SetValue("F_BOA_Supplier", supplier);
                isUpdateField = true;
            }
        }

        public override void AfterCreateNewEntryRow(CreateNewEntryEventArgs e)
        {
            base.AfterCreateNewEntryRow(e);
            if (!isCopyEntry)
            {
                var sendNo = this.View.Model.GetValue("F_BOA_GoodsBill");//送货单号
                var supplier = this.View.Model.GetValue("F_BOA_Supplier");//供应商
                if (!sendNo.IsNullOrEmptyOrWhiteSpace())
                {
                    this.View.Model.SetValue("F_BOA_SendNo", sendNo, e.Row);
                }
                if (!supplier.IsNullOrEmptyOrWhiteSpace())
                {
                    this.View.Model.SetValue("F_BOA_SupplierId", supplier, e.Row);
                }
            }
            isCopyEntry = false;
        }

        /// <summary>
        /// 过磅 或 重新过磅操作
        /// </summary>
        /// <param name="_data">称重数据</param>
        private void WeightOperate(string _data)
        {
            realWeightQty = _data;
            var isTrue = WeighBTClick(out int index, out string msg, out object stockInfo,
                                       out object printTemp, out decimal inStockQty);
            if (isTrue)
            {
                SetUniqueNo(uniqueNoValue);
                InStockBTClick(index, inStockQty, stockInfo, printTemp, msg);
            }
            realWeightQty = "0";
            uniqueNoValue = "";
            inStockBillNo = "";
        }

        /// <summary>
        /// 刷新采购订单信息
        /// </summary>
        /// <param name="currentRow"></param>
        private void RefreshPurOrderInfo(int currentRow)
        {
            var rowsupplierInfo = this.View.Model.GetValue("F_BOA_SupplierId", currentRow);//单据体供应商信息
            var supplierId = "";//供应商内码
            if (rowsupplierInfo != null)
            {
                supplierId = (rowsupplierInfo as DynamicObject)["Id"].ToString();
            }
            //else
            //{
            //    var supplierInfo = this.View.Model.GetValue("F_BOA_Supplier");//供应商
            //    if (supplierInfo != null)
            //    {
            //        supplierId = (supplierInfo as DynamicObject)["Id"].ToString();
            //    }
            //}
            if (supplierId == "")
            {
                return;
            }
            var materialInfo = this.View.Model.GetValue("F_BOA_Model", currentRow);
            if (materialInfo == null)
            {
                return;
            }
            var materialId = (materialInfo as DynamicObject)["Id"];//物料内码
            var purOrderInfo = SqlHelper.GetPurOrderInfo(this.Context, materialId, supplierId);
            var purOrderInfoItem = purOrderInfos.FirstOrDefault(t => t.Row == currentRow && t.MaterialId == Convert.ToInt64(materialId));
            if (purOrderInfoItem != null)
            {
                purOrderInfos.Remove(purOrderInfoItem);
            }
            purOrderInfos.Add(new PurOrderInfo { Row = currentRow, MaterialId = Convert.ToInt64(materialId), OrderInfo = purOrderInfo });
        }

        /// <summary>
        /// 生成入库单
        /// </summary>
        /// <param name="rowIndex">选择的行索引</param>
        /// <param name="inStockQty">入库重量</param>
        /// <param name="stockInfo">仓库信息</param>
        /// <param name="printTemp">打印模板信息</param>
        /// <param name="msg">提示信息</param>
        private void InStockBTClick(int rowIndex, decimal inStockQty, object stockInfo, object printTemp, string msg)
        {
            entity = entity ?? this.View.BillBusinessInfo.GetEntity("F_BOA_Entity");
            var row = this.View.Model.GetEntityDataObject(entity, rowIndex);
            var pushResult = PurOrderPushPurIn(row, inStockQty, stockInfo, rowIndex);
            var pushMsg = "";
            if (pushResult == null || !pushResult.IsSuccessed)
            {
                pushMsg = $"{(pushResult == null ? "数据异常！" : pushResult.ErrorResult)}";
                //this.View.ShowWarnningMessage();
                //return;
            }

            _inStockDtos = new List<InStockDto>();

            var barCode = CreateBarCodeMainFile(row);

            printId = CreatePrintBill(row, out string errorMsg, barCode);
            if (printId == 0)
            {
                //var uniqueNoValue111 = this.View.Model.GetValue("F_BOA_UniqueNo", rowIndex).ToString();
                var result = SqlHelper.GetInStockIdByUniqueNo(this.Context, uniqueNo);
                if (result.Count > 0)
                {
                    var inStockIdList = result.Where(t => t["flag"].ToString() == "采购入库单")
                                              .Select(t => t["fid"].ToString()).ToList();
                    if (inStockIdList.Count > 0)
                    {
                        var inStockIdStr = string.Join(",", inStockIdList);
                        WebApiHelper.UnAudit(this.Context, "STK_InStock", inStockIdStr);
                        WebApiHelper.Delete(this.Context, "STK_InStock", inStockIdStr);
                    }
                    var purOrderIdList = result.Where(t => t["flag"].ToString() == "采购订单")
                                              .Select(t => t["fid"].ToString()).ToList();
                    if (purOrderIdList.Count > 0)
                    {
                        var purOrderIdStr = string.Join(",", purOrderIdList);
                        WebApiHelper.UnAudit(this.Context, "PUR_PurchaseOrder", purOrderIdStr);
                        WebApiHelper.Delete(this.Context, "PUR_PurchaseOrder", purOrderIdStr);
                    }
                }
                this.View.ShowErrMessage($"打印标签失败，原因如下：{errorMsg}");
                FillSubEntity(rowIndex);//刷新
                return;
            }

            this.View.Model.SetValue("F_BOA_IsInStock", true, rowIndex);
            this.View.GetFieldEditor("F_BOA_SendQty", rowIndex).Enabled = false;

            DoPrint(printId.ToString(), printTemp);
            this.View.Model.SetValue("F_BOA_Select", false, rowIndex);
            this.View.GetFieldEditor("F_BOA_Select", rowIndex).Enabled = false;
            this.View.GetFieldEditor("F_BOA_PurOrder", rowIndex).Enabled = false;
            this.View.GetFieldEditor("F_BOA_Model", rowIndex).Enabled = false;
            this.View.GetFieldEditor("F_BOA_Pieces", rowIndex).Enabled = false;
            this.View.Model.SetValue("F_BOA_UniqueNo", uniqueNo, rowIndex);
            RecordWeighLog(entity);

            FillSubEntity(rowIndex);//刷新

            var rows = this.View.Model.GetEntityDataObject(entity);
            var rowsW = rows.Where(t => Convert.ToBoolean(t["F_BOA_IsInStock"]) == false)
                        .OrderBy(t => Convert.ToInt32(t["Seq"])).FirstOrDefault();
            if (rowsW != null)
            {
                var focusRowIndex = Convert.ToInt32(rowsW["Seq"]) - 1;
                this.View.SetEntityFocusRow("F_BOA_Entity", focusRowIndex);//单据体焦点行
            }
            else
            {
                this.View.SetEntityFocusRow("F_BOA_Entity", rowIndex);//单据体焦点行
            }

            this.View.ShowMessage($"生成入库单成功！\n{msg}\n{pushMsg}");
        }

        /// <summary>
        /// 过磅按钮点击事件，判断称重条件是否都满足
        /// </summary>
        /// <param name="rowIndex1">当前行</param>
        /// <param name="msg1">提示信息</param>
        /// <param name="stockInfo">仓库信息</param>
        /// <param name="printTemp">打印模板信息</param>
        /// <param name="inStockQty">入库重量</param>
        private bool WeighBTClick(out int rowIndex1, out string msg1, out object stockInfo, out object printTemp,
                 out decimal inStockQty)
        {
            rowIndex1 = 0;
            msg1 = string.Empty;
            stockInfo = this.View.Model.GetValue("F_BOA_Stock");
            printTemp = this.View.Model.GetValue("F_BOA_Template");
            inStockQty = 0m;
            if (stockInfo == null)
            {
                this.View.ShowWarnningMessage("请先录入仓库！");
                return false;
            }
            if (printTemp == null)
            {
                this.View.ShowWarnningMessage("请选择打印模板！");
                return false;
            }
            var supplierInfo = this.View.Model.GetValue("F_BOA_Supplier");
            if (supplierInfo == null)
            {
                this.View.ShowWarnningMessage("请先录入供应商！");
                return false;
            }

            entity = entity ?? this.View.BillBusinessInfo.GetEntity("F_BOA_Entity");
            var entityData = this.View.Model.GetEntityDataObject(entity);

            var currentSelectRow = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");//当前分录行索引
            var selectList = entityData.Where(t => Convert.ToInt32(t["Seq"]) - 1 == currentSelectRow)  //Convert.ToBoolean(t["F_BOA_Select"]) == true
                            .Select(t => new
                            {
                                Seq = Convert.ToInt32(t["Seq"]),
                                MaterialId = Convert.ToInt64(t["F_BOA_Model_Id"])
                            }).ToList();
            if (selectList.Count != 1 && !isResetWeight)//重新过磅不需要判断选择行
            {
                this.View.ShowWarnningMessage("请选择过磅的行，并且只能选择一行！");
                return false;
            }
            var rowIndex = isResetWeight ? resetCurrentRow : selectList.First().Seq - 1;//选择的行索引
            _currentRow = rowIndex;
            var materialInfo = this.View.Model.GetValue("F_BOA_Model", rowIndex);
            if (materialInfo == null)
            {
                this.View.ShowWarnningMessage("收料信息当前选中分录行无效，请选择物料！");
                return false;
            }
            var isInStock = Convert.ToBoolean(this.View.Model.GetValue("F_BOA_IsInStock", rowIndex));
            if (isInStock && !isResetWeight)//重新过磅不需要判断是否生成入库单
            {
                this.View.ShowWarnningMessage("当前行已生成入库单！无需再次过磅。");
                return false;
            }

            var materialId = isResetWeight ? Convert.ToInt64((materialInfo as DynamicObject)["Id"])
                             : selectList.First().MaterialId;
            var purOrderInfo = purOrderInfos.FirstOrDefault(t => t.MaterialId == materialId && t.Row == rowIndex);
            if (purOrderInfo == null || purOrderInfo.OrderInfo.Count == 0)
            {
                this.View.ShowWarnningMessage("没有采购订单信息，不能过磅！");
                return false;
            }
            var sendQty1 = Convert.ToDecimal(this.View.Model.GetValue("F_BOA_SendQty", rowIndex));//送货重量
            if (sendQty1 <= 0)
            {
                this.View.ShowWarnningMessage("送货重量为0！");
                //this.View.Model.SetValue("F_BOA_Select", true, rowIndex);
                return false;
            }

            this.View.Model.SetValue("F_BOA_Qty", realWeightQty);//毛重赋值(毛重就是称重重量)
            //获取净重
            var realQty = GetRealQty();
            //净重赋值
            this.View.Model.SetValue("F_BOA_RealQty", realQty);//净重赋值
            this.View.Model.SetValue("F_BOA_WeighQty", realQty, rowIndex);//过磅重量赋值(过磅重量就是净重)
            //this.View.Model.SetValue("F_BOA_Select", false, rowIndex);
            if (realQty <= 0)
            {
                this.View.ShowWarnningMessage("过磅重量必须大于0！");
                return false;
            }

            var msg = string.Empty;
            var diff = (realQty - sendQty1) / sendQty1;
            _diffRata = diff;
            if (diff <= 0.003m && diff >= -0.003m)
            {
                this.View.Model.SetValue("F_BOA_IsIn", true, rowIndex);
                inStockQty = sendQty1;
            }
            else
            {
                this.View.Model.SetValue("F_BOA_IsIn", false, rowIndex);
                msg = "过磅重量超出送货重量0.3%范围！\n";
                inStockQty = realQty;
            }
            var unInStockQty = purOrderInfo.OrderInfo.Sum(t => Convert.ToDecimal(t["FREMAINSTOCKINQTY"]));//未入库数量
            if (sendQty1 > unInStockQty)
            {
                msg += "送货重量超出采购订单未入库数量！\n";
            }
            var sendNo = this.View.Model.GetValue("F_BOA_GoodsBill");
            if (sendNo.IsNullOrEmptyOrWhiteSpace())
            {
                msg += "送货单号未填写！";
            }

            //新增的逻辑  整车磅差 即 送货重量为入库重量
            if (CheckChoose())
            {
                inStockQty = sendQty1;
            }

            _stockNumber = (stockInfo as DynamicObject)["Number"].ToString();

            msg1 = msg;
            rowIndex1 = rowIndex;
            return true;
        }

        /// <summary>
        /// 设置每包唯一号（弃用）
        /// </summary>
        /// <param name="row">行索引</param>
        //private void SetUniqueNo(int row, object newValue)
        //{
        //    if (newValue == null)
        //    {
        //        this.View.Model.SetValue("F_BOA_UniqueNo", null, row);
        //        return;
        //    }
        //    int no = 1;
        //    var noList = SqlHelper.GetLastUniqueNo(this.Context);
        //    if (noList.Count > 0)
        //    {
        //        var defectNoList = GetDefectNo(noList);
        //        if (defectNoList.Count > 0 && defectNoList.Count != noListRecord.Count)
        //        {
        //            foreach (var item in defectNoList)
        //            {
        //                var isExist = noListRecord.Where(t => t == item).ToList();
        //                if (isExist.Count == 0)
        //                {
        //                    no = item;
        //                    noListRecord.Add(item);
        //                    break;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            if (row == 0)//第一行数据
        //            {
        //                no = noList.Last() + 1;
        //            }
        //            else
        //            {
        //                var uniqueNo = this.View.Model.GetValue("F_BOA_UniqueNo", row - 1).ToString();//上一行记录的唯一号
        //                var upNo = Convert.ToInt32(uniqueNo.Split('/')[1]) + 1;
        //                if (upNo <= noList.Last())
        //                {
        //                    no = noList.Last() + 1;
        //                }
        //                else
        //                {
        //                    no = upNo;
        //                }
        //            }
        //        }
        //    }
        //    else
        //    {
        //        if (row > 0)
        //        {
        //            var uniqueNo = this.View.Model.GetValue("F_BOA_UniqueNo", row - 1).ToString();//上一行记录的唯一号
        //            no = Convert.ToInt32(uniqueNo.Split('/')[1]) + 1;
        //        }
        //    }
        //    var currentDayOfWeek = DateTime.Now.DayOfWeek;//获取当前日期是星期几
        //    var currentDate = DateTime.Now.ToString("yyyy-MM-dd");//当前日期
        //    if (currentDayOfWeek == DayOfWeek.Sunday)//如果今天是星期日，则回退一天
        //    {
        //        currentDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        //    }
        //    this.View.Model.SetValue("F_BOA_UniqueNo", $"{currentDate}/{no}", row);
        //}

        /// <summary>
        /// 设置每包唯一号
        /// </summary>
        private void SetUniqueNo(string uniqueNoOld = "")
        {
            if (uniqueNoOld != "")//表示重新过磅，不需要生成新的每包唯一号
            {
                uniqueNo = uniqueNoOld;
                return;
            }
            int no = 1;
            var noUniq = SqlHelper.GetLastUniqueNo1(this.Context);
            if (noUniq != "")
            {
                no = Convert.ToInt32(noUniq.Split('/')[1]) + 1;
            }
            var currentDayOfWeek = DateTime.Now.DayOfWeek;//获取当前日期是星期几
            var currentDate = DateTime.Now.ToString("yyyy-MM-dd");//当前日期
            if (currentDayOfWeek == DayOfWeek.Sunday)//如果今天是星期日，则回退一天
            {
                currentDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                //判断周日是否已经存在每包唯一号，存在，则不取前一天的每包唯一好，直接新增
                if (noUniq == "")//周日不存在每包唯一号，取前一天的最近的每包唯一号
                {
                    //判断前一天是否存在每包唯一号
                    var sqlStr = $@"/*dialect*/select top 1 FBILLNO
from T_BOA_PrintBill
where convert(varchar(100), FCREATEDATE, 23) = '{currentDate}' order by FCREATEDATE desc";
                    var result = DBUtils.ExecuteScalar(this.Context, sqlStr, "");
                    if (result != "")
                    {
                        no = Convert.ToInt32(result.Split('/')[1]) + 1;
                    }
                }
                //else//周日存在每包为一号
                //{
                //    if (noUniq != "")
                //    {
                //        no = Convert.ToInt32(noUniq.Split('/')[1]) + 1;
                //    }
                //}
            }

            var newNo = no.ToString("00");

            uniqueNo = $"{currentDate}/{newNo}";
        }

        /// <summary>
        /// 填充子单据体数据(获取采购订单信息)
        /// </summary>
        /// <param name="currentRow">当前行索引</param>
        private void FillSubEntity(int currentRow)
        {
            var rowsupplierInfo = this.View.Model.GetValue("F_BOA_SupplierId", currentRow);//单据体供应商信息
            var supplierId = "";//供应商内码
            if (rowsupplierInfo != null)
            {
                supplierId = (rowsupplierInfo as DynamicObject)["Id"].ToString();
            }
            //else
            //{
            //    var supplierInfo = this.View.Model.GetValue("F_BOA_Supplier");//供应商
            //    if (supplierInfo != null)
            //    {
            //        supplierId = (supplierInfo as DynamicObject)["Id"].ToString();
            //    }
            //}
            if (supplierId == "")
            {
                return;
            }

            var materialInfo = this.View.Model.GetValue("F_BOA_Model", currentRow);
            var subEntityRowCount = this.View.Model.GetEntryRowCount("F_BOA_SubEntity");
            if (subEntityRowCount > 0)//清除已有分录
            {
                //for (var i = 0; i < subEntityRowCount; i++)
                //{
                //    this.View.Model.DeleteEntryRow("F_BOA_SubEntity", 0);
                //}
                this.View.Model.DeleteEntryData("F_BOA_SubEntity");
            }
            if (materialInfo == null)
            {
                return;
            }
            var materialId = (materialInfo as DynamicObject)["Id"];//物料内码
            var purOrderInfo = SqlHelper.GetPurOrderInfo(this.Context, materialId, supplierId);
            if (purOrderInfo.Count > 0)
            {
                var purOrderInfoItem = purOrderInfos.FirstOrDefault(t => t.Row == currentRow && t.MaterialId == Convert.ToInt64(materialId));
                if (purOrderInfoItem != null)
                {
                    purOrderInfos.Remove(purOrderInfoItem);
                }
                purOrderInfos.Add(new PurOrderInfo { Row = currentRow, MaterialId = Convert.ToInt64(materialId), OrderInfo = purOrderInfo });
                for (var i = 0; i < purOrderInfo.Count; i++)
                {
                    isCopyEntry = true;
                    var purOrderItem = purOrderInfo[i];
                    this.View.Model.InsertEntryRow("F_BOA_SubEntity", i);
                    this.View.Model.SetValue("F_BOA_PurNumber", purOrderItem["FBILLNO"], i);//采购订单编号
                    this.View.Model.SetValue("F_BOA_Seq", purOrderItem["FSeq"], i);//行号
                    this.View.Model.SetValue("F_BOA_Name", purOrderItem["FMATERIALID"], i);//物料
                    this.View.Model.SetValue("F_BOA_OrderQty", purOrderItem["FQTY"], i);//订单数量
                    this.View.Model.SetValue("F_BOA_OrderUnitID", purOrderItem["FUNITID"], i);//单位
                    this.View.Model.SetValue("F_BOA_UnInStockQty", purOrderItem["FREMAINSTOCKINQTY"], i);//未入库数量
                    this.View.Model.SetValue("F_BOA_DeliveryDate", purOrderItem["FDELIVERYEARLYDATE"], i);//要货日期
                    this.View.Model.SetValue("F_BOA_AuditDate", purOrderItem["FAPPROVEDATE"], i);//审核日期
                    this.View.Model.SetValue("F_BOA_BillStatus", purOrderItem["FDOCUMENTSTATUS"], i);//审核状态
                    this.View.Model.SetValue("F_BOA_CloseStatus", purOrderItem["FCLOSESTATUS"], i);//关闭状态
                    this.View.Model.SetValue("F_BOA_BillId", purOrderItem["FID"], i);//单据内码
                    this.View.Model.SetValue("F_BOA_EntryId", purOrderItem["FENTRYID"], i);//分录内码
                }
                //isCopyEntry = false;
                this.View.UpdateView("F_BOA_SubEntity");
            }
        }

        /// <summary>
        /// 初始化串口
        /// </summary>
        private void InitPort()
        {
            if (portInfo.Count == 0)
            {
                this.View.ShowErrMessage("端口未配置或未启用！");
                return;
            }
            var portName = portInfo[0]["F_BOA_PORT"].ToString();
            var Weighbridge = portInfo[0]["F_BOA_WEIGHBRIDGE"];
            this.View.Model.SetValue("F_BOA_Weighbridge", Weighbridge);
            this.View.UpdateView("F_BOA_Weighbridge");
            var cfg = new KDSerialPortConfig();
            cfg.PortName = portName;
            cfg.Rate = 9600;
            cfg.Parity = 0;
            cfg.Bits = 8;
            cfg.StopBits = 1;
            cfg.Timeout = -1;
            cfg.EncodingName = "ASCII";
            //cfg.RecDataEndingChars = "";
            this.View.GetControl<SerialPortControl>("F_BOA_SerialPortCtrl").Init(cfg);
            this.View.GetControl<SerialPortControl>("F_BOA_SerialPortCtrl").SetAllowFireEvtBack(true);
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        private void CloseWeighSerial()
        {
            this.View.GetControl<SerialPortControl>("F_BOA_SerialPortCtrl").Close();
        }

        /// <summary>
        /// 转码
        /// </summary>
        /// <param name="mHex">称头传过来的值</param>
        /// <returns></returns>
        private string HexToStr(string mHex)
        {
            mHex = mHex.Replace(" ", "");
            if (mHex.Length <= 0)
            {
                return "";
            }
            byte[] vBytes = new byte[mHex.Length / 2];
            for (int i = 0; i < mHex.Length; i += 2)
            {
                if (!byte.TryParse(mHex.Substring(i, 2), NumberStyles.HexNumber, null, out vBytes[i / 2])) vBytes[i / 2] = 0;
            }
            return Encoding.Default.GetString(vBytes);
        }

        /// <summary>
        /// 生成打印标签单据
        /// </summary>
        /// <param name="barCode">条形码</param>
        private long CreatePrintBill(DynamicObject row, out string msg, string barCode)
        {
            msg = string.Empty;
            var printBillId = SqlHelper.GetPrintBillByUniqueNo(this.Context, uniqueNo);
            if (printBillId == 0)
            {
                var materialInfo = row["F_BOA_Model"] as DynamicObject;
                var materialId = Convert.ToInt64(row["F_BOA_Model_Id"]);
                var rowIndex = Convert.ToInt32(row["Seq"]) - 1;
                var purOrderInfo = purOrderInfos.FirstOrDefault(t => t.MaterialId == materialId && t.Row == rowIndex);
                var orderNo = string.Empty;
                if (purOrderInfo != null && purOrderInfo.OrderInfo.Count > 0)
                {
                    var orderNoList = purOrderInfo.OrderInfo.Select(t => t["FBILLNO"].ToString()).FirstOrDefault();
                    orderNo = orderNoList ?? "";
                }
                var sendQty = Convert.ToDecimal(row["F_BOA_SendQty"]);
                var weighQty = Convert.ToDecimal(row["F_BOA_WeighQty"]);
                var isIn = Convert.ToBoolean(row["F_BOA_IsIn"]);
                var inStockQty = isIn ? sendQty : weighQty;//入库重量

                //新增的逻辑  整车磅差 即 送货重量为入库重量
                if (CheckChoose())
                {
                    inStockQty = sendQty;
                }

                //var wQy = Convert.ToDecimal(this.View.Model.GetValue("F_BOA_Qty"));//过磅重量(称重重量)
                var head = new JObject();
                head["FBillNo"] = uniqueNo;
                head["F_BOA_InStockDate"] = uniqueNo;
                head["F_BOA_MaterialName"] = materialInfo["F_BOA_Name"].ToString();
                head["F_BOA_OrderNo"] = orderNo;
                head["F_BOA_Model"] = materialInfo["F_BOA_SPECIFICATION"].ToString();
                //head["F_BOA_MaterialStatus"] = "";
                head["F_BOA_Qty"] = sendQty;//送货重量
                head["F_BOA_WQty"] = weighQty;//过磅重量
                head["F_BOA_InOrderNo"] = inStockBillNo;
                head["F_BOA_InstockQty"] = inStockQty;//入库重量
                head["F_BOA_BarCode"] = barCode;//条形码
                head["F_BOA_Pieces"] = row["F_BOA_Pieces"].ToString();//片数
                var result = WebApiHelper.ExcuteSaveOperate(this.Context, head, "BOA_PrintBill", isAutoSubmitAndAudit: true);
                msg = result.ErrorResult;
                return result.Id;
            }
            else
            {
                if (uniqueNoValue != "")//重新过磅需要修改打印单据上的字段值
                {
                    var sendQty = Convert.ToDecimal(row["F_BOA_SendQty"]);
                    var weighQty = Convert.ToDecimal(row["F_BOA_WeighQty"]);
                    var isIn = Convert.ToBoolean(row["F_BOA_IsIn"]);
                    var inStockQty = isIn ? sendQty : weighQty;//入库重量

                    //新增的逻辑  整车磅差 即 送货重量为入库重量
                    if (CheckChoose())
                    {
                        inStockQty = sendQty;
                    }

                    var updateSql = $@"update T_BOA_PrintBill 
set F_BOA_Qty = {sendQty},F_BOA_WQty = {weighQty},F_BOA_InstockQty = {inStockQty}
,F_BOA_InOrderNo = '{inStockBillNo}',F_BOA_BarCode = '{barCode}',F_BOA_Pieces = {row["F_BOA_Pieces"]}
where FID = {printBillId}";
                    var res = DBUtils.Execute(this.Context, updateSql);
                }
                return printBillId;
            }
        }

        /// <summary>
        /// 打印
        /// </summary>
        /// <param name="id">单据内码</param>
        /// <param name="printTemp">打印标签模板</param>
        private void DoPrint(string id, object printTemp)
        {
            var printTempNumber = (printTemp as DynamicObject)["F_BOA_Number"].ToString();
            var printName = this.View.Model.GetValue("F_BOA_Port");
            var printInfoList = new List<PrintJobItem>();
            var printItemInfo = new PrintJobItem();
            printItemInfo.BillId = id;
            printItemInfo.FormId = "BOA_PrintBill";
            printItemInfo.TemplateId = printTempNumber;
            printInfoList.Add(printItemInfo);
            var key = Guid.NewGuid().ToString();
            this.View.Session[key] = printInfoList;
            var jsonObj = new JSONObject();
            jsonObj.Put("pageID", this.View.PageId);
            jsonObj.Put("printJobId", key);
            jsonObj.Put("action", "print");
            //jsonObj.Put("action", "preview");
            if (!printName.IsNullOrEmptyOrWhiteSpace())
            {
                jsonObj.Put("printerAddress", printName.ToString());
            }
            this.View.AddAction(JSAction.print, jsonObj);
        }

        /// <summary>
        /// 记录称重日志
        /// </summary>
        /// <param name="row"></param>
        private void RecordWeighLog(Entity entity)
        {
            if (isResetWeight)//重新过磅，已重新过磅复选框打勾
            {
                SqlHelper.UpdateRecoreStatus(this.Context, uniqueNo);
            }
            var rowW = this.View.Model.GetEntityDataObject(entity);
            var rows = rowW.Where(t => Convert.ToInt32(t["Seq"]) - 1 == _currentRow).ToList();
            var head = new JObject();
            head["F_BOA_Qty"] = GetFieldJObjectValue("F_BOA_Qty");//毛重
            head["F_BOA_OrgId"] = GetJObjectValue("F_BOA_OrgId", "Number", "Id");//生产组织
            head["F_BOA_RealQty"] = GetFieldJObjectValue("F_BOA_RealQty");//净重
            //head["F_BOA_Template"] = GetJObjectValue("F_BOA_Template", "F_BOA_Number", "Id", "F_BOA_Number");//打印标签模板
            //head["F_BOA_Port"] = GetFieldJObjectValue("F_BOA_Port");//打印机端口
            head["F_BOA_Stock"] = GetJObjectValue("F_BOA_Stock", "Number", "Id", jsonKey: "FNUMBER");//仓库
            head["F_BOA_QtyText"] = GetFieldJObjectValue("F_BOA_QtyText");//称重重量
            head["F_BOA_EmpId"] = GetJObjectValue("F_BOA_EmpId", "FStaffNumber", "Id", jsonKey: "FSTAFFNUMBER");//称重人员
            head["F_BOA_Package"] = GetJObjectValue("F_BOA_Package", "Number", "Id");//包装重量
            //head["F_BOA_Weighbridge"] = GetFieldJObjectValue("F_BOA_Weighbridge");//启用地磅
            head["F_BOA_GoodsBill"] = GetFieldJObjectValue("F_BOA_GoodsBill");//送货单号
            head["F_BOA_WeighDataS"] = GetJObjectValue("F_BOA_WeighDataS", "Number", "Id", jsonKey: "FNUMBER");//称重数据选择
            head["F_BOA_Supplier"] = GetJObjectValue("F_BOA_Supplier", "Number", "Id");//供应商

            var isAllDiff = CheckChoose();
            if (isAllDiff)
            {
                head["F_BOA_IsAllDiff"] = true;//是否整车磅差
            }

            var entry = new JArray();
            foreach (var row in rows)
            {
                var entryObj = new JObject();
                entryObj["F_BOA_Select"] = row["F_BOA_Select"].ToString();//选择
                entryObj["F_BOA_PurOrder"] = GetJObjectValue("", "F_BOA_BillNo", "Id", "FID", row["F_BOA_PurOrder"], false);//采购订单
                entryObj["F_BOA_Model"] = GetJObjectValue("", "F_BOA_SPECIFICATION", "Id", "FID", row["F_BOA_Model"], false);//物料规格型号
                entryObj["F_BOA_UnitID"] = GetJObjectValue("", "Number", "Id", "FNumber", row["F_BOA_UnitID"]);//单位
                entryObj["F_BOA_SendQty"] = row["F_BOA_SendQty"].ToString();//送货重量
                entryObj["F_BOA_WeighQty"] = row["F_BOA_WeighQty"].ToString();//过磅重量
                entryObj["F_BOA_IsIn"] = row["F_BOA_IsIn"].ToString();//是否0.3%范围内
                entryObj["F_BOA_UniqueNo"] = GetFieldJObjectValue("", row["F_BOA_UniqueNo"]);//每包唯一号
                entryObj["F_BOA_Date"] = GetFieldJObjectValue("", row["F_BOA_Date"]);//要货时间
                entryObj["F_BOA_IsInStock"] = row["F_BOA_IsInStock"].ToString();//是否已生成入库单
                entryObj["F_BOA_InStockNo"] = inStockBillNo;//入库单号
                var isIn = Convert.ToBoolean(row["F_BOA_IsIn"]);

                var sendQtyStr = row["F_BOA_SendQty"].ToString();

                var inStockQty = isIn ? sendQtyStr : row["F_BOA_WeighQty"].ToString();//入库重量

                //新增的逻辑  整车磅差 即 送货重量为入库重量
                if (isAllDiff)
                {
                    inStockQty = sendQtyStr;
                }

                entryObj["F_BOA_InStockQty"] = inStockQty;
                entryObj["F_BOA_ReduceQty"] = _currentPackage;//包装重量

                entryObj["F_BOA_Pieces"] = row["F_BOA_Pieces"].ToString();//片数
                if (!row["F_BOA_SendNo"].IsNullOrEmptyOrWhiteSpace())
                {
                    entryObj["F_BOA_SendNo"] = GetFieldJObjectValue("", row["F_BOA_SendNo"]);//送货单号
                }
                else
                {
                    entryObj["F_BOA_SendNo"] = GetFieldJObjectValue("F_BOA_GoodsBill");//送货单号
                }

                if (!row["F_BOA_SupplierId"].IsNullOrEmptyOrWhiteSpace())
                {
                    entryObj["F_BOA_SupplierId"] = GetJObjectValue("", "Number", "Id", "FNumber", row["F_BOA_SupplierId"]);//供应商
                }
                else
                {
                    entryObj["F_BOA_SupplierId"] = GetJObjectValue("F_BOA_Supplier", "Number", "Id");//供应商
                }

                var subEntry = new JArray();
                foreach (var item in row["F_BOA_SubEntity"] as DynamicObjectCollection)
                {
                    var subEntryItem = new JObject();
                    subEntryItem["F_BOA_PurNumber"] = item["F_BOA_PurNumber"].ToString();//采购订单
                    subEntryItem["F_BOA_Seq"] = item["F_BOA_Seq"].ToString();//行号
                    subEntryItem["F_BOA_Name"] = GetJObjectValue("", "Number", "Id", "FNumber", item["F_BOA_Name"]);//物料名称
                    subEntryItem["F_BOA_OrderUnitID"] = GetJObjectValue("", "Number", "Id", "FNumber", item["F_BOA_OrderUnitID"]);//单位
                    subEntryItem["F_BOA_OrderQty"] = item["F_BOA_OrderQty"].ToString();//订单数量
                    subEntryItem["F_BOA_UnInStockQty"] = item["F_BOA_UnInStockQty"].ToString();//未入库数量
                    subEntryItem["F_BOA_DeliveryDate"] = GetFieldJObjectValue("", item["F_BOA_DeliveryDate"]);//要货日期
                    subEntryItem["F_BOA_AuditDate"] = GetFieldJObjectValue("", item["F_BOA_AuditDate"]);//审核日期
                    //subEntryItem["F_BOA_BillStatus"] = item["F_BOA_BillStatus"].ToString();//审核状态
                    //subEntryItem["F_BOA_CloseStatus"] = item["F_BOA_CloseStatus"].ToString();//关闭状态
                    subEntryItem["F_BOA_BillId"] = item["F_BOA_BillId"].ToString();//单据内码
                    subEntryItem["F_BOA_EntryId"] = item["F_BOA_EntryId"].ToString();//分录内码
                    subEntry.Add(subEntryItem);
                }
                entryObj["F_BOA_SubEntity"] = subEntry;
                entry.Add(entryObj);
            }
            head["F_BOA_Entity"] = entry;
            var result = WebApiHelper.ExcuteSaveOperate(this.Context, head, "BOA_WeighDataRecord");
        }

        /// <summary>
        /// 采购订单下推采购入库单
        /// </summary>
        /// <param name="row">当前行数据</param>
        /// <param name="inStockQty">入库重量</param>
        /// <param name="stockInfo">仓库</param>
        /// <param name="currentWeighRow">当前行</param>
        private ApiResult PurOrderPushPurIn(DynamicObject row, decimal inStockQty, object stockInfo, int currentWeighRow)
        {
            var sendGoodsBillNoInfo = row["F_BOA_SendNo"];//送货单号
            sendGoodsBillNo = sendGoodsBillNoInfo == null ? "" : sendGoodsBillNoInfo.ToString();//送货单号

            _sendQty = Convert.ToDecimal(row["F_BOA_SendQty"]);
            _weightQty = Convert.ToDecimal(row["F_BOA_WeighQty"]);
            _pieces = row["F_BOA_Pieces"].ToString();//片数

            var materialId = Convert.ToInt64(row["F_BOA_Model_Id"]);

            var purOrderInfo = purOrderInfos.FirstOrDefault(t => t.MaterialId == materialId && t.Row == currentWeighRow);
            var stockNumber = (stockInfo as DynamicObject)["Number"].ToString();
            if (purOrderInfo.OrderInfo != null && purOrderInfo.OrderInfo.Count > 0)
            {
                var entryIds = string.Empty;//分录内码集合
                var sumUnInStockQty = 0m;//未入库数量汇总
                var isNeedAddPurOrderBill = false;//是否需要新增采购订单
                foreach (var item in purOrderInfo.OrderInfo)
                {
                    var unInStockQty = Convert.ToDecimal(item["FREMAINSTOCKINQTY"]);//未入库数量
                    var entryId = item["FENTRYID"].ToString();//分录内码
                    entryIds += $"{entryId},";

                    _maxEntryId = entryId;

                    var instockItem = new InStockDto
                    {
                        EntryId = entryId,
                        Qty = unInStockQty
                    };

                    sumUnInStockQty += unInStockQty;
                    if (inStockQty <= sumUnInStockQty)
                    {
                        isNeedAddPurOrderBill = false;

                        instockItem.Qty = inStockQty - _inStockDtos.Sum(t => t.Qty);//最后一条分录的入库数量
                        _inStockDtos.Add(instockItem);

                        break;
                    }
                    else
                    {
                        isNeedAddPurOrderBill = true;
                    }

                    _inStockDtos.Add(instockItem);
                }
                entryIds = entryIds.SubStr(0, entryIds.Length - 1);

                if (!isNeedAddPurOrderBill)//不需要新增采购订单
                {
                    return OrderPushInStock(entryIds, inStockQty, stockNumber);
                }
                else//需要新增采购订单
                {
                    var supplier = this.View.Model.GetValue("F_BOA_SupplierId", currentWeighRow);//单据体供应商信息
                    var supplierId = "-1";
                    var supplierNumber = "";//供应商编码
                    if (supplier != null)
                    {
                        var supplierInfo = supplier as DynamicObject;
                        supplierId = supplierInfo["Id"].ToString();
                        supplierNumber = supplierInfo["Number"].ToString();
                    }
                    //采购订单数量不够入库重量，新增采购订单并下推采购入库单(判断是否有未下推的采购申请单)
                    var purReqEntryInfo = SqlHelper.GetPurReqEntryIdByMaterialId(this.Context, materialId, supplierId);
                    if (purReqEntryInfo.Count > 0)//采购申请单下推采购订单
                    {
                        return ReqPushOrder(purReqEntryInfo, inStockQty, sumUnInStockQty,
                                           purOrderInfo, stockNumber, entryIds, supplierNumber);
                    }
                    else//未找到采购申请单，新增采购订单
                    {
                        var saveResult1 = CreateNewOrder(purOrderInfo, inStockQty, sumUnInStockQty);
                        if (saveResult1.IsSuccessed)
                        {
                            var purchaseOrderEntryId = Convert.ToString(saveResult1.NeedReturnData[0]["FPOOrderEntry"][0]["FEntryID"]);//采购订单分录内码
                            entryIds += $",{purchaseOrderEntryId}";
                            var pushResult = OrderPushInStock(entryIds, stockNumber);
                            if (!pushResult.IsSuccessed)//如果下推失败，则删除新增的采购订单
                            {
                                //WebApiHelper.UnAudit(this.Context, "PUR_PurchaseOrder", saveResult1.Id.ToString());
                                //WebApiHelper.Delete(this.Context, "PUR_PurchaseOrder", saveResult1.Id.ToString());
                            }
                            return pushResult;
                        }
                        else
                        {
                            return saveResult1;
                        }
                    }
                }
            }
            else
            {
                _maxEntryId = SqlHelper.GetEntryIdByMaterialId(this.Context, materialId);

                var saveResult1 = CreateNewOrder(purOrderInfo, inStockQty, 0);
                if (saveResult1.IsSuccessed)
                {
                    var purchaseOrderEntryId = Convert.ToString(saveResult1.NeedReturnData[0]["FPOOrderEntry"][0]["FEntryID"]);//采购订单分录内码
                    return OrderPushInStock(purchaseOrderEntryId, stockNumber);
                }
                else
                {
                    return saveResult1;
                }
            }
        }

        /// <summary>
        /// 基础资料参数
        /// </summary>
        /// <param name="fieldKey">字段标识</param>
        /// <param name="numberKey">编码key</param>
        /// <param name="idKey">内码key</param>
        /// <param name="FieldValue">字段值</param>
        /// <param name="jsonKey">参数key</param>
        /// <param name="isNmber">是否传编码</param>
        /// <returns></returns>
        private JObject GetJObjectValue(string fieldKey, string numberKey, string idKey, string jsonKey = "FNumber", object FieldValue = null, bool isNmber = true)
        {
            var jObjectValue = new JObject();
            var baseData = FieldValue ?? this.View.Model.GetValue(fieldKey);
            if (baseData == null)
            {
                jObjectValue[jsonKey] = "";
                return jObjectValue;
            }
            var baseDataDynamic = baseData as DynamicObject;
            var number = baseDataDynamic[numberKey].ToString();
            var Id = baseDataDynamic[idKey].ToString();
            jObjectValue[jsonKey] = isNmber ? number : Id;
            return jObjectValue;
        }

        /// <summary>
        /// 其他字段参数
        /// </summary>
        /// <param name="fieldKey">字段标识</param>
        /// <param name="row">行号</param>
        /// <returns></returns>
        private string GetFieldJObjectValue(string fieldKey, object fieldValue = null)
        {
            var fieldValue1 = fieldValue ?? this.View.Model.GetValue(fieldKey);
            if (fieldValue1 == null)
            {
                return "";
            }
            return fieldValue1.ToString();
        }

        /// <summary>
        /// 日期返回(周日回退一天)
        /// </summary>
        /// <returns></returns>
        private string GetCurrentDate()
        {
            var currentDayOfWeek = DateTime.Now.DayOfWeek;//获取当前日期是星期几
            var currentDate = DateTime.Now.ToString("yyyy-MM-dd");//当前日期
            if (currentDayOfWeek == DayOfWeek.Sunday)//如果今天是星期日，则回退一天
            {
                currentDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
            }
            return currentDate;
        }

        /// <summary>
        /// 获取净重
        /// </summary>
        /// <returns></returns>
        private decimal GetRealQty()
        {
            var qty = Convert.ToDecimal(this.View.Model.GetValue("F_BOA_Qty"));//毛重
            var package = this.View.Model.GetValue("F_BOA_Package");//包装重量信息
            var packageQty = 0m;//包装重量
            if (package != null)
            {
                var packageId = (package as DynamicObject)["Id"];
                var packageInfo = SqlHelper.GetPackageInfoById(this.Context, packageId);
                if (packageInfo.Count > 0)
                {
                    foreach (var item in packageInfo)
                    {
                        var logicQty = Convert.ToDecimal(item["F_BOA_QTY"]);//用于比较的重量
                        var reduceQty = Convert.ToDecimal(item["F_BOA_REDUCEQTY"]);//包装重量
                        var logicFlag = item["F_BOA_LOGIC"].ToString();//逻辑符号
                        if (logicFlag == ">" && qty > logicQty)
                        {
                            packageQty = reduceQty;
                            break;
                        }
                        if (logicFlag == ">=" && qty >= logicQty)
                        {
                            packageQty = reduceQty;
                            break;
                        }
                        if (logicFlag == "=" && qty == logicQty)
                        {
                            packageQty = reduceQty;
                            break;
                        }
                        if (logicFlag == "<" && qty < logicQty)
                        {
                            packageQty = reduceQty;
                            break;
                        }
                        if (logicFlag == "<=" && qty <= logicQty)
                        {
                            packageQty = reduceQty;
                            break;
                        }
                    }
                }
            }
            _currentPackage = packageQty;
            return qty - packageQty;
        }

        /// <summary>
        /// 获取采购订单编码
        /// </summary>
        /// <returns></returns>
        private string GetOrderBillNo()
        {
            var billNo = SqlHelper.GetPurchaseOrder(this.Context);
            if (billNo != "")
            {
                var billNoInfo = billNo.Split('/');
                var dateInfo = billNoInfo[1];
                if (dateInfo == DateTime.Now.ToString("yyyy-MM-dd"))
                {
                    return $"备货/{dateInfo}/{Convert.ToInt32(billNoInfo[2]) + 1}";
                }
                else
                {
                    return $"备货/{DateTime.Now:yyyy-MM-dd}/1";
                }
            }
            else
            {
                return $"备货/{DateTime.Now:yyyy-MM-dd}/1";
            }
        }

        /// <summary>
        /// 采购订单下推采购入库单(部分下推)
        /// </summary>
        /// <param name="inStockQty">入库重量</param>
        /// <returns></returns>
        private ApiResult OrderPushInStock(string entryIds, decimal inStockQty, string stockNumber)
        {
            var pushResult = WebApiHelper.ExcutePushOperate(this.Context, "PUR_PurchaseOrder", entryIds, "PUR_PurchaseOrder-STK_InStock");
            if (pushResult.IsSuccessed)//下推成功
            {
                var stkInStockEntryIds = SqlHelper.GetSTKInStockBillById(this.Context, pushResult.Id);
                var head = new JObject();
                head["FID"] = pushResult.Id;
                head["FDate"] = GetCurrentDate();//入库日期
                var diffRataStr = (_diffRata * 100m).ToString("0.000");
                head["F_BOA_Note"] = $"单包磅差率：{diffRataStr}%";//备注
                head["F_BOA_SendQty"] = _sendQty;//单包送货重量
                head["F_BOA_RealQty"] = _weightQty;//单包过磅净重
                head["F_BOA_DiffQty"] = _weightQty - _sendQty;//单包磅差
                head["F_BOA_SendNo"] = sendGoodsBillNo;//送货单号
                head["F_BOA_Pieces"] = _pieces;//片数
                var entries = new JArray();
                //var sumQty = 0m;
                foreach (var item in stkInStockEntryIds)
                {
                    var orderEntryId = item["fsid"].ToString();//采购订单分录内码
                    var qty = _inStockDtos.First(t => t.EntryId == orderEntryId).Qty;

                    //var qty = Convert.ToDecimal(item["FREALQTY"]);
                    //var lastRowQty = inStockQty - sumQty;//最后一条分录数量
                    //sumQty += qty;//分录数量汇总
                    //if (sumQty > inStockQty)
                    //{
                    //    qty = lastRowQty;
                    //}
                    var entry = new JObject();
                    entry["FEntryID"] = item["FENTRYID"].ToString();
                    entry["FStockId"] = new JObject { ["FNumber"] = _stockNumber };
                    entry["FRealQty"] = qty;
                    entry["F_BOA_UniqueNo"] = uniqueNo;
                    if (!item["F_BGP_MTO"].IsNullOrEmptyOrWhiteSpace())
                    {
                        var mtoStr = item["F_BGP_MTO"].ToString();
                        if (mtoStr.StartsWith("备库"))
                        {
                            entry["FNote"] = $"{item["FNOTE"]}({item["F_BGP_MTO"]})";
                        }
                    }
                    entries.Add(entry);
                }
                head["FInStockEntry"] = entries;
                var saveResult = WebApiHelper.ExcuteSaveOperate(this.Context, head, "STK_InStock", false, true);
                if (!saveResult.IsSuccessed)//采购入库单保存/提交/审核失败
                {
                    //WebApiHelper.Delete(this.Context, "STK_InStock", pushResult.Id.ToString());
                    return saveResult;
                }
                inStockBillNo = saveResult.Number;
            }
            return pushResult;
        }

        /// <summary>
        /// 采购订单下推采购入库单(全部下推)
        /// </summary>
        /// <returns></returns>
        private ApiResult OrderPushInStock(string entryIds, string stockNumber)
        {
            var pushResult = WebApiHelper.ExcutePushOperate(this.Context, "PUR_PurchaseOrder", entryIds, "PUR_PurchaseOrder-STK_InStock");
            if (pushResult.IsSuccessed)//下推成功
            {
                var stkInStockEntryIds = SqlHelper.GetSTKInStockBillById(this.Context, pushResult.Id);
                var head = new JObject();
                head["FID"] = pushResult.Id;
                head["FDate"] = GetCurrentDate();//入库日期
                var diffRataStr = (_diffRata * 100m).ToString("0.000");
                head["F_BOA_Note"] = $"单包磅差率：{diffRataStr}%";//备注
                head["F_BOA_SendQty"] = _sendQty;//单包送货重量
                head["F_BOA_RealQty"] = _weightQty;//单包过磅净重
                head["F_BOA_DiffQty"] = _weightQty - _sendQty;//单包磅差
                head["F_BOA_SendNo"] = sendGoodsBillNo;//送货单号
                head["F_BOA_Pieces"] = _pieces;//片数
                var entries = new JArray();
                foreach (var item in stkInStockEntryIds)
                {
                    var entry = new JObject();
                    entry["FEntryID"] = item["FENTRYID"].ToString();
                    entry["FStockId"] = new JObject { ["FNumber"] = _stockNumber };
                    entry["F_BOA_UniqueNo"] = uniqueNo;
                    if (!item["F_BGP_MTO"].IsNullOrEmptyOrWhiteSpace())
                    {
                        var mtoStr = item["F_BGP_MTO"].ToString();
                        if (mtoStr.StartsWith("备库"))
                        {
                            entry["FNote"] = $"{item["FNOTE"]}({item["F_BGP_MTO"]})";
                        }
                    }
                    entries.Add(entry);
                }
                head["FInStockEntry"] = entries;
                var saveResult = WebApiHelper.ExcuteSaveOperate(this.Context, head, "STK_InStock", false, true);
                if (!saveResult.IsSuccessed)//采购入库单保存/提交/审核失败
                {
                    //WebApiHelper.Delete(this.Context, "STK_InStock", pushResult.Id.ToString());
                    return saveResult;
                }
                inStockBillNo = saveResult.Number;
            }
            return pushResult;
        }

        /// <summary>
        /// 新增采购订单
        /// </summary>
        /// <returns></returns>
        private ApiResult CreateNewOrder(PurOrderInfo purOrderInfo, decimal inStockQty, decimal sumUnInStockQty, decimal qty = 0)
        {
            //var lastInfo = purOrderInfo.OrderInfo.First();
            var orderInfo = SqlHelper.GetOrderInfoByBillInfo(this.Context, _maxEntryId);
            var orderHead = new JObject();
            orderHead["FBillTypeID"] = new JObject { ["FNUMBER"] = orderInfo["billTypeNumber"].ToString() };//单据类型
            orderHead["FBillNo"] = $"备库/{uniqueNo}";//单据编号
            orderHead["FSupplierId"] = new JObject { ["FNumber"] = orderInfo["supplierNumber"].ToString() };//供应商
            var orderEntries = new JArray();
            var orderEntry = new JObject();
            orderEntry["FMaterialId"] = new JObject { ["FNumber"] = orderInfo["materialNumber"].ToString() };//物料
            var purQty = qty > 0 ? qty : inStockQty - sumUnInStockQty;//采购数量
            orderEntry["FQty"] = purQty;//采购数量
            orderEntry["FTaxPrice"] = orderInfo["FTAXPRICE"].ToString();//含税单价
            orderEntry["FEntryTaxRate"] = orderInfo["FTAXRATE"].ToString();//税率%
            orderEntry["F_BGP_YSFYXM"] = new JObject { ["FNumber"] = orderInfo["expenseNumber"].ToString() };//预算费用项目
            orderEntry["F_BGP_YSBM"] = new JObject { ["FNumber"] = orderInfo["departNumber"].ToString() };//预算部门
            var linkNo = uniqueNo.Split('/')[0];
            var linkNo1 = linkNo.Split('-');
            var year = linkNo1[0].SubStr(2, 2);
            orderEntry["F_BGP_MTO"] = $"备库{year}{linkNo1[1]}";//计划跟踪号关联
            orderEntry["FEntryNote"] = orderInfo["FNOTE"].ToString();//备注
            orderEntries.Add(orderEntry);
            orderHead["FPOOrderEntry"] = orderEntries;

            ////付款计划
            var payPlans1 = new JArray();
            var payPlan1 = new JObject();
            payPlan1["FYFRATIO"] = 100;//应付比例(%)
            payPlan1["FYFAMOUNT"] = purQty * Convert.ToDecimal(orderInfo["FTAXPRICE"]);//应付金额
            payPlan1["FISPREPAYMENT"] = true;//是否预付
            payPlans1.Add(payPlan1);
            orderHead["FIinstallment"] = payPlans1;

            var needReturnFields1 = new JArray();
            needReturnFields1.Add("FIinstallment.FEntryID");
            var draftResult = WebApiHelper.ExcuteDraftOperate(this.Context, orderHead, "PUR_PurchaseOrder", needReturnFields1);

            var fIinstallmentEntryId = Convert.ToString(draftResult.NeedReturnData[0]["FIinstallment"][0]["FENTRYID"]);//

            //var fIinstallmentReturnData = draftResult.NeedReturnData[0] as Dictionary<string, object>;
            //var fIinstallmentInfo = (dynamic)fIinstallmentReturnData["FIinstallment"];
            //var fIinstallmentInfo1 = fIinstallmentInfo[0] as Dictionary<string, object>;
            //var fIinstallmentEntryId = fIinstallmentInfo1["FENTRYID"].ToString();

            var orderHead1 = new JObject();
            orderHead1["FID"] = draftResult.Id;
            var payPlans = new JArray();
            var payPlan = new JObject();
            payPlan["FENTRYID"] = fIinstallmentEntryId;//付款计划分录内码
            payPlan["FISPREPAYMENT"] = true;//是否预付
            payPlans.Add(payPlan);
            orderHead1["FIinstallment"] = payPlans;

            var needReturnFields = new JArray();
            needReturnFields.Add("FPOOrderEntry.FEntryID");

            var saveResult1 = WebApiHelper.ExcuteSaveOperate(this.Context, orderHead1, "PUR_PurchaseOrder", false, true, needReturnFields);
            //if (!saveResult1.IsSuccessed)
            //{
            //    WebApiHelper.Delete(this.Context, "PUR_PurchaseOrder", saveResult1.Id.ToString());
            //}
            return saveResult1;
        }

        /// <summary>
        /// 采购申请单下推采购订单(若数量不够下推，则新增采购订单)
        /// 采购订单下推采购入库单
        /// </summary>
        /// <param name="inStockQty">入库重量</param>
        /// <param name="sumUnInStockQty">已有的采购订单未入库重量</param>
        /// <param name="entryIdstr">采购订单分录内码</param>
        /// <param name="supplierNumber">供应商编码</param>
        /// <returns></returns>
        private ApiResult ReqPushOrder(DynamicObjectCollection purReqEntryInfo, decimal inStockQty, decimal sumUnInStockQty,
                  PurOrderInfo purOrderInfo, string stockNumber, string entryIdstr, string supplierNumber)
        {
            //var purReqEntryId = purReqEntryInfo["FENTRYID"].ToString();//采购申请单分录

            var reqInfoList = ReqQty(purReqEntryInfo, inStockQty, sumUnInStockQty);//采购申请单分录

            var purReqEntryIdStr = string.Join(",", reqInfoList.Select(t => t.EntryId));//采购申请单分录内码

            var purReqResult = WebApiHelper.ExcutePushOperate(this.Context, "PUR_Requisition", purReqEntryIdStr, "PUR_Requisition-PUR_PurchaseOrder");
            if (purReqResult.IsSuccessed)
            {
                //var lastInfo = purOrderInfo.OrderInfo.Last();
                var orderInfo = SqlHelper.GetOrderInfoByBillInfo(this.Context, _maxEntryId);

                //付款计划分录数据
                var orderInfoPayPlan = SqlHelper.GetPayPlanOrder(this.Context, purReqResult.Id);//
                var payPlanAmount = 0m;//付款计划中应付金额

                //var purReqQty = Convert.ToDecimal(purReqEntryInfo["qty"]);//采购申请数量
                var purReqQty = reqInfoList.Sum(t => t.Qty);//采购申请数量
                var purCurrentQty = inStockQty - sumUnInStockQty - purReqQty;//实际采购订单数量差额

                var purchaseOrderId = purReqResult.Id;//采购订单内码
                var purchaseOrderEntryIds = SqlHelper.GetPurchaseOrderEntryId(this.Context, purchaseOrderId);

                var purchaseOrderEntryId = string.Join(",", purchaseOrderEntryIds.Select(t => t["fentryId"]));//采购订单分录内码

                var purchaseOrderHead = new JObject();
                purchaseOrderHead["FID"] = purchaseOrderId;
                purchaseOrderHead["FDate"] = DateTime.Now.ToString("yyyy-MM-dd");//采购日期
                purchaseOrderHead["FSupplierId"] = new JObject { ["FNumber"] = supplierNumber };//供应商

                var purchaseOrderEntries = new JArray();

                foreach (var purItem in purchaseOrderEntryIds)
                {
                    var purchaseOrderEntry = new JObject();
                    var purchaseOrderEntryIditem = purItem["fentryId"].ToString();//采购订单分录内码
                    var srcEntryId = purItem["FSID"].ToString();//采购申请单分录内码
                    var qtyInfo = reqInfoList.FirstOrDefault(t => t.EntryId == srcEntryId);
                    if (qtyInfo == null)
                    {
                        continue;
                    }
                    var qty = qtyInfo.Qty;

                    purchaseOrderEntry["FEntryID"] = purchaseOrderEntryIditem;
                    purchaseOrderEntry["FQty"] = qty;//采购数量
                    purchaseOrderEntry["FTaxPrice"] = orderInfo["FTAXPRICE"].ToString();//含税单价
                    purchaseOrderEntry["FEntryTaxRate"] = orderInfo["FTAXRATE"].ToString();//税率%
                    purchaseOrderEntry["FDeliveryDate"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");//交货日期
                    purchaseOrderEntry["FEntryNote"] = orderInfo["FNOTE"].ToString();//备注
                    purchaseOrderEntries.Add(purchaseOrderEntry);

                    payPlanAmount += qty * Convert.ToDecimal(orderInfo["FTAXPRICE"]);
                }
                purchaseOrderHead["FPOOrderEntry"] = purchaseOrderEntries;

                //付款计划
                if (orderInfoPayPlan != null)
                {
                    var fIinstallmentEntryId = orderInfoPayPlan["FENTRYID"].ToString();//
                    var payPlans = new JArray();
                    var payPlan = new JObject();
                    payPlan["FENTRYID"] = fIinstallmentEntryId;//付款计划分录内码
                    payPlan["FISPREPAYMENT"] = true;//是否预付
                    payPlans.Add(payPlan);
                    purchaseOrderHead["FIinstallment"] = payPlans;
                }
                else
                {
                    var payPlans1 = new JArray();
                    var payPlan1 = new JObject();
                    payPlan1["FYFRATIO"] = 100;//应付比例(%)
                    payPlan1["FYFAMOUNT"] = payPlanAmount;//应付金额
                    payPlan1["FISPREPAYMENT"] = true;//是否预付
                    payPlans1.Add(payPlan1);
                    purchaseOrderHead["FIinstallment"] = payPlans1;
                }

                var saveResult2 = WebApiHelper.ExcuteSaveOperate(this.Context, purchaseOrderHead, "PUR_PurchaseOrder", false, true);
                if (!saveResult2.IsSuccessed)//采购申请单下推采购订单保存失败，删除保存失败的采购订单，并新增备库采购订单
                {
                    //删除保存失败的采购订单
                    var deresult = WebApiHelper.Delete(this.Context, "PUR_PurchaseOrder", purReqResult.Id.ToString());
                    //新增备库采购订单
                    var saveResult1 = CreateNewOrder(purOrderInfo, inStockQty, sumUnInStockQty);
                    if (!saveResult1.IsSuccessed)
                    {
                        return saveResult1;
                    }
                    string purchaseOrderEntryId1 = Convert.ToString(saveResult1.NeedReturnData[0]["FPOOrderEntry"][0]["FEntryID"]);
                    var purchaseOrderEntryId2 = $"{entryIdstr},{purchaseOrderEntryId1}";
                    var result = OrderPushInStock(purchaseOrderEntryId2, stockNumber);
                    if (!result.IsSuccessed)
                    {
                        return result;
                    }
                }
                else
                {
                    if (purCurrentQty > 0)//采购申请数量不够采购订单数量下推，新增采购订单
                    {
                        var saveResult1 = CreateNewOrder(purOrderInfo, inStockQty, sumUnInStockQty, purCurrentQty);
                        if (!saveResult1.IsSuccessed)
                        {
                            //WebApiHelper.UnAudit(this.Context, "PUR_PurchaseOrder", purReqResult.Id.ToString());
                            //WebApiHelper.Delete(this.Context, "PUR_PurchaseOrder", purReqResult.Id.ToString());
                            return saveResult1;
                        }
                        string purchaseOrderEntryId1 = Convert.ToString(saveResult1.NeedReturnData[0]["FPOOrderEntry"][0]["FEntryID"]);
                        var purchaseOrderEntryId2 = $"{entryIdstr},{purchaseOrderEntryId},{purchaseOrderEntryId1}";
                        var result = OrderPushInStock(purchaseOrderEntryId2, stockNumber);
                        if (!result.IsSuccessed)
                        {
                            //WebApiHelper.UnAudit(this.Context, "PUR_PurchaseOrder", purReqResult.Id.ToString());
                            //WebApiHelper.Delete(this.Context, "PUR_PurchaseOrder", purReqResult.Id.ToString());
                            //WebApiHelper.UnAudit(this.Context, "PUR_PurchaseOrder", saveResult1.Id.ToString());
                            //WebApiHelper.Delete(this.Context, "PUR_PurchaseOrder", saveResult1.Id.ToString());
                            return result;
                        }
                    }
                    else
                    {
                        var entryIdStr01 = $"{entryIdstr},{purchaseOrderEntryId}";
                        var result0011 = OrderPushInStock(entryIdStr01, stockNumber);
                        return result0011;
                    }
                }
            }
            return purReqResult;
        }

        /// <summary>
        /// 填充单据体信息
        /// </summary>
        private void FillInEntry(DynamicObjectCollection dataInfo)
        {
            var subEntityRowCount = this.View.Model.GetEntryRowCount("F_BOA_Entity");
            if (subEntityRowCount > 0)//清除已有分录
            {
                for (var i = 0; i < subEntityRowCount; i++)
                {
                    this.View.Model.DeleteEntryRow("F_BOA_Entity", 0);
                }
            }
            var index = 0;
            foreach (var item in dataInfo)
            {
                this.View.Model.InsertEntryRow("F_BOA_Entity", index);
                this.View.Model.SetValue("F_BOA_Model", item["F_BOA_MATERIALID"], index);
                this.View.Model.SetValue("F_BOA_SendQty", item["F_BOA_QTY"], index);
                this.View.InvokeFieldUpdateService("F_BOA_Model", index);
                index++;
            }
        }

        /// <summary>
        /// 获取串口数据
        /// </summary>
        private void ExcuteCustomerCtl()
        {
            portInfo = portInfo ?? SqlHelper.GetPort(this.Context);
            var args = new object[1];
            args[0] = portInfo[0]["F_BOA_PORT"];
            this.View.GetControl("F_BOA_CustomCtl").InvokeControlMethod("DoCustomMethod", "GetData", args);
        }

        /// <summary>
        /// 获取串口数据(新)
        /// </summary>
        private void ExcuteCustomerCtlNew()
        {
            var args = new object[1];
            args[0] = null;
            this.View.GetControl("F_BOA_CustomCtl").InvokeControlMethod("DoCustomMethod", "GetDataNew", args);
        }

        /// <summary>
        /// 打开串口
        /// </summary>
        private void OpenCom()
        {
            portInfo = portInfo ?? SqlHelper.GetPort(this.Context);
            var args = new object[1];
            args[0] = portInfo[0]["F_BOA_PORT"];
            this.View.GetControl("F_BOA_CustomCtl").InvokeControlMethod("DoCustomMethod", "OpenCom", args);
        }

        /// <summary>
        /// 关闭串口
        /// </summary>
        private void CloseCom()
        {
            var args = new object[1];
            args[0] = null;
            this.View.GetControl("F_BOA_CustomCtl").InvokeControlMethod("DoCustomMethod", "CloseCom", args);
        }

        /// <summary>
        /// 获取缺失的入库卷号
        /// </summary>
        /// <param name="noList"></param>
        /// <returns></returns>
        private List<int> GetDefectNo(List<int> noList)
        {
            int a = noList.OrderBy(x => x).First();
            int b = noList.OrderBy(x => x).Last();
            List<int> myList2 = Enumerable.Range(a, b - a + 1).ToList();
            List<int> remaining = myList2.Except(noList).ToList();
            return remaining;
        }

        /// <summary>
        /// 成生条码主档
        /// </summary>
        private string CreateBarCodeMainFile(DynamicObject row)
        {
            var head = new JObject();
            var materialInfo = row["F_BOA_Model"] as DynamicObject;
            var materialNumber = materialInfo["F_BOA_Number"].ToString();
            var barCode = $"{materialNumber}*{inStockBillNo}*";
            head["FNumber"] = barCode;//单据编码
            head["FBarCode"] = barCode;//条形码
            head["FBarCodeRule"] = new JObject { ["FNUMBER"] = "TMGZ01_SYS" };//条码规则 默认 物料采购批次条码
            head["FMaterialId"] = new JObject { ["FNumber"] = materialNumber };//物料编码
            head["FBillCode"] = inStockBillNo;//单据编号
            head["FDetailBillcode"] = inStockBillNo;//明细单据编号
            head["FBarCodeType"] = "LotBarCode";//条码类型 默认 批号条码
            head["FCreateOrgId"] = new JObject { ["FORGID"] = this.Context.CurrentOrganizationInfo.ID };//创建组织
            var result = WebApiHelper.ExcuteSaveOperate(this.Context, head, "BD_BarCodeMainFile");
            return barCode;
        }

        /// <summary>
        /// 采购申请单判断每条分录的对应数量
        /// </summary>
        /// <param name="purReqEntryInfo">采购申请单信息</param>
        /// <param name="inStockQty">称重重量</param>
        /// <param name="sumUnInStockQty">采购订单重量</param>
        /// <returns></returns>
        private List<ReqQtyDto> ReqQty(DynamicObjectCollection purReqEntryInfo,
            decimal inStockQty, decimal sumUnInStockQty)
        {
            var reqQtyDtos = new List<ReqQtyDto>();
            var purCurrentQty = inStockQty - sumUnInStockQty;//采购申请单需要下推采购订单的总数量
            foreach (var item in purReqEntryInfo)//遍历采购申请单，计算需要下推的数量
            {
                var qty = Convert.ToDecimal(item["qty"]);//采购申请单未下推采购订单数量
                var entryId = item["FENTRYID"].ToString();//采购申请单分录内码
                if (qty < purCurrentQty)
                {
                    var reqQtyDto = new ReqQtyDto();
                    reqQtyDto.EntryId = entryId;
                    reqQtyDto.Qty = qty;
                    reqQtyDtos.Add(reqQtyDto);
                    purCurrentQty -= qty;
                }
                else
                {
                    var reqQtyDto = new ReqQtyDto();
                    reqQtyDto.EntryId = entryId;
                    reqQtyDto.Qty = purCurrentQty;
                    reqQtyDtos.Add(reqQtyDto);
                    break;
                }
            }
            return reqQtyDtos;
        }

        #region 整车过磅

        /// <summary>
        /// 整车过磅
        /// </summary>
        private void Weight()
        {
            var allQty = Convert.ToDecimal(this.View.Model.GetValue("F_BOA_AllQty"));//整车重量
            if (allQty <= 0)
            {
                this.View.ShowWarnningMessage("整车重量未填写，请填写！");
                return;
            }

            entity = entity ?? this.View.BillBusinessInfo.GetEntity("F_BOA_Entity");
            var rows = this.View.Model.GetEntityDataObject(entity);
            if (rows.Count == 0)
            {
                this.View.ShowWarnningMessage("请录入收料信息行！");
                return;
            }
            if (rows.Count > 0)
            {
                var rowsWhere = rows.Where(t => Convert.ToInt64(t["F_BOA_Model_Id"]) == 0
                              || Convert.ToInt64(t["F_BOA_SendQty"]) == 0).ToList();
                if (rowsWhere.Count > 0)
                {
                    this.View.ShowWarnningMessage("收料信息行中存在物料规格型号或送货重量未填写，请填写！");
                    return;
                }
            }

            var stockInfo = this.View.Model.GetValue("F_BOA_Stock");
            if (stockInfo == null)
            {
                this.View.ShowWarnningMessage("请先录入仓库！");
                return;
            }
            var printTemp = this.View.Model.GetValue("F_BOA_Template");
            if (printTemp == null)
            {
                this.View.ShowWarnningMessage("请选择打印模板！");
                return;
            }
            var supplierInfo = this.View.Model.GetValue("F_BOA_Supplier");
            if (supplierInfo == null)
            {
                this.View.ShowWarnningMessage("请先录入供应商！");
                return;
            }
            var sendNo = this.View.Model.GetValue("F_BOA_GoodsBill");
            if (sendNo.IsNullOrEmptyOrWhiteSpace())
            {
                this.View.ShowWarnningMessage("送货单号未填写！");
                return;
            }


            //根据送货重量，从小到大 排序
            var rowsOrder = rows.OrderBy(t => Convert.ToInt64(t["F_BOA_SendQty"])).ToList();
            var rowCount = rows.Count;//分录行总数量
            var rowIndexCount = 0;//已计算的行数量
            var msg = string.Empty;
            foreach (var row in rowsOrder)
            {
                rowIndexCount++;
                var seq = Convert.ToInt32(row["Seq"]);//当前行
                var sendQty = Convert.ToInt64(row["F_BOA_SendQty"]);//送货重量

                var isInStock = GetInStockQty(rowCount, rowIndexCount, sendQty, ref allQty
                                              , out decimal inStockQty, out bool isDiff);

                this.View.Model.SetValue("F_BOA_WeighQty", inStockQty, seq - 1);//入库重量(过磅重量)
                this.View.Model.SetValue("F_BOA_IsIn", isDiff, seq - 1); //过磅重量是否超出送货重量0.3 % 范围
                if (!isInStock)
                {
                    continue;
                }
                SetUniqueNo();
                //var iUniqueNo = uniqueNo;
                InStockBTClick(seq - 1, inStockQty, stockInfo, printTemp, "");
            }
        }

        /// <summary>
        /// 获取入库重量
        /// </summary>
        /// <param name="rowCount">分录行总数量</param>
        /// <param name="rowIndexCount">已计算的行数量</param>
        /// <param name="sendQty">送货重量</param>
        /// <param name="allQty">整车重量</param>
        /// <param name="inStockQty">入库重量</param>
        /// <param name="isDiff">过磅重量 是否 在送货重量0.3%范围 ,true = 在，false = 不在</param>
        /// <returns>是否可以入库，true = 可以，false = 不可以</returns>
        private bool GetInStockQty(int rowCount, int rowIndexCount, decimal sendQty
                                      , ref decimal allQty, out decimal inStockQty
                                      , out bool isDiff)
        {
            if (allQty == 0)//待分配的整车重量为0，则 入库重量 为 0
            {
                inStockQty = 0;
                isDiff = false;
                return false;
            }
            if (rowIndexCount != rowCount && allQty >= sendQty)
            {
                inStockQty = sendQty;//入库重量 默认 等于送货重量
                allQty -= sendQty;
                isDiff = true;
                return true;
            }
            //待分配的整车重量小于当前分录的送货重量，则 入库重量 = 待分配的整车重量
            else if (rowIndexCount != rowCount && allQty < sendQty)
            {
                inStockQty = allQty;
                allQty = 0;
                isDiff = IsDiff3(inStockQty, sendQty);
                return true;
            }
            else if (rowIndexCount == rowCount)
            {
                inStockQty = allQty;
                isDiff = IsDiff3(inStockQty, sendQty);
                return true;
            }
            inStockQty = 0;
            isDiff = false;
            return false;
        }

        /// <summary>
        /// 判断 过磅重量 是否 超出送货重量0.3%范围
        /// </summary>
        /// <param name="realQty">入库重量(过磅重量)</param>
        /// <param name="sendQty1">送货重量</param>
        /// <returns>true = 0.3%范围内，false = 0.3%范围外</returns>
        private bool IsDiff3(decimal realQty, decimal sendQty1)
        {
            var diff = (realQty - sendQty1) / sendQty1;
            if (diff <= 0.003m && diff >= -0.003m)
            {
                return true;
            }
            return false;
        }
        #endregion

        #region 整车过磅完毕

        /// <summary>
        /// 创建整车标示下拉列表字段值
        /// </summary>
        /// <param name="enumValue">单据体-送货单号</param>
        private void CreateEnumValue(string enumValue)
        {
            var result = _zcbs.FirstOrDefault(t => t == enumValue);
            if (result != null)
            {
                return;
            }
            _zcbs.Add(enumValue);
            var emunList = new List<EnumItem>();
            var index = 0;
            foreach (var item in _zcbs)
            {
                index++;
                var emunItem = new EnumItem
                {
                    EnumId = item,
                    Value = item,
                    Caption = new LocaleValue(item),
                    Seq = index
                };
                emunList.Add(emunItem);
            }
            _combo = _combo ?? this.View.GetControl<ComboFieldEditor>("F_BOA_ZCBS");
            _combo.SetComboItems(emunList);
            this.View.UpdateView("F_BOA_ZCBS");
        }

        /// <summary>
        /// 整车过磅按钮点击
        /// </summary>
        private void BtnWeightEnd()
        {
            if (!CheckChoose())
            {
                this.View.ShowWarnningMessage("请先选择整车磅差，再点击该按钮！");
                return;
            }
            var sendNo = GetSendNo(out bool isEmpty);
            if (isEmpty)
            {
                this.View.ShowWarnningMessage("未选择整车标示！");
                return;
            }
            CheckEntryData(sendNo);
        }

        /// <summary>
        /// 判断是否选择了整车磅差复选框
        /// 1 = 单包磅差，2 = 整车磅差
        /// </summary>
        /// <returns>true = 整车磅差，false = 单包磅差</returns>
        private bool CheckChoose()
        {
            var typeValue = this.View.Model.GetValue("F_BOA_Type").ToString();
            return typeValue == "2";
        }

        /// <summary>
        /// 判断是否选择了整车标示(送货单号)
        /// </summary>
        /// <param name="isEmpty">整车标示是否为空，true = 空，false = 非空</param>
        /// <returns>选择的整车标示值</returns>
        private string GetSendNo(out bool isEmpty)
        {
            var sendNo = this.View.Model.GetValue("F_BOA_ZCBS");
            if (sendNo.IsNullOrEmptyOrWhiteSpace())
            {
                isEmpty = true;
                return "";
            }
            isEmpty = false;
            return sendNo.ToString();
        }

        /// <summary>
        /// 判断整车磅差是否超过千分之三
        /// </summary>
        /// <param name="sendNo">送货单号</param>
        private void CheckEntryData(string sendNo)
        {
            entity = entity ?? this.View.BillBusinessInfo.GetEntity("F_BOA_Entity");
            var entryData = this.View.Model.GetEntityDataObject(entity);
            if (entryData == null)
            {
                this.View.ShowMessage("未录入送货信息，请录入！");
                return;
            }
            var entryDataW2 = entryData.Where(t => t["F_BOA_SendNo"].IsNullOrEmptyOrWhiteSpace()).ToList();
            if (entryDataW2.Count > 0)
            {
                this.View.ShowMessage("送货单号未填写，请填写！");
                return;
            }
            var entryDataW1 = entryData
                            .Where(t => t["F_BOA_SendNo"].ToString().Trim() == sendNo
                              && Convert.ToDecimal(t["F_BOA_WeighQty"]) == 0).ToList();
            if (entryDataW1.Count > 0)
            {
                this.View.ShowMessage("存在未过磅的信息，请过磅！");
                return;
            }
            var entryDataW = entryData
                            .Where(t => t["F_BOA_SendNo"].ToString().Trim() == sendNo
                              && Convert.ToDecimal(t["F_BOA_WeighQty"]) > 0
                              && Convert.ToDecimal(t["F_BOA_SendQty"]) > 0).ToList();
            if (entryDataW.Count == 0)
            {
                this.View.ShowMessage("不存在过磅信息，请重新选择送货单号！");
                return;
            }
            var sendQtySum = entryDataW.Sum(t => Convert.ToDecimal(t["F_BOA_SendQty"]));//整车送货重量
            var weighQtySum = entryDataW.Sum(t => Convert.ToDecimal(t["F_BOA_WeighQty"]));//整车过磅重量
            var diffSum = weighQtySum - sendQtySum;//整车磅差
            var sumRate = diffSum / sendQtySum;//整车磅差率
            if (sumRate <= 0.003m && sumRate >= -0.003m)
            {
                var diffRataStr = (sumRate * 100m).ToString("0.000");
                var remark = $"整车磅差率:{diffRataStr}%;磅差:{diffSum:0.00}kg";
                SqlHelper.UpdateInstock(this.Context, sendNo, remark);
                this.View.ShowMessage("整车磅差率未超千分之三！");
                return;
            }
            ResetInstockInfo(entryDataW, diffSum, sumRate);
        }

        /// <summary>
        /// 整车磅差率超千分之三，重新生成入库数据
        /// </summary>
        /// <param name="inStockInfoList">称重数据</param>
        /// <param name="diffSum">整车磅差</param>
        /// <param name="sumRate">整车磅差率</param>
        private void ResetInstockInfo(List<DynamicObject> inStockInfoList, decimal diffSum, decimal sumRate)
        {
            this.View.ShowProcessForm(formResult => { }, false, "整车磅差率超千分之三！重新生成入库数据");
            MainWorker.QuequeTask(this.View.Context, () =>
            {
                this.View.Session["ProcessRateValue"] = 1;
                var msg = "";
                try
                {
                    var jhList = inStockInfoList.Select(t => t["F_BOA_UniqueNo"].ToString()).ToList();//入库卷号
                    var jhListStr = string.Join("','", jhList);
                    var deResult = DeleteBill(jhListStr, out Dictionary<string, string> jhrks);
                    msg += $"{deResult}\n";
                    this.View.Session["ProcessRateValue"] = 10;

                    var stockInfo = this.View.Model.GetValue("F_BOA_Stock");
                    var stockNumer = (stockInfo as DynamicObject)["Number"].ToString();
                    var weightHelper = new WeightHelper(this.Context, stockNumer);
                    var i = 1;
                    var count = inStockInfoList.Count;
                    foreach (var item in inStockInfoList)
                    {
                        var uniqueNo = item["F_BOA_UniqueNo"].ToString();//入库卷号(每包唯一号)
                        var oldInstockNo = "";//旧的采购入库单号
                        if (jhrks.ContainsKey(uniqueNo))
                        {
                            oldInstockNo = jhrks[uniqueNo];//旧的采购入库单号
                        }

                        var createMsg = weightHelper.ResetInStockInfo(item, diffSum, sumRate, oldInstockNo);
                        msg += $"{createMsg}\n";

                        var rate = Convert.ToInt32(Math.Floor((decimal)i / count * 100m));
                        if (rate <= 10)
                        {
                            rate = 10;
                        }
                        if (rate >= 100)
                        {
                            rate = 99;
                        }
                        this.View.Session["ProcessRateValue"] = rate;
                        i++;
                    }
                }
                catch (Exception ex)
                {
                    msg += $"{ex.Message}\n";
                }
                finally
                {
                    this.View.ShowWarnningMessage($"整车磅差率超千分之三！已重新生成入库数据。\n{msg}");
                    this.View.Session["ProcessRateValue"] = 100;
                    this.View.SendDynamicFormAction(this.View);
                }
            }, null);
        }

        /// <summary>
        /// 删除入库相关单据
        /// </summary>
        /// <param name="uniqueNo">入库卷号</param>
        /// <param name="jhrks">入库卷号 和 对应的采购入库单 信息</param>
        /// <returns></returns>
        private string DeleteBill(string uniqueNo, out Dictionary<string, string> jhrks)
        {
            var result = SqlHelper.InstockBillQuery(this.Context, uniqueNo);
            if (result.Count == 0)
            {
                jhrks = new Dictionary<string, string>();
                return "";
            }
            var id = result.Select(t => t["fid"].ToString()).Distinct().ToList();//采购入库单内码
            var purId = result.Where(t => Convert.ToInt64(t["purOrderId"]) > 0)
                              .Select(t => t["purOrderId"].ToString())
                              .Distinct().ToList();//备库采购订单内码

            jhrks = new Dictionary<string, string>();
            var resultW = result.GroupBy(t => new { jh = t["F_BOA_UNIQUENO"].ToString(), rkdh = t["FBILLNO"].ToString() })
                                .Select(t => new { t.Key.jh, t.Key.rkdh }).ToList();
            foreach (var item in resultW)
            {
                jhrks.Add(item.jh, item.rkdh);
            }

            //反审核 并 删除 采购入库单 数据
            var inStockIdStr = string.Join(",", id);
            var msg1 = WebApiHelper.UnAudit(this.Context, "STK_InStock", inStockIdStr);
            var msg2 = WebApiHelper.Delete(this.Context, "STK_InStock", inStockIdStr);
            //反审核 并 删除 备库采购订单 数据
            string msg3 = "";
            string msg4 = "";
            if (purId.Count > 0)
            {
                var purOrderIdStr = string.Join(",", purId);
                msg3 = WebApiHelper.UnAudit(this.Context, "PUR_PurchaseOrder", purOrderIdStr).ErrorResult;
                msg4 = WebApiHelper.Delete(this.Context, "PUR_PurchaseOrder", purOrderIdStr).ErrorResult;
            }
            return $"采购入库单反审核:{msg1.ErrorResult},采购入库单删除:{msg2.ErrorResult},采购订单反审核:{msg3},采购订单删除:{msg4}";
        }
        #endregion
    }
}
