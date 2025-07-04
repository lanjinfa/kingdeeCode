using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace BOA.YZW.PayAndRec.ServicePlugIn.BillPlugin
{
    public static class Record
    {
        public static Dictionary<long, DynamicObject> modelData = new Dictionary<long, DynamicObject>();
    }

    [HotUpdate]
    [Description("收款单表单插件")]
    public class ReceiveBillPlugin : AbstractBillPlugIn
    {
        public override void AfterBindData(EventArgs e)
        {
            base.AfterBindData(e);
            if (Record.modelData.ContainsKey(this.Context.UserId))
            {
                var pkId = this.View.Model.GetPKValue();//当前单据主键
                if (pkId == null)
                {
                    return;
                }
                var billValues = Record.modelData[this.Context.UserId];
                var recordPkId = billValues["Id"];//记录的单据主键
                if (pkId.ToString() != recordPkId.ToString())
                {
                    return;
                }
                this.View.Model.SetValue("FCONTACTUNITTYPE", billValues["CONTACTUNITTYPE"]);//往来单位类型
                this.View.Model.SetValue("FCONTACTUNIT", billValues["CONTACTUNIT"]);//往来单位
                this.View.Model.SetValue("FPAYUNITTYPE", billValues["PAYUNITTYPE"]);//付款单位类型
                this.View.Model.SetValue("FPAYUNIT", billValues["PAYUNIT"]);//付款单位
                this.View.Model.SetValue("FREMARK", billValues["FREMARK"]);//备注
                this.View.Model.SetValue("F_BOA_BNKFLW", billValues["F_BOA_BNKFLW"]);//银行流水号
                this.View.Model.SetValue("F_BOA_ACTNBR", billValues["F_BOA_ACTNBR"]);//银行账号
                this.View.Model.SetValue("F_BOA_OTHACT", billValues["F_BOA_OTHACT"]);//对方账号
                this.View.Model.SetValue("F_BOA_ITMDIR", billValues["F_BOA_ITMDIR"]);//借贷方向
                this.View.Model.SetValue("F_BOA_TRSBAL", billValues["F_BOA_TRSBAL"]);//金额
                var entryData = billValues["RECEIVEBILLENTRY"] as DynamicObjectCollection;//收款单明细
                var entity = this.View.BillBusinessInfo.GetEntity("FRECEIVEBILLENTRY");
                foreach (var item in entryData)
                {
                    var seq = Convert.ToInt32(item["Seq"]);
                    foreach (var field in entity.Fields)
                    {
                        var key = field.Key;
                        var prop = field.PropertyName;
                        if (item.Contains(prop))
                        {
                            this.View.Model.SetValue(key, item[prop], seq - 1);
                        }
                    }
                }
            }
        }

        public override void BeforeUpdateValue(BeforeUpdateValueEventArgs e)
        {
            base.BeforeUpdateValue(e);
            if (e.Key.EqualsIgnoreCase("FBillTypeID") || e.Key.EqualsIgnoreCase("FPAYORGID"))
            {
                if (Record.modelData.ContainsKey(this.Context.UserId))
                {
                    Record.modelData.Remove(this.Context.UserId);
                    Record.modelData.Add(this.Context.UserId, this.View.Model.DataObject);
                }
                else
                {
                    Record.modelData.Add(this.Context.UserId, this.View.Model.DataObject);
                }
            }
        }

        public override void BeforeClosed(BeforeClosedEventArgs e)
        {
            base.BeforeClosed(e);
            if (Record.modelData.ContainsKey(this.Context.UserId))
            {
                Record.modelData.Remove(this.Context.UserId);
            }
        }
    }
}
