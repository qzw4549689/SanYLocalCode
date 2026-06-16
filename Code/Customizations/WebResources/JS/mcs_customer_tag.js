/**
 * 客户信用标签表 - 表单逻辑
 * 实体: mcs_customer_tag
 * 功能: 数据集成值展示、人工复核编辑控制、评分结果展示
 * 影响范围: 仅限mcs_customer_tag实体
 */

var CustomerTagForm = CustomerTagForm || {};

/**
 * 表单加载事件
 */
CustomerTagForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    
    // 设置字段只读
    CustomerTagForm.setFieldsReadOnly(formContext);
    
    // 注册字段变更事件
    CustomerTagForm.registerEvents(formContext);
    
    // 根据数据类型控制复核字段显隐
    CustomerTagForm.toggleReviewFields(formContext);
    
    // 根据评估状态控制可编辑性
    CustomerTagForm.toggleEditableByStatus(formContext);
};

/**
 * 设置字段只读
 * 集成值、计算字段、关联字段只读
 */
CustomerTagForm.setFieldsReadOnly = function (formContext) {
    // 关联字段始终只读
    var readOnlyFields = [
        "mcs_scoreid",        // 信用评估编码
        "mcs_credit_record",  // 信用评估（Lookup）
        "mcs_credit_item",    // 评分项目（Lookup）
        "mcs_accountid",    // 客户编码
        "mcs_group",        // 评分项目分类
        "mcs_itemid",       // 指标编码
        "mcs_itemname",     // 评分项目名称
        "mcs_itemdesc",     // 评分项目说明
        "mcs_datatype",     // 数据类型
        "mcs_itemintvalue1", // 集成定量指标
        "mcs_itemtxtvalue1", // 集成定性指标
        "mcs_itemvalue1",    // 集成指标（合并展示）
        "mcs_itemvalue2",    // 复核指标（合并展示）
        "mcs_scorevalue",    // 得分值
        "mcs_isscore",       // 是否评分
        "mcs_active"         // 有效状态
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
CustomerTagForm.registerEvents = function (formContext) {
    // 复核定量指标变更 - 更新合并展示值
    var intValue2Field = formContext.getAttribute("mcs_itemintvalue2");
    if (intValue2Field) {
        intValue2Field.addOnChange(CustomerTagForm.onReviewValueChange);
    }
    
    // 复核定性指标变更 - 更新合并展示值
    var txtValue2Field = formContext.getAttribute("mcs_itemtxtvalue2");
    if (txtValue2Field) {
        txtValue2Field.addOnChange(CustomerTagForm.onReviewValueChange);
    }
};

/**
 * 复核值变更事件
 * 更新复核合并展示值
 */
CustomerTagForm.onReviewValueChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var dataType = dataTypeField ? dataTypeField.getValue() : null;
    
    var reviewValue = "";
    
    if (dataType === 1) {
        // 定量：取复核定量指标
        var intValue2 = formContext.getAttribute("mcs_itemintvalue2");
        if (intValue2) {
            var val = intValue2.getValue();
            reviewValue = val !== null ? val.toString() : "";
        }
    } else if (dataType === 2) {
        // 定性：取复核定性指标
        var txtValue2 = formContext.getAttribute("mcs_itemtxtvalue2");
        if (txtValue2) {
            reviewValue = txtValue2.getValue() || "";
        }
    }
    
    // 更新复核合并展示值
    var itemValue2Field = formContext.getAttribute("mcs_itemvalue2");
    if (itemValue2Field) {
        itemValue2Field.setValue(reviewValue);
    }
};

/**
 * 根据数据类型控制字段显隐
 * 定量：显示集成/复核定量指标，隐藏集成/复核定性指标
 * 定性：显示集成/复核定性指标，隐藏集成/复核定量指标
 */
CustomerTagForm.toggleReviewFields = function (formContext) {
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var dataType = dataTypeField ? dataTypeField.getValue() : null;
    
    // 集成字段显隐控制
    var intValue1Control = formContext.getControl("mcs_itemintvalue1");
    var txtValue1Control = formContext.getControl("mcs_itemtxtvalue1");
    
    // 复核字段显隐控制
    var intReviewControl = formContext.getControl("mcs_itemintvalue2");
    var txtReviewControl = formContext.getControl("mcs_itemtxtvalue2");
    
    if (dataType === 1) {
        // 定量：显示定量字段，隐藏定性字段
        if (intValue1Control) intValue1Control.setVisible(true);
        if (txtValue1Control) txtValue1Control.setVisible(false);
        if (intReviewControl) intReviewControl.setVisible(true);
        if (txtReviewControl) txtReviewControl.setVisible(false);
    } else if (dataType === 2) {
        // 定性：显示定性字段，隐藏定量字段
        if (intValue1Control) intValue1Control.setVisible(false);
        if (txtValue1Control) txtValue1Control.setVisible(true);
        if (intReviewControl) intReviewControl.setVisible(false);
        if (txtReviewControl) txtReviewControl.setVisible(true);
    } else {
        // 未选择：都隐藏
        if (intValue1Control) intValue1Control.setVisible(false);
        if (txtValue1Control) txtValue1Control.setVisible(false);
        if (intReviewControl) intReviewControl.setVisible(false);
        if (txtReviewControl) txtReviewControl.setVisible(false);
    }
};

/**
 * 根据评估状态控制复核字段可编辑性
 * 只有在【人工复核】状态才允许编辑复核字段
 */
CustomerTagForm.toggleEditableByStatus = function (formContext) {
    // 获取关联的评估记录状态（通过mcs_scoreid查找）
    var scoreIdField = formContext.getAttribute("mcs_scoreid");
    if (!scoreIdField || !scoreIdField.getValue()) return;
    
    var scoreId = scoreIdField.getValue();
    
    // 查询评估记录状态
    var fetchXml = [
        "<fetch top='1'>",
        "  <entity name='mcs_credit_record'>",
        "    <attribute name='mcs_status' />",
        "    <filter>",
        "      <condition attribute='mcs_scoreid' operator='eq' value='" + scoreId + "' />",
        "    </filter>",
        "  </entity>",
        "</fetch>"
    ].join("");
    
    Xrm.WebApi.retrieveMultipleRecords("mcs_credit_record", "?fetchXml=" + encodeURIComponent(fetchXml))
        .then(function (result) {
            if (result.entities.length > 0) {
                var status = result.entities[0].mcs_status;
                // 12 = 人工复核状态，才允许编辑复核字段
                var isReviewStatus = (status === 12);
                
                // 同时需要校验评分项目是否允许人工补录
                CustomerTagForm.setReviewFieldsEditable(formContext, isReviewStatus);
            }
        })
        .catch(function (error) {
            console.error("查询评估记录状态失败:", error);
        });
};

/**
 * 设置复核字段可编辑性
 */
CustomerTagForm.setReviewFieldsEditable = function (formContext, isReviewStatus) {
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var dataType = dataTypeField ? dataTypeField.getValue() : null;
    
    var editable = false;
    
    if (isReviewStatus) {
        // 需要校验该评分项目是否允许人工补录
        // 这里简化处理：实际应查询mcs_credit_items的mcs_validate字段
        // 由于表单上可能没有直接关联，先开放编辑，由Plugin校验
        editable = true;
    }
    
    if (dataType === 1) {
        // 定量
        var intReviewControl = formContext.getControl("mcs_itemintvalue2");
        if (intReviewControl) intReviewControl.setDisabled(!editable);
    } else if (dataType === 2) {
        // 定性
        var txtReviewControl = formContext.getControl("mcs_itemtxtvalue2");
        if (txtReviewControl) txtReviewControl.setDisabled(!editable);
    }
};

/**
 * 保存前校验
 */
CustomerTagForm.onSave = function (executionContext) {
    var formContext = executionContext.getFormContext();
    
    // 校验必填字段
    var requiredFields = [
        { name: "mcs_credit_record", label: "信用评估" },
        { name: "mcs_accountid", label: "客户编码" },
        { name: "mcs_credit_item", label: "评分项目" }
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
