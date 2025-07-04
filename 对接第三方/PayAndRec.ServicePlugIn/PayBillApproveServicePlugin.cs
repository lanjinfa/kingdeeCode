using BOA.YZW.PayAndRec.ServicePlugIn.Helper;
using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Xml;

namespace BOA.YZW.PayAndRec.ServicePlugIn
{
    [HotUpdate]
    [Description("付款单-审核服务插件")]
    public class PayBillApproveServicePlugin : AbstractOperationServicePlugIn
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
            e.FieldKeys.Add("F_BOA_NeedSync");
            e.FieldKeys.Add("FCNAPS");
            e.FieldKeys.Add("FREMARK");
            e.FieldKeys.Add("F_BOA_IsLand");//是否落地
            e.FieldKeys.Add("F_BOA_Direction");//是否定向
            e.FieldKeys.Add("F_BOA_Urgent");//是否加急
            e.FieldKeys.Add("F_BOA_IsCity");//是否同城
            e.FieldKeys.Add("F_BOA_SizeAmount");//大小额
            e.FieldKeys.Add("FREALPAYAMOUNTFOR_H");//实付金额
            e.FieldKeys.Add("F_BOA_NeedSyncH");//是否需要同步CBS
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
            var msg = string.Empty;//信息记录
            var updateList = new List<string>();
            var logRecodList = new List<string>();
            foreach (var item in e.DataEntitys)
            {
                var isSyncH = Convert.ToBoolean(item["F_BOA_NeedSyncH"]);//是否需要同步cbs
                if (!isSyncH)
                {
                    continue;
                }

                var billNO = item["BillNo"].ToString();//单据编号
                var entries = item["PAYBILLENTRY"] as DynamicObjectCollection;
                var remake = item["FREMARK"] == null ? "" : item["FREMARK"].ToString();//备注(作为cbs的用途)
                var F_BOA_IsLand = item["F_BOA_IsLand"];// 是否落地
                var F_BOA_Direction = item["F_BOA_Direction"];//是否定向
                var F_BOA_Urgent = item["F_BOA_Urgent"];//是否加急
                var F_BOA_IsCity = item["F_BOA_IsCity"];//是否同城
                var F_BOA_SizeAmount = item["F_BOA_SizeAmount"];//大小额
                var amount = ConvertHelper.GetConvertNumValue(Convert.ToDecimal(item["REALPAYAMOUNTFOR"]));//实付金额
                var rowIndex = 1;//遍历的次数
                var seqStart = SqlHelper.GetCBSREFNBRSEQ(this.Context, billNO);
                foreach (var entry in entries)
                {
                    var entrySeq = entry["Seq"].ToString();//分录行号
                    if (Convert.ToInt32(entrySeq) > 1)
                    {
                        break;
                    }

                    var settleType = (entry["SETTLETYPEID"] as DynamicObject)["Name"].ToString();//结算方式
                    if (!(settleType == "电汇" || settleType == "信汇" || settleType == "转账支票"))
                    {
                        continue;
                    }
                    var isSync = Convert.ToBoolean(entry["F_BOA_IsSync"]);//是否已同步
                    if (isSync)
                    {
                        continue;
                    }
                    //var needSync = Convert.ToBoolean(entry["F_BOA_NeedSync"]);//是否需要同步cbs
                    //if (!needSync)
                    //{
                    //    continue;
                    //}
                    if (entry["NProvince"].IsNullOrEmptyOrWhiteSpace() || entry["NCity"].IsNullOrEmptyOrWhiteSpace())
                    {
                        throw new KDBusinessException("500", "省或城市不能为空！");
                    }

                    var accountInfo = entry["OPPOSITEBANKACCOUNT"];//对方银行账号
                    var accountName = entry["OPPOSITECCOUNTNAME"];//对方账户名称
                    var accountBankName = entry["OPPOSITEBANKNAME"];//对方开户行
                    var fcnaps = entry["CNAPS"];//联行号
                    if (accountName.IsNullOrEmptyOrWhiteSpace() || accountInfo.IsNullOrEmptyOrWhiteSpace())
                    {
                        throw new KDBusinessException("500", "对方银行账号或对方账户名称不能为空！");
                    }
                    else
                    {

                        var seq = seqStart + rowIndex;//序号
                        var entryId = entry["Id"].ToString();//分录内码
                        //var amount = ConvertHelper.GetConvertNumValue(Convert.ToDecimal(entry["REALPAYAMOUNTFOR"]));//实付金额
                        //var payType = entry["FPayType"].ToString().Trim();//支付类型(对应cbs操作类型)
                        //var purpose = (entry["PURPOSEID"] as DynamicObject)["Name"].ToString();//付款用途(对应cbs交易用途)
                        var payAccount = "";//我方银行账号
                        var payAccountName = "";//我方账户名称
                        var payBankName = "";//我方开户行
                        var payAccountInfo = entry["FACCOUNTID"];//我方银行账号信息
                        if (payAccountInfo != null)
                        {
                            var payAccountInfoDy = payAccountInfo as DynamicObject;
                            payAccount = payAccountInfoDy["Number"].ToString();//我方银行账号
                            payAccountName = payAccountInfoDy["Name"].ToString();//我方账户名称
                            payBankName = (payAccountInfoDy["BANKID"] as DynamicObject)["Name"].ToString();//我方开户行
                        }

                        var province = entry["NProvince"].ToString().Trim();//省
                        var city = entry["NCity"].ToString().Trim();//城市

                        //账户名称字符多于四个字符调用CBS经办接口
                        //rootERPAYSAV为一级节点，接口参数根节点添加在该节点下 --ERP支付经办请求
                        var xmlERPAYSAV = XmlHelper.CreateERPAYSAVXml(out XmlElement rootERPAYSAV);

                        var xmlFunc1 = xmlERPAYSAV.CreateElement("APATHINFY");//二级节点，接口参数根节点，所有接口字段参数作为三级节点添加到该节点下
                        var athnam = xmlERPAYSAV.CreateElement("ATHNAM");//备注
                        athnam.InnerText = $"{this.Context.UserName}";
                        xmlFunc1.AppendChild(athnam);
                        rootERPAYSAV.AppendChild(xmlFunc1);

                        var xmlFunc = xmlERPAYSAV.CreateElement("APPAYSAVX");//二级节点，接口参数根节点，所有接口字段参数作为三级节点添加到该节点下
                        rootERPAYSAV.AppendChild(xmlFunc);
                        var recnum = xmlERPAYSAV.CreateElement("RECNUM");//记录序号
                        recnum.InnerText = $"1";
                        xmlFunc.AppendChild(recnum);

                        //var bnktyp = xmlERPAYSAV.CreateElement("BNKTYP");//银行接口类型(收款银行类型)
                        //bnktyp.InnerText = $"CMB";
                        //xmlFunc.AppendChild(bnktyp);

                        var brdnbr = xmlERPAYSAV.CreateElement("BRDNBR");//银行联行号
                        brdnbr.InnerText = $"{fcnaps}";
                        xmlFunc.AppendChild(brdnbr);
                        var oprtyp = xmlERPAYSAV.CreateElement("OPRTYP");//操作类型
                        oprtyp.InnerText = $"202";//对外支付
                        xmlFunc.AppendChild(oprtyp);
                        var bustyp = xmlERPAYSAV.CreateElement("BUSTYP");//业务子类型
                        bustyp.InnerText = $"0";//标准支付
                        xmlFunc.AppendChild(bustyp);
                        var refnbr = xmlERPAYSAV.CreateElement("REFNBR");//企业参考业务号
                        refnbr.InnerText = $"{billNO}-{seq}";
                        xmlFunc.AppendChild(refnbr);
                        var cltacc = xmlERPAYSAV.CreateElement("CLTACC");//付方账号
                        cltacc.InnerText = $"{payAccount}";
                        xmlFunc.AppendChild(cltacc);
                        var cltnbr = xmlERPAYSAV.CreateElement("CLTNBR");//付方企业号
                        cltnbr.InnerText = $"0001";
                        xmlFunc.AppendChild(cltnbr);
                        var trsamt = xmlERPAYSAV.CreateElement("TRSAMT");//金额
                        trsamt.InnerText = $"{amount}";
                        xmlFunc.AppendChild(trsamt);

                        //新增的逻辑
                        var exttx1 = xmlERPAYSAV.CreateElement("EXTTX1");//摘要
                        exttx1.InnerText = $"{remake}";
                        xmlFunc.AppendChild(exttx1);

                        //if (payBankName != "" && payBankName.Contains("厦门国际银行"))//我方开户行 包含 厦门国际银行
                        //{
                        //    var exttx1 = xmlERPAYSAV.CreateElement("EXTTX1");//摘要
                        //    exttx1.InnerText = $"{remake}";
                        //    xmlFunc.AppendChild(exttx1);
                        //}
                        //else
                        //{
                        //    var exttx1 = xmlERPAYSAV.CreateElement("EXTTX1");//摘要
                        //    exttx1.InnerText = $".";
                        //    xmlFunc.AppendChild(exttx1);
                        //}

                        var trsuse = xmlERPAYSAV.CreateElement("TRSUSE");//交易用途
                        trsuse.InnerText = $"{remake}";
                        xmlFunc.AppendChild(trsuse);
                        var revacc = xmlERPAYSAV.CreateElement("REVACC");//收款人账号
                        revacc.InnerText = $"{accountInfo}";
                        xmlFunc.AppendChild(revacc);
                        var revbnk = xmlERPAYSAV.CreateElement("REVBNK");//收款人开户行
                        revbnk.InnerText = $"{accountBankName}";
                        xmlFunc.AppendChild(revbnk);
                        var revnam = xmlERPAYSAV.CreateElement("REVNAM");//收款人名称
                        revnam.InnerText = $"{accountName}";
                        xmlFunc.AppendChild(revnam);
                        var oprmod = xmlERPAYSAV.CreateElement("OPRMOD");//支付渠道  付款银行账号开通直连时，默认选择3
                        oprmod.InnerText = $"3";
                        xmlFunc.AppendChild(oprmod);
                        var paytyp = xmlERPAYSAV.CreateElement("PAYTYP");//结算方式
                        paytyp.InnerText = $"2";//转账
                        xmlFunc.AppendChild(paytyp);
                        var revprv = xmlERPAYSAV.CreateElement("REVPRV");//收方省份
                        revprv.InnerText = $"{province}";
                        xmlFunc.AppendChild(revprv);
                        var revcit = xmlERPAYSAV.CreateElement("REVCIT");//收方城市
                        revcit.InnerText = $"{city}";
                        xmlFunc.AppendChild(revcit);

                        var ciyflg = xmlERPAYSAV.CreateElement("CTYFLG");//是否同城
                        ciyflg.InnerText = $"{F_BOA_IsCity}";
                        xmlFunc.AppendChild(ciyflg);
                        var grdflg = xmlERPAYSAV.CreateElement("GRDFLG");//是否落地 
                        grdflg.InnerText = $"{F_BOA_IsLand}";
                        xmlFunc.AppendChild(grdflg);
                        var ornttn = xmlERPAYSAV.CreateElement("ORNTTN");//是否定向 
                        ornttn.InnerText = $"{F_BOA_Direction}";
                        xmlFunc.AppendChild(ornttn);
                        var payson = xmlERPAYSAV.CreateElement("PAYSON");//是否加急 
                        payson.InnerText = $"{F_BOA_Urgent}";
                        xmlFunc.AppendChild(payson);
                        var trasty = xmlERPAYSAV.CreateElement("TRASTY");//转账方式 (大小额)
                        trasty.InnerText = $"{F_BOA_SizeAmount}";
                        xmlFunc.AppendChild(trasty);

                        var createERPAYSAVXml = XmlHelper.CreateSendXml(xmlERPAYSAV.OuterXml, key, crc32_password, crc32_prefix);
                        var apiResult = WebApiHelper.ExcuteCBSApi(apiUrl, createERPAYSAVXml);
                        if (apiResult == "无法连接到远程服务器")
                        {
                            throw new KDBusinessException("500", apiResult);
                        }

                        var result = XmlHelper.GetERPAYSAVResponse(apiResult);

                        if (result == null)
                        {
                            throw new KDBusinessException("500", $"CBS接口服务异常：{apiResult}\n接口参数：{xmlERPAYSAV.OuterXml}");
                        }

                        if (result.APPAYSAVZ == null)
                        {
                            throw new KDBusinessException("500", $"CBS接口服务异常：{apiResult}\n接口参数：{xmlERPAYSAV.OuterXml}");
                        }

                        if (result.APPAYSAVZ.ERRCOD == "0000000")
                        {
                            updateList.Add($"update T_AP_PAYBILLENTRY set F_BOA_ISSYNC = 1,F_BOA_CBSNUMBER = '{result.APPAYSAVZ.BUSNBR}' where FEntryId = {entryId}");
                            var logReocdStr = $@"insert into T_BOA_CBSSyncLog (F_BOA_CREATEDATE,F_BOA_CREATORID,F_BOA_BILLTYPE,
F_BOA_SYNCRESULT,F_BOA_SYNCMSG,F_BOA_OPTYPE,
F_BOA_BILLNO,F_BOA_REFNBR,F_BOA_SEQ,
F_BOA_BUSNBR,F_BOA_ISAUDIT,F_BOA_REFNBRSEQ,F_BOA_ISAUTO)
values ('{DateTime.Now}','{this.Context.UserName}','付款单',
'成功',' ','付款单审核(经办)',
'{billNO}','{billNO}-{seq}','{entrySeq}',
'{result.APPAYSAVZ.BUSNBR}','1','{seq}','0')";
                            logRecodList.Add(logReocdStr);
                        }
                        else
                        {
                            msg += $"单据编号：{billNO},行号：{entrySeq}。错误信息：{result.APPAYSAVZ.ERRMSG}。\n接口参数：{xmlERPAYSAV.OuterXml}\n";
                            //msg += xmlERPAYSAV.OuterXml;
                        }

                        rowIndex++;
                    }
                }
            }
            if (logRecodList.Count > 0)
            {
                DBUtils.ExecuteBatch(this.Context, logRecodList, 100);
            }
            if (updateList.Count > 0)
            {
                DBUtils.ExecuteBatch(this.Context, updateList, 100);
            }
            if (msg != string.Empty)
            {
                throw new KDBusinessException("500", msg);
            }
        }
    }
}
