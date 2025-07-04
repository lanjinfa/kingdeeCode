using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace BOA.SR.ContractReportPlugin.Plugins
{
    [HotUpdate]
    [Description("发货通知单_更新父项套数")]
    public class DeliveryNoticePlugin : AbstractBillPlugIn
    {
        public override void DataChanged(DataChangedEventArgs e)
        {
            base.DataChanged(e);
            if (e.Field.Key.EqualsIgnoreCase("FQty"))
            {
                var rowType = this.View.Model.GetValue("FRowType", e.Row);
                if (rowType.ToString().EqualsIgnoreCase("Parent"))
                {
                    return;
                }
                if (!rowType.ToString().EqualsIgnoreCase("Son"))
                {
                    return;
                }
                var parentRowId = this.View.Model.GetValue("FParentRowId", e.Row).ToString();
                var entity = this.View.BillBusinessInfo.GetEntity("FEntity");
                var entityData = this.View.Model.GetEntityDataObject(entity);
                var entryInfoCount = entityData.Where(t => t["ParentRowId"].ToString() == parentRowId
                                                                 && Convert.ToDouble(t["Qty"]) == Convert.ToDouble(e.NewValue)).Count();
                var entryInfoCount1 = entityData.Where(t => t["ParentRowId"].ToString() == parentRowId).Count();
                if (entryInfoCount == entryInfoCount1)
                {
                    var parentRowSeq = Convert.ToInt32(entityData.First(t => t["RowId"].ToString() == parentRowId)["Seq"]);
                    this.View.Model.SetValue("FQty", e.NewValue, parentRowSeq - 1);
                }
            }
        }
    }
}
