/**
 * 成交条件样板库 - 表单逻辑
 * 实体: mcs_trade_stpayterm
 * 功能: 克隆新增、表单默认值、Lookup 与文本字段同步
 * 说明: 2026-08-14 Bug #1834 取消审批功能，批量申请/审批/拒绝相关代码已注释（按钮直接隐藏，不删除组件）
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

var TradeStPayTermForm = TradeStPayTermForm || {};
var TradeStPayTermGrid = TradeStPayTermGrid || {};

/**
 * 多语言取词（带中文兜底）
 * 语言包已加载时返回对应语言文本；未加载/未找到时返回原中文，保证中文用户不受影响
 */
TradeStPayTermForm.L = function (key, defaultText) {
    if (typeof LanguageHelper !== "undefined") {
        var v = LanguageHelper.getLabel(key);
        if (v && v !== key) return v;
    }
    return defaultText;
};

// ==================== 角色权限配置（2026-07-20 按业务角色矩阵落地） ====================
// 注意：按 D365 安全角色"名称"匹配，若环境中角色改名需同步修改此处
// CREATOR  = 成交条件制定人（发起配置/申请审批）
// APPROVER = 成交条件审批人（审核配置数据）
// ADMIN    = 系统管理员（放行，便于管理与测试）
// 2026-08-21 禅道 #1989：制定人角色按基线库口径由 Risk Control 改为 Business Control
TradeStPayTermForm.ROLES = {
    CREATOR: "LTC Business Control Configuration Admin",
    APPROVER: "LTC Regional Overseas Risk Director",
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

    // 预加载语言包（异步，不阻塞后续逻辑）
    if (typeof LanguageHelper !== "undefined") {
        LanguageHelper.loadLanguagePack();
    }

    var formType = formContext.ui.getFormType();

    // 注册 Lookup 字段变更事件
    TradeStPayTermForm.registerLookupEvents(formContext);

    // 注册多选查找组件字段变更事件（同步编码/名称）
    TradeStPayTermForm.registerMultiSelectLookupEvents(formContext);

    // 注册级联过滤
    TradeStPayTermForm.registerCascadeFilters(formContext);

    // 设置只读字段
    TradeStPayTermForm.setReadOnlyFields(formContext);

    // 控制克隆新增按钮显隐（仅成交条件制定人或系统管理员可见）
    TradeStPayTermForm.hideCloneButtonIfNoPermission();

    // 新建时如 URL 携带克隆源参数，则回填字段
    if (formType === 1) {
        TradeStPayTermForm.fillCloneData(formContext);
    }
};

/**
 * 判断当前用户是否有克隆新增按钮权限
 * 成交条件制定人（LTC Business Control Configuration Admin）或系统管理员可见
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
 * 生效（2）状态下全表单只读：生效记录不允许直接变更（应克隆新建）。
 * 2026-08-14 Bug #1834：取消审批功能，待审批状态（1）已停用，不再只读锁定。
 */
TradeStPayTermForm.setReadOnlyFields = function (formContext) {
    var statusAttr = formContext.getAttribute("mcs_status");
    var status = statusAttr ? statusAttr.getValue() : null;

    // 生效（2）：禁用全部字段
    if (status === 2) {
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

    // 父级变更时，校验所有子级 Lookup 是否仍属于新父级，不属于则清空
    // （如：事业部变更后，原大区/子公司不属于新事业部则清空，由用户按过滤范围重选）
    TradeStPayTermForm.validateChildLookups(formContext, lookupField);

    // 有父级配置时，顺带查询父级 Lookup 值（WebAPI 中 Lookup 字段的 select 名为 _字段名_value）
    // 用于实现「根据所选大区/子公司自动带出事业部」
    var parentLookupSelect = null;
    if (config.parentField && config.parentFilterField) {
        parentLookupSelect = "_" + config.parentFilterField + "_value";
    }
    var selectFields = config.targetCodeField + "," + config.targetNameField + (parentLookupSelect ? "," + parentLookupSelect : "");

    // 通过 WebAPI 查询编码字段
    Xrm.WebApi.retrieveRecord(config.targetEntity, recordId, "?$select=" + selectFields)
        .then(function (result) {
            var code = result[config.targetCodeField];
            var name = result[config.targetNameField];
            TradeStPayTermForm.setFieldValue(formContext, config.codeField, code);
            if (name) {
                TradeStPayTermForm.setFieldValue(formContext, config.nameField, name);
            }

            // 自动带出父级 Lookup（如：根据大区/子公司带出事业部）
            if (parentLookupSelect) {
                TradeStPayTermForm.autoFillParentLookup(formContext, config.parentField, result[parentLookupSelect]);
            }
        })
        .catch(function (error) {
            console.error("查询 " + lookupField + " 编码失败: " + error.message);
        });
};

/**
 * 自动带出父级 Lookup（如：根据所选大区/子公司带出事业部）
 * 注意：setValue 赋值不会触发该字段的 onChange 事件，需同步父级编码/名称文本字段
 * @param {object} formContext - 表单上下文
 * @param {string} parentLookupField - 父级 Lookup 字段逻辑名（如 mcs_businessunit）
 * @param {string} parentId - 父级目标记录 ID（可带花括号）
 */
TradeStPayTermForm.autoFillParentLookup = function (formContext, parentLookupField, parentId) {
    if (!parentId) {
        console.warn("所选记录未维护父级（" + parentLookupField + "），跳过自动带出。");
        return;
    }

    var parentConfig = TradeStPayTermForm.LOOKUP_CONFIG[parentLookupField];
    if (!parentConfig) return;

    var cleanId = parentId.replace(/[{}]/g, "");

    Xrm.WebApi.retrieveRecord(parentConfig.targetEntity, cleanId, "?$select=" + parentConfig.targetCodeField + "," + parentConfig.targetNameField)
        .then(function (parent) {
            var parentAttr = formContext.getAttribute(parentLookupField);
            if (parentAttr) {
                parentAttr.setValue([{
                    id: cleanId,
                    name: parent[parentConfig.targetNameField],
                    entityType: parentConfig.targetEntity
                }]);
            }
            TradeStPayTermForm.setFieldValue(formContext, parentConfig.codeField, parent[parentConfig.targetCodeField]);
            TradeStPayTermForm.setFieldValue(formContext, parentConfig.nameField, parent[parentConfig.targetNameField]);
        })
        .catch(function (error) {
            console.error("自动带出父级 " + parentLookupField + " 失败: " + error.message);
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
 * 注意：WebAPI 中 Lookup 字段的返回键为 _字段名_value（GUID 字符串）
 */
TradeStPayTermForm.validateLookupParent = function (formContext, lookupField, parentId, selectedId) {
    var config = TradeStPayTermForm.LOOKUP_CONFIG[lookupField];
    if (!config || !config.parentFilterField || !config.targetEntity) return;

    var parentValueKey = "_" + config.parentFilterField + "_value";
    Xrm.WebApi.retrieveRecord(config.targetEntity, selectedId, "?$select=" + parentValueKey)
        .then(function (result) {
            var actualParentRef = result[parentValueKey];
            if (!actualParentRef) {
                TradeStPayTermForm.clearLookupAndChildren(formContext, lookupField);
                return;
            }

            var actualParentId = actualParentRef.toString().replace(/[{}]/g, "").toLowerCase();
            if (actualParentId !== parentId.toLowerCase()) {
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
        Xrm.Navigation.openAlertDialog({ text: TradeStPayTermForm.L("TradeStPayTerm_CloneNoPermission", "您没有权限执行克隆操作。") });
        return;
    }

    var formContext = primaryControl;
    var entityName = formContext.data.entity.getEntityName();
    var entityId = formContext.data.entity.getId();

    if (!entityId) {
        Xrm.Navigation.openAlertDialog({ text: TradeStPayTermForm.L("TradeStPayTerm_SaveBeforeClone", "请先保存当前记录后再克隆。") });
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
// 2026-08-14 Bug #1834：取消审批功能，以下批量申请/审批/拒绝入口已注释。
// 保留空壳函数防止 Ribbon/AppAction 残留引用时报错；按钮通过 Ribbon XML 注释直接隐藏。

/**
 * 批量申请：将选中记录状态更新为 1（待审批）
 * 仅允许对状态为 0（未生效）的记录执行
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 * @param {object} selectedControl - 列表控件（SelectedControl 参数，用于成功后刷新列表）
 */
TradeStPayTermGrid.apply = function (selectedIds, selectedControl) {
    // 2026-08-14 Bug #1834：取消审批功能，批量申请已停用
    // if (!TradeStPayTermGrid.checkBatchPermission([TradeStPayTermForm.ROLES.CREATOR, TradeStPayTermForm.ROLES.ADMIN], TradeStPayTermForm.L("TradeStPayTerm_ActionApply", "申请"))) {
    //     return;
    // }
    // TradeStPayTermGrid.batchUpdateStatus(selectedIds, 1, TradeStPayTermForm.L("TradeStPayTerm_ActionApply", "申请"), 0, selectedControl);
};

/**
 * 批量审批：将选中记录状态更新为 2（生效）
 * 仅允许对状态为 1（待审批）的记录执行
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 * @param {object} selectedControl - 列表控件（SelectedControl 参数，用于成功后刷新列表）
 */
TradeStPayTermGrid.approve = function (selectedIds, selectedControl) {
    // 2026-08-14 Bug #1834：取消审批功能，批量审批已停用
    // if (!TradeStPayTermGrid.checkBatchPermission([TradeStPayTermForm.ROLES.APPROVER, TradeStPayTermForm.ROLES.ADMIN], TradeStPayTermForm.L("TradeStPayTerm_ActionApprove", "审批"))) {
    //     return;
    // }
    // TradeStPayTermGrid.batchUpdateStatus(selectedIds, 2, TradeStPayTermForm.L("TradeStPayTerm_ActionApprove", "审批"), 1, selectedControl);
};

/**
 * 批量拒绝：将选中记录状态更新为 0（未生效）
 * 仅允许对状态为 1（待审批）的记录执行
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 * @param {object} selectedControl - 列表控件（SelectedControl 参数，用于成功后刷新列表）
 */
TradeStPayTermGrid.reject = function (selectedIds, selectedControl) {
    // 2026-08-14 Bug #1834：取消审批功能，批量拒绝已停用
    // if (!TradeStPayTermGrid.checkBatchPermission([TradeStPayTermForm.ROLES.APPROVER, TradeStPayTermForm.ROLES.ADMIN], TradeStPayTermForm.L("TradeStPayTerm_ActionReject", "拒绝"))) {
    //     return;
    // }
    // TradeStPayTermGrid.batchUpdateStatus(selectedIds, 0, TradeStPayTermForm.L("TradeStPayTerm_ActionReject", "拒绝"), 1, selectedControl);
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
        text: TradeStPayTermForm.L("TradeStPayTerm_NoPermission", "您没有【{0}】权限，该操作仅允许【{1}】角色执行。").replace("{0}", actionName).replace("{1}", allowedRoles.join(TradeStPayTermForm.L("TradeStPayTerm_RoleJoinSep", "】或【")))
    });
    return false;
};

/**
 * 状态值转显示名称
 * @param {number} status - 状态值
 */
TradeStPayTermGrid.getStatusName = function (status) {
    switch (status) {
        case 0: return TradeStPayTermForm.L("TradeStPayTerm_Status_0", "未生效");
        // 2026-08-14 Bug #1834：取消审批功能，待审批状态（1）已停用
        // case 1: return TradeStPayTermForm.L("TradeStPayTerm_Status_1", "待审批");
        case 2: return TradeStPayTermForm.L("TradeStPayTerm_Status_2", "生效");
        default: return TradeStPayTermForm.L("TradeStPayTerm_StatusUnknown", "未知");
    }
};

/**
 * 批量更新状态通用方法
 * @param {string[]|string} selectedIds - 选中记录 ID（数组或逗号分隔字符串）
 * @param {number} status - 目标状态值
 * @param {string} actionName - 操作名称（用于提示）
 * @param {number} expectedSourceStatus - 允许执行本操作的源状态值
 * @param {object} selectedControl - 列表控件（SelectedControl 参数，用于成功后刷新列表）
 */
TradeStPayTermGrid.batchUpdateStatus = function (selectedIds, status, actionName, expectedSourceStatus, selectedControl) {
    var selected = [];

    if (Array.isArray(selectedIds)) {
        selected = selectedIds;
    } else if (typeof selectedIds === "string" && selectedIds.length > 0) {
        // Command Designer 的 SelectedControlSelectedItemIds 可能传逗号分隔字符串
        selected = selectedIds.split(",").map(function (id) { return id.trim().replace(/[{}]/g, ""); });
    } else {
        Xrm.Navigation.openAlertDialog({ text: TradeStPayTermForm.L("TradeStPayTerm_NoSelection", "无法获取选中的记录，请检查按钮参数配置。") });
        return;
    }

    if (!selected || selected.length === 0) {
        Xrm.Navigation.openAlertDialog({ text: TradeStPayTermForm.L("TradeStPayTerm_SelectAtLeastOne", "请至少选择一条记录。") });
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
            Xrm.Navigation.openAlertDialog({ text: TradeStPayTermForm.L("TradeStPayTerm_StatusInconsistent", "选中的记录生效状态不一致，只能批量处理状态一致的数据。") });
            return;
        }

        if (firstStatus !== expectedSourceStatus) {
            var statusName = TradeStPayTermGrid.getStatusName(firstStatus);
            Xrm.Navigation.openAlertDialog({ text: TradeStPayTermForm.L("TradeStPayTerm_StatusNotSupported", "当前选中的记录状态为【{0}】，不支持执行【{1}】操作。").replace("{0}", statusName).replace("{1}", actionName) });
            return;
        }

        // 校验通过，弹出确认框并执行批量更新
        Xrm.Navigation.openConfirmDialog({
            title: TradeStPayTermForm.L("TradeStPayTerm_ConfirmTitle", "确认{0}").replace("{0}", actionName),
            text: TradeStPayTermForm.L("TradeStPayTerm_ConfirmText", "确定要{0}选中的 {1} 条记录吗？").replace("{0}", actionName).replace("{1}", selected.length)
        }).then(function (success) {
            if (!success.confirmed) return;

            var updateData = {
                mcs_status: status
            };

            var promises = selected.map(function (id) {
                return Xrm.WebApi.updateRecord("mcs_trade_stpayterm", id, updateData);
            });

            Promise.all(promises).then(function () {
                Xrm.Navigation.openAlertDialog({ text: TradeStPayTermForm.L("TradeStPayTerm_SuccessSuffix", "{0}成功。").replace("{0}", actionName) }).then(function () {
                    // 禅道 #1160：现代命令栏 JS 运行在隔离沙箱，window.location.reload() 无效；
                    // 优先用 SelectedControl 参数刷新列表，无参数时兜底整页刷新
                    TradeStPayTermGrid.refreshGrid(selectedControl);
                });
            }).catch(function (error) {
                Xrm.Navigation.openAlertDialog({ text: TradeStPayTermForm.L("TradeStPayTerm_FailedSuffix", "{0}失败：").replace("{0}", actionName) + error.message });
            });
        });
    }).catch(function (error) {
        Xrm.Navigation.openAlertDialog({ text: TradeStPayTermForm.L("TradeStPayTerm_QueryStatusFailed", "查询选中记录状态失败：") + error.message });
    });
};

/**
 * 刷新列表（禅道 #1160）
 * 现代命令栏 JS 沙箱中 window.location.reload() 与 Xrm.Navigation.navigateTo 均不可靠
 *（实测：reload 偶发无效；navigateTo 稳定抛 "Unexpected xrm page input"），
 * 必须通过按钮 SelectedControl 参数（type=12）拿到的列表控件 refresh()。
 * @param {object} selectedControl - SelectedControl 参数传入的列表控件
 */
TradeStPayTermGrid.refreshGrid = function (selectedControl) {
    // 2026-07-27 实测：经典按钮上下文下 type=12（SelectedControl）参数同样能拿到带 refresh() 的列表控件，
    // 局部刷新生效（控制台 page_list_load_time 遥测证实列表重新拉取），无需整页 reload
    if (selectedControl && typeof selectedControl.refresh === "function") {
        try {
            selectedControl.refresh();
            return;
        } catch (e) { /* 刷新失败时走兜底 */ }
    }
    // 无 SelectedControl 参数时：弹窗关闭动画期间调用 reload 会被吞掉，延迟到弹窗关闭后整页刷新
    setTimeout(function () {
        try { window.location.reload(); return; } catch (e) { /* 继续兑底 */ }
        try { window.top.location.reload(); } catch (e) { /* 沙箱中可能无效，忽略 */ }
    }, 600);
};
