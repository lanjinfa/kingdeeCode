using BOA.FQGT.ProfitReport.BusinessPlugIn.Common;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using System;
using System.ComponentModel;
using System.Linq;

namespace BOA.FQGT.ProfitReport.BusinessPlugIn
{
    [HotUpdate]
    [Description("成本明细表")]
    public class DetailFormPlugin : AbstractDynamicFormPlugIn
    {
        public override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ShowFilterForm();
        }

        public override void BarItemClick(BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.BarItemKey.Equals("BOA_filter"))
            {
                ShowFilterForm();
            }
        }

        /// <summary>
        /// 显示过滤框
        /// </summary>
        private void ShowFilterForm()
        {
            this.View.ShowForm(CommonHelper.GetFilterForm(1), delegate (FormResult filterResult)
            {
                var condition = CommonHelper.GetFilterCondition(filterResult);
                if (condition.Filter != null)
                {
                    ShowEntryData(new SqlHelper(this.Context, condition.Filter).GetCostDataInfo(condition.BillType));
                }
            });
        }

        /// <summary>
        /// 将获取到的数据填充到单据体上
        /// </summary>
        /// <param name="data">获取到的数据</param>
        private void ShowEntryData(DynamicObjectCollection entryInfo)
        {
            //清空表体
            int rowcount = base.View.Model.GetEntryRowCount("F_BOA_Entity");
            if (rowcount > 0)
            {
                this.View.Model.DeleteEntryData("F_BOA_Entity");
            }
            //加载数据
            if (entryInfo.Count == 0)
            {
                this.View.ShowMessage("未查询到数据，请重新选择过滤条件！");
            }
            var entity = this.View.BillBusinessInfo.GetEntity("F_BOA_Entity");
            var rows = this.Model.GetEntityDataObject(entity);
            for (var i = 0; i < entryInfo.Count; i++)
            {
                var row = new DynamicObject(entity.DynamicObjectType);
                entity.SeqDynamicProperty.SetValue(row, i + 1);
                row["F_BOA_BillName"] = entryInfo[i]["typename"];
                row["F_BOA_Customer"] = entryInfo[i]["cusName"];
                row["F_BOA_BillNo"] = entryInfo[i]["billNo"];
                row["F_BOA_Date"] = entryInfo[i]["fdate"].ToString().Substring(0, 10);
                row["F_BOA_RowNumber"] = entryInfo[i]["fseq"];
                row["F_BOA_MaterialName"] = entryInfo[i]["materialName"];
                row["F_BOA_Department"] = entryInfo[i]["cateName"];
                row["F_BOA_Desribe"] = entryInfo[i]["fspecification"];
                row["F_BOA_Model"] = entryInfo[i]["fmodel"];
                row["F_BOA_FGModel"] = entryInfo[i]["F_BOA_FGModel"];
                var price = string.Format("{0:N2}", entryInfo[i]["price"]);
                var qty = string.Format("{0:N3}", entryInfo[i]["costqty"]);//qty 实发数量
                var costQty = string.Format("{0:N3}", entryInfo[i]["qty"]);//costqty 应发数量
                row["F_BOA_Price"] = price;
                row["F_BOA_Qty"] = qty;
                row["F_BOA_Amount"] = string.Format("{0:N2}", Convert.ToDecimal(price) * Convert.ToDecimal(qty));
                row["F_BOA_Flot"] = entryInfo[i]["flotnumber"];
                var costPrice = string.Format("{0:N2}", entryInfo[i]["costprice"]);
                row["F_BOA_PurPrice"] = costPrice;
                row["F_BOA_PurQty"] = costQty;
                row["F_BOA_PurCost"] = string.Format("{0:N2}", Convert.ToDecimal(costPrice) * Convert.ToDecimal(costQty));
                row["F_BOA_Remark"] = entryInfo[i]["FCOMMENT"];
                row["F_BOA_PurMaterialNum"] = entryInfo[i]["materialnumber"];
                rows.Add(row);
            }
            //汇总栏
            var countRow = new DynamicObject(entity.DynamicObjectType);
            entity.SeqDynamicProperty.SetValue(countRow, entryInfo.Count + 1);
            countRow["F_BOA_BillName"] = "汇总";
            countRow["F_BOA_Price"] = rows.Select(t => Convert.ToDecimal(t["F_BOA_Price"])).Sum();
            countRow["F_BOA_Qty"] = rows.Select(t => Convert.ToDecimal(t["F_BOA_Qty"])).Sum();
            countRow["F_BOA_Amount"] = rows.Select(t => Convert.ToDecimal(t["F_BOA_Amount"])).Sum();
            countRow["F_BOA_PurPrice"] = rows.Select(t => Convert.ToDecimal(t["F_BOA_PurPrice"])).Sum();
            countRow["F_BOA_PurQty"] = rows.Select(t => Convert.ToDecimal(t["F_BOA_PurQty"])).Sum();
            countRow["F_BOA_PurCost"] = rows.Select(t => Convert.ToDecimal(t["F_BOA_PurCost"])).Sum();
            rows.Add(countRow);
            this.View.UpdateView("F_BOA_Entity");
        }
    }
}
