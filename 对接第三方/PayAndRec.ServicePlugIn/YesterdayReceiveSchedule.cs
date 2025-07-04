using BOA.YZW.PayAndRec.ServicePlugIn.Helper;
using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace BOA.YZW.PayAndRec.ServicePlugIn
{
    /// <summary>
    /// 收款单 昨天的数据
    /// </summary>
    [HotUpdate]
    [Description("收款单(昨天)-定时任务服务插件")]
    public class YesterdayReceiveSchedule : IScheduleService
    {
        public void Run(Context context, Schedule schedule)
        {
            var jsonStr = string.Empty;

            if (ConvertHelper.CheckDateTime())
            {
                return;
            }

            try
            {
                YesterdayScheduleHelper.ReceiveAutoHelper(context, out string _jsonStr);
                jsonStr = _jsonStr;
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
'失败','系统异常：{msg}','收款单定时任务(昨天)',
'','','',
'','0','1',
'{jsonStr.Replace("'", "").Replace("-", "")}')";
                DBUtils.Execute(context, logReocdStr);
            }
        }
    }
}
