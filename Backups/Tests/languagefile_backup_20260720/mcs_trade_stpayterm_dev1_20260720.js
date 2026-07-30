/**
 * 成交条件样板库 - 表单逻辑
 * 实体: mcs_trade_stpayterm
 * 功能: 克隆新增、表单默认值、Lookup 与文本字段同步、批量申请/审批/拒绝
 */

var TradeStPayTermForm = TradeStPayTermForm || {};
var TradeStPayTermGrid = TradeStPayTermGrid || {};

// ==================== 角色权限配置（2026-07-20 按业务角色矩阵落地） ====================
// 注意：按 D365 安全角色"名称"匹配，若环境中角色改名需同步修改此处
// CREATOR  = 成交条件制定人（发起配置/申请审批）
// APPROVER = 成交条件审批人（审核配置数据）
// ADMIN    = 系统管理员（放行，便于管理与测试）
TradeStPayTermForm.ROLES = {
    CREATOR: "风控配置管理员",
    APPROVER: "事业部军长",
    ADMIN: "System Administrator"
};

/**
 * 判断当前用户是否拥有指定角色中的任意一个
 * @param {string[]} roleNames - D365 安全角色名称数组
 * @returns {boolean}
 */
TradeStPayTermForm.currentUserHasAnyRole = function (roleNames) {
    var roles = Xrm.Utility.getGlobalContext().userSettings.roles;
    if (!roles) {
        return false;
    }

    var matched = false;
    roles.forEach(function (r) {
        if (roleNames.indexOf(r.name) >= 0) {
            matched = true;
        }
    });
    return matched;
};

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
    }
};

// 多选查找组件字段配置
// 多行文本字段存储 GUID 逗号分隔，变更时同步编码/名称到对应文本字段
TradeStPayTermForm.MULTISELECT_LOOKUP_CONFIG = {
    "mcs_countries": {
        targetEntity: "mcs_country",
        codeField: "mcs_countrycode",
        nameField: "mcs_countryname",
        targetCodeField: "mcs_countrycode",
        targetNameField: "mcs_name"
    },
    "mcs_trade_type": {
        targetEntity: "mcs_trade_pttype",
        codeField: "mcs_typeid",
        nameField: "mcs_typename",
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

    // 注册多选查找组件字段变更事件（同步编码/名称）
    TradeStPayTermForm.registerMultiSelectLookupEvents(formContext);

    // 注册级联过滤
    TradeStPayTermForm.registerCascadeFilters(formContext);

    // 设置只读字段
    TradeStPayTermForm.setReadOnlyFields(formContext);

    // 控制克隆新增按钮显隐（仅成交条件制定人【风控配置管理员】或系统管理员可见）
    TradeStPayTermForm.hideCloneButtonIfNoPermission();

    // 新建时如 URL 携带克隆源参数，则回填字段
    if (formType === 1) {
        TradeStPayTermForm.fillCloneData(formContext);
    }
};

/**
 * 判断当前用户是否有克隆新增按钮权限
 * 成交条件制定人【风控配置管理员】或系统管理员可见
 */
TradeStPayTermForm.hasCloneButtonPermission = function () {
    return TradeStPayTermForm.currentUserHasAnyRole([
        TradeStPayTermForm.ROLES.CREATOR,
        TradeStPayTermForm.ROLES.ADMIN
    ]);
};

/**
 * 无权限时隐藏克隆新增按钮
 * 注意：Modern Command Bar 渲染有延迟，通过轮询定位按钮
 */
TradeStPayTermForm.hideCloneButtonIfNoPermission = function () {
    if (TradeStPayTermForm.hasCloneButtonPermission()) {
        return;
    }

    var maxRetry = 30;
    var retry = 0;
    var timer = setInterval(function () {
        // 通过 data-id 或 aria-label 定位克隆新增按钮
        var buttons = document.querySelectorAll(
            '[data-id*="mcs_trade_stpayterm_clone"], [aria-label="克隆新增"]'
        );

        if (buttons.length > 0) {
            buttons.forEach(function (btn) {
                btn.style.display = "none";
                btn.style.visibility = "hidden";
            });
            clearInterval(timer);
        }

        retry++;
        if (retry >= maxRetry) {
            clearInterval(timer);
        }
    }, 300);
};

/**
 * 设置只读字段
 * 标准条件编码由系统自动生成，生效状态由批量按钮控制流转。
 * 待审批（1）/ 生效（2）状态下全表单只读：
 * 防止审批中的内容被修改导致审批不一致，生效记录不允许直接变更（应克隆新建）。
 */
TradeStPayTermForm.setReadOnlyFields = function (formContext) {
    var statusAttr = formContext.getAttribute("mcs_status");
    var status = statusAttr ? statusAttr.getValue() : null;

    // 待审批（1）或生效（2）：禁用全部字段
    if (status === 1 || status === 2) {
        formContext.data.entity.attributes.forEach(function (attr) {
            attr.controls.forEach(function (control) {
                if (control.setDisabled) {
                    control.setDisabled(true);
                }
            });
        });
        return;
    }

    // 未生效（0）或新建：仅禁用固定只读字段
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
            });
        }
    });
};

/**
 * 注册多选查找组件字段变更事件
 * 多行文本字段值变化时，同步编码/名称到对应文本字段
 */
TradeStPayTermForm.registerMultiSelectLookupEvents = function (formContext) {
    Object.keys(TradeStPayTermForm.MULTISELECT_LOOKUP_CONFIG).forEach(function (fieldName) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) {
            attr.addOnChange(function () {
                TradeStPayTermForm.onMultiSelectLookupChanged(formContext, fieldName);
            });
        }
    });
};

/**
 * 多选查找组件字段变更处理
 * 解析 GUID 逗号分隔列表，查询对应编码/名称，同步到文本字段
 */
TradeStPayTermForm.onMultiSelectLookupChanged = function (formContext, fieldName) {
    var config = TradeStPayTermForm.MULTISELECT_LOOKUP_CONFIG[fieldName];
    if (!config) return;

    var attr = formContext.getAttribute(fieldName);
    if (!attr) return;

    var rawValue = attr.getValue();
    if (!rawValue) {
        TradeStPayTermForm.setFieldValue(formContext, config.codeField, null);
        TradeStPayTermForm.setFieldValue(formContext, config.nameField, null);
        return;
    }

    var guids = rawValue.toString().split(",").map(function (s) { return s.trim(); }).filter(function (s) { return s.length > 0; });
    if (guids.length === 0) {
        TradeStPayTermForm.setFieldValue(formContext, config.codeField, null);
        TradeStPayTermForm.setFieldValue(formContext, config.nameField, null);
        return;
    }

    var promises = guids.map(function (guid) {
        return Xrm.WebApi.retrieveRecord(config.targetEntity, guid, "?$select=" + config.targetCodeField + "," + config.targetNameField)
            .then(function (result) {
                return {
                    code: result[config.targetCodeField],
                    name: result[config.targetNameField]
                };
            })
            .catch(function (error) {
                console.error("查询 " + fieldName + " 对应记录失败: " + error.message);
                return null;
            });
    });

    Promise.all(promises).then(function (results) {
        var codes = [];
        var names = [];
        results.forEach(function (item) {
            if (item) {
                if (item.code) codes.push(item.code);
                if (item.name) names.push(item.name);
            }
        });
        TradeStPayTermForm.setFieldValue(formContext, config.codeField, codes.join(","));
        TradeStPayTermForm.setFieldValue(formContext, config.nameField, names.join(","));
    });
};

/**
 * 注册 Lookup 级联过滤
 * 事业部 -> 大区 -> 国家
 */
TradeStPayTermForm.registerCascadeFilters = function (formContext) {
    // 暂不启用级联 PreSearch 过滤：事业部/子公司/国家目标实体不一致，
    // 过滤条件无法正确匹配，会导致子级选不出值。后续字段关系修复后可恢复。
    // Object.keys(TradeStPayTermForm.LOOKUP_CONFIG).forEach(function (lookupField) {
    //     var config = TradeStPayTermForm.LOOKUP_CONFIG[lookupField];
    //     if (!config.parentField) return;
    //
    //     var control = formContext.getControl(lookupField);
    //     if (!control || !control.addPreSearch) return;
    //
    //     control.addPreSearch(function () {
    //         TradeStPayTermForm.applyCascadeFilter(formContext, lookupField);
    //     });
    // });
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

            var actualParentId;
            if (typeof actualParentRef === "object" && actualParentRef.id) {
                // Lookup 字段返回对象 { id, name, entityType }
                actualParentId = actualParentRef.id.replace(/[{}]/g, "");
            } else {
                // 文本/数字字段直接比较字符串
                actualParentId = actualParentRef.toString().replace(/[{}]/g, "");
            }

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
    // 兜底权限校验
    if (!TradeStPayTermForm.hasCloneButtonPermission()) {
        Xrm.Navigation.openAlertDialog({ text: "您没有权限执行克隆操作。" });
        return;
    }

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
        "mcs_downpay", "mcs_payterm", "mcs_payfreq"
    ];

    // 复制多行文本字段（多选查找组件值）
    var multiSelectLookupFields = ["mcs_countries", "mcs_trade_type"];
    multiSelectLookupFields.forEach(function (fieldName) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) {
            var value = attr.getValue();
            if (value !== null && value !== undefined && value.toString().length > 0) {
                parameters[fieldName] = value;
            }
        }
    });

    textFields.forEach(function (fieldName) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) {
            var value = attr.getValue();
            if (value !== null && value !== undefined) {
                parameters[fieldName] = value;
            }
        }
    });

    // 复制多选选项集字段（getValue 返回整数数组）
    var multiSelectFields = ["mcs_buyergrade", "mcs_creditgrade"];
    multiSelectFields.forEach(function (fieldName) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) {
            var value = attr.getValue();
            if (value && value.length > 0) {
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

    // 复制多选选项集字段（getValue 返回整数数组）
    var multiSelectFields = ["mcs_buyergrade", "mcs_creditgrade"];
    multiSelectFields.forEach(function (fieldName) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) {
            var value = attr.getValue();
            if (value && value.length > 0) {
                parameters[fieldName] = value;
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

    // 处理多选选项集字段（URL 参数为重复整数，parseUrlParams 已聚合为数组）
    var multiSelectFields = ["mcs_buyergrade", "mcs_creditgrade"];
    multiSelectFields.forEach(function (fieldName) {
        if (params.hasOwnProperty(fieldName)) {
            var attr = formContext.getAttribute(fieldName);
            if (attr && (!attr.getValue() || attr.getValue().length === 0)) {
                var rawValue = params[fieldName];
                var values = Array.isArray(rawValue) ? rawValue : [rawValue];
                var optionValues = values
                    .map(function (v) { return parseInt(v, 10); })
                    .filter(function (v) { return !isNaN(v); });
                if (optionValues.length > 0) {
                    attr.setValue(optionValues);
                }
            }
        }
    });

    // 处理多行文本字段（多选查找组件值，GUID 逗号分隔）
    var multiSelectLookupFields = ["mcs_countries", "mcs_trade_type"];
    multiSelectLookupFields.forEach(function (fieldName) {
        if (params.hasOwnProperty(fieldName)) {
            var attr = formContext.getAttribute(fieldName);
            if (attr && !attr.getValue()) {
                var rawValue = params[fieldName];
                if (rawValue && rawValue.toString().length > 0) {
                    attr.setValue(rawValue.toString());
                }
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
            // 多选选项集等字段在 URL 中会出现多个同名参数，聚合为数组
            if (result.hasOwnProperty(key)) {
                if (!Array.isArray(result[key])) {
                    result[key] = [result[key]];
                }
                result[key].push(value);
            } else {
                result[key] = value;
            }
        }
    });

    return result;
};

// ==================== 列表视图批量操作（阶段 3） ====================

/**
 * 批量申请：将选中记录状态更新为 1（待审批）
 * 仅允许对状态为 0（未生效）的记录执行
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 */
TradeStPayTermGrid.apply = function (selectedIds) {
    if (!TradeStPayTermGrid.checkBatchPermission([TradeStPayTermForm.ROLES.CREATOR, TradeStPayTermForm.ROLES.ADMIN], "申请")) {
        return;
    }
    TradeStPayTermGrid.batchUpdateStatus(selectedIds, 1, "申请", 0);
};

/**
 * 批量审批：将选中记录状态更新为 2（生效）
 * 仅允许对状态为 1（待审批）的记录执行
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 */
TradeStPayTermGrid.approve = function (selectedIds) {
    if (!TradeStPayTermGrid.checkBatchPermission([TradeStPayTermForm.ROLES.APPROVER, TradeStPayTermForm.ROLES.ADMIN], "审批")) {
        return;
    }
    TradeStPayTermGrid.batchUpdateStatus(selectedIds, 2, "审批", 1);
};

/**
 * 批量拒绝：将选中记录状态更新为 0（未生效）
 * 仅允许对状态为 1（待审批）的记录执行
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 */
TradeStPayTermGrid.reject = function (selectedIds) {
    if (!TradeStPayTermGrid.checkBatchPermission([TradeStPayTermForm.ROLES.APPROVER, TradeStPayTermForm.ROLES.ADMIN], "拒绝")) {
        return;
    }
    TradeStPayTermGrid.batchUpdateStatus(selectedIds, 0, "拒绝", 1);
};

/**
 * 批量操作角色权限校验（前端拦截，后端 Plugin 仍有兜底校验）
 * @param {string[]} allowedRoles - 允许执行操作的角色名数组
 * @param {string} actionName - 操作名称（用于提示）
 * @returns {boolean} true=有权限
 */
TradeStPayTermGrid.checkBatchPermission = function (allowedRoles, actionName) {
    if (TradeStPayTermForm.currentUserHasAnyRole(allowedRoles)) {
        return true;
    }
    Xrm.Navigation.openAlertDialog({
        text: "您没有【" + actionName + "】权限，该操作仅允许【" + allowedRoles.join("】或【") + "】角色执行。"
    });
    return false;
};

/**
 * 状态值转显示名称
 * @param {number} status - 状态值
 */
TradeStPayTermGrid.getStatusName = function (status) {
    switch (status) {
        case 0: return "未生效";
        case 1: return "待审批";
        case 2: return "生效";
        default: return "未知";
    }
};

/**
 * 批量更新状态通用方法
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 * @param {number} status - 目标状态值
 * @param {string} actionName - 操作名称（用于提示）
 * @param {number} expectedSourceStatus - 允许执行本操作的源状态值
 */
TradeStPayTermGrid.batchUpdateStatus = function (selectedIds, status, actionName, expectedSourceStatus) {
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

    // 先查询所有选中记录的当前状态，校验状态一致性
    var statusPromises = selected.map(function (id) {
        return Xrm.WebApi.retrieveRecord("mcs_trade_stpayterm", id, "?$select=mcs_status");
    });

    Promise.all(statusPromises).then(function (records) {
        var firstStatus = records[0].mcs_status;
        var allSame = records.every(function (r) { return r.mcs_status === firstStatus; });

        if (!allSame) {
            Xrm.Navigation.openAlertDialog({ text: "选中的记录生效状态不一致，只能批量处理状态一致的数据。" });
            return;
        }

        if (firstStatus !== expectedSourceStatus) {
            var statusName = TradeStPayTermGrid.getStatusName(firstStatus);
            Xrm.Navigation.openAlertDialog({ text: "当前选中的记录状态为【" + statusName + "】，不支持执行【" + actionName + "】操作。" });
            return;
        }

        // 校验通过，弹出确认框并执行批量更新
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
    }).catch(function (error) {
        Xrm.Navigation.openAlertDialog({ text: "查询选中记录状态失败：" + error.message });
    });
};
