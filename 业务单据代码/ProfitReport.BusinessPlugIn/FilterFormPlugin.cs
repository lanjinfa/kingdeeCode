using Kingdee.BOS;
using Kingdee.BOS.Core.CommonFilter.PlugIn;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using System;
using System.ComponentModel;

namespace BOA.FQGT.ProfitReport.BusinessPlugIn
{
    [HotUpdate]
    [Description("过滤框_利润报表/成本明细表")]
    public class FilterFormPlugin : AbstractCommonFilterPlugIn
    {
        public override void OnInitialize(InitializeEventArgs e)
        {
            base.OnInitialize(e);
            var flag = (int)this.View.OpenParameter.GetCustomParameter("flag");
            var formTitle = new LocaleValue("利润表过滤条件框");
            if (flag == 1)
            {
                formTitle = new LocaleValue("成本明细过滤条件框 （注：由于数据量过多，建议时间跨度不宜过大！）");
            }
            this.View.SetFormTitle(formTitle);
            this.View.SendDynamicFormAction(View);
        }

        public override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            //判断是否需要初始化过滤方案
            var orgInfo = this.View.Model.GetValue("F_BOA_OrgId") as DynamicObject;
            if (orgInfo != null)
            {
                return;
            }
            //默认年份和月份为当前年份和月份
            var nowYear = DateTime.Today.Year;
            var nowMonth = DateTime.Today.Month;
            this.View.Model.SetValue("F_BOA_YearFrom", nowYear);
            this.View.Model.SetValue("F_BOA_YearTo", nowYear);
            this.View.Model.SetValue("F_BOA_MonthFrom", nowMonth);
            this.View.Model.SetValue("F_BOA_MonthTo", nowMonth);
            this.View.Model.SetValue("F_BOA_OrgId", this.Context.CurrentOrganizationInfo.ID);
        }

        public override void AfterBindData(EventArgs e)
        {
            base.AfterBindData(e);
            var flag = (int)this.View.OpenParameter.GetCustomParameter("flag");
            if (flag == 1)//成本明细表
            {
                this.View.GetControl("F_BOA_Type").Visible = true;
            }
            else//利润表
            {
                this.View.GetControl("F_BOA_Type").Visible = false;
            }
        }
    }
}
