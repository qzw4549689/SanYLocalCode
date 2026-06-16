/**
 * 客户信用评估记录表 - 表单逻辑
 * 实体: mcs_credit_record
 * 功能: 客户信息带出、校验、状态流转、按钮控制、默认字段
 * 影响范围: 仅限mcs_credit_record实体
 */

var CreditRecordForm = CreditRecordForm || {};

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

// 状态名称映射（用于提示信息）
CreditRecordForm.STATUS_NAMES = {
    9: "发起信用评估",
    10: "关联客户代码",
    11: "内外部数据集成",
    12: "人工复核",
    13: "信用分计算",
    14: "审核申请",
    15: "审批通过",
    16: "审批未通过"
};

// ==================== 表单事件 ====================

/**
 * 表单加载事件
 */
CreditRecordForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    
    // 新建时设置默认值
    if (formType === 1) {
        CreditRecordForm.setDefaults(formContext);
    }
    
    // 设置只读字段（含评估状态）
    CreditRecordForm.setFieldsReadOnly(formContext);
    
    // 注册字段变更事件
    CreditRecordForm.registerEvents(formContext);
    
    // 根据状态控制按钮/字段/通知
    CreditRecordForm.toggleByStatus(formContext);
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
    // 评估状态始终只读（由BPF/工作流控制，不允许手动修改）
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
        "mcs_abidate", "mcs_checkdate", "mcs_scoredate"
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
    }
    
    // 状态变更 - 控制字段锁定和按钮
    var statusField = formContext.getAttribute("mcs_status");
    if (statusField) {
        statusField.addOnChange(CreditRecordForm.onStatusChange);
    }
};

/**
 * 状态变更事件
 */
CreditRecordForm.onStatusChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
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
    
    // 查询Account信息
    // 注意：客户表字段说明
    // - accountnumber: 客户编码（标准字段）
    // - mcs_englishname: 客户英文名称（新增字段）
    // - mcs_country: 注册国家/地区（Lookup），需展开取国家代码
    // - mcs_cofaceid: 科法斯客户代码（新增字段）
    Xrm.WebApi.retrieveRecord("account", accountGuid, "?$select=accountnumber,mcs_englishname,mcs_cofaceid&$expand=mcs_country($select=mcs_countrycode)")
        .then(function (result) {
            // 客户编码（从accountnumber带出）
            var custNameField = formContext.getAttribute("mcs_custname");
            if (custNameField) {
                custNameField.setValue(result.accountnumber || "");
            }
            
            // 客户英文名称
            var custNameEnField = formContext.getAttribute("mcs_custnameen");
            if (custNameEnField) {
                custNameEnField.setValue(result.mcs_englishname || "");
            }
            
            // 国家编码（从mcs_country Lookup展开获取）
            var countryCodeField = formContext.getAttribute("mcs_countrycode");
            if (countryCodeField) {
                var countryCode = "";
                if (result.mcs_country && result.mcs_country.mcs_countrycode) {
                    countryCode = result.mcs_country.mcs_countrycode;
                }
                countryCodeField.setValue(countryCode);
            }
            
            // 科法斯ID
            var cofaceField = formContext.getAttribute("mcs_cofaceid");
            if (cofaceField) {
                cofaceField.setValue(result.mcs_cofaceid || "");
            }
            
            // 校验提示
            CreditRecordForm.validateAccountFields(formContext);
        })
        .catch(function (error) {
            console.error("查询客户信息失败:", error);
            Xrm.Utility.alertDialog("查询客户信息失败，请重试。错误：" + (error.message || JSON.stringify(error)));
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
        messages.push("客户英文名称不能为空，请先维护客户主数据");
    }
    
    if (!countryCode) {
        messages.push("国家编码不能为空");
    }
    
    if (!cofaceId) {
        messages.push("未关联科法斯客户，请先执行【关联客户代码】操作");
    }
    
    if (messages.length > 0) {
        // 显示通知（不阻断）
        var notification = {
            messages: messages,
            level: "WARNING",
            uniqueId: "account_validation"
        };
        formContext.ui.setFormNotification(messages.join("；"), "WARNING", "account_validation");
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
            
        case CreditRecordForm.STATUS.DATA_INTEGRATION: // 11 - 内外部数据集成
            // 数据集成中，关键字段锁定
            CreditRecordForm.setControlEditable(formContext, "mcs_accountid", false);
            break;
            
        case CreditRecordForm.STATUS.MANUAL_REVIEW: // 12 - 人工复核
            // 复核阶段，允许编辑标签子网格（客户锁定）
            CreditRecordForm.setControlEditable(formContext, "mcs_accountid", false);
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
                Xrm.Utility.alertDialog("请先选择客户");
                return;
            }
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.LINK_ACCOUNT, "已关联客户");
            break;
            
        case CreditRecordForm.STATUS.LINK_ACCOUNT: // 10 → 11
            // 校验Coface ID存在
            var cofaceId = formContext.getAttribute("mcs_cofaceid").getValue();
            if (!cofaceId) {
                Xrm.Utility.alertDialog("未关联科法斯客户，无法进入数据集成阶段");
                return;
            }
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, "进入数据集成");
            break;
            
        case CreditRecordForm.STATUS.DATA_INTEGRATION: // 11 → 12
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.MANUAL_REVIEW, "进入人工复核");
            break;
            
        case CreditRecordForm.STATUS.MANUAL_REVIEW: // 12 → 13
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.SCORE_CALC, "进入信用分计算");
            break;
            
        case CreditRecordForm.STATUS.SCORE_CALC: // 13 → 14
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.AUDIT_APPLY, "提交审核申请");
            break;
            
        default:
            Xrm.Utility.alertDialog("当前状态不支持【下一步】操作");
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
        Xrm.Utility.alertDialog("【数据集成刷新】仅在人工复核阶段可用");
        return;
    }
    
    Xrm.Utility.confirmDialog(
        "确定要重新执行数据集成吗？这将刷新所有指标数据。",
        function () {
            // 调用自定义Action触发数据集成刷新
            var req = {
                getMetadata: function () {
                    return {
                        boundParameter: "entity",
                        parameterTypes: {},
                        operationType: 0,
                        operationName: "mcs_RefreshDataIntegration"
                    };
                }
            };
            
            Xrm.WebApi.online.execute(req)
                .then(function (result) {
                    if (result.ok) {
                        Xrm.Utility.alertDialog("数据集成刷新已触发，请稍后查看结果");
                        formContext.data.refresh(true);
                    } else {
                        Xrm.Utility.alertDialog("数据集成刷新失败");
                    }
                })
                .catch(function (error) {
                    console.error("数据集成刷新失败:", error);
                    Xrm.Utility.alertDialog("数据集成刷新失败：" + (error.message || JSON.stringify(error)));
                });
        },
        function () {
            // 用户取消，不做操作
        }
    );
};

/**
 * 【重新发起】按钮命令
 * 仅状态16（审批未通过）可用，回到数据集成阶段重新评估
 */
CreditRecordForm.restartEvaluation = function (primaryControl) {
    var formContext = primaryControl;
    var status = formContext.getAttribute("mcs_status").getValue();
    var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
    
    if (status !== CreditRecordForm.STATUS.REJECTED) {
        Xrm.Utility.alertDialog("【重新发起】仅在审批未通过状态可用");
        return;
    }
    
    Xrm.Utility.confirmDialog(
        "确定要重新发起信用评估吗？这将回到数据集成阶段，您可以修改数据后重新提交审批。",
        function () {
            // 更新状态到数据集成阶段（11），允许重新评估
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, "重新发起评估");
        },
        function () {
            // 用户取消，不做操作
        }
    );
};

/**
 * 更新状态通用方法
 */
CreditRecordForm.updateStatus = function (formContext, recordId, newStatus, successMsg) {
    var entity = {};
    entity.mcs_status = newStatus;
    
    Xrm.WebApi.online.updateRecord("mcs_credit_record", recordId, entity)
        .then(function () {
            formContext.ui.setFormNotification(
                successMsg + "，状态已更新为：" + CreditRecordForm.STATUS_NAMES[newStatus],
                "SUCCESS", "status_update"
            );
            // 刷新表单以反映状态变更
            formContext.data.refresh(true);
        })
        .catch(function (error) {
            console.error("状态更新失败:", error);
            Xrm.Utility.alertDialog("状态更新失败：" + (error.message || JSON.stringify(error)));
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
 */
CreditRecordForm.canRestart = function () {
    var formContext = Xrm.Page;
    var status = formContext.getAttribute("mcs_status").getValue();
    return status === CreditRecordForm.STATUS.REJECTED;
};

// ==================== 保存前校验 ====================

/**
 * 保存前校验
 */
CreditRecordForm.onSave = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    
    // 新建时校验客户信息
    if (formType === 1) {
        var accountId = formContext.getAttribute("mcs_accountid").getValue();
        if (!accountId) {
            Xrm.Utility.alertDialog("请选择客户");
            executionContext.getEventArgs().preventDefault();
            return;
        }
        
        var custNameEn = formContext.getAttribute("mcs_custnameen").getValue();
        var countryCode = formContext.getAttribute("mcs_countrycode").getValue();
        
        if (!custNameEn || !countryCode) {
            Xrm.Utility.alertDialog("客户英文名称和国家编码不能为空，请先维护客户主数据");
            executionContext.getEventArgs().preventDefault();
            return;
        }
    }
};
