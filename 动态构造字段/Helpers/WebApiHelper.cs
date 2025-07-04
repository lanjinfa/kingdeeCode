using BOA.YD.JYFX.PlugIns.Dtos;
using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Authentication;
using Kingdee.BOS.Core.CommonFilter.ConditionVariableAnalysis;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceFacade.KDServiceClient.User;
using Kingdee.BOS.Util;
using Kingdee.BOS.WebApi.Client;
using Kingdee.BOS.WebApi.FormService;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BOA.YD.JYFX.PlugIns.Helpers
{
    /// <summary>
    /// 接口帮助类
    /// </summary>
    public static class WebApiHelper
    {
        /// <summary>
        /// WebApi登录授权
        /// </summary>
        /// <param name="loginInfo">登陆信息</param>
        /// <returns>登录状态</returns>
        public static LoginStatus Login(DynamicObject loginInfo)
        {
            var dbId = loginInfo["F_BOA_ACCOUNTID"].ToString();//账套Id
            var userName = loginInfo["F_BOA_USERNAME"].ToString();//用户名
            var passWord = loginInfo["F_BOA_PASSWORD"].ToString();//密码
            var apiUrl = loginInfo["F_BOA_APIURL"].ToString();//api地址
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
        ///  调用金蝶系统保存接口
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="parameter">参数</param>
        /// <param name="formId">单据标识</param>
        /// <param name="isDeleteEntry">是否删除分录</param>
        /// <param name="isAutoSubmitAndAudit">是否自动提交审核</param>
        /// <param name="needReturnFields">需返回结果的字段集合</param>
        /// <returns></returns>
        public static ApiResult ExcuteSaveOperate(Context context, JObject parameter, string formId, bool isDeleteEntry = true,
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
            var result = WebApiServiceCall.Save(newContext, formId, jsonStr);
            bool isSuccessed = ((dynamic)result)["Result"]["ResponseStatus"]["IsSuccess"];
            return new ApiResult
            {
                IsSuccessed = isSuccessed,
                ErrorResult = isSuccessed == false ? ((dynamic)result)["Result"]["ResponseStatus"]["Errors"][0]["Message"] : "",
                Id = isSuccessed ? ((dynamic)result)["Result"]["Id"] : 0,
                Number = isSuccessed ? ((dynamic)result)["Result"]["Number"] : "",
                NeedReturnData = isSuccessed ? ((dynamic)result)["Result"]["NeedReturnData"] : null
            };
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
        public static void ExcuteBatchSaveOperate(Context context, JArray parameter, string formId, bool isDeleteEntry = true,
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
            //bool isSuccessed = ((dynamic)result)["Result"]["ResponseStatus"]["IsSuccess"];
            //return new ApiResult
            //{
            //    IsSuccessed = isSuccessed,
            //    ErrorResult = isSuccessed == false ? ((dynamic)result)["Result"]["ResponseStatus"]["Errors"][0]["Message"] : "",
            //    Id = isSuccessed ? ((dynamic)result)["Result"]["Id"] : 0,
            //    Number = isSuccessed ? ((dynamic)result)["Result"]["Number"] : "",
            //    NeedReturnData = isSuccessed ? ((dynamic)result)["Result"]["NeedReturnData"] : null
            //};
        }

        /// <summary>
        /// 调用金蝶删除接口
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="ids">内码集合</param>
        /// <param name="billTypeId">单据类型</param>
        public static object ExcuteBatchDeleteOperate(Context context, string ids, string billTypeId)
        {
            var model = new JObject
            {
                ["Ids"] = ids
            };
            var jsonStr = JsonConvert.SerializeObject(model);
            var newContext = ObjectUtils.CreateCopy(context) as Context;
            return WebApiServiceCall.Delete(newContext, billTypeId, jsonStr);
        }

        /// <summary>
        /// 单张单据同步(保存，提交，审核)
        /// </summary>
        /// <param name="client">接口客户端</param>
        /// <param name="billType">源单类型</param>
        /// <param name="billStatus">单据状态1、创建 2、提交 3、审核</param>
        /// <param name="model">json参数</param>
        /// <returns></returns>
        public static SyncApiResult SingleSubmit(K3CloudApiClient client, string billType, int billStatus, JObject model, JArray needReturnFields = null)
        {
            //var result = string.Empty;//保存接口返回值
            var responseStatus = new SyncApiResult();
            var fields = new JObject
            {
                ["IsVerifyBaseDataField"] = "true",//是否验证所有的基础资料有效性
                ["NeedReturnFields"] = needReturnFields ?? new JArray(),
                ["Model"] = model
            };
            var jsonStr = JsonConvert.SerializeObject(fields);
            var result = client.Save(billType, jsonStr);//保存
            //单据同步状态
            if (billStatus == 1)//保存
            {
                responseStatus = ConvertResponseStatus(result);
            }
            else if (billStatus == 2)//提交
            {
                responseStatus = ConvertResponseStatus(result);
                if (responseStatus.ResponseStatus.IsSuccess)
                {
                    var billId = responseStatus.ResponseStatus.SuccessEntitys[0].Id;
                    var parameter = SingleSumitOrAuditJson(billId);
                    var submitResult = client.Submit(billType, parameter);
                    var isSuccess = IsSubmitOrAuditSuccess(submitResult);
                    if (!isSuccess.IsSuccess)
                    {
                        responseStatus.ResponseStatus = isSuccess;
                        //提交失败，删除数据
                        client.Delete(billType, parameter);
                    }
                }
            }
            else if (billStatus == 3)//审核
            {
                responseStatus = ConvertResponseStatus(result);
                if (responseStatus.ResponseStatus.IsSuccess)
                {
                    var billId = responseStatus.ResponseStatus.SuccessEntitys[0].Id;
                    var parameter = SingleSumitOrAuditJson(billId);
                    var submitResult = client.Submit(billType, parameter);
                    var submitSuccess = IsSubmitOrAuditSuccess(submitResult);
                    if (submitSuccess.IsSuccess)
                    {
                        var auditResult = client.Audit(billType, parameter);
                        var auditSuccess = IsSubmitOrAuditSuccess(auditResult);
                        if (!auditSuccess.IsSuccess)
                        {
                            responseStatus.ResponseStatus = auditSuccess;
                            //审核失败，删除数据
                            client.UnAudit(billType, parameter);
                            client.Delete(billType, parameter);
                        }
                    }
                    else
                    {
                        responseStatus.ResponseStatus = submitSuccess;
                        //提交失败，删除数据
                        client.Delete(billType, parameter);
                    }
                }
            }
            responseStatus.JsonStr = jsonStr;
            return responseStatus;
        }

        /// <summary>
        /// 单张单据提交或审核参数转换
        /// </summary>
        /// <param name="billId"></param>
        /// <returns></returns>
        public static string SingleSumitOrAuditJson(string billId)
        {
            var parameter = new JObject
            {
                ["InterationFlags"] = "STK_InvCheckResult;",
                ["Ids"] = billId
            };
            return JsonConvert.SerializeObject(parameter);
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
        /// 判断是否提交审核成功
        /// </summary>
        /// <param name="result"></param>
        /// <returns></returns>
        public static ResponseStatus IsSubmitOrAuditSuccess(string result)
        {
            var responseResult = JObject.Parse(result)["Result"]["ResponseStatus"].ToString();
            var responseStatus = JsonConvert.DeserializeObject<ResponseStatus>(responseResult);
            return responseStatus;
        }

        /// <summary>
        /// 下推操作
        /// </summary>
        /// <param name="client">接口客户端</param>
        /// <param name="billType">源单类型</param>
        /// <param name="entryIdStr">分录内码</param>
        /// <param name="ruleId">转换规则内码</param>
        /// <returns></returns>
        public static PushResultDto SinglePush(K3CloudApiClient client, string billType, string entryIdStr,
            string ruleId)
        {
            var fields = new JObject
            {
                ["EntryIds"] = entryIdStr,
                ["RuleId"] = ruleId,
                ["IsDraftWhenSaveFail"] = true
            };
            var jsonStr = JsonConvert.SerializeObject(fields);
            var result = client.Push(billType, jsonStr);
            var isSuccess = JObject.Parse(result)["Result"]["ResponseStatus"]["IsSuccess"].ToString();
            var pushResultDto = new PushResultDto();
            pushResultDto.IsSuccess = isSuccess == "True";
            if (isSuccess == "True")
            {
                var number = JObject.Parse(result)["Result"]["ResponseStatus"]["SuccessEntitys"][0]["Number"].ToString();
                pushResultDto.Number = number;
                var id = JObject.Parse(result)["Result"]["ResponseStatus"]["SuccessEntitys"][0]["Id"].ToString();
                pushResultDto.Id = id;
            }
            else
            {
                pushResultDto.Msg = JObject.Parse(result)["Result"]["ResponseStatus"]["Errors"][0]["Message"].ToString();
                pushResultDto.JsonStr = jsonStr;
            }
            return pushResultDto;
        }

        /// <summary>
        /// 单据查询
        /// </summary>
        /// <param name="client">接口客户端</param>
        /// <param name="billType">单据类型</param>
        /// <param name="fieldkeys">需要返回的字段</param>
        /// <param name="filterStr">过滤条件</param>
        /// <returns></returns>
        public static List<List<object>> SingleQuery(K3CloudApiClient client, string billType, string fieldkeys, JArray filterStr)
        {
            var filterModel = new JObject();
            filterModel["FormId"] = billType;
            filterModel["FieldKeys"] = fieldkeys;
            filterModel["FilterString"] = filterStr;
            var jsonStr = JsonConvert.SerializeObject(filterModel);
            var result = client.ExecuteBillQuery(jsonStr);
            return result;
        }
    }
}
