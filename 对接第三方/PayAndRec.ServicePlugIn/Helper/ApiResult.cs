using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.YZW.PayAndRec.ServicePlugIn.Helper
{
    public class ApiResult
    {
        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccessed { get; set; }

        /// <summary>
        /// 失败的结果
        /// </summary>
        public string ErrorResult { get; set; }

        /// <summary>
        /// 成功的实体内码
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 单据编号
        /// </summary>
        public string Number { get; set; }

        /// <summary>
        /// 返回结果的字段集合
        /// </summary>
        public dynamic NeedReturnData { get; set; }
    }

    /// <summary>
    /// 接口返回结果
    /// </summary>
    public class SyncApiResult
    {
        /// <summary>
        /// 单据内码
        /// </summary>
        public string Id { get; set; } = "0";

        /// <summary>
        /// 单据编号
        /// </summary>
        public string Number { get; set; } = "";

        /// <summary>
        /// 需要返回的字段
        /// </summary>
        public string NeedReturnData { get; set; } = "";

        /// <summary>
        /// 响应状态
        /// </summary>
        public ResponseStatus ResponseStatus { get; set; } = new ResponseStatus();

        /// <summary>
        /// 同步的参数
        /// </summary>
        public string JsonStr { get; set; } = "";
    }

    /// <summary>
    /// 响应状态
    /// </summary>
    public class ResponseStatus
    {
        /// <summary>
        /// 错误码
        /// </summary>
        public string ErrorCode { get; set; } = "";

        /// <summary>
        /// 是否成功
        /// </summary>
        public bool IsSuccess { get; set; } = false;

        /// <summary>
        /// 错误集合
        /// </summary>
        public List<Errors> Errors { get; set; } = new List<Errors>();

        /// <summary>
        /// 成功实体
        /// </summary>
        public List<SuccessEntitys> SuccessEntitys { get; set; } = new List<SuccessEntitys>();

        /// <summary>
        /// 成功信息
        /// </summary>
        public List<SuccessMessages> SuccessMessages { get; set; } = new List<SuccessMessages>();

        /// <summary>
        /// 信息码
        /// </summary>
        public string MsgCode { get; set; } = "";
    }

    /// <summary>
    /// 错误
    /// </summary>
    public class Errors
    {
        /// <summary>
        /// 字段名
        /// </summary>
        public string FieldName { get; set; } = "";

        /// <summary>
        /// 信息
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// 索引
        /// </summary>
        public int DIndex { get; set; } = 0;
    }

    /// <summary>
    /// 成功实体
    /// </summary>
    public class SuccessEntitys
    {
        /// <summary>
        /// 单据内码
        /// </summary>
        public string Id { get; set; } = "0";

        /// <summary>
        /// 单据编码
        /// </summary>
        public string Number { get; set; } = "";

        /// <summary>
        /// 索引
        /// </summary>
        public int DIndex { get; set; } = 0;
    }

    /// <summary>
    /// 成功信息
    /// </summary>
    public class SuccessMessages
    {
        /// <summary>
        /// 字段名称
        /// </summary>
        public string FieldName { get; set; } = "";

        /// <summary>
        /// 信息
        /// </summary>
        public string Message { get; set; } = "";

        /// <summary>
        /// 索引
        /// </summary>
        public int DIndex { get; set; } = 0;
    }
}
