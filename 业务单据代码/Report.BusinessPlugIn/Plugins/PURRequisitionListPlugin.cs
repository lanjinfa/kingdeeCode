using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.List.PlugIn;
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
    [Description("采购申请单_列表_百宝箱")]
    public class PURRequisitionListPlugin : AbstractListPlugIn
    {
        public override void BarItemClick(BarItemClickEventArgs e)
        {
            base.BarItemClick(e);
            if (e.BarItemKey.EqualsIgnoreCase("BOA_TreasureL"))
            {
                var selectList = this.ListView.SelectedRowsInfo;
                if (selectList.Count == 0)
                {
                    this.View.ShowWarnningMessage("未选择数据行！");
                    return;
                }
                if (selectList.Count > 1)
                {
                    this.View.ShowWarnningMessage("该功能只能选择一行数据！");
                    return;
                }
                var data = selectList[0].DataRow;
                if (!data.ColumnContains("FMaterialId_Ref"))
                {
                    this.View.ShowWarnningMessage("过滤方案需勾选明细信息复选框！");
                    return;
                }
                var orgId = data["FApplicationOrgId_Id"];
                var supplierInfo = !data.ColumnContains("FSuggestSupplierId_Ref") ? null : data["FSuggestSupplierId_Ref"];
                var supplierId = supplierInfo == null ? 0 : (supplierInfo as DynamicObject)["Id"];
                var material = data["FMaterialId_Ref"] as DynamicObject;
                var mateiralId = material["Id"];
                var materialNumber = material["Number"];
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
}
