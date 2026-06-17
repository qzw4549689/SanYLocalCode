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

// BPF 状态变更标记 — 用于区分是按钮触发还是 BPF 直接修改
CreditRecordForm._bpfNavigating = false;
CreditRecordForm._lastButtonStatus = null;

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
    
    // 初始化状态记录
    var statusField = formContext.getAttribute("mcs_status");
    if (statusField) {
        CreditRecordForm._lastButtonStatus = statusField.getValue();
    }
    
    // 初始化附件页签（通用上传组件）
    CreditRecordForm.initAttachmentTab(formContext);
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
        Xrm.Utility.alertDialog("请使用上方工具栏的按钮进行状态流转，不要直接修改进度条中的状态。");
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
    
    // 查询Account信息及关联的客户主数据
    // PRD: 客户编码唯一性在客户主数据表维护，引用客户数据使用客户主数据表
    // - mcs_customermasterdata.mcs_accountnumber: 客户编码（SAP客户代码）
    // - mcs_customermasterdata.mcs_englishname: 客户英文名称
    // - mcs_customermasterdata.mcs_countrycode: 国家编码
    // - mcs_customermasterdata.mcs_cofaceid: 科法斯ID
    Xrm.WebApi.retrieveRecord("account", accountGuid, "?$select=mcs_name&$expand=mcs_customermasterdata($select=mcs_accountnumber,mcs_englishname,mcs_countrycode,mcs_cofaceid)")
        .then(function (result) {
            var customerMasterData = result.mcs_customermasterdata || {};

            // 客户编码（从客户主数据表mcs_accountnumber带出）
            var custNameField = formContext.getAttribute("mcs_custname");
            if (custNameField) {
                custNameField.setValue(customerMasterData.mcs_accountnumber || "");
            }
            
            // 客户英文名称（从客户主数据表带出）
            var custNameEnField = formContext.getAttribute("mcs_custnameen");
            if (custNameEnField) {
                custNameEnField.setValue(customerMasterData.mcs_englishname || "");
            }
            
            // 国家编码（从客户主数据表带出）
            var countryCodeField = formContext.getAttribute("mcs_countrycode");
            if (countryCodeField) {
                countryCodeField.setValue(customerMasterData.mcs_countrycode || "");
            }
            
            // 科法斯ID（从客户主数据表带出）
            var cofaceField = formContext.getAttribute("mcs_cofaceid");
            if (cofaceField) {
                cofaceField.setValue(customerMasterData.mcs_cofaceid || "");
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
            // 提交审核申请，触发BPP Plugin
            CreditRecordForm.submitBppApproval(formContext, recordId);
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
            // 方式：将状态改回11（数据集成），触发 CofaceDataSyncPlugin 重新执行
            CreditRecordForm.updateStatus(formContext, recordId, CreditRecordForm.STATUS.DATA_INTEGRATION, "数据集成刷新已触发");
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
            throw new Error("当前用户未配置mcs_personnel.domainaccount");
        });
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
        Xrm.Utility.alertDialog("信用分尚未计算，请先完成信用分计算");
        return;
    }

    // 防重复提交：已有 workflowid 时跳过
    var workflowId = formContext.getAttribute("mcs_workflowid").getValue();
    if (workflowId) {
        Xrm.Utility.alertDialog("当前记录已存在BPP审批流程，请勿重复提交");
        return;
    }

    // 确认提交
    Xrm.Utility.confirmDialog(
        "确定要提交BPP审批吗？提交后将锁定所有字段并进入审批流程。",
        function () {
            CreditRecordForm.showLoading(formContext, "正在提交BPP审批...");

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
                            formContext.ui.setFormNotification("BPP审批提交成功", "INFO", "bpp_start");
                            formContext.data.refresh(true);
                            setTimeout(function () {
                                formContext.ui.clearFormNotification("bpp_start");
                            }, 3000);
                        } else {
                            Xrm.Utility.alertDialog(result.Description || "BPP审批提交失败");
                        }
                    } else {
                        Xrm.Utility.alertDialog("BPP审批提交返回异常");
                    }
                })
                .catch(function (error) {
                    CreditRecordForm.hideLoading(formContext);
                    console.error("提交BPP审批失败:", error);
                    Xrm.Utility.alertDialog("提交BPP审批失败：" + (error.message || JSON.stringify(error)));
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
        Xrm.Utility.alertDialog("暂无BPP审批信息");
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
        Xrm.Utility.alertDialog("当前没有进行中的BPP审批流程");
        return;
    }
    
    Xrm.Utility.confirmDialog(
        "确定要废弃当前BPP审批流程吗？废弃后将回到人工复核阶段。",
        function () {
            CreditRecordForm.showLoading(formContext, "正在废弃BPP审批流程...");
            
            // 调用mcs_bppabandonapi废弃审批
            var request = {
                EntityId: recordId,
                EntityName: "mcs_credit_record"
            };
            
            Xrm.WebApi.online.execute(request)
                .then(function (response) {
                    CreditRecordForm.hideLoading(formContext);
                    if (response.ok) {
                        formContext.ui.setFormNotification("BPP审批已废弃", "INFO", "bpp_abandon");
                        formContext.data.refresh(true);
                        setTimeout(function () {
                            formContext.ui.clearFormNotification("bpp_abandon");
                        }, 3000);
                    } else {
                        response.json().then(function (data) {
                            Xrm.Utility.alertDialog("废弃失败：" + (data.error?.message || "未知错误"));
                        });
                    }
                })
                .catch(function (error) {
                    CreditRecordForm.hideLoading(formContext);
                    console.error("废弃BPP审批失败:", error);
                    Xrm.Utility.alertDialog("废弃失败：" + (error.message || JSON.stringify(error)));
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
        var msg = "BPP审批中 | 流程ID: " + workflowId;
        if (nextApprover) {
            msg += " | 当前审批人: " + nextApprover;
        }
        if (bppStatus) {
            msg += " | 状态: " + bppStatus;
        }
        formContext.ui.setFormNotification(msg, "INFO", "bpp_info");
    } else {
        formContext.ui.setFormNotification("BPP审批流程发起中，请稍后...", "INFO", "bpp_info");
    }
};

/**
 * 【搜索 Coface 企业】按钮命令
 * 状态9（发起）或状态10（关联客户）且 mcs_cofaceid 为空时可用
 */
CreditRecordForm.searchCofaceCompany = function (primaryControl) {
    var formContext = primaryControl;
    var status = formContext.getAttribute("mcs_status").getValue();
    var cofaceId = formContext.getAttribute("mcs_cofaceid").getValue();
    var accountId = formContext.getAttribute("mcs_accountid").getValue();
    
    if (status !== CreditRecordForm.STATUS.INIT && status !== CreditRecordForm.STATUS.LINK_ACCOUNT) {
        Xrm.Utility.alertDialog("【搜索 Coface 企业】仅在发起或关联客户阶段可用");
        return;
    }
    
    if (!accountId) {
        Xrm.Utility.alertDialog("请先选择客户");
        return;
    }
    
    if (cofaceId) {
        Xrm.Utility.alertDialog("当前记录已绑定 Coface ID，无需重新搜索");
        return;
    }
    
    // 打开企业搜索弹窗，通过 data 传递上下文（Modern UI 中弹窗无法直接访问 parent.Xrm.Page）
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
            // 弹窗关闭后刷新表单
            formContext.data.refresh(true);
        })
        .catch(function (error) {
            console.error("打开企业搜索弹窗失败:", error);
            Xrm.Utility.alertDialog("打开搜索弹窗失败：" + (error.message || JSON.stringify(error)));
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
        Xrm.Utility.alertDialog("【重新发起】仅在人工复核或审批未通过状态可用");
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
 * 显示等待遮罩
 */
CreditRecordForm.showLoading = function (formContext, message) {
    formContext.ui.setFormNotification(message || "正在处理，请稍候...", "INFO", "loading_indicator");
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
    CreditRecordForm.showLoading(formContext, "正在更新状态，请稍候...");
    
    var entity = {};
    entity.mcs_status = newStatus;
    
    Xrm.WebApi.online.updateRecord("mcs_credit_record", recordId, entity)
        .then(function () {
            // 隐藏等待画面
            CreditRecordForm.hideLoading(formContext);
            
            // 显示成功通知（INFO级别，SUCCESS不被支持）
            formContext.ui.setFormNotification(
                successMsg + "，状态已更新为：" + CreditRecordForm.STATUS_NAMES[newStatus],
                "INFO", "status_update"
            );
            
            // 刷新表单以反映状态变更
            formContext.data.refresh(true);
            
            // 3秒后清除成功通知
            setTimeout(function () {
                formContext.ui.clearFormNotification("status_update");
            }, 3000);
            
            // 清除按钮触发标记
            CreditRecordForm._bpfNavigating = false;
        })
        .catch(function (error) {
            // 隐藏等待画面
            CreditRecordForm.hideLoading(formContext);
            
            // 清除按钮触发标记
            CreditRecordForm._bpfNavigating = false;
            
            console.error("状态更新失败:", error);
            
            // 显示错误通知
            formContext.ui.setFormNotification(
                "状态更新失败：" + (error.message || JSON.stringify(error)),
                "ERROR", "status_update_error"
            );
            
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
 * 状态9或10，且 mcs_cofaceid 为空，且已选择客户
 */
CreditRecordForm.canSearchCofaceCompany = function () {
    var formContext = Xrm.Page;
    var status = formContext.getAttribute("mcs_status").getValue();
    var cofaceId = formContext.getAttribute("mcs_cofaceid").getValue();
    var accountId = formContext.getAttribute("mcs_accountid").getValue();
    
    return (status === CreditRecordForm.STATUS.INIT || status === CreditRecordForm.STATUS.LINK_ACCOUNT) &&
           !cofaceId && !!accountId;
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
