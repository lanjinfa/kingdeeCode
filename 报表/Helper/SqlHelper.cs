using Kingdee.BOS;
using Kingdee.BOS.App.Data;
using Kingdee.BOS.Orm.DataEntity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.SBT.Report.PlugIns.Helper
{
    public static class SqlHelper
    {
        /// <summary>
        /// 获取组织名称
        /// </summary>
        /// <param name="context"></param>
        /// <param name="orgIdStr"></param>
        /// <returns></returns>
        public static string GetOrgNames(Context context, string orgIdStr)
        {
            var sqlStr = $"select fname from T_ORG_Organizations_L where forgId in ({orgIdStr}) and flocaleid = 2052";
            var result = DBUtils.ExecuteDynamicObject(context, sqlStr);
            return string.Join(",", result.Select(t => t["fname"]));
        }

        /// <summary>
        /// 获取用户组权限
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static DynamicObjectCollection GetUserAuthor(Context context)
        {
            var sqlStr = "exec CitySpark_GetUserGroupInfo";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }

        /// <summary>
        /// 获取业务模式列表
        /// 只获取 材料出口 和 设备出口
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static DynamicObjectCollection GetASSISTANTDATAENTRY(Context context)
        {
            var sqlStr = $@"select FENTRYID
from T_BAS_ASSISTANTDATAENTRY_L 
where FDATAVALUE in ('材料出口','设备出口') and FLOCALEID = 2052";
            return DBUtils.ExecuteDynamicObject(context, sqlStr);
        }
    }
}
