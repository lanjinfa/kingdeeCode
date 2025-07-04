using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.YD.JYFX.PlugIns.Helpers
{
    /// <summary>
    /// 接口返回值
    /// </summary>
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
    /// 物料分组返回值
    /// </summary>
    public class GroupNeedReturnData
    {
        public string FID { get; set; }

        public string FNUMBER { get; set; }

        public string FGROUPID { get; set; }

        public string FPARENTID { get; set; }

        public string FFULLPARENTID { get; set; }
    }
}
