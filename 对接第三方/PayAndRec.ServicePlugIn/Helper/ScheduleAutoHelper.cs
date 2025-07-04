using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace BOA.YZW.PayAndRec.ServicePlugIn.Helper
{
    /// <summary>
    /// 收款单定时任务帮助类 (当日明细)
    /// </summary>
    public static class ScheduleAutoHelper
    {
        /// <summary>
        /// 收款单定时任务帮助类 (当日明细)
        /// </summary>
        /// <param name="context"></param>
        public static void ReceiveAutoHelper(Context context, out string jsonStr)
        {
            jsonStr = string.Empty;
            SqlHelper.UpdateSyncTaskStatusReceive(context);
            var sysParameters = GSPSysParameterServiceHelper.GetAllSysParameters(context) as DynamicObject;
            if (sysParameters["F_BOA_URL"].IsNullOrEmptyOrWhiteSpace() || sysParameters["F_BOA_KEY"].IsNullOrEmptyOrWhiteSpace()
                || sysParameters["F_BOA_CRCPW"].IsNullOrEmptyOrWhiteSpace() || sysParameters["F_BOA_CRCPrefix"].IsNullOrEmptyOrWhiteSpace())
            {
                var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO)
values ('{DateTime.Now}','{context.UserName}','收款单',
'失败','该【{context.CurrentOrganizationInfo.Name}】组织，api未配置！请在【出纳管理参数】配置api地址。','收款单定时任务',
'','','',
'','0','1')";
                DBUtils.Execute(context, logReocdStr);
                SqlHelper.UpdateSyncTaskStatusReceive(context, "0");
                return;
            }

            var loginStatus = WebApiHelper.Login1();
            if (loginStatus.ResultType != 1)
            {
                var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO)
values ('{DateTime.Now}','{context.UserName}','收款单',
'失败','调用金蝶系统登录接口失败','收款单定时任务',
'','','',
'','0','1')";
                DBUtils.Execute(context, logReocdStr);
                SqlHelper.UpdateSyncTaskStatusReceive(context, "0");
                return;
            }

            var apiUrl = sysParameters["F_BOA_URL"].ToString();
            var key = sysParameters["F_BOA_KEY"].ToString();
            var crc32_password = sysParameters["F_BOA_CRCPW"].ToString();
            var crc32_prefix = sysParameters["F_BOA_CRCPrefix"].ToString();

            #region (已在另外一个定时任务中执行)获取历史明细 即 昨天的数据再获取一次 保证数据不会丢失(因为cbs接口返回的数据不是实时的)

            //            var yesterdayDate = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");//昨天的日期
            //            var yesterdayLastSeq = "";//如果数据超过1000条，需要记录流水号
            //            while (true)
            //            {
            //                var xml01 = XmlHelper.CreateERQRYTRSXml(yesterdayLastSeq, yesterdayDate, yesterdayDate);
            //                var sendXml01 = XmlHelper.CreateSendXml(xml01.OuterXml, key, crc32_password, crc32_prefix);
            //                var apiResult01 = WebApiHelper.ExcuteCBSApi(apiUrl, sendXml01);
            //                if (apiResult01 == "无法连接到远程服务器")
            //                {
            //                    var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
            //F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
            //F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
            //F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO)
            //values ('{DateTime.Now}','{context.UserName}','收款单',
            //'失败','无法连接到远程服务器','收款单定时任务',
            //'','','',
            //'','0','1')";
            //                    DBUtils.Execute(context, logReocdStr);
            //                    SqlHelper.UpdateSyncTaskStatusReceive(context, "0");
            //                    return;
            //                }
            //                jsonStr = xml01.OuterXml;
            //                var result01 = XmlHelper.GetERPAYSAVResponse(apiResult01);
            //                if (result01.INFO.RETCOD != "0000000")
            //                {
            //                    var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
            //F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
            //F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
            //F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO,
            //F_BOA_SYNCPARA)
            //values ('{DateTime.Now}','{context.UserName}','收款单',
            //'失败','{result01.INFO.ERRMSG.Replace("'", "").Replace("-", "")}','收款单定时任务',
            //'','','',
            //'','0','1',
            //'{xml01.OuterXml.Replace("'", "").Replace("-", "")}')";
            //                    DBUtils.Execute(context, logReocdStr);
            //                    break;
            //                }
            //                else if (result01.INFO.RETCOD == "0000000" && result01.EREXPTRSZ.Count == 0)
            //                {
            //                    //                    var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
            //                    //F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
            //                    //F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
            //                    //F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO,
            //                    //F_BOA_SYNCPARA)
            //                    //values ('{DateTime.Now}','{context.UserName}','收款单',
            //                    //'失败','未查询到数据','收款单定时任务',
            //                    //'','','',
            //                    //'','0','1',
            //                    //'{xml01.OuterXml}')";
            //                    //                    DBUtils.Execute(context, logReocdStr);
            //                    break;
            //                }
            //                else
            //                {
            //                    var cbsInfos = SqlHelper.GetReceiveCBSInfos(context);
            //                    //var codlist = SqlHelper.GetSyncReceiveInfo(context);//业务参考号
            //                    if (cbsInfos.Count == 0)//后面新增的逻辑，如果未查询到收款单记录，表示不需要新增收款单。
            //                    {
            //                        break;
            //                    }

            //                    var orgDefaultNumber = SqlHelper.GetDefaultOrgNumber(context, context.CurrentOrganizationInfo.ID);
            //                    //var customerNameList = result01.EREXPTRSZ.Select(t => t.OTHNAM).Distinct().ToList();//对方账户名称对应客户名称
            //                    //var customerNumberList = SqlHelper.GetCustomerNumber(context, customerNameList);
            //                    var accountList = result01.EREXPTRSZ.Select(t => t.ACTNBR).Distinct().ToList();//银行账号
            //                    var accountInfo = SqlHelper.GetOrgNumber(context, accountList);
            //                    foreach (var item in result01.EREXPTRSZ)
            //                    {
            //                        var codInfo = cbsInfos.FirstOrDefault(t => t["F_BOA_BNKFLW"].ToString().Trim() == item.BNKFLW.Trim()
            //                                                  && t["F_BOA_ACTNBR"].ToString().Trim() == item.ACTNBR.Trim()
            //                                                  && t["F_BOA_OTHACT"].ToString().Trim() == item.OTHACT.Trim()
            //                                                  && t["F_BOA_ITMDIR"].ToString().Trim() == item.ITMDIR.Trim()
            //                                                  && t["F_BOA_TRSBAL"].ToString().Trim() == item.TRSBAL.Trim());
            //                        if (codInfo != null)
            //                        {
            //                            continue;
            //                        }

            //                        var itemDate = Convert.ToDateTime(item.BNKTIM);
            //                        var newDate = new DateTime(2022, 7, 11, 14, 0, 0);
            //                        if (itemDate < newDate) //只获取 2022年7月11日 14:00:00 以后的数据
            //                        {
            //                            continue;
            //                        }

            //                        var orgNumber = accountInfo.FirstOrDefault(t => t["accountNumber"].ToString() == item.ACTNBR);//
            //                        var orgNumberStr = orgNumber == null ? orgDefaultNumber : orgNumber["orgNumber"].ToString();//收款组织

            //                        //var cusNumber = customerNumberList.FirstOrDefault(t => t["fname"].ToString() == item.OTHNAM);//&& t["orgNumber"].ToString() == orgNumberStr
            //                        //var cusNumberStr = cusNumber == null ? "" : cusNumber["fnumber"].ToString();//往来单位(必填)或付款单位(必填)
            //                        var cusNumberStr = SqlHelper.GetCustomerNumber(context, item.OTHNAM, item.OTHACT);

            //                        var model = new JObject();

            //                        var billTypeId = "SKDLX01_SYS";//单据类型，销售收款单

            //                        if (item.NUSAGE.Contains("银联") || item.NUSAGE.Contains("提现"))
            //                        {
            //                            billTypeId = "SKDLX07";//提现业务收款单
            //                        }
            //                        else if (item.NUSAGE.Contains("利息") || item.NUSAGE.Contains("息"))
            //                        {
            //                            billTypeId = "SKDLX02_SYS";//其他业务收款单
            //                        }
            //                        //后面修改的逻辑
            //                        if (item.OTHNAM.Contains("银联") || item.OTHNAM.Contains("提现"))
            //                        {
            //                            billTypeId = "SKDLX07";//提现业务收款单
            //                        }

            //                        model["FBillTypeID"] = new JObject { ["FNUMBER"] = billTypeId };//单据类型
            //                        model["FPAYORGID"] = new JObject { ["FNumber"] = orgNumberStr };//收款组织
            //                        model["FCONTACTUNITTYPE"] = "BD_Customer";//往来单位类型(必填项)
            //                        model["FCONTACTUNIT"] = new JObject { ["FNUMBER"] = cusNumberStr };//往来单位(必填)
            //                        model["FPAYUNITTYPE"] = "BD_Customer";//付款单位类型(必填项)
            //                        model["FPAYUNIT"] = new JObject { ["FNUMBER"] = cusNumberStr };//付款单位(必填)
            //                        model["FREMARK"] = item.NUSAGE;//备注

            //                        model["F_BOA_BNKFLW"] = item.BNKFLW;//银行流水号
            //                        model["F_BOA_ACTNBR"] = item.ACTNBR;//银行账号
            //                        model["F_BOA_OTHACT"] = item.OTHACT;//对方账号
            //                        model["F_BOA_ITMDIR"] = item.ITMDIR;//借贷方向
            //                        model["F_BOA_TRSBAL"] = item.TRSBAL;//金额

            //                        model["FDate"] = yesterdayDate;//业务日期

            //                        var entries = new JArray();
            //                        var entry = new JObject();

            //                        if (billTypeId == "SKDLX01_SYS" || billTypeId == "SKDLX07")//销售收款单,提现业务收款单
            //                        {
            //                            entry["FPURPOSEID"] = new JObject { ["FNumber"] = "SFKYT01_SYS" };//收款用途，销售收款
            //                        }
            //                        else if (billTypeId == "SKDLX02_SYS")//其他业务收款单
            //                        {
            //                            entry["FPURPOSEID"] = new JObject { ["FNumber"] = "SFKYT07_SYS" };//收款用途，其他收入
            //                        }

            //                        entry["FSETTLETYPEID"] = new JObject { ["FNumber"] = "JSFS04_SYS" };//结算方式 默认电汇
            //                        entry["FRECTOTALAMOUNTFOR"] = item.TRSBAL;//应收金额
            //                        entry["FACCOUNTID"] = new JObject { ["FNumber"] = item.ACTNBR };//我方银行账号(必填项)
            //                        entry["FOPPOSITEBANKACCOUNT"] = item.OTHACT;//对方银行账号
            //                        entry["FOPPOSITECCOUNTNAME"] = item.OTHNAM;//对方账户名称
            //                        entry["FOPPOSITEBANKNAME"] = item.OTHOPN;//对方开户行
            //                        entry["F_BOA_CBSNumber"] = item.DTLSEQ;//明细流水号
            //                        entries.Add(entry);
            //                        model["FRECEIVEBILLENTRY"] = entries;
            //                        var fields = new JObject
            //                        {
            //                            ["Model"] = model
            //                        };
            //                        var parameterStr = JsonConvert.SerializeObject(fields);
            //                        jsonStr = parameterStr;

            //                        var apiResult1 = WebApiHelper.Save(loginStatus.Client, "AR_RECEIVEBILL", parameterStr);
            //                        //var apiResult1 = WebApiHelper.ExcuteSaveOperate(out string jsonStr, context, model, "AR_RECEIVEBILL");
            //                        if (apiResult1.ResponseStatus.IsSuccess)
            //                        {
            //                            //isGetHistoryData = true;

            //                            var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
            //F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
            //F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
            //F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO)
            //values ('{DateTime.Now}','{context.UserName}','收款单',
            //'成功',' ','收款单定时任务(昨天)',
            //'{apiResult1.Number.Replace("'", "").Replace("-", "")}','{item.REFCOD.Replace("'", "").Replace("-", "")}','1',
            //'{item.DTLSEQ}','3','1')";
            //                            DBUtils.Execute(context, logReocdStr);
            //                        }
            //                        else
            //                        {
            //                            var msg = string.Empty;
            //                            if (apiResult1.ResponseStatus.Errors.Count > 0)
            //                            {
            //                                msg = apiResult1.ResponseStatus.Errors.First().Message;
            //                            }
            //                            var apiResult2 = WebApiHelper.Draft(loginStatus.Client, "AR_RECEIVEBILL", parameterStr);

            //                            //isGetHistoryData = true;

            //                            parameterStr = parameterStr.Replace("'", "").Replace("-", "");
            //                            //var apiResult2 = WebApiHelper.ExcuteDraftOperate(context, model, "AR_RECEIVEBILL", out string paraStr);
            //                            var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
            //F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
            //F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
            //F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO,
            //F_BOA_SYNCPARA)
            //values ('{DateTime.Now}','{context.UserName}','收款单',
            //'成功','单据暂存，暂存原因如下：{msg}','收款单定时任务(昨天)',
            //'{apiResult2.Id}','{item.REFCOD.Replace("'", "").Replace("-", "")}','1',
            //'{item.DTLSEQ}','3','1',
            //'{parameterStr}')";
            //                            DBUtils.Execute(context, logReocdStr);
            //                        }
            //                    }
            //                }
            //                if (result01.ERDTLSEQZ == null || result01.ERDTLSEQZ.DTLSEQ == null
            //                    || result01.ERDTLSEQZ.DTLSEQ == "" || result01.ERDTLSEQZ.DTLSEQ == "0")
            //                {
            //                    break;
            //                }
            //                else
            //                {
            //                    yesterdayLastSeq = result01.ERDTLSEQZ.DTLSEQ;
            //                }
            //            }

            #endregion

            #region 当日明细

            var lastSeq = SqlHelper.GetLastReceiveSeq(context);//最后一次流水号

            var xml = XmlHelper.CreateERCURDTLXml(lastSeq);
            var sendXml = XmlHelper.CreateSendXml(xml.OuterXml, key, crc32_password, crc32_prefix);
            var apiResult = WebApiHelper.ExcuteCBSApi(apiUrl, sendXml);
            if (apiResult == "无法连接到远程服务器")
            {
                var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO)
values ('{DateTime.Now}','{context.UserName}','收款单',
'失败','无法连接到远程服务器','收款单定时任务',
'','','',
'','0','1')";
                DBUtils.Execute(context, logReocdStr);
                SqlHelper.UpdateSyncTaskStatusReceive(context, "0");
                return;
            }
            jsonStr = xml.OuterXml;
            var result = XmlHelper.GetERPAYSAVResponse(apiResult);
            if (result.INFO.RETCOD != "0000000")
            {
                var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO,
F_BOA_SYNCPARA)
values ('{DateTime.Now}','{context.UserName}','收款单',
'失败','{result.INFO.ERRMSG.Replace("'", "").Replace("-", "")}','收款单定时任务',
'','','',
'','0','1',
'{xml.OuterXml.Replace("'", "").Replace("-", "")}')";
                DBUtils.Execute(context, logReocdStr);
            }
            else if (result.INFO.RETCOD == "0000000" && result.ERCURDTLZ.Count == 0)
            {
                var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO,
F_BOA_SYNCPARA)
values ('{DateTime.Now}','{context.UserName}','收款单',
'失败','未查询到数据','收款单定时任务',
'','','',
'','0','1',
'{xml.OuterXml.Replace("'", "").Replace("-", "")}')";
                DBUtils.Execute(context, logReocdStr);
            }
            else
            {
                var cbsInfos = SqlHelper.GetReceiveCBSInfosJT(context);
                var orgDefaultNumber = SqlHelper.GetDefaultOrgNumber(context, context.CurrentOrganizationInfo.ID);
                //var codlist = SqlHelper.GetSyncReceiveInfo(context);//业务参考号
                //var customerNameList = result.ERCURDTLZ.Select(t => t.OTHNAM).Distinct().ToList();//对方账户名称对应客户名称
                //var customerNumberList = SqlHelper.GetCustomerNumber(context, customerNameList);
                var accountList = result.ERCURDTLZ.Select(t => t.ACTNBR).Distinct().ToList();//银行账号
                var accountInfo = SqlHelper.GetOrgNumber(context, accountList);
                foreach (var item in result.ERCURDTLZ)
                {
                    //var codInfo = codlist.FirstOrDefault(t => t["F_BOA_BUSNBR"].ToString() == item.DTLSEQ);
                    //if (codInfo != null)
                    //{
                    //    continue;
                    //}

                    var codInfo = cbsInfos.FirstOrDefault(t => t["F_BOA_BNKFLW"].ToString().Trim() == item.BNKFLW.Trim()
                                                   && t["F_BOA_ACTNBR"].ToString().Trim() == item.ACTNBR.Trim()
                                                   && t["F_BOA_OTHACT"].ToString().Trim() == item.OTHACT.Trim()
                                                   && t["F_BOA_ITMDIR"].ToString().Trim() == item.ITMDIR.Trim()
                                                   && t["F_BOA_TRSBAL"].ToString().Trim() == item.TRSBAL.Trim());
                    if (codInfo != null)
                    {
                        continue;
                    }

                    //var itemDate = Convert.ToDateTime(item.BNKTIM);
                    //var newDate = new DateTime(2022, 7, 11, 14, 0, 0);
                    //if (itemDate < newDate) //只获取 2022年7月11日 14:00:00 以后的数据
                    //{
                    //    continue;
                    //}

                    var orgNumber = accountInfo.FirstOrDefault(t => t["accountNumber"].ToString() == item.ACTNBR);
                    var orgNumberStr = orgNumber == null ? orgDefaultNumber : orgNumber["orgNumber"].ToString();//收款组织

                    //var cusNumber = customerNumberList.FirstOrDefault(t => t["fname"].ToString() == item.OTHNAM);
                    //&& t["orgNumber"].ToString() == orgNumberStr
                    //var cusNumberStr = cusNumber == null ? "" : cusNumber["fnumber"].ToString();//往来单位(必填)或付款单位(必填)
                    var cusNumberStr = SqlHelper.GetCustomerNumber(context, item.OTHNAM, item.OTHACT);

                    var model = new JObject();

                    var billTypeId = "SKDLX01_SYS";//单据类型，销售收款单
                    if (item.NUSAGE.Contains("银联") || item.NUSAGE.Contains("提现"))
                    {
                        billTypeId = "SKDLX07";//提现业务收款单
                    }
                    else if (item.NUSAGE.Contains("利息") || item.NUSAGE.Contains("息"))
                    {
                        billTypeId = "SKDLX02_SYS";//其他业务收款单
                    }
                    //后面修改的逻辑
                    if (item.OTHNAM.Contains("银联") || item.OTHNAM.Contains("提现"))
                    {
                        billTypeId = "SKDLX07";//提现业务收款单
                    }

                    model["FBillTypeID"] = new JObject { ["FNUMBER"] = billTypeId };//单据类型
                    model["FPAYORGID"] = new JObject { ["FNumber"] = orgNumberStr };//收款组织
                    model["FCONTACTUNITTYPE"] = "BD_Customer";//往来单位类型(必填项)
                    model["FCONTACTUNIT"] = new JObject { ["FNUMBER"] = cusNumberStr };//往来单位(必填)
                    model["FPAYUNITTYPE"] = "BD_Customer";//付款单位类型(必填项)
                    model["FPAYUNIT"] = new JObject { ["FNUMBER"] = cusNumberStr };//付款单位(必填)
                    model["FREMARK"] = item.NUSAGE;//备注

                    model["F_BOA_BNKFLW"] = item.BNKFLW;//银行流水号
                    model["F_BOA_ACTNBR"] = item.ACTNBR;//银行账号
                    model["F_BOA_OTHACT"] = item.OTHACT;//对方账号
                    model["F_BOA_ITMDIR"] = item.ITMDIR;//借贷方向
                    model["F_BOA_TRSBAL"] = item.TRSBAL;//金额

                    model["FDate"] = item.BNKTIM;//业务日期

                    var entries = new JArray();
                    var entry = new JObject();

                    if (billTypeId == "SKDLX01_SYS" || billTypeId == "SKDLX07")//销售收款单,提现业务收款单
                    {
                        entry["FPURPOSEID"] = new JObject { ["FNumber"] = "SFKYT01_SYS" };//收款用途，销售收款
                    }
                    else if (billTypeId == "SKDLX02_SYS")//其他业务收款单
                    {
                        entry["FPURPOSEID"] = new JObject { ["FNumber"] = "SFKYT07_SYS" };//收款用途，其他收入
                    }

                    entry["FSETTLETYPEID"] = new JObject { ["FNumber"] = "JSFS04_SYS" };//结算方式 默认电汇
                    entry["FRECTOTALAMOUNTFOR"] = item.TRSBAL;//应收金额
                    entry["FACCOUNTID"] = new JObject { ["FNumber"] = item.ACTNBR };//我方银行账号(必填项)
                    entry["FOPPOSITEBANKACCOUNT"] = item.OTHACT;//对方银行账号
                    entry["FOPPOSITECCOUNTNAME"] = item.OTHNAM;//对方账户名称
                    entry["FOPPOSITEBANKNAME"] = item.OTHOPN;//对方开户行
                    entry["F_BOA_CBSNumber"] = item.DTLSEQ;//明细流水号
                    entries.Add(entry);
                    model["FRECEIVEBILLENTRY"] = entries;
                    var fields = new JObject
                    {
                        ["Model"] = model
                    };
                    var parameterStr = JsonConvert.SerializeObject(fields);
                    jsonStr = parameterStr;

                    var apiResult1 = WebApiHelper.Save(loginStatus.Client, "AR_RECEIVEBILL", parameterStr);
                    //var apiResult1 = WebApiHelper.ExcuteSaveOperate(out string jsonStr, context, model, "AR_RECEIVEBILL");
                    if (apiResult1.ResponseStatus.IsSuccess)
                    {
                        var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO)
values ('{DateTime.Now}','{context.UserName}','收款单',
'成功',' ','收款单定时任务',
'{apiResult1.Number.Replace("'", "").Replace("-", "")}','{item.REFCOD.Replace("'", "").Replace("-", "")}','1',
'{item.DTLSEQ}','3','1')";
                        DBUtils.Execute(context, logReocdStr);
                    }
                    else
                    {
                        var msg = string.Empty;
                        if (apiResult1.ResponseStatus.Errors.Count > 0)
                        {
                            msg = apiResult1.ResponseStatus.Errors.First().Message;
                            msg = msg.Replace("'", "").Replace("-", "");
                            if (msg.Length > 3000)//保证长度不能超过3000
                            {
                                msg = msg.SubStr(0, 3000);
                            }
                        }
                        var apiResult2 = WebApiHelper.Draft(loginStatus.Client, "AR_RECEIVEBILL", parameterStr);

                        //var apiResult2 = WebApiHelper.ExcuteDraftOperate(context, model, "AR_RECEIVEBILL", out string paraStr);
                        var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_ISAUTO,
F_BOA_SYNCPARA)
values ('{DateTime.Now}','{context.UserName}','收款单',
'成功','单据暂存，暂存原因如下：{msg}','收款单定时任务',
'{apiResult2.Id}','{item.REFCOD.Replace("'", "").Replace("-", "")}','1',
'{item.DTLSEQ}','3','1',
'{parameterStr.Replace("'", "").Replace("-", "")}')";
                        DBUtils.Execute(context, logReocdStr);
                    }
                }
            }

            #endregion

            SqlHelper.UpdateSyncTaskStatusReceive(context, "0");
        }
    }
}
