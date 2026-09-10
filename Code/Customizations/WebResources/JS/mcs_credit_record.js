/**
 * 客户信用评估记录表 - 表单逻辑
 * 实体: mcs_credit_record
 * 功能: 客户信息带出、校验、状态流转、按钮控制、默认字段
 * 影响范围: 仅限mcs_credit_record实体
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

var CreditRecordForm = CreditRecordForm || {};

/**
 * 多语言取词（带中文兜底）
 * 语言包已加载时返回对应语言文本；未加载/未找到时返回原中文，保证中文用户不受影响
 */
CreditRecordForm.L = function (key, defaultText) {
    if (typeof LanguageHelper !== "undefined") {
        var v = LanguageHelper.getLabel(key);
        if (v && v !== key) return v;
    }
    return defaultText;
};

// ==================== 状态常量 ====================
CreditRecordForm.STATUS = {
    INIT: 9,           // 发起信用评估
    LINK_ACCOUNT: 10,  // 关联客户代码
    DATA_INTEGRATION: 11,  // 内外部数据集成
    MANUAL_REVIEW: 12,     // 人工复核
    SCORE_CALC: 13,        // 信用分计算
    AUDIT_APPLY: 14,       // 审核申请
    APPROVED: 15,          // 审批通过
    REJECTED: 16           // 审批未通过
};

// BPF 状态变更标记 — 用于区分是按钮触发还是 BPF 直接修改
CreditRecordForm._bpfNavigating = false;
CreditRecordForm._lastButtonStatus = null;

// 状态名称（多语言，用于提示信息）
CreditRecordForm.getStatusName = function (status) {
    var defaults = {
        9: "发起信用评估", 10: "关联客户代码", 11: "内外部数据集成", 12: "人工复核",
        13: "信用分计算", 14: "审核申请", 15: "审批通过", 16: "审批未通过"
    };
    return CreditRecordForm.L("CreditRecord_Status_" + status, defaults[status] || ("未知状态(" + status + ")"));
};

// ==================== 表单事件 ====================

/**
 * 表单加载事件
 */
CreditRecordForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();

    // 预加载语言包；状态相关通知在语言包就绪后的回调中渲染，避免英文用户在 onLoad 阶段看到中文兜底
    if (typeof LanguageHelper !== "undefined") {
        LanguageHelper.loadLanguagePack(function () {
            CreditRecordForm.toggleByStatus(formContext);
        });
    }

    var formType = formContext.ui.getFormType();
    
    // 新建时设置默认值
    if (formType === 1) {
        CreditRecordForm.setDefaults(formContext);
    }
    
    // 设置只读字段（含评估状态）
    CreditRecordForm.setFieldsReadOnly(formContext);
    
    // 注册字段变更事件
    CreditRecordForm.registerEvents(formContext);
    
    // 根据状态控制按钮/字段/通知（无语言包时立即渲染；有语言包时由上方回调渲染）
    if (typeof LanguageHelper === "undefined") {
        CreditRecordForm.toggleByStatus(formContext);
    }
    
    // 初始化状态记录
    var statusField = formContext.getAttribute("mcs_status");
    if (statusField) {
        CreditRecordForm._lastButtonStatus = statusField.getValue();
    }
    
    // 初始化附件页签（通用上传组件）
    CreditRecordForm.initAttachmentTab(formContext);
    
    // 阻止 BPF 流程条回退（官方 Client API）
    CreditRecordForm.preventBpfGoBack(formContext);
    
    // 将 BPF 侧窗格字段设为只读
    CreditRecordForm.lockBpfFields(formContext);
};

/**
 * 阻止 BPF 流程条回退
 * 使用官方 Client API addOnPreStageChange，在阶段变化前拦截
 */
CreditRecordForm.preventBpfGoBack = function (formContext) {
    if (!formContext || !formContext.data || !formContext.data.process) {
        return;
    }
    
    try {
        formContext.data.process.addOnPreStageChange(function (stageChangeContext) {
            // 程序化重选活动阶段（修复禅道#874 弹出框不重绘）期间放行，避免拦截/死循环
            if (CreditRecordForm._bpfNavigating) {
                return;
            }

            var args = stageChangeContext.getEventArgs();
            if (!args) return;
            
            var direction = args.getDirection();
            
            if (direction === "Previous") {
                // 阻止回退
                args.preventDefault();
                Xrm.Navigation.openAlertDialog({
                    text: CreditRecordForm.L("CreditRecord_BpfNoGoBack", "不允许通过流程条回退阶段，请使用上方工具栏的按钮操作。"),
                    title: CreditRecordForm.L("CreditRecord_TipTitle", "提示")
                });
                return;
            }
            
            if (direction === "Next") {
                // 阻止 BPF 默认前进，改为走自定义按钮的下一步逻辑
                // 这样会和上方工具栏《进入下一阶段》按钮效果完全一致
                args.preventDefault();
                CreditRecordForm.nextStep(formContext);
            }
        });
        
        // 阶段变化后重新锁定 BPF 字段（BPF 侧窗格会重新渲染）
        formContext.data.process.addOnStageChange(function () {
            CreditRecordForm.lockBpfFields(formContext);
        });
    } catch (ex) {
        console.error("[CreditRecordForm] 注册 BPF PreStageChange 事件失败:", ex);
    }
};

/**
 * 强制 BPF 控件重新选中当前活动阶段，重绘弹出框（flyout）
 * 修复禅道#874：自定义按钮/BPF 下一阶段拦截后走 updateStatus 推进，
 * data.refresh 只重绘阶段条，已打开的弹出框仍显示上一阶段字段。
 * setActiveStage 会再次触发 PreStageChange，用 _bpfNavigating 标志防重入。
 */
CreditRecordForm.reselectActiveBpfStage = function (formContext, done) {
    var finish = function () {
        CreditRecordForm._bpfNavigating = false;
        if (done) done();
    };
    try {
        var process = formContext.data && formContext.data.process;
        if (!process) { finish(); return; }
        var activeStage = process.getActiveStage();
        if (!activeStage) { finish(); return; }
        CreditRecordForm._bpfNavigating = true;
        process.setActiveStage(activeStage.getId(), function () {
            CreditRecordForm.lockBpfFields(formContext);
            // setActiveStage 对「已是活动阶段」为 no-op，弹出框不会重绘（禅道#874 DEV1 实测）。
            // 兜底：延时点击阶段条上新活动阶段的按钮，由平台原生重绘弹出框
            setTimeout(function () {
                CreditRecordForm.clickBpfStageButton(activeStage.getId());
            }, 500);
            finish();
        });
    } catch (ex) {
        console.error("[CreditRecordForm] 重选 BPF 活动阶段失败:", ex);
        finish();
    }
};

/**
 * 点击 BPF 阶段条上指定阶段的按钮，让已打开的弹出框重绘到该阶段（禅道#874）
 * 阶段条按钮 ID = 固定前缀 + 阶段 GUID（DEV1 实测验证）
 * 仅在弹出框当前处于打开状态时点击，避免工具栏按钮推进后无故弹出侧窗格
 */
CreditRecordForm.clickBpfStageButton = function (stageId) {
    try {
        // BPF 阶段条/弹出框在顶层文档，表单脚本运行在内容 iframe 中，两处都要找
        var docs = [document];
        try {
            if (window.top && window.top.document && window.top.document !== document) {
                docs.push(window.top.document);
            }
        } catch (e) { /* 跨域时只用当前 document */ }

        var id = (stageId || "").replace(/[{}]/g, "").toLowerCase();
        for (var i = 0; i < docs.length; i++) {
            // 仅在弹出框当前处于打开状态时才点击，避免工具栏按钮推进后无故弹出侧窗格
            var dockBtn = docs[i].getElementById("MscrmControls.Containers.ProcessStageControl-stageDockModeButton");
            var flyoutOpen = dockBtn && dockBtn.offsetParent !== null;
            if (!flyoutOpen) continue;
            var btn = docs[i].getElementById("MscrmControls.Containers.ProcessBreadCrumb-processHeaderStageButton_" + id);
            if (btn) {
                btn.click();
                return;
            }
        }
        console.warn("[CreditRecordForm] 未找到打开的 BPF 弹出框或阶段按钮:", id);
    } catch (ex) {
        console.warn("[CreditRecordForm] 点击 BPF 阶段按钮失败:", ex);
    }
};

/**
 * 将 BPF 侧窗格中的字段设为只读
 * BPF 设计器本身没有 Read-only 选项，通过官方 Client API 在运行时锁定
 */
CreditRecordForm.lockBpfFields = function (formContext) {
    if (!formContext || !formContext.ui || !formContext.ui.controls) {
        return;
    }
    
    try {
        // BPF 字段控件通常以 header_process_ 开头
        formContext.ui.controls.forEach(function (control) {
            if (!control || !control.getName) return;
            
            var name = control.getName();
            if (name.indexOf("header_process_") === 0 && control.setDisabled) {
                control.setDisabled(true);
            }
        });
    } catch (ex) {
        console.error("[CreditRecordForm] 设置 BPF 字段只读失败:", ex);
    }
};

/**
 * 设置新建时默认值
 */
CreditRecordForm.setDefaults = function (formContext) {
    // 申请人 = 当前用户
    var applicantField = formContext.getAttribute("mcs_applicant");
    if (applicantField && !applicantField.getValue()) {
        var userName = Xrm.Utility.getGlobalContext().userSettings.userName;
        applicantField.setValue(userName);
    }
    
    // 发起评估日期 = 今天
    var initDateField = formContext.getAttribute("mcs_initdate");
    if (initDateField && !initDateField.getValue()) {
        initDateField.setValue(new Date());
    }
    
    // 评估状态 = 9(发起信用评估) - 选项集实际值
    var statusField = formContext.getAttribute("mcs_status");
    if (statusField && !statusField.getValue()) {
        statusField.setValue(CreditRecordForm.STATUS.INIT);
    }
    
    // 有效状态 = 否(0)
    var activeField = formContext.getAttribute("mcs_active");
    if (activeField && !activeField.getValue()) {
        activeField.setValue(false);
    }
};

/**
 * 设置字段只读
 */
CreditRecordForm.setFieldsReadOnly = function (formContext) {
    // 评估状态始终只读（由按钮控制流转，不允许手动修改）
    var statusControl = formContext.getControl("mcs_status");
    if (statusControl) statusControl.setDisabled(true);
    
    // 编码字段始终只读
    var codeField = formContext.getControl("mcs_scoreid");
    if (codeField) codeField.setDisabled(true);
    
    // 带出字段只读
    var readOnlyFields = [
        "mcs_custname", "mcs_custnameen", "mcs_countrycode", "mcs_cofaceid",
        "mcs_creditscore", "mcs_applicant"
    ];
    readOnlyFields.forEach(function (fieldName) {
        var control = formContext.getControl(fieldName);
        if (control) control.setDisabled(true);
    });
    
    // 接口回填字段只读
    var apiFields = [
        "mcs_urba360id", "mcs_urbastatus", "mcs_rptorderid", "mcs_rptstatus",
        "mcs_publicationid", "mcs_api_status", "mcs_api_name", "mcs_api_msg",
        "mcs_urbajson", "mcs_reportjson", "mcs_bppstatus", "mcs_bppappriver",
        "mcs_bppid", "mcs_bpperrormsg", "mcs_bpprejectreason", "mcs_approvedate",
        "mcs_abidate", "mcs_checkdate", "mcs_scoredate",
        "mcs_workflowid", "mcs_nextapprover"  // BPP字段只读
    ];
    apiFields.forEach(function (fieldName) {
        var control = formContext.getControl(fieldName);
        if (control) control.setDisabled(true);
    });
};

/**
 * 注册字段变更事件
 */
CreditRecordForm.registerEvents = function (formContext) {
    // 客户变更 - 自动带出客户信息
    var accountField = formContext.getAttribute("mcs_accountid");
    if (accountField) {
        accountField.addOnChange(CreditRecordForm.onAccountChange);
        // 新建表单：客户变更后检测同客户未生效评估记录（禅道 #1645）
        if (formContext.ui.getFormType() === 1) {
            accountField.addOnChange(CreditRecordForm.checkInFlightRecordOnCreate);
        }
    }
    
    // 状态变更 - 控制字段锁定和按钮
    var statusField = formContext.getAttribute("mcs_status");
    if (statusField) {
        statusField.addOnChange(CreditRecordForm.onStatusChange);
    }
};

/**
 * 状态变更事件
 * 拦截 BPF 直接修改状态（用户点击 BPF 阶段或面板里的下拉框）
 */
CreditRecordForm.onStatusChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var statusField = formContext.getAttribute("mcs_status");
    var newStatus = statusField.getValue();
    var oldStatus = CreditRecordForm._lastButtonStatus;
    
    // 如果状态被改回更小值（往回跳），且不是通过我们的按钮触发的 → 阻断
    if (newStatus !== null && oldStatus !== null && newStatus < oldStatus && !CreditRecordForm._bpfNavigating) {
        // 恢复原来的状态值
        statusField.setValue(oldStatus);
        
        // 提示用户必须通过按钮操作
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_UseToolbarButtons", "请使用上方工具栏的按钮进行状态流转，不要直接修改进度条中的状态。"));
        return;
    }
    
    // 记录当前状态（用于下次比较）
    CreditRecordForm._lastButtonStatus = newStatus;
    
    // 正常处理状态变更
    CreditRecordForm.toggleByStatus(formContext);
};

// ==================== 客户变更事件 ====================

/**
 * 客户变更事件
 * 自动带出：客户编码、英文名称、国家编码、科法斯ID
 */
CreditRecordForm.onAccountChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var accountField = formContext.getAttribute("mcs_accountid");
    
    if (!accountField) return;
    
    var accountValue = accountField.getValue();
    
    if (!accountValue || accountValue.length === 0) {
        // 清空带出字段
        CreditRecordForm.clearAccountFields(formContext);
        return;
    }
    
    var accountGuid = accountValue[0].id.replace(/[{}]/g, "");
    
    // 业务规则：客户（account）只是导航入口，客户数据统一从关联的 mcs_customermasterdata 读取。
    // 先查 account 找到关联的客户主数据，再从主数据读取客户编码/英文名称/国家编码/科法斯ID。
    // 注意：真正的客户编号在 account.mcs_sapnumber，mcs_customermasterdata.mcs_accountnumber 实际存的是 account.accountnumber（关系流水号）。
    var accountSapNumber = "";
    Xrm.WebApi.retrieveRecord("account", accountGuid, "?$select=_mcs_customermasterdata_value,mcs_sapnumber")
        .then(function (accountResult) {
            var customerMasterDataId = accountResult._mcs_customermasterdata_value;
            accountSapNumber = accountResult.mcs_sapnumber || "";
            
            if (!customerMasterDataId) {
                throw new Error(CreditRecordForm.L("CreditRecord_NoMasterData", "该客户未关联客户主数据，请先维护客户主数据"));
            }
            
            var plainCustomerMasterDataId = customerMasterDataId.replace(/[{}]/g, "");
            return Xrm.WebApi.retrieveRecord("mcs_customermasterdata", plainCustomerMasterDataId,
                "?$select=mcs_englishname,mcs_countrycode,mcs_cofaceid");
        })
        .then(function (cm) {
            cm = cm || {};
            
            // 客户编码（从 account.mcs_sapnumber 读取，避免取到关系流水号）
            var custNameField = formContext.getAttribute("mcs_custname");
            if (custNameField) {
                custNameField.setValue(accountSapNumber);
            }
            
            // 客户英文名称
            var custNameEnField = formContext.getAttribute("mcs_custnameen");
            if (custNameEnField) {
                custNameEnField.setValue(cm.mcs_englishname || "");
            }
            
            // 国家编码
            var countryCodeField = formContext.getAttribute("mcs_countrycode");
            if (countryCodeField) {
                countryCodeField.setValue(cm.mcs_countrycode || "");
            }
            
            // 科法斯ID
            var cofaceField = formContext.getAttribute("mcs_cofaceid");
            if (cofaceField) {
                cofaceField.setValue(cm.mcs_cofaceid || "");
            }
            
            // 校验提示
            CreditRecordForm.validateAccountFields(formContext);
        })
        .catch(function (error) {
            console.error("查询客户信息失败:", error);
            Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_QueryAccountFailed", "查询客户信息失败：") + (error.message || JSON.stringify(error)));
        });
};

/**
 * 清空客户相关字段
 */
CreditRecordForm.clearAccountFields = function (formContext) {
    var fields = ["mcs_custname", "mcs_custnameen", "mcs_countrycode", "mcs_cofaceid"];
    fields.forEach(function (fieldName) {
        var field = formContext.getAttribute(fieldName);
        if (field) field.setValue(null);
    });
};

/**
 * 校验客户字段
 */
CreditRecordForm.validateAccountFields = function (formContext) {
    var custNameEn = formContext.getAttribute("mcs_custnameen").getValue();
    var countryCode = formContext.getAttribute("mcs_countrycode").getValue();
    var cofaceId = formContext.getAttribute("mcs_cofaceid").getValue();
    
    var messages = [];
    
    if (!custNameEn) {
        messages.push(CreditRecordForm.L("CreditRecord_EnglishNameRequired", "客户英文名称不能为空，请先维护客户主数据"));
    }
    
    if (!countryCode) {
        messages.push(CreditRecordForm.L("CreditRecord_CountryCodeRequired", "国家编码不能为空"));
    }
    
    if (!cofaceId) {
        messages.push(CreditRecordForm.L("CreditRecord_NoCofaceLink", "未关联科法斯客户，请先执行【关联客户代码】操作"));
    }
    
    if (messages.length > 0) {
        // 显示通知（不阻断）
        var notification = {
            messages: messages,
            level: "WARNING",
            uniqueId: "account_validation"
        };
        formContext.ui.setFormNotification(messages.join(CreditRecordForm.L("CreditRecord_MsgSeparator", "；")), "WARNING", "account_validation");
    } else {
        formContext.ui.clearFormNotification("account_validation");
    }
};

// ==================== 状态控制与按钮逻辑 ====================

/**
 * 根据状态控制字段可编辑性和按钮显隐
 */
CreditRecordForm.toggleByStatus = function (formContext) {
    var statusField = formContext.getAttribute("mcs_status");
    if (!statusField) return;
    
    var status = statusField.getValue();
    
    // 状态值可能为null（表单加载时数据尚未就绪），此时不做处理
    if (status === null) return;
    
    // 默认：逾期未回收率模型分始终只读，仅在人工复核阶段开放编辑
    CreditRecordForm.setControlEditable(formContext, "mcs_overduerate", false);
    
    // 默认：信用标签子网格锁定，仅在人工复核阶段开放编辑
    CreditRecordForm.setGridEditable(formContext, "Subgrid_new_1", false);
    
    // 不同状态控制不同字段的可编辑性（选项集实际值）
    switch (status) {
        case CreditRecordForm.STATUS.INIT: // 9 - 发起信用评估
            // 允许编辑客户
            CreditRecordForm.setControlEditable(formContext, "mcs_accountid", true);
            break;
            
        case CreditRecordForm.STATUS.LINK_ACCOUNT: // 10 - 关联客户代码
            // 客户锁定
            CreditRecordForm.setControlEditable(formContext, "mcs_accountid", false);
            break;
            
        // 注意：【搜索 Coface 企业】按钮通过 Modern Command Bar (App Action) 部署，
        // 当前 AppActionDeployer 创建的按钮不设置 EnableRule，由 JS 函数内部校验控制
            
        case CreditRecordForm.STATUS.DATA_INTEGRATION: // 11 - 内外部数据集成
            // 数据集成中，关键字段锁定
            CreditRecordForm.setControlEditable(formContext, "mcs_accountid", false);
            break;
            
        case CreditRecordForm.STATUS.MANUAL_REVIEW: // 12 - 人工复核
            // 复核阶段，允许编辑标签子网格和逾期未回收率模型分（客户锁定）
            CreditRecordForm.setControlEditable(formContext, "mcs_accountid", false);
            CreditRecordForm.setControlEditable(formContext, "mcs_overduerate", true);
            CreditRecordForm.setGridEditable(formContext, "Subgrid_new_1", true);
            break;
            
        case CreditRecordForm.STATUS.SCORE_CALC: // 13 - 信用分计算
            // 计算阶段，所有字段锁定
            CreditRecordForm.lockAllFields(formContext);
            // 重新锁定状态字段（lockAllFields会解锁所有，需要重新锁定）
            CreditRecordForm.setControlEditable(formContext, "mcs_status", false);
            break;
            
        case CreditRecordForm.STATUS.AUDIT_APPLY: // 14 - 审核申请
            // 等待BPP审批，所有字段锁定
            CreditRecordForm.lockAllFields(formContext);
            CreditRecordForm.setControlEditable(formContext, "mcs_status", false);
            // 显示BPP审批信息
            CreditRecordForm.showBppInfo(formContext);
            break;
            
        case CreditRecordForm.STATUS.APPROVED: // 15 - 审批通过
            // 所有字段锁定
            CreditRecordForm.lockAllFields(formContext);
            CreditRecordForm.setControlEditable(formContext, "mcs_status", false);
            break;
            
        case CreditRecordForm.STATUS.REJECTED: // 16 - 审批未通过
            // 客户锁定，但允许点击【重新发起】
            CreditRecordForm.setControlEditable(formContext, "mcs_accountid", false);
            break;
    }
};

/**
 * 设置控件可编辑性
 */
CreditRecordForm.setControlEditable = function (formContext, fieldName, editable) {
    var control = formContext.getControl(fieldName);
    if (control) {
        control.setDisabled(!editable);
    }
};

/**
 * 设置子网格可编辑性
 * 控制可编辑子网格（Editable Grid）的编辑状态
 */
CreditRecordForm.setGridEditable = function (formContext, gridName, editable) {
    var gridControl = formContext.getControl(gridName);
    if (gridControl) {
        try {
            // 标准子网格控制方式
            gridControl.setDisabled(!editable);
            
            // 如果子网格已加载，同时控制内部的编辑按钮
            var grid = gridControl.getGrid();
            if (grid) {
                var rows = grid.getRows();
                if (rows) {
                    rows.forEach(function (row) {
                        var cells = row.getData().getEntity().getAttributes();
                        cells.forEach(function (attr) {
                            attr.setDisabled(!editable);
                        });
                    });
                }
            }
        } catch (e) {
            // 子网格可能尚未完全加载，忽略错误
            console.log("子网格控制失败（可能尚未加载）: " + e.message);
        }
    }
};


/**
 * 锁定所有字段
 */
CreditRecordForm.lockAllFields = function (formContext) {
    var allControls = formContext.ui.controls.get();
    allControls.forEach(function (control) {
        if (control.setDisabled) {
            control.setDisabled(true);
        }
    });
};

// ==================== 自定义按钮命令 ====================

/**
 * 【下一步】按钮命令
 * 根据当前状态执行对应的下一步操作
 */
CreditRecordForm.nextStep = function (primaryControl) {
    var formContext = primaryControl;
    var status = formContext.getAttribute("mcs_status").getValue();
    var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
    
    switch (status) {
        case CreditRecordForm.STATUS.INIT: // 9 → 10
            // 校验客户已选
            var accountId = formContext.getAttribute("mcs_accountid").getValue();
            if (!accountId) {
                Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_SelectAccountFirst", "请先选择客户"));
                return;
            }
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.LINK_ACCOUNT, CreditRecordForm.L("CreditRecord_AccountLinked", "已关联客户"));
            break;
            
        case CreditRecordForm.STATUS.LINK_ACCOUNT: // 10 → 11
            // 未绑定 Coface ID：弹确认框，确定放行、取消阻断（#1850）
            var cofaceId = formContext.getAttribute("mcs_cofaceid").getValue();
            if (!cofaceId) {
                Xrm.Utility.confirmDialog(
                    CreditRecordForm.L("CreditRecord_ConfirmNextWithoutCofaceId", "没有绑定Coface代码，是否进入下一阶段？"),
                    function () {
                        formContext.ui.setFormNotification(
                            CreditRecordForm.L("CreditRecord_CofaceDataMissingNotice", "Coface 数据缺失，已生成待补充标签，请在人工复核阶段录入。"),
                            "WARNING", "coface_missing"
                        );
                        CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, CreditRecordForm.L("CreditRecord_EnterDataIntegration", "进入数据集成"));
                    },
                    function () {
                        // 用户取消，阻断不进入下一阶段
                    }
                );
                return;
            }
            // Coface 订单未就绪时同样弹确认框，确定放行、取消阻断（#1850）
            CreditRecordForm.checkCofaceOrderReadyAndProceed(formContext, recordId);
            break;
            
        case CreditRecordForm.STATUS.DATA_INTEGRATION: // 11 → 12
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.MANUAL_REVIEW, CreditRecordForm.L("CreditRecord_EnterManualReview", "进入人工复核"));
            break;
            
        case CreditRecordForm.STATUS.MANUAL_REVIEW: // 12 → 13
            CreditRecordForm.validateTagsCompleted(formContext, recordId, function () {
                CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.SCORE_CALC, CreditRecordForm.L("CreditRecord_EnterScoreCalc", "进入信用分计算"));
            });
            break;
            
        case CreditRecordForm.STATUS.SCORE_CALC: // 13 → 14
            // 提交审核申请，触发BPP Plugin
            CreditRecordForm.submitBppApproval(formContext, recordId);
            break;
            
        default:
            Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_NextStepNotSupported", "当前状态不支持【下一步】操作"));
            break;
    }
};

/**
 * 【数据集成刷新】按钮命令
 * 仅状态12（人工复核）可用，重新触发数据集成
 */
CreditRecordForm.refreshDataIntegration = function (primaryControl) {
    var formContext = primaryControl;
    var status = formContext.getAttribute("mcs_status").getValue();
    var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
    
    if (status !== CreditRecordForm.STATUS.MANUAL_REVIEW) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_RefreshOnlyInReview", "【数据集成刷新】仅在人工复核阶段可用"));
        return;
    }
    
    Xrm.Utility.confirmDialog(
        CreditRecordForm.L("CreditRecord_ConfirmRefresh", "确定要重新执行数据集成吗？这将刷新所有指标数据。"),
        function () {
            // 方式：将状态改回11（数据集成），触发 CofaceDataSyncPlugin 重新执行
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, CreditRecordForm.L("CreditRecord_RefreshTriggered", "数据集成刷新已触发"));
        },
        function () {
            // 用户取消，不做操作
        }
    );
};

// ==================== BPP审批功能 ====================

/**
 * 调用 CommonExtensionApi 通用方法
 * 通过 mcs_commonextensionapi 转发到 SanyD365.D365ExtensionApi
 */
CreditRecordForm.callCommonExtensionApi = function (path, params, hasAuth) {
    var requestBody = {
        Method: "post",
        Path: path,
        HasAuth: hasAuth === true
    };
    if (params) {
        requestBody.Params = params;
    }

    var request = {
        getMetadata: function () {
            return {
                boundParameter: null,
                parameterTypes: {},
                operationType: 0,
                operationName: "mcs_commonextensionapi"
            };
        },
        RequestBody: JSON.stringify(requestBody)
    };

    return Xrm.WebApi.online.execute(request).then(function (response) {
        return response.json();
    }).then(function (result) {
        if (result.Result) {
            return JSON.parse(result.Result);
        }
        return result;
    });
};

/**
 * 获取当前用户的 mcs_domainaccount（BPP用户账号）
 */
CreditRecordForm.getCurrentUserDomainAccount = function () {
    var userId = Xrm.Utility.getGlobalContext().userSettings.userId.replace(/[{}]/g, "");
    var fetchXml = [
        "<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>",
        "  <entity name='mcs_personnel'>",
        "    <attribute name='mcs_domainaccount' />",
        "    <link-entity name='mcs_useraccount' from='mcs_useraccountid' to='mcs_systemuseraccount' link-type='inner' alias='aa'>",
        "      <link-entity name='systemuser' from='systemuserid' to='mcs_systemuserid' link-type='inner' alias='ab'>",
        "        <filter type='and'>",
        "          <condition attribute='systemuserid' operator='eq' value='" + userId + "' />",
        "        </filter>",
        "      </link-entity>",
        "    </link-entity>",
        "  </entity>",
        "</fetch>"
    ].join("");

    return Xrm.WebApi.online.retrieveMultipleRecords("mcs_personnel", "?fetchXml=" + encodeURIComponent(fetchXml))
        .then(function (result) {
            if (result.entities.length > 0 && result.entities[0].mcs_domainaccount) {
                return result.entities[0].mcs_domainaccount;
            }
            throw new Error(CreditRecordForm.L("CreditRecord_NoDomainAccount", "当前用户未配置mcs_personnel.domainaccount"));
        });
};

/**
 * 判断BPP状态是否为"进行中"
 * 结束态（Approved/Rejected/Withdrawn/Abandoned/SubmitFailed 及 numeric 终止态）返回 false
 */
CreditRecordForm.isBppInProgress = function (bppStatus) {
    if (!bppStatus) return false;
    var status = bppStatus.toString().toLowerCase();
    return status === "submitted" ||
           status === "inreview" ||
           status === "pending" ||
           status === "10" ||
           status === "20";
};

/**
 * 【提交审批】按钮命令
 * 状态13（信用分计算）→ 14（审核申请），前端直接调 mcs_bppstartapi 发起 BPP
 * 与限额申请保持一致：DynaHx.Da.invokeAction("mcs_bppstartapi", ...)
 * 这里用 Xrm.WebApi.online.execute 等价的 Custom API 调用
 */
CreditRecordForm.submitBppApproval = function (formContext, recordId) {
    // 校验信用分已计算
    var creditScore = formContext.getAttribute("mcs_creditscore").getValue();
    if (creditScore === null || creditScore === undefined) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_ScoreNotCalculated", "信用分尚未计算，请先完成信用分计算"));
        return;
    }

    // 防重复提交：只有当前BPP流程仍在进行中时才阻止；
    // 已结束（Approved/Rejected/Withdrawn/Abandoned/SubmitFailed）允许重新提交
    var workflowId = formContext.getAttribute("mcs_workflowid").getValue();
    var bppStatus = formContext.getAttribute("mcs_bppstatus").getValue();
    if (workflowId && CreditRecordForm.isBppInProgress(bppStatus)) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_BppAlreadyInProgress", "当前记录已存在BPP审批流程，请勿重复提交"));
        return;
    }

    // 确认提交
    Xrm.Utility.confirmDialog(
        CreditRecordForm.L("CreditRecord_ConfirmSubmitBpp", "确定要提交BPP审批吗？提交后将锁定所有字段并进入审批流程。"),
        function () {
            CreditRecordForm.showLoading(formContext, CreditRecordForm.L("CreditRecord_SubmittingBpp", "正在提交BPP审批..."));

            // 1. 先保存状态到 14
            var entity = { mcs_status: CreditRecordForm.STATUS.AUDIT_APPLY };
            Xrm.WebApi.online.updateRecord("mcs_credit_record", recordId, entity)
                .then(function () {
                    // 2. 调 Custom API mcs_bppstartapi（与限额申请一致）
                    var userId = Xrm.Utility.getGlobalContext().userSettings.userId.replace(/[{}]/g, "");
                    var request = {
                        getMetadata: function () {
                            return {
                                boundParameter: null,
                                parameterTypes: {
                                    "EntityId": { typeName: "Edm.String", structuralProperty: 1 },
                                    "EntityName": { typeName: "Edm.String", structuralProperty: 1 },
                                    "UserId": { typeName: "Edm.String", structuralProperty: 1 }
                                },
                                operationType: 0,
                                operationName: "mcs_bppstartapi"
                            };
                        },
                        EntityId: recordId,
                        EntityName: "mcs_credit_record",
                        UserId: userId
                    };

                    return Xrm.WebApi.online.execute(request);
                })
                .then(function (response) {
                    return response.json();
                })
                .then(function (data) {
                    CreditRecordForm.hideLoading(formContext);
                    if (data && data.Result) {
                        var result = JSON.parse(data.Result);
                        if (result.Result === true || result.Result === "true") {
                            formContext.ui.setFormNotification(CreditRecordForm.L("CreditRecord_BppSubmitSuccess", "BPP审批提交成功"), "INFO", "bpp_start");
                            formContext.data.refresh(true).then(function () {
                                // 强制 BPF 弹出框重绘到新活动阶段（修复禅道#874）
                                CreditRecordForm.reselectActiveBpfStage(formContext);
                            });
                            setTimeout(function () {
                                formContext.ui.clearFormNotification("bpp_start");
                            }, 3000);
                        } else {
                            Xrm.Utility.alertDialog(result.Description || CreditRecordForm.L("CreditRecord_BppSubmitFailed", "BPP审批提交失败"));
                        }
                    } else {
                        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_BppSubmitAbnormal", "BPP审批提交返回异常"));
                    }
                })
                .catch(function (error) {
                    CreditRecordForm.hideLoading(formContext);
                    console.error("提交BPP审批失败:", error);
                    Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_SubmitBppFailedPrefix", "提交BPP审批失败：") + (error.message || JSON.stringify(error)));
                });
        }
    );
};

/**
 * 【查看审批】按钮命令
 * 有workflowid时可用，打开BPP审批页面
 */
CreditRecordForm.viewBppApproval = function (primaryControl) {
    var formContext = primaryControl;
    var workflowId = formContext.getAttribute("mcs_workflowid").getValue();
    
    if (!workflowId) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_NoBppInfo", "暂无BPP审批信息"));
        return;
    }
    
    // BPP审批页面地址（测试环境orgId=3，生产环境orgId=3）
    var bppUrl = "https://sanybpp-portal-uat.sany.com.cn/approval-form?instanceId=" + workflowId + "&orgId=3";
    window.open(bppUrl, "_blank");
};

/**
 * 【废弃审批】按钮命令
 * 状态14且有workflowid时可用，调用BPP废弃API
 */
CreditRecordForm.abandonBppApproval = function (primaryControl) {
    var formContext = primaryControl;
    var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
    var workflowId = formContext.getAttribute("mcs_workflowid").getValue();
    
    if (!workflowId) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_NoActiveBpp", "当前没有进行中的BPP审批流程"));
        return;
    }
    
    Xrm.Utility.confirmDialog(
        CreditRecordForm.L("CreditRecord_ConfirmAbandonBpp", "确定要废弃当前BPP审批流程吗？废弃后将回到人工复核阶段。"),
        function () {
            CreditRecordForm.showLoading(formContext, CreditRecordForm.L("CreditRecord_AbandoningBpp", "正在废弃BPP审批流程..."));
            
            // 调用mcs_bppabandonapi废弃审批
            var request = {
                EntityId: recordId,
                EntityName: "mcs_credit_record"
            };
            
            Xrm.WebApi.online.execute(request)
                .then(function (response) {
                    CreditRecordForm.hideLoading(formContext);
                    if (response.ok) {
                        formContext.ui.setFormNotification(CreditRecordForm.L("CreditRecord_BppAbandoned", "BPP审批已废弃"), "INFO", "bpp_abandon");
                        formContext.data.refresh(true).then(function () {
                            // 强制 BPF 弹出框重绘到新活动阶段（修复禅道#874）
                            CreditRecordForm.reselectActiveBpfStage(formContext);
                        });
                        setTimeout(function () {
                            formContext.ui.clearFormNotification("bpp_abandon");
                        }, 3000);
                    } else {
                        response.json().then(function (data) {
                            Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_AbandonFailedPrefix", "废弃失败：") + (data.error?.message || CreditRecordForm.L("CreditRecord_UnknownError", "未知错误")));
                        });
                    }
                })
                .catch(function (error) {
                    CreditRecordForm.hideLoading(formContext);
                    console.error("废弃BPP审批失败:", error);
                    Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_AbandonFailedPrefix", "废弃失败：") + (error.message || JSON.stringify(error)));
                });
        }
    );
};

/**
 * 显示BPP审批信息
 * 状态14时显示审批链接和当前审批人
 */
CreditRecordForm.showBppInfo = function (formContext) {
    var workflowId = formContext.getAttribute("mcs_workflowid").getValue();
    var nextApprover = formContext.getAttribute("mcs_nextapprover").getValue();
    var bppStatus = formContext.getAttribute("mcs_bppstatus").getValue();
    
    if (workflowId) {
        var msg = CreditRecordForm.L("CreditRecord_BppInProgressInfo", "BPP审批中 | 流程ID: ") + workflowId;
        if (nextApprover) {
            msg += CreditRecordForm.L("CreditRecord_CurrentApprover", " | 当前审批人: ") + nextApprover;
        }
        if (bppStatus) {
            msg += CreditRecordForm.L("CreditRecord_BppStatusLabel", " | 状态: ") + bppStatus;
        }
        formContext.ui.setFormNotification(msg, "INFO", "bpp_info");
    } else {
        formContext.ui.setFormNotification(CreditRecordForm.L("CreditRecord_BppStarting", "BPP审批流程发起中，请稍后..."), "INFO", "bpp_info");
    }
};

/**
 * 【搜索 Coface 企业】按钮命令
 * 状态9（发起）或状态10（关联客户）时可用；
 * mcs_cofaceid 已绑定时允许重新绑定（#1961，确认后打开搜索弹窗覆盖原绑定）
 */
CreditRecordForm.searchCofaceCompany = function (primaryControl) {
    var formContext = primaryControl;
    
    // 未保存记录时禁止搜索
    var recordId = formContext.data.entity.getId();
    if (!recordId) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_SaveRecordFirst", "请先保存记录"));
        return;
    }
    
    var status = formContext.getAttribute("mcs_status").getValue();
    var cofaceId = formContext.getAttribute("mcs_cofaceid").getValue();
    var accountId = formContext.getAttribute("mcs_accountid").getValue();
    
    if (status !== CreditRecordForm.STATUS.INIT && status !== CreditRecordForm.STATUS.LINK_ACCOUNT) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_SearchCofaceStageLimit", "【搜索 Coface 企业】仅在发起或关联客户阶段可用"));
        return;
    }
    
    if (!accountId) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_SelectAccountFirst", "请先选择客户"));
        return;
    }
    
    // #1961 允许重新绑定：仅在绑定阶段（上方已校验状态9/10），确认后打开搜索弹窗覆盖原绑定
    if (cofaceId) {
        Xrm.Utility.confirmDialog(
            CreditRecordForm.L("CreditRecord_CofaceRebindConfirm", "当前记录已绑定 Coface ID（{0}），重新绑定将覆盖原绑定，是否继续？").replace("{0}", cofaceId),
            function () {
                CreditRecordForm.openCofaceSearchDialog(formContext);
            }
        );
        return;
    }
    
    CreditRecordForm.openCofaceSearchDialog(formContext);
};

/**
 * 打开 Coface 企业搜索弹窗（首绑/重绑共用，#1961 抽取）
 * 通过 data 传递上下文（Modern UI 中弹窗无法直接访问 parent.Xrm.Page）
 */
CreditRecordForm.openCofaceSearchDialog = function (formContext) {
    var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
    var accountRef = formContext.getAttribute("mcs_accountid").getValue();
    var pageInput = {
        pageType: "webresource",
        webresourceName: "mcs_coface_company_search.html",
        data: JSON.stringify({
            creditRecordId: recordId,
            accountId: accountRef && accountRef.length > 0 ? accountRef[0].id.replace(/[{}]/g, "") : null,
            accountName: accountRef && accountRef.length > 0 ? accountRef[0].name : "",
            companyName: formContext.getAttribute("mcs_custnameen").getValue() || "",
            countryCode: formContext.getAttribute("mcs_countrycode").getValue() || ""
        })
    };
    var navigationOptions = {
        target: 2, // 弹窗
        width: 900,
        height: 600,
        position: 1 // 居中
    };
    
    Xrm.Navigation.navigateTo(pageInput, navigationOptions)
        .then(function () {
            // 弹窗关闭后刷新表单，并重新校验字段/清除旧提示
            return formContext.data.refresh(true);
        })
        .then(function () {
            CreditRecordForm.validateAccountFields(formContext);
        })
        .catch(function (error) {
            console.error("打开企业搜索弹窗失败:", error);
            Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_OpenSearchFailed", "打开搜索弹窗失败：") + (error.message || JSON.stringify(error)));
        });
};

// ==================== Coface 系统内下单 ====================

/**
 * Coface 下单状态选项集（与 mcs_credit_record.mcs_cofaceorderstatus 一致）
 */
CreditRecordForm.COFACE_ORDER_STATUS = {
    NONE: 0,              // 未下单
    INVESTIGATING: 1,     // 调查单已提交
    URBA_PENDING: 2,      // URBA已下单待就绪
    REPORT_PENDING: 3,    // Report已下单待就绪
    READY: 4,             // 已就绪
    FAILED: 5             // 下单失败
};

/**
 * Coface 下单状态名称（多语言，用于提示信息）
 */
CreditRecordForm.getCofaceOrderStatusName = function (orderStatus) {
    var defaults = {
        0: "未下单", 1: "调查单已提交", 2: "URBA已下单待就绪",
        3: "Report已下单待就绪", 4: "已就绪", 5: "下单失败"
    };
    if (orderStatus === null || orderStatus === undefined) orderStatus = 0;
    return CreditRecordForm.L("CreditRecord_CofaceOrderStatus_" + orderStatus, defaults[orderStatus] || ("未知状态(" + orderStatus + ")"));
};

/**
 * 调用 Custom API mcs_CofacePlaceOrder（推进下单状态机）
 * 返回解析后的 ResultJson：{ Status, Message, OrderStatus, OrderStatusName }
 */
CreditRecordForm.callCofacePlaceOrderApi = function (recordId) {
    var request = {
        getMetadata: function () {
            return {
                boundParameter: null,
                parameterTypes: {
                    "CreditRecordId": { typeName: "Edm.String", structuralProperty: 1 }
                },
                operationType: 0,
                operationName: "mcs_CofacePlaceOrder"
            };
        },
        CreditRecordId: recordId
    };

    return Xrm.WebApi.online.execute(request).then(function (response) {
        return response.json();
    }).then(function (data) {
        if (data && data.ResultJson) {
            return JSON.parse(data.ResultJson);
        }
        throw new Error(CreditRecordForm.L("CreditRecord_CofaceOrderAbnormal", "Coface 下单接口返回异常"));
    });
};

/**
 * 循环推进 Coface 下单状态机，直到「已就绪」或状态不再前进（T-0066，2026-09-03 生产反馈）
 * 背景：Coface 侧报告早已就绪（其他记录已下单用过）时，新记录本地状态仍从 0 起步，
 * 原来每点一次只推一步（0→2→3→4），用户要点 2-3 次。
 * 插件每步幂等（先查已有订单再下单），连续调用等价于用户连续点击，不会重复下单扣费。
 * 停止条件：已就绪(4) / 下单失败(5) / 接口失败(Status!=1) / 状态与上一次相同（到达真实等待态）/ 达到次数上限
 */
CreditRecordForm.callCofacePlaceOrderUntilStable = function (recordId, maxCalls) {
    maxCalls = maxCalls || 5;
    var lastStatus = -1;
    var attempt = function (remaining) {
        return CreditRecordForm.callCofacePlaceOrderApi(recordId).then(function (result) {
            var status = result ? result.OrderStatus : null;
            if (!result || result.Status !== 1 ||
                status === CreditRecordForm.COFACE_ORDER_STATUS.READY ||
                status === CreditRecordForm.COFACE_ORDER_STATUS.FAILED ||
                status === null || status === undefined ||
                status === lastStatus ||
                remaining <= 1) {
                return result;
            }
            lastStatus = status;
            return attempt(remaining - 1);
        });
    };
    return attempt(maxCalls);
};

/**
 * 【Coface 下单】按钮命令
 * 状态10（关联客户代码）且已绑定 Coface ID 时可用；
 * 点击后自动连续推进至「已就绪」或真实等待态（T-0066，原为每次点击只推一步）
 */
CreditRecordForm.placeCofaceOrder = function (primaryControl) {
    var formContext = primaryControl;

    // 未保存记录时禁止下单
    var recordId = formContext.data.entity.getId();
    if (!recordId) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_SaveRecordFirst", "请先保存记录"));
        return;
    }
    recordId = recordId.replace(/[{}]/g, "");

    var status = formContext.getAttribute("mcs_status").getValue();
    if (status !== CreditRecordForm.STATUS.LINK_ACCOUNT) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_CofaceOrderStageLimit", "【Coface 下单】仅在关联客户代码阶段可用"));
        return;
    }

    var cofaceId = formContext.getAttribute("mcs_cofaceid").getValue();
    if (!cofaceId) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_CofaceOrderNoCofaceId", "未关联科法斯客户，请先执行【搜索 Coface 企业】绑定 Coface ID"));
        return;
    }

    CreditRecordForm.showLoading(formContext, CreditRecordForm.L("CreditRecord_CofaceOrderPlacing", "正在执行 Coface 下单/状态查询，请稍候..."));

    CreditRecordForm.callCofacePlaceOrderUntilStable(recordId)
        .then(function (result) {
            CreditRecordForm.hideLoading(formContext);
            var message = (result && result.Message) || CreditRecordForm.L("CreditRecord_CofaceOrderAbnormal", "Coface 下单接口返回异常");
            if (result && result.OrderStatusName) {
                message += "\n" + CreditRecordForm.L("CreditRecord_CofaceOrderCurrentStatusPrefix", "当前下单状态：") + result.OrderStatusName;
            }
            Xrm.Utility.alertDialog(message);
            // 刷新表单显示最新下单三字段
            formContext.data.refresh(true);
        })
        .catch(function (error) {
            CreditRecordForm.hideLoading(formContext);
            console.error("Coface 下单失败:", error);
            Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_CofaceOrderFailedPrefix", "Coface 下单失败：") + (error.message || JSON.stringify(error)));
        });
};

/**
 * 进入数据集成前的就绪查询（#1850 改确认制）
 * 已就绪时直接放行；未就绪/查询失败时弹确认框，确定放行、取消阻断
 */
CreditRecordForm.checkCofaceOrderReadyAndProceed = function (formContext, recordId) {
    var orderStatusAttr = formContext.getAttribute("mcs_cofaceorderstatus");
    var orderStatus = orderStatusAttr ? orderStatusAttr.getValue() : null;

    // 已就绪：直接放行
    if (orderStatus === CreditRecordForm.COFACE_ORDER_STATUS.READY) {
        CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, CreditRecordForm.L("CreditRecord_EnterDataIntegration", "进入数据集成"));
        return;
    }

    // 未就绪：自动连续推进状态查询直至已就绪或真实等待态（T-0066），就绪则直接放行，否则弹确认框
    CreditRecordForm.showLoading(formContext, CreditRecordForm.L("CreditRecord_CofaceOrderChecking", "正在查询 Coface 订单状态，请稍候..."));

    CreditRecordForm.callCofacePlaceOrderUntilStable(recordId)
        .then(function (result) {
            CreditRecordForm.hideLoading(formContext);
            if (result && result.OrderStatus === CreditRecordForm.COFACE_ORDER_STATUS.READY) {
                formContext.data.refresh(true).then(function () {
                    CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, CreditRecordForm.L("CreditRecord_EnterDataIntegration", "进入数据集成"));
                });
                return;
            }
            formContext.data.refresh(true).then(function () {
                Xrm.Utility.confirmDialog(
                    CreditRecordForm.L("CreditRecord_ConfirmNextWithoutCofaceOrder", "没有Coface下单（订单未就绪），是否进入下一阶段？"),
                    function () {
                        CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, CreditRecordForm.L("CreditRecord_EnterDataIntegration", "进入数据集成"));
                        formContext.ui.setFormNotification(
                            CreditRecordForm.L("CreditRecord_CofaceOrderNotReadyCanProceed", "Coface 订单未就绪，已进入数据集成阶段，请在人工复核阶段补充标签数据。"),
                            "WARNING", "coface_not_ready"
                        );
                    },
                    function () {
                        // 用户取消，阻断不进入下一阶段
                    }
                );
            });
        })
        .catch(function (error) {
            CreditRecordForm.hideLoading(formContext);
            console.error("Coface 订单状态查询失败:", error);
            // 查询失败无法确认订单状态，弹确认框由用户决定，避免外部接口问题直接放行或阻断
            Xrm.Utility.confirmDialog(
                CreditRecordForm.L("CreditRecord_ConfirmNextOrderCheckFailed", "Coface订单状态查询失败，无法确认是否已下单，是否仍进入下一阶段？"),
                function () {
                    CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, CreditRecordForm.L("CreditRecord_EnterDataIntegration", "进入数据集成"));
                    formContext.ui.setFormNotification(
                        CreditRecordForm.L("CreditRecord_CofaceOrderCheckFailedCanProceed", "Coface 订单状态查询失败，已进入数据集成阶段，请在人工复核阶段补充标签数据。"),
                        "WARNING", "coface_check_failed"
                    );
                },
                function () {
                    // 用户取消，阻断不进入下一阶段
                }
            );
        });
};

/**
 * 校验所有客户信用标签是否已补录完成
 * 定量：mcs_itemintvalue2 有有效值且 mcs_itemvalue2 != "N/A"
 * 定性：mcs_credititem_value 有值
 */
CreditRecordForm.validateTagsCompleted = function (formContext, recordId, onPass) {
    var fetchXml = [
        "<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>",
        "  <entity name='mcs_customer_tag'>",
        "    <attribute name='mcs_customer_tagid' />",
        "    <attribute name='mcs_itemcode' />",
        "    <attribute name='mcs_datatype' />",
        "    <attribute name='mcs_itemintvalue2' />",
        "    <attribute name='mcs_itemvalue2' />",
        "    <attribute name='mcs_credititem_value' />",
        "    <attribute name='mcs_credit_item' />",
        "    <filter type='and'>",
        "      <condition attribute='mcs_credit_record' operator='eq' value='" + recordId + "' />",
        "      <condition attribute='mcs_active' operator='eq' value='1' />",
        "    </filter>",
        "  </entity>",
        "</fetch>"
    ].join("");

    Xrm.WebApi.online.retrieveMultipleRecords("mcs_customer_tag", "?fetchXml=" + encodeURIComponent(fetchXml))
        .then(function (result) {
            var missingItems = [];
            result.entities.forEach(function (tag) {
                // 提示名单优先显示评分项目中文名（Lookup 的 FormattedValue），取不到回退项目编码
                var itemName = tag["_mcs_credit_item_value@OData.Community.Display.V1.FormattedValue"] || tag.mcs_itemcode || "";
                var dataType = tag.mcs_datatype;
                if (dataType === 1) { // 定量
                    var intValue = tag.mcs_itemintvalue2;
                    var strValue = tag.mcs_itemvalue2;
                    if ((intValue == null) && (strValue == null || strValue === "N/A")) {
                        missingItems.push(itemName);
                    }
                } else { // 定性
                    // WebAPI（含 fetchXml）返回 Lookup 值的属性名为 _<逻辑名>_value，
                    // 直接读 mcs_credititem_value 恒为 undefined 会误报缺失
                    var lookupValue = tag["_mcs_credititem_value_value"];
                    if (!lookupValue) {
                        missingItems.push(itemName);
                    }
                }
            });

            if (missingItems.length > 0) {
                Xrm.Utility.alertDialog(
                    CreditRecordForm.L("CreditRecord_TagsNotCompleted", "以下标签尚未补录完成，请先在人工复核阶段录入：") + "\n" + missingItems.join("、")
                );
                return;
            }

            onPass();
        })
        .catch(function (error) {
            console.error("校验标签完整性失败:", error);
            Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_ValidateTagsFailed", "校验标签完整性失败：") + (error.message || JSON.stringify(error)));
        });
};

/**
 * 【重新发起】按钮命令
 * 状态12（人工复核）或状态16（审批未通过）可用，回到数据集成阶段重新评估
 */
CreditRecordForm.restartEvaluation = function (primaryControl) {
    var formContext = primaryControl;
    var status = formContext.getAttribute("mcs_status").getValue();
    var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
    
    // 仅在人工复核(12)或审批未通过(16)时可用
    if (status !== CreditRecordForm.STATUS.MANUAL_REVIEW && status !== CreditRecordForm.STATUS.REJECTED) {
        Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_RestartStageLimit", "【重新发起】仅在人工复核或审批未通过状态可用"));
        return;
    }
    
    Xrm.Utility.confirmDialog(
        CreditRecordForm.L("CreditRecord_ConfirmRestart", "确定要重新发起信用评估吗？这将回到数据集成阶段，您可以修改数据后重新提交审批。"),
        function () {
            // 更新状态到数据集成阶段（11），允许重新评估
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, CreditRecordForm.L("CreditRecord_RestartEvaluation", "重新发起评估"));
        },
        function () {
            // 用户取消，不做操作
        }
    );
};

/**
 * 显示等待遮罩
 */
CreditRecordForm.showLoading = function (formContext, message) {
    formContext.ui.setFormNotification(message || CreditRecordForm.L("CreditRecord_Processing", "正在处理，请稍候..."), "INFO", "loading_indicator");
};

/**
 * 隐藏等待遮罩
 */
CreditRecordForm.hideLoading = function (formContext) {
    formContext.ui.clearFormNotification("loading_indicator");
};

/**
 * 更新状态通用方法（带等待画面）
 */
CreditRecordForm.updateStatus = function (formContext, recordId, newStatus, successMsg) {
    // 标记为按钮触发，允许状态变更
    CreditRecordForm._bpfNavigating = true;
    CreditRecordForm._lastButtonStatus = newStatus;
    
    // 显示等待画面
    CreditRecordForm.showLoading(formContext, CreditRecordForm.L("CreditRecord_UpdatingStatus", "正在更新状态，请稍候..."));
    
    var entity = {};
    entity.mcs_status = newStatus;
    
    Xrm.WebApi.online.updateRecord("mcs_credit_record", recordId, entity)
        .then(function () {
            // 隐藏等待画面
            CreditRecordForm.hideLoading(formContext);
            
            // 显示成功通知（INFO级别，SUCCESS不被支持）
            formContext.ui.setFormNotification(
                successMsg + CreditRecordForm.L("CreditRecord_StatusUpdatedSuffix", "，状态已更新为：") + CreditRecordForm.getStatusName(newStatus),
                "INFO", "status_update"
            );
            
            // 刷新表单以反映状态变更，刷新完成后再清除标记
            return formContext.data.refresh(true);
        })
        .then(function () {
            // 3秒后清除成功通知
            setTimeout(function () {
                formContext.ui.clearFormNotification("status_update");
            }, 3000);

            // 强制 BPF 弹出框重绘到新活动阶段（修复禅道#874），完成后清除按钮触发标记
            CreditRecordForm.reselectActiveBpfStage(formContext, function () {
                CreditRecordForm._bpfNavigating = false;
            });
        })
        .catch(function (error) {
            // 隐藏等待画面
            CreditRecordForm.hideLoading(formContext);
            
            // 清除按钮触发标记
            CreditRecordForm._bpfNavigating = false;
            
            console.error("状态更新失败:", error);
            
            // 显示错误通知
            formContext.ui.setFormNotification(
                CreditRecordForm.L("CreditRecord_StatusUpdateFailed", "状态更新失败：") + (error.message || JSON.stringify(error)),
                "ERROR", "status_update_error"
            );
            
            Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_StatusUpdateFailed", "状态更新失败：") + (error.message || JSON.stringify(error)));
        });
};

// ==================== 按钮可用性规则（供Ribbon调用） ====================

/**
 * 【下一步】按钮是否可用
 * 供Ribbon EnableRule调用
 */
CreditRecordForm.canNextStep = function () {
    var formContext = Xrm.Page;
    var status = formContext.getAttribute("mcs_status").getValue();
    // 状态9-13可用
    return status >= CreditRecordForm.STATUS.INIT && status <= CreditRecordForm.STATUS.SCORE_CALC;
};

/**
 * 【数据集成刷新】按钮是否可用
 * 供Ribbon EnableRule调用
 */
CreditRecordForm.canRefreshData = function () {
    var formContext = Xrm.Page;
    var status = formContext.getAttribute("mcs_status").getValue();
    return status === CreditRecordForm.STATUS.MANUAL_REVIEW;
};

/**
 * 【重新发起】按钮是否可用
 * 供Ribbon EnableRule调用
 * 人工复核(12)或审批未通过(16)时可用
 */
CreditRecordForm.canRestart = function () {
    var formContext = Xrm.Page;
    var status = formContext.getAttribute("mcs_status").getValue();
    return status === CreditRecordForm.STATUS.MANUAL_REVIEW || status === CreditRecordForm.STATUS.REJECTED;
};

/**
 * 【查看审批】按钮是否可用
 * 供Ribbon EnableRule调用
 * 有workflowid时可用
 */
CreditRecordForm.canViewBpp = function () {
    var formContext = Xrm.Page;
    var workflowId = formContext.getAttribute("mcs_workflowid").getValue();
    return !!workflowId;
};

/**
 * 【废弃审批】按钮是否可用
 * 供Ribbon EnableRule调用
 * 状态14且已有workflowid时可用
 */
CreditRecordForm.canAbandonBpp = function () {
    var formContext = Xrm.Page;
    var status = formContext.getAttribute("mcs_status").getValue();
    var workflowId = formContext.getAttribute("mcs_workflowid").getValue();
    return status === CreditRecordForm.STATUS.AUDIT_APPLY && !!workflowId;
};

/**
 * 【搜索 Coface 企业】按钮是否可用
 * 供Ribbon EnableRule调用
 * 状态9或10，且已选择客户（已绑定 Coface ID 时仍可用，用于重新绑定 #1961）
 */
CreditRecordForm.canSearchCofaceCompany = function () {
    var formContext = Xrm.Page;
    var status = formContext.getAttribute("mcs_status").getValue();
    var accountId = formContext.getAttribute("mcs_accountid").getValue();
    
    return (status === CreditRecordForm.STATUS.INIT || status === CreditRecordForm.STATUS.LINK_ACCOUNT) &&
           !!accountId;
};

// ==================== 附件页签初始化 ====================

/**
 * 初始化附件页签
 * 评估记录表单嵌入通用上传组件 Uploader.html，用于管理客户资信附件
 */
CreditRecordForm.initAttachmentTab = function (formContext) {
    try {
        var accountField = formContext.getAttribute("mcs_accountid");
        if (!accountField || !accountField.getValue()) {
            // 未选择客户时，附件页签不初始化
            return;
        }
        
        var accountValue = accountField.getValue()[0];
        var accountId = accountValue.id.replace(/[{}]/g, "");
        
        // 尝试获取 Uploader WebResource 控件，动态补充当前客户上下文
        var uploaderControl = formContext.getControl("mcs_credit_record_uploader");
        if (uploaderControl && uploaderControl.getObject) {
            var uploaderObj = uploaderControl.getObject();
            if (uploaderObj && uploaderObj.contentWindow && uploaderObj.contentWindow.initUploaderContext) {
                uploaderObj.contentWindow.initUploaderContext({
                    entityName: "mcs_customer_file",
                    relatedEntityName: "account",
                    relatedEntityId: accountId,
                    relatedEntityDisplayName: accountValue.name || ""
                });
            }
        }
        
        console.log("附件页签已初始化，关联客户: " + accountId);
    } catch (ex) {
        console.error("初始化附件页签失败:", ex);
    }
};

// ==================== 保存前校验 ====================

/**
 * 保存前校验
 */
// 重复客户在途评估校验状态标志
CreditRecordForm._duplicateCheckInProgress = false;
CreditRecordForm._duplicateCheckPassed = false;

/**
 * 查询同客户是否存在未生效（状态 9-14）评估记录，返回第一条或 null（禅道 #867/#1645）
 */
CreditRecordForm.queryInFlightRecord = function (accountGuid) {
    var statusFilter = [
        "mcs_status eq 9",
        "mcs_status eq 10",
        "mcs_status eq 11",
        "mcs_status eq 12",
        "mcs_status eq 13",
        "mcs_status eq 14"
    ].join(" or ");
    var filter = "_mcs_accountid_value eq " + accountGuid + " and (" + statusFilter + ") and statecode eq 0";

    return Xrm.WebApi.retrieveMultipleRecords("mcs_credit_record", "?$select=mcs_scoreid&$filter=" + encodeURIComponent(filter) + "&$top=1")
        .then(function (result) {
            return result.entities.length > 0 ? result.entities[0] : null;
        });
};

/**
 * 新建表单客户变更检测（禅道 #1645）
 * 同一客户只允许存在一条未生效（状态 9-14）评估记录：
 * 检测到已存在时提示，确认后跳转到已存在记录（当前新建表单不保存）；取消则继续编辑，保存时由 onSave 兜底阻断
 */
CreditRecordForm.checkInFlightRecordOnCreate = function (executionContext) {
    var formContext = executionContext.getFormContext();
    if (formContext.ui.getFormType() !== 1) return;

    var accountValue = formContext.getAttribute("mcs_accountid").getValue();
    if (!accountValue || accountValue.length === 0) return;

    var accountGuid = accountValue[0].id.replace(/[{}]/g, "");
    CreditRecordForm.queryInFlightRecord(accountGuid).then(function (record) {
        if (!record) return;
        Xrm.Utility.confirmDialog(
            CreditRecordForm.L("CreditRecord_InFlightExistsOpen", "检测到该客户下有一条正在编辑中的数据（{0}），是否需要为你打开？").replace("{0}", record.mcs_scoreid || ""),
            function () {
                // 确认：跳转到已存在记录，当前新建表单不保存
                Xrm.Navigation.navigateTo({
                    pageType: "entityrecord",
                    entityName: "mcs_credit_record",
                    entityId: record.mcs_credit_recordid
                });
            },
            function () {
                // 取消：不做任何操作，保存时会再次校验并阻断
            }
        );
    }).catch(function (error) {
        console.error("检测未生效评估记录失败:", error);
    });
};

CreditRecordForm.onSave = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    
    // 新建时校验客户信息
    if (formType === 1) {
        // 已通过重复客户校验，放行本次保存
        if (CreditRecordForm._duplicateCheckPassed) {
            CreditRecordForm._duplicateCheckPassed = false;
            return;
        }
        
        var accountId = formContext.getAttribute("mcs_accountid").getValue();
        if (!accountId) {
            Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_PleaseSelectCustomer", "请选择客户"));
            executionContext.getEventArgs().preventDefault();
            return;
        }
        
        var custNameEn = formContext.getAttribute("mcs_custnameen").getValue();
        var countryCode = formContext.getAttribute("mcs_countrycode").getValue();
        
        if (!custNameEn || !countryCode) {
            Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_EnNameCountryRequired", "客户英文名称和国家编码不能为空，请先维护客户主数据"));
            executionContext.getEventArgs().preventDefault();
            return;
        }
        
        // 防止重复点击保存导致多次查询
        if (CreditRecordForm._duplicateCheckInProgress) {
            executionContext.getEventArgs().preventDefault();
            return;
        }
        
        // 校验是否存在相同客户的未生效评估记录（状态 9-14，禅道 #867/#1645）
        CreditRecordForm._duplicateCheckInProgress = true;
        executionContext.getEventArgs().preventDefault();
        
        var accountGuid = accountId[0].id.replace(/[{}]/g, "");
        
        CreditRecordForm.queryInFlightRecord(accountGuid)
            .then(function (record) {
                CreditRecordForm._duplicateCheckInProgress = false;
                if (record) {
                    Xrm.Utility.alertDialog(CreditRecordForm.L("CreditRecord_InFlightExistsBlock", "该客户下已存在一条未生效的评估记录（{0}），不允许保存，请打开已有记录继续编辑。").replace("{0}", record.mcs_scoreid || ""));
                } else {
                    // 没有重复，标记通过后重新触发保存
                    CreditRecordForm._duplicateCheckPassed = true;
                    formContext.data.save().then(
                        function () { CreditRecordForm._duplicateCheckPassed = false; },
                        function (error) {
                            CreditRecordForm._duplicateCheckPassed = false;
                            console.error("保存失败:", error);
                        }
                    );
                }
            })
            .catch(function (error) {
                CreditRecordForm._duplicateCheckInProgress = false;
                CreditRecordForm._duplicateCheckPassed = false;
                console.error("查询重复评估记录失败:", error);
                Xrm.Utility.alertDialog("校验重复评估记录失败：" + (error.message || JSON.stringify(error)));
            });
    }
};
