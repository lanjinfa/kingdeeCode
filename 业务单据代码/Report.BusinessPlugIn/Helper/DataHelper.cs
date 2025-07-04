using Kingdee.BOS;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using System.Linq;

namespace BOA.DJJX.Report.BusinessPlugIn.Helper
{
    /// <summary>
    /// 获取基础资料数据包
    /// </summary>
    public static class DataHelper
    {
        /// <summary>
        /// 获取表单原数据对象
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="billType">单据标识</param>
        /// <returns></returns>
        public static FormMetadata GetFormMetaData(Context context, string billType)
        {
            return MetaDataServiceHelper.Load(context, billType) as FormMetadata;
        }

        /// <summary>
        ///根据内码获取数据
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="formMetadata">表单原数据对象</param>
        /// <param name="billId">单据内码</param>
        /// <returns></returns>
        public static DynamicObject GetDataById(Context context, FormMetadata formMetadata, object billId)
        {
            var dt = formMetadata.BusinessInfo.GetDynamicObjectType();
            IViewService ivs = Kingdee.BOS.App.ServiceHelper.GetService<IViewService>();
            return ivs.LoadSingle(context, billId, dt);
            //var list = BusinessDataServiceHelper.Load(context, new object[] { billId }, formMetadata.BusinessInfo.GetDynamicObjectType(true));
            //return list.Count() == 0 ? null : list[0];
        }
    }
}
