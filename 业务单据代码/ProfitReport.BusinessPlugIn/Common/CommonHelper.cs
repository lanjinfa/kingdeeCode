using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.CommonFilter;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.ServiceHelper.Excel;
using Kingdee.BOS.Util;
using System;
using System.Data;

namespace BOA.FQGT.ProfitReport.BusinessPlugIn.Common
{
    public static class CommonHelper
    {
        /// <summary>
        /// 导出利润表
        /// </summary>
        public static void ExportExcelOfProfitReport(DynamicObject data, IDynamicFormView view)
        {
            var orgList = data["F_BOA_OrgIdList"] as DynamicObjectCollection;
            if (orgList.Count == 0)
            {
                view.ShowMessage("请先查询数据，再导出！");
                return;
            }
            var orgNameList = string.Empty;
            foreach (var item in orgList)
            {
                orgNameList += $"{(item["F_BOA_OrgIdList"] as DynamicObject)["Name"]},";
            }
            orgNameList = orgNameList.Substring(0, orgNameList.Length - 1);
            var period = data["F_BOA_Period"];
            var exportData = data["BOA_K8479b236"] as DynamicObjectCollection;
            var dt = new DataTable();
            dt.Columns.Add("项目");
            dt.Columns.Add("金额");
            dt.Rows.Add($"组织:{orgNameList}", $"期间:{period}");
            dt.Rows.Add("项目", "金额");
            foreach (var item in exportData)
            {
                dt.Rows.Add(item["F_BOA_Project"], item["F_BOA_Amount"]);
            }
            var fileName = $"利润表_{DateTime.Now:yyyyMMddHHmmssff}.xls";
            var filePath = PathUtils.GetPhysicalPath(KeyConst.TEMPFILEPATH, fileName);
            //获取服务器Url地址,把文件传到服务器上面,然后下载
            string fileUrl = PathUtils.GetServerPath(KeyConst.TEMPFILEPATH, fileName);
            using (ExcelOperation excelHelper = new ExcelOperation(view))
            {
                excelHelper.BeginExport();
                excelHelper.ExportToFile(dt);
                excelHelper.EndExport(filePath, SaveFileType.XLS);
            }
            //打开文件下载界面
            DynamicFormShowParameter showParameter = new DynamicFormShowParameter();
            showParameter.FormId = "BOS_FileDownload";
            showParameter.OpenStyle.ShowType = ShowType.Modal;
            showParameter.CustomComplexParams.Add("url", fileUrl);
            //显示
            view.ShowForm(showParameter);
        }

        /// <summary>
        /// 获取过滤窗体
        /// </summary>
        /// <param name="flag">1=成本明细，0=利润表</param>
        /// <returns></returns>
        public static BillShowParameter GetFilterForm(int flag)
        {
            var form = new BillShowParameter();
            form.FormId = "BOA_ProfitFilter";
            form.OpenStyle.ShowType = ShowType.Modal;
            form.Status = OperationStatus.VIEW;
            form.CustomComplexParams.Add("flag", flag);
            return form;
        }

        /// <summary>
        /// 获取过滤窗体返回的数据
        /// </summary>
        public static FilterCondition GetFilterCondition(FormResult filterResult)
        {
            var filter = new FilterCondition();
            FilterParameter result = filterResult.ReturnData as FilterParameter;
            if (result != null)
            {
                var customFilter = result.CustomFilter;
                var orgInfo = customFilter["F_BOA_OrgId"] as DynamicObject;
                var yearFrom = Convert.ToInt32(customFilter["F_BOA_YearFrom"]);
                var yearTo = Convert.ToInt32(customFilter["F_BOA_YearTo"]);
                var monthFrom = Convert.ToInt32(customFilter["F_BOA_MonthFrom"]);
                var monthTo = Convert.ToInt32(customFilter["F_BOA_MonthTo"]);
                var filterCondition = new string[3];
                filterCondition[0] = orgInfo["Id"].ToString();
                filterCondition[1] = $"{yearFrom}-{monthFrom}-01";
                filterCondition[2] = $"{yearTo}-{monthTo}-{DateTime.DaysInMonth(yearTo, monthTo)}";
                filter.Filter = filterCondition;
                filter.OrgIds = new string[] { orgInfo["Id"].ToString() };
                filter.Period = $"{yearFrom}-{monthFrom}--{yearTo}-{monthTo}";
                filter.BillType = Convert.ToInt32(customFilter["F_BOA_Type"]);
            }
            return filter;
        }
    }
}
