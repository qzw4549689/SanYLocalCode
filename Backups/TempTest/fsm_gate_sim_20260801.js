// 仿真验证 mcs_fsm_data.js 2026-08-01 修复：门禁判定矩阵 + syncBpfFromStatus moveNext 循环
// 逻辑从修改后的源码原样复制，常量与源码一致
const FSM_STATUS = { DEMAND: 1, INITIATION: 2, SOLUTION: 3, IMPLEMENTATION: 4 };
const APPROVE_TYPE = { INITIATION: 1, PROJECT: 2 };
const BPP_STATUS = { APPLY: 1, IN_REVIEW: 2, APPROVED: 3, REJECTED: 4 };

let _reconcilingFromStatus = false;

// ===== 门禁核心判定（与源码一致）=====
function gateCheck(direction, targetNum, approveType, bppStatus) {
    if (_reconcilingFromStatus) return { blocked: false, via: "程序化豁免" };
    if (direction !== "Next") return { blocked: false, via: "回退放行" };
    var initiationPassed = (approveType === APPROVE_TYPE.INITIATION && bppStatus === BPP_STATUS.APPROVED)
        || (approveType === APPROVE_TYPE.PROJECT);
    var projectPassed = (approveType === APPROVE_TYPE.PROJECT && bppStatus === BPP_STATUS.APPROVED);
    if (targetNum >= FSM_STATUS.SOLUTION && !initiationPassed) return { blocked: true, msg: "立项审批提示" };
    if (targetNum >= FSM_STATUS.IMPLEMENTATION && !projectPassed) return { blocked: true, msg: "方案审批提示" };
    return { blocked: false, via: "审批校验通过" };
}

let pass = 0, fail = 0;
function t(name, actual, expect) {
    const ok = JSON.stringify(actual) === JSON.stringify(expect);
    console.log((ok ? "✅" : "❌") + " " + name + " => " + JSON.stringify(actual));
    ok ? pass++ : fail++;
}

// ===== 场景矩阵 =====
t("S1 方案已通过(2,3) 进4", gateCheck("Next", 4, 2, 3), { blocked: false, via: "审批校验通过" });
t("S2 方案已通过(2,3) 进3", gateCheck("Next", 3, 2, 3), { blocked: false, via: "审批校验通过" });
t("S3 方案审批中(2,2) 进4", gateCheck("Next", 4, 2, 2), { blocked: true, msg: "方案审批提示" });
t("S4 方案审批中(2,2) 进3", gateCheck("Next", 3, 2, 2), { blocked: false, via: "审批校验通过" });
t("S5 立项已通过(1,3) 进3", gateCheck("Next", 3, 1, 3), { blocked: false, via: "审批校验通过" });
t("S6 立项已通过(1,3) 进4", gateCheck("Next", 4, 1, 3), { blocked: true, msg: "方案审批提示" });
t("S7 立项审批中(1,2) 进3", gateCheck("Next", 3, 1, 2), { blocked: true, msg: "立项审批提示" });
t("S8 未提交(null) 进3", gateCheck("Next", 3, null, null), { blocked: true, msg: "立项审批提示" });
t("S9 方案驳回(2,4) 进4", gateCheck("Next", 4, 2, 4), { blocked: true, msg: "方案审批提示" });
t("S10 回退 Previous", gateCheck("Previous", 2, null, null), { blocked: false, via: "回退放行" });
t("S11 未提交 进2", gateCheck("Next", 2, null, null), { blocked: false, via: "审批校验通过" });
_reconcilingFromStatus = true;
t("S12 reconcile 程序化豁免", gateCheck("Next", 4, 2, 2), { blocked: false, via: "程序化豁免" });
_reconcilingFromStatus = false;

// ===== syncBpfFromStatus moveNext 循环仿真 =====
function makeProcess(startIdx, stageNums, moveResults) {
    return {
        idx: startIdx,
        calls: 0,
        moveNext(cb) { this.calls++; const r = moveResults.shift() || "success"; if (r === "success") this.idx++; cb(r); },
        stageNum() { return stageNums[this.idx]; }
    };
}
function syncLoop(process, status) {
    let finished = 0;
    const stepForward = () => {
        const cur = process.stageNum();
        if (cur === null || cur >= status) { finished++; return; }
        process.moveNext(function (result) {
            if (result === "success") stepForward();
            else finished++;
        });
    };
    stepForward();
    return { 最终阶段: process.stageNum(), moveNext次数: process.calls, finish次数: finished };
}
const stages = [1, 2, 3, 4];
t("T1 BPF=2 status=4 前进2次", syncLoop(makeProcess(1, stages, ["success", "success"]), 4), { 最终阶段: 4, moveNext次数: 2, finish次数: 1 });
t("T2 BPF=3 status=4 前进1次", syncLoop(makeProcess(2, stages, ["success"]), 4), { 最终阶段: 4, moveNext次数: 1, finish次数: 1 });
t("T3 BPF=4 status=4 不动作", syncLoop(makeProcess(3, stages, []), 4), { 最终阶段: 4, moveNext次数: 0, finish次数: 1 });
t("T4 dirtyForm 中止", syncLoop(makeProcess(1, stages, ["dirtyForm"]), 4), { 最终阶段: 2, moveNext次数: 1, finish次数: 1 });
t("T5 success后invalid 中止", syncLoop(makeProcess(1, stages, ["success", "invalid"]), 4), { 最终阶段: 3, moveNext次数: 2, finish次数: 1 });
t("T6 BPF=3 status=2 不动作", syncLoop(makeProcess(2, stages, []), 2), { 最终阶段: 3, moveNext次数: 0, finish次数: 1 });

console.log(`\n===== ${pass}/${pass + fail} 通过 =====`);
process.exit(fail ? 1 : 0);
