/**
 * 客户评分项目表 - 表单逻辑
 * 实体: mcs_credit_items
 * 功能: 基础配置表，字段校验、显隐控制
 * 影响范围: 仅限mcs_credit_items实体
 */

var CreditItemsForm = CreditItemsForm || {};

/**
 * 表单加载事件
 */
CreditItemsForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    
    // 设置字段只读
    CreditItemsForm.setFieldsReadOnly(formContext, formType);
    
    // 注册字段变更事件
    CreditItemsForm.registerEvents(formContext);
    
    // 根据数据类型初始化显隐
    CreditItemsForm.toggleFieldsByDataType(formContext);
};

/**
 * 设置字段只读
 * 评分项目编码不允许业务修改（预置数据）
 */
CreditItemsForm.setFieldsReadOnly = function (formContext, formType) {
    // 评分项目编码始终只读（预置数据，不允许业务修改）
    var itemIdControl = formContext.getControl("mcs_itemid");
    if (itemIdControl) {
        itemIdControl.setDisabled(true);
    }
};

/**
 * 注册字段变更事件
 */
CreditItemsForm.registerEvents = function (formContext) {
    // 数据类型变更 - 显隐控制
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    if (dataTypeField) {
        dataTypeField.addOnChange(CreditItemsForm.onDataTypeChange);
    }
};

/**
 * 数据类型变更事件
 */
CreditItemsForm.onDataTypeChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    CreditItemsForm.toggleFieldsByDataType(formContext);
};

/**
 * 根据数据类型控制字段显隐和提示
 * 定量(1): 提示需配置评分卡时填写范围
 * 定性(2): 提示需在枚举值表配置选项
 */
CreditItemsForm.toggleFieldsByDataType = function (formContext) {
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var dataType = dataTypeField ? dataTypeField.getValue() : null;
    
    // 清除之前的通知
    formContext.ui.clearFormNotification("datatype_hint");
    
    if (dataType === 1) {
        // 定量
        formContext.ui.setFormNotification(
            "当前为定量指标，请在评分卡配置表中配置分值范围（最小值/最大值）",
            "INFO",
            "datatype_hint"
        );
    } else if (dataType === 2) {
        // 定性
        formContext.ui.setFormNotification(
            "当前为定性指标，请在枚举值表中配置选项值",
            "INFO",
            "datatype_hint"
        );
    }
};

/**
 * 保存前校验
 */
CreditItemsForm.onSave = function (executionContext) {
    var formContext = executionContext.getFormContext();
    
    // 校验必填字段
    var requiredFields = [
        { name: "mcs_itemid", label: "评分项目编码" },
        { name: "mcs_itemname", label: "评分项目名称" },
        { name: "mcs_itemdesc", label: "评分项目说明" },
        { name: "mcs_group", label: "评分项目分类" },
        { name: "mcs_datatype", label: "数据类型" },
        { name: "mcs_source", label: "内外部" },
        { name: "mcs_validate", label: "人工补录" },
        { name: "mcs_3p", label: "外部提供" }
    ];
    
    var missingFields = [];
    requiredFields.forEach(function (field) {
        var attr = formContext.getAttribute(field.name);
        if (!attr || attr.getValue() === null || attr.getValue() === "") {
            missingFields.push(field.label);
        }
    });
    
    if (missingFields.length > 0) {
        Xrm.Utility.alertDialog("以下字段不能为空：" + missingFields.join("、"));
        executionContext.getEventArgs().preventDefault();
        return;
    }
};
