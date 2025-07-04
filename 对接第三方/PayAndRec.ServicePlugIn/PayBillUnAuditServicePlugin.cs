using BOA.YZW.PayAndRec.ServicePlugIn.Helper;
using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml;

namespace BOA.YZW.PayAndRec.ServicePlugIn
{
    [HotUpdate]
    [Description("付款单-反审核服务插件")]
    public class PayBillUnAuditServicePlugin : AbstractOperationServicePlugIn
    {
        public override void OnPreparePropertys(PreparePropertysEventArgs e)
        {
            base.OnPreparePropertys(e);
            e.FieldKeys.Add("F_BOA_IsSync");
            e.FieldKeys.Add("FBillNo");
            e.FieldKeys.Add("FOPPOSITEBANKACCOUNT");
            e.FieldKeys.Add("FOPPOSITECCOUNTNAME");
            e.FieldKeys.Add("FOPPOSITEBANKNAME");
            e.FieldKeys.Add("FREALPAYAMOUNTFOR_H");
            e.FieldKeys.Add("FSETTLETYPEID");
            e.FieldKeys.Add("FPayType");
            e.FieldKeys.Add("FPURPOSEID");
            e.FieldKeys.Add("FACCOUNTID");
            e.FieldKeys.Add("FNProvince");
            e.FieldKeys.Add("FNCity");
            e.FieldKeys.Add("F_BOA_NeedSyncH");
            e.FieldKeys.Add("F_BOA_IsSync");
        }

        public override void EndOperationTransaction(EndOperationTransactionArgs e)
        {
            base.EndOperationTransaction(e);
            var sysParameters = GSPSysParameterServiceHelper.GetAllSysParameters(this.Context) as DynamicObject;
            if (sysParameters["F_BOA_URL"].IsNullOrEmptyOrWhiteSpace() || sysParameters["F_BOA_KEY"].IsNullOrEmptyOrWhiteSpace()
                || sysParameters["F_BOA_CRCPW"].IsNullOrEmptyOrWhiteSpace() || sysParameters["F_BOA_CRCPrefix"].IsNullOrEmptyOrWhiteSpace())
            {
                throw new KDBusinessException("500", $"该【{this.Context.CurrentOrganizationInfo.Name}】组织，api未配置！请在【出纳管理参数】配置api地址。");
            }
            var apiUrl = sysParameters["F_BOA_URL"].ToString();
            var key = sysParameters["F_BOA_KEY"].ToString();
            var crc32_password = sysParameters["F_BOA_CRCPW"].ToString();
            var crc32_prefix = sysParameters["F_BOA_CRCPrefix"].ToString();
            var updateList = new List<string>();
            var errorMsg = string.Empty;//错误信息
            foreach (var item in e.DataEntitys)
            {
                var isSyncH = Convert.ToBoolean(item["F_BOA_NeedSyncH"]);//是否需要同步cbs
                if (!isSyncH)
                {
                    continue;
                }

                var billNO = item["BillNo"].ToString();//单据编号
                var entries = item["PAYBILLENTRY"] as DynamicObjectCollection;
                foreach (var entry in entries)
                {
                    var isSync = Convert.ToBoolean(entry["F_BOA_IsSync"]);//是否已同步
                    if (!isSync)
                    {
                        continue;
                    }

                    var settleType = (entry["SETTLETYPEID"] as DynamicObject)["Name"].ToString();//结算方式
                    if (!(settleType == "电汇" || settleType == "信汇" || settleType == "转账支票"))
                    {
                        continue;
                    }
                    var entrySeq = entry["Seq"].ToString();//行号
                    var seqInfo = SqlHelper.GetCBSREFNBRSEQ(this.Context, billNO, entrySeq);
                    if (seqInfo == null)
                    {
                        continue;
                    }
                    var refnbrTxt = seqInfo["F_BOA_REFNBR"].ToString();//企业参考业务号
                    //var accountName = entry["OPPOSITECCOUNTNAME"];//对方账户名称
                    var xmlAPAUTQRY = XmlHelper.CreateAPAUTQRYXml(out XmlElement rootERAGNOPR);
                    var xmlFunc = xmlAPAUTQRY.CreateElement("APAUTQRYX");
                    rootERAGNOPR.AppendChild(xmlFunc);
                    var refnbr = xmlAPAUTQRY.CreateElement("REFNBR");//参考业务号
                    refnbr.InnerText = $"{refnbrTxt}";
                    xmlFunc.AppendChild(refnbr);
                    var createERAGNOPRXml = XmlHelper.CreateSendXml(xmlAPAUTQRY.OuterXml, key, crc32_password, crc32_prefix);
                    var apiResult = WebApiHelper.ExcuteCBSApi(apiUrl, createERAGNOPRXml);
                    if (apiResult == "无法连接到远程服务器")
                    {
                        throw new KDBusinessException("500", apiResult);
                    }
                    var result = XmlHelper.GetERPAYSAVResponse(apiResult);
                    if (result.INFO.RETCOD == "0000000")//该条数据在CBS上未审批
                    {
                        var entryId = entry["Id"].ToString();//分录内码
                        var payType = entry["FPayType"].ToString().Trim();//支付类型(对应cbs操作类型)，（默认202，对外支付）
                        var cancleXml = XmlHelper.CreateERPAYCANXml(refnbrTxt, "202");//payType
                        var createERAGNOPRXml1 = XmlHelper.CreateSendXml(cancleXml.OuterXml, key, crc32_password, crc32_prefix);
                        var apiResult1 = WebApiHelper.ExcuteCBSApi(apiUrl, createERAGNOPRXml1);
                        var result1 = XmlHelper.GetERPAYSAVResponse(apiResult1);
                        if (result1.ERPAYCANZ.ERRCOD == "0000000")
                        {
                            updateList.Add($"update T_AP_PAYBILLENTRY set F_BOA_ISSYNC = 0,F_BOA_CBSNUMBER = '' where FEntryId = {entryId}");
                            var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_REFNBRSEQ,F_BOA_ISAUTO)
values ('{DateTime.Now}','{this.Context.UserName}','付款单',
'成功',' ','付款单反审核',
'{billNO}','{refnbrTxt}','{entrySeq}',
'{seqInfo["F_BOA_BUSNBR"]}','0',{seqInfo["F_BOA_REFNBRSEQ"]},'0')";
                            updateList.Add(logReocdStr);
                        }
                        else
                        {
                            var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_REFNBRSEQ,F_BOA_ISAUTO)
values ('{DateTime.Now}','{this.Context.UserName}','付款单',
'失败','{result1.ERPAYCANZ.ERRMSG}','付款单反审核',
'{billNO}','{refnbrTxt}','{entrySeq}',
'{seqInfo["F_BOA_BUSNBR"]}','0',{seqInfo["F_BOA_REFNBRSEQ"]},'0')";
                            updateList.Add(logReocdStr);
                            errorMsg += $"{result1.ERPAYCANZ.ERRMSG}\n";
                        }
                    }
                    else
                    {
                        errorMsg += $"{result.INFO.ERRMSG}\n";
                        //throw new KDBusinessException("500", $"反审核失败，原因：CBS报错，{result.INFO.ERRMSG}");
                    }
                }
            }
            if (updateList.Count > 0)
            {
                DBUtils.ExecuteBatch(this.Context, updateList, 100);
            }
            if (errorMsg != string.Empty)
            {
                throw new KDBusinessException("500", $"反审核失败，原因：CBS信息，{errorMsg}");
            }
        }
    }
}
