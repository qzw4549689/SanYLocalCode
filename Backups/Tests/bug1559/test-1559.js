/**
 * Bug #1559 修复逻辑仿真：
 * ① 必填清单变更（四项非必填 + 机构多选/融资产品入列）
 * ② 锁定矩阵（mcs_fsm_product 仅状态1可写；resource_ids 仅状态3可写；6 带入字段始终只读）
 * ③ 机构多选分组带入（银行/保险/其它 名称+编码 逗号分隔）
 * ④ 机构过滤（停用/产品不匹配移除）
 * 逻辑复制自 mcs_fsm_data.js（#1559 版），保持处理顺序一致：
 * [INITIATION_TAB, SOLUTION_TAB] → INITIATION_TAB_METADATA_REQUIRED → DEMAND_AREA_FIELDS → SOLUTION_AUTO_FIELDS
 */
"use strict";

var BPP_STATUS = { APPLY: 1, IN_REVIEW: 2, APPROVED: 3, REJECTED: 4 };
var FSM_STATUS = { DEMAND: 1, INITIATION: 2, SOLUTION: 3, IMPLEMENTATION: 4 };

// ===== 与源码一致的清单 =====
var REQUIRED_SIX_ELEMENTS = [
    "mcs_fsm_amount", "mcs_fsm_currency", "mcs_fsm_period", "mcs_fsm_payment_ratio",
    "mcs_fsm_interest_rate", "mcs_fsm_product", "mcs_fsm_device_count", "mcs_fsm_device_name"
];
var REQUIRED_SOLUTION_EXTRA = [
    "mcs_fsm_product", "mcs_fsm_resource_ids",
    "mcs_fsm_credit_amount", "mcs_fsm_credit_amount_usd"
];
var INITIATION_TAB = {
    stage: FSM_STATUS.INITIATION,
    required: ["mcs_fsm_manager", "mcs_fsm_is_initiated", "mcs_can_initiated"],
    optional: []
};
var SOLUTION_TAB = {
    stage: FSM_STATUS.SOLUTION,
    required: ["mcs_fsm_product", "mcs_fsm_resource_ids", "mcs_fsm_credit_amount", "mcs_fsm_credit_amount_usd",
               "mcs_can_project", "mcs_is_valid"],
    optional: []
};
var SOLUTION_AUTO_FIELDS = [
    "mcs_fsm_bank_names", "mcs_fsm_bank_codes",
    "mcs_fsm_insurance_names", "mcs_fsm_insurance_codes",
    "mcs_fsm_other_names", "mcs_fsm_other_codes"
];
var INITIATION_TAB_METADATA_REQUIRED = ["mcs_fsm_device_count", "mcs_fsm_device_name"];
var DEMAND_AREA_FIELDS = [
    "mcs_leadmain_id", "mcs_quoter_id", "mcs_contract_id",
    "mcs_big_area", "mcs_country_id", "mcs_country_area",
    "mcs_division_id", "mcs_sub_company", "mcs_customer_name", "mcs_customer_id",
    "mcs_fsm_amount", "mcs_fsm_currency", "mcs_fsm_period",
    "mcs_fsm_payment_ratio", "mcs_fsm_interest_rate", "mcs_fsm_product"
];
var DEMAND_INITIATION_EDITABLE = ["mcs_quoter_id", "mcs_contract_id"];
var DEMAND_SOLUTION_EDITABLE = ["mcs_contract_id"];

function isBppInProgress(c) {
    if (!c) return false;
    var s = ("" + c).toLowerCase();
    return s === "submitted" || s === "inreview" || s === "pending" || s === "10" || s === "20";
}

/** 仿真 applyStageControl：返回 { locks: {f: disabled}, required: {f: required} } */
function computeStage(status, bppStatus, bppStatusCode) {
    var bppInReview = (bppStatus === BPP_STATUS.IN_REVIEW) || isBppInProgress(bppStatusCode);
    var lockAll = (status === FSM_STATUS.IMPLEMENTATION) || bppInReview;
    var locks = {}, required = {};

    [INITIATION_TAB, SOLUTION_TAB].forEach(function (tab) {
        var editable = !lockAll && (status === tab.stage);
        tab.required.forEach(function (f) { locks[f] = !editable; required[f] = editable; });
        tab.optional.forEach(function (f) { locks[f] = !editable; required[f] = false; });
    });

    var demandMetaEditable = !lockAll && (status === FSM_STATUS.DEMAND);
    INITIATION_TAB_METADATA_REQUIRED.forEach(function (f) { locks[f] = !demandMetaEditable; });

    DEMAND_AREA_FIELDS.forEach(function (f) {
        var editable = !lockAll && (status === FSM_STATUS.DEMAND
            || (status === FSM_STATUS.INITIATION && DEMAND_INITIATION_EDITABLE.indexOf(f) >= 0)
            || (status === FSM_STATUS.SOLUTION && DEMAND_SOLUTION_EDITABLE.indexOf(f) >= 0));
        locks[f] = !editable;
    });

    SOLUTION_AUTO_FIELDS.forEach(function (f) { locks[f] = true; });
    return { locks: locks, required: required };
}

// ===== 机构分组带入（与 fillInstitutionFields 一致） =====
function groupInstitutions(resources) {
    var g = { bankNames: [], bankCodes: [], insNames: [], insCodes: [], othNames: [], othCodes: [] };
    resources.forEach(function (r) {
        var type = r.mcs_fsm_institution_type;
        var names = type === 1 ? g.bankNames : type === 2 ? g.insNames : g.othNames;
        var codes = type === 1 ? g.bankCodes : type === 2 ? g.insCodes : g.othCodes;
        if (r.mcs_fsm_institution_name) names.push(r.mcs_fsm_institution_name);
        if (r.mcs_fsm_institution_code) codes.push(r.mcs_fsm_institution_code);
    });
    return {
        mcs_fsm_bank_names: g.bankNames.join(",") || null,
        mcs_fsm_bank_codes: g.bankCodes.join(",") || null,
        mcs_fsm_insurance_names: g.insNames.join(",") || null,
        mcs_fsm_insurance_codes: g.insCodes.join(",") || null,
        mcs_fsm_other_names: g.othNames.join(",") || null,
        mcs_fsm_other_codes: g.othCodes.join(",") || null
    };
}

// ===== 机构过滤（与 onResourceIdsChanged 内判定一致） =====
function filterResources(resources, product) {
    var valid = [], removed = [];
    resources.forEach(function (r) {
        if (!r) { removed.push("?"); return; }
        var ok = r.statecode === 0;
        if (ok && product !== null && product !== undefined) {
            var prods = r.mcs_fsm_institution_products
                ? String(r.mcs_fsm_institution_products).split(",").map(function (x) { return parseInt(x, 10); })
                : [];
            ok = prods.indexOf(product) >= 0;
        }
        if (ok) valid.push(r); else removed.push(r.mcs_fsm_institution_name || "?");
    });
    return { valid: valid, removed: removed };
}

var pass = 0, fail = 0;
function check(name, actual, expected) {
    var a = JSON.stringify(actual), e = JSON.stringify(expected);
    if (a === e) { pass++; console.log("  ✅ " + name); }
    else { fail++; console.log("  ❌ " + name + "：期望=" + e + "，实际=" + a); }
}

console.log("【1】必填清单");
check("REQUIRED_SOLUTION_EXTRA 含融资产品", REQUIRED_SOLUTION_EXTRA.indexOf("mcs_fsm_product") >= 0, true);
check("REQUIRED_SOLUTION_EXTRA 含机构多选", REQUIRED_SOLUTION_EXTRA.indexOf("mcs_fsm_resource_ids") >= 0, true);
check("REQUIRED_SOLUTION_EXTRA 含授信金额/USD", REQUIRED_SOLUTION_EXTRA.indexOf("mcs_fsm_credit_amount") >= 0 && REQUIRED_SOLUTION_EXTRA.indexOf("mcs_fsm_credit_amount_usd") >= 0, true);
["mcs_fsm_interest_discount", "mcs_fsm_fee", "mcs_fsm_repurchase_conditions", "mcs_fsm_other_conditions"].forEach(function (f) {
    check("四项非必填：EXTRA 不含 " + f, REQUIRED_SOLUTION_EXTRA.indexOf(f) < 0, true);
    check("四项非必填：SOLUTION_TAB.required 不含 " + f, SOLUTION_TAB.required.indexOf(f) < 0, true);
});
check("SIX 含新融资产品字段", REQUIRED_SIX_ELEMENTS.indexOf("mcs_fsm_product") >= 0, true);
check("SIX 不含旧文本字段", REQUIRED_SIX_ELEMENTS.indexOf("mcs_fsm_product_desc") < 0, true);
check("SOLUTION_TAB.optional 已清空旧机构字段", SOLUTION_TAB.optional.length, 0);
var submitProjectList = REQUIRED_SIX_ELEMENTS.concat(REQUIRED_SOLUTION_EXTRA).concat(["mcs_contract_id"]);
check("提交方案审批校验清单不含四项", ["mcs_fsm_interest_discount", "mcs_fsm_fee", "mcs_fsm_repurchase_conditions", "mcs_fsm_other_conditions"].every(function (f) { return submitProjectList.indexOf(f) < 0; }), true);

console.log("【2】锁定矩阵");
var s1 = computeStage(FSM_STATUS.DEMAND, null, null);
check("状态1 融资产品可写", s1.locks["mcs_fsm_product"], false);
check("状态1 机构多选锁定", s1.locks["mcs_fsm_resource_ids"], true);
check("状态1 6带入字段锁定", SOLUTION_AUTO_FIELDS.every(function (f) { return s1.locks[f] === true; }), true);
var s2 = computeStage(FSM_STATUS.INITIATION, null, null);
check("状态2 融资产品锁定（#1540 口径）", s2.locks["mcs_fsm_product"], true);
check("状态2 报价/合同可写", s2.locks["mcs_quoter_id"] === false && s2.locks["mcs_contract_id"] === false, true);
var s3 = computeStage(FSM_STATUS.SOLUTION, null, null);
check("状态3 融资产品锁定（DEMAND_AREA 兜底，方案侧只读）", s3.locks["mcs_fsm_product"], true);
check("状态3 融资产品必填星标", s3.required["mcs_fsm_product"], true);
check("状态3 机构多选可写", s3.locks["mcs_fsm_resource_ids"], false);
check("状态3 机构多选必填星标", s3.required["mcs_fsm_resource_ids"], true);
check("状态3 授信金额可写+必填", s3.locks["mcs_fsm_credit_amount"] === false && s3.required["mcs_fsm_credit_amount"] === true, true);
check("状态3 合同编号可写（#1560 口径不破）", s3.locks["mcs_contract_id"], false);
check("状态3 6带入字段锁定", SOLUTION_AUTO_FIELDS.every(function (f) { return s3.locks[f] === true; }), true);
var s4 = computeStage(FSM_STATUS.IMPLEMENTATION, null, null);
check("状态4 机构多选锁定", s4.locks["mcs_fsm_resource_ids"], true);
check("状态4 融资产品锁定", s4.locks["mcs_fsm_product"], true);
var s3r = computeStage(FSM_STATUS.SOLUTION, BPP_STATUS.IN_REVIEW, "Submitted");
check("状态3审批中 机构多选锁定", s3r.locks["mcs_fsm_resource_ids"], true);

console.log("【3】机构分组带入");
var g1 = groupInstitutions([
    { mcs_fsm_institution_type: 1, mcs_fsm_institution_name: "银行A", mcs_fsm_institution_code: "BK001" },
    { mcs_fsm_institution_type: 1, mcs_fsm_institution_name: "银行B", mcs_fsm_institution_code: "BK002" },
    { mcs_fsm_institution_type: 2, mcs_fsm_institution_name: "保险C", mcs_fsm_institution_code: "IN001" },
    { mcs_fsm_institution_type: 9, mcs_fsm_institution_name: "其它D", mcs_fsm_institution_code: "OT001" }
]);
check("银行名称逗号分隔", g1.mcs_fsm_bank_names, "银行A,银行B");
check("银行编码逗号分隔", g1.mcs_fsm_bank_codes, "BK001,BK002");
check("保险名称", g1.mcs_fsm_insurance_names, "保险C");
check("其它名称/编码", g1.mcs_fsm_other_names === "其它D" && g1.mcs_fsm_other_codes === "OT001", true);
var g2 = groupInstitutions([{ mcs_fsm_institution_type: 1, mcs_fsm_institution_name: "银行A", mcs_fsm_institution_code: "BK001" }]);
check("仅银行时保险/其它为 null", g2.mcs_fsm_insurance_names === null && g2.mcs_fsm_other_codes === null, true);
check("空选择全 null", groupInstitutions([]).mcs_fsm_bank_names === null, true);

console.log("【4】机构过滤（启用+产品匹配）");
var f1 = filterResources([
    { mcs_fsm_institution_name: "银行A", statecode: 0, mcs_fsm_institution_products: "1,2,3" },
    { mcs_fsm_institution_name: "银行B停用", statecode: 1, mcs_fsm_institution_products: "1" },
    { mcs_fsm_institution_name: "保险C无产品", statecode: 0, mcs_fsm_institution_products: "101,102" }
], 1);
check("匹配产品且启用→保留", f1.valid.length, 1);
check("停用+产品不匹配→移除 2 个", f1.removed.join(","), "银行B停用,保险C无产品");
var f2 = filterResources([{ mcs_fsm_institution_name: "银行A", statecode: 0, mcs_fsm_institution_products: "1" }], null);
check("产品未选保守放行", f2.valid.length, 1);
var f3 = filterResources([{ mcs_fsm_institution_name: "银行A", statecode: 0, mcs_fsm_institution_products: null }], 5);
check("机构无产品时按不匹配移除", f3.removed.length, 1);

console.log("\n结果：" + pass + " 通过 / " + fail + " 失败");
process.exit(fail > 0 ? 1 : 0);
