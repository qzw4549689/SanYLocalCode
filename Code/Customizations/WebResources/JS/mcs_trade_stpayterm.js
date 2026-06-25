/**
 * 成交条件样板库 - 表单逻辑
 * 实体: mcs_trade_stpayterm
 * 功能: 克隆新增、表单默认值、Lookup 与文本字段同步、批量申请/审批/拒绝
 */

var TradeStPayTermForm = TradeStPayTermForm || {};
var TradeStPayTermGrid = TradeStPayTermGrid || {};

// Lookup 字段与文本字段映射配置
// parentField: 父级 Lookup 字段逻辑名
// parentFilterField: 目标实体上用于关联父级的字段逻辑名
TradeStPayTermForm.LOOKUP_CONFIG = {
    "mcs_businessunit": {
        codeField: "mcs_buid",
        nameField: "mcs_buname",
        targetEntity: "mcs_bu",
        targetCodeField: "mcs_code",
        targetNameField: "mcs_name"
    },
    "mcs_subsidiary": {
        codeField: "mcs_subid",
        nameField: "mcs_subname",
        targetEntity: "mcs_region",
        targetCodeField: "mcs_code",
        targetNameField: "mcs_name",
        parentField: "mcs_businessunit",
        parentFilterField: "mcs_buid"
    },
    "mcs_nation": {
        codeField: "mcs_countrycode",
        nameField: "mcs_countryname",
        targetEntity: "mcs_country",
        targetCodeField: "mcs_countrycode",
        targetNameField: "mcs_name",
        parentField: "mcs_subsidiary",
        parentFilterField: "mcs_region"
    },
    "mcs_trade_pttype": {
        codeField: "mcs_typeid",
        nameField: "mcs_typename",
        targetEntity: "mcs_trade_pttype",
        targetCodeField: "mcs_typeid",
        targetNameField: "mcs_trade_pttypename"
    }
};

/**
 * 表单保存事件（占位，避免窗体绑定报错）
 */
TradeStPayTermForm.onSave = function (executionContext) {
    // 当前无特殊保存逻辑，由 D365 默认逻辑处理
};

/**
 * 表单加载事件
 */
TradeStPayTermForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();

    // 注册 Lookup 字段变更事件
    TradeStPayTermForm.registerLookupEvents(formContext);

    // 注册级联过滤
    TradeStPayTermForm.registerCascadeFilters(formContext);

    // 设置只读字段
    TradeStPayTermForm.setReadOnlyFields(formContext);

    // 新建时如 URL 携带克隆源参数，则回填字段
    if (formType === 1) {
        TradeStPayTermForm.fillCloneData(formContext);
    }
};

/**
 * 设置只读字段
 * 标准条件编码由系统自动生成，生效状态由批量按钮控制流转
 */
TradeStPayTermForm.setReadOnlyFields = function (formContext) {
    var readOnlyFields = ["mcs_trade_stpaytermname", "mcs_status"];
    readOnlyFields.forEach(function (fieldName) {
        var control = formContext.getControl(fieldName);
        if (control && control.setDisabled) {
            control.setDisabled(true);
        }
    });
};

/**
 * 注册 Lookup 字段变更事件
 */
TradeStPayTermForm.registerLookupEvents = function (formContext) {
    Object.keys(TradeStPayTermForm.LOOKUP_CONFIG).forEach(function (lookupField) {
        var attr = formContext.getAttribute(lookupField);
        if (attr) {
            attr.addOnChange(function () {
                TradeStPayTermForm.onLookupChanged(formContext, lookupField);

                // 父级变更后，验证并清空不符合条件的子级
                TradeStPayTermForm.validateChildLookups(formContext, lookupField);
            });
        }
    });
};

/**
 * 注册 Lookup 级联过滤
 * 事业部 -> 大区 -> 国家
 */
TradeStPayTermForm.registerCascadeFilters = function (formContext) {
    Object.keys(TradeStPayTermForm.LOOKUP_CONFIG).forEach(function (lookupField) {
        var config = TradeStPayTermForm.LOOKUP_CONFIG[lookupField];
        if (!config.parentField) return;

        var control = formContext.getControl(lookupField);
        if (!control || !control.addPreSearch) return;

        control.addPreSearch(function () {
            TradeStPayTermForm.applyCascadeFilter(formContext, lookupField);
        });
    });
};

/**
 * 应用级联过滤条件
 * 说明：父级未选择时不过滤，允许子级独立选择全部数据
 */
TradeStPayTermForm.applyCascadeFilter = function (formContext, lookupField) {
    var config = TradeStPayTermForm.LOOKUP_CONFIG[lookupField];
    if (!config || !config.parentField || !config.parentFilterField) return;

    var parentAttr = formContext.getAttribute(config.parentField);
    if (!parentAttr) return;

    var parentValue = parentAttr.getValue();
    var control = formContext.getControl(lookupField);
    if (!control || !control.addCustomFilter) return;

    // 父级未选择时，不过滤（允许独立选择全部）
    if (!parentValue || parentValue.length === 0) {
        return;
    }

    var parentId = parentValue[0].id.replace(/[{}]/g, "");
    var filterXml = "<filter type=\"and\"><condition attribute=\"" + config.parentFilterField + "\" operator=\"eq\" value=\"" + parentId + "\" /></filter>";
    control.addCustomFilter(filterXml, config.targetEntity);
};

/**
 * Lookup 字段变更处理：同步编码和名称到对应文本字段
 */
TradeStPayTermForm.onLookupChanged = function (formContext, lookupField) {
    var config = TradeStPayTermForm.LOOKUP_CONFIG[lookupField];
    if (!config) return;

    var lookupAttr = formContext.getAttribute(lookupField);
    if (!lookupAttr) return;

    var lookupValue = lookupAttr.getValue();
    if (!lookupValue || lookupValue.length === 0) {
        // Lookup 清空时，同步清空文本字段
        TradeStPayTermForm.setFieldValue(formContext, config.codeField, null);
        TradeStPayTermForm.setFieldValue(formContext, config.nameField, null);

        // 清空下级关联 Lookup 及其文本字段
        TradeStPayTermForm.clearChildLookups(formContext, lookupField);
        return;
    }

    // 父级有值时，验证当前 Lookup 是否仍符合过滤条件；不符合则清空当前及下级
    if (config.parentField) {
        var parentAttr = formContext.getAttribute(config.parentField);
        if (parentAttr) {
            var parentValue = parentAttr.getValue();
            if (parentValue && parentValue.length > 0) {
                var parentId = parentValue[0].id.replace(/[{}]/g, "");
                var selectedId = lookupValue[0].id.replace(/[{}]/g, "");
                TradeStPayTermForm.validateLookupParent(formContext, lookupField, parentId, selectedId);
            }
        }
    }

    var selected = lookupValue[0];
    var recordId = selected.id.replace(/[{}]/g, "");

    // 先回填名称（Lookup 的 name 属性）
    TradeStPayTermForm.setFieldValue(formContext, config.nameField, selected.name);

    // 通过 WebAPI 查询编码字段
    Xrm.WebApi.retrieveRecord(config.targetEntity, recordId, "?$select=" + config.targetCodeField + "," + config.targetNameField)
        .then(function (result) {
            var code = result[config.targetCodeField];
            var name = result[config.targetNameField];
            TradeStPayTermForm.setFieldValue(formContext, config.codeField, code);
            if (name) {
                TradeStPayTermForm.setFieldValue(formContext, config.nameField, name);
            }
        })
        .catch(function (error) {
            console.error("查询 " + lookupField + " 编码失败: " + error.message);
        });
};

/**
 * 安全设置字段值
 */
TradeStPayTermForm.setFieldValue = function (formContext, fieldName, value) {
    var attr = formContext.getAttribute(fieldName);
    if (attr) {
        attr.setValue(value);
    }
};

/**
 * 清空指定 Lookup 字段及其关联文本字段
 */
TradeStPayTermForm.clearLookup = function (formContext, lookupField) {
    var config = TradeStPayTermForm.LOOKUP_CONFIG[lookupField];
    if (!config) return;

    TradeStPayTermForm.setFieldValue(formContext, lookupField, null);
    TradeStPayTermForm.setFieldValue(formContext, config.codeField, null);
    TradeStPayTermForm.setFieldValue(formContext, config.nameField, null);
};

/**
 * 清空指定 Lookup 字段及其所有下级 Lookup 字段
 */
TradeStPayTermForm.clearLookupAndChildren = function (formContext, lookupField) {
    TradeStPayTermForm.clearLookup(formContext, lookupField);
    TradeStPayTermForm.clearChildLookups(formContext, lookupField);
};

/**
 * 清空当前 Lookup 字段的所有下级 Lookup 字段
 */
TradeStPayTermForm.clearChildLookups = function (formContext, parentLookupField) {
    Object.keys(TradeStPayTermForm.LOOKUP_CONFIG).forEach(function (lookupField) {
        var config = TradeStPayTermForm.LOOKUP_CONFIG[lookupField];
        if (config.parentField === parentLookupField) {
            TradeStPayTermForm.clearLookupAndChildren(formContext, lookupField);
        }
    });
};

/**
 * 父级变更后，验证所有子级 Lookup 是否符合过滤条件
 */
TradeStPayTermForm.validateChildLookups = function (formContext, parentLookupField) {
    var parentAttr = formContext.getAttribute(parentLookupField);
    if (!parentAttr) return;

    var parentValue = parentAttr.getValue();
    var parentId = (parentValue && parentValue.length > 0) ? parentValue[0].id.replace(/[{}]/g, "") : null;

    Object.keys(TradeStPayTermForm.LOOKUP_CONFIG).forEach(function (lookupField) {
        var config = TradeStPayTermForm.LOOKUP_CONFIG[lookupField];
        if (config.parentField !== parentLookupField) return;

        var lookupAttr = formContext.getAttribute(lookupField);
        if (!lookupAttr) return;

        var lookupValue = lookupAttr.getValue();
        if (!lookupValue || lookupValue.length === 0) return;

        // 父级清空时，保留子级选择（允许独立选择）
        if (!parentId) return;

        var selectedId = lookupValue[0].id.replace(/[{}]/g, "");
        TradeStPayTermForm.validateLookupParent(formContext, lookupField, parentId, selectedId);
    });
};

/**
 * 验证 Lookup 选中记录是否符合父级过滤条件；不符合则清空
 */
TradeStPayTermForm.validateLookupParent = function (formContext, lookupField, parentId, selectedId) {
    var config = TradeStPayTermForm.LOOKUP_CONFIG[lookupField];
    if (!config || !config.parentFilterField || !config.targetEntity) return;

    var filter = "$select=" + config.parentFilterField + "&$filter=" + config.targetEntity + "id eq " + selectedId;
    Xrm.WebApi.retrieveRecord(config.targetEntity, selectedId, "?$select=" + config.parentFilterField)
        .then(function (result) {
            var actualParentRef = result[config.parentFilterField];
            if (!actualParentRef) {
                TradeStPayTermForm.clearLookupAndChildren(formContext, lookupField);
                return;
            }

            var actualParentId = actualParentRef.toString().replace(/[{}]/g, "");
            if (actualParentId !== parentId) {
                TradeStPayTermForm.clearLookupAndChildren(formContext, lookupField);
            }
        })
        .catch(function (error) {
            console.error("验证 " + lookupField + " 父级关系失败: " + error.message);
        });
};

/**
 * 克隆当前记录：打开新建表单并预填充字段
 */
TradeStPayTermForm.cloneRecord = function (primaryControl) {
    var formContext = primaryControl;
    var entityName = formContext.data.entity.getEntityName();
    var entityId = formContext.data.entity.getId();

    if (!entityId) {
        Xrm.Navigation.openAlertDialog({ text: "请先保存当前记录后再克隆。" });
        return;
    }

    var parameters = {};

    // 复制文本字段
    var textFields = [
        "mcs_buid", "mcs_buname",
        "mcs_subid", "mcs_subname",
        "mcs_countrycode", "mcs_countryname",
        "mcs_typeid", "mcs_typename",
        "mcs_buyergrade", "mcs_creditgrade",
        "mcs_downpay", "mcs_payterm", "mcs_payfreq"
    ];

    textFields.forEach(function (fieldName) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) {
            var value = attr.getValue();
            if (value !== null && value !== undefined) {
                parameters[fieldName] = value;
            }
        }
    });

    // 复制 Lookup 字段
    Object.keys(TradeStPayTermForm.LOOKUP_CONFIG).forEach(function (lookupField) {
        var attr = formContext.getAttribute(lookupField);
        if (attr) {
            var value = attr.getValue();
            if (value && value.length > 0) {
                parameters[lookupField] = value;
            }
        }
    });

    // 生效状态重置为未生效
    parameters["mcs_status"] = 0;

    Xrm.Navigation.openForm({
        entityName: entityName,
        formId: null,
        openInNewWindow: false,
        useQuickCreate: false
    }, parameters);
};

/**
 * 新建表单时，从 URL 参数回填字段
 */
TradeStPayTermForm.fillCloneData = function (formContext) {
    var url = Xrm.Utility.getGlobalContext().getCurrentAppUrl
        ? Xrm.Utility.getGlobalContext().getCurrentAppUrl()
        : window.parent.location.href;

    var params = TradeStPayTermForm.parseUrlParams(url);
    if (!params) {
        return;
    }

    // 处理文本字段
    var textFields = [
        "mcs_buid", "mcs_buname",
        "mcs_subid", "mcs_subname",
        "mcs_countrycode", "mcs_countryname",
        "mcs_typeid", "mcs_typename",
        "mcs_buyergrade", "mcs_creditgrade",
        "mcs_downpay", "mcs_payterm", "mcs_payfreq", "mcs_status"
    ];

    textFields.forEach(function (fieldName) {
        if (params.hasOwnProperty(fieldName)) {
            var attr = formContext.getAttribute(fieldName);
            if (attr && !attr.getValue()) {
                var rawValue = params[fieldName];
                var value = rawValue;

                if (fieldName === "mcs_downpay") {
                    value = parseFloat(rawValue);
                } else if (fieldName === "mcs_payterm" || fieldName === "mcs_payfreq" || fieldName === "mcs_status") {
                    value = parseInt(rawValue, 10);
                }

                attr.setValue(value);
            }
        }
    });

    // Lookup 字段由 D365 表单引擎自动从 URL 参数解析（如果参数名是 Lookup 字段逻辑名）
    // 这里不需要额外处理
};

/**
 * 解析 URL 查询参数
 */
TradeStPayTermForm.parseUrlParams = function (url) {
    if (!url) return null;
    var queryIndex = url.indexOf("?");
    if (queryIndex < 0) return null;

    var query = url.substring(queryIndex + 1);
    var pairs = query.split("&");
    var result = {};

    pairs.forEach(function (pair) {
        var eq = pair.indexOf("=");
        if (eq > 0) {
            var key = decodeURIComponent(pair.substring(0, eq));
            var value = decodeURIComponent(pair.substring(eq + 1));
            result[key] = value;
        }
    });

    return result;
};

// ==================== 列表视图批量操作（阶段 3） ====================

/**
 * 批量申请：将选中记录状态更新为 1（待审批）
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 */
TradeStPayTermGrid.apply = function (selectedIds) {
    TradeStPayTermGrid.batchUpdateStatus(selectedIds, 1, "申请");
};

/**
 * 批量审批：将选中记录状态更新为 2（生效）
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 */
TradeStPayTermGrid.approve = function (selectedIds) {
    TradeStPayTermGrid.batchUpdateStatus(selectedIds, 2, "审批");
};

/**
 * 批量拒绝：将选中记录状态更新为 0（未生效）
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 */
TradeStPayTermGrid.reject = function (selectedIds) {
    TradeStPayTermGrid.batchUpdateStatus(selectedIds, 0, "拒绝");
};

/**
 * 批量更新状态通用方法
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 * @param {number} status - 目标状态值
 * @param {string} actionName - 操作名称（用于提示）
 */
TradeStPayTermGrid.batchUpdateStatus = function (selectedIds, status, actionName) {
    var selected = [];

    if (Array.isArray(selectedIds)) {
        selected = selectedIds;
    } else if (typeof selectedIds === "string" && selectedIds.length > 0) {
        // Command Designer 的 SelectedControlSelectedItemIds 可能传逗号分隔字符串
        selected = selectedIds.split(",").map(function (id) { return id.trim().replace(/[{}]/g, ""); });
    } else {
        Xrm.Navigation.openAlertDialog({ text: "无法获取选中的记录，请检查按钮参数配置。" });
        return;
    }

    if (!selected || selected.length === 0) {
        Xrm.Navigation.openAlertDialog({ text: "请至少选择一条记录。" });
        return;
    }

    Xrm.Navigation.openConfirmDialog({
        title: "确认" + actionName,
        text: "确定要" + actionName + "选中的 " + selected.length + " 条记录吗？"
    }).then(function (success) {
        if (!success.confirmed) return;

        var updateData = {
            mcs_status: status
        };

        var promises = selected.map(function (id) {
            return Xrm.WebApi.updateRecord("mcs_trade_stpayterm", id, updateData);
        });

        Promise.all(promises).then(function () {
            Xrm.Navigation.openAlertDialog({ text: actionName + "成功。" }).then(function () {
                // 没有 grid control 参数时，刷新当前页面
                window.location.reload();
            });
        }).catch(function (error) {
            Xrm.Navigation.openAlertDialog({ text: actionName + "失败：" + error.message });
        });
    });
};
