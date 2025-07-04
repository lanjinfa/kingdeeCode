using Kingdee.BOS.Core.Bill.PlugIn;
using Kingdee.BOS.Core.DynamicForm;
using Kingdee.BOS.Core.DynamicForm.PlugIn.Args;
using Kingdee.BOS.Core.Metadata.FieldElement;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace BOA.YD.JYFX.PlugIns.Plugins
{
    [HotUpdate]
    [Description("单据生成工作台配置表")]
    public class BillCreateConfigurePlugin : AbstractBillPlugIn
    {
        public override void DataChanged(DataChangedEventArgs e)
        {
            base.DataChanged(e);
            if (e.Field.Key.EqualsIgnoreCase("F_BOA_SyncBill"))
            {
                this.View.Model.DeleteEntryData("F_BOA_Entity");
            }
        }

        public override void BeforeF7Select(BeforeF7SelectEventArgs e)
        {
            base.BeforeF7Select(e);
            if (e.FieldKey.Equals("F_BOA_SrcField"))
            {
                LoadElementSelectForm(e.Row, e.FieldKey);
            }
        }

        /// <summary>
        /// 弹出表单元素选择框
        /// </summary>
        /// <param name="row">请求F7所在的行</param>
        /// <param name="fieldKey">请求F7所在的字段标识</param>
        private void LoadElementSelectForm(int row, string fieldKey)
        {
            //判断单据是否选择
            var srcForm = this.View.Model.GetValue("F_BOA_SyncBill");
            if (srcForm == null)
            {
                this.View.ShowWarnningMessage("请先选择单据类型！");
                return;
            }
            var srcFormId = (srcForm as DynamicObject)["Id"].ToString();//源单标识
            var formName = (srcForm as DynamicObject)["Name"].ToString();//单据名称
            var showParameter = new DynamicFormShowParameter();
            showParameter.FormId = "MFG_ELEMENTSELECTOR";
            showParameter.ParentPageId = base.View.PageId;
            showParameter.OpenStyle.ShowType = ShowType.NonModal;
            showParameter.CustomParams["FormId"] = srcFormId;
            showParameter.CustomParams["SelMode"] = "1";
            showParameter.Caption = $"[{formName}]元素选择器";
            this.View.ShowForm(showParameter, delegate (FormResult result)
            {
                if (result.ReturnData != null)
                {
                    var returnData = result.ReturnData as Field;
                    this.View.Model.SetValue("F_BOA_SrcField", returnData.Name.ToString(), row);
                    this.View.Model.SetValue("F_BOA_SrcFieldKey", returnData.Key, row);
                    this.View.Model.SetValue("F_BOA_SrcFieldProp", returnData.PropertyName, row);
                    this.View.Model.SetValue("F_BOA_SrcEntity", returnData.Entity.Name.ToString(), row);
                    this.View.Model.SetValue("F_BOA_SrcEntityKey", returnData.Entity.Key, row);
                    this.View.Model.SetValue("F_BOA_SrcEntityProp", returnData.Entity.EntryName, row);
                }
            });
        }

        public override void AfterEntryBarItemClick(AfterBarItemClickEventArgs e)
        {
            base.AfterEntryBarItemClick(e);
            if (e.BarItemKey.Equals("BOA_Up"))
            {
                var currentRowIndex = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");
                RowMove("F_BOA_Entity", currentRowIndex, 0);
            }
            else if (e.BarItemKey.Equals("BOA_Down"))
            {
                var currentRowIndex = this.View.Model.GetEntryCurrentRowIndex("F_BOA_Entity");
                RowMove("F_BOA_Entity", currentRowIndex, 1);
            }
        }

        /// <summary>
        /// 行移动
        /// </summary>
        /// <param name="entityKey">单据体标识</param>
        /// <param name="rowIndex">当前行</param>
        /// <param name="moveType">移动类型</param>
        private void RowMove(string entityKey, int rowIndex, int moveType)
        {
            var entity = this.Model.BillBusinessInfo.GetEntity(entityKey);
            var rows = this.Model.GetEntityDataObject(entity);
            if (moveType == 0)//上移
            {
                if (rowIndex <= 0)
                {
                    this.View.ShowWarnningMessage("已经是第一行，不能上移！");
                    return;
                }
                var currRow = rows[rowIndex];
                // 从行集合中，删除当前行
                rows.Remove(currRow);
                // 再把此行，插入到上一行的位置上
                rows.Insert(rowIndex - 1, currRow);
                // 更新行序号
                if (entity.SeqDynamicProperty != null)
                {
                    entity.SeqDynamicProperty.SetValue(rows[rowIndex - 1], rowIndex);
                    entity.SeqDynamicProperty.SetValue(rows[rowIndex], rowIndex + 1);
                }
                // 刷新界面上的表格数据
                this.View.UpdateView(entityKey);
                //焦点到移动后的分录行
                this.View.SetEntityFocusRow(entityKey, rowIndex - 1);
            }
            else//下移
            {
                if (rowIndex >= rows.Count - 1)
                {
                    this.View.ShowWarnningMessage("已经是最后一行，不能下移！");
                    return;
                }
                var currRow = rows[rowIndex];
                // 从行集合中，删除当前行
                rows.Remove(currRow);
                // 再把此行，插入到下一行的位置上
                rows.Insert(rowIndex + 1, currRow);
                // 更新行序号
                if (entity.SeqDynamicProperty != null)
                {
                    entity.SeqDynamicProperty.SetValue(rows[rowIndex], rowIndex + 1);
                    entity.SeqDynamicProperty.SetValue(rows[rowIndex + 1], rowIndex + 2);
                }
                // 刷新界面上的表格数据
                this.View.UpdateView(entityKey);
                //焦点到移动后的分录行
                this.View.SetEntityFocusRow(entityKey, rowIndex + 1);
            }
        }
    }
}
