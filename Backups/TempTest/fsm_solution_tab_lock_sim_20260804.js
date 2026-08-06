/**
 * Bug（2026-08-04 新建方案 tab 误放开）修复仿真：
 * ① SOLUTION_TAB.optional 补 4 字段后阶段控制恢复
 * ② mcs_fsm_product 双单元格按所在 tab 分别控制（tab_2 六要素=状态1 可写；tab_4 方案=状态3 可写）
 * 逻辑复制自修复后 mcs_fsm_data.js applyStageControl，保持处理顺序一致。
 */
"use strict";

var BPP_STATUS = { APPLY: 1, IN_REVIEW: 2, APPROVED: 3, REJECTED: 4 };
var FSM_STATUS = { DEMAND: 1, INITIATION: 2, SOLUTION: 3, IMPLEMENTATION: 4 };

var INITIATION_TAB = {
    stage: FSM_STATUS.INITIATION,
    required: ["mcs_fsm_manager", "mcs_fsm_is_initiated", "mcs_can_initiated"],
    optional: []
};
var SOLUTION_TAB = {
    stage: FSM_STATUS.SOLUTION,
    required: ["mcs_fsm_product", "mcs_fsm_resource_ids", "mcs_fsm_credit_amount", "mcs_fsm_credit_amount_usd",
               "mcs_can_project", "mcs_is_valid"],
    optional: ["mcs_fsm_interest_discount", "mcs_fsm_fee", "mcs_fsm_repurchase_conditions", "mcs_fsm_other_conditions"]
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

/** 仿真 applyStageControl；disabled["f"] = 属性级结果，disabled["f@tab_2"/"f@tab_4"] = 金融产品按控件覆盖 */
function computeStage(status, bppStatus, bppStatusCode) {
    var bppInReview = (bppStatus === BPP_STATUS.IN_REVIEW) || isBppInProgress(bppStatusCode);
    var lockAll = (status === FSM_STATUS.IMPLEMENTATION) || bppInReview;
    var disabled = {};

    [INITIATION_TAB, SOLUTION_TAB].forEach(function (tab) {
        var editable = !lockAll && (status === tab.stage);
        tab.required.concat(tab.optional).forEach(function (f) { disabled[f] = !editable; });
    });

    var demandMetaEditable = !lockAll && (status === FSM_STATUS.DEMAND
        || status === FSM_STATUS.INITIATION || status === FSM_STATUS.SOLUTION);
    INITIATION_TAB_METADATA_REQUIRED.forEach(function (f) { disabled[f] = !demandMetaEditable; });

    DEMAND_AREA_FIELDS.forEach(function (f) {
        var editable = !lockAll && (status === FSM_STATUS.DEMAND
            || (status === FSM_STATUS.INITIATION && DEMAND_INITIATION_EDITABLE.indexOf(f) >= 0)
            || (status === FSM_STATUS.SOLUTION && DEMAND_SOLUTION_EDITABLE.indexOf(f) >= 0));
        disabled[f] = !editable;
    });

    SOLUTION_AUTO_FIELDS.forEach(function (f) { disabled[f] = true; });

    // 新增：金融产品双单元格按 tab 分别控制（最后执行，覆盖属性级结果）
    var productDemandEditable = !lockAll && status === FSM_STATUS.DEMAND;
    var productSolutionEditable = !lockAll && status === FSM_STATUS.SOLUTION;
    disabled["mcs_fsm_product@tab_2"] = !productDemandEditable;
    disabled["mcs_fsm_product@tab_4"] = !productSolutionEditable;

    return disabled;
}

var pass = 0, fail = 0;
function check(name, actual, expected) {
    if (actual === expected) { pass++; }
    else { fail++; console.log("  ❌ " + name + ": 期望 disabled=" + expected + ", 实际=" + actual); }
}

var SOLUTION_FIELDS = ["mcs_fsm_credit_amount", "mcs_fsm_credit_amount_usd", "mcs_fsm_resource_ids",
    "mcs_fsm_interest_discount", "mcs_fsm_fee", "mcs_fsm_repurchase_conditions", "mcs_fsm_other_conditions"];

function scenario(label, status, bppStatus, bppCode, expect) {
    var d = computeStage(status, bppStatus, bppCode);
    console.log(label);
    SOLUTION_FIELDS.forEach(function (f) { check("  " + f, d[f], expect.solutionFields); });
    check("  mcs_fsm_product@tab_2(六要素)", d["mcs_fsm_product@tab_2"], expect.productSix);
    check("  mcs_fsm_product@tab_4(方案)", d["mcs_fsm_product@tab_4"], expect.productSolution);
}

scenario("状态1/新建：方案 tab 全锁，六要素融资产品可写", 1, null, null,
    { solutionFields: true, productSix: false, productSolution: true });
scenario("状态2：方案 tab 全锁，融资产品两格全锁", 2, null, null,
    { solutionFields: true, productSix: true, productSolution: true });
scenario("状态3：方案 tab 放开，六要素格锁定", 3, null, null,
    { solutionFields: false, productSix: true, productSolution: false });
scenario("状态4：全锁", 4, null, null,
    { solutionFields: true, productSix: true, productSolution: true });
scenario("状态3+审批中：全锁", 3, 2, "Submitted",
    { solutionFields: true, productSix: true, productSolution: true });

console.log("\n结果: " + pass + " 通过, " + fail + " 失败");
process.exit(fail > 0 ? 1 : 0);
