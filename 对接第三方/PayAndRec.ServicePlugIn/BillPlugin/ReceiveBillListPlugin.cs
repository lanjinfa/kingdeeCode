using BOA.YZW.PayAndRec.ServicePlugIn.Helper;
using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.List.PlugIn;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.Linq;

namespace BOA.YZW.PayAndRec.ServicePlugIn.BillPlugin
{
    [HotUpdate]
    [Description("收款单列表插件")]
    public class ReceiveBillListPlugin : AbstractListPlugIn
    {
        public override void AfterBarItemClick(AfterBarItemClickEventArgs e)
        {
            base.AfterBarItemClick(e);
            if (e.BarItemKey.EqualsIgnoreCase("BOA_GetData"))
            {
                var context = this.Context;
                Test2(context);
                this.View.ShowMessage("执行成功！");
            }
        }

        /// <summary>
        /// 收款单定时任务测试
        /// </summary>
        /// <param name="context"></param>
        /// <exception cref="KDBusinessException"></exception>
        private void Test2(Context context)
        {
            var jsonStr = string.Empty;
            //var jsonStr1 = string.Empty;

            if (ConvertHelper.CheckDateTime())
            {
                return;
            }

            try
            {
                if (!SqlHelper.GetIsSyncTaskReceive(context))
                {
                    if (!SqlHelper.GetLastExcuteTime(context, "收款单"))
                    {
                        this.View.ShowMessage("收款单定时任务正在执行，请稍后再点击该按钮！");
                        return;
                    }
                }

                YesterdayScheduleHelper.ReceiveAutoHelper(context, out string _jsonStr1);
                ScheduleAutoHelper.ReceiveAutoHelper(context, out string _jsonStr);
                jsonStr = _jsonStr;
                //jsonStr1 = _jsonStr1;
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Replace("'", "").Replace("-", "");
                var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO,
F_BOA_SYNCPARA)
values ('{DateTime.Now}','{context.UserName}','收款单',
'失败','系统异常：{msg}','收款单定时任务',
'','','',
'','0','1',
'{jsonStr.Replace("'", "").Replace("-", "")}')";
                DBUtils.Execute(context, logReocdStr);
                SqlHelper.UpdateSyncTaskStatusReceive(context, "0");
            }
        }
    }
}
