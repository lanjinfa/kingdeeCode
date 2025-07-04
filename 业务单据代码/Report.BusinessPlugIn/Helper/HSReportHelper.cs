using Kingdee.BOS;
using Kingdee.BOS.App;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Report;
using Kingdee.BOS.Model.ReportFilter;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace BOA.DJJX.Report.BusinessPlugIn.Helper
{
    /// <summary>
    /// 存货收发存汇总表
    /// </summary>
    public class HSReportHelper
    {
        private FormMetadata accountSystemMetaData;//核算体系
        private FormMetadata accountOrgMetaData;//组织
        private FormMetadata accountPolicyMetaData;//会计政策
        private FormMetadata materialMetaData;//物料
        private FormMetadata stockMetaData;//仓库

        /// <summary>
        /// 获取报表数据
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="accountOrgId">核算组织内码</param>
        public List<HSReportResultDto> GetReportData(Context context, string accountOrgId, int year, int month, long materialId = 0, long stockId = 0)
        {
            var sysReporSservice = ServiceFactory.GetSysReportService(context);
            var permissionService = ServiceFactory.GetPermissionService(context);
            var filterMetadata = FormMetaDataCache.GetCachedFilterMetaData(context);//加载字段比较条件元数据
            var reportMetadata = FormMetaDataCache.GetCachedFormMetaData(context, "HS_INOUTSTOCKSUMMARYRPT");//报表元数据
            var reportFilterMetadata = FormMetaDataCache.GetCachedFormMetaData(context, "HS_INOUTSTOCKSUMMARYFILTER");//报表过滤框元数据
            var reportFilterServiceProvider = reportFilterMetadata.BusinessInfo.GetForm().GetFormServiceProvider();
            var model = new SysReportFilterModel();
            model.SetContext(context, reportFilterMetadata.BusinessInfo, reportFilterServiceProvider);
            model.FormId = reportFilterMetadata.BusinessInfo.GetForm().Id;
            model.FilterObject.FilterMetaData = filterMetadata;
            model.InitFieldList(reportMetadata, reportFilterMetadata);
            model.GetSchemeList();
            var filter = model.GetFilterParameter();
            //构造过滤条件
            var rptParas = new DynamicObject(filter.CustomFilter.DynamicObjectType);
            accountSystemMetaData = accountSystemMetaData ?? DataHelper.GetFormMetaData(context, "Org_AccountSystem");//核算体系
            var accountSystemDataObject = DataHelper.GetDataById(context, accountSystemMetaData, 107733);
            rptParas["ACCTGSYSTEMID_Id"] = 107733;//核算体系内码
            rptParas["ACCTGSYSTEMID"] = accountSystemDataObject;//核算体系实体
            accountOrgMetaData = accountOrgMetaData ?? DataHelper.GetFormMetaData(context, "ORG_Organizations");//核算组织组织
            var accountOrgDataObject = DataHelper.GetDataById(context, accountOrgMetaData, accountOrgId);
            rptParas["ACCTGORGID_Id"] = accountOrgId;//核算组织内码
            rptParas["ACCTGORGID"] = accountOrgDataObject;//核算组织实体
            accountPolicyMetaData = accountPolicyMetaData ?? DataHelper.GetFormMetaData(context, "BD_ACCTPOLICY");//会计政策
            var accountPolicyDataObject = DataHelper.GetDataById(context, accountPolicyMetaData, 1);
            rptParas["ACCTPOLICYID_Id"] = 1;//会计政策内码
            rptParas["ACCTPOLICYID"] = accountPolicyDataObject;//会计政策实体
            rptParas["Year"] = year;//开始会计年度
            rptParas["EndYear"] = year;//结束会计年度
            rptParas["Period"] = month;//开始会计期间
            rptParas["EndPeriod"] = month;//结束会计期间
            rptParas["COMBOTotalType"] = 0;//汇总依据，默认 物料=0
            rptParas["FDimType"] = 1;//显示维度，默认 按库存维度显示=1
            if (materialId != 0)//物料
            {
                materialMetaData = materialMetaData ?? DataHelper.GetFormMetaData(context, "BD_MATERIAL");//物料
                var materialDataObject = DataHelper.GetDataById(context, materialMetaData, materialId);
                rptParas["MATERIALID_Id"] = materialId;//物料从
                rptParas["MATERIALID"] = materialDataObject;
                rptParas["ENDMATERIALID_Id"] = materialId;//物料至
                rptParas["ENDMATERIALID"] = materialDataObject;
            }
            if (stockId != 0)//仓库
            {
                stockMetaData = stockMetaData ?? DataHelper.GetFormMetaData(context, "BD_STOCK");//仓库
                var stockDataObject = DataHelper.GetDataById(context, stockMetaData, stockId);
                rptParas["STOCKID_Id"] = stockId;//仓库从
                rptParas["STOCKID"] = stockDataObject;
                rptParas["ENDSTOCKID_Id"] = stockId;//仓库至
                rptParas["ENDSTOCKID"] = stockDataObject;
            }
            filter.CustomFilter = rptParas;
            var p = new RptParams();
            p.FormId = reportFilterMetadata.BusinessInfo.GetForm().Id;
            p.StartRow = 1;
            p.EndRow = int.MaxValue;//StartRow和EndRow是报表数据分页的起始行数和截至行数，一般取所有数据，所以EndRow取int最大值。
            p.FilterParameter = filter;
            p.FilterFieldInfo = model.FilterFieldInfo;
            p.BaseDataTempTable.AddRange(permissionService.GetBaseDataTempTable(context, reportMetadata.BusinessInfo.GetForm().Id));
            var result = sysReporSservice.GetData(context, reportMetadata.BusinessInfo, p);
            var hSReportResultDtos = new List<HSReportResultDto>();
            if (result.Rows.Count > 0)
            {
                foreach (DataRow item in result.Rows)
                {
                    var hSReportResultDto = new HSReportResultDto
                    {
                        MaterialId = item["FMATERIALBASEID"].IsNullOrEmpty() ? 0 : Convert.ToInt64(item["FMATERIALBASEID"]),
                        StockId = item["FSTOCKID"].IsNullOrEmpty() ? 0 : Convert.ToInt64(item["FSTOCKID"]),
                        InitPrice = item["FINITPRICE"].IsNullOrEmpty() ? 0 : Convert.ToDecimal(item["FINITPRICE"]),
                        ReceivePrice = item["FRECEIVEPRICE"].IsNullOrEmpty() ? 0 : Convert.ToDecimal(item["FRECEIVEPRICE"]),
                        SEndPrice = item["FSENDPRICE"].IsNullOrEmpty() ? 0 : Convert.ToDecimal(item["FSENDPRICE"]),
                        EndPrice = item["FENDPRICE"].IsNullOrEmpty() ? 0 : Convert.ToDecimal(item["FENDPRICE"])
                    };
                    hSReportResultDtos.Add(hSReportResultDto);
                }
            }
            return hSReportResultDtos;
            //ServiceFactory.CloseService(sysReporSservice);
            //ServiceFactory.CloseService(permissionService);
        }

        /// <summary>
        /// 获取上上月数据，上上上月数据
        /// </summary>
        /// <param name="context"></param>
        /// <param name="accountOrgId"></param>
        /// <param name="materialId"></param>
        /// <returns></returns>
        public decimal GetMaterialPriceByMaterialId(Context context, string accountOrgId, long materialId, long stockId)
        {
            var year = DateTime.Now.AddMonths(-2).Year;
            var month = DateTime.Now.AddMonths(-2).Month;
            var data = GetReportData(context, accountOrgId, year, month, materialId, stockId);
            var averagePriceInfo = data.FirstOrDefault();
            if (averagePriceInfo != null)//上上月数据不为空
            {
                if (averagePriceInfo.EndPrice > 0)
                {
                    return averagePriceInfo.EndPrice;
                }
                else if (averagePriceInfo.SEndPrice > 0)
                {
                    return averagePriceInfo.SEndPrice;
                }
                else if (averagePriceInfo.ReceivePrice > 0)
                {
                    return averagePriceInfo.ReceivePrice;
                }
                else if (averagePriceInfo.InitPrice > 0)
                {
                    return averagePriceInfo.InitPrice;
                }
                else//上上月数据不为空，但是单价都为0，查上上上月数据
                {
                    return GetMaterialPriceByMaterialId01(context, accountOrgId, materialId, stockId);
                }
            }
            else//上上月数据为空，查上上上月数据
            {
                return GetMaterialPriceByMaterialId01(context, accountOrgId, materialId, stockId);
            }
        }

        /// <summary>
        /// 获取上上上月数据
        /// </summary>
        /// <param name="context"></param>
        /// <param name="accountOrgId"></param>
        /// <param name="materialId"></param>
        /// <param name="stockId"></param>
        /// <returns></returns>
        public decimal GetMaterialPriceByMaterialId01(Context context, string accountOrgId, long materialId, long stockId)
        {
            var year = DateTime.Now.AddMonths(-3).Year;
            var month = DateTime.Now.AddMonths(-3).Month;
            var data = GetReportData(context, accountOrgId, year, month, materialId, stockId);
            var averagePriceInfo = data.FirstOrDefault();
            if (averagePriceInfo != null)
            {
                if (averagePriceInfo.EndPrice > 0)
                {
                    return averagePriceInfo.EndPrice;
                }
                else if (averagePriceInfo.SEndPrice > 0)
                {
                    return averagePriceInfo.SEndPrice;
                }
                else if (averagePriceInfo.ReceivePrice > 0)
                {
                    return averagePriceInfo.ReceivePrice;
                }
                else
                {
                    return averagePriceInfo.InitPrice;
                }
            }
            else
            {
                return 0m;
            }
        }
    }
}

