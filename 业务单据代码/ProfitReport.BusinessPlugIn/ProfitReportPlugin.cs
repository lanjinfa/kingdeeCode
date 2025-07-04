using BOA.FQGT.ProfitReport.BusinessPlugIn.Common;
using Kingdee.BOS;
using Kingdee.BOS.Contracts;
using Kingdee.BOS.Core;
using Kingdee.BOS.Core.Bill;
using Kingdee.BOS.Core.CommonFilter;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Report;
using Kingdee.BOS.JSON;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using Kingdee.K3.FIN.Contracts;
using Kingdee.K3.FIN.GL.App.Report;
using Kingdee.K3.FIN.GL.Report.PlugIn.Base;
using Kingdee.K3.FIN.GL.ServiceHelper;
using Kingdee.K3.FIN.ServiceHelper;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace BOA.FQGT.ProfitReport.BusinessPlugIn
{
    [HotUpdate]
    [Description("利润报表")]
    public class ProfitReportPlugin : AbstractDynamicFormPlugIn
    {
        /// <summary>
        /// 过滤条件，组织，开始日期，结束日期
        /// </summary>
        private string[] filterCondition = new string[3];

        /// <summary>
        /// 营业收入
        /// </summary>
        private decimal YYSR = 0m;

        /// <summary>
        /// 主营业务成本
        /// </summary>
        private decimal ZYYWCB = 0m;

        public override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ShowFilterForm();
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        private void LoadProfitData()
        {
            //清空表体
            int rowcount = base.View.Model.GetEntryRowCount("F_BOA_Entity");
            if (rowcount > 0)
            {
                this.View.Model.DeleteEntryData("F_BOA_Entity");
            }
            //加载数据
            var entity = this.View.BillBusinessInfo.GetEntity("F_BOA_Entity");
            var rows = this.Model.GetEntityDataObject(entity);
            foreach (var item in GetRowData(new JSONArray()))
            {
                var itemArray = item as JSONArray;
                var row = new DynamicObject(entity.DynamicObjectType);
                row["F_BOA_Project"] = itemArray[0];
                row["F_BOA_Amount"] = itemArray[1];
                row["F_BOA_AccoutId"] = itemArray[2];
                rows.Add(row);
            }
            this.View.Model.SetValue("F_BOA_Amount", string.Format("{0:N2}", YYSR), 0);
            this.View.Model.SetValue("F_BOA_Amount", string.Format("{0:N2}", ZYYWCB), 22);
            YYSR = 0m;//重置营业收入的值
            ZYYWCB = 0m;//重置主营业务成本的值
            this.View.UpdateView("F_BOA_Entity");
        }

        /// <summary>
        /// 构建行数据结构
        /// </summary>
        /// <param name="col1">第一列</param>
        /// <param name="col2">第二列</param>
        /// <returns></returns>
        private JSONArray CreateRowInfo(string col1, string col2, string col3 = "0")
        {
            return new JSONArray
            {
                col1,
                col2,
                col3
            };
        }

        /// <summary>
        /// 获取报表每行数据
        /// </summary>
        /// <param name="rows"></param>
        private JSONArray GetRowData(JSONArray rows)
        {
            var YYLR = 0m;//营业利润
            //营业收入--内代
            var result = new SqlHelper(this.Context, filterCondition).GetInternalData();
            YYSR += Convert.ToDecimal(result[5]);
            rows.Add(CreateRowInfo("一、营业收入", ""));
            rows.Add(CreateRowInfo("    营业收入-內代", $"{result[5]}"));
            rows.Add(CreateRowInfo("        冷轧", $"{result[0]}"));
            rows.Add(CreateRowInfo("        热轧", $"{result[1]}"));
            rows.Add(CreateRowInfo($"        {(filterCondition[0] == "100002" ? "黑退火" : "酸洗")}", $"{result[2]}"));
            rows.Add(CreateRowInfo($"        {(filterCondition[0] == "100002" ? "轧硬" : "高张力")}", $"{result[3]}"));
            rows.Add(CreateRowInfo($"        {(filterCondition[0] == "100002" ? "光亮" : "其他")}", $"{result[4]}"));
            //减：销售退回
            var result1 = new SqlHelper(this.Context, filterCondition).GetOrderBackData();
            YYSR -= Convert.ToDecimal(result1[3]);
            rows.Add(CreateRowInfo("    减：销售退回", $"{result1[3]}"));
            rows.Add(CreateRowInfo("        业务", $"{result1[0]}"));
            rows.Add(CreateRowInfo("        采购", $"{result1[1]}"));
            rows.Add(CreateRowInfo("        车间", $"{result1[2]}"));
            //减：销售折让
            var result2 = new SqlHelper(this.Context, filterCondition).GetOrderDiscountData();
            YYSR -= Convert.ToDecimal(result2[3]);
            rows.Add(CreateRowInfo("    减：销售折让", $"{result2[3]}"));
            rows.Add(CreateRowInfo("        业务", $"{result2[0]}"));
            rows.Add(CreateRowInfo("        采购", $"{result2[1]}"));
            rows.Add(CreateRowInfo("        车间", $"{result2[2]}"));
            //营业收入-外代
            var result3 = new SqlHelper(this.Context, filterCondition).GetOutData();
            YYSR += Convert.ToDecimal(result3[0]);
            rows.Add(CreateRowInfo("    营业收入-外代", $"{result3[0]}"));
            //营业收入-下腳料收入
            var result4 = new SqlHelper(this.Context, filterCondition).GetLeftoversData();
            YYSR += Convert.ToDecimal(result4[5]);
            rows.Add(CreateRowInfo("    营业收入-下脚料收入", $"{result4[5]}"));
            rows.Add(CreateRowInfo("        冷轧", $"{result4[0]}"));
            rows.Add(CreateRowInfo("        热轧", $"{result4[1]}"));
            rows.Add(CreateRowInfo($"        {(filterCondition[0] == "100002" ? "黑退火" : "酸洗")}", $"{result4[2]}"));
            rows.Add(CreateRowInfo($"        {(filterCondition[0] == "100002" ? "轧硬" : "高张力")}", $"{result4[3]}"));
            rows.Add(CreateRowInfo($"        {(filterCondition[0] == "100002" ? "光亮" : "其他")}", $"{result4[4]}"));
            //营业成本 -- 内代
            var result5 = new SqlHelper(this.Context, filterCondition).InternalCostData();
            ZYYWCB += Convert.ToDecimal(result5[5]);
            rows.Add(CreateRowInfo("    减：主营营业成本", ""));
            rows.Add(CreateRowInfo("        营业成本-內代", $"{result5[5]}"));
            rows.Add(CreateRowInfo("            冷轧", $"{result5[0]}"));
            rows.Add(CreateRowInfo("            热轧", $"{result5[1]}"));
            rows.Add(CreateRowInfo($"           {(filterCondition[0] == "100002" ? " 黑退火" : " 酸洗")}", $"{result5[2]}"));
            rows.Add(CreateRowInfo($"           {(filterCondition[0] == "100002" ? " 轧硬" : " 高张力")}", $"{result5[3]}"));
            rows.Add(CreateRowInfo($"           {(filterCondition[0] == "100002" ? " 光亮" : " 其他")}", $"{result5[4]}"));
            //减：销售退回
            var result6 = new SqlHelper(this.Context, filterCondition).OrderBackCostData();
            ZYYWCB -= Convert.ToDecimal(result6[3]);
            rows.Add(CreateRowInfo("    减：销售退回", $"{result6[3]}"));
            rows.Add(CreateRowInfo("        业务", $"{result6[0]}"));
            rows.Add(CreateRowInfo("        采购", $"{result6[1]}"));
            rows.Add(CreateRowInfo("        车间", $"{result6[2]}"));
            //营业成本-下腳料收入
            var result7 = new SqlHelper(this.Context, filterCondition).LeftoversCostData();
            ZYYWCB += Convert.ToDecimal(result7[5]);
            rows.Add(CreateRowInfo("    营业成本-下脚料收入", $"{result7[5]}"));
            rows.Add(CreateRowInfo("        冷轧", $"{result7[0]}"));
            rows.Add(CreateRowInfo("        热轧", $"{result7[1]}"));
            rows.Add(CreateRowInfo($"        {(filterCondition[0] == "100002" ? "黑退火" : "酸洗")}", $"{result7[2]}"));
            rows.Add(CreateRowInfo($"        {(filterCondition[0] == "100002" ? "轧硬" : "高张力")}", $"{result7[3]}"));
            rows.Add(CreateRowInfo($"        {(filterCondition[0] == "100002" ? "光亮" : "其他")}", $"{result7[4]}"));
            //进货运费
            var result8 = new SqlHelper(this.Context, filterCondition).PurchaseCostData();
            var result8Cost = string.Format("{0:N2}", result8.Sum(t => Convert.ToDecimal(t["cost"])));
            ZYYWCB += Convert.ToDecimal(result8Cost);
            rows.Add(CreateRowInfo("    进货运费", $"{result8Cost}"));
            foreach (var item in result8)
            {
                rows.Add(CreateRowInfo($"        {item["Fname"]}", $"{string.Format("{0:N2}", item["cost"])}"));
            }
            //委外加工費
            var result9 = new SqlHelper(this.Context, filterCondition).OutsourcingCostData();
            var result9Sum = result9.Count == 0 ? 0 : result9[0]["cost"];
            ZYYWCB += Convert.ToDecimal(result9Sum);
            rows.Add(CreateRowInfo("    委外加工费", $"{string.Format("{0:N2}", result9Sum)}", $"{(result9.Count == 0 ? "0" : result9[0]["FACCOUNTID"])}"));
            //直接人工
            var result10 = new SqlHelper(this.Context, filterCondition).LaborCostData();
            var result10Sum = string.Format("{0:N2}", result10.Sum(t => Convert.ToDecimal(t["cost"])));
            ZYYWCB += Convert.ToDecimal(result10Sum);
            rows.Add(CreateRowInfo("    直接人工薪资", $"{result10Sum}", $"{(result10.Count == 0 ? "0" : result10[0]["FACCOUNTID"])}"));
            foreach (var item in result10)
            {
                rows.Add(CreateRowInfo($"        {item["fname"]}", $"{string.Format("{0:N2}", item["cost"])}", $"{item["FACCOUNTID"]}"));
            }
            //制造费用
            var result11 = new SqlHelper(this.Context, filterCondition).ManufactureCostData();
            var result11Sum = string.Format("{0:N2}", result11.Sum(t => Convert.ToDecimal(t["cost"])));
            ZYYWCB += Convert.ToDecimal(result11Sum);
            rows.Add(CreateRowInfo("    制造费用", $"{result11Sum}", $"{(result11.Count == 0 ? "0" : result11[0]["FACCOUNTID"])}"));
            foreach (var item in result11)
            {
                rows.Add(CreateRowInfo($"        {item["fname"]}", $"{string.Format("{0:N2}", item["cost"])}", $"{item["FACCOUNTID"]}"));
            }
            //营业税金及附加
            var result13 = new SqlHelper(this.Context, filterCondition).TaxCostData();
            var result13Sum = string.Format("{0:N2}", result13.Sum(t => Convert.ToDecimal(t["cost"])));
            ZYYWCB += Convert.ToDecimal(result13Sum);
            rows.Add(CreateRowInfo("    营业税金及附加", $"{result13Sum}", $"{(result13.Count == 0 ? "0" : result13[0]["FACCOUNTID"])}"));
            foreach (var item in result13)
            {
                rows.Add(CreateRowInfo($"        {item["fname"]}", $"{string.Format("{0:N2}", item["cost"])}", $"{item["FACCOUNTID"]}"));
            }
            YYLR = YYSR - ZYYWCB;
            //销售费用
            //销售人员,交际应酬费，办公费，车辆费用，邮电费，保险费，销售运费，水电费，折旧费，差旅费
            var result14 = new SqlHelper(this.Context, filterCondition).SaleCostData();
            var result14Sum = string.Format("{0:N2}", result14.Sum(t => Convert.ToDecimal(t["cost"])));
            YYLR -= Convert.ToDecimal(result14Sum);
            rows.Add(CreateRowInfo("    销售费用", $"{result14Sum}", $"{(result14.Count == 0 ? "0" : result14[0]["FACCOUNTID"])}"));
            foreach (var item in result14)
            {
                rows.Add(CreateRowInfo($"        {item["fname"]}", $"{string.Format("{0:N2}", item["cost"])}", $"{item["FACCOUNTID"]}"));
            }
            //管理费用，非车间销售与其余人员
            //交际应酬费，办公费，车辆费用，邮电费，保险费，水电费，折旧费，差旅费，修缮费，培训费，劳务支出，杂费，职工福利
            var result15 = new SqlHelper(this.Context, filterCondition).ManageCostData();
            var result15Sum = string.Format("{0:N2}", result15.Sum(t => Convert.ToDecimal(t["cost"])));
            YYLR -= Convert.ToDecimal(result15Sum);
            rows.Add(CreateRowInfo("    管理费用", $"{result15Sum}", $"{(result15.Count == 0 ? "0" : result15[0]["FACCOUNTID"])}"));
            foreach (var item in result15)
            {
                rows.Add(CreateRowInfo($"        {item["fname"]}", $"{string.Format("{0:N2}", item["cost"])}", $"{item["FACCOUNTID"]}"));
            }
            //财务费用
            var result16 = new SqlHelper(this.Context, filterCondition).FinancialExpensesData();
            var interestInCome = result16.Where(t => t["fname"].ToString().Contains("利息收入")).Sum(t => Convert.ToDecimal(t["cost"]));
            var otherData = result16.Where(t => !t["fname"].ToString().Contains("利息收入")).Sum(t => Convert.ToDecimal(t["cost"]));
            var finSum = otherData - interestInCome;
            YYLR -= finSum;
            rows.Add(CreateRowInfo("    财务费用", $"{string.Format("{0:N2}", finSum)}", $"{(result16.Count == 0 ? "0" : result16[0]["FACCOUNTID"])}"));
            foreach (var item in result16)
            {
                rows.Add(CreateRowInfo($"        {item["fname"]}", $"{string.Format("{0:N2}", item["cost"])}", $"{item["FACCOUNTID"]}"));
            }
            //资产减值损失
            var result17 = new SqlHelper(this.Context, filterCondition).RowAssetsData("资产减值损失");
            var result17Sum = result17.Count == 0 ? "0.00" : string.Format("{0:N2}", result17[0]["cost"]);
            YYLR -= Convert.ToDecimal(result17Sum);
            rows.Add(CreateRowInfo("    资产减值损失", $"{result17Sum}", $"{(result17.Count == 0 ? "0" : result17[0]["FACCOUNTID"])}"));
            //公允价值变动损益
            var result18 = new SqlHelper(this.Context, filterCondition).RowAssetsData("公允价值变动损益");
            var result18Sum = result18.Count == 0 ? "0.00" : string.Format("{0:N2}", result18[0]["cost"]);
            YYLR -= Convert.ToDecimal(result18Sum);
            rows.Add(CreateRowInfo("    加:公允价值变动收益", $"{result18Sum}", $"{(result18.Count == 0 ? "0" : result18[0]["FACCOUNTID"])}"));
            //投资收益
            var result19 = new SqlHelper(this.Context, filterCondition).RowAssetsData("投资收益");
            var result19Sum = result19.Count == 0 ? "0.00" : string.Format("{0:N2}", result19[0]["cost"]);
            YYLR -= Convert.ToDecimal(result19Sum);
            rows.Add(CreateRowInfo("    投资收益", $"{result19Sum}", $"{(result19.Count == 0 ? "0" : result19[0]["FACCOUNTID"])}"));
            //对联营企业和合营企业的投资收益
            rows.Add(CreateRowInfo("    其中:对联营企业和合营企业的投资收益", "0.00"));
            //营业利润
            var result20 = new SqlHelper(this.Context, filterCondition).GetPackageData();
            var result21 = new SqlHelper(this.Context, filterCondition).RowAssetsData("营业外收入");
            var result20Amount = result20.Count == 0 ? "0.00" : string.Format("{0:N2}", result20[0]["Amount"]);
            var result21Amount = result21.Count == 0 ? "0.00" : string.Format("{0:N2}", result21[0]["cost"]);
            var profitData = Convert.ToDecimal(result20Amount) + Convert.ToDecimal(result21Amount);
            //利润总额
            var LRZE = YYLR + profitData;
            rows.Add(CreateRowInfo("二、营业利润", $"{string.Format("{0:N2}", YYLR)}"));
            rows.Add(CreateRowInfo("    加：营业外收入", $"{string.Format("{0:N2}", profitData)}"));
            rows.Add(CreateRowInfo("        包裝皮", $"{result20Amount}"));
            rows.Add(CreateRowInfo("        营业外收入", $"{result21Amount}", $"{(result21.Count == 0 ? "0" : result21[0]["FACCOUNTID"])}"));
            //营业外支出
            var result22 = new SqlHelper(this.Context, filterCondition).RowAssetsData("营业外支出");
            var result22Sum = result22.Count == 0 ? "0.00" : string.Format("{0:N2}", result22[0]["cost"]);
            LRZE -= Convert.ToDecimal(result22Sum);
            rows.Add(CreateRowInfo("    减：营业外支出", $"{result22Sum}", $"{(result22.Count == 0 ? "0" : result22[0]["FACCOUNTID"])}"));
            rows.Add(CreateRowInfo("        其中：非流动资产处置损失", "0.00"));
            rows.Add(CreateRowInfo("三、利润总额", $"{string.Format("{0:N2}", LRZE)}"));
            //所得税费用
            var result23 = new SqlHelper(this.Context, filterCondition).RowAssetsData("所得税费用");
            var result23Sum = result23.Count == 0 ? "0.00" : string.Format("{0:N2}", result23[0]["cost"]);
            rows.Add(CreateRowInfo("    减：所得税费用", $"{result23Sum}", $"{(result23.Count == 0 ? "0" : result23[0]["FACCOUNTID"])}"));
            rows.Add(CreateRowInfo("四、净利润", $"{string.Format("{0:N2}", LRZE - Convert.ToDecimal(result23Sum))}"));
            return rows;
        }

        public override void BarItemClick(BarItemClickEventArgs e)
        {
            switch (e.BarItemKey)
            {
                case "BOA_Filter"://过滤
                    ShowFilterForm();
                    break;
                case "BOA_Refresh"://刷新
                    if (filterCondition[0] != null)
                    {
                        LoadProfitData();
                    }
                    break;
                case "BOA_CostDetail"://查看成本明细
                    ShowDetailForm();
                    break;
                case "BOA_Export"://导出利润表
                    CommonHelper.ExportExcelOfProfitReport(this.View.Model.DataObject, this.View);
                    break;
                case "BOA_tbSubLedger":
                    ShowAccountBalance(e);
                    break;
                default:
                    break;
            }
            base.BarItemClick(e);
        }

        /// <summary>
        /// 显示过滤框
        /// </summary>
        private void ShowFilterForm()
        {
            this.View.ShowForm(CommonHelper.GetFilterForm(0), delegate (FormResult filterResult)
              {
                  var condition = CommonHelper.GetFilterCondition(filterResult);
                  if (condition.Filter != null)
                  {
                      this.View.Model.SetValue("F_BOA_OrgIdList", condition.OrgIds);
                      this.View.Model.SetValue("F_BOA_Period", condition.Period);
                      filterCondition = condition.Filter;
                      LoadProfitData();
                  }
              });
        }

        /// <summary>
        /// 查看成本明细
        /// </summary>
        public void ShowDetailForm()
        {
            var form = new BillShowParameter();
            form.FormId = "BOA_DetailForm";
            form.OpenStyle.ShowType = ShowType.MainNewTabPage;
            form.Status = OperationStatus.VIEW;
            this.View.ShowForm(form);
        }

        /// <summary>
        /// 查看明细帐
        /// </summary>
        public void ShowAccountBalance(BarItemClickEventArgs e)
        {
            var parameter = new SysReportShowParameter();
            parameter.FormId = "GL_RPT_SubLedger";
            parameter.IsShowFilter = true;
            parameter.OpenStyle.ShowType = ShowType.MainNewTabPage;
            var currentRow = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");
            var accountId = this.View.Model.GetValue("F_BOA_AccoutId", currentRow).ToString().Trim();//获取当前选择单元格的内容
            if (accountId == "0")
            {
                this.View.ShowMessage("未查询到科目！");
                return;
            }
            var itemName = this.View.Model.GetValue("F_BOA_Project", currentRow).ToString().Trim();//获取当前选择单元格的内容
            var itemId = SqlHelper.GetAccountIdByAccountName(this.Context, accountId, itemName);
            var openParameter = new Dictionary<string, object>
            {
                ["FACCTBOOKID"] = SqlHelper.GetAccountBookIdByOrgId(this.Context, filterCondition[0]),//账簿id
                ["FCURRENCYID"] = 1,//币别，默认人民币
                ["FUSEPERIOD"] = true,//默认按期间查询
                ["FSTARTYEAR"] = filterCondition[1].Split('-')[0],//会计年度
                ["FENDYEAR"] = filterCondition[2].Split('-')[0],//会计年度至
                ["FSTARTPERIOD"] = filterCondition[1].Split('-')[1],//会计期间
                ["FENDPERIOD"] = filterCondition[2].Split('-')[1],//会计期间至
                ["FCONBALANCE"] = true,//默认连续科目范围查询
                ["FSTARTBALANCELEVEL"] = 1,//科目级别
                ["FENDBALANCELEVEL"] = 3,//科目级别至
                ["FSTARTBALANCE"] = accountId,//科目编码
                ["FENDBALANCE"] = accountId,//科目编码至
            };
            if (itemId != "-1")
            {
                DynamicObject account = GLCommonServiceHelper.LoadObject(base.Context, "BD_Account", Convert.ToInt64(accountId));
                var itemDetailId = account["ItemDetail_Id"];
                var lstSource = CommonServiceHelper.GetValueSourceByDetailItem(Context, Convert.ToInt64(itemDetailId));
                var itemValue = new string[2];
                foreach (var item in lstSource)
                {
                    if (item.Item2 == "费用项目")
                    {
                        itemValue = item.Item1.Split(new string[] { "*&" }, 2, StringSplitOptions.RemoveEmptyEntries);
                        break;
                    }
                }
                openParameter.Add("FSHOWACCTITEMS", true);//显示核算维度明细
                openParameter.Add("DETAIL", itemValue[0]);//核算维度，默认为费用项目
                openParameter.Add("STARTDETAIL", itemId);//核算维度编码从
                openParameter.Add("ENDDETAIL", itemId);//核算维度编码至
            }
            parameter.CustomComplexParams.Add("MyFilter", openParameter);
            parameter.CustomComplexParams.Add("MyFlag", true);

            this.View.ShowForm(parameter);
        }
    }
}
