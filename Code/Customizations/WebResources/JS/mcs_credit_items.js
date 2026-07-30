/**
 * 客户评分项目表 - 表单逻辑
 * 实体: mcs_credit_items
 * 功能: 基础配置表，字段校验、显隐控制
 * 影响范围: 仅限mcs_credit_items实体
 */

// 同步加载多语言帮助类（实验阶段，验证通过后可改为窗体依赖库）
(function () {
    if (typeof LanguageHelper !== "undefined") return;
    try {
        var req = new XMLHttpRequest();
        req.open("GET", Xrm.Utility.getGlobalContext().getClientUrl() + "/WebResources/mcs_language_helper.js", false);
        req.send();
        if (req.status === 200) {
            // 通过 script 标签注入，确保 LanguageHelper 定义在全局作用域
            var script = document.createElement("script");
            script.type = "text/javascript";
            script.text = req.responseText;
            document.getElementsByTagName("head")[0].appendChild(script);
        } else {
            console.warn("mcs_language_helper.js 加载失败，状态码:", req.status);
        }
    } catch (e) {
        console.error("加载 mcs_language_helper.js 异常:", e);
    }
})();

var CreditItemsForm = CreditItemsForm || {};

/**
 * 多语言取词（带中文兜底）
 * 语言包已加载时返回对应语言文本；未加载/未找到时返回原中文，保证中文用户不受影响
 */
CreditItemsForm.L = function (key, defaultText) {
    if (typeof LanguageHelper !== "undefined") {
        var v = LanguageHelper.getLabel(key);
        if (v && v !== key) return v;
    }
    return defaultText;
};

/**
 * 表单加载事件
 */
CreditItemsForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();

    // 预加载语言包；数据类型提示在语言包就绪后的回调中渲染，避免英文用户在 onLoad 阶段看到中文兜底
    if (typeof LanguageHelper !== "undefined") {
        LanguageHelper.loadLanguagePack(function () {
            CreditItemsForm.toggleFieldsByDataType(formContext);
        });
    }

    var formType = formContext.ui.getFormType();
    
    // 设置字段只读
    CreditItemsForm.setFieldsReadOnly(formContext, formType);
    
    // 注册字段变更事件
    CreditItemsForm.registerEvents(formContext);
    
    // 根据数据类型初始化显隐（无语言包时立即渲染；有语言包时由上方回调渲染）
    if (typeof LanguageHelper === "undefined") {
        CreditItemsForm.toggleFieldsByDataType(formContext);
    }
};

/**
 * 设置字段只读
 * 评分项目表为预置基础数据表，所有字段只读
 */
CreditItemsForm.setFieldsReadOnly = function (formContext, formType) {
    // 评分项目表为预置基础数据表，所有字段只读，不允许业务修改
    var readOnlyFields = [
        "mcs_credit_itemsno", // 评分项目编码
        "mcs_itemname",       // 评分项目名称
        "mcs_itemdesc",       // 评分项目说明
        "mcs_group",          // 评分项目分类
        "mcs_datatype",       // 数据类型
        "mcs_source",         // 内外部
        "mcs_validate",       // 人工补录
        "mcs__3p"             // 外部提供（注意：实际字段名是双下划线）
    ];
    
    readOnlyFields.forEach(function (fieldName) {
        var control = formContext.getControl(fieldName);
        if (control) {
            control.setDisabled(true);
        }
    });
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
            CreditItemsForm.L("CreditItems_QuantHint", "当前为定量指标，请在评分卡配置表中配置分值范围（最小值/最大值）"),
            "INFO",
            "datatype_hint"
        );
    } else if (dataType === 2) {
        // 定性
        formContext.ui.setFormNotification(
            CreditItemsForm.L("CreditItems_QualHint", "当前为定性指标，请在枚举值表中配置选项值"),
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
        { name: "mcs_itemid", label: CreditItemsForm.L("CreditItems_Field_ItemCode", "评分项目编码") },
        { name: "mcs_itemname", label: CreditItemsForm.L("CreditItems_Field_ItemName", "评分项目名称") },
        { name: "mcs_itemdesc", label: CreditItemsForm.L("CreditItems_Field_ItemDesc", "评分项目说明") },
        { name: "mcs_group", label: CreditItemsForm.L("CreditItems_Field_Group", "评分项目分类") },
        { name: "mcs_datatype", label: CreditItemsForm.L("CreditItems_Field_DataType", "数据类型") },
        { name: "mcs_source", label: CreditItemsForm.L("CreditItems_Field_Source", "内外部") },
        { name: "mcs_validate", label: CreditItemsForm.L("CreditItems_Field_Validate", "人工补录") },
        { name: "mcs_3p", label: CreditItemsForm.L("CreditItems_Field_3p", "外部提供") }
    ];
    
    var missingFields = [];
    requiredFields.forEach(function (field) {
        var attr = formContext.getAttribute(field.name);
        if (!attr || attr.getValue() === null || attr.getValue() === "") {
            missingFields.push(field.label);
        }
    });
    
    if (missingFields.length > 0) {
        Xrm.Utility.alertDialog(CreditItemsForm.L("CreditForm_MissingFieldsPrefix", "以下字段不能为空：") + missingFields.join(CreditItemsForm.L("CreditForm_ListSeparator", "、")));
        executionContext.getEventArgs().preventDefault();
        return;
    }
};
