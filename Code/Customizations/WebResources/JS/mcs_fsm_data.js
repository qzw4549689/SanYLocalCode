/**
 * 融资管理（mcs_fsm_data）表单脚本
 * 功能：BPP 立项审批 / 融资方案审批提交
 * 触发：表单命令栏【提交立项审批】【提交融资方案审批】App Action 按钮
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

var FsmDataForm = (function () {
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

    var self = {};

    // mcs_fsm_data.mcs_fsm_status 选项集值
    var FSM_STATUS = {
        DEMAND: 1,          // 融资需求
        INITIATION: 2,      // 融资立项
        SOLUTION: 3,        // 融资解决方案
        IMPLEMENTATION: 4   // 融资落实
    };

    // mcs_fsm_data.mcs_bppstatus 选项集值
    var BPP_STATUS = {
        APPLY: 1,       // 申请
        IN_REVIEW: 2,   // 审批中
        APPROVED: 3,    // 通过
        REJECTED: 4     // 驳回
    };

    // mcs_fsm_data.mcs_approve_type 选项集值
    var APPROVE_TYPE = {
        INITIATION: 1,  // 立项审批
        PROJECT: 2      // 融资方案审批
    };

    /**
     * 判断 BPP 状态是否为"进行中"
     */
    function isBppInProgress(bppStatusCode) {
        if (!bppStatusCode) {
            return false;
        }
        var status = ("" + bppStatusCode).toLowerCase();
        return status === "submitted" || status === "inreview" ||
               status === "pending" || status === "10" || status === "20";
    }

    /**
     * 提交 BPP 审批（公共逻辑）
     * @param {object} formContext 表单上下文
     * @param {number} approveType 审批类型（1 立项审批 / 2 融资方案审批）
     * @param {string} typeName 审批类型名称（用于提示）
     */
    function submitApproval(formContext, approveType, typeName) {
        // 记录必须已保存
        var recordId = formContext.data.entity.getId();
        if (!recordId) {
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_SaveBeforeSubmit", "请先保存记录后再提交审批。") });
            return;
        }
        recordId = recordId.replace(/[{}]/g, "");

        // 审批状态校验：已有审批在进行中则拦截（mcs_bppstatus 未上表单，回退读缓存）
        var currentBppStatus = getFieldValue(formContext, "mcs_bppstatus");
        if (currentBppStatus === BPP_STATUS.IN_REVIEW) {
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_BppInProgress", "已有审批在进行中，无法重复提交。") });
            return;
        }

        var bppStatusCodeAttr = formContext.getAttribute("mcs_bppstatuscode");
        var currentBppStatusCode = bppStatusCodeAttr ? bppStatusCodeAttr.getValue() : null;
        if (isBppInProgress(currentBppStatusCode)) {
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_BppInProgress", "已有审批在进行中，无法重复提交。") });
            return;
        }

        // 更新审批类型 + 审批状态为 2（审批中），触发后端 Plugin 调用 mcs_bppstartapi
        Xrm.WebApi.updateRecord("mcs_fsm_data", recordId, {
            "mcs_approve_type": approveType,
            "mcs_bppstatus": BPP_STATUS.IN_REVIEW
        }).then(
            function () {
                Xrm.Navigation.openAlertDialog({ text: typeName + t("FsmData_SubmittedSuffix", "已提交。") }).then(function () {
                    formContext.data.refresh(false);
                    refreshFieldCache(formContext);
                });
            },
            function (error) {
                console.error("提交" + typeName + "失败:", error);
                Xrm.Navigation.openAlertDialog({ text: t("FsmData_SubmitFailed", "提交{0}失败： ").replace("{0}", typeName) + (error.message || JSON.stringify(error)) });
            }
        );
    }

    // =====================================================================
    // 融资需求：级联带出 / 弹窗过滤 / 清空联动 / 保存校验（2026-07-24 Bug 修复）
    // =====================================================================

    // 三个来源字段（新字段，替代错关联的 mcs_lead_id / mcs_quote_id）
    var SRC = {
        LEAD: "mcs_leadmain_id",      // 线索编号 → mcs_leadmain
        QUOTER: "mcs_quoter_id",      // 报价单编号 → mcs_quoter
        CONTRACT: "mcs_contract_id"   // 合同编号 → mcs_contract
    };

    // 被级联带出管理的派生字段（清空联动时一并清空）
    var DERIVED_FIELDS = [
        "mcs_big_area",       // 大区（合同 mcs_region）
        "mcs_country_id",     // 国家
        "mcs_country_area",   // 国区（由国家经「大区-国区-地区」映射推导）
        "mcs_division_id",    // 事业部
        "mcs_customer_name",  // 客户名称
        "mcs_customer_id"     // 客户编码
    ];

    // 上次由代入逻辑写入的线索 ID（用于区分代入值与用户手工修改）
    var _lastDerivedLeadId = null;
    // 重复校验通过后放行一次保存
    var _saveApproved = false;
    // 已选线索关联的报价主表 ID 缓存（用于报价单弹窗扁平过滤，避免 link-entity 查询生成器错误 0x80041103）
    var _quoteMainIdsForLead = null;

    // 未上表单字段的服务端值缓存（onLoad/保存后刷新），供 getFieldValue 回退读取
    // 背景：mcs_bppstatus/mcs_approve_type/mcs_can_initiated/mcs_can_project 未放到表单上
    var _fieldCache = {};
    var CACHE_FIELDS = ["mcs_bppstatus", "mcs_approve_type", "mcs_can_initiated", "mcs_can_project"];

    function getLookup(formContext, fieldName) {
        var attr = formContext.getAttribute(fieldName);
        var v = attr ? attr.getValue() : null;
        return (v && v.length > 0) ? v[0] : null;
    }

    function setLookup(formContext, fieldName, id, entityType, name) {
        var attr = formContext.getAttribute(fieldName);
        if (!attr) return;
        if (!id) { attr.setValue(null); return; }
        attr.setValue([{ id: id, entityType: entityType, name: name || "" }]);
    }

    function setValue(formContext, fieldName, value) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) attr.setValue(value);
    }

    function formattedName(record, logicalName) {
        return record[logicalName + "@OData.Community.Display.V1.FormattedValue"] || "";
    }

    function retrieveSafe(entityName, id, select) {
        return Xrm.WebApi.retrieveRecord(entityName, id.replace(/[{}]/g, ""), select).then(
            function (r) { return r; },
            function (e) { console.warn("retrieveSafe 失败:", entityName, e); return null; }
        );
    }

    /**
     * 刷新未上表单字段的服务端值缓存
     */
    function refreshFieldCache(formContext) {
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        if (!recordId) return Promise.resolve();
        return Xrm.WebApi.retrieveRecord("mcs_fsm_data", recordId, "?$select=" + CACHE_FIELDS.join(",")).then(
            function (r) {
                CACHE_FIELDS.forEach(function (f) { _fieldCache[f] = r[f]; });
            },
            function (e) { console.warn("[FSM] 字段缓存刷新失败:", e); }
        );
    }

    /**
     * 读取字段值：表单上有则读表单（含未保存的最新值），否则读服务端缓存
     */
    function getFieldValue(formContext, fieldName) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) return attr.getValue();
        return (_fieldCache[fieldName] !== undefined) ? _fieldCache[fieldName] : null;
    }

    /**
     * 实时查询服务端字段值（提交审批等关键校验使用，避免缓存过期）
     */
    function getServerFieldValue(formContext, fieldName) {
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        if (!recordId) return Promise.resolve(getFieldValue(formContext, fieldName));
        return Xrm.WebApi.retrieveRecord("mcs_fsm_data", recordId, "?$select=" + fieldName).then(
            function (r) { return r[fieldName]; },
            function () { return getFieldValue(formContext, fieldName); }
        );
    }

    /**
     * 级联重算：任一来源字段变更后全量重算派生字段
     * 优先级：合同 > 报价单 > 线索；合同/报价单可向上代入线索
     */
    function recomputeDerived(formContext) {
        try {
        var leadRef = getLookup(formContext, SRC.LEAD);
        var quoterRef = getLookup(formContext, SRC.QUOTER);
        var contractRef = getLookup(formContext, SRC.CONTRACT);

        var pLead = leadRef ? retrieveSafe("mcs_leadmain", leadRef.id,
            "?$select=_mcs_countryid_value,_mcs_buid_value,_mcs_customermasterdataid_value,mcs_accountnumber") : Promise.resolve(null);
        var pQuoter = quoterRef ? retrieveSafe("mcs_quoter", quoterRef.id,
            "?$select=_mcs_countryid_value,_mcs_customermasterdataid_value,mcs_customercode,_mcs_quote_mainid_value") : Promise.resolve(null);
        var pContract = contractRef ? retrieveSafe("mcs_contract", contractRef.id,
            "?$select=_mcs_region_value,_mcs_country_value,_mcs_bu_value,_mcs_customermaster_value,_mcs_leadmain_value") : Promise.resolve(null);

        Promise.all([pLead, pQuoter, pContract]).then(function (results) {
            var lead = results[0], quoter = results[1], contract = results[2];

            var pQuoteMainLead = (quoter && quoter._mcs_quote_mainid_value)
                ? retrieveSafe("mcs_quote_main", quoter._mcs_quote_mainid_value, "?$select=_mcs_leadmainid_value")
                : Promise.resolve(null);

            return pQuoteMainLead.then(function (qm) {
                // ---- 1. 线索代入（合同 > 报价单） ----
                var derivedLeadId = (contract && contract._mcs_leadmain_value) ||
                                    (qm && qm._mcs_leadmainid_value) || null;
                var derivedLeadName = (contract && contract._mcs_leadmain_value) ? formattedName(contract, "_mcs_leadmain_value")
                                    : (qm && qm._mcs_leadmainid_value) ? formattedName(qm, "_mcs_leadmainid_value") : "";
                var leadChainDone;
                if (derivedLeadId) {
                    var curLeadId = leadRef ? leadRef.id.replace(/[{}]/g, "").toLowerCase() : null;
                    if (!curLeadId || curLeadId === (_lastDerivedLeadId || "").toLowerCase()) {
                        if (curLeadId !== derivedLeadId.toLowerCase()) {
                            setLookup(formContext, SRC.LEAD, derivedLeadId, "mcs_leadmain", derivedLeadName);
                        }
                        _lastDerivedLeadId = derivedLeadId;
                        leadChainDone = retrieveSafe("mcs_leadmain", derivedLeadId,
                            "?$select=_mcs_countryid_value,_mcs_buid_value,_mcs_customermasterdataid_value,mcs_accountnumber");
                    } else {
                        leadChainDone = Promise.resolve(lead); // 用户手工改过线索，保留
                    }
                } else {
                    if (leadRef && _lastDerivedLeadId &&
                        leadRef.id.replace(/[{}]/g, "").toLowerCase() === _lastDerivedLeadId.toLowerCase()) {
                        setLookup(formContext, SRC.LEAD, null); // 来源已清空，清掉代入值
                        lead = null;
                    }
                    _lastDerivedLeadId = null;
                    leadChainDone = Promise.resolve(lead);
                }

                return leadChainDone.then(function (effLead) {
                    // ---- 2. 派生字段全量重算 ----
                    DERIVED_FIELDS.forEach(function (f) { setValue(formContext, f, null); });

                    var customerId = null, customerType = "mcs_customermasterdata", customerName = "";

                    if (contract) {
                        if (contract._mcs_region_value) {
                            setLookup(formContext, "mcs_big_area", contract._mcs_region_value, "mcs_region", formattedName(contract, "_mcs_region_value"));
                        }
                        if (contract._mcs_country_value) {
                            setLookup(formContext, "mcs_country_id", contract._mcs_country_value, "mcs_country", formattedName(contract, "_mcs_country_value"));
                        }
                        if (contract._mcs_bu_value) {
                            setLookup(formContext, "mcs_division_id", contract._mcs_bu_value, "mcs_bu", formattedName(contract, "_mcs_bu_value"));
                        }
                        if (contract._mcs_customermaster_value) {
                            customerId = contract._mcs_customermaster_value;
                            customerName = formattedName(contract, "_mcs_customermaster_value");
                        }
                    }
                    if (!customerId && quoter && quoter._mcs_customermasterdataid_value) {
                        customerId = quoter._mcs_customermasterdataid_value;
                        customerName = formattedName(quoter, "_mcs_customermasterdataid_value");
                    }
                    if (!customerId && effLead && effLead._mcs_customermasterdataid_value) {
                        customerId = effLead._mcs_customermasterdataid_value;
                        customerName = formattedName(effLead, "_mcs_customermasterdataid_value");
                    }
                    if (!getLookup(formContext, "mcs_country_id")) {
                        var countryId = (quoter && quoter._mcs_countryid_value) || (effLead && effLead._mcs_countryid_value) || null;
                        var countryName = quoter && quoter._mcs_countryid_value ? formattedName(quoter, "_mcs_countryid_value")
                            : effLead ? formattedName(effLead, "_mcs_countryid_value") : "";
                        if (countryId) setLookup(formContext, "mcs_country_id", countryId, "mcs_country", countryName);
                    }
                    if (!getLookup(formContext, "mcs_division_id") && effLead && effLead._mcs_buid_value) {
                        setLookup(formContext, "mcs_division_id", effLead._mcs_buid_value, "mcs_bu", formattedName(effLead, "_mcs_buid_value"));
                    }
                    if (customerId) {
                        setLookup(formContext, "mcs_customer_name", customerId, customerType, customerName);
                    }

                    // ---- 3. 客户编码：客户主数据 sapnumber 优先，报价单/线索自带编码兜底 ----
                    var codePromise = customerId
                        ? retrieveSafe("mcs_customermasterdata", customerId, "?$select=mcs_sapnumber").then(function (cm) {
                            return (cm && cm.mcs_sapnumber) || null;
                        })
                        : Promise.resolve(null);
                    return codePromise.then(function (code) {
                        if (!code && quoter && quoter.mcs_customercode) code = quoter.mcs_customercode;
                        if (!code && effLead && effLead.mcs_accountnumber) code = effLead.mcs_accountnumber;
                        setValue(formContext, "mcs_customer_id", code || null);
                        // ---- 4. 国区：由国家经映射推导 ----
                        var countryRef = getLookup(formContext, "mcs_country_id");
                        if (countryRef) {
                            return deriveNationalRegion(formContext, countryRef.id);
                        }
                        setValue(formContext, "mcs_country_area", null);
                    });
                });
            });
        }).catch(function (e) {
            console.error("[FSM] 融资需求级联带出异常:", e);
        });
        } catch (ex) {
            console.error("[FSM] recomputeDerived 同步异常:", ex);
        }
    }

    /**
     * 国区推导：国家（mcs_countrycode）→ mcs_bunationalcountry（大区-国区-地区映射）→ mcs_nationalregion
     */
    function deriveNationalRegion(formContext, countryId) {
        return retrieveSafe("mcs_country", countryId, "?$select=mcs_countrycode").then(function (country) {
            if (!country || !country.mcs_countrycode) { setValue(formContext, "mcs_country_area", null); return; }
            var q = "?$select=mcs_nationalregioncode&$top=1&$filter=mcs_countryareacode eq '" + country.mcs_countrycode + "' and statecode eq 0";
            return Xrm.WebApi.retrieveMultipleRecords("mcs_bunationalcountry", q).then(function (mapResult) {
                if (mapResult.entities.length === 0) { setValue(formContext, "mcs_country_area", null); return; }
                var nrCode = mapResult.entities[0].mcs_nationalregioncode;
                var q2 = "?$select=mcs_nationalregionid,mcs_name&$top=1&$filter=mcs_nationalregioncode eq '" + nrCode + "' and statecode eq 0";
                return Xrm.WebApi.retrieveMultipleRecords("mcs_nationalregion", q2).then(function (nrResult) {
                    if (nrResult.entities.length > 0) {
                        var nr = nrResult.entities[0];
                        setLookup(formContext, "mcs_country_area", nr.mcs_nationalregionid, "mcs_nationalregion", nr.mcs_name);
                    } else {
                        setValue(formContext, "mcs_country_area", null);
                    }
                });
            });
        }).catch(function (e) {
            console.error("[FSM] 国区推导异常:", e);
        });
    }

    /**
     * 刷新已选线索关联的报价主表 ID 缓存
     */
    function refreshQuoteMainCache(formContext) {
        var leadRef = getLookup(formContext, SRC.LEAD);
        if (!leadRef) { _quoteMainIdsForLead = null; return Promise.resolve(); }
        var leadId = leadRef.id.replace(/[{}]/g, "");
        var q = "?$select=mcs_quote_mainid&$filter=_mcs_leadmainid_value eq " + leadId;
        return Xrm.WebApi.retrieveMultipleRecords("mcs_quote_main", q).then(function (result) {
            _quoteMainIdsForLead = result.entities.map(function (e) { return e.mcs_quote_mainid; });
        }, function (e) {
            console.error("[FSM] 报价主表缓存查询失败:", e);
            _quoteMainIdsForLead = null;
        });
    }

    /**
     * 报价单弹窗按已选线索过滤（扁平 in 条件：mcs_quote_mainid in 该线索的报价主表）
     */
    function filterQuoterByLead(formContext) {
        var leadRef = getLookup(formContext, SRC.LEAD);
        if (!leadRef) return;
        var fetchXml;
        if (_quoteMainIdsForLead && _quoteMainIdsForLead.length > 0) {
            var values = _quoteMainIdsForLead.map(function (id) { return "<value>" + id + "</value>"; }).join("");
            fetchXml = "<fetch><entity name='mcs_quoter'>" +
                "<filter type='and'><condition attribute='mcs_quote_mainid' operator='in'>" + values + "</condition></filter>" +
                "</entity></fetch>";
        } else {
            // 缓存未就绪或该线索无报价主表：显示空结果
            fetchXml = "<fetch><entity name='mcs_quoter'>" +
                "<filter type='and'><condition attribute='mcs_quote_mainid' operator='eq' value='00000000-0000-0000-0000-000000000000' /></filter>" +
                "</entity></fetch>";
        }
        var ctrl = formContext.getControl(SRC.QUOTER);
        if (ctrl) ctrl.addCustomFilter(fetchXml, "mcs_quoter");
    }

    /**
     * 合同弹窗按已选线索过滤（contract.mcs_leadmain）
     */
    function filterContractByLead(formContext) {
        var leadRef = getLookup(formContext, SRC.LEAD);
        if (!leadRef) return;
        var leadId = leadRef.id.replace(/[{}]/g, "");
        var fetchXml = "<fetch><entity name='mcs_contract'>" +
            "<filter type='and'><condition attribute='mcs_leadmain' operator='eq' value='" + leadId + "' /></filter>" +
            "</entity></fetch>";
        var ctrl = formContext.getControl(SRC.CONTRACT);
        if (ctrl) ctrl.addCustomFilter(fetchXml, "mcs_contract");
    }

    /**
     * 重复性校验：线索/报价单/合同三者有一个相同存在即重复
     * @returns Promise<string|null> 重复记录的融资管理编号；无重复返回 null
     */
    function checkDuplicateSource(formContext) {
        var conditions = [];
        var leadRef = getLookup(formContext, SRC.LEAD);
        var quoterRef = getLookup(formContext, SRC.QUOTER);
        var contractRef = getLookup(formContext, SRC.CONTRACT);
        if (leadRef) conditions.push("_mcs_leadmain_id_value eq " + leadRef.id.replace(/[{}]/g, ""));
        if (quoterRef) conditions.push("_mcs_quoter_id_value eq " + quoterRef.id.replace(/[{}]/g, ""));
        if (contractRef) conditions.push("_mcs_contract_id_value eq " + contractRef.id.replace(/[{}]/g, ""));
        if (conditions.length === 0) return Promise.resolve(null);

        var filter = "(" + conditions.join(" or ") + ")";
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        if (recordId) filter += " and mcs_fsm_dataid ne " + recordId;

        return Xrm.WebApi.retrieveMultipleRecords("mcs_fsm_data",
            "?$select=mcs_fsm_no&$top=1&$filter=" + filter).then(
            function (result) {
                return (result.entities.length > 0) ? (result.entities[0].mcs_fsm_no || "") : null;
            },
            function (e) {
                console.error("重复性校验查询失败:", e);
                return null; // 查询失败不阻断保存
            });
    }

    /**
     * 保存校验：至少一个来源 + 重复性校验
     * 注意：表单挂有 BPF「融资管理」，BPF 阶段导航会触发平台内部保存，
     * 若 onSave 无条件 preventDefault 会被平台拦截报错（0x80060802 阻止保存窗体的 Web 资源）。
     * 因此仅当来源字段发生变更时才 preventDefault 做异步重复校验；其余保存直接放行。
     */
    function onSaveValidate(executionContext) {
        if (_saveApproved) { _saveApproved = false; return; }
        var formContext = executionContext.getFormContext();
        var eventArgs = executionContext.getEventArgs();

        var hasSource = getLookup(formContext, SRC.LEAD) || getLookup(formContext, SRC.QUOTER) || getLookup(formContext, SRC.CONTRACT);
        if (!hasSource) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_RequireOneSource", "线索编号、报价单编号、合同编号至少填写一个。") });
            return;
        }

        // 来源字段均未变更：跳过重复校验直接放行（避免拦截 BPF 阶段导航等系统保存）
        var sourceDirty = [SRC.LEAD, SRC.QUOTER, SRC.CONTRACT].some(function (f) {
            var attr = formContext.getAttribute(f);
            return attr && attr.getIsDirty();
        });
        if (!sourceDirty) return;

        eventArgs.preventDefault();
        checkDuplicateSource(formContext).then(function (dupNo) {
            if (dupNo) {
                Xrm.Navigation.openAlertDialog({ text: t("FsmData_DuplicateSource", "已存在相同线索/报价单/合同编号的融资管理记录：") + dupNo });
            } else {
                _saveApproved = true;
                formContext.data.entity.save();
            }
        });
    }

    /**
     * 通用必填校验（控件 label 取当前 UI 语言）
     * @returns 缺失字段 label 数组
     */
    function validateRequiredFields(formContext, fieldNames) {
        var missing = [];
        fieldNames.forEach(function (f) {
            var attr = formContext.getAttribute(f);
            var v = attr ? attr.getValue() : null;
            var empty = (v === null || v === undefined || v === "" || (Array.isArray(v) && v.length === 0));
            if (empty) {
                var ctrl = formContext.getControl(f);
                missing.push(ctrl ? ctrl.getLabel() : f);
            }
        });
        return missing;
    }

    // 融资六要素 + 融资需求管理阶段必填（提交立项审批时校验）
    var REQUIRED_SIX_ELEMENTS = [
        "mcs_fsm_amount", "mcs_fsm_currency", "mcs_fsm_period", "mcs_fsm_payment_ratio",
        "mcs_fsm_interest_rate", "mcs_fsm_product_desc", "mcs_fsm_device_count", "mcs_fsm_device_name"
    ];
    // 融资解决方案阶段追加必填（提交融资方案审批时校验，另含合同号必填）
    // 注：融资机构编号/名称按 PRD 截图为选填，不在本清单内
    var REQUIRED_SOLUTION_EXTRA = [
        "mcs_fsm_resource_product", "mcs_fsm_credit_amount",
        "mcs_fsm_credit_amount_usd", "mcs_fsm_interest_discount", "mcs_fsm_fee",
        "mcs_fsm_repurchase_conditions", "mcs_fsm_other_conditions"
    ];

    // =====================================================================
    // 按当前阶段（mcs_fsm_status）控制 tab 字段可写 + 必填（2026-07-25）
    // =====================================================================

    // 需求融资管理 tab（阶段 2 融资立项时可写 + 必填）
    var INITIATION_TAB = {
        stage: FSM_STATUS.INITIATION,
        required: ["mcs_fsm_manager", "mcs_fsm_is_initiated", "mcs_can_initiated"],
        optional: []
    };
    // 融资解决方案 tab（阶段 3 融资解决方案时可写 + 必填/选填）
    var SOLUTION_TAB = {
        stage: FSM_STATUS.SOLUTION,
        required: ["mcs_fsm_resource_product", "mcs_fsm_credit_amount", "mcs_fsm_credit_amount_usd",
                   "mcs_fsm_interest_discount", "mcs_fsm_fee", "mcs_fsm_repurchase_conditions",
                   "mcs_fsm_other_conditions", "mcs_can_project", "mcs_is_valid"],
        optional: ["mcs_fsm_resource_id", "mcs_fsm_resource_name"]
    };
    // 需求融资管理 tab 中元数据已必填的字段：只控禁用，不调 setRequiredLevel（避免与元数据必填冲突）
    var INITIATION_TAB_METADATA_REQUIRED = ["mcs_fsm_device_count", "mcs_fsm_device_name"];

    /**
     * 设置字段可写状态（遍历该字段所有控件）
     */
    function setFieldDisabled(formContext, fieldName, disabled) {
        var attr = formContext.getAttribute(fieldName);
        if (!attr) return;
        attr.controls.forEach(function (ctrl) { ctrl.setDisabled(disabled); });
    }

    /**
     * 设置字段必填级别（仅动态必填字段使用，元数据必填字段不调用）
     */
    function setFieldRequired(formContext, fieldName, required) {
        var attr = formContext.getAttribute(fieldName);
        if (attr) attr.setRequiredLevel(required ? "required" : "none");
    }

    // =====================================================================
    // BPF 阶段条集成：阶段来源 / 双向同步 / 侧窗格字段只读（2026-07-25）
    // =====================================================================

    // BPF 阶段名称关键字 → 阶段号（与 mcs_fsm_status 选项值一致）
    var BPF_STAGE_NAMES = [
        { keyword: "融资需求", stage: FSM_STATUS.DEMAND },
        { keyword: "融资立项", stage: FSM_STATUS.INITIATION },
        { keyword: "融资解决方案", stage: FSM_STATUS.SOLUTION },
        { keyword: "融资落实", stage: FSM_STATUS.IMPLEMENTATION }
    ];

    // 阶段双向同步重入保护
    var _syncingStage = false;

    function getStageNumberByName(stageName) {
        if (!stageName) return null;
        for (var i = 0; i < BPF_STAGE_NAMES.length; i++) {
            if (stageName.indexOf(BPF_STAGE_NAMES[i].keyword) >= 0) return BPF_STAGE_NAMES[i].stage;
        }
        return null;
    }

    function getBpfStageNumber(formContext) {
        try {
            var process = formContext.data && formContext.data.process;
            if (!process) return null;
            var activeStage = process.getActiveStage();
            return activeStage ? getStageNumberByName(activeStage.getName()) : null;
        } catch (e) {
            console.warn("[FSM] 读取 BPF 阶段失败:", e);
            return null;
        }
    }

    /**
     * 当前阶段：优先取 BPF 当前阶段（用户在阶段条上看到/操作的阶段），无 BPF 时回退到 mcs_fsm_status
     */
    function getCurrentStage(formContext) {
        var bpfNum = getBpfStageNumber(formContext);
        if (bpfNum) return bpfNum;
        var statusAttr = formContext.getAttribute("mcs_fsm_status");
        return (statusAttr && statusAttr.getValue()) ? statusAttr.getValue() : FSM_STATUS.DEMAND;
    }

    /**
     * BPF 阶段 → mcs_fsm_status：用户在阶段条上推进/回退时保持状态字段一致
     */
    function syncStatusFromBpf(formContext) {
        if (_syncingStage) return;
        var bpfNum = getBpfStageNumber(formContext);
        if (!bpfNum) return;
        var statusAttr = formContext.getAttribute("mcs_fsm_status");
        if (statusAttr && statusAttr.getValue() !== bpfNum) {
            _syncingStage = true;
            statusAttr.setValue(bpfNum);
            _syncingStage = false;
        }
    }

    /**
     * mcs_fsm_status → BPF 阶段：BPP 审批回调等服务端推进状态后，阶段条跟随
     */
    function syncBpfFromStatus(formContext) {
        if (_syncingStage) return;
        var process = formContext.data && formContext.data.process;
        if (!process) return;
        var statusAttr = formContext.getAttribute("mcs_fsm_status");
        var status = (statusAttr && statusAttr.getValue()) ? statusAttr.getValue() : FSM_STATUS.DEMAND;
        var bpfNum = getBpfStageNumber(formContext);
        if (bpfNum === status) return;
        try {
            var targetId = null;
            process.getActivePath().forEach(function (st) {
                if (getStageNumberByName(st.getName()) === status) targetId = st.getId();
            });
            if (!targetId) return;
            _syncingStage = true;
            process.setActiveStage(targetId, function () {
                _syncingStage = false;
                applyStageControl(formContext);
                lockBpfFields(formContext);
            });
        } catch (e) {
            _syncingStage = false;
            console.warn("[FSM] 同步 BPF 阶段失败:", e);
        }
    }

    /**
     * 打开表单时校正 BPF 阶段与状态字段的脱节：
     *  - 状态字段领先（如 BPP 审批回调服务端推进）→ 阶段条跟随状态
     *  - BPF 阶段领先（如旧数据用户在阶段条上已推进但状态未同步）→ 状态跟随 BPF（需保存生效）
     */
    function reconcileStagesOnLoad(formContext) {
        var bpfNum = getBpfStageNumber(formContext);
        if (!bpfNum) return;
        var statusAttr = formContext.getAttribute("mcs_fsm_status");
        var status = (statusAttr && statusAttr.getValue()) ? statusAttr.getValue() : FSM_STATUS.DEMAND;
        if (status > bpfNum) {
            syncBpfFromStatus(formContext);
        } else if (bpfNum > status) {
            syncStatusFromBpf(formContext);
        }
    }

    /**
     * 拦截 BPF 阶段前进：进入下一阶段前必须已通过对应阶段的审批
     *  - 进入融资解决方案(3)：需立项审批已通过（mcs_approve_type=1 且 mcs_bppstatus=3）
     *  - 进入融资落实(4)：需融资方案审批已通过（mcs_approve_type=2 且 mcs_bppstatus=3）
     * 回退不拦截；融资需求 → 融资立项 无前置审批，放行
     */
    function preventBpfNextWithoutApproval(formContext) {
        var process = formContext.data && formContext.data.process;
        if (!process) return;
        try {
            process.addOnPreStageChange(function (stageChangeContext) {
                var args = stageChangeContext.getEventArgs();
                if (!args || args.getDirection() !== "Next") return;
                var targetStage = args.getStage ? args.getStage() : null;
                var targetNum = targetStage ? getStageNumberByName(targetStage.getName()) : null;
                if (!targetNum) return;

                var approveType = getFieldValue(formContext, "mcs_approve_type");
                var bppStatus = getFieldValue(formContext, "mcs_bppstatus");

                var initiationPassed = (approveType === APPROVE_TYPE.INITIATION && bppStatus === BPP_STATUS.APPROVED);
                var projectPassed = (approveType === APPROVE_TYPE.PROJECT && bppStatus === BPP_STATUS.APPROVED);

                if (targetNum >= FSM_STATUS.SOLUTION && !initiationPassed) {
                    args.preventDefault();
                    Xrm.Navigation.openAlertDialog({ text: t("FsmData_NeedInitiationApproval", "请先提交立项审批并通过后，才能进入融资解决方案阶段。") });
                    return;
                }
                if (targetNum >= FSM_STATUS.IMPLEMENTATION && !projectPassed) {
                    args.preventDefault();
                    Xrm.Navigation.openAlertDialog({ text: t("FsmData_NeedProjectApproval", "请先提交融资方案审批并通过后，才能进入融资落实阶段。") });
                }
            });
        } catch (ex) {
            console.error("[FSM] 注册 BPF PreStageChange 拦截失败:", ex);
        }
    }

    /**
     * 将 BPF 侧窗格中的字段设为只读（复用 mcs_credit_record.js 模式）
     * BPF 设计器本身没有只读选项，通过官方 Client API 在运行时锁定
     */
    function lockBpfFields(formContext) {
        if (!formContext || !formContext.ui || !formContext.ui.controls) return;
        try {
            // BPF 侧窗格字段控件以 header_process_ 开头
            formContext.ui.controls.forEach(function (control) {
                if (!control || !control.getName) return;
                var name = control.getName();
                if (name && name.indexOf("header_process_") === 0 && control.setDisabled) {
                    control.setDisabled(true);
                }
            });
        } catch (ex) {
            console.error("[FSM] 设置 BPF 字段只读失败:", ex);
        }
    }

    /**
     * 按当前阶段控制 需求融资管理 / 融资解决方案 两个 tab 的可写与必填
     * 规则：
     *  - 状态 1（含新建）：两个 tab 均禁用、不必填
     *  - 状态 2：需求融资管理 tab 可写 + 必填；方案 tab 禁用
     *  - 状态 3：方案 tab 可写 + 必填/选填；需求融资管理 tab 锁定只读
     *  - 状态 4 或审批中（mcs_bppstatus=2）：全部锁定只读
     */
    function applyStageControl(formContext) {
        var status = getCurrentStage(formContext);
        var bppStatus = getFieldValue(formContext, "mcs_bppstatus");
        var bppCodeAttr = formContext.getAttribute("mcs_bppstatuscode");
        var bppInReview = (bppStatus === BPP_STATUS.IN_REVIEW) || isBppInProgress(bppCodeAttr ? bppCodeAttr.getValue() : null);
        var lockAll = (status === FSM_STATUS.IMPLEMENTATION) || bppInReview;

        [INITIATION_TAB, SOLUTION_TAB].forEach(function (tab) {
            var editable = !lockAll && (status === tab.stage);
            tab.required.forEach(function (f) {
                setFieldDisabled(formContext, f, !editable);
                setFieldRequired(formContext, f, editable);
            });
            tab.optional.forEach(function (f) {
                setFieldDisabled(formContext, f, !editable);
                setFieldRequired(formContext, f, false);
            });
        });

        // 元数据必填字段随需求融资管理 tab 只做禁用控制
        var initiationEditable = !lockAll && (status === INITIATION_TAB.stage);
        INITIATION_TAB_METADATA_REQUIRED.forEach(function (f) {
            setFieldDisabled(formContext, f, !initiationEditable);
        });
    }

    function alertMissingFields(missing) {
        Xrm.Navigation.openAlertDialog({
            text: t("FsmData_RequiredMissing", "以下必填字段未填写：") + missing.join("、")
        });
    }

    /**
     * 提交审批前确认弹窗（确认/取消）
     */
    function confirmSubmit(typeName, onConfirm) {
        Xrm.Navigation.openConfirmDialog({
            title: t("FsmData_ConfirmTitle", "确认提交"),
            text: t("FsmData_ConfirmSubmit", "确认提交{0}吗？提交后将进入审批流程。").replace("{0}", typeName),
            confirmButtonLabel: t("FsmData_Confirm", "确认"),
            cancelButtonLabel: t("FsmData_Cancel", "取消")
        }).then(function (result) {
            if (result.confirmed) onConfirm();
        });
    }

    /**
     * 表单 onLoad：注册来源字段 onChange、弹窗过滤、保存校验
     */
    self.onLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();

        [SRC.LEAD, SRC.QUOTER, SRC.CONTRACT].forEach(function (f) {
            var attr = formContext.getAttribute(f);
            if (attr) attr.addOnChange(function () { recomputeDerived(formContext); });
        });

        // 线索变化时刷新报价主表缓存（代入也会触发）
        var leadAttrForCache = formContext.getAttribute(SRC.LEAD);
        if (leadAttrForCache) leadAttrForCache.addOnChange(function () { refreshQuoteMainCache(formContext); });
        refreshQuoteMainCache(formContext);

        var quoterCtrl = formContext.getControl(SRC.QUOTER);
        if (quoterCtrl) quoterCtrl.addPreSearch(function () { filterQuoterByLead(formContext); });
        var contractCtrl = formContext.getControl(SRC.CONTRACT);
        if (contractCtrl) contractCtrl.addPreSearch(function () { filterContractByLead(formContext); });

        formContext.data.entity.addOnSave(onSaveValidate);

        // 未上表单字段的服务端值缓存（BPF 门禁/提交校验回退读取）
        refreshFieldCache(formContext);
        formContext.data.entity.addOnPostSave(function () { refreshFieldCache(formContext); });

        // 按当前阶段控制 tab 字段可写 + 必填；阶段/审批状态变化时重新应用
        applyStageControl(formContext);
        var statusAttrForStage = formContext.getAttribute("mcs_fsm_status");
        if (statusAttrForStage) statusAttrForStage.addOnChange(function () {
            applyStageControl(formContext);
            syncBpfFromStatus(formContext);
        });
        var bppAttrForStage = formContext.getAttribute("mcs_bppstatus");
        if (bppAttrForStage) bppAttrForStage.addOnChange(function () { applyStageControl(formContext); });

        // BPF 阶段条：侧窗格字段只读 + 阶段与状态字段双向同步
        lockBpfFields(formContext);
        if (formContext.data && formContext.data.process) {
            try {
                formContext.data.process.addOnStageChange(function () {
                    syncStatusFromBpf(formContext);
                    applyStageControl(formContext);
                    lockBpfFields(formContext);
                });
            } catch (ex) {
                console.error("[FSM] 注册 BPF 阶段事件失败:", ex);
            }
            // 进入下一阶段前必须已通过对应阶段审批
            preventBpfNextWithoutApproval(formContext);
            // 打开表单时校正阶段条与状态字段的脱节（含旧数据）
            reconcileStagesOnLoad(formContext);
        }
    };

    /**
     * 提交立项审批
     * 前置条件：融资状态 = 2（融资立项）且 mcs_can_initiated = 1
     * 入口：表单命令栏【提交立项审批】按钮，参数 PrimaryControl
     */
    self.submitInitiationApproval = function (primaryControl) {
        var formContext = primaryControl;

        // 融资经理：取系统登录人（PRD：页面融资经理取系统登陆人）
        var managerAttr = formContext.getAttribute("mcs_fsm_manager");
        if (managerAttr && !managerAttr.getValue()) {
            var userSettings = Xrm.Utility.getGlobalContext().userSettings;
            managerAttr.setValue([{ id: userSettings.userId, entityType: "systemuser", name: userSettings.userName }]);
            formContext.data.entity.save();
        }

        // 融资需求管理提交：融资六要素必填（PRD：除附件外所有字段必填）
        var missing = validateRequiredFields(formContext, REQUIRED_SIX_ELEMENTS);
        if (missing.length > 0) { alertMissingFields(missing); return; }

        var statusAttr = formContext.getAttribute("mcs_fsm_status");
        var fsmStatus = statusAttr ? statusAttr.getValue() : null;
        if (fsmStatus !== FSM_STATUS.INITIATION) {
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_OnlyInitiationStatus", "只有融资立项状态才能提交立项审批。") });
            return;
        }

        // mcs_can_initiated 未上表单，实时查服务端值校验
        getServerFieldValue(formContext, "mcs_can_initiated").then(function (canInitiated) {
            if (canInitiated !== true) {
                Xrm.Navigation.openAlertDialog({ text: t("FsmData_InitiationNotAllowed", "当前不允许提交立项审批。") });
                return;
            }
            confirmSubmit(t("FsmData_TypeInitiation", "立项审批"), function () {
                submitApproval(formContext, APPROVE_TYPE.INITIATION, t("FsmData_TypeInitiation", "立项审批"));
            });
        });
    };

    /**
     * 提交融资方案审批
     * 前置条件：融资状态 = 3（融资解决方案）且 mcs_can_project = 1
     * 入口：表单命令栏【提交融资方案审批】按钮，参数 PrimaryControl
     */
    self.submitProjectApproval = function (primaryControl) {
        var formContext = primaryControl;

        // 融资解决方案提交：所有页面字段除附件外必填 + 合同号必填（PRD）
        var missing = validateRequiredFields(formContext,
            REQUIRED_SIX_ELEMENTS.concat(REQUIRED_SOLUTION_EXTRA).concat([SRC.CONTRACT]));
        if (missing.length > 0) { alertMissingFields(missing); return; }

        var statusAttr = formContext.getAttribute("mcs_fsm_status");
        var fsmStatus = statusAttr ? statusAttr.getValue() : null;
        if (fsmStatus !== FSM_STATUS.SOLUTION) {
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_OnlySolutionStatus", "只有融资解决方案状态才能提交融资方案审批。") });
            return;
        }

        // mcs_can_project 未上表单，实时查服务端值校验
        getServerFieldValue(formContext, "mcs_can_project").then(function (canProject) {
            if (canProject !== true) {
                Xrm.Navigation.openAlertDialog({ text: t("FsmData_ProjectNotAllowed", "当前不允许提交融资方案审批。") });
                return;
            }
            confirmSubmit(t("FsmData_TypeProject", "融资方案审批"), function () {
                submitApproval(formContext, APPROVE_TYPE.PROJECT, t("FsmData_TypeProject", "融资方案审批"));
            });
        });
    };

    // 预加载语言包（异步，不阻塞后续逻辑）
    if (typeof LanguageHelper !== "undefined") {
        LanguageHelper.loadLanguagePack();
    }

    return self;
})();
