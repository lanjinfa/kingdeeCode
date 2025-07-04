using Kingdee.BOS;
using Kingdee.BOS.Core.Metadata;
using Kingdee.BOS.Core.Metadata.ControlElement;
using Kingdee.BOS.Core.Metadata.ElementMetadata;
using Kingdee.BOS.Core.Metadata.FieldElement;
using Kingdee.BOS.Core.Metadata.Util;
using Kingdee.BOS.Orm.Metadata.DataEntity;
using Kingdee.BOS.ServiceHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace BOA.YD.JYFX.PlugIns.Helpers
{
    /// <summary>
    /// 动态构造字段帮助类
    /// </summary>
    public static class FieldCreateHelper
    {
        /// <summary>
        /// 创建字段的泛型方法
        /// </summary>
        /// <typeparam name="T">外观</typeparam>
        /// <typeparam name="K">字段</typeparam>
        /// <param name="ctx">上下文</param>
        /// <param name="entityKey">所在分录标识</param>
        /// <param name="fieldKey">字段标识</param>
        /// <param name="caption">字段标题</param>
        /// <param name="fieldProp">字段属性</param>
        /// <param name="elementType">元素类型</param>
        /// <returns></returns>
        private static T CreateField<T, K>(Context ctx, string entityKey, string fieldKey, string caption, string fieldProp, ElementType elementType = null)
            where T : FieldAppearance, new() where K : Field, new()
        {
            var fieldAppearance = new T();
            fieldAppearance.Field = new K();
            if (elementType != null)
            {
                PropertyUtil.SetAppearenceDefaultValue(fieldAppearance, elementType, ctx.UserLocale.LCID);
                PropertyUtil.SetBusinessDefaultValue(fieldAppearance.Field, elementType, ctx.UserLocale.LCID);
            }
            fieldAppearance.Key = fieldKey;
            fieldAppearance.EntityKey = entityKey;
            fieldAppearance.Caption = new LocaleValue(caption, ctx.UserLocale.LCID);
            fieldAppearance.Width = new LocaleValue("100", ctx.UserLocale.LCID);
            fieldAppearance.Locked = -1;
            fieldAppearance.Visible = -1;
            fieldAppearance.Field.Key = fieldKey;
            fieldAppearance.Field.EntityKey = entityKey;
            fieldAppearance.Field.Name = fieldAppearance.Caption;
            fieldAppearance.Field.FieldName = fieldKey;
            fieldAppearance.Field.PropertyName = fieldProp;
            fieldAppearance.Field.FireUpdateEvent = 0;
            return fieldAppearance;
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

        /// <summary>
        /// 动态构造字段
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entityKey">单据体或子单据体标识</param>
        /// <param name="copyField">需要动态构造的字段副本</param>
        /// <param name="fieldPrefix">字段前缀</param>
        /// <param name="isLocked">字段是否锁定</param>
        public static FieldAppearance CreateField(Context context, string entityKey, Field copyField,
            string fieldPrefix, int isLocked = 0, int fireUpdateEvent = 0)
        {
            var fieldKey = $"{fieldPrefix}_{copyField.Key}";//字段标识
            var fieldProp = $"{fieldPrefix}_{copyField.PropertyName}";//字段属性
            var fieldName = copyField.Name;//字段名称
            //var dynamicObjectType = new DynamicObjectType("f_displayer");
            if (copyField is TextField)
            {
                var fieldApp = CreateField<TextFieldAppearance, TextField>(context, entityKey, fieldKey, fieldName, fieldProp);
                fieldApp.Locked = isLocked;
                //FieldRegisterDynamicProperty(fieldApp.Field, dynamicObjectType);
                fieldApp.Field.FireUpdateEvent = fireUpdateEvent;
                return fieldApp;
            }
            else if (copyField is DateTimeField)
            {
                var fieldApp = CreateField<DateTimeFieldAppearance, DateTimeField>(context, entityKey, fieldKey, fieldName, fieldProp);
                fieldApp.Locked = isLocked;
                fieldApp.Field.FireUpdateEvent = fireUpdateEvent;
                return fieldApp;
            }
            else if (copyField is DecimalField)
            {
                var fieldApp = CreateField<DecimalFieldAppearance, DecimalField>(context, entityKey, fieldKey, fieldName, fieldProp);
                fieldApp.Locked = isLocked;
                fieldApp.Field.FieldScale = 4;
                fieldApp.Field.FieldPrecision = 20;
                fieldApp.Field.FireUpdateEvent = fireUpdateEvent;
                return fieldApp;
            }
            else if (copyField is CheckBoxField)
            {
                var fieldApp = CreateField<CheckBoxFieldAppearance, CheckBoxField>(context, entityKey, fieldKey, fieldName, fieldProp);
                fieldApp.Locked = isLocked;
                fieldApp.Field.FireUpdateEvent = fireUpdateEvent;
                return fieldApp;
            }
            else if (copyField is ComboField)
            {
                var fieldApp = CreateField<ComboFieldAppearance, ComboField>(context, entityKey, fieldKey, fieldName, fieldProp);
                fieldApp.Locked = isLocked;
                (fieldApp.Field as ComboField).EnumType = (copyField as ComboField).EnumType;
                (fieldApp.Field as ComboField).EnumObject = (copyField as ComboField).EnumObject;
                fieldApp.Field.FireUpdateEvent = fireUpdateEvent;
                return fieldApp;
            }
            else if (copyField is BaseDataField)//基础资料类型字段
            {
                var fieldApp = CreateField<BaseDataFieldAppearance, BaseDataField>(context, entityKey, fieldKey, fieldName, fieldProp);
                fieldApp.Locked = isLocked;
                fieldApp.Field.LookUpObjectID = (copyField as BaseDataField).LookUpObjectID;
                fieldApp.Field.LookUpObject = (copyField as BaseDataField).LookUpObject;
                fieldApp.Field.NameProperty = (copyField as BaseDataField).NameProperty;
                fieldApp.Field.NumberProperty = (copyField as BaseDataField).NumberProperty;
                fieldApp.Field.RefFormDynamicObjectType = (copyField as BaseDataField).RefFormDynamicObjectType;
                fieldApp.Field.FireUpdateEvent = fireUpdateEvent;
                return fieldApp;
            }
            return null;
        }

        /// <summary>
        /// 创建法人组织编码字段
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entityKey">单据体或子单据体标识</param>
        /// <param name="fieldPrefix">字段前缀</param>
        /// <returns></returns>
        public static FieldAppearance CreateCorpOrgNumberField(Context context, string entityKey, string fieldPrefix)
        {
            var fieldKey = $"{fieldPrefix}_FCorpOrgNumber";//字段标识
            var fieldProp = $"{fieldPrefix}_CorpOrgNumber";//字段属性
            var fieldName = "法人组织编码";//字段名称
            var fieldApp = CreateField<TextFieldAppearance, TextField>(context, entityKey, fieldKey, fieldName, fieldProp);
            fieldApp.Locked = 1;
            return fieldApp;
        }

        /// <summary>
        /// 创建法人仓库字段
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entityKey">单据体或子单据体标识</param>
        /// <param name="fieldPrefix">字段前缀</param>
        /// <returns></returns>
        public static FieldAppearance CreateCorpStockField(Context context, string entityKey, string fieldPrefix)
        {
            var fieldKey = $"{fieldPrefix}_FCorpStockId";//字段标识
            var fieldProp = $"{fieldPrefix}_CorpStockId";//字段属性
            var fieldName = "法人仓库编码";//字段名称
            var fieldApp = CreateField<TextFieldAppearance, TextField>(context, entityKey, fieldKey, fieldName, fieldProp);
            fieldApp.Locked = 1;
            return fieldApp;
        }

        /// <summary>
        /// 创建法人物料字段
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityKey"></param>
        /// <param name="fieldPrefix"></param>
        /// <returns></returns>
        public static FieldAppearance CreateCorpMaterialField(Context context, string entityKey, string fieldPrefix)
        {
            var fieldKey = $"{fieldPrefix}_FCorpMaterialId";//字段标识
            var fieldProp = $"{fieldPrefix}_CorpMaterialId";//字段属性
            var fieldName = "法人物料编码";//字段名称
            var fieldApp = CreateField<TextFieldAppearance, TextField>(context, entityKey, fieldKey, fieldName, fieldProp);
            fieldApp.Locked = 1;
            return fieldApp;
        }

        /// <summary>
        /// 创建法人业务日期字段
        /// </summary>
        /// <param name="context"></param>
        /// <param name="entityKey"></param>
        /// <param name="fieldPrefix"></param>
        /// <returns></returns>
        public static FieldAppearance CreateCorpDateTimeField(Context context, string entityKey, string fieldPrefix)
        {
            var fieldKey = $"{fieldPrefix}_FCorpDateTime";//字段标识
            var fieldProp = $"{fieldPrefix}_CorpDateTime";//字段属性
            var fieldName = "法人业务日期";//字段名称
            var fieldApp = CreateField<DateTimeFieldAppearance, DateTimeField>(context, entityKey, fieldKey, fieldName, fieldProp);
            fieldApp.Locked = 1;
            return fieldApp;
        }

        /// <summary>
        /// 创建内码字段（单据内码，分录内码）
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entityKey">单据体或子单据体标识</param>
        /// <param name="fieldKey">字段标识</param>
        /// <param name="fieldProp">字段属性</param>
        /// <param name="fieldName">字段名称</param>
        /// <returns></returns>
        public static FieldAppearance CreateIdField(Context context, string entityKey, string fieldKey, string fieldProp, string fieldName)
        {
            //var fieldKey = $"{fieldPrefix}_FCorpStockId";//字段标识
            //var fieldProp = $"{fieldPrefix}_CorpStockId";//字段属性
            //var fieldName = "法人仓库编码";//字段名称
            var fieldApp = CreateField<TextFieldAppearance, TextField>(context, entityKey, fieldKey, fieldName, fieldProp);
            fieldApp.Locked = 1;
            fieldApp.Visible = 0;
            return fieldApp;
        }

        /// <summary>
        /// 创建法人即时库存字段
        /// </summary>
        /// <returns></returns>
        public static FieldAppearance CreateCorpInventoryQtyField(Context context, string entityKey)
        {
            var fieldApp = CreateField<DecimalFieldAppearance, DecimalField>(context, entityKey, "target_FInventory", "法人即时库存", "target_Inventory");
            fieldApp.Locked = 1;
            fieldApp.Field.FieldScale = 4;
            fieldApp.Field.FieldPrecision = 20;
            return fieldApp;
        }

        /// <summary>
        /// 创建文本字段
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entityKey">单据体或子单据体标识</param>
        /// <param name="fieldKey">字段标识/字段属性</param>
        /// <param name="fieldName">字段名称</param>
        /// <returns></returns>
        public static FieldAppearance CreateTextField(Context context, string entityKey, string fieldKey, string fieldName)
        {
            var fieldApp = CreateField<TextFieldAppearance, TextField>(context, entityKey, fieldKey, fieldName, fieldKey);
            fieldApp.Locked = 1;
            return fieldApp;
        }

        /// <summary>
        /// 创建数量字段
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="entityKey">单据体或子单据体标识</param>
        /// <param name="fieldKey">字段标识/字段属性</param>
        /// <param name="fieldName">字段名称</param>
        /// <returns></returns>
        public static FieldAppearance CreateQtyField(Context context, string entityKey, string fieldKey, string fieldName)
        {
            var fieldApp = CreateField<DecimalFieldAppearance, DecimalField>(context, entityKey, fieldKey, fieldName, fieldKey);
            fieldApp.Locked = 1;
            fieldApp.Field.FieldScale = 4;
            fieldApp.Field.FieldPrecision = 20;
            //fieldApp.Field.FireUpdateEvent = 0;
            return fieldApp;
        }
    }
}
