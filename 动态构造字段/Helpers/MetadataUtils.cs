using Kingdee.BOS;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.ControlElement;
using Kingdee.BOS.Core.Metadata.ElementMetadata;
using Kingdee.BOS.Core.Metadata.FieldElement;
using Kingdee.BOS.Core.Metadata.Util;
using Kingdee.BOS.Orm.DataEntity;
using Kingdee.BOS.Orm.Metadata.DataEntity;
using Kingdee.BOS.ServiceHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BOA.YD.JYFX.PlugIns.Helpers
{
    public static class MetadataUtils
    {
        /// <summary>
        /// /// 创建字段 
        /// /// </summary>
        /// /// <typeparam name="T"></typeparam> 
        /// /// <typeparam name="K"></typeparam>  
        /// /// <param name="ctx"></param>  
        /// /// <param name="entityKey"></param>      
        /// /// <param name="fieldName"></param>      
        /// /// <param name="caption"></param>    
        /// /// <param name="propName"></param>    
        /// /// <param name="elementType"></param>   
        /// /// <returns></returns> 
        public static T CreateField<T, K>(Context ctx, string entityKey, string fieldName, string caption, string propName = "", ElementType elementType = null)
            where T : FieldAppearance, new() where K : Field, new()
        {
            var fieldAppearance = new T();
            fieldAppearance.Field = new K();
            if (elementType != null)
            {
                PropertyUtil.SetAppearenceDefaultValue(fieldAppearance, elementType, ctx.UserLocale.LCID);
                PropertyUtil.SetBusinessDefaultValue(fieldAppearance.Field, elementType, ctx.UserLocale.LCID);
            }
            fieldAppearance.Key = fieldName;
            fieldAppearance.EntityKey = entityKey;
            fieldAppearance.Caption = new LocaleValue(caption, ctx.UserLocale.LCID);
            fieldAppearance.Width = new LocaleValue("100", ctx.UserLocale.LCID);
            fieldAppearance.Locked = -1;
            fieldAppearance.Visible = -1;
            fieldAppearance.Field.Key = fieldName;
            fieldAppearance.Field.EntityKey = entityKey;
            fieldAppearance.Field.Name = fieldAppearance.Caption;
            fieldAppearance.Field.FieldName = fieldName;
            fieldAppearance.Field.PropertyName = string.IsNullOrWhiteSpace(propName) ? fieldName : propName;
            fieldAppearance.Field.FireUpdateEvent = 0;
            return fieldAppearance;
        }

        /// <summary>     
        /// /// 创建字段      
        /// /// </summary>     
        /// /// <param name="ctx"></param>    
        /// /// <param name="layoutInfo"></param>  
        /// /// <param name="entityKey"></param>     
        /// /// <param name="fields"></param>    
        public static void CreateFields(Context ctx, LayoutInfo layoutInfo, string entityKey, string[] fields)
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer");
            // 添加选择列     
            var checkBoxFieldAp = CreateField<CheckBoxFieldAppearance, CheckBoxField>(ctx, entityKey, "F_MultiSelectKey2", "选择");
            checkBoxFieldAp.Width = new LocaleValue("60", ctx.UserLocale.LCID);
            checkBoxFieldAp.Locked = 0;
            checkBoxFieldAp.Field.DefValue = "0";
            FieldRegisterDynamicProperty(checkBoxFieldAp.Field, dynamicObjectType);
            layoutInfo.Add(checkBoxFieldAp);
            //基础资料
            var baseField = CreateField<BaseDataFieldAppearance, BaseDataField>(ctx, entityKey, "F_MultiSelectKey1", "物料");
            var metaData = MetaDataServiceHelper.Load(ctx, "BD_MATERIAL");
            //baseField.Width = new LocaleValue("60", ctx.UserLocale.LCID);
            baseField.Locked = 0;
            baseField.Field.LookUpObjectID = "624b39cf-5504-42e0-9124-7d75e64a05f1";
            //select top 1 * from T_META_LOOKUPCLASS where FFORMID = 'BD_MATERIAL'
            baseField.Field.LookUpObject = MetaDataServiceHelper.GetLookupObjects(ctx, new LookUpObjectFilter() { Filter = string.Format("FFORMID='{0}'", "BD_MATERIAL") }).First();

            baseField.Field.NameProperty = new BaseDataFieldRefProperty("FName", "Name");
            baseField.Field.NumberProperty = new BaseDataFieldRefProperty("FNumber", "Number");

            baseField.Field.RefFormDynamicObjectType = ((FormMetadata)metaData).BusinessInfo.GetDynamicObjectType();
            FieldRegisterDynamicProperty(baseField.Field, dynamicObjectType);
            layoutInfo.Add(baseField);
            // 添加其他列     
            foreach (var colName in fields)
            {
                string key = string.Format("F{0}", colName);
                var fieldAp = CreateField<TextFieldAppearance, TextField>(ctx, entityKey, key, colName, key);
                FieldRegisterDynamicProperty(fieldAp.Field, dynamicObjectType);
                layoutInfo.Add(fieldAp);
            }
        }

        /// <summary>   
        /// 给字段动态注册属性    
        /// </summary>  
        /// <param name="field"></param> 
        /// <param name="dynamicObjectType"></param>
        public static void FieldRegisterDynamicProperty(Field field, DynamicObjectType dynamicObjectType)
        {
            var methodInfo = field.GetType().GetMethod("RegisterDynamicProperty", BindingFlags.Instance | BindingFlags.NonPublic);
            if (methodInfo == null)
            {
                return;
            }
            methodInfo.Invoke(field, new object[] { dynamicObjectType });
        }

        public static FieldAppearance CreateField(Context ctx, string entityKey, string fieldKey = "F_MultiSelectKey1")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer");
            //基础资料
            var baseField = CreateField<BaseDataFieldAppearance, BaseDataField>(ctx, entityKey, fieldKey, "物料");
            var metaData = MetaDataServiceHelper.Load(ctx, "BD_MATERIAL");
            //baseField.Width = new LocaleValue("60", ctx.UserLocale.LCID);
            baseField.Locked = 0;
            baseField.Field.LookUpObjectID = "624b39cf-5504-42e0-9124-7d75e64a05f1";
            //select top 1 * from T_META_LOOKUPCLASS where FFORMID = 'BD_MATERIAL'
            baseField.Field.LookUpObject = MetaDataServiceHelper.GetLookupObjects(ctx, new LookUpObjectFilter() { Filter = string.Format("FFORMID='{0}'", "BD_MATERIAL") }).First();

            baseField.Field.NameProperty = new BaseDataFieldRefProperty("FName", "Name");
            baseField.Field.NumberProperty = new BaseDataFieldRefProperty("FNumber", "Number");

            baseField.Field.RefFormDynamicObjectType = ((FormMetadata)metaData).BusinessInfo.GetDynamicObjectType();

            FieldRegisterDynamicProperty(baseField.Field, dynamicObjectType);

            return baseField;
        }

        public static FieldAppearance CreateMulBaseDataField(Context ctx, string entityKey, string fieldKey = "FMulMaterial")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer1");
            //基础资料
            var baseField = CreateField<MulBaseDataFieldAppearance, MulBaseDataField>(ctx, entityKey, fieldKey, "多选物料");
            var metaData = MetaDataServiceHelper.Load(ctx, "BD_MATERIAL");
            //baseField.Width = new LocaleValue("60", ctx.UserLocale.LCID);
            baseField.Locked = 0;
            baseField.Field.LookUpObjectID = "624b39cf-5504-42e0-9124-7d75e64a05f1";
            //select top 1 * from T_META_LOOKUPCLASS where FFORMID = 'BD_MATERIAL'
            baseField.Field.LookUpObject = MetaDataServiceHelper.GetLookupObjects(ctx, new LookUpObjectFilter() { Filter = string.Format("FFORMID='{0}'", "BD_MATERIAL") }).First();
            baseField.Field.NameProperty = new BaseDataFieldRefProperty("FName", "Name");
            baseField.Field.NumberProperty = new BaseDataFieldRefProperty("FNumber", "Number");
            baseField.Field.RefFormDynamicObjectType = ((FormMetadata)metaData).BusinessInfo.GetDynamicObjectType();
            //FieldRegisterDynamicProperty(baseField.Field, dynamicObjectType);
            return baseField;
        }

        public static FieldAppearance CreateMulBaseDataField1(Context ctx, string entityKey, string fieldKey = "FMulMaterial")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer1");
            //基础资料
            var baseField = CreateField<MulBaseDataFieldAppearance, MulBaseDataField>(ctx, entityKey, fieldKey, "多选物料");
            var metaData = MetaDataServiceHelper.Load(ctx, "BD_MATERIAL");
            //baseField.Width = new LocaleValue("60", ctx.UserLocale.LCID);
            baseField.Locked = 0;
            baseField.Field.LookUpObjectID = "624b39cf-5504-42e0-9124-7d75e64a05f1";
            //select top 1 * from T_META_LOOKUPCLASS where FFORMID = 'BD_MATERIAL'
            baseField.Field.LookUpObject = MetaDataServiceHelper.GetLookupObjects(ctx, new LookUpObjectFilter() { Filter = string.Format("FFORMID='{0}'", "BD_MATERIAL") }).First();
            baseField.Field.NameProperty = new BaseDataFieldRefProperty("FName", "Name");
            baseField.Field.NumberProperty = new BaseDataFieldRefProperty("FNumber", "Number");
            baseField.Field.RefFormDynamicObjectType = ((FormMetadata)metaData).BusinessInfo.GetDynamicObjectType();
            //FieldRegisterDynamicProperty(baseField.Field, dynamicObjectType);
            return baseField;
        }

        public static FieldAppearance CreateTextField(Context ctx, string entityKey, string fieldKey = "tsx")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer");
            //string key = string.Format("F{0}", "文本");
            var fieldAp = CreateField<TextFieldAppearance, TextField>(ctx, entityKey, fieldKey, $"文本{fieldKey}", fieldKey);
            fieldAp.Locked = 0;
            FieldRegisterDynamicProperty(fieldAp.Field, dynamicObjectType);
            return fieldAp;
        }

        public static FieldAppearance CreateCheckField(Context ctx, string entityKey, string fieldKey = "FCheck")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer");
            var checkBoxFieldAp = CreateField<CheckBoxFieldAppearance, CheckBoxField>(ctx, entityKey, fieldKey, "选择");
            checkBoxFieldAp.Width = new LocaleValue("60", ctx.UserLocale.LCID);
            checkBoxFieldAp.Locked = 0;
            checkBoxFieldAp.Field.DefValue = "0";
            FieldRegisterDynamicProperty(checkBoxFieldAp.Field, dynamicObjectType);
            return checkBoxFieldAp;
        }

        public static FieldAppearance CreateUnitField(Context ctx, string entityKey, string fieldKey = "FUnitId")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer");
            //基础资料
            var baseField = CreateField<BaseDataFieldAppearance, BaseDataField>(ctx, entityKey, fieldKey, "单位");
            var metaData = MetaDataServiceHelper.Load(ctx, "BD_UNIT");
            //baseField.Width = new LocaleValue("60", ctx.UserLocale.LCID);
            baseField.Locked = 0;
            baseField.Field.LookUpObjectID = "e6213815-6b93-4cff-83bf-f26f807b7f4d";
            //select top 1 * from T_META_LOOKUPCLASS where FFORMID = 'BD_MATERIAL'
            baseField.Field.LookUpObject = MetaDataServiceHelper.GetLookupObjects(ctx,
                  new LookUpObjectFilter() { Filter = string.Format("FFORMID='{0}'", "BD_UNIT") }).First();
            baseField.Field.NameProperty = new BaseDataFieldRefProperty("FName", "Name");
            baseField.Field.NumberProperty = new BaseDataFieldRefProperty("FNumber", "Number");
            baseField.Field.RefFormDynamicObjectType = ((FormMetadata)metaData).BusinessInfo.GetDynamicObjectType();
            FieldRegisterDynamicProperty(baseField.Field, dynamicObjectType);
            return baseField;
        }

        public static FieldAppearance CreateComboField(Context ctx, string entityKey, string fieldKey = "FCombo")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer");
            var fieldAp = CreateField<ComboFieldAppearance, ComboField>(ctx, entityKey, fieldKey, "下拉框", fieldKey);
            fieldAp.Locked = 0;
            var field = (ComboField)fieldAp.Field;
            field.FireUpdateEvent = 1;
            field.MustInput = 1;
            field.EnumType = "ac14913e-bd72-416d-a50b-2c7432bbff63";
            var enumObject = new EnumObject(new DynamicObject(EnumObject.EnumObjectType));
            enumObject.Id = field.EnumType;
            EnumItem eitem = new EnumItem();
            eitem.Caption = new LocaleValue("外购");
            eitem.EnumId = Convert.ToString(1);
            eitem.Value = Convert.ToString(1);
            eitem.Seq = 1;
            enumObject.Items.Add(eitem);
            EnumItem eitem1 = new EnumItem();
            eitem1.Caption = new LocaleValue("内购");
            eitem1.EnumId = Convert.ToString(2);
            eitem1.Value = Convert.ToString(2);
            eitem1.Seq = 2;
            enumObject.Items.Add(eitem1);
            field.EnumObject = enumObject;
            FieldRegisterDynamicProperty(fieldAp.Field, dynamicObjectType);
            return fieldAp;
        }

        public static FieldAppearance CreateDateField(Context ctx, string entityKey, string fieldKey = "FDate")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer");
            var fieldAp = CreateField<DateTimeFieldAppearance, DateTimeField>(ctx, entityKey, fieldKey, "日期", fieldKey);
            fieldAp.Locked = 0;
            FieldRegisterDynamicProperty(fieldAp.Field, dynamicObjectType);
            return fieldAp;
        }

        public static FieldAppearance CreateAssistantField(Context ctx, string entityKey, string fieldKey = "FAssistant")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer");
            var fieldAp = CreateField<AssistantFieldAppearance, AssistantField>(ctx, entityKey, fieldKey, "单选辅助资料", fieldKey);
            fieldAp.Locked = 0;
            var metaData = MetaDataServiceHelper.Load(ctx, "BOS_ASSISTANTDATA_SELECT");
            fieldAp.Field.LookUpObjectID = "0026220efe099ee611e411a932b862d8";
            //select * from T_BAS_ASSISTANTDATA where fid = '0026220efe099ee611e411a932b862d8'
            fieldAp.Field.LookUpObject = MetaDataServiceHelper.GetLookupObjects(ctx, new LookUpObjectFilter() { Filter = string.Format("FFORMID='{0}'", "BOS_ASSISTANTDATA_SELECT") }).First();
            fieldAp.Field.NameProperty = new BaseDataFieldRefProperty("FDataValue", "FDataValue");
            fieldAp.Field.NumberProperty = new BaseDataFieldRefProperty("FNumber", "FNumber");
            fieldAp.Field.RefFormDynamicObjectType = ((FormMetadata)metaData).BusinessInfo.GetDynamicObjectType();
            FieldRegisterDynamicProperty(fieldAp.Field, dynamicObjectType);
            return fieldAp;
        }

        public static FieldAppearance CreateMulAssistantField(Context ctx, string entityKey, string fieldKey = "FMulAssistant")
        {
            var dynamicObjectType = new DynamicObjectType("f_displayer");
            var fieldAp = CreateField<MulAssistantFieldAppearance, MulAssistantField>(ctx, entityKey, fieldKey, "多选选辅助资料", fieldKey);
            fieldAp.Locked = 0;
            var metaData = MetaDataServiceHelper.Load(ctx, "BOS_ASSISTANTDATA_SELECT");
            fieldAp.Field.LookUpObjectID = "005056942d56b22311e35c933b526c2a";
            fieldAp.Field.LookUpObject = MetaDataServiceHelper.GetLookupObjects(ctx, new LookUpObjectFilter() { Filter = string.Format("FFORMID='{0}'", "BOS_ASSISTANTDATA_SELECT") }).First();
            fieldAp.Field.NameProperty = new BaseDataFieldRefProperty("FDataValue", "FDataValue");
            fieldAp.Field.NumberProperty = new BaseDataFieldRefProperty("FNumber", "FNumber");
            fieldAp.Field.RefFormDynamicObjectType = ((FormMetadata)metaData).BusinessInfo.GetDynamicObjectType();
            //FieldRegisterDynamicProperty(fieldAp.Field, dynamicObjectType);
            return fieldAp;
        }
    }
}
