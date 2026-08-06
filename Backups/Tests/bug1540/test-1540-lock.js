/**
 * Bug #1540 修复逻辑仿真：BPF 进入融资立项（状态=2）后，融资需求+六要素全部上锁，
 * 仅报价编码(mcs_quoter_id)/合同编码(mcs_contract_id)除外。
 * 仿真 applyStageControl 中 DEMAND_AREA_FIELDS / INITIATION_TAB_METADATA_REQUIRED 的锁定矩阵。
 */
"use strict";

// ===== 从源码复制的判定逻辑（与 mcs_fsm_data.js 一致） =====
var BPP_STATUS = { APPLY: 1, IN_REVIEW: 2, APPROVED: 3, REJECTED: 4 };
var FSM_STATUS = { DEMAND: 1, INITIATION: 2, SOLUTION: 3, IMPLEMENTATION: 4 };

var DEMAND_AREA_FIELDS = [
    "mcs_leadmain_id", "mcs_quoter_id", "mcs_contract_id",
    "mcs_big_area", "mcs_country_id", "mcs_country_area",
    "mcs_division_id", "mcs_sub_company", "mcs_customer_name", "mcs_customer_id",
    "mcs_fsm_amount", "mcs_fsm_currency", "mcs_fsm_period",
    "mcs_fsm_payment_ratio", "mcs_fsm_interest_rate", "mcs_fsm_product_desc"
];
var DEMAND_INITIATION_EDITABLE = ["mcs_quoter_id", "mcs_contract_id"];
var INITIATION_TAB_METADATA_REQUIRED = ["mcs_fsm_device_count", "mcs_fsm_device_name"];

function isBppInProgress(bppStatusCode) {
    if (!bppStatusCode) return false;
    var status = ("" + bppStatusCode).toLowerCase();
    return status === "submitted" || status === "inreview" ||
           status === "pending" || status === "10" || status === "20";
}

/** 返回 { fieldName: disabled } 的锁定结果映射 */
function computeLocks(status, bppStatus, bppStatusCode) {
    var bppInReview = (bppStatus === BPP_STATUS.IN_REVIEW) || isBppInProgress(bppStatusCode);
    var lockAll = (status === FSM_STATUS.IMPLEMENTATION) || bppInReview;
    var locks = {};

    var demandMetaEditable = !lockAll && (status === FSM_STATUS.DEMAND);
    INITIATION_TAB_METADATA_REQUIRED.forEach(function (f) { locks[f] = !demandMetaEditable; });

    DEMAND_AREA_FIELDS.forEach(function (f) {
        var editable = !lockAll && (status === FSM_STATUS.DEMAND
            || (status === FSM_STATUS.INITIATION && DEMAND_INITIATION_EDITABLE.indexOf(f) >= 0));
        locks[f] = !editable;
    });
    return locks;
}

var pass = 0, fail = 0;
function check(name, actual, expected) {
    if (actual === expected) { pass++; console.log("  ✅ " + name); }
    else { fail++; console.log("  ❌ " + name + "：期望=" + expected + "，实际=" + actual); }
}
function allLocked(locks, except) {
    except = except || [];
    var bad = Object.keys(locks).filter(function (f) {
        return except.indexOf(f) < 0 && locks[f] !== true;
    });
    return bad.length === 0 ? true : ("未锁字段: " + bad.join(","));
}

console.log("【状态1 融资需求（无审批）】融资需求+六要素全部可写");
var l1 = computeLocks(FSM_STATUS.DEMAND, null, null);
check("DEMAND_AREA 16 字段 + 设备台数/产品名称共 18 字段全部可写",
    Object.keys(l1).filter(function (f) { return l1[f] === false; }).length, 18);

console.log("【状态2 融资立项（无审批）】全锁，仅报价/合同编码除外（#1540 核心）");
var l2 = computeLocks(FSM_STATUS.INITIATION, null, null);
check("报价编码 mcs_quoter_id 可写", l2["mcs_quoter_id"], false);
check("合同编码 mcs_contract_id 可写", l2["mcs_contract_id"], false);
check("商机编号 mcs_leadmain_id 锁定", l2["mcs_leadmain_id"], true);
check("设备台数 mcs_fsm_device_count 锁定", l2["mcs_fsm_device_count"], true);
check("产品名称 mcs_fsm_device_name 锁定", l2["mcs_fsm_device_name"], true);
check("其余 16 字段全部锁定", allLocked(l2, DEMAND_INITIATION_EDITABLE), true);

console.log("【状态2 立项审批中】lockAll 全锁（含报价/合同编码）");
var l2r = computeLocks(FSM_STATUS.INITIATION, BPP_STATUS.IN_REVIEW, "Submitted");
check("18 字段全部锁定", allLocked(l2r), true);

console.log("【状态3 融资解决方案】全锁（#1509 口径保持）");
check("18 字段全部锁定", allLocked(computeLocks(FSM_STATUS.SOLUTION, null, null)), true);

console.log("【状态4 融资落实】全锁");
check("18 字段全部锁定", allLocked(computeLocks(FSM_STATUS.IMPLEMENTATION, null, null)), true);

console.log("【状态1 + 审批中（防御）】lockAll 优先，全锁");
check("18 字段全部锁定", allLocked(computeLocks(FSM_STATUS.DEMAND, BPP_STATUS.IN_REVIEW, null)), true);

console.log("\n结果：" + pass + " 通过 / " + fail + " 失败");
process.exit(fail > 0 ? 1 : 0);
