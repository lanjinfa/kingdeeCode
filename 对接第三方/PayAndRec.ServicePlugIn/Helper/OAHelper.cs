using Kingdee.BOS.App.Data;
using Kingdee.BOS.Log;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace BOA.YZW.PayAndRec.ServicePlugIn.Helper
{
    /// <summary>
    /// 泛微OA接口调用
    /// </summary>
    public static class OAHelper
    {
        /// <summary>
        /// 接口注册认证并返回token
        /// </summary>
        /// <param name="apiResult">接口是否调用成功</param>
        /// <param name="spk">公钥信息</param>
        /// <returns></returns>
        public static string RegistAndGetToken(out bool apiResult, out string spk)
        {
            apiResult = true;
            spk = "";
            //向OA系统发送许可证信息进行注册认证
            var urlReg = "https://oa.com/api/ec/dev/auth/regist";
            var resultReg = WebApiHelper.ExcuteOAApi(urlReg, "JDAA5436-7577-4BE0-8C6C-89E9D88805EA");
            var regResult = JsonConvert.DeserializeObject<OADTO>(resultReg);
            if (regResult.Status.EqualsIgnoreCase("false"))
            {
                apiResult = false;
                Logger.Debug("泛微OA-注册认证", resultReg);
                return "";
            }
            spk = regResult.Spk;
            //向OA系统发送获取token请求
            var rsaSecret = RSAEncrypt(regResult.Secrit, regResult.Spk);//加密
            var urlToken = "https://oa.com/api/ec/dev/auth/applytoken";
            var getTokenResult = WebApiHelper.ExcuteOAApi(urlToken,
                                   "JDAA5436-7577-4BE0-8C6C-89E9D88805EA", rsaSecret);
            var tokenResult = JsonConvert.DeserializeObject<OADTO>(getTokenResult);
            if (tokenResult.Status.EqualsIgnoreCase("false"))
            {
                apiResult = false;
                Logger.Debug("泛微OA-获取token", getTokenResult);
                return "";
            }
            return tokenResult.Token;
        }

        /// <summary>
        /// 调用泛微OA提交接口
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="dy">付款单信息</param>
        /// <param name="payTime">支付日期</param>
        /// <returns></returns>
        public static void Submit(Kingdee.BOS.Context context, DynamicObject dy, string payTime)
        {
            var payTimeStr = Convert.ToDateTime(payTime).ToString("yyyy-MM-dd");
            var id = dy["FID"].ToString();//付款单内码
            var billNo = dy["FBILLNO"].ToString();//付款单单据编号
            var updataSql = $"update T_AP_PAYBILL set FDATE = '{payTimeStr}' where FID = {id}";
            DBUtils.Execute(context, updataSql);
            Logger.Debug($"CBS-{billNo}", "支付成功修改付款单业务日期为支付日期");
            var userId = dy["F_PAEZ_OAUserID"].ToString().Trim();//OA出纳人员ID
            var requestId = dy["F_PAEZ_OARequest"].ToString().Trim();//OA流程
            if (userId == "" || requestId == "")
            {
                Logger.Debug($"泛微OA-提交流程[{billNo}]", "OA出纳人员ID为空或OA流程为空");
                return;
            }
            var token = RegistAndGetToken(out bool apiResult, out string spk);
            if (apiResult)
            {
                var url = "https://oa.com/api/workflow/paService/submitRequest";

                var remark = "CBS支付成功，流程自动归档";
                var jsonStr = $"remark={remark}&requestId={requestId}";

                var useIdStr = RSAEncrypt(userId, spk);
                var result = WebApiHelper.ExcuteOAApi(url,
                 appid: "JDAA5436-7577-4BE0-8C6C-89E9D88805EA", token: token, postData: jsonStr, userId: useIdStr);
                Logger.Debug($"泛微OA-提交流程[{billNo}]", result);
            }
        }

        /// <summary>
        /// 加密
        /// </summary>
        /// <param name="data">加密内容</param>
        /// <param name="keyStr">公钥(pem)</param>
        /// <returns>加密结果</returns>
        private static string RSAEncrypt(string data, string keyStr)
        {
            var publicKeyParam = (RsaKeyParameters)PublicKeyFactory.CreateKey(Convert.FromBase64String(keyStr));
            string result = string.Empty;
            var rsacryptoServiceProvider = new RSACryptoServiceProvider();
            var modeuls = Convert.ToBase64String(publicKeyParam.Modulus.ToByteArrayUnsigned());
            var exponent = Convert.ToBase64String(publicKeyParam.Exponent.ToByteArrayUnsigned());
            var rsaStr = $"<RSAKeyValue><Modulus>{modeuls}</Modulus><Exponent>{exponent}</Exponent></RSAKeyValue>";
            rsacryptoServiceProvider.FromXmlString(rsaStr);
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            int num = rsacryptoServiceProvider.KeySize / 8;
            int num2 = num - 11;
            byte[] array = new byte[num2];
            using (MemoryStream memoryStream = new MemoryStream(bytes))
            {
                using (MemoryStream memoryStream2 = new MemoryStream())
                {
                    for (int i = memoryStream.Read(array, 0, num2); i > 0; i = memoryStream.Read(array, 0, num2))
                    {
                        byte[] array2 = new byte[i];
                        Array.Copy(array, 0, array2, 0, i);
                        byte[] array3 = rsacryptoServiceProvider.Encrypt(array2, false);
                        memoryStream2.Write(array3, 0, array3.Length);
                    }
                    byte[] inArray = memoryStream2.ToArray();
                    result = Convert.ToBase64String(inArray);
                }
            }
            rsacryptoServiceProvider.Clear();
            return result;
        }
    }

    /// <summary>
    /// OA接口返回值
    /// </summary>
    public class OADTO
    {
        /// <summary>
        /// 响应状态。true:成功,false:失败
        /// </summary>
        public string Status { get; set; } = "";

        /// <summary>
        /// 响应码。0代表成功
        /// </summary>
        public string Code { get; set; } = "";

        /// <summary>
        /// 错误码。0代表成功（可忽略）
        /// </summary>
        public string ErrCode { get; set; } = "";

        /// <summary>
        /// 响应信息
        /// </summary>
        public string Msg { get; set; } = "";

        /// <summary>
        /// 信息显示类型。默认“none”
        /// </summary>
        public string MsgShowType { get; set; } = "";

        /// <summary>
        /// 秘钥信息。注意此处secrit单词拼写错误（原词为：secret），请使用 secrit获取结果
        /// </summary>
        public string Secrit { get; set; } = "";

        /// <summary>
        /// 系统公钥信息
        /// </summary>
        public string Spk { get; set; } = "";

        /// <summary>
        /// 认证通过的token信息。（默认30分钟内有效）
        /// </summary>
        public string Token { get; set; } = "";
    }
}
