using Kingdee.BOS;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Kingdee.BOS.WebApi.Client;
using Kingdee.BOS.WebApi.FormService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace BOA.YZW.PayAndRec.ServicePlugIn.Helper
{
    public static class WebApiHelper
    {
        /// <summary>
        /// 调用CBS接口
        /// </summary>
        /// <param name="posturl">接口地址</param>
        /// <param name="postData">接口数据</param>
        /// <returns></returns>
        public static string ExcuteCBSApi(string posturl, string postData)
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            Stream outstream = null;
            Stream instream = null;
            StreamReader sr = null;
            HttpWebResponse response = null;
            HttpWebRequest request = null;
            //byte[] data = Encoding.UTF8.GetBytes(postData);
            byte[] data = Encoding.GetEncoding("GBK").GetBytes(postData);
            // 准备请求...
            try
            {
                // 设置参数
                if (posturl.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    ServicePointManager.ServerCertificateValidationCallback =
                    new RemoteCertificateValidationCallback(CheckValidationResult);
                    request = WebRequest.Create(posturl) as HttpWebRequest;
                    request.ProtocolVersion = HttpVersion.Version10;
                    request.KeepAlive = false;
                    ServicePointManager.CheckCertificateRevocationList = true;
                    ServicePointManager.DefaultConnectionLimit = 100;
                    ServicePointManager.Expect100Continue = false;
                }
                else
                {
                    request = WebRequest.Create(posturl) as HttpWebRequest;
                }
                CookieContainer cookieContainer = new CookieContainer();
                request.CookieContainer = cookieContainer;
                request.AllowAutoRedirect = true;
                request.Method = "POST";
                //request.ContentType = "application/json";
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36";
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";
                request.ContentLength = data.Length;
                // request.TransferEncoding = encoding.HeaderName; 
                outstream = request.GetRequestStream();
                outstream.Write(data, 0, data.Length);
                outstream.Close();
                //发送请求并获取相应回应数据
                response = request.GetResponse() as HttpWebResponse;
                //直到request.GetResponse()程序才开始向目标网页发送Post请求
                instream = response.GetResponseStream();
                sr = new StreamReader(instream, Encoding.GetEncoding("GBK"));
                //返回结果网页（html）代码
                string content = sr.ReadToEnd();
                //Logger.Error("ZJ001", "LJF同步5", new KDBusinessException("", $"{content}"));
                //string err = string.Empty;
                response.Close();
                response = null;
                request = null;
                return content;
                //var r1 = JObject.Parse(content);
                //Logger.Error("ZJ001", "LJF同步6", new KDBusinessException("", $"{r1}"));
                //return new ExcutewljsDataResult { ErrCode = "", ResponseContext = r1["data"].ToString() };
            }
            catch (Exception ex)
            {
                string err = ex.Message;
                //Logger.Error("ZJ001", "LJF同步7", new KDBusinessException("", $"{err}"));
                //return new ExcutewljsDataResult { ErrCode = "501", ResponseContext = err };
                return err;
            }
        }

        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true; //总是接受  
        }

        /// <summary>
        /// 调用金蝶系统批量保存接口
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="parameter">参数</param>
        /// <param name="formId">单据标识</param>
        /// <param name="isDeleteEntry">是否删除分录</param>
        /// <param name="isAutoSubmitAndAudit">是否自动审核</param>
        /// <param name="needReturnFields">需返回结果的字段集合</param>
        /// <returns></returns>
        public static bool ExcuteBatchSaveOperate(Context context, JArray parameter, string formId, bool isDeleteEntry = true,
                                                                           bool isAutoSubmitAndAudit = false, JArray needReturnFields = null)
        {
            var model = new JObject
            {
                ["NeedReturnFields"] = needReturnFields ?? new JArray(),
                ["IsDeleteEntry"] = isDeleteEntry,
                ["IsAutoSubmitAndAudit"] = isAutoSubmitAndAudit,
                ["Model"] = parameter
            };
            var jsonStr = JsonConvert.SerializeObject(model);
            var newContext = ObjectUtils.CreateCopy(context) as Context;
            var result = WebApiServiceCall.BatchSave(newContext, formId, jsonStr);
            bool isSuccessed = ((dynamic)result)["Result"]["ResponseStatus"]["IsSuccess"];
            return isSuccessed;
        }

        /// <summary>
        /// 单据暂存接口
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="parameter">参数</param>
        /// <param name="formId">单据标识</param>
        /// <returns>单据内码</returns>
        public static string ExcuteDraftOperate(Context context, JObject parameter, string formId, out string paraStr)
        {
            var model = new JObject
            {
                ["Model"] = parameter
            };
            var jsonStr = JsonConvert.SerializeObject(model);
            var newContext = ObjectUtils.CreateCopy(context) as Context;
            var result = WebApiServiceCall.Draft(newContext, formId, jsonStr);
            paraStr = jsonStr;
            return ((dynamic)result)["Result"]["Id"].ToString();
        }

        /// <summary>
        ///  调用金蝶系统保存接口
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="parameter">参数</param>
        /// <param name="formId">单据标识</param>
        /// <param name="isDeleteEntry">是否删除分录</param>
        /// <param name="isAutoSubmitAndAudit">是否自动提交审核</param>
        /// <param name="needReturnFields">需返回结果的字段集合</param>
        /// <returns></returns>
        public static ApiResult ExcuteSaveOperate(out string jsonStr1, Context context, JObject parameter, string formId, bool isDeleteEntry = true,
              bool isAutoSubmitAndAudit = false, JArray needReturnFields = null)
        {
            var model = new JObject
            {
                ["NeedReturnFields"] = needReturnFields ?? new JArray(),
                ["IsDeleteEntry"] = isDeleteEntry,
                ["IsAutoSubmitAndAudit"] = isAutoSubmitAndAudit,
                //[""] = true,
                ["Model"] = parameter
            };
            var jsonStr = JsonConvert.SerializeObject(model);
            jsonStr1 = jsonStr;
            var newContext = ObjectUtils.CreateCopy(context) as Context;
            var result = WebApiServiceCall.Save(newContext, formId, jsonStr);
            bool isSuccessed = ((dynamic)result)["Result"]["ResponseStatus"]["IsSuccess"];
            return new ApiResult
            {
                IsSuccessed = isSuccessed,
                ErrorResult = isSuccessed == false ? ((dynamic)result)["Result"]["ResponseStatus"]["Errors"][0]["Message"] : "",
                //Id = isSuccessed ? ((dynamic)result)["Result"]["Id"] : 0,
                Number = isSuccessed ? ((dynamic)result)["Result"]["Number"] : "",
                //NeedReturnData = isSuccessed ? ((dynamic)result)["Result"]["NeedReturnData"] : null
            };
        }

        /// <summary>
        /// WebApi登录授权(test环境的信息)
        /// </summary>
        /// <param name="loginInfo">登陆信息</param>
        /// <returns>登录状态</returns>
        public static LoginStatus Login()
        {
            var dbId = "d7ef8209cbf";// loginInfo["F_BOA_ACCOUNTID"].ToString();//账套Id
            var userName = "Administrator";// loginInfo["F_BOA_USERNAME"].ToString();//用户名
            var passWord = "2082";// loginInfo["F_BOA_PASSWORD"].ToString();//密码
            var apiUrl = "http://win/K3Cloud/";// loginInfo["F_BOA_APIURL"].ToString();//api地址
            var client = new K3CloudApiClient(apiUrl, 3000);
            var loginResult = client.ValidateLogin(dbId, userName, passWord, 2052);
            var resultType = JObject.Parse(loginResult)["LoginResultType"].Value<int>();
            return new LoginStatus
            {
                ResultType = resultType,
                Client = client
            };
        }

        /// <summary>
        /// WebApi登录授权(正式环境的信息)
        /// </summary>
        /// <param name="loginInfo">登陆信息</param>
        /// <returns>登录状态</returns>
        public static LoginStatus Login1()
        {
            var dbId = "63a1e1b385e";// loginInfo["F_BOA_ACCOUNTID"].ToString();//账套Id
            var userName = "Administrator";// loginInfo["F_BOA_USERNAME"].ToString();//用户名
            var passWord = "2082";// loginInfo["F_BOA_PASSWORD"].ToString();//密码
            var apiUrl = "http://192/k3cloud/";// loginInfo["F_BOA_APIURL"].ToString();//api地址
            var client = new K3CloudApiClient(apiUrl, 3000);
            var loginResult = client.ValidateLogin(dbId, userName, passWord, 2052);
            var resultType = JObject.Parse(loginResult)["LoginResultType"].Value<int>();
            return new LoginStatus
            {
                ResultType = resultType,
                Client = client
            };
        }


        /// <summary>
        /// 保存
        /// </summary>
        /// <returns></returns>
        public static SyncApiResult Save(K3CloudApiClient client, string billType, string jsonStr)
        {
            var result = client.Save(billType, jsonStr);//保存
            var responseStatus = ConvertResponseStatus(result);
            return responseStatus;
        }

        /// <summary>
        /// 暂存
        /// </summary>
        /// <returns></returns>
        public static SyncApiResult Draft(K3CloudApiClient client, string billType, string jsonStr)
        {
            var result = client.Draft(billType, jsonStr);//暂存
            var responseStatus = ConvertResponseStatus(result);
            return responseStatus;
        }

        /// <summary>
        /// 接口返回值转化为对应的类
        /// </summary>
        /// <returns></returns>
        public static SyncApiResult ConvertResponseStatus(string result)
        {
            var responseResult = JObject.Parse(result)["Result"]["ResponseStatus"].ToString();
            var responseStatus = JsonConvert.DeserializeObject<ResponseStatus>(responseResult);
            var id = responseStatus.IsSuccess ? JObject.Parse(result)["Result"]["Id"].ToString() : "";
            var number = responseStatus.IsSuccess ? JObject.Parse(result)["Result"]["Number"].ToString() : "";
            var needReturnDataStr = responseStatus.IsSuccess ? JObject.Parse(result)["Result"]["NeedReturnData"].ToString() : "";
            return new SyncApiResult
            {
                Id = id,
                Number = number,
                NeedReturnData = needReturnDataStr,
                ResponseStatus = responseStatus
            };
        }

        /// <summary>
        /// 调用泛微OA接口
        /// </summary>
        /// <param name="posturl">接口地址</param>
        /// <param name="appid">许可证号码</param>
        /// <param name="secret">密钥信息</param>
        /// <param name="token">token信息</param>
        /// <param name="postData">请求参数</param>
        /// <param name="userId">用户id</param>
        /// <returns></returns>
        public static string ExcuteOAApi(string posturl, string appid = "", string secret = "",
                                   string token = "", string postData = "", string userId = "")
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
            Stream outstream = null;
            Stream instream = null;
            StreamReader sr = null;
            HttpWebResponse response = null;
            HttpWebRequest request = null;
            byte[] data = Encoding.UTF8.GetBytes(postData);
            //byte[] data = Encoding.GetEncoding("GBK").GetBytes(postData);
            // 准备请求...
            try
            {
                // 设置参数
                if (posturl.StartsWith("https", StringComparison.OrdinalIgnoreCase))
                {
                    ServicePointManager.ServerCertificateValidationCallback =
                    new RemoteCertificateValidationCallback(CheckValidationResult);
                    request = WebRequest.Create(posturl) as HttpWebRequest;
                    request.ProtocolVersion = HttpVersion.Version10;
                    request.KeepAlive = false;
                    ServicePointManager.CheckCertificateRevocationList = true;
                    ServicePointManager.DefaultConnectionLimit = 100;
                    ServicePointManager.Expect100Continue = false;
                }
                else
                {
                    request = WebRequest.Create(posturl) as HttpWebRequest;
                }
                CookieContainer cookieContainer = new CookieContainer();
                request.CookieContainer = cookieContainer;
                request.AllowAutoRedirect = true;
                request.Method = "POST";
                //request.ContentType = "application/json";
                request.ContentType = "application/x-www-form-urlencoded";
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/83.0.4103.61 Safari/537.36";
                request.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.9";

                if (appid != "")
                {
                    request.Headers.Add("appid", appid);
                }
                if (secret != "")
                {
                    request.Headers.Add("secret", secret);
                }
                if (token != "")
                {
                    request.Headers.Add("token", token);
                }
                if (userId != "")
                {
                    request.Headers.Add("userid", userId);
                }

                if (postData != "")
                {
                    request.ContentLength = data.Length;
                }

                // request.TransferEncoding = encoding.HeaderName; 
                outstream = request.GetRequestStream();
                if (postData != "")
                {
                    outstream.Write(data, 0, data.Length);
                }
                outstream.Close();
                //发送请求并获取相应回应数据
                response = request.GetResponse() as HttpWebResponse;
                //直到request.GetResponse()程序才开始向目标网页发送Post请求
                instream = response.GetResponseStream();
                sr = new StreamReader(instream, Encoding.UTF8);
                //返回结果网页（html）代码
                string content = sr.ReadToEnd();
                //Logger.Error("ZJ001", "LJF同步5", new KDBusinessException("", $"{content}"));
                //string err = string.Empty;
                response.Close();
                response = null;
                request = null;
                return content;
                //var r1 = JObject.Parse(content);
                //Logger.Error("ZJ001", "LJF同步6", new KDBusinessException("", $"{r1}"));
                //return new ExcutewljsDataResult { ErrCode = "", ResponseContext = r1["data"].ToString() };
            }
            catch (Exception ex)
            {
                string err = ex.Message;
                //Logger.Error("ZJ001", "LJF同步7", new KDBusinessException("", $"{err}"));
                //return new ExcutewljsDataResult { ErrCode = "501", ResponseContext = err };
                return err;
            }
        }
    }
}
