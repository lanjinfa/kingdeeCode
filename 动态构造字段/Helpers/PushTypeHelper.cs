using Kingdee.BOS;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.ConvertElement;
using Kingdee.BOS.Core.SqlBuilder;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BOA.YD.JYFX.PlugIns.Helpers
{
    //单据类型
    public static class PushTypeHelper
    {
        public static string GetBillTypeID(Context ctx, ConvertRuleElement rule, string sourceFormId, string fid)
        {
            string targetBillTypeId = string.Empty;
            try
            {
                var plics = rule.Policies;
                BillTypeMapPolicyElement em = new BillTypeMapPolicyElement();
                foreach (var item in plics)
                {
                    string typename = item.ObjToStr();
                    if (typename == "_单据类型映射")
                    {
                        em = item as BillTypeMapPolicyElement;
                    }
                }
                //BillTypeMapPolicyElement em = rule.Policies[1] as BillTypeMapPolicyElement;
                var billTypeMaps = em.BillTypeMaps;
                if (billTypeMaps.Count > 0)
                {
                    try
                    {
                        DynamicObjectCollection billinfo = GetQueryDatas(ctx, sourceFormId,
                        new string[] { "FBILLTYPEID" }, " fid=" + fid);
                        if (billinfo.Count > 0)
                        {
                            if (billTypeMaps.Count == 1)
                            {
                                targetBillTypeId = billTypeMaps[0].TargetBillTypeId;
                            }
                            else
                            {
                                var d3 = billTypeMaps.Where(x => x.SourceBillTypeId == billinfo[0]["FBILLTYPEID"].ObjToStr()).ToList();
                                if (d3.Count > 0)
                                {
                                    targetBillTypeId = d3[0].TargetBillTypeId;
                                }
                            }
                            if (string.IsNullOrEmpty(targetBillTypeId))
                            {
                                targetBillTypeId = billTypeMaps[0].TargetBillTypeId;
                            }

                        }
                    }
                    catch (Exception ex)
                    {
                        targetBillTypeId = billTypeMaps[0].TargetBillTypeId;
                    }

                }
            }
            catch (Exception ex)
            {
                targetBillTypeId = string.Empty;
            }

            return targetBillTypeId;
        }

        /// <summary>
        /// queryservice取数方案，通过业务对象来获取数据，推荐使用
        /// </summary>
        /// <returns></returns>
        public static DynamicObjectCollection GetQueryDatas(Context ctx, string formid, string[] items, string filter)
        {
            QueryBuilderParemeter paramCatalog = new QueryBuilderParemeter()
            {
                FormId = formid,//取数的业务对象
                FilterClauseWihtKey = filter,//过滤条件，通过业务对象的字段Key拼装过滤条件
                SelectItems = SelectorItemInfo.CreateItems(items),//要筛选的字段【业务对象的字段Key】，可以多个，如果要取主键，使用主键名
            };

            DynamicObjectCollection dyDatas = Kingdee.BOS.ServiceHelper.QueryServiceHelper.GetDynamicObjectCollection(ctx, paramCatalog);
            return dyDatas;
        }
        /// <summary>
        /// 查询数据
        /// </summary>
        /// <returns></returns>
        public static DynamicObject[] GetQueryDatas(Context ctx, string formId, string filter)
        {
            FormMetadata meta = GetForm(ctx, formId);
            QueryBuilderParemeter paramCatalog = new QueryBuilderParemeter()
            {
                FormId = formId,//取数的业务对象
                FilterClauseWihtKey = filter,//过滤条件
                BusinessInfo = meta.BusinessInfo,
            };

            var bdObjs = BusinessDataServiceHelper.Load(ctx, meta.BusinessInfo.GetDynamicObjectType(), paramCatalog);
            return bdObjs;
        }
        /// <summary>
        /// 根据单据的标识，获取元数据
        /// </summary>
        /// <param name="ctx"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public static FormMetadata GetForm(Context ctx, string key)
        {
            return MetaDataServiceHelper.GetFormMetaData(ctx, key);
        }
    }
}
