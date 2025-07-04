using Kingdee.BOS;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.YD.JYFX.PlugIns.Helpers
{
    public static class BillQueryHelper
    {
        /// <summary>
        /// 根据单据内码集合获取单据列表
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="billTypeId">单据标识</param>
        /// <param name="billId">单据内码集合</param>
        /// <returns></returns>
        public static DynamicObject[] GetBillListByIdList(Context context, string billTypeId, object[] billId)
        {
            var formMetadata = MetaDataServiceHelper.Load(context, billTypeId) as FormMetadata;
            return BusinessDataServiceHelper.Load(context, billId, formMetadata.BusinessInfo.GetDynamicObjectType(true));
        }
    }
}
