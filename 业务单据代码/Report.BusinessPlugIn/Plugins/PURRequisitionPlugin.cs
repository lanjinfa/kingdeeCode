using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace BOA.DJJX.Report.BusinessPlugIn.Plugins
{
    [HotUpdate]
    [Description("采购申请单")]
    public class PURRequisitionPlugin : AbstractBillPlugIn
    {
        public override void EntityRowDoubleClick(EntityRowClickEventArgs e)
        {
            base.EntityRowDoubleClick(e);
            if (e.Key.EqualsIgnoreCase("FEntity"))
            {
                OpenTreasureChestForm(e.Row);
            }
        }

        public override void EntryBarItemClick(BarItemClickEventArgs e)
        {
            base.EntryBarItemClick(e);
            if (e.BarItemKey.EqualsIgnoreCase("BOA_Treasure"))
            {
                var row = this.View.Model.GetEntryCurrentRowIndex("FEntity");
                OpenTreasureChestForm(row);
            }
        }

        /// <summary>
        /// 跳转百宝箱
        /// </summary>
        private void OpenTreasureChestForm(int row)
        {
            var material = this.View.Model.GetValue("FMaterialId", row);//物料
            if (material == null)
            {
                this.View.ShowErrMessage("物料不能为空！");
                return;
            }
            var orgInfo = this.View.Model.GetValue("FApplicationOrgId");//
            if (orgInfo == null)
            {
                this.View.ShowErrMessage("申请组织不能为空！");
                return;
            }
            var orgId = (orgInfo as DynamicObject)["Id"];
            var supplier = this.View.Model.GetValue("FSuggestSupplierId", row);//建议供应商
            var supplierId = supplier != null ? (supplier as DynamicObject)["Id"] : 0;
            var mateiralId = (material as DynamicObject)["Id"];
            var materialNumber = (material as DynamicObject)["Number"];
            var form = new BillShowParameter();
            form.FormId = "BOA_TreasureChest";
            form.OpenStyle.ShowType = ShowType.Modal;
            form.Status = OperationStatus.VIEW;
            form.CustomComplexParams.Add("supplierId", supplierId);
            form.CustomComplexParams.Add("materialId", mateiralId);
            form.CustomComplexParams.Add("materialNumber", materialNumber);
            form.CustomComplexParams.Add("orgId", orgId);
            this.View.ShowForm(form);
        }
    }
}
