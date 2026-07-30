/**
 * 评分项目枚举值表 - 表单逻辑
 * 实体: mcs_credititem_value
 * 功能: 定性评分项目枚举值配置，关联评分项目表
 * 影响范围: 仅限mcs_credititem_value实体
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

var CreditItemValueForm = CreditItemValueForm || {};

/**
 * 多语言取词（带中文兜底）
 * 语言包已加载时返回对应语言文本；未加载/未找到时返回原中文，保证中文用户不受影响
 */
CreditItemValueForm.L = function (key, defaultText) {
    if (typeof LanguageHelper !== "undefined") {
        var v = LanguageHelper.getLabel(key);
        if (v && v !== key) return v;
    }
    return defaultText;
};

/**
 * 表单加载事件
 */
CreditItemValueForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();

    // 预加载语言包；评分项目信息提示在语言包就绪后的回调中渲染，避免英文用户在 onLoad 阶段看到中文兜底
    if (typeof LanguageHelper !== "undefined") {
        LanguageHelper.loadLanguagePack(function () {
            if (formContext.ui.getFormType() === 1) {
                CreditItemValueForm.loadItemInfo(formContext);
            }
        });
    }

    var formType = formContext.ui.getFormType();
    
    // 设置字段只读
    CreditItemValueForm.setFieldsReadOnly(formContext, formType);
    
    // 注册字段变更事件
    CreditItemValueForm.registerEvents(formContext);
    
    // 加载评分项目信息（无语言包时立即执行；有语言包时由上方回调执行）
    if (typeof LanguageHelper === "undefined" && formType === 1) {
        CreditItemValueForm.loadItemInfo(formContext);
    }
};

/**
 * 设置字段只读
 * 枚举值表为预置基础数据表，所有字段只读
 */
CreditItemValueForm.setFieldsReadOnly = function (formContext, formType) {
    // 枚举值表为预置基础数据表，所有字段只读，不允许业务修改
    var readOnlyFields = [
        "mcs_credititemno", // 评分项目编码
        "mcs_listvalue",    // 选择项编码
        "mcs_listname"      // 选择项名称
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
CreditItemValueForm.registerEvents = function (formContext) {
    // 评分项目编码变更 - 带出信息
    var itemField = formContext.getAttribute("mcs_credititemno");
    if (itemField) {
        itemField.addOnChange(CreditItemValueForm.onItemChange);
    }
};

/**
 * 评分项目编码变更事件
 * 自动带出评分项目名称和数据类型
 */
CreditItemValueForm.onItemChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var itemField = formContext.getAttribute("mcs_credititemno");
    
    if (!itemField) return;
    
    var itemValue = itemField.getValue();
    
    if (!itemValue || itemValue.length === 0) {
        CreditItemValueForm.clearItemInfo(formContext);
        return;
    }
    
    var itemGuid = itemValue[0].id.replace(/[{}]/g, "");
    
    // 查询评分项目信息
    Xrm.WebApi.retrieveRecord("mcs_credit_items", itemGuid, "?$select=mcs_itemname,mcs_datatype")
        .then(function (result) {
            // 校验：只允许为定性项目配置枚举值
            if (result.mcs_datatype !== 2) {
                Xrm.Utility.alertDialog(CreditItemValueForm.L("CreditItemValue_QuantNoEnum", "该评分项目为定量指标，不需要配置枚举值"));
                itemField.setValue(null);
                return;
            }
            
            // 显示评分项目名称（通过通知）
            var itemName = result.mcs_itemname || "";
            formContext.ui.clearFormNotification("item_info");
            formContext.ui.setFormNotification(
                CreditItemValueForm.L("CreditItemValue_QualItemPrefix", "当前为定性项目：") + itemName + CreditItemValueForm.L("CreditItemValue_QualItemSuffix", "，请配置枚举值"),
                "INFO",
                "item_info"
            );
        })
        .catch(function (error) {
            console.error("查询评分项目失败:", error);
            Xrm.Utility.alertDialog(CreditItemValueForm.L("CreditItemValue_QueryItemFailed", "查询评分项目信息失败"));
        });
};

/**
 * 加载评分项目信息（新建时）
 */
CreditItemValueForm.loadItemInfo = function (formContext) {
    var itemField = formContext.getAttribute("mcs_credititemno");
    if (!itemField || !itemField.getValue()) return;
    
    // 触发变更事件
    CreditItemValueForm.onItemChange({ getFormContext: function () { return formContext; } });
};

/**
 * 清空评分项目信息
 */
CreditItemValueForm.clearItemInfo = function (formContext) {
    formContext.ui.clearFormNotification("item_info");
};

/**
 * 保存前校验
 */
CreditItemValueForm.onSave = function (executionContext) {
    var formContext = executionContext.getFormContext();
    
    // 校验必填字段
    var requiredFields = [
        { name: "mcs_credititemno", label: CreditItemValueForm.L("CreditItemValue_Field_ItemCode", "评分项目编码") },
        { name: "mcs_listvalue", label: CreditItemValueForm.L("CreditItemValue_Field_ListValue", "选择项编码") },
        { name: "mcs_listname", label: CreditItemValueForm.L("CreditItemValue_Field_ListName", "选择项目名称") }
    ];
    
    var missingFields = [];
    requiredFields.forEach(function (field) {
        var attr = formContext.getAttribute(field.name);
        if (!attr || attr.getValue() === null || attr.getValue() === "") {
            missingFields.push(field.label);
        }
    });
    
    if (missingFields.length > 0) {
        Xrm.Utility.alertDialog(CreditItemValueForm.L("CreditForm_MissingFieldsPrefix", "以下字段不能为空：") + missingFields.join(CreditItemValueForm.L("CreditForm_ListSeparator", "、")));
        executionContext.getEventArgs().preventDefault();
        return;
    }
    
    // 校验选择项编码：不允许为空字符串
    var listValue = formContext.getAttribute("mcs_listvalue").getValue();
    if (listValue && listValue.trim() === "") {
        Xrm.Utility.alertDialog(CreditItemValueForm.L("CreditItemValue_EmptyListValue", "选择项编码不能为空字符串"));
        executionContext.getEventArgs().preventDefault();
        return;
    }
};
