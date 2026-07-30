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
    console.log("CustomerTagForm.onLoad start");
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    console.log("CustomerTagForm.onLoad formType=", formType);
    
    // 设置字段只读
    CustomerTagForm.setFieldsReadOnly(formContext);
    
    // 注册字段变更事件
    CustomerTagForm.registerEvents(formContext);
    
    // 根据数据类型控制复核字段显隐
    CustomerTagForm.toggleReviewFields(formContext);
    
    // 根据评估状态控制可编辑性
    CustomerTagForm.toggleEditableByStatus(formContext);
    
    // 为定性复核Lookup添加按评分项目过滤
    CustomerTagForm.filterReviewLookup(formContext);
    
    // 兼容过渡期：如果新Lookup为空但旧文本字段有值，把旧值显示到合并展示字段
    CustomerTagForm.syncDisplayFromLegacyFields(formContext);
};

/**
 * 设置字段只读
 * 集成值、计算字段、关联字段只读
 */
CustomerTagForm.setFieldsReadOnly = function (formContext) {
    // 关联字段和展示字段始终只读
    var readOnlyFields = [
        "mcs_scoreid",        // 信用评估编码
        "mcs_credit_record",  // 信用评估（Lookup）
        "mcs_credit_item",    // 评分项目（Lookup）- 默认锁定，复核新建时动态解锁
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
        "mcs_itemtxtvalue2", // 复核定性指标（旧文本字段，过渡期保留）
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
    
    // 复核定性指标(Lookup)变更 - 更新合并展示值
    var credititemValueField = formContext.getAttribute("mcs_credititem_value");
    if (credititemValueField) {
        credititemValueField.addOnChange(CustomerTagForm.onReviewValueChange);
    }
    
    // 评分项目变更 - 新建时带出信息并更新过滤
    var creditItemField = formContext.getAttribute("mcs_credit_item");
    if (creditItemField) {
        creditItemField.addOnChange(CustomerTagForm.onCreditItemChange);
    }
};

/**
 * 复核值变更事件
 * 更新复核合并展示值
 */
CustomerTagForm.onReviewValueChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var rawDataType = dataTypeField ? dataTypeField.getValue() : null;
    var dataType = rawDataType != null ? parseInt(rawDataType, 10) : null;
    
    var reviewValue = "";
    
    if (dataType === 1) {
        // 定量：取复核定量指标
        var intValue2 = formContext.getAttribute("mcs_itemintvalue2");
        if (intValue2) {
            var val = intValue2.getValue();
            reviewValue = val !== null ? val.toString() : "";
        }
    } else if (dataType === 2) {
        // 定性：取复核定性指标Lookup的显示名
        var lookupField = formContext.getAttribute("mcs_credititem_value");
        if (lookupField) {
            var lookupValue = lookupField.getValue();
            if (lookupValue && lookupValue.length > 0) {
                reviewValue = lookupValue[0].name || "";
            }
        }
        
        // 兼容过渡期：新Lookup为空则回退旧文本字段
        if (!reviewValue) {
            var txtValue2 = formContext.getAttribute("mcs_itemtxtvalue2");
            if (txtValue2) {
                reviewValue = txtValue2.getValue() || "";
            }
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
 * 定性：显示集成/复核定性指标(Lookup)，隐藏集成/复核定量指标
 */
CustomerTagForm.toggleReviewFields = function (formContext) {
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var rawDataType = dataTypeField ? dataTypeField.getValue() : null;
    // 兼容数字和字符串返回值
    var dataType = rawDataType != null ? parseInt(rawDataType, 10) : null;
    
    console.log("CustomerTagForm.toggleReviewFields: dataType=", dataType);
    
    // 集成字段显隐控制
    var intValue1Control = formContext.getControl("mcs_itemintvalue1");
    var txtValue1Control = formContext.getControl("mcs_itemtxtvalue1");
    
    // 复核字段显隐控制
    var intReviewControl = formContext.getControl("mcs_itemintvalue2");
    var lookupReviewControl = formContext.getControl("mcs_credititem_value");
    // 旧文本字段在过渡期不显示
    var txtReviewControl = formContext.getControl("mcs_itemtxtvalue2");
    
    console.log("Controls: intValue1=", !!intValue1Control, "txtValue1=", !!txtValue1Control,
                "intReview=", !!intReviewControl, "lookupReview=", !!lookupReviewControl,
                "txtReview=", !!txtReviewControl);
    
    if (dataType === 1) {
        // 定量：显示定量字段，隐藏定性字段
        if (intValue1Control) intValue1Control.setVisible(true);
        if (txtValue1Control) txtValue1Control.setVisible(false);
        if (intReviewControl) intReviewControl.setVisible(true);
        if (lookupReviewControl) lookupReviewControl.setVisible(false);
        if (txtReviewControl) txtReviewControl.setVisible(false);
    } else if (dataType === 2) {
        // 定性：显示定性字段，隐藏定量字段
        if (intValue1Control) intValue1Control.setVisible(false);
        if (txtValue1Control) txtValue1Control.setVisible(true);
        if (intReviewControl) intReviewControl.setVisible(false);
        if (lookupReviewControl) lookupReviewControl.setVisible(true);
        if (txtReviewControl) txtReviewControl.setVisible(false);
    } else {
        // 未选择：都隐藏
        if (intValue1Control) intValue1Control.setVisible(false);
        if (txtValue1Control) txtValue1Control.setVisible(false);
        if (intReviewControl) intReviewControl.setVisible(false);
        if (lookupReviewControl) lookupReviewControl.setVisible(false);
        if (txtReviewControl) txtReviewControl.setVisible(false);
    }
};

/**
 * 根据评估状态控制复核字段可编辑性
 * 只有在【人工复核】状态才允许编辑复核字段
 */
CustomerTagForm.toggleEditableByStatus = function (formContext) {
    // 优先通过关联的信用评估记录（mcs_credit_record）获取状态，
    // 该字段在表单上可直接取到 ID，比 mcs_scoreid 更可靠。
    var creditRecordField = formContext.getAttribute("mcs_credit_record");
    var creditRecordId = null;
    
    if (creditRecordField && creditRecordField.getValue()) {
        var lookupValue = creditRecordField.getValue();
        if (Array.isArray(lookupValue) && lookupValue.length > 0) {
            creditRecordId = lookupValue[0].id;
        } else if (lookupValue.id) {
            creditRecordId = lookupValue.id;
        }
    }
    
    // 若表单上未加载信用评估 lookup，则回退到 mcs_scoreid 查询（兼容旧数据）
    if (!creditRecordId) {
        var scoreIdField = formContext.getAttribute("mcs_scoreid");
        if (!scoreIdField || !scoreIdField.getValue()) {
            CustomerTagForm.setReviewFieldsEditable(formContext, false);
            return;
        }
        
        var scoreId = scoreIdField.getValue();
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
                    CustomerTagForm.setReviewFieldsEditable(formContext, status === 12);
                } else {
                    CustomerTagForm.setReviewFieldsEditable(formContext, false);
                }
            })
            .catch(function (error) {
                console.error("查询评估记录状态失败:", error);
                CustomerTagForm.setReviewFieldsEditable(formContext, false);
            });
        return;
    }
    
    // 通过 lookup ID 直接查询单条记录状态
    Xrm.WebApi.retrieveRecord("mcs_credit_record", creditRecordId, "?$select=mcs_status")
        .then(function (result) {
            // 12 = 人工复核状态，才允许编辑复核字段
            CustomerTagForm.setReviewFieldsEditable(formContext, result.mcs_status === 12);
        })
        .catch(function (error) {
            console.error("查询评估记录状态失败:", error);
            CustomerTagForm.setReviewFieldsEditable(formContext, false);
        });
};

/**
 * 设置复核字段可编辑性
 */
CustomerTagForm.setReviewFieldsEditable = function (formContext, isReviewStatus) {
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var rawDataType = dataTypeField ? dataTypeField.getValue() : null;
    var dataType = rawDataType != null ? parseInt(rawDataType, 10) : null;
    var formType = formContext.ui.getFormType();
    
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
        var lookupReviewControl = formContext.getControl("mcs_credititem_value");
        if (lookupReviewControl) lookupReviewControl.setDisabled(!editable);
    }
    
    // 新建记录且在复核状态，允许选择评分项目
    if (isReviewStatus && formType === 1) {
        var creditItemControl = formContext.getControl("mcs_credit_item");
        if (creditItemControl) creditItemControl.setDisabled(false);
    }
};

/**
 * 评分项目变更事件
 * 仅新建模式下有效：带出评分项目信息并更新Lookup过滤
 */
CustomerTagForm.onCreditItemChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    
    // 仅新建记录需要处理
    if (formType !== 1) return;
    
    var creditItemField = formContext.getAttribute("mcs_credit_item");
    if (!creditItemField) return;
    
    var lookupValue = creditItemField.getValue();
    if (!lookupValue || lookupValue.length === 0) {
        // 清空时重置数据类型和字段
        CustomerTagForm.clearItemDerivedFields(formContext);
        return;
    }
    
    var itemId = lookupValue[0].id;
    var itemName = lookupValue[0].name || "";
    
    // 查询评分项目信息
    Xrm.WebApi.retrieveRecord("mcs_credit_items", itemId, "?$select=mcs_credit_itemsno,mcs_itemname,mcs_itemdesc,mcs_datatype,mcs_group")
        .then(function (result) {
            // 设置数据类型
            if (result.mcs_datatype !== undefined && result.mcs_datatype !== null) {
                formContext.getAttribute("mcs_datatype").setValue(result.mcs_datatype);
            }
            
            // 设置评分项目名称、说明、分类、编码
            if (result.mcs_itemname) {
                var itemNameAttr = formContext.getAttribute("mcs_itemname");
                if (itemNameAttr) itemNameAttr.setValue(result.mcs_itemname);
            }
            if (result.mcs_itemdesc) {
                var itemDescAttr = formContext.getAttribute("mcs_itemdesc");
                if (itemDescAttr) itemDescAttr.setValue(result.mcs_itemdesc);
            }
            if (result.mcs_group !== undefined && result.mcs_group !== null) {
                var groupAttr = formContext.getAttribute("mcs_group");
                if (groupAttr) groupAttr.setValue(result.mcs_group);
            }
            if (result.mcs_credit_itemsno) {
                var itemCodeAttr = formContext.getAttribute("mcs_itemcode");
                if (itemCodeAttr) itemCodeAttr.setValue(result.mcs_credit_itemsno);
                // 兼容旧字段 mcs_itemid
                var itemIdAttr = formContext.getAttribute("mcs_itemid");
                if (itemIdAttr) itemIdAttr.setValue(result.mcs_credit_itemsno);
            }
            
            // 清空已有的复核值（因为评分项目变了）
            var lookupReviewAttr = formContext.getAttribute("mcs_credititem_value");
            if (lookupReviewAttr) lookupReviewAttr.setValue(null);
            var intReviewAttr = formContext.getAttribute("mcs_itemintvalue2");
            if (intReviewAttr) intReviewAttr.setValue(null);
            var itemValue2Attr = formContext.getAttribute("mcs_itemvalue2");
            if (itemValue2Attr) itemValue2Attr.setValue(null);
            
            // 重新控制字段显隐和过滤
            CustomerTagForm.toggleReviewFields(formContext);
            CustomerTagForm.filterReviewLookup(formContext);
        })
        .catch(function (error) {
            console.error("查询评分项目信息失败:", error);
            Xrm.Utility.alertDialog("评分项目信息带出失败：" + error.message);
        });
};

/**
 * 清空评分项目带出字段
 */
CustomerTagForm.clearItemDerivedFields = function (formContext) {
    var fields = ["mcs_datatype", "mcs_itemname", "mcs_itemdesc", "mcs_group", "mcs_itemcode", "mcs_itemid"];
    fields.forEach(function (fieldName) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) attr.setValue(null);
    });
    CustomerTagForm.toggleReviewFields(formContext);
};

/**
 * 为定性复核Lookup添加按评分项目过滤
 */
CustomerTagForm.filterReviewLookup = function (formContext) {
    var lookupControl = formContext.getControl("mcs_credititem_value");
    if (!lookupControl) return;
    
    var creditItemField = formContext.getAttribute("mcs_credit_item");
    if (!creditItemField || !creditItemField.getValue() || creditItemField.getValue().length === 0) {
        // 未选择评分项目时不加过滤（或过滤为无结果）
        return;
    }
    
    var itemId = creditItemField.getValue()[0].id;
    
    // 移除旧的 PreSearch 处理器（如果存在）
    if (lookupControl.removePreSearch && CustomerTagForm._preSearchHandler) {
        lookupControl.removePreSearch(CustomerTagForm._preSearchHandler);
    }
    
    CustomerTagForm._preSearchHandler = function () {
        var fetchXml = [
            "<filter type='and'>",
            "  <condition attribute='mcs_credititemno' operator='eq' value='" + itemId + "' />",
            "  <condition attribute='statecode' operator='eq' value='0' />",
            "</filter>"
        ].join("");
        lookupControl.addCustomFilter(fetchXml, "mcs_credititem_value");
    };
    
    lookupControl.addPreSearch(CustomerTagForm._preSearchHandler);
};

/**
 * 兼容过渡期：新Lookup为空但旧文本字段有值时，回退显示旧值
 */
CustomerTagForm.syncDisplayFromLegacyFields = function (formContext) {
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var rawDataType = dataTypeField ? dataTypeField.getValue() : null;
    var dataType = rawDataType != null ? parseInt(rawDataType, 10) : null;
    
    if (dataType !== 2) return;
    
    var lookupField = formContext.getAttribute("mcs_credititem_value");
    var hasLookupValue = lookupField && lookupField.getValue() && lookupField.getValue().length > 0;
    if (hasLookupValue) return;
    
    var txtValue2 = formContext.getAttribute("mcs_itemtxtvalue2");
    if (!txtValue2 || !txtValue2.getValue()) return;
    
    var itemValue2Field = formContext.getAttribute("mcs_itemvalue2");
    if (itemValue2Field && !itemValue2Field.getValue()) {
        itemValue2Field.setValue(txtValue2.getValue());
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
    
    // 定性项目必须选择定性项目值
    var dataTypeField = formContext.getAttribute("mcs_datatype");
    var rawDataType = dataTypeField ? dataTypeField.getValue() : null;
    var dataType = rawDataType != null ? parseInt(rawDataType, 10) : null;
    if (dataType === 2) {
        var lookupField = formContext.getAttribute("mcs_credititem_value");
        var hasLookupValue = lookupField && lookupField.getValue() && lookupField.getValue().length > 0;
        if (!hasLookupValue) {
            Xrm.Utility.alertDialog("定性评分项目必须选择复核定性指标。");
            executionContext.getEventArgs().preventDefault();
            return;
        }
    }
};
