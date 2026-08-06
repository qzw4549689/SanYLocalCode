/**
 * Bug #1508 修复逻辑仿真：提交融资方案审批后表单字段应立即锁定；审批中记录打开表单应锁定
 * 仿真 applyStageControl 的判定输入（bppInReview）在三处时序下的结果
 */
"use strict";

// ===== 从源码复制的判定逻辑（与 mcs_fsm_data.js 一致） =====
var BPP_STATUS = { APPLY: 1, IN_REVIEW: 2, APPROVED: 3, REJECTED: 4 };
var FSM_STATUS = { DEMAND: 1, INITIATION: 2, SOLUTION: 3, IMPLEMENTATION: 4 };

function isBppInProgress(bppStatusCode) {
    if (!bppStatusCode) return false;
    var status = ("" + bppStatusCode).toLowerCase();
    return status === "submitted" || status === "inreview" ||
           status === "pending" || status === "10" || status === "20";
}

// 模拟 getFieldValue：mcs_bppstatus 未上表单 → 读缓存；mcs_bppstatuscode 在表单上
function makeCtx(cache, bppstatuscodeOnForm, status) {
    return { cache: cache, bppstatuscode: bppstatuscodeOnForm, status: status };
}
function getFieldValue(ctx, fieldName) {
    if (fieldName === "mcs_bppstatuscode") return ctx.bppstatuscode; // 在表单上
    return (ctx.cache[fieldName] !== undefined) ? ctx.cache[fieldName] : null;
}
function computeLockAll(ctx) {
    var bppStatus = getFieldValue(ctx, "mcs_bppstatus");
    var bppCode = getFieldValue(ctx, "mcs_bppstatuscode");
    var bppInReview = (bppStatus === BPP_STATUS.IN_REVIEW) || isBppInProgress(bppCode);
    return (ctx.status === FSM_STATUS.IMPLEMENTATION) || bppInReview;
}

var pass = 0, fail = 0;
function check(name, actual, expected) {
    if (actual === expected) { pass++; console.log("  ✅ " + name); }
    else { fail++; console.log("  ❌ " + name + "：期望 lockAll=" + expected + "，实际=" + actual); }
}

console.log("【场景1】onLoad 同步执行 applyStageControl（缓存未填充）");
var ctx1 = makeCtx({}, "Submitted", FSM_STATUS.SOLUTION);
check("缓存空 + bppstatuscode=Submitted(表单上) → 仍靠兜底锁定", computeLockAll(ctx1), true);
var ctx1b = makeCtx({}, null, FSM_STATUS.SOLUTION);
check("缓存空 + bppstatuscode 也为空(回调前) → 暂不锁（待缓存就绪重放）", computeLockAll(ctx1b), false);

console.log("【场景2】onLoad 异步缓存就绪后重放 applyStageControl（修复点②）");
check("缓存 bppstatus=2 → 锁定", computeLockAll(makeCtx({ mcs_bppstatus: 2, mcs_approve_type: 2 }, null, FSM_STATUS.SOLUTION)), true);
check("缓存 bppstatus=1(申请) → 不锁", computeLockAll(makeCtx({ mcs_bppstatus: 1, mcs_approve_type: 2 }, null, FSM_STATUS.SOLUTION)), false);
check("审批已通过且状态=4 → 锁定", computeLockAll(makeCtx({ mcs_bppstatus: 3, mcs_approve_type: 2 }, "Approved", FSM_STATUS.IMPLEMENTATION)), true);
check("审批驳回且状态=3 → 不锁（可修改后重新提交）", computeLockAll(makeCtx({ mcs_bppstatus: 4, mcs_approve_type: 2 }, "Rejected", FSM_STATUS.SOLUTION)), false);

console.log("【场景3】提交成功后立即更新缓存并锁定（修复点①）");
var ctx3 = makeCtx({}, null, FSM_STATUS.SOLUTION);
ctx3.cache["mcs_bppstatus"] = BPP_STATUS.IN_REVIEW;
ctx3.cache["mcs_approve_type"] = 2;
check("提交后缓存 bppstatus=2 → 立即锁定", computeLockAll(ctx3), true);

console.log("【场景4】提交后 data.refresh + 缓存刷新重放（服务端 bppstatuscode=Submitted）");
check("refresh 后双通道均为审批中 → 锁定", computeLockAll(makeCtx({ mcs_bppstatus: 2, mcs_approve_type: 2 }, "Submitted", FSM_STATUS.SOLUTION)), true);

console.log("【场景5】普通阶段控制回归（不受修复影响）");
check("状态=2 无审批 → 不锁", computeLockAll(makeCtx({ mcs_bppstatus: 1 }, null, FSM_STATUS.INITIATION)), false);
check("状态=2 立项审批中 → 锁", computeLockAll(makeCtx({ mcs_bppstatus: 2 }, "Pending", FSM_STATUS.INITIATION)), true);
check("状态=4 无审批 → 锁", computeLockAll(makeCtx({}, null, FSM_STATUS.IMPLEMENTATION)), true);

console.log("\n结果：" + pass + " 通过 / " + fail + " 失败");
process.exit(fail > 0 ? 1 : 0);
