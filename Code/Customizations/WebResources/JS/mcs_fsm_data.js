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

    // 融资落实页签（tab_5）子网格控件名（目标实体 mcs_fsm_detail_data）
    var IMPLEMENTATION_SUBGRID = "Subgrid_new_1";

    // mcs_fsm_data.mcs_bppstatus 选项集值
    var BPP_STATUS = {
        APPLY: 1,       // 申请
        IN_REVIEW: 2,   // 审批中
        APPROVED: 3,    // 通过
        REJECTED: 4     // 驳回
    };

    // Bug #1559（2026-08-04）：金融产品改为与六要素「融资产品」同一字段 mcs_fsm_product（单选选项集，仅银行类 1-11），
    // 原 #1507 多选字段 mcs_fsm_resource_products 表单隐藏弃用，ALL_PRODUCT_OPTIONS / filterProductsByResource 同步移除

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
        // Bug #1561：备注随本次 Update 同一事务落库（表单上未保存的修改也一并提交），
        // 保证 BPP 侧 GetBppFormData 读取到最新值；立项/方案分别提交各自的备注字段
        var remarkField = (approveType === APPROVE_TYPE.INITIATION) ? "mcs_fsm_initiation_remark" : "mcs_fsm_project_remark";
        var remarkAttr = formContext.getAttribute(remarkField);
        var payload = {
            "mcs_approve_type": approveType,
            "mcs_bppstatus": BPP_STATUS.IN_REVIEW
        };
        payload[remarkField] = remarkAttr ? remarkAttr.getValue() : null;
        Xrm.WebApi.updateRecord("mcs_fsm_data", recordId, payload).then(
            function () {
                Xrm.Navigation.openAlertDialog({ text: typeName + t("FsmData_SubmittedSuffix", "已提交。") }).then(function () {
                    // Bug #1508：提交成功后立即更新本地缓存并锁定表单字段（审批中全锁），
                    // 再刷新表单与服务端缓存后重放一次阶段控制，确保以服务端真实状态为准
                    _fieldCache["mcs_bppstatus"] = BPP_STATUS.IN_REVIEW;
                    _fieldCache["mcs_approve_type"] = approveType;
                    applyStageControl(formContext);
                    formContext.data.refresh(false).then(function () {
                        refreshFieldCache(formContext).then(function () { applyStageControl(formContext); });
                    });
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
        CONTRACT: "mcs_contract_id"   // 合同编号 → mcs_contract（禅道 #2150 起表单隐藏停用，由 CONTRACT_IDS 多选替代）
    };

    // 禅道 #2150：合同编号多选——平台公共 PCF 控件 mcs_common.control.lookup.multiplechoice 绑值，
    // 存 mcs_contract GUID 逗号分隔（同成交条件基线库 mcs_trade_type / 本表单机构多选 mcs_fsm_resource_ids 模式）
    var CONTRACT_IDS = "mcs_contract_ids"; // 多选合同 GUID 逗号分隔（Memo，控件绑值）
    var CONTRACT_NOS = "mcs_contract_nos"; // 合同编号（mcs_contract.mcs_name，LTC-xxx）逗号分隔文本

    /**
     * 禅道 #2150：解析多选合同 GUID 数组（按用户选择顺序）
     */
    function getSelectedContractIds(formContext) {
        var attr = formContext.getAttribute(CONTRACT_IDS);
        var raw = attr ? attr.getValue() : null;
        if (!raw) return [];
        return String(raw).split(",").map(function (x) { return x.trim().replace(/[{}]/g, ""); }).filter(function (x) { return x.length > 0; });
    }

    /**
     * 禅道 #2150：多选合同变更——同步合同编号文本 + 以第一个合同级联带出相关数据
     */
    function onContractIdsChanged(formContext) {
        syncContractNos(formContext);
        recomputeDerived(formContext);
    }

    /**
     * 禅道 #2150：按多选 GUID 顺序批量查合同编号，逗号分隔写入 mcs_contract_nos（展示/接口匹配用）
     */
    function syncContractNos(formContext) {
        var ids = getSelectedContractIds(formContext);
        if (ids.length === 0) { setValue(formContext, CONTRACT_NOS, null); return; }
        var filter = ids.map(function (id) { return "mcs_contractid eq " + id; }).join(" or ");
        Xrm.WebApi.retrieveMultipleRecords("mcs_contract", "?$select=mcs_contractid,mcs_name&$top=" + ids.length + "&$filter=" + filter).then(function (res) {
            var nameMap = {};
            res.entities.forEach(function (e) { nameMap[("" + e.mcs_contractid).toLowerCase()] = e.mcs_name || ""; });
            var nos = ids.map(function (id) { return nameMap[id.toLowerCase()] || ""; }).filter(function (n) { return n.length > 0; });
            setValue(formContext, CONTRACT_NOS, nos.length > 0 ? nos.join(",") : null);
        }, function (e) {
            console.warn("[FSM] 合同编号文本同步失败:", e);
        });
    }

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
    // 已选线索关联的报价主表 ID 缓存（用于报价单弹窗扁平过滤，避免 link-entity 查询生成器错误 0x80041103）
    var _quoteMainIdsForLead = null;

    // 未上表单字段的服务端值缓存（onLoad/保存后刷新），供 getFieldValue 回退读取
    // 背景：mcs_bppstatus/mcs_approve_type/mcs_can_initiated/mcs_can_project/mcs_fsm_credit_amount_usd 未放到表单上
    var _fieldCache = {};
    var CACHE_FIELDS = ["mcs_bppstatus", "mcs_approve_type", "mcs_can_initiated", "mcs_can_project", "mcs_fsm_credit_amount_usd"];

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

    // =====================================================================
    // 融资金额USD 自动折算（PRD：根据授信金额自动转换为USD存入）
    // 组织基础币种 = USD，公式：USD = 授信金额 ÷ 所选币种汇率（D365 交易货币表）
    // =====================================================================
    var _currencyRateCache = {};   // transactioncurrencyid → exchangerate

    /**
     * 授信金额/融资币种变更时自动折算 USD 并写入服务端（字段未上表单，仅存库）
     */
    function updateCreditAmountUsd(formContext) {
        var amountAttr = formContext.getAttribute("mcs_fsm_credit_amount");
        var currencyRef = getLookup(formContext, "mcs_fsm_currency");
        var amount = amountAttr ? amountAttr.getValue() : null;
        if (amount === null || amount === undefined || !currencyRef) return; // 缺一不算，保留现值

        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        if (!recordId) return; // 未保存记录不后台写（授信金额为融资解决方案阶段字段，此时记录已保存）

        var currencyId = currencyRef.id.replace(/[{}]/g, "").toLowerCase();
        var applyRate = function (rate) {
            if (!rate) return;
            var usd = Math.round((amount / rate) * 100) / 100;
            var current = getFieldValue(formContext, "mcs_fsm_credit_amount_usd");
            if (current !== null && current !== undefined && Math.abs(current - usd) < 0.005) return; // 值未变化不写
            Xrm.WebApi.updateRecord("mcs_fsm_data", recordId, { "mcs_fsm_credit_amount_usd": usd }).then(
                function () {
                    _fieldCache["mcs_fsm_credit_amount_usd"] = usd;
                    console.log("[FSM] 融资金额USD 自动折算: " + amount + " / " + rate + " = " + usd);
                },
                function (e) { console.warn("[FSM] 融资金额USD 折算写入失败:", e); }
            );
        };

        if (_currencyRateCache[currencyId]) {
            applyRate(_currencyRateCache[currencyId]);
        } else {
            Xrm.WebApi.retrieveRecord("transactioncurrency", currencyId, "?$select=exchangerate").then(
                function (r) {
                    _currencyRateCache[currencyId] = r["exchangerate"];
                    applyRate(r["exchangerate"]);
                },
                function (e) { console.warn("[FSM] 汇率读取失败:", e); }
            );
        }
    }

    // =====================================================================
    // Bug #1781（2026-08-12）：Money 字段货币符号跟随「融资币种」
    // 平台机制：Money 控件符号由记录标准币种字段 transactioncurrencyid 驱动（不可绑定自定义 Lookup），
    // 主表单已隐藏放置标准 Currency 字段作符号载体，此处保持其与 mcs_fsm_currency 一致，
    // 融资金额/授信金额/贴息/融资费用 4 个 Money 字段符号即随融资币种即时切换。
    // 实测（DEV1 2026-08-12）：程序化 setValue 不同步刷新符号显示，仅保存/重载后平台才重格式化；
    // 故附加 DOM 补丁：onChange 同步后立即改写 4 个金额输入框的显示符号，blur/tab 切换后重贴兜底。
    // =====================================================================
    var MONEY_FIELDS_FOR_SYMBOL = ["mcs_fsm_amount", "mcs_fsm_credit_amount", "mcs_fsm_interest_discount", "mcs_fsm_fee"];
    var _currencySymbolCache = {};   // transactioncurrencyid → currencysymbol
    var _currentSymbol = null;       // 当前融资币种符号（重贴兜底用）

    /**
     * 同步标准币种字段 transactioncurrencyid = mcs_fsm_currency（值不同才写入，避免表单标脏）
     * 融资币种为空时不动（新建表单平台默认币种保留，用户选择后立即同步）
     */
    function syncTransactionCurrency(formContext) {
        var txnAttr = formContext.getAttribute("transactioncurrencyid");
        if (!txnAttr) return; // 标准 Currency 字段未上表单（隐藏单元格缺失）时静默跳过
        var currencyRef = getLookup(formContext, "mcs_fsm_currency");
        if (!currencyRef) return;
        var newId = currencyRef.id.replace(/[{}]/g, "").toLowerCase();
        var curRef = getLookup(formContext, "transactioncurrencyid");
        var curId = curRef ? curRef.id.replace(/[{}]/g, "").toLowerCase() : null;
        if (newId === curId) return; // 已一致不置脏（onLoad 打开存量记录时平台已按保存的币种格式化，无需补丁）
        txnAttr.setValue([{
            id: currencyRef.id,
            name: currencyRef.name,
            entityType: "transactioncurrency"
        }]);
        patchMoneyFieldSymbols(currencyRef.id); // 金额符号显示即时跟随（平台要等保存后才重格式化）
    }

    /**
     * 查询币种符号并补丁 4 个 Money 输入框的显示值（带缓存；仅改显示文本，不动属性值，保存不受影响）
     */
    function patchMoneyFieldSymbols(currencyId) {
        var cid = (currencyId || "").replace(/[{}]/g, "").toLowerCase();
        if (!cid) return;
        if (_currencySymbolCache[cid] !== undefined) {
            _applySymbolToMoneyInputs(_currencySymbolCache[cid]);
            return;
        }
        Xrm.WebApi.retrieveRecord("transactioncurrency", cid, "?$select=currencysymbol").then(
            function (r) {
                var sym = r["currencysymbol"] || "";
                _currencySymbolCache[cid] = sym;
                _applySymbolToMoneyInputs(sym);
            },
            function (e) { console.warn("[FSM] 币种符号读取失败:", e); }
        );
    }

    /**
     * 取表单所在 document（UCI 表单脚本在 ClientApiFrame iframe 执行，表单 DOM 在顶层同源 document）
     */
    function _getFormDocument() {
        try {
            if (document.querySelector('input[data-id^="mcs_fsm_amount"]')) return document;
            if (window.parent && window.parent.document
                && window.parent.document.querySelector('input[data-id^="mcs_fsm_amount"]')) return window.parent.document;
        } catch (e) { /* 跨域等异常静默兜底 */ }
        return document;
    }

    /**
     * 把 4 个 Money 输入框显示值的前导符号替换为指定符号（React 不重渲染该控件则补丁保持，实测 20s+ 稳定）
     */
    function _applySymbolToMoneyInputs(symbol) {
        if (!symbol) return;
        _currentSymbol = symbol;
        var doc = _getFormDocument();
        var nativeSetter = Object.getOwnPropertyDescriptor(window.HTMLInputElement.prototype, "value").set;
        MONEY_FIELDS_FOR_SYMBOL.forEach(function (f) {
            var input = doc.querySelector('input[data-id="' + f + '.fieldControl-currency-text-input"]');
            if (!input || !input.value) return;
            var newVal = input.value.replace(/^[^\d\-]+/, symbol);
            if (newVal === input.value && input.value.indexOf(symbol) !== 0) newVal = symbol + input.value; // 无前导符号兜底
            if (newVal !== input.value) nativeSetter.call(input, newVal);
        });
    }

    // =====================================================================
    // Bug #1559（2026-08-04）：融资资源机构多选（mcs_fsm_resource_ids 存 GUID 逗号分隔）
    // 候选校验：启用（statecode=0）且机构产品包含所选融资产品；
    // 选中变化按机构类型（1银行/2保险/9其它）分组，名称/编码逗号分隔带入 6 个只读字段
    // =====================================================================
    var _resourceFilterRunning = false; // onChange 内 setValue 会再触发 onChange，防重入

    /**
     * 值不同才写入（避免 onLoad 回填把表单标脏）
     */
    function setIfChanged(formContext, fieldName, value) {
        var attr = formContext.getAttribute(fieldName);
        if (!attr) return;
        var current = attr.getValue();
        var normalized = (value === null || value === undefined || value === "") ? null : value;
        if (current !== normalized) attr.setValue(normalized);
    }

    /**
     * 按机构类型分组，把名称/编码逗号分隔带入 6 个字段
     */
    function fillInstitutionFields(formContext, resources) {
        var bankNames = [], bankCodes = [], insNames = [], insCodes = [], othNames = [], othCodes = [];
        resources.forEach(function (r) {
            var type = r["mcs_fsm_institution_type"];
            var name = r["mcs_fsm_institution_name"];
            var code = r["mcs_fsm_institution_code"];
            var names = type === 1 ? bankNames : type === 2 ? insNames : othNames;
            var codes = type === 1 ? bankCodes : type === 2 ? insCodes : othCodes;
            if (name) names.push(name);
            if (code) codes.push(code);
        });
        setIfChanged(formContext, "mcs_fsm_bank_names", bankNames.join(","));
        setIfChanged(formContext, "mcs_fsm_bank_codes", bankCodes.join(","));
        setIfChanged(formContext, "mcs_fsm_insurance_names", insNames.join(","));
        setIfChanged(formContext, "mcs_fsm_insurance_codes", insCodes.join(","));
        setIfChanged(formContext, "mcs_fsm_other_names", othNames.join(","));
        setIfChanged(formContext, "mcs_fsm_other_codes", othCodes.join(","));
        updateInstitutionVisibility(formContext);
    }

    /** 机构名称/编码 6 字段分组（名称+编码 同组同显隐） */
    var INSTITUTION_FIELD_GROUPS = [
        ["mcs_fsm_bank_names", "mcs_fsm_bank_codes"],
        ["mcs_fsm_insurance_names", "mcs_fsm_insurance_codes"],
        ["mcs_fsm_other_names", "mcs_fsm_other_codes"]
    ];

    /**
     * 机构名称/编码 6 字段显隐（Bug #1559 补充 2026-08-04）：
     * 组内任一字段有值则整组显示，全空则整组隐藏。
     * 触发时机：onLoad 初始化 / 表单异步带入完成后 / picker 写入后 fireOnChange。
     */
    function updateInstitutionVisibility(formContext) {
        INSTITUTION_FIELD_GROUPS.forEach(function (pair) {
            var hasValue = pair.some(function (f) {
                var a = formContext.getAttribute(f);
                var v = a ? a.getValue() : null;
                return v !== null && v !== undefined && ("" + v).trim() !== "";
            });
            pair.forEach(function (f) {
                var ctrl = formContext.getControl(f);
                if (ctrl && ctrl.setVisible) ctrl.setVisible(hasValue);
            });
        });
    }

    /**
     * 融资资源机构多选变更：校验启用状态 + 融资产品匹配，不匹配项移除并提示；有效项带入 6 个名称/编码字段
     */
    function onResourceIdsChanged(formContext) {
        if (_resourceFilterRunning) return;
        var attr = formContext.getAttribute("mcs_fsm_resource_ids");
        if (!attr) return;
        var raw = attr.getValue();
        var guids = raw ? String(raw).split(",").map(function (x) { return x.trim(); }).filter(function (x) { return x.length > 0; }) : [];
        if (guids.length === 0) {
            fillInstitutionFields(formContext, []);
            return;
        }
        var productAttr = formContext.getAttribute("mcs_fsm_product");
        var product = productAttr ? productAttr.getValue() : null; // 单选选项集 int 或 null

        var promises = guids.map(function (guid) {
            return Xrm.WebApi.retrieveRecord("mcs_fsm_resource", guid,
                "?$select=mcs_fsm_institution_type,mcs_fsm_institution_name,mcs_fsm_institution_code,mcs_fsm_institution_products,statecode")
                .catch(function (e) { console.warn("[FSM] 查询融资资源失败:", guid, e); return null; });
        });
        Promise.all(promises).then(function (results) {
            // 防异步乱序覆盖：发起查询后字段值已被更新（如用户连续勾选），放弃本次写入，由最新一次变更负责
            var currentRaw = attr.getValue() || "";
            if (currentRaw !== (raw || "")) {
                console.log("[FSM] 机构多选带入放弃过期写入（值已变更）");
                return;
            }
            var valid = [], removed = [];
            results.forEach(function (r, i) {
                if (!r) { removed.push(guids[i]); return; }
                var ok = r["statecode"] === 0;
                // 融资产品已选时，机构产品必须包含所选产品；产品为空（不应出现，立项审批已校验）保守放行
                if (ok && product !== null && product !== undefined) {
                    var prods = r["mcs_fsm_institution_products"]
                        ? String(r["mcs_fsm_institution_products"]).split(",").map(function (x) { return parseInt(x, 10); })
                        : [];
                    ok = prods.indexOf(product) >= 0;
                }
                if (ok) valid.push(r); else removed.push(r["mcs_fsm_institution_name"] || guids[i]);
            });
            if (removed.length > 0) {
                _resourceFilterRunning = true;
                attr.setValue(valid.length > 0 ? valid.map(function (r) { return r["mcs_fsm_resourceid"]; }).join(",") : null);
                _resourceFilterRunning = false;
                Xrm.Navigation.openAlertDialog({
                    text: t("FsmData_ResourceFiltered", "以下机构已停用或不包含所选融资产品，已移除：") + removed.join("、")
                });
            }
            fillInstitutionFields(formContext, valid);
        });
    }

    /**
     * 融资产品变更时清空机构多选及 6 个带入字段（防脏数据）
     */
    function clearResourceSelection(formContext) {
        var attr = formContext.getAttribute("mcs_fsm_resource_ids");
        if (attr && attr.getValue()) attr.setValue(null); // 触发 onResourceIdsChanged 清空 6 字段
        fillInstitutionFields(formContext, []);
    }

    /**
     * 授信金额默认值：取融资金额（有值时放入，支持手工修改）（Bug #1507，PRD）
     * 仅融资解决方案阶段且授信金额为空时默认，不覆盖用户已录入/已修改的值
     */
    function defaultCreditAmount(formContext) {
        if (getCurrentStage(formContext) !== FSM_STATUS.SOLUTION) return;
        var creditAttr = formContext.getAttribute("mcs_fsm_credit_amount");
        if (!creditAttr || creditAttr.getValue() !== null) return;
        var amountAttr = formContext.getAttribute("mcs_fsm_amount");
        var amount = amountAttr ? amountAttr.getValue() : null;
        if (amount === null || amount === undefined) return;
        creditAttr.setValue(amount); // 触发 onChange 自动折算 USD
    }

    /**
     * Bug #1538：融资经理默认值——进入融资立项阶段（状态 2）且字段为空时，自动带出当前登录人
     * （不强制保存，保持可编辑，随用户保存落库；用户手改后不覆盖）
     */
    function defaultManager(formContext) {
        if (getCurrentStage(formContext) !== FSM_STATUS.INITIATION) return;
        var managerAttr = formContext.getAttribute("mcs_fsm_manager");
        if (!managerAttr || managerAttr.getValue()) return;
        var userSettings = Xrm.Utility.getGlobalContext().userSettings;
        managerAttr.setValue([{ id: userSettings.userId, entityType: "systemuser", name: userSettings.userName }]);
    }

    /**
     * 级联重算：任一来源字段变更后全量重算派生字段
     * 优先级：合同 > 报价单 > 线索；合同/报价单可向上代入线索
     */
    function recomputeDerived(formContext) {
        try {
        var leadRef = getLookup(formContext, SRC.LEAD);
        var quoterRef = getLookup(formContext, SRC.QUOTER);
        // 禅道 #2150：合同多选，以第一个合同带出相关数据
        var contractIds = getSelectedContractIds(formContext);
        var contractId = contractIds.length > 0 ? contractIds[0] : null;

        var pLead = leadRef ? retrieveSafe("mcs_leadmain", leadRef.id,
            "?$select=_mcs_countryid_value,_mcs_buid_value,_mcs_customermasterdataid_value,mcs_accountnumber") : Promise.resolve(null);
        var pQuoter = quoterRef ? retrieveSafe("mcs_quoter", quoterRef.id,
            "?$select=_mcs_countryid_value,_mcs_customermasterdataid_value,mcs_customercode,_mcs_quote_mainid_value") : Promise.resolve(null);
        var pContract = contractId ? retrieveSafe("mcs_contract", contractId,
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
                        applyCustomerCodeReadonly(formContext); // 带出后即锁（有值恒只读口径）
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

    // =====================================================================
    // 禅道 #2176：接口人放大镜按安全角色过滤（addCustomView 自定义视图）
    // =====================================================================

    // 角色名口径（角色 GUID 各环境不同，FetchXML 按名称联查角色不写死 GUID；⚠️角色改名后过滤会失效为空结果）
    var MANAGER_ROLE_FILTER = [
        { field: "mcs_fsm_manager", roleName: "Financing Solutions Manager", viewId: "{2B7E6A10-2176-4A01-9F01-000000000001}" },   // 融资方案接口人（融资需求页签）
        { field: "mcs_fsm_postloan_manager", roleName: "Post-Financing Manager", viewId: "{2B7E6A10-2176-4A01-9F01-000000000002}" } // 贷后管理人（融资落实页签）
    ];

    /**
     * 禅道 #2176：接口人放大镜设为「按角色过滤」自定义视图（默认视图，初始列表与搜索结果都过滤）。
     * 成员口径 = 直接分配该角色的用户 + 拥有该角色的团队的成员用户（团队继承），均为启用用户；
     * 角色不存在/无成员时视图为空（放大镜无结果，绝不放开全量）。
     * 返工说明：初版 addPreSearch+addCustomFilter 只过滤输入后的搜索；v2 addCustomView 过滤视图结果，
     * 但点开放大镜的初始列表=平台「最近使用（MRU）」列表，两种 API 都管不到 → v3 补 disableMru 关闭 MRU。
     */
    function applyManagerRoleView(formContext, fieldName, roleName, viewId) {
        var fetchXml =
            "<fetch version='1.0' mapping='logical' distinct='true'>" +
            "<entity name='systemuser'>" +
            "<attribute name='fullname' />" +
            "<attribute name='systemuserid' />" +
            "<order attribute='fullname' />" +
            // 直接分配角色（outer join，成员判定在下方 or 过滤）
            "<link-entity name='systemuserroles' from='systemuserid' to='systemuserid' link-type='outer' alias='sur'>" +
            "<link-entity name='role' from='roleid' to='roleid' link-type='outer' alias='sr' />" +
            "</link-entity>" +
            // 团队继承角色（用户所在团队拥有该角色）
            "<link-entity name='teammembership' from='systemuserid' to='systemuserid' link-type='outer' alias='tm'>" +
            "<link-entity name='teamroles' from='teamid' to='teamid' link-type='outer' alias='tr'>" +
            "<link-entity name='role' from='roleid' to='roleid' link-type='outer' alias='trr' />" +
            "</link-entity></link-entity>" +
            "<filter type='and'>" +
            "<condition attribute='isdisabled' operator='eq' value='0' />" +
            "<filter type='or'>" +
            "<condition entityname='sr' attribute='name' operator='eq' value='" + roleName + "' />" +
            "<condition entityname='trr' attribute='name' operator='eq' value='" + roleName + "' />" +
            "</filter>" +
            "</filter>" +
            "</entity></fetch>";
        var layoutXml =
            "<grid name='resultset' object='8' jump='fullname' select='1' icon='1' preview='1'>" +
            "<row name='result' id='systemuserid'><cell name='fullname' width='300' /></row></grid>";
        var ctrl = formContext.getControl(fieldName);
        if (ctrl) {
            ctrl.addCustomView(viewId, "systemuser", roleName, fetchXml, layoutXml, true);
            // 关闭「最近使用」列表（MRU 不受任何过滤 API 控制，不关则点开仍显示未过滤人员）
            ctrl.disableMru = true;
        }
    }

    /**
     * 重复性校验：线索/报价单/合同三者有一个相同存在即重复
     * Bug #1652（2026-08-07 二次修复）：改为**同步** XMLHttpRequest 查询。
     * 原异步 preventDefault→查询→重新保存模式会在 BPF 阶段导航保存时中止导航，
     * 导致回退（融资立项→融资需求）被平台弹回立项阶段（先退回又立刻回去）。
     * 同步查询无需 preventDefault 等待，导航保存/手动保存均不被误中止。
     * @returns 同步返回重复记录的融资管理编号；无重复/查询失败返回 null（失败不阻断保存）
     */
    function checkDuplicateSourceSync(formContext) {
        var conditions = [];
        var leadRef = getLookup(formContext, SRC.LEAD);
        var quoterRef = getLookup(formContext, SRC.QUOTER);
        var contractIds = getSelectedContractIds(formContext); // 禅道 #2150：多选合同任一相同即重复
        if (leadRef) conditions.push("_mcs_leadmain_id_value eq " + leadRef.id.replace(/[{}]/g, ""));
        if (quoterRef) conditions.push("_mcs_quoter_id_value eq " + quoterRef.id.replace(/[{}]/g, ""));
        contractIds.forEach(function (id) { conditions.push("contains(mcs_contract_ids,'" + id + "')"); });
        if (conditions.length === 0) return null;

        var filter = "(" + conditions.join(" or ") + ")";
        var recordId = formContext.data.entity.getId().replace(/[{}]/g, "");
        if (recordId) filter += " and mcs_fsm_dataid ne " + recordId;

        try {
            var url = Xrm.Utility.getGlobalContext().getClientUrl()
                + "/api/data/v9.2/mcs_fsm_datas?$select=mcs_fsm_no&$top=1&$filter=" + encodeURIComponent(filter);
            var req = new XMLHttpRequest();
            req.open("GET", url, false); // 同步
            req.setRequestHeader("OData-MaxVersion", "4.0");
            req.setRequestHeader("OData-Version", "4.0");
            req.setRequestHeader("Accept", "application/json");
            req.setRequestHeader("Content-Type", "application/json; charset=utf-8");
            req.send();
            if (req.status === 200) {
                var result = JSON.parse(req.responseText);
                return (result.value && result.value.length > 0) ? (result.value[0].mcs_fsm_no || "") : null;
            }
            console.warn("[FSM] 重复性校验同步查询返回 " + req.status + "，放行保存");
            return null; // 查询失败不阻断保存（含实体集名称不符 404 的兜底）
        } catch (e) {
            console.error("[FSM] 重复性校验同步查询异常:", e);
            return null;
        }
    }

    /**
     * 保存校验：至少一个来源 + 重复性校验（同步，不再 preventDefault 等待）
     * 注意：表单挂有 BPF「融资管理」，BPF 阶段导航（含回退）会触发平台内部保存，
     * onSave 里的 preventDefault 会中止阶段导航（平台报错 0x80060802 或静默弹回原阶段），
     * 因此重复性校验必须同步完成：有重复才 preventDefault，无重复直接放行。
     */
    function onSaveValidate(executionContext) {
        var formContext = executionContext.getFormContext();
        var eventArgs = executionContext.getEventArgs();

        var hasSource = getLookup(formContext, SRC.LEAD) || getLookup(formContext, SRC.QUOTER) || getSelectedContractIds(formContext).length > 0;
        if (!hasSource) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_RequireOneSource", "线索编号、报价单编号、合同编号至少填写一个。") });
            return;
        }

        // 来源字段均未变更：跳过重复校验直接放行（避免拦截 BPF 阶段导航等系统保存）
        var sourceDirty = [SRC.LEAD, SRC.QUOTER, CONTRACT_IDS].some(function (f) {
            var attr = formContext.getAttribute(f);
            return attr && attr.getIsDirty();
        });
        if (!sourceDirty) return;

        var dupNo = checkDuplicateSourceSync(formContext);
        if (dupNo) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_DuplicateSource", "已存在相同线索/报价单/合同编号的融资管理记录：") + dupNo });
        }
    }

    // 未上表单字段的提示标签回退（无控件可取 label 时避免弹窗显示架构名，2026-08-05 用户反馈）
    var OFF_FORM_FIELD_LABELS = {
        "mcs_fsm_credit_amount_usd": { key: "FsmData_Field_CreditAmountUsd", zh: "融资金额USD" }
    };

    /**
     * 通用必填校验（控件 label 取当前 UI 语言）
     * @returns 缺失字段 label 数组
     */
    function validateRequiredFields(formContext, fieldNames) {
        var missing = [];
        fieldNames.forEach(function (f) {
            // 表单上读不到时回退读服务端缓存（如 mcs_fsm_credit_amount_usd 未上表单，由自动折算写入）
            var v = getFieldValue(formContext, f);
            var empty = (v === null || v === undefined || v === "" || (Array.isArray(v) && v.length === 0));
            if (empty) {
                var ctrl = formContext.getControl(f);
                missing.push(ctrl ? ctrl.getLabel() : (OFF_FORM_FIELD_LABELS[f] ? t(OFF_FORM_FIELD_LABELS[f].key, OFF_FORM_FIELD_LABELS[f].zh) : f));
            }
        });
        return missing;
    }

    // 融资六要素 + 融资需求管理阶段必填（提交立项审批时校验）
    // Bug #1559：融资产品文本 mcs_fsm_product_desc → 单选选项集 mcs_fsm_product
    // Bug #1578：设备台数/产品名称改完全非必填，移出本清单（立项/方案审批均不校验）
    var REQUIRED_SIX_ELEMENTS = [
        "mcs_fsm_amount", "mcs_fsm_currency", "mcs_fsm_period", "mcs_fsm_payment_ratio",
        "mcs_fsm_interest_rate", "mcs_fsm_product"
    ];
    // 融资解决方案阶段追加必填（提交融资方案审批时校验，另含合同号必填）
    // Bug #1559：金融产品=六要素融资产品同字段 mcs_fsm_product；融资资源机构多选 mcs_fsm_resource_ids 必填；
    //           贴息/融资费用/回购条件/其它条件 4 项改非必填
    var REQUIRED_SOLUTION_EXTRA = [
        "mcs_fsm_product", "mcs_fsm_resource_ids",
        "mcs_fsm_credit_amount", "mcs_fsm_credit_amount_usd"
    ];

    // =====================================================================
    // 按当前阶段（mcs_fsm_status）控制 tab 字段可写 + 必填（2026-07-25）
    // =====================================================================

    // 需求融资管理 tab（阶段 2 融资立项时可写 + 必填）
    var INITIATION_TAB = {
        stage: FSM_STATUS.INITIATION,
        required: ["mcs_fsm_manager", "mcs_fsm_is_initiated", "mcs_can_initiated"],
        // Bug #1561：立项提交审批备注（评审意见），仅状态 2 可填，提交立项审批时随记录推给 BPP
        optional: ["mcs_fsm_initiation_remark"]
    };
    // 融资解决方案 tab（阶段 3 融资解决方案时可写 + 必填/选填）
    // Bug #1559：金融产品=六要素融资产品同字段（SOLUTION_TAB.required 提供必填星标，可写锁定由 DEMAND_AREA_FIELDS 兜底）、
    //           机构多选必填；4 项改非必填；旧单选机构字段表单隐藏移出清单
    var SOLUTION_TAB = {
        stage: FSM_STATUS.SOLUTION,
        // Bug #1635（2026-08-11 用户指示）：授信金额移出 required 改非必填（元数据本就 Required=None，
        // 红星与保存拦截仅来自本清单 setRequiredLevel），避免默认值未带出时必填拦截保存/刷新；
        // 提交融资方案审批时仍校验必填（REQUIRED_SOLUTION_EXTRA 已含 mcs_fsm_credit_amount/usd）
        required: ["mcs_fsm_product", "mcs_fsm_resource_ids", "mcs_fsm_credit_amount_usd",
                   "mcs_can_project", "mcs_is_valid"],
        // Bug（2026-08-04 新建误放开）：贴息/融资费用/回购条件/其它条件 4 项 #1559 改非必填时漏进本清单，
        // 导致不受阶段控制恒可编辑；移入 optional 恢复「仅状态 3 可写，其余阶段禁用」
        optional: ["mcs_fsm_interest_discount", "mcs_fsm_fee", "mcs_fsm_repurchase_conditions", "mcs_fsm_other_conditions",
                   // Bug #1561：方案提交审批备注（评审意见），仅状态 3 可填，提交融资方案审批时随记录推给 BPP
                   "mcs_fsm_project_remark",
                   // Bug #1635：授信金额非必填，但保持「仅状态 3 可写、其余阶段禁用」阶段控制
                   "mcs_fsm_credit_amount"]
    };
    // Bug #1559：方案页 6 个机构名称/编码字段由多选组件自动带入，任何阶段始终只读
    var SOLUTION_AUTO_FIELDS = [
        "mcs_fsm_bank_names", "mcs_fsm_bank_codes",
        "mcs_fsm_insurance_names", "mcs_fsm_insurance_codes",
        "mcs_fsm_other_names", "mcs_fsm_other_codes"
    ];
    // 设备台数/产品名称（六要素）：只控禁用，不调 setRequiredLevel
    // Bug #1578：两字段已改元数据非必填；锁定口径——状态 1/2/3 均可编辑，提交方案审批后锁死
    //            （审批中随 lockAll 锁定，方案审批通过转状态 4 全锁，取代 #1540「状态 1 才可写」口径）
    var INITIATION_TAB_METADATA_REQUIRED = ["mcs_fsm_device_count", "mcs_fsm_device_name"];

    // 融资需求区 + 融资六要素字段（Bug #1509 建清单、#1540 改口径）：仅阶段 1（融资需求）可写，
    // BPF 点「下一步」进入融资立项（状态=2）起锁定只读，仅报价编码/合同编码除外；
    // 状态 4 / 审批中随 lockAll 全锁。
    // 注：设备台数/产品名称已由 INITIATION_TAB_METADATA_REQUIRED 控制（同口径），不在此重复；
    //     本组字段均为元数据必填，只做禁用控制，不调 setRequiredLevel。
    var DEMAND_AREA_FIELDS = [
        // 来源三字段（禅道 #2150：合同编号改多选 mcs_contract_ids）
        "mcs_leadmain_id", "mcs_quoter_id", "mcs_contract_ids",
        // 级联带出（融资需求信息）
        "mcs_big_area", "mcs_country_id", "mcs_country_area",
        "mcs_division_id", "mcs_sub_company", "mcs_customer_name", "mcs_customer_id",
        // 融资六要素（除设备台数/产品名称）
        // Bug #1559：融资产品文本 mcs_fsm_product_desc → 单选选项集 mcs_fsm_product（六要素/方案双单元格， attr.controls 全量锁定）
        "mcs_fsm_amount", "mcs_fsm_currency", "mcs_fsm_period",
        "mcs_fsm_payment_ratio", "mcs_fsm_interest_rate", "mcs_fsm_product"
    ];

    // Bug #1540：进入融资立项（状态=2）后融资需求区锁定，仅报价编码/合同编码仍可写（#2150 合同改多选）
    var DEMAND_INITIATION_EDITABLE = ["mcs_quoter_id", "mcs_contract_ids"];

    // 客户编码（mcs_customer_id）：由客户名称级联带出（客户主数据 SAP 编码→报价单/线索编码兜底），
    // 2026-09-02 用户拍板口径变更（覆盖 PRD「自动带出，可修改」）：有值恒只读；
    // 兜底：带出失败字段为空时放开可写（元数据必填，防空值锁死无法保存）
    function applyCustomerCodeReadonly(formContext) {
        var attr = formContext.getAttribute("mcs_customer_id");
        var val = attr ? attr.getValue() : null;
        setFieldDisabled(formContext, "mcs_customer_id", !!(val && ("" + val).trim()));
    }

    // Bug #1656（取代 #1540 六要素全锁口径）：进入融资立项（状态=2）未提交立项审批前，六要素可编辑；
    // 提交审批后锁住（审批中随 lockAll），驳回（bppstatus=4）恢复可编辑，通过则锁死（initiationPassed 守卫）
    var SIX_ELEMENTS_INITIATION_EDITABLE = [
        "mcs_fsm_amount", "mcs_fsm_currency", "mcs_fsm_period",
        "mcs_fsm_payment_ratio", "mcs_fsm_interest_rate", "mcs_fsm_product"
    ];

    // Bug #1560：融资解决方案（状态=3）提交融资方案审批时校验合同编号必填，
    // 故状态 3 放行合同编号可修改（仅合同编号，报价编码仍锁定）（#2150 合同改多选）
    var DEMAND_SOLUTION_EDITABLE = ["mcs_contract_ids"];

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

    // reconcileStagesOnLoad → syncBpfFromStatus 程序化推进标记（门禁放行，2026-08-01）
    var _reconcilingFromStatus = false;

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
        // 官方文档：setActiveStage 仅用于回退到"已走过"的阶段（且要求选中阶段=活动阶段），
        // 前进到未走过阶段一律返回 invalid（2026-08-01 UAT/DEV1 实测实锤）；前进必须用 moveNext
        _syncingStage = true;
        _reconcilingFromStatus = true;
        var finish = function () {
            _syncingStage = false;
            _reconcilingFromStatus = false;
            applyStageControl(formContext);
            lockBpfFields(formContext);
        };
        // 预算式推进：最多移动 (status - bpfNum) 次，绝不多发 moveNext。
        // 教训（2026-08-01 DEV1 实测）：moveNext 回调触发时 getActiveStage() 可能仍返回旧阶段（竞态），
        // 若按“移动后重读阶段”决定继续，目标为最终阶段时会多发一次 moveNext 把流程直接推到 completed（无法撤销）。
        var movesLeft = status - bpfNum;
        var stepForward = function () {
            if (movesLeft <= 0) { finish(); return; }
            var cur = getBpfStageNumber(formContext);
            if (cur === null || cur >= status) { finish(); return; }
            movesLeft--;
            try {
                process.moveNext(function (result) {
                    if (result === "success") {
                        stepForward();
                    } else {
                        console.warn("[FSM] moveNext 同步阶段中止:", result);
                        finish();
                    }
                });
            } catch (e) {
                console.warn("[FSM] 同步 BPF 阶段失败:", e);
                finish();
            }
        };
        stepForward();
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
     * BPF 阶段变更门禁：
     *  - 前进（Next）：进入融资解决方案(3) 需立项审批已通过；进入融资落实(4) 需融资方案审批已通过；
     *    融资需求 → 融资立项 无前置审批，放行
     *  - 回退（Previous，Bug #1652）：提交立项审批前允许从融资立项回退融资需求；
     *    审批中（bppstatus=2）或立项已通过（含存在方案审批）后禁止回退，防架空「通过后锁住」（Bug #1656）
     */
    function preventBpfNextWithoutApproval(formContext) {
        var process = formContext.data && formContext.data.process;
        if (!process) return;
        try {
            process.addOnPreStageChange(function (stageChangeContext) {
                if (_reconcilingFromStatus) return; // 程序化同步（syncBpfFromStatus→moveNext）直接放行
                var args = stageChangeContext.getEventArgs();
                if (!args) return;
                var direction = args.getDirection();
                var targetStage = args.getStage ? args.getStage() : null;
                var targetNum = targetStage ? getStageNumberByName(targetStage.getName()) : null;
                if (!targetNum) return;

                var approveType = getFieldValue(formContext, "mcs_approve_type");
                var bppStatus = getFieldValue(formContext, "mcs_bppstatus");

                // 方案类型审批存在（任何状态）⟹ 立项必然已通过（提交方案的前置=立项通过且状态=3，2026-08-01 修复误判）
                var initiationPassed = (approveType === APPROVE_TYPE.INITIATION && bppStatus === BPP_STATUS.APPROVED)
                    || (approveType === APPROVE_TYPE.PROJECT);
                var projectPassed = (approveType === APPROVE_TYPE.PROJECT && bppStatus === BPP_STATUS.APPROVED);

                // Bug #1652：回退方向——审批中/立项已通过后禁止回退；提交立项审批前放行（状态 2 → 1）
                if (direction === "Previous") {
                    var bppCodeAttrPrev = formContext.getAttribute("mcs_bppstatuscode");
                    var inReviewPrev = (bppStatus === BPP_STATUS.IN_REVIEW)
                        || isBppInProgress(bppCodeAttrPrev ? bppCodeAttrPrev.getValue() : null);
                    if (inReviewPrev || initiationPassed) {
                        args.preventDefault();
                        Xrm.Navigation.openAlertDialog({ text: t("FsmData_NoRollbackAfterSubmit", "已提交立项审批，不允许回退到之前的阶段。") });
                        return;
                    }
                    // Bug #1652 三次修复（2026-08-09）：回退放行时立即把状态字段置为目标阶段值（标脏），
                    // 让平台自己的导航保存顺带落库；不要在 OnStageChange 里主动 save——
                    // 那会与平台导航保存并发冲突，导致首次回退被中止弹回（UAT 实测需点两次）
                    var statusAttrPrev2 = formContext.getAttribute("mcs_fsm_status");
                    if (statusAttrPrev2 && statusAttrPrev2.getValue() !== targetNum) {
                        statusAttrPrev2.setValue(targetNum);
                    }
                    return;
                }
                if (direction !== "Next") return;

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
     * Bug #1766：BPF 流程是否已点「完成」（状态 finished）
     */
    function isBpfFinished(formContext) {
        try {
            var proc = formContext.data && formContext.data.process;
            return !!(proc && proc.getStatus && proc.getStatus() === "finished");
        } catch (e) {
            return false;
        }
    }

    /**
     * Bug #1816/#1817：单据完成后融资落实子网格只读——隐藏「新建融资落实」「添加现有融资落实」按钮。
     * 说明：UCI 子网格无支持的只读 API，此处为 DOM 级 UI 控制（本脚本在 ClientApiFrame iframe 执行，
     * 表单 DOM 在顶层同源 document）；服务端 FsmDetailDataCompletedGuardPlugin 兜底拦截，UI 隐藏仅降噪。
     */
    function applyImplGridButtonsVisibility(finished) {
        try {
            var docs = [document];
            try {
                if (window.parent && window.parent.document && window.parent.document !== document) docs.push(window.parent.document);
            } catch (e) { /* 跨域静默跳过 */ }
            docs.forEach(function (doc) {
                // 按 aria-label 匹配（实测：新建按钮 aria=「添加新融资落实…」，添加现有在溢出菜单）
                // + 按命令 data-id 匹配（Mscrm.SubGrid.mcs_fsm_detail_data.AddNewStandard/AddExisting）双保险
                var nodes = doc.querySelectorAll('button[aria-label*="融资落实"], [role="menuitem"][aria-label*="融资落实"], button[data-id*="SubGrid.mcs_fsm_detail_data.Add"]');
                nodes.forEach(function (n) {
                    var label = n.getAttribute("aria-label") || "";
                    var dataId = n.getAttribute("data-id") || "";
                    var isAddBtn = label.indexOf("新建") >= 0 || label.indexOf("添加新") >= 0 || label.indexOf("添加现有") >= 0
                        || dataId.indexOf("AddNew") >= 0 || dataId.indexOf("AddExisting") >= 0;
                    if (isAddBtn) n.style.display = finished ? "none" : "";
                });
            });
        } catch (e) { console.warn("[FSM] 落实子网格按钮显隐异常:", e); }
    }

    /**
     * Bug #1816/#1817：带重试的子网格按钮显隐调度。
     * 实锤：tab 切换/子网格加载后命令栏按钮渲染晚于事件回调（tab 点击 4 秒后按钮仍未插入 DOM），
     * 单次隐藏会落空；完成后 500ms×12 轮询补隐（找到与否都跑，幂等），未完成时立即恢复。
     */
    function scheduleImplGridButtons(formContext) {
        if (!isBpfFinished(formContext)) { applyImplGridButtonsVisibility(false); return; }
        var attempts = 0;
        var tick = function () {
            applyImplGridButtonsVisibility(true);
            attempts++;
            if (attempts < 12) setTimeout(tick, 500);
        };
        tick();
    }

    /**
     * Bug #1766：BPF 点「完成」后重跑锁定（贷后管理人锁死）。
     * 注意：「完成前必填校验」不在前端做——2026-08-11 DEV1 实锤本环境 OnPreProcessStatusChange
     * 的 eventArgs 无 preventDefault（UCI 不支持取消流程状态变更），且 pre 事件 getStatus() 返回旧值 active；
     * 必填阻断由服务端 FsmDataBpfCompleteShareNotifyPlugin PreOperation Step 完成（抛异常回滚）。
     */
    function lockAfterBpfComplete(formContext) {
        try {
            var proc = formContext.data.process;
            if (!proc || !proc.addOnProcessStatusChange) return;
            proc.addOnProcessStatusChange(function () {
                applyStageControl(formContext);
            });
        } catch (ex) {
            console.error("[FSM] 注册 BPF 状态变更事件失败:", ex);
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
     *  - 状态 1（含新建）：立项/方案两个 tab 均禁用、不必填；融资需求区 + 融资六要素可写
     *  - 状态 2：需求融资管理 tab（立项信息字段）可写 + 必填；方案 tab 禁用；
     *           融资需求区锁定只读，报价编码/合同编码除外（Bug #1540，来源用例-545）；
     *           六要素未提交立项审批前可编辑、审批中锁、驳回解锁、通过锁死（Bug #1656）
     *  - 状态 3：方案 tab 可写 + 必填/选填；需求融资管理 tab 锁定只读；
     *           融资需求区 + 融资六要素锁定只读（Bug #1509，用例 TC-FSM-DATA-013）
     *  - 状态 4 或审批中（mcs_bppstatus=2）：全部锁定只读（用例 TC-FSM-DATA-014）
     *  - 融资需求区：仅状态 1 可写，状态 2 仅报价/合同编码可写（#1540）；
     *    六要素例外（Bug #1656）：状态 1/2 均可写（状态 2 限未提交立项审批前，审批中随 lockAll 锁、
     *    驳回恢复可编辑、通过锁死），取代 #1540「状态 2 六要素全锁」口径
     *  - 设备台数/产品名称例外（Bug #1578）：状态 1/2/3 均可编辑，提交方案审批后锁死（审批中/状态 4 随 lockAll）
     */
    function applyStageControl(formContext) {
        var status = getCurrentStage(formContext);
        var bppStatus = getFieldValue(formContext, "mcs_bppstatus");
        var bppCodeAttr = formContext.getAttribute("mcs_bppstatuscode");
        var bppInReview = (bppStatus === BPP_STATUS.IN_REVIEW) || isBppInProgress(bppCodeAttr ? bppCodeAttr.getValue() : null);
        var lockAll = (status === FSM_STATUS.IMPLEMENTATION) || bppInReview;
        // Bug #1656：立项已通过（含存在方案审批）时六要素锁死；驳回（bppstatus=4）不命中本条件，恢复可编辑
        var approveType = getFieldValue(formContext, "mcs_approve_type");
        var initiationPassed = (approveType === APPROVE_TYPE.INITIATION && bppStatus === BPP_STATUS.APPROVED)
            || (approveType === APPROVE_TYPE.PROJECT);

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

        // 设备台数/产品名称（六要素，只做禁用控制；Bug #1578 已改非必填）：
        // Bug #1578 口径——状态 1/2/3 均可编辑，提交融资方案审批后锁死
        // （审批中 lockAll 自动锁；方案审批通过转状态 4 全锁；取代 #1540「进入融资立项即锁」口径）
        var demandMetaEditable = !lockAll && (status === FSM_STATUS.DEMAND
            || status === FSM_STATUS.INITIATION || status === FSM_STATUS.SOLUTION);
        INITIATION_TAB_METADATA_REQUIRED.forEach(function (f) {
            setFieldDisabled(formContext, f, !demandMetaEditable);
        });

        // 融资需求区 + 融资六要素：融资需求阶段（状态 1）可写；进入融资立项（状态 2）后需求区锁定，
        // 仅报价编码/合同编码除外（Bug #1540）；六要素在状态 2 未提交立项审批前可编辑、驳回可再改、
        // 通过锁死（Bug #1656，取代 #1540 六要素全锁口径）；
        // 融资解决方案（状态 3）再放行合同编号（Bug #1560：提交融资方案审批校验合同编号必填，须可修改）
        DEMAND_AREA_FIELDS.forEach(function (f) {
            var editable = !lockAll && (status === FSM_STATUS.DEMAND
                || (status === FSM_STATUS.INITIATION && (DEMAND_INITIATION_EDITABLE.indexOf(f) >= 0
                    || (!initiationPassed && SIX_ELEMENTS_INITIATION_EDITABLE.indexOf(f) >= 0)))
                || (status === FSM_STATUS.SOLUTION && DEMAND_SOLUTION_EDITABLE.indexOf(f) >= 0));
            setFieldDisabled(formContext, f, !editable);
        });

        // Bug #1559：方案页 6 个机构名称/编码带入字段任何阶段始终只读
        SOLUTION_AUTO_FIELDS.forEach(function (f) {
            setFieldDisabled(formContext, f, true);
        });

        // 客户编码恒只读（2026-09-02 用户拍板，覆盖 DEMAND_AREA_FIELDS 在状态 1 的放行结果）
        applyCustomerCodeReadonly(formContext);

        // Bug #1727（2026-08-10）：融资方案接口人（mcs_fsm_manager）
        // 在融资需求阶段（状态 1）由融资经理于「融资需求」Tab 填写，融资立项阶段（状态 2）保持可编辑
        // （#1538 口径：状态 2 为空带出当前登录人、可手改）；状态 3/4 及审批中（lockAll）锁定。
        // mcs_fsm_manager 同时在 INITIATION_TAB.required 中（保留其状态 2 必填星标语义），通用规则会在状态 1
        // 误锁该字段全部控件（含 tab_1 第二实例），此处按阶段口径单独覆盖。
        // Bug #2071（2026-08-29）：业务要求新增时（状态 1）即必填——必填覆盖与可编辑同口径
        // （状态 1/2 必填、状态 3/4 及审批中非必填；覆盖 INITIATION_TAB 通用规则在状态 1 置 none 的结果）。
        var managerEditable = !lockAll && (status === FSM_STATUS.DEMAND || status === FSM_STATUS.INITIATION);
        setFieldDisabled(formContext, "mcs_fsm_manager", !managerEditable);
        setFieldRequired(formContext, "mcs_fsm_manager", managerEditable);

        // Bug #1766（2026-08-11）：贷后管理人（mcs_fsm_postloan_manager）移至「融资落实」页签，
        // 仅融资落实阶段（状态 4）且 BPF 未点「完成」时可编辑（点完成时的必填校验由服务端 Plugin
        // PreOp 阻断，见 lockAfterBpfComplete 注释）；其余阶段及 BPF 完成后锁死（状态 4 命中 lockAll 全锁，此处单独开口）。
        var postloanEditable = (status === FSM_STATUS.IMPLEMENTATION) && !isBpfFinished(formContext);
        setFieldDisabled(formContext, "mcs_fsm_postloan_manager", !postloanEditable);

        // Bug（2026-08-04 新建误放开）：金融产品双单元格同属性（六要素 tab_2 + 方案 tab_4），
        // 属性级 setDisabled 两格互相覆盖（SOLUTION_TAB 禁用后被 DEMAND_AREA_FIELDS 六要素规则误放开），
        // 按控件所在 tab 分别控制：六要素格=状态 1 可写、状态 2 未提交立项审批前可写（Bug #1656）；方案格=仅状态 3 可写
        var productStageAttr = formContext.getAttribute("mcs_fsm_product");
        if (productStageAttr) {
            var productDemandEditable = !lockAll && (status === FSM_STATUS.DEMAND
                || (status === FSM_STATUS.INITIATION && !initiationPassed));
            var productSolutionEditable = !lockAll && status === FSM_STATUS.SOLUTION;
            productStageAttr.controls.forEach(function (ctrl) {
                try {
                    var tab = ctrl.getParent() && ctrl.getParent().getParent() ? ctrl.getParent().getParent().getName() : "";
                    ctrl.setDisabled(tab === "tab_4" ? !productSolutionEditable : !productDemandEditable);
                } catch (e) { /* 单元格定位异常时保持属性级结果 */ }
            });
        }

        // 融资落实子网格：仅融资落实阶段（状态 4）显示，其余阶段隐藏（页签保留，禁止提前新增）
        // 2026-08-06 用户需求：未到融资落实阶段不允许新增融资落实记录
        var implGrid = formContext.getControl(IMPLEMENTATION_SUBGRID);
        if (implGrid && implGrid.setVisible) implGrid.setVisible(status === FSM_STATUS.IMPLEMENTATION);

        // Bug #1816/#1817：单据完成后落实子网格只读（隐藏新建/添加现有按钮；服务端插件兜底）
        scheduleImplGridButtons(formContext);

        // Bug #1559：阶段控制每次执行后通知 picker 重算可编辑状态（picker 自读表单属性判定，标准自定义事件）
        try { window.dispatchEvent(new CustomEvent("FsmStageChanged")); } catch (e) { console.warn("[FSM] FsmStageChanged 事件分发失败:", e); }
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

        [SRC.LEAD, SRC.QUOTER].forEach(function (f) {
            var attr = formContext.getAttribute(f);
            if (attr) attr.addOnChange(function () { recomputeDerived(formContext); });
        });
        // 禅道 #2150：多选合同变更 → 同步合同编号文本 + 以第一个合同级联带出
        var contractIdsAttr = formContext.getAttribute(CONTRACT_IDS);
        if (contractIdsAttr) contractIdsAttr.addOnChange(function () { onContractIdsChanged(formContext); });

        // 线索变化时刷新报价主表缓存（代入也会触发）
        var leadAttrForCache = formContext.getAttribute(SRC.LEAD);
        if (leadAttrForCache) leadAttrForCache.addOnChange(function () { refreshQuoteMainCache(formContext); });
        refreshQuoteMainCache(formContext);

        var quoterCtrl = formContext.getControl(SRC.QUOTER);
        if (quoterCtrl) quoterCtrl.addPreSearch(function () { filterQuoterByLead(formContext); });
        // 禅道 #2150：合同改多选 PCF 控件（不支持 addPreSearch 过滤），原按线索过滤合同能力随控件取消

        // 禅道 #2176：接口人放大镜按安全角色过滤（自定义视图：初始列表与搜索均过滤）
        MANAGER_ROLE_FILTER.forEach(function (cfg) {
            applyManagerRoleView(formContext, cfg.field, cfg.roleName, cfg.viewId);
        });

        formContext.data.entity.addOnSave(onSaveValidate);

        // 未上表单字段的服务端值缓存（BPF 门禁/提交校验回退读取）
        refreshFieldCache(formContext).then(function () {
            // 存量记录打开表单时补齐空的 融资金额USD（自动折算兜底）
            var existingUsd = _fieldCache["mcs_fsm_credit_amount_usd"];
            if (existingUsd === null || existingUsd === undefined) updateCreditAmountUsd(formContext);
            // Bug #1508：缓存就绪后重放阶段控制，确保审批中（bppstatus=2）记录打开表单即锁定
            // （onLoad 同步执行的 applyStageControl 早于异步缓存填充，bppInReview 必为 false）
            applyStageControl(formContext);
        });
        formContext.data.entity.addOnPostSave(function () { refreshFieldCache(formContext); });

        // 授信金额默认值兜底：数据级 onLoad 在首次加载和「刷新」按钮重载数据时都会触发
        // （表单 onLoad/字段 onChange 均不随刷新触发；BPP 审批回调服务端推进阶段时客户端也无任何事件）
        formContext.data.addOnLoad(function () { defaultCreditAmount(formContext); });

        // 融资金额USD 自动折算：授信金额/融资币种变更时重算
        ["mcs_fsm_credit_amount", "mcs_fsm_currency"].forEach(function (f) {
            var attr = formContext.getAttribute(f);
            if (attr) attr.addOnChange(function () { updateCreditAmountUsd(formContext); });
        });

        // Bug #1781：标准币种字段跟随融资币种（onLoad 兜底纠正存量记录符号 + onChange 即时切换）
        syncTransactionCurrency(formContext);
        var currencyAttrForSymbol = formContext.getAttribute("mcs_fsm_currency");
        if (currencyAttrForSymbol) currencyAttrForSymbol.addOnChange(function () { syncTransactionCurrency(formContext); });
        // Bug #1781 符号补丁重贴：①金额输入框 blur 后平台用加载时币种重格式化（符号回退），focusout 后重贴；
        // ②tab 切换控件重建显示旧符号，tabStateChange 后重贴
        // 注意：本脚本在 ClientApiFrame iframe 执行，focusout 需挂到顶层同源 document（表单 DOM 所在）
        var _symbolFocusoutHandler = function (ev) {
            var did = ev && ev.target && ev.target.getAttribute ? (ev.target.getAttribute("data-id") || "") : "";
            var isMoney = MONEY_FIELDS_FOR_SYMBOL.some(function (f) { return did.indexOf(f + ".fieldControl") === 0; });
            if (isMoney && _currentSymbol) {
                setTimeout(function () { _applySymbolToMoneyInputs(_currentSymbol); }, 400);
            }
        };
        document.addEventListener("focusout", _symbolFocusoutHandler, true);
        try {
            if (window.parent && window.parent.document && window.parent.document !== document) {
                window.parent.document.addEventListener("focusout", _symbolFocusoutHandler, true);
            }
        } catch (e) { /* 跨域静默跳过 */ }
        try {
            formContext.ui.tabs.forEach(function (tab) {
                tab.addTabStateChange(function () {
                    setTimeout(function () { if (_currentSymbol) _applySymbolToMoneyInputs(_currentSymbol); }, 300);
                    // Bug #1816/#1817：tab 切换后子网格 DOM 重建，重跑落实子网格按钮显隐
                    setTimeout(function () { scheduleImplGridButtons(formContext); }, 400);
                });
            });
        } catch (e) { console.warn("[FSM] tabStateChange 注册失败:", e); }
        // Bug #1816/#1817：子网格数据加载完成后（行渲染会重建命令栏）重跑按钮显隐
        try {
            var implGridForBtns = formContext.getControl(IMPLEMENTATION_SUBGRID);
            if (implGridForBtns && implGridForBtns.addOnLoad) {
                implGridForBtns.addOnLoad(function () {
                    setTimeout(function () { scheduleImplGridButtons(formContext); }, 300);
                });
            }
        } catch (e) { console.warn("[FSM] 子网格 OnLoad 注册失败:", e); }

        // Bug #1559：融资资源机构多选 → 校验（启用+产品匹配）并带入机构名称/编码；融资产品变更清空重选
        var resourceIdsAttr = formContext.getAttribute("mcs_fsm_resource_ids");
        if (resourceIdsAttr) resourceIdsAttr.addOnChange(function () { onResourceIdsChanged(formContext); });
        var productAttr1559 = formContext.getAttribute("mcs_fsm_product");
        if (productAttr1559) productAttr1559.addOnChange(function () { clearResourceSelection(formContext); });
        // Bug #1559 补充：机构名称/编码 6 字段按值显隐（空组隐藏）；picker 写入后经 fireOnChange 触发
        updateInstitutionVisibility(formContext);
        INSTITUTION_FIELD_GROUPS.forEach(function (pair) {
            pair.forEach(function (f) {
                var a = formContext.getAttribute(f);
                if (a) a.addOnChange(function () { updateInstitutionVisibility(formContext); });
            });
        });
        // 存量回填：机构多选有值但名称字段全空时重新带入（覆盖 Excel/API 导入直写 resource_ids 的场景）
        if (resourceIdsAttr && resourceIdsAttr.getValue()) {
            var bankNamesAttr = formContext.getAttribute("mcs_fsm_bank_names");
            var insNamesAttr = formContext.getAttribute("mcs_fsm_insurance_names");
            var othNamesAttr = formContext.getAttribute("mcs_fsm_other_names");
            if ((!bankNamesAttr || !bankNamesAttr.getValue()) && (!insNamesAttr || !insNamesAttr.getValue())
                && (!othNamesAttr || !othNamesAttr.getValue())) {
                onResourceIdsChanged(formContext);
            }
        }

        // Bug #1507：授信金额默认取融资金额（阶段变化进入融资解决方案时也兜底默认一次）
        defaultCreditAmount(formContext);

        // Bug #1538：已在融资立项阶段的存量记录打开表单时带出融资经理
        defaultManager(formContext);

        // 按当前阶段控制 tab 字段可写 + 必填；阶段/审批状态变化时重新应用
        applyStageControl(formContext);
        var statusAttrForStage = formContext.getAttribute("mcs_fsm_status");
        if (statusAttrForStage) statusAttrForStage.addOnChange(function () {
            applyStageControl(formContext);
            syncBpfFromStatus(formContext);
            defaultCreditAmount(formContext); // Bug #1507：进入融资解决方案阶段时兜底默认授信金额
            defaultManager(formContext); // Bug #1538：进入融资立项阶段时带出融资经理
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
                    defaultManager(formContext); // Bug #1538：BPF 点「下一步」进入融资立项时带出融资经理
                    defaultCreditAmount(formContext); // Bug #1507 兜底：BPF 点「下一步」进入融资解决方案时默认授信金额
                    // Bug #1652 三次修复：回退时状态字段已在 PreStageChange 置脏，由平台导航保存落库；
                    // 若平台未触发保存（极端路径），4 秒后仍脏则兜底保存一次，防刷新后被 reconcile 推回。
                    // 切勿在此立即 data.save()：与平台导航保存并发会导致首次回退被弹回（UAT 实测需点两次）
                    var bpfNumAfter = getBpfStageNumber(formContext);
                    var statusAttrAfter = formContext.getAttribute("mcs_fsm_status");
                    var statusAfter = statusAttrAfter ? statusAttrAfter.getValue() : null;
                    if (bpfNumAfter && statusAfter && bpfNumAfter < statusAfter) {
                        setTimeout(function () {
                            try {
                                if (statusAttrAfter.getIsDirty()) {
                                    var p = formContext.data.save();
                                    if (p && p.then) p.then(null, function (e) { console.warn("[FSM] 阶段回退兜底保存失败:", e); });
                                }
                            } catch (e) { console.warn("[FSM] 阶段回退兜底保存异常:", e); }
                        }, 4000);
                    }
                });
            } catch (ex) {
                console.error("[FSM] 注册 BPF 阶段事件失败:", ex);
            }
            // 进入下一阶段前必须已通过对应阶段审批
            preventBpfNextWithoutApproval(formContext);
            // Bug #1766：BPF 点「完成」后重跑锁定（贷后管理人锁死）
            lockAfterBpfComplete(formContext);
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

        // Bug #1788（2026-08-12）：仅融资方案接口人（mcs_fsm_manager）可提交（显隐规则的点击兜底）
        if (!isCurrentUserFsmManager(formContext)) {
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_OnlyFsmManagerSubmit", "只有融资方案接口人才能提交审批。") });
            return;
        }

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

        // Bug #1788（2026-08-12）：仅融资方案接口人（mcs_fsm_manager）可提交（显隐规则的点击兜底）
        if (!isCurrentUserFsmManager(formContext)) {
            Xrm.Navigation.openAlertDialog({ text: t("FsmData_OnlyFsmManagerSubmit", "只有融资方案接口人才能提交审批。") });
            return;
        }

        // 先刷新未上表单字段的服务端缓存再校验（2026-08-05 用户反馈「要刷新后才能检测到字段有值」：
        // mcs_fsm_credit_amount_usd 由自动折算异步写库，刚填完授信金额点提交时缓存未更新会误报必填）
        refreshFieldCache(formContext).then(function () {
        // 融资解决方案提交：所有页面字段除附件外必填 + 合同号必填（PRD）（#2150 合同改多选，校验 mcs_contract_ids）
        // Bug #1578：设备台数/产品名称已移出 REQUIRED_SIX_ELEMENTS，方案审批天然不校验
        var missing = validateRequiredFields(formContext,
            REQUIRED_SIX_ELEMENTS.concat(REQUIRED_SOLUTION_EXTRA).concat([CONTRACT_IDS]));
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
        }); // refreshFieldCache 后再走校验与提交
    };

    // =====================================================================
    // Ribbon 显隐规则（经典 Ribbon DisplayRule 同步 CustomRule，2026-07-31）
    // 按 PRD：【提交立项审批】仅融资立项(2)可见；【提交融资方案审批】仅融资解决方案(3)可见；新增未保存均隐藏
    // Bug #1788（2026-08-12）：叠加「当前登录人 == 融资方案接口人（mcs_fsm_manager）」判断，
    // 融资经理等其他人员不可见；接口人为空时所有人隐藏（用户确认口径）。
    // 字段比对同步可判；安全角色判断需异步查询，同步规则做不了，故按字段控制。
    // =====================================================================
    // Bug #1788：当前登录人是否为记录的融资方案接口人（mcs_fsm_manager）
    function isCurrentUserFsmManager(formContext) {
        try {
            var attr = formContext.getAttribute("mcs_fsm_manager");
            var val = attr ? attr.getValue() : null;
            if (!val || !val.length || !val[0].id) return false; // 接口人为空：所有人不可见/不可提交
            var currentId = Xrm.Utility.getGlobalContext().userSettings.userId || "";
            return val[0].id.replace(/[{}]/g, "").toLowerCase()
                === currentId.replace(/[{}]/g, "").toLowerCase();
        } catch (e) {
            console.warn("[FSM] 接口人判断异常:", e);
            return false;
        }
    }

    function ribbonShowForStage(primaryControl, stage) {
        try {
            var formContext = primaryControl;
            if (!formContext || !formContext.data || !formContext.data.entity) return false;
            if (!formContext.data.entity.getId()) return false; // 新增未保存
            var attr = formContext.getAttribute("mcs_fsm_status");
            if (!attr || attr.getValue() !== stage) return false;
            return isCurrentUserFsmManager(formContext); // Bug #1788：仅接口人可见
        } catch (e) {
            console.warn("[FSM] Ribbon 显隐规则异常:", e);
            return false;
        }
    }

    /** Ribbon DisplayRule：【提交立项审批】仅融资立项阶段可见 */
    self.ribbonShowSubmitInitiation = function (primaryControl) {
        return ribbonShowForStage(primaryControl, FSM_STATUS.INITIATION);
    };

    /** Ribbon DisplayRule：【提交融资方案审批】仅融资解决方案阶段可见 */
    self.ribbonShowSubmitProject = function (primaryControl) {
        return ribbonShowForStage(primaryControl, FSM_STATUS.SOLUTION);
    };

    // 预加载语言包（异步，不阻塞后续逻辑）
    if (typeof LanguageHelper !== "undefined") {
        LanguageHelper.loadLanguagePack();
    }

    return self;
})();
