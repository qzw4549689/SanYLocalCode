/**
 * 评分项目枚举值表 - 表单逻辑
 * 实体: mcs_credititem_value
 * 功能: 定性评分项目枚举值配置，关联评分项目表
 * 影响范围: 仅限mcs_credititem_value实体
 */

var CreditItemValueForm = CreditItemValueForm || {};

/**
 * 表单加载事件
 */
CreditItemValueForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    
    // 设置字段只读
    CreditItemValueForm.setFieldsReadOnly(formContext, formType);
    
    // 注册字段变更事件
    CreditItemValueForm.registerEvents(formContext);
    
    // 加载评分项目信息
    if (formType === 1) {
        CreditItemValueForm.loadItemInfo(formContext);
    }
};

/**
 * 设置字段只读
 * 评分项目编码和选择项编码不允许修改
 */
CreditItemValueForm.setFieldsReadOnly = function (formContext, formType) {
    // 评分项目编码始终只读
    var itemIdControl = formContext.getControl("mcs_itemid");
    if (itemIdControl) {
        itemIdControl.setDisabled(true);
    }
    
    // 选择项编码在编辑时只读（不允许修改，用于和Coface映射）
    if (formType !== 1) {
        var listValueControl = formContext.getControl("mcs_listvalue");
        if (listValueControl) {
            listValueControl.setDisabled(true);
        }
    }
};

/**
 * 注册字段变更事件
 */
CreditItemValueForm.registerEvents = function (formContext) {
    // 评分项目编码变更 - 带出信息
    var itemField = formContext.getAttribute("mcs_itemid");
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
    var itemField = formContext.getAttribute("mcs_itemid");
    
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
                Xrm.Utility.alertDialog("该评分项目为定量指标，不需要配置枚举值");
                itemField.setValue(null);
                return;
            }
            
            // 显示评分项目名称（通过通知）
            var itemName = result.mcs_itemname || "";
            formContext.ui.clearFormNotification("item_info");
            formContext.ui.setFormNotification(
                "当前为定性项目：" + itemName + "，请配置枚举值",
                "INFO",
                "item_info"
            );
        })
        .catch(function (error) {
            console.error("查询评分项目失败:", error);
            Xrm.Utility.alertDialog("查询评分项目信息失败");
        });
};

/**
 * 加载评分项目信息（新建时）
 */
CreditItemValueForm.loadItemInfo = function (formContext) {
    var itemField = formContext.getAttribute("mcs_itemid");
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
        { name: "mcs_itemid", label: "评分项目编码" },
        { name: "mcs_listvalue", label: "选择项编码" },
        { name: "mcs_listname", label: "选择项目名称" }
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
    
    // 校验选择项编码：不允许为空字符串
    var listValue = formContext.getAttribute("mcs_listvalue").getValue();
    if (listValue && listValue.trim() === "") {
        Xrm.Utility.alertDialog("选择项编码不能为空字符串");
        executionContext.getEventArgs().preventDefault();
        return;
    }
};
