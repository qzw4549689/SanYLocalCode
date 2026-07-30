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
    
    // 带出字段只读（基于评分项目Lookup自动带入，不可手动修改）
    var readOnlyFields = ["mcs_itemid", "mcs_itemname", "mcs_datatype", "mcs_typeid"];
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
    // 评分项目(Lookup)变更 - 自动带出编码、名称、类型、分类
    var creditItemField = formContext.getAttribute("mcs_credititem");
    if (creditItemField) {
        creditItemField.addOnChange(ScoringCardForm.onCreditItemChange);
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
 * 评分项目(Lookup)变更事件
 * 自动带出：评分项目编码、名称、数据类型、评分项目分类
 * 触发字段：mcs_credititem
 */
ScoringCardForm.onCreditItemChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var creditItemField = formContext.getAttribute("mcs_credititem");
    
    if (!creditItemField) return;
    
    var creditItemValue = creditItemField.getValue();
    
    if (!creditItemValue || creditItemValue.length === 0) {
        // 清空带出字段
        ScoringCardForm.clearLookupFields(formContext);
        return;
    }
    
    // Lookup字段取值：id是评分项目记录的GUID
    var itemGuid = creditItemValue[0].id.replace(/[{}]/g, "");
    
    // 查询评分项目信息（编码、名称、类型、分类）
    var fetchXml = [
        "<fetch top='1'>",
        "  <entity name='mcs_credit_items'>",
        "    <attribute name='mcs_credit_itemsno' />",
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
                
                // 带出评分项目编码
                var itemIdField = formContext.getAttribute("mcs_itemid");
                if (itemIdField) {
                    itemIdField.setValue(item.mcs_credit_itemsno || "");
                }
                
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
                // 映射: mcs_credit_items.mcs_group -> mcs_credit_scoringcard.mcs_typeid
                // 100000000(客户实力)->1, 100000001(客户财务)->2, 100000002(宏观市场)->3, 100000003(历史交易)->4
                var typeField = formContext.getAttribute("mcs_typeid");
                if (typeField && item.mcs_group !== undefined) {
                    var groupMap = {
                        100000000: 1,  // 客户实力
                        100000001: 2,  // 客户财务
                        100000002: 3,  // 宏观市场
                        100000003: 4   // 历史交易
                    };
                    var typeValue = groupMap[item.mcs_group];
                    if (typeValue !== undefined) {
                        typeField.setValue(typeValue);
                    }
                }
                
                // 更新显隐控制
                ScoringCardForm.toggleFieldsByDataType(formContext);
                
                // 注：mcs_listvalue现在是Lookup字段，不需要手动加载选项
                // Lookup选择窗口会自动显示关联实体的记录
                // 如需按评分项目过滤，请在D365中配置Lookup视图的过滤条件
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
    
    // 调试日志 - 关键信息
    console.log("=== toggleFieldsByDataType START ===");
    console.log("dataType raw value:", dataType);
    console.log("dataType type:", typeof dataType);
    
    // 转换为数字进行比较（D365选项集值可能是字符串）
    var dataTypeNum = dataType !== null ? Number(dataType) : null;
    console.log("dataTypeNum:", dataTypeNum);
    
    // 检查字段是否存在
    var listValueControl = formContext.getControl("mcs_listvalue");
    var minValueControl = formContext.getControl("mcs_minvalue");
    var maxValueControl = formContext.getControl("mcs_maxvalue");
    console.log("mcs_listvalue control exists:", !!listValueControl);
    console.log("mcs_minvalue control exists:", !!minValueControl);
    console.log("mcs_maxvalue control exists:", !!maxValueControl);
    
    if (dataTypeNum === 100000000) {
        // 定量(100000000)：显示min/max，隐藏listvalue
        console.log("分支: 定量");
        if (minValueControl) { minValueControl.setVisible(true); console.log("minvalue visible=true"); }
        if (maxValueControl) { maxValueControl.setVisible(true); console.log("maxvalue visible=true"); }
        if (listValueControl) { listValueControl.setVisible(false); console.log("listvalue visible=false"); }
        // 清除定性值
        var listValueField = formContext.getAttribute("mcs_listvalue");
        if (listValueField) listValueField.setValue(null);
        
    } else if (dataTypeNum === 100000001) {
        // 定性(100000001)：显示listvalue，隐藏min/max
        console.log("分支: 定性");
        if (minValueControl) { minValueControl.setVisible(false); console.log("minvalue visible=false"); }
        if (maxValueControl) { maxValueControl.setVisible(false); console.log("maxvalue visible=false"); }
        if (listValueControl) { listValueControl.setVisible(true); console.log("listvalue visible=true"); }
        // 清除定量值
        var minField = formContext.getAttribute("mcs_minvalue");
        var maxField = formContext.getAttribute("mcs_maxvalue");
        if (minField) minField.setValue(null);
        if (maxField) maxField.setValue(null);
        
    } else {
        // 未选择：全部隐藏
        console.log("分支: 未匹配，全部隐藏");
        if (minValueControl) minValueControl.setVisible(false);
        if (maxValueControl) maxValueControl.setVisible(false);
        if (listValueControl) listValueControl.setVisible(false);
    }
    console.log("=== toggleFieldsByDataType END ===");
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
    var dataTypeNum = dataType !== null ? Number(dataType) : null;
    
    // 定量时校验min/max
    if (dataTypeNum === 100000000) {
        var minValue = formContext.getAttribute("mcs_minvalue").getValue();
        var maxValue = formContext.getAttribute("mcs_maxvalue").getValue();
        
        if (minValue === null || maxValue === null) {
            Xrm.Utility.alertDialog("定量项目必须填写最小值和最大值");
            executionContext.getEventArgs().preventDefault();
            return;
        }
    }
    
    // 定性时校验listvalue (Lookup字段getValue()返回对象数组)
    if (dataTypeNum === 100000001) {
        var listValue = formContext.getAttribute("mcs_listvalue").getValue();
        if (!listValue || listValue.length === 0) {
            Xrm.Utility.alertDialog("定性项目必须选择定性项目值");
            executionContext.getEventArgs().preventDefault();
            return;
        }
    }
};
