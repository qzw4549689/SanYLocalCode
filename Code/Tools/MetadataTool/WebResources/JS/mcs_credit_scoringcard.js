/**
 * 客户评分卡配置表 - 表单逻辑
 * 实体: mcs_credit_scoringcard
 * 功能: 自动带出、显隐控制、下拉联动、校验
 */

var ScoringCardForm = ScoringCardForm || {};

/**
 * 表单加载事件
 */
ScoringCardForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    
    // 设置字段只读
    ScoringCardForm.setFieldsReadOnly(formContext);
    
    // 根据数据类型控制显隐
    ScoringCardForm.toggleFieldsByDataType(formContext);
    
    // 注册字段变更事件
    ScoringCardForm.registerEvents(formContext);
};

/**
 * 设置字段只读
 */
ScoringCardForm.setFieldsReadOnly = function (formContext) {
    // 编码字段始终只读
    var codeField = formContext.getAttribute("mcs_credit_scoringcardno");
    if (codeField) {
        formContext.getControl("mcs_credit_scoringcardno").setDisabled(true);
    }
    
    // 带出字段只读
    var readOnlyFields = ["mcs_itemname", "mcs_datatype", "mcs_typeid"];
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
ScoringCardForm.registerEvents = function (formContext) {
    // 评分项目编码变更 - 自动带出
    var itemField = formContext.getAttribute("mcs_itemid");
    if (itemField) {
        itemField.addOnChange(ScoringCardForm.onItemChange);
    }
    
    // 数据类型变更 - 显隐控制
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    if (dataTypeField) {
        dataTypeField.addOnChange(ScoringCardForm.onDataTypeChange);
    }
    
    // 最小值变更 - 联动校验
    var minField = formContext.getAttribute("mcs_minvalue");
    if (minField) {
        minField.addOnChange(ScoringCardForm.onMinValueChange);
    }
    
    // 最大值变更 - 联动校验
    var maxField = formContext.getAttribute("mcs_maxvalue");
    if (maxField) {
        maxField.addOnChange(ScoringCardForm.onMaxValueChange);
    }
};

/**
 * 评分项目编码变更事件
 * 自动带出：评分项目名称、数据类型、评分项目分类
 */
ScoringCardForm.onItemChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var itemField = formContext.getAttribute("mcs_itemid");
    
    if (!itemField) return;
    
    var itemId = itemField.getValue();
    
    if (!itemId || itemId.length === 0) {
        // 清空带出字段
        ScoringCardForm.clearLookupFields(formContext);
        return;
    }
    
    var itemGuid = itemId[0].id.replace(/[{}]/g, "");
    
    // 查询评分项目信息
    var fetchXml = [
        "<fetch top='1'>",
        "  <entity name='mcs_credit_items'>",
        "    <attribute name='mcs_itemname' />",
        "    <attribute name='mcs_datatype' />",
        "    <attribute name='mcs_group' />",
        "    <filter>",
        "      <condition attribute='mcs_credit_itemsid' operator='eq' value='" + itemGuid + "' />",
        "    </filter>",
        "  </entity>",
        "</fetch>"
    ].join("");
    
    Xrm.WebApi.retrieveMultipleRecords("mcs_credit_items", "?fetchXml=" + encodeURIComponent(fetchXml))
        .then(function (result) {
            if (result.entities.length > 0) {
                var item = result.entities[0];
                
                // 带出评分项目名称
                var itemNameField = formContext.getAttribute("mcs_itemname");
                if (itemNameField) {
                    itemNameField.setValue(item.mcs_itemname || "");
                }
                
                // 带出数据类型
                var dataTypeField = formContext.getAttribute("mcs_datatype");
                if (dataTypeField && item.mcs_datatype !== undefined) {
                    dataTypeField.setValue(item.mcs_datatype);
                }
                
                // 带出评分项目分类
                var typeField = formContext.getAttribute("mcs_typeid");
                if (typeField && item.mcs_group !== undefined) {
                    typeField.setValue(item.mcs_group);
                }
                
                // 更新显隐控制
                ScoringCardForm.toggleFieldsByDataType(formContext);
                
                // 更新定性下拉选项
                if (item.mcs_datatype === 1) { // 定性
                    ScoringCardForm.loadListValues(formContext, itemGuid);
                }
            }
        })
        .catch(function (error) {
            console.error("查询评分项目失败:", error);
            Xrm.Utility.alertDialog("查询评分项目信息失败，请重试");
        });
};

/**
 * 清空带出字段
 */
ScoringCardForm.clearLookupFields = function (formContext) {
    var fields = ["mcs_itemname", "mcs_datatype", "mcs_typeid"];
    fields.forEach(function (fieldName) {
        var field = formContext.getAttribute(fieldName);
        if (field) {
            field.setValue(null);
        }
    });
    
    // 重置显隐
    ScoringCardForm.toggleFieldsByDataType(formContext);
};

/**
 * 数据类型变更事件
 */
ScoringCardForm.onDataTypeChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    ScoringCardForm.toggleFieldsByDataType(formContext);
};

/**
 * 根据数据类型控制字段显隐
 * 定量(0): 显示min/max, 隐藏listvalue
 * 定性(1): 显示listvalue, 隐藏min/max
 */
ScoringCardForm.toggleFieldsByDataType = function (formContext) {
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var dataType = dataTypeField ? dataTypeField.getValue() : null;
    
    // 定量字段
    var quantitativeFields = ["mcs_minvalue", "mcs_maxvalue"];
    // 定性字段
    var qualitativeFields = ["mcs_listvalue"];
    
    if (dataType === 0) {
        // 定量：显示min/max，隐藏listvalue
        quantitativeFields.forEach(function (fieldName) {
            var control = formContext.getControl(fieldName);
            if (control) control.setVisible(true);
        });
        qualitativeFields.forEach(function (fieldName) {
            var control = formContext.getControl(fieldName);
            if (control) control.setVisible(false);
        });
        // 清除定性值
        var listValueField = formContext.getAttribute("mcs_listvalue");
        if (listValueField) listValueField.setValue(null);
        
    } else if (dataType === 1) {
        // 定性：显示listvalue，隐藏min/max
        quantitativeFields.forEach(function (fieldName) {
            var control = formContext.getControl(fieldName);
            if (control) control.setVisible(false);
        });
        qualitativeFields.forEach(function (fieldName) {
            var control = formContext.getControl(fieldName);
            if (control) control.setVisible(true);
        });
        // 清除定量值
        var minField = formContext.getAttribute("mcs_minvalue");
        var maxField = formContext.getAttribute("mcs_maxvalue");
        if (minField) minField.setValue(null);
        if (maxField) maxField.setValue(null);
        
    } else {
        // 未选择：全部隐藏
        quantitativeFields.forEach(function (fieldName) {
            var control = formContext.getControl(fieldName);
            if (control) control.setVisible(false);
        });
        qualitativeFields.forEach(function (fieldName) {
            var control = formContext.getControl(fieldName);
            if (control) control.setVisible(false);
        });
    }
};

/**
 * 加载定性项目值下拉选项
 * 根据评分项目ID过滤枚举值
 */
ScoringCardForm.loadListValues = function (formContext, itemGuid) {
    var listValueControl = formContext.getControl("mcs_listvalue");
    if (!listValueControl) return;
    
    // 查询该评分项目下的枚举值
    var fetchXml = [
        "<fetch>",
        "  <entity name='mcs_credititem_value'>",
        "    <attribute name='mcs_credititem_valueid' />",
        "    <attribute name='mcs_listvalue' />",
        "    <attribute name='mcs_listname' />",
        "    <filter>",
        "      <condition attribute='mcs_credititemno' operator='eq' value='" + itemGuid + "' />",
        "    </filter>",
        "  </entity>",
        "</fetch>"
    ].join("");
    
    Xrm.WebApi.retrieveMultipleRecords("mcs_credititem_value", "?fetchXml=" + encodeURIComponent(fetchXml))
        .then(function (result) {
            var options = [];
            result.entities.forEach(function (entity) {
                options.push({
                    value: entity.mcs_credititem_valueid,
                    text: entity.mcs_listname + " (" + entity.mcs_listvalue + ")"
                });
            });
            
            // 清空现有选项
            listValueControl.clearOptions();
            
            // 添加默认空选项
            listValueControl.addOption({ value: "", text: "--请选择--" });
            
            // 添加查询到的选项
            options.forEach(function (option) {
                listValueControl.addOption(option);
            });
        })
        .catch(function (error) {
            console.error("查询枚举值失败:", error);
        });
};

/**
 * 最小值变更事件 - 校验
 */
ScoringCardForm.onMinValueChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var minField = formContext.getAttribute("mcs_minvalue");
    var maxField = formContext.getAttribute("mcs_maxvalue");
    
    if (!minField || !maxField) return;
    
    var minValue = minField.getValue();
    var maxValue = maxField.getValue();
    
    // 校验：最小值不能小于0
    if (minValue !== null && minValue < 0) {
        Xrm.Utility.alertDialog("定量最小值不能小于0");
        minField.setValue(null);
        return;
    }
    
    // 校验：最小值必须小于最大值
    if (minValue !== null && maxValue !== null && minValue >= maxValue) {
        Xrm.Utility.alertDialog("定量最小值必须小于最大值");
        minField.setValue(null);
    }
};

/**
 * 最大值变更事件 - 校验
 */
ScoringCardForm.onMaxValueChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var minField = formContext.getAttribute("mcs_minvalue");
    var maxField = formContext.getAttribute("mcs_maxvalue");
    
    if (!minField || !maxField) return;
    
    var minValue = minField.getValue();
    var maxValue = maxField.getValue();
    
    // 校验：最大值必须大于最小值
    if (minValue !== null && maxValue !== null && maxValue <= minValue) {
        Xrm.Utility.alertDialog("定量最大值必须大于最小值");
        maxField.setValue(null);
    }
};

/**
 * 保存前校验
 */
ScoringCardForm.onSave = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var dataType = dataTypeField ? dataTypeField.getValue() : null;
    
    // 定量时校验min/max
    if (dataType === 0) {
        var minValue = formContext.getAttribute("mcs_minvalue").getValue();
        var maxValue = formContext.getAttribute("mcs_maxvalue").getValue();
        
        if (minValue === null || maxValue === null) {
            Xrm.Utility.alertDialog("定量项目必须填写最小值和最大值");
            executionContext.getEventArgs().preventDefault();
            return;
        }
    }
    
    // 定性时校验listvalue
    if (dataType === 1) {
        var listValue = formContext.getAttribute("mcs_listvalue").getValue();
        if (!listValue) {
            Xrm.Utility.alertDialog("定性项目必须选择定性项目值");
            executionContext.getEventArgs().preventDefault();
            return;
        }
    }
};
