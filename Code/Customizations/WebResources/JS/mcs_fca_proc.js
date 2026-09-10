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

var FcaProcForm = FcaProcForm || {};
(function (self) {
    "use strict";

    /**
     * 多语言取词（带中文兜底）
     * 语言包已加载时返回对应语言文本；未加载/未找到时返回原中文，保证中文用户不受影响
     */
    function t(key, defaultText) {
        if (typeof LanguageHelper !== "undefined") {
            var v = LanguageHelper.getLabel(key);
            if (v && v !== key) return v;
        }
        return defaultText;
    }

    // BPF 阶段名称（与 BPF 配置保持一致）
    var STAGE_CREDIT_CHECK = "授信校验";
    var STAGE_MODEL_CALC = "模型计算";
    var STAGE_ACTIVE = "生效启用";

    // BPF 阶段 ID（更可靠，避免阶段名重复或包含关系导致误判）
    var STAGE_ID_CREDIT_CHECK = "d99dffdf-9695-4d81-80c8-90f1729114cf";
    var STAGE_ID_MODEL_CALC = "2bcdcf27-1976-4a6f-89af-be34dcd5c857";
    var STAGE_ID_ACTIVE = "8e2525de-5005-4767-83cf-dd9dc5fe0aa2";

    // 状态值
    var STATUS_CREDIT_CHECK = 1;
    var STATUS_MODEL_CALC = 2;
    var STATUS_ACTIVE = 3;
    var STATUS_RETURN = 4;

    // 不予授信场景值
    // 1/2 由人工选择（禅道 #1307：人工勾选也需拦截，不允许进入模型计算）
    // 3/4 由系统判断优先于人工（逾期/黑名单自动校验）
    var REJECT_SCENE_CREDIT_BLACKLIST = 1;
    var REJECT_SCENE_BAD_DEBT = 2;
    var REJECT_SCENE_OVERDUE = 3;
    var REJECT_SCENE_BLACKLIST = 4;

    /**
     * 表单加载事件
     * 1. 新建记录时设置默认值
     * 2. 注册 BPF 阶段切换前事件
     * 3. 注册客户名称变更事件，自动带出客户编码
     * 4. 计算状态 = 生效启用 时，全表单字段只读
     */
    self.onLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();

        // 预加载语言包（异步，不阻塞后续逻辑）
        if (typeof LanguageHelper !== "undefined") {
            LanguageHelper.loadLanguagePack();
        }

        if (formContext.ui.getFormType() === 1) {
            setDefaultValues(formContext);
            // 新建前置校验：无生效且在有效期内的模型版本时提示并阻止保存
            checkActiveModelVersionOnCreate();
        }

        // 注册客户名称变更事件
        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (accountAttr) {
            accountAttr.addOnChange(function () {
                onAccountChange(formContext);
            });
            // 新建表单：客户变更后检测同客户未生效模型计算记录（禅道 #1644）
            if (formContext.ui.getFormType() === 1) {
                accountAttr.addOnChange(function () {
                    checkInFlightRecordOnCreate(formContext);
                });
            }
            // 表单加载时如果已有值，也触发一次
            if (accountAttr.getValue() !== null) {
                onAccountChange(formContext);
            }
        }

        // 注册 BPF 阶段切换前事件，拦截“下一步”做业务校验
        if (formContext.data && formContext.data.process) {
            // 先移除可能已存在的旧 handler，避免重复注册导致事件多次触发
            if (self._preStageChangeHandler) {
                formContext.data.process.removeOnPreStageChange(self._preStageChangeHandler);
            }
            self._preStageChangeHandler = function (eventContext) {
                onPreStageChange(eventContext, formContext);
            };
            formContext.data.process.addOnPreStageChange(self._preStageChangeHandler);

            // 阶段切换时刷新阶段字段只读状态
            if (self._stageChangeHandler) {
                formContext.data.process.removeOnStageChange(self._stageChangeHandler);
            }
            self._stageChangeHandler = function () {
                lockBpfFields(formContext);
                applyReadOnlyByStatus(formContext);
                applyCreditRejectLock(formContext);
            };
            formContext.data.process.addOnStageChange(self._stageChangeHandler);
        }

        // 初始设置 BPF 阶段字段只读
        lockBpfFields(formContext);

        // 计算状态 = 生效启用 时，全表单字段只读
        applyReadOnlyByStatus(formContext);

        // 不予授信字段仅授信校验阶段可编辑
        applyCreditRejectLock(formContext);
    };

    /**
     * 客户名称变更处理
     * 选择客户后：
     * 1. mcs_accountid 指向 mcs_customermasterdata，直接取客户主数据的 mcs_sapnumber 写入客户编码(mcs_custname)
     * 2. 同时反查关联 account，缓存真实 Account ID，供逾期数据查询使用
     * 3. 自动校验场景 3/4 并默认勾选（覆盖人工选择）
     */
    function onAccountChange(formContext) {
        var accountAttr = formContext.getAttribute("mcs_accountid");
        var custNameAttr = formContext.getAttribute("mcs_custname");
        if (!accountAttr) return;

        var accountValue = accountAttr.getValue();
        if (!accountValue || accountValue.length === 0) {
            if (custNameAttr) custNameAttr.setValue(null);
            _cachedAccountId = null;
            _cachedRejectScenes = [];
            lockBpfFields(formContext);
            return;
        }

        var masterDataId = accountValue[0].id.replace(/[{}]/g, "");

        // mcs_accountid 指向 mcs_customermasterdata，客户编码应取主数据的 mcs_sapnumber
        var masterDataPromise = Xrm.WebApi.retrieveRecord("mcs_customermasterdata", masterDataId, "?$select=mcs_sapnumber").then(
            function (result) {
                if (custNameAttr) {
                    custNameAttr.setValue(result.mcs_sapnumber || "");
                }
                return result;
            },
            function (error) {
                console.error("查询客户主数据失败:", error);
                if (custNameAttr) custNameAttr.setValue(null);
                return null;
            }
        );

        // 同时反查关联 account，缓存真实 Account ID，供逾期数据查询使用
        var accountFilter = "_mcs_customermasterdata_value eq " + masterDataId;
        var accountPromise = Xrm.WebApi.retrieveMultipleRecords("account", "?$select=accountid&$filter=" + accountFilter + "&$top=1").then(
            function (result) {
                if (result.entities.length > 0) {
                    _cachedAccountId = result.entities[0].accountid;
                } else {
                    _cachedAccountId = null;
                    console.warn("未找到关联 Account，逾期校验将跳过");
                }
                return _cachedAccountId;
            },
            function (error) {
                console.error("查询 Account 失败:", error);
                _cachedAccountId = null;
                return null;
            }
        );

        Promise.all([masterDataPromise, accountPromise]).then(function () {
            // 自动校验场景 3/4，并默认勾选/覆盖
            checkAutoRejectScenes(_cachedAccountId, masterDataId).then(function (rejectScenes) {
                if (rejectScenes.length > 0) {
                    applyAutoRejectScenes(formContext, rejectScenes);
                    updateCustomerRejectFlag(masterDataId, true);
                }
                // 客户选择完成后刷新只读状态
                lockBpfFields(formContext);
            });
        });
    }

    // 标记是否正在执行手动保存，避免异步校验后再次触发 onSave 造成循环
    var _isManualSaving = false;

    // 标记是否正在 BPF 推进过程中，避免 onSave 中的异步 preventDefault 阻断 moveNext
    var _isBpfMoving = false;

    // 缓存当前客户的真实 Account ID 及系统自动判定的不予授信场景
    var _cachedAccountId = null;
    var _cachedRejectScenes = [];

    // 新建时是否存在生效且处于有效期内的模型版本（false 时阻止新建保存）
    // 默认 true：检测未完成或查询失败时不阻止用户，由后端 Plugin 兑底拦截
    var _hasActiveModelVersion = true;

    /**
     * 表单保存前校验
     * 仅保留同步校验（客户编码必填）。
     * 
     * 重复记录校验已前移到 PreStageChange（授信校验→模型计算时执行），
     * 避免 onSave 中调用 preventDefault 阻断 BPF 阶段切换保存。
     * D365 官方建议：BPF 相关的业务校验应放在 onPreStageChange 中处理。
     */
    self.onSave = function (executionContext) {
        var formContext = executionContext.getFormContext();
        var eventArgs = executionContext.getEventArgs();

        // 手动保存触发时不再拦截，避免循环
        if (_isManualSaving) return;

        // BPF 推进过程中触发的保存，跳过 onSave 校验。
        if (_isBpfMoving) return;

        // 自动保存/刷新保存不做校验
        if (eventArgs.getSaveMode() === 2 || eventArgs.getSaveMode() === 70) return;

        // 同步校验：新建时若无生效且在有效期内的模型版本，阻止保存
        if (formContext.ui.getFormType() === 1 && !_hasActiveModelVersion) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FcaProc_NoActiveModelVersion", "未找到生效且处于有效期内的模型版本，请先维护【授信模型版本】。") });
            return;
        }

        // 同步校验：客户编码必填
        if (!validateAccountRequired(formContext, eventArgs)) return;

        // 同步校验：若系统已判定场景 3/4，自动覆盖勾选并不再允许用户取消
        if (_cachedRejectScenes && _cachedRejectScenes.length > 0) {
            applyAutoRejectScenes(formContext, _cachedRejectScenes);
            var accountAttr = formContext.getAttribute("mcs_accountid");
            if (accountAttr && accountAttr.getValue() && accountAttr.getValue().length > 0) {
                var masterDataId = accountAttr.getValue()[0].id.replace(/[{}]/g, "");
                updateCustomerRejectFlag(masterDataId, true);
            }
        }

        // 异步校验：新建保存时同一客户仅允许一条未生效记录（禅道 #1644，兜底阻断）
        if (formContext.ui.getFormType() === 1) {
            blockSaveIfInFlightExists(formContext, eventArgs);
        }
    };

    // ==================== 默认值设置 ====================

    function setDefaultValues(formContext) {
        // 计算状态 = 授信校验
        setPicklistIfNull(formContext, "mcs_status", STATUS_CREDIT_CHECK);

        // 计算日期 = 当前时间
        var validFromAttr = formContext.getAttribute("mcs_validfrom");
        if (validFromAttr && validFromAttr.getValue() === null) {
            validFromAttr.setValue(new Date());
        }

        // 模型计算额度、调整后额度默认 0
        setMoneyIfNull(formContext, "mcs_modelgrant", 0);
        setMoneyIfNull(formContext, "mcs_initigrant", 0);

        // 事业部、归属组织从当前系统用户带出
        loadCurrentUserOrgInfo(formContext);
    }

    /**
     * 从当前系统用户带出事业部/归属组织名称
     * 取数链路（按 PRD）: systemuser -> mcs_useraccount -> mcs_org -> mcs_bu
     * 表单上可见字段为 mcs_buid（事业部）/ mcs_orgid（归属组织），均回填名称
     */
    function loadCurrentUserOrgInfo(formContext) {
        try {
            var globalContext = Xrm.Utility.getGlobalContext();
            var userId = globalContext.userSettings.userId;
            if (!userId) {
                console.warn("loadCurrentUserOrgInfo: 无法获取当前用户ID");
                return;
            }

            userId = userId.replace(/[{}]/g, "");
            console.log("loadCurrentUserOrgInfo: 当前用户ID = " + userId);

            // 若已存在值，不再覆盖
            if (getStringValue(formContext, "mcs_buid") || getStringValue(formContext, "mcs_orgid")) {
                console.log("loadCurrentUserOrgInfo: 字段已有值，跳过");
                return;
            }

            // 第一步：查询 mcs_useraccount，获取 mcs_orgid
            var filter = "_mcs_systemuserid_value eq " + userId + " and statecode eq 0";
            var query = "?$select=mcs_useraccountid,_mcs_orgid_value&$filter=" + filter + "&$top=1";

            Xrm.WebApi.retrieveMultipleRecords("mcs_useraccount", query).then(
                function (uaResult) {
                    if (!uaResult.entities || uaResult.entities.length === 0) {
                        console.warn("loadCurrentUserOrgInfo: 未找到当前用户对应的 mcs_useraccount 记录");
                        return;
                    }

                    var ua = uaResult.entities[0];
                    var orgId = getLookupGuidFromResult(ua, "mcs_orgid");
                    console.log("loadCurrentUserOrgInfo: mcs_useraccount orgId = " + orgId);

                    if (!orgId) {
                        console.warn("loadCurrentUserOrgInfo: mcs_useraccount.mcs_orgid 为空");
                        return;
                    }

                    // 第二步：查询 mcs_org，获取名称和 mcs_buid
                    Xrm.WebApi.retrieveRecord("mcs_org", orgId, "?$select=mcs_name,mcs_buid").then(
                        function (orgResult) {
                            var orgName = orgResult.mcs_name || "";
                            var buId = getLookupGuidFromResult(orgResult, "mcs_buid");
                            console.log("loadCurrentUserOrgInfo: orgName = " + orgName + ", buId = " + buId);

                            if (!buId) {
                                // 组织存在但事业部为空时，仍然回填组织
                                setOrgValues(formContext, orgName, orgName);
                                console.warn("loadCurrentUserOrgInfo: mcs_org.mcs_buid 为空，仅回填归属组织");
                                return;
                            }

                            // 第三步：查询 mcs_bu，获取名称
                            Xrm.WebApi.retrieveRecord("mcs_bu", buId, "?$select=mcs_name").then(
                                function (buResult) {
                                    var buName = buResult.mcs_name || "";
                                    console.log("loadCurrentUserOrgInfo: buName = " + buName);
                                    setOrgValues(formContext, orgName, buName);
                                },
                                function (error) {
                                    console.error("loadCurrentUserOrgInfo: 查询 mcs_bu 失败:", error);
                                    // 查询 bu 失败时仍回填组织
                                    setOrgValues(formContext, orgName, orgName);
                                }
                            );
                        },
                        function (error) {
                            console.error("loadCurrentUserOrgInfo: 查询 mcs_org 失败:", error);
                        }
                    );
                },
                function (error) {
                    console.error("loadCurrentUserOrgInfo: 查询 mcs_useraccount 失败:", error);
                }
            );
        } catch (ex) {
            console.error("loadCurrentUserOrgInfo 异常:", ex);
        }
    }

    /**
     * 回填事业部/归属组织四个字段
     */
    function setOrgValues(formContext, orgName, buName) {
        // 表单上可见的是 mcs_buid / mcs_orgid
        setStringValue(formContext, "mcs_buid", buName);
        setStringValue(formContext, "mcs_orgid", orgName);
        // 名称字段同步回填
        setStringValue(formContext, "mcs_buname", buName);
        setStringValue(formContext, "mcs_orgname", orgName);
    }

    function getStringValue(formContext, field) {
        var attr = formContext.getAttribute(field);
        if (!attr) return null;
        var value = attr.getValue();
        return value === null || value === "" ? null : value;
    }

    /**
     * 从 Web API retrieveRecord 结果中提取 Lookup GUID
     * 兼容 {id,name} / string / _field_value 三种返回格式
     */
    function getLookupGuidFromResult(result, field) {
        var val = result[field];
        if (val) {
            if (typeof val === "string") return val.replace(/[{}]/g, "");
            if (val.id) return val.id.replace(/[{}]/g, "");
        }
        var altField = "_" + field + "_value";
        if (result[altField]) return result[altField].replace(/[{}]/g, "");
        return null;
    }

    function setStringValue(formContext, field, value) {
        var attr = formContext.getAttribute(field);
        if (!attr) {
            console.warn("setStringValue: 字段不存在 " + field);
            return;
        }

        // 字段最大长度（按 DEV1 元数据）
        var maxLengthMap = {
            "mcs_buid": 20,
            "mcs_orgid": 20,
            "mcs_buname": 100,
            "mcs_orgname": 100
        };
        var maxLength = maxLengthMap[field];
        if (maxLength && value && value.length > maxLength) {
            value = value.substring(0, maxLength);
            console.warn("setStringValue: " + field + " 值超长，已截断至 " + maxLength + " 字符");
        }

        // 禁用字段仍需随表单提交
        attr.setSubmitMode("always");

        var currentValue = attr.getValue();
        if (currentValue === null || currentValue === "") {
            attr.setValue(value);
            console.log("setStringValue: " + field + " = " + value);
        } else {
            console.log("setStringValue: " + field + " 已有值，跳过");
        }
    }

    function setPicklistIfNull(formContext, field, value) {
        var attr = formContext.getAttribute(field);
        if (attr && attr.getValue() === null) {
            attr.setValue(value);
        }
    }

    function setMoneyIfNull(formContext, field, value) {
        var attr = formContext.getAttribute(field);
        if (attr && attr.getValue() === null) {
            attr.setValue(value);
        }
    }

    /**
     * 将 BPF 侧窗格中的字段设为只读
     * BPF 设计器本身没有 Read-only 选项，通过官方 Client API 在运行时锁定
     * 参考：mcs_credit_record.js 的 lockBpfFields 实现
     */
    function lockBpfFields(formContext) {
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
                    console.log("lockBpfFields: disabled " + name);
                }
            });
        } catch (ex) {
            console.error("lockBpfFields 设置 BPF 字段只读失败:", ex);
        }
    }

    // 记录因“生效启用”被本脚本禁用的控件名，退回时仅解锁这些控件，
    // 避免把表单设计器中本身只读的字段（如编辑人员/编辑日期）误放开
    var _disabledByStatus = [];

    /**
     * 计算状态 = 生效启用(3) 时，将表单所有字段设为只读；
     * 退回（状态变更为退回计算等）时解锁本脚本禁用的字段。
     * BPF 侧窗格字段（header_process_ 前缀）由 lockBpfFields 统一锁定，此处跳过。
     */
    function applyReadOnlyByStatus(formContext) {
        if (!formContext || !formContext.ui || !formContext.ui.controls) {
            return;
        }

        try {
            var statusAttr = formContext.getAttribute("mcs_status");
            var isActive = statusAttr && statusAttr.getValue() === STATUS_ACTIVE;

            if (isActive) {
                formContext.ui.controls.forEach(function (control) {
                    if (!control || !control.getName || !control.setDisabled || !control.getDisabled) return;

                    var name = control.getName();
                    if (name.indexOf("header_process_") === 0) return;

                    if (!control.getDisabled()) {
                        control.setDisabled(true);
                        _disabledByStatus.push(name);
                        console.log("applyReadOnlyByStatus: disabled " + name);
                    }
                });
            } else if (_disabledByStatus.length > 0) {
                _disabledByStatus.forEach(function (name) {
                    var control = formContext.getControl(name);
                    if (control && control.setDisabled) {
                        control.setDisabled(false);
                        console.log("applyReadOnlyByStatus: enabled " + name);
                    }
                });
                _disabledByStatus = [];
            }
        } catch (ex) {
            console.error("applyReadOnlyByStatus 设置字段只读失败:", ex);
        }
    }

    /**
     * 不予授信字段阶段锁定（#1307 衍生：授信校验阶段已过后不允许再勾选）
     * 仅 BPF 活动阶段 = 授信校验 时允许编辑；进入模型计算及以后（含退回计算）锁定。
     * 须在 applyReadOnlyByStatus 之后调用，避免其退回解锁时把本字段误放开。
     */
    function applyCreditRejectLock(formContext) {
        try {
            var control = formContext.getControl("mcs_creditreject");
            if (!control || !control.setDisabled) return;

            var stage = null;
            if (formContext.data && formContext.data.process && formContext.data.process.getActiveStage) {
                stage = formContext.data.process.getActiveStage();
            }
            var stageId = stage ? stage.getId().replace(/[{}]/g, "").toLowerCase() : "";
            // 无 BPF（或读不到阶段）时兑底按计算状态判断：仅授信校验(1)可编辑
            var editable;
            if (stageId) {
                editable = (stageId === STAGE_ID_CREDIT_CHECK);
            } else {
                var statusAttr = formContext.getAttribute("mcs_status");
                editable = !statusAttr || statusAttr.getValue() === STATUS_CREDIT_CHECK;
            }
            control.setDisabled(!editable);
            console.log("applyCreditRejectLock: " + (editable ? "可编辑" : "已锁定"));
        } catch (ex) {
            console.error("applyCreditRejectLock 设置只读失败:", ex);
        }
    }

    // ==================== 保存前校验 ====================

    /**
     * 校验客户编码必填
     */
    function validateAccountRequired(formContext, eventArgs) {
        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (!accountAttr || accountAttr.getValue() === null) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FcaProc_CustCodeRequired", "客户编码必填。") });
            return false;
        }
        return true;
    }

    /**
     * 异步校验：重复记录
     * 返回 Promise<boolean>：true=校验通过，false=校验不通过
     */
    function validateDuplicateAsync(formContext) {
        return new Promise(function (resolve) {
            var accountAttr = formContext.getAttribute("mcs_accountid");
            if (!accountAttr || accountAttr.getValue() === null) {
                resolve(true);
                return;
            }

            var accountId = accountAttr.getValue()[0].id.replace(/[{}]/g, "");
            var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");

            // 校验重复：相同客户存在 计算状态 != 生效启用(3) 的记录
            var duplicateFilter = "_mcs_accountid_value eq " + accountId +
                " and mcs_status ne " + STATUS_ACTIVE +
                (recordId ? " and mcs_fca_procid ne " + recordId : "");

            Xrm.WebApi.retrieveMultipleRecords("mcs_fca_proc", "?$select=mcs_fca_procid&$filter=" + duplicateFilter).then(
                function (result) {
                    if (result.entities.length > 0) {
                        Xrm.Navigation.openAlertDialog({ text: t("FcaProc_DuplicateRecord", "存在有重复记录，需核查！该客户已有未生效启用的授信模型计算记录。") });
                        resolve(false);
                        return;
                    }
                    resolve(true);
                },
                function (error) {
                    console.error("校验重复记录失败:", error);
                    resolve(false);
                }
            );
        });
    }

    // ==================== 未生效记录唯一性校验（禅道 #1644） ====================

    // 重复记录校验状态标志（防止重复点击保存导致多次查询 / 异步校验通过后放行重存）
    var _duplicateCheckInProgress = false;
    var _duplicateCheckPassed = false;

    /**
     * 查询同客户是否存在未生效（计算状态 ≠ 3 生效启用）模型计算记录，返回第一条或 null（禅道 #1644）
     */
    function queryInFlightRecord(accountGuid) {
        var filter = "_mcs_accountid_value eq " + accountGuid +
            " and mcs_status ne " + STATUS_ACTIVE +
            " and statecode eq 0";

        return Xrm.WebApi.retrieveMultipleRecords("mcs_fca_proc", "?$select=mcs_doid&$filter=" + encodeURIComponent(filter) + "&$top=1")
            .then(function (result) {
                return result.entities.length > 0 ? result.entities[0] : null;
            });
    }

    /**
     * 新建表单客户变更检测（禅道 #1644）
     * 同一客户只允许存在一条未生效模型计算记录：
     * 检测到已存在时提示，确认后跳转到已存在记录（当前新建表单不保存）；取消则继续编辑，保存时由 onSave 兜底阻断
     */
    function checkInFlightRecordOnCreate(formContext) {
        if (formContext.ui.getFormType() !== 1) return;

        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (!accountAttr) return;
        var accountValue = accountAttr.getValue();
        if (!accountValue || accountValue.length === 0) return;

        var accountGuid = accountValue[0].id.replace(/[{}]/g, "");
        queryInFlightRecord(accountGuid).then(function (record) {
            if (!record) return;
            Xrm.Navigation.openConfirmDialog({
                text: t("FcaProc_InFlightExistsOpen", "检测到该客户下有一条正在编辑中的数据（{0}），是否需要为你打开？").replace("{0}", record.mcs_doid || "")
            }).then(function (result) {
                if (result && result.confirmed) {
                    // 确认：跳转到已存在记录，当前新建表单不保存
                    Xrm.Navigation.navigateTo({
                        pageType: "entityrecord",
                        entityName: "mcs_fca_proc",
                        entityId: record.mcs_fca_procid
                    });
                }
                // 取消：不做任何操作，保存时会再次校验并阻断
            });
        }).catch(function (error) {
            console.error("检测未生效模型计算记录失败:", error);
        });
    }

    /**
     * 新建保存时兜底阻断（禅道 #1644）
     * 同客户已存在未生效记录时不允许保存；无重复则标记通过后重新触发保存
     */
    function blockSaveIfInFlightExists(formContext, eventArgs) {
        // 已通过重复记录校验，放行本次保存
        if (_duplicateCheckPassed) {
            _duplicateCheckPassed = false;
            return;
        }

        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (!accountAttr || accountAttr.getValue() === null || accountAttr.getValue().length === 0) {
            return; // 客户必填由 validateAccountRequired 处理
        }

        // 防止重复点击保存导致多次查询
        if (_duplicateCheckInProgress) {
            eventArgs.preventDefault();
            return;
        }

        _duplicateCheckInProgress = true;
        eventArgs.preventDefault();

        var accountGuid = accountAttr.getValue()[0].id.replace(/[{}]/g, "");

        queryInFlightRecord(accountGuid)
            .then(function (record) {
                _duplicateCheckInProgress = false;
                if (record) {
                    Xrm.Navigation.openAlertDialog({ text: t("FcaProc_InFlightExistsBlock", "该客户下已存在一条未生效的厂端授信模型计算记录（{0}），不允许保存，请打开已有记录继续编辑。").replace("{0}", record.mcs_doid || "") });
                } else {
                    // 没有重复，标记通过后重新触发保存
                    _duplicateCheckPassed = true;
                    formContext.data.save().then(
                        function () { _duplicateCheckPassed = false; },
                        function (error) {
                            _duplicateCheckPassed = false;
                            console.error("保存失败:", error);
                        }
                    );
                }
            })
            .catch(function (error) {
                _duplicateCheckInProgress = false;
                _duplicateCheckPassed = false;
                console.error("查询重复模型计算记录失败:", error);
                Xrm.Navigation.openAlertDialog({ text: t("FcaProc_InFlightCheckFailed", "校验重复记录失败：") + (error.message || JSON.stringify(error)) });
            });
    }

    // ==================== BPF 阶段切换事件 ====================

    /**
     * BPF 阶段切换前事件
     * 方向 Next：按当前阶段做业务校验
     * 方向 Previous：允许退回，但生效启用阶段退回时更新状态为“退回计算”
     */
    function onPreStageChange(eventContext, formContext) {
        // BPF 推进过程中（moveNext 触发的新 PreStageChange）直接放行，避免递归循环
        if (_isBpfMoving) {
            console.log("BPF 推进中，跳过本次 PreStageChange 业务校验");
            return;
        }

        var stage = eventContext.getEventArgs().getStage();
        var direction = eventContext.getEventArgs().getDirection();

        if (direction === "Next") {
            handleNextStage(eventContext, formContext, stage);
        } else if (direction === "Previous") {
            handlePreviousStage(eventContext, formContext, stage);
        }
    }

    /**
     * 处理 BPF “下一步”
     * 注意：PreStageChange 中 getStage() 返回的是目标阶段，不是当前阶段
     * 所以按目标阶段判断当前实际要从哪个阶段切换出去
     */
    function handleNextStage(eventContext, formContext, targetStage) {
        var stageId = targetStage.getId().replace(/[{}]/g, "").toLowerCase();

        if (stageId === STAGE_ID_MODEL_CALC) {
            // 目标阶段 = 模型计算，说明当前在授信校验 → 模型计算
            handleCreditCheckNext(eventContext, formContext);
        } else if (stageId === STAGE_ID_ACTIVE) {
            // 目标阶段 = 生效启用，说明当前在模型计算 → 生效启用
            handleModelCalcNext(eventContext, formContext);
        }
    }

    /**
     * 授信校验阶段点击“下一步”
     * 1. 客户编码必填
     * 2. 若用户未选任何场景，系统自动校验场景3/4并勾选（覆盖人工选择）
     * 3. 若判定不予授信（人工勾选任一场景 1/2/3/4，或系统命中 3/4），阻止推进并更新客户主数据【不予授信客户】=1
     * 4. 否则校验重复记录、自动加载最新生效模型版本，更新 mcs_status=2，推进到模型计算
     */
    function handleCreditCheckNext(eventContext, formContext) {
        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (!accountAttr || accountAttr.getValue() === null) {
            eventContext.getEventArgs().preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FcaProc_CustCodeRequired", "客户编码必填。") });
            return;
        }

        var masterDataId = accountAttr.getValue()[0].id.replace(/[{}]/g, "");
        var rejectAttr = formContext.getAttribute("mcs_creditreject");
        var selectedValues = rejectAttr ? rejectAttr.getValue() : [];

        // 若已手动选择任一场景（1/2/3/4），直接判定为不予授信，阻止推进（禅道 #1307：1/2 也需拦截）
        if (selectedValues && selectedValues.length > 0) {
            blockAndUpdateCustomerReject(eventContext, formContext, masterDataId);
            return;
        }

        // 未选任何场景，需要自动校验 3/4
        eventContext.getEventArgs().preventDefault(); // 先阻止，等异步校验完成后再决定是否推进

        var checkAccountId = _cachedAccountId || masterDataId;
        checkAutoRejectScenes(checkAccountId, masterDataId).then(function (rejectScenes) {
            if (rejectScenes.length > 0) {
                // 系统自动判定优先，覆盖人工选择
                applyAutoRejectScenes(formContext, rejectScenes);
                updateCustomerRejectFlag(masterDataId, true);
                Xrm.Navigation.openAlertDialog({ text: t("FcaProc_NoCreditAutoCheck", "该客户满足不予授信条件，已自动勾选相应场景，停留在授信校验阶段。") });
            } else {
                // BPF 推进时 onSave 会跳过重复记录校验，故在阶段切换前完成该校验
                validateDuplicateAsync(formContext).then(function (isValid) {
                    if (!isValid) return;

                    // 自动加载最新生效模型版本，然后推进到模型计算
                    loadLatestModelVersion(formContext).then(function (loaded) {
                        if (loaded) {
                            formContext.getAttribute("mcs_status").setValue(STATUS_MODEL_CALC);
                            moveToNextStage(formContext);
                        } else {
                            Xrm.Navigation.openAlertDialog({ text: t("FcaProc_NoActiveModelVersion", "未找到生效且处于有效期内的模型版本，请先维护【授信模型版本】。") });
                        }
                    }, function (error) {
                        console.error("自动加载模型版本失败:", error);
                    });
                });
            }
        }, function (error) {
            console.error("自动校验不予授信场景失败:", error);
        });
    }

    /**
     * 模型计算阶段点击“下一步”
     * 校验模型版本已自动加载、计算额度已生成且大于 0，然后更新 mcs_status=3
     */
    function handleModelCalcNext(eventContext, formContext) {
        // 兑底校验：已选不予授信场景的记录不允许生效启用（正常已被拦截/锁定在授信校验阶段，此处防在途记录/API 绕过）
        var rejectAttr = formContext.getAttribute("mcs_creditreject");
        var rejectValues = rejectAttr ? rejectAttr.getValue() : null;
        if (rejectValues && rejectValues.length > 0) {
            eventContext.getEventArgs().preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FcaProc_CustomerCreditDenied", "该客户已被判定为不予授信，停留在授信校验阶段。") });
            return;
        }

        var versionAttr = formContext.getAttribute("mcs_versionid");
        var modelGrantAttr = formContext.getAttribute("mcs_modelgrant");
        var initGrantAttr = formContext.getAttribute("mcs_initigrant");

        // 模型版本由系统在授信校验→模型计算时自动加载，此处仅做兜底校验
        if (!versionAttr || versionAttr.getValue() === null) {
            eventContext.getEventArgs().preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FcaProc_ModelVersionNotLoaded", "模型版本未自动加载，请退回【授信校验】后重新进入【模型计算】。") });
            return;
        }

        var modelGrant = modelGrantAttr ? modelGrantAttr.getValue() : null;
        var initGrant = initGrantAttr ? initGrantAttr.getValue() : null;
        if (modelGrant === null || initGrant === null || initGrant < 0) {
            eventContext.getEventArgs().preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FcaProc_AdjustModelQuotaPositive", "调整模型额度不能小于 0，无法生效启用。请检查模型计算结果或测试数据。") });
            return;
        }

        // 更新状态为生效启用
        formContext.getAttribute("mcs_status").setValue(STATUS_ACTIVE);
    }

    /**
     * 处理 BPF “上一步”
     * 注意：PreStageChange 中 getStage() 返回的是目标阶段
     * 目标阶段 = 模型计算，说明当前在生效启用 → 模型计算，更新状态为退回计算
     */
    function handlePreviousStage(eventContext, formContext, targetStage) {
        var stageId = targetStage.getId().replace(/[{}]/g, "").toLowerCase();
        if (stageId === STAGE_ID_MODEL_CALC) {
            formContext.getAttribute("mcs_status").setValue(STATUS_RETURN);
        }
    }

    /**
     * 新建表单检测：无生效且处于有效期内的模型版本时提示并阻止保存
     * 过滤条件与 loadLatestModelVersion 一致（生效 + 开始日期 <= 当前 <= 结束日期）
     */
    function checkActiveModelVersionOnCreate() {
        var now = new Date().toISOString();
        var filter = "mcs_isactive eq 1" +
            " and mcs_validfrom le " + now +
            " and mcs_validend ge " + now;

        Xrm.WebApi.retrieveMultipleRecords("mcs_fca_mdlversion", "?$select=mcs_fca_mdlversionid&$filter=" + filter + "&$top=1").then(
            function (result) {
                if (result.entities.length === 0) {
                    _hasActiveModelVersion = false;
                    Xrm.Navigation.openAlertDialog({ text: t("FcaProc_NoActiveModelVersion", "未找到生效且处于有效期内的模型版本，请先维护【授信模型版本】。") });
                }
            },
            function (error) {
                // 查询失败不阻止用户操作，由后端 Plugin 兑底拦截
                console.error("检测生效模型版本失败:", error);
            }
        );
    }

    /**
     * 自动加载最新生效且处于有效期内的模型版本
     * 返回 Promise<boolean>：true=成功加载或查询失败放行，false=未找到
     * 禅道 #1854：无模型版本读权限的角色查询会 403——放行推进，
     * 由后端 FcaProcCalculationPlugin（系统身份）兜底校验版本并给出明确报错
     */
    function loadLatestModelVersion(formContext) {
        return new Promise(function (resolve) {
            var versionAttr = formContext.getAttribute("mcs_versionid");
            if (!versionAttr) {
                resolve(false);
                return;
            }

            var now = new Date().toISOString();
            var filter = "mcs_isactive eq 1" +
                " and mcs_validfrom le " + now +
                " and mcs_validend ge " + now;

            Xrm.WebApi.retrieveMultipleRecords("mcs_fca_mdlversion", "?$select=mcs_fca_mdlversionid,mcs_versionid&$filter=" + filter + "&$orderby=createdon desc&$top=1").then(
                function (result) {
                    if (result.entities.length === 0) {
                        versionAttr.setValue(null);
                        resolve(false);
                        return;
                    }

                    var e = result.entities[0];
                    versionAttr.setValue([{
                        id: e.mcs_fca_mdlversionid,
                        name: e.mcs_versionid || "",
                        entityType: "mcs_fca_mdlversion"
                    }]);
                    resolve(true);
                },
                function (error) {
                    // #1854：查询失败（无读取权限/网络等）不阻断推进，后端插件系统身份兜底校验
                    console.warn("查询最新生效模型版本失败（可能无读取权限），放行由后端校验:", error);
                    resolve(true);
                }
            );
        });
    }

    // ==================== 业务辅助方法 ====================

    /**
     * 阻止推进并更新客户主数据不予授信标志
     */
    function blockAndUpdateCustomerReject(eventContext, formContext, accountId) {
        eventContext.getEventArgs().preventDefault();
        updateCustomerRejectFlag(accountId, true);
        Xrm.Navigation.openAlertDialog({ text: t("FcaProc_CustomerCreditDenied", "该客户已被判定为不予授信，停留在授信校验阶段。") });
    }

    /**
     * 自动校验场景 3 和 4
     * accountId：真实 Account ID（account.entityid），用于查询 mcs_outstanding
     * masterDataId：客户主数据 ID，用于查询黑名单
     * 返回 Promise，resolve 为需要自动勾选的场景值数组
     */
    function checkAutoRejectScenes(accountId, masterDataId) {
        var scenes = [];

        return new Promise(function (resolve, reject) {
            // 场景 4：黑名单客户（按客户主数据查询）
            var blacklistPromise;
            if (masterDataId) {
                blacklistPromise = Xrm.WebApi.retrieveRecord("mcs_customermasterdata", masterDataId, "?$select=mcs_blacklist").then(
                    function (result) {
                        if (result.mcs_blacklist === true || result.mcs_blacklist === 1) {
                            scenes.push(REJECT_SCENE_BLACKLIST);
                        }
                    },
                    function (error) {
                        console.error("查询黑名单失败:", error);
                    }
                );
            } else {
                blacklistPromise = Promise.resolve();
            }

            // 场景 3：逾期账龄>=6个月 且 逾期金额/在外货款余额 > 50%
            // 统一按 mcs_account（真实 Account ID）查询，与 Plugin 保持一致
            var overduePromise = Xrm.WebApi.retrieveMultipleRecords("mcs_outstanding", "?$select=mcs_overdurationdays,mcs_newoverdueamount,mcs_newremainingamount&$filter=_mcs_account_value eq " + accountId).then(
                function (result) {
                    var maxOverdueDays = 0;
                    var totalOverdueAmount = 0;
                    var totalOutstandingBalance = 0;

                    for (var i = 0; i < result.entities.length; i++) {
                        var e = result.entities[i];
                        var overdueDays = e.mcs_overdurationdays || 0;
                        if (overdueDays > maxOverdueDays) {
                            maxOverdueDays = overdueDays;
                        }
                        totalOverdueAmount += (e.mcs_newoverdueamount || 0);
                        totalOutstandingBalance += (e.mcs_newremainingamount || 0);
                    }

                    if (maxOverdueDays >= 180 && totalOutstandingBalance > 0 && (totalOverdueAmount / totalOutstandingBalance) > 0.5) {
                        scenes.push(REJECT_SCENE_OVERDUE);
                    }
                },
                function (error) {
                    console.error("查询逾期数据失败:", error);
                }
            );

            Promise.all([blacklistPromise, overduePromise]).then(function () {
                _cachedRejectScenes = scenes;
                resolve(scenes);
            }, function (error) {
                reject(error);
            });
        });
    }

    /**
     * 应用系统自动判定的不予授信场景，覆盖人工选择
     */
    function applyAutoRejectScenes(formContext, scenes) {
        var rejectAttr = formContext.getAttribute("mcs_creditreject");
        if (!rejectAttr) return;

        // 去重并保持数值类型
        var unique = [];
        for (var i = 0; i < scenes.length; i++) {
            if (unique.indexOf(scenes[i]) < 0) {
                unique.push(scenes[i]);
            }
        }
        rejectAttr.setValue(unique);
    }

    /**
     * 更新客户主数据【不予授信客户】标志
     */
    function updateCustomerRejectFlag(accountId, isReject) {
        var entity = {
            mcs_creditgrant: isReject ? true : false
        };
        Xrm.WebApi.updateRecord("mcs_customermasterdata", accountId, entity).then(
            function () {
                console.log("客户主数据不予授信标志已更新:", isReject);
            },
            function (error) {
                console.error("更新客户主数据不予授信标志失败:", error);
            }
        );
    }

    /**
     * 推进到 BPF 下一阶段
     * 注意：
     * 1. 在 PreStageChange 中 preventDefault 后，需要调用 moveNext 才能继续推进。
     * 2. 为避免 moveNext 再次触发 PreStageChange 导致递归，先临时移除事件处理器，
     *    推进完成后再重新注册。
     * 3. 在调用 moveNext 前先执行 formContext.data.save()，确保 mcs_status/mcs_versionid
     *    等字段变更已持久化，避免 moveNext 因 dirtyForm 返回失败。
     * 4. 保存前设置 _isBpfMoving=true，让 onSave 中的异步 preventDefault 跳过，
     *    否则 save() 的 Promise 会因 preventDefault 而被拒绝，moveNext 无法执行。
     */
    function moveToNextStage(formContext) {
        if (formContext.data && formContext.data.process) {
            var handler = self._preStageChangeHandler;
            if (handler) {
                formContext.data.process.removeOnPreStageChange(handler);
            }

            _isBpfMoving = true;
            console.log("BPF 推进中，先保存字段变更...");
            formContext.data.save().then(
                function () {
                    console.log("保存成功，调用 moveNext 推进 BPF");
                    formContext.data.process.moveNext(function (result) {
                        _isBpfMoving = false;
                        if (handler) {
                            formContext.data.process.addOnPreStageChange(handler);
                        }
                        if (result === "success") {
                            console.log("BPF 已成功推进到下一阶段");
                        } else {
                            console.error("BPF 推进失败:", result);
                        }
                    });
                },
                function (error) {
                    _isBpfMoving = false;
                    if (handler) {
                        formContext.data.process.addOnPreStageChange(handler);
                    }
                    console.error("保存失败，无法推进 BPF:", error);
                }
            );
        }
    }

})(FcaProcForm);
