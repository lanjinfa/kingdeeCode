using BOA.YZW.PayAndRec.ServicePlugIn.Helper;
using Kingdee.BOS;
using Kingdee.BOS.App.Core;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.List.PlugIn;
using Kingdee.BOS.Log;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Xml;

namespace BOA.YZW.PayAndRec.ServicePlugIn.BillPlugin
{
    [HotUpdate]
    [Description("付款单列表插件")]
    public class PayBillListPlugin : AbstractListPlugIn
    {
        public override void AfterBarItemClick(AfterBarItemClickEventArgs e)
        {
            base.AfterBarItemClick(e);
            if (e.BarItemKey.EqualsIgnoreCase("BOA_GetData"))
            {
                var context = this.Context;
                Test1(context);
            }
        }

        /// <summary>
        /// 付款单定时任务(列表按钮使用)
        /// </summary>
        /// <param name="context"></param>
        /// <exception cref="KDBusinessException"></exception>
        private void Test1(Context context)
        {
            var xmlStr = string.Empty;

            if (ConvertHelper.CheckDateTime())
            {
                return;
            }

            try
            {
                if (!SqlHelper.GetIsSyncTaskPay(context))
                {
                    if (!SqlHelper.GetLastExcuteTime(context, "付款单"))
                    {
                        this.View.ShowMessage("付款单定时任务正在执行，请稍后再点击该按钮！");
                        return;
                    }
                }
                SqlHelper.UpdateSyncTaskStatusPay(context);
                var sysParameters = GSPSysParameterServiceHelper.GetAllSysParameters(context) as DynamicObject;
                if (sysParameters["F_BOA_URL"].IsNullOrEmptyOrWhiteSpace() || sysParameters["F_BOA_KEY"].IsNullOrEmptyOrWhiteSpace()
                    || sysParameters["F_BOA_CRCPW"].IsNullOrEmptyOrWhiteSpace() || sysParameters["F_BOA_CRCPrefix"].IsNullOrEmptyOrWhiteSpace())
                {
                    var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO)
values ('{DateTime.Now}','{context.UserName}','付款单',
'失败','该【{context.CurrentOrganizationInfo.Name}】组织，api未配置！请在【出纳管理参数】配置api地址。','付款单定时任务',
'','','',
'','0','1')";
                    DBUtils.Execute(context, logReocdStr);
                    SqlHelper.UpdateSyncTaskStatusPay(context, "0");
                    return;
                }
                var apiUrl = sysParameters["F_BOA_URL"].ToString();
                var key = sysParameters["F_BOA_KEY"].ToString();
                var crc32_password = sysParameters["F_BOA_CRCPW"].ToString();
                var crc32_prefix = sysParameters["F_BOA_CRCPrefix"].ToString();
                var queryResult = SqlHelper.GetCBSPayBillInfo(context);
                if (queryResult.Count > 0)
                {
                    var xmlERPAYSTA = XmlHelper.CreateERPAYSTAXml(out XmlElement rootERPAYSTA);
                    foreach (var item in queryResult)
                    {
                        var rEFNBR = item["F_BOA_REFNBR"].ToString();//企业参考业务号
                        var bUSNBR = item["F_BOA_BUSNBR"].ToString();//业务流水号
                        var xmlFunc = xmlERPAYSTA.CreateElement("ERPAYSTAX");//二级节点，接口参数根节点，所有接口字段参数作为三级节点添加到该节点下
                        rootERPAYSTA.AppendChild(xmlFunc);
                        var refnbr = xmlERPAYSTA.CreateElement("REFNBR");//企业参考业务号
                        refnbr.InnerText = $"{rEFNBR}";
                        xmlFunc.AppendChild(refnbr);
                        var busnbr = xmlERPAYSTA.CreateElement("BUSNBR");//业务流水号
                        busnbr.InnerText = $"{bUSNBR}";
                        xmlFunc.AppendChild(busnbr);
                    }
                    xmlStr = xmlERPAYSTA.OuterXml;
                    var createERPAYSTAXml = XmlHelper.CreateSendXml(xmlERPAYSTA.OuterXml, key, crc32_password, crc32_prefix);
                    var apiResult = WebApiHelper.ExcuteCBSApi(apiUrl, createERPAYSTAXml);
                    var logRecodList = new List<string>();
                    if (apiResult == "无法连接到远程服务器")
                    {
                        var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO)
values ('{DateTime.Now}','{context.UserName}','付款单',
'失败','无法连接到远程服务器','付款单定时任务',
'','','',
'','0','1')";
                        DBUtils.Execute(context, logReocdStr);
                        SqlHelper.UpdateSyncTaskStatusPay(context, "0");
                        return;
                    }
                    var result = XmlHelper.GetERPAYSAVResponse(apiResult);
                    foreach (var item in result.ERPAYSTAZ)
                    {
                        var billInfo = queryResult.FirstOrDefault(t => t["F_BOA_BUSNBR"].ToString() == item.BUSNBR
                                     && t["F_BOA_REFNBR"].ToString() == item.REFNBR);
                        var entryId = "0";
                        if (billInfo != null)
                        {
                            entryId = billInfo["FEntryId"].ToString();//
                        }
                        if (item.ERRCOD == "0000000")
                        {
                            var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO)
values ('{DateTime.Now}','{context.UserName}','付款单',
'成功',' ','付款单定时任务',
'{billInfo["FBILLNO"]}','{item.REFNBR}','{billInfo["FSeq"]}',
'{item.BUSNBR}','0','1')";
                            DBUtils.Execute(context, logReocdStr);
                            var status = item.STATUS;
                            var statusValue = "";
                            if (status == "0")//
                            {
                                statusValue = "查无此记录";
                            }
                            else if (status == "1")
                            {
                                OAHelper.Submit(context, billInfo, item.PAYTIM);
                                statusValue = "支付成功";
                            }
                            else if (status == "2")
                            {
                                statusValue = "支付失败";
                            }
                            else if (status == "3")
                            {
                                statusValue = "未完成";
                            }
                            else if (status == "4")
                            {
                                statusValue = "银行退票";
                            }
                            DBUtils.Execute(context, $"update T_AP_PAYBILLENTRY set F_BOA_CBSStatus = '{statusValue}',F_BOA_PaymentStatusExt = '{status}' where FEntryId = {entryId}");
                        }
                        else
                        {
                            var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO,
F_BOA_SYNCPARA)
values ('{DateTime.Now}','{context.UserName}','付款单',
'失败','CBS接口报错，错误码：{item.ERRCOD}','付款单定时任务',
'{billInfo["FBILLNO"]}','{item.REFNBR}','{billInfo["FSeq"]}',
'{item.BUSNBR}','0','1',
'{xmlERPAYSTA.OuterXml.Replace("'", "")}')";
                            DBUtils.Execute(context, logReocdStr);
                        }
                    }
                }
                SqlHelper.UpdateSyncTaskStatusPay(context, "0");
            }
            catch (Exception ex)
            {
                var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO,
F_BOA_SYNCPARA)
values ('{DateTime.Now}','{context.UserName}','付款单',
'失败','系统异常：{ex.Message}','付款单定时任务',
'','','',
'','0','1',
'{xmlStr.Replace("'", "")}')";
                DBUtils.Execute(context, logReocdStr);
                SqlHelper.UpdateSyncTaskStatusPay(context, "0");
            }
            this.View.ShowMessage("执行成功！");
        }
    }
}
