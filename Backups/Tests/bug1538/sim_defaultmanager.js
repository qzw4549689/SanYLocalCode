// Bug #1538 defaultManager 逻辑仿真
const FSM_STATUS = { DEMAND: 1, INITIATION: 2, SOLUTION: 3, IMPLEMENTATION: 4 };

// 与被测文件一致的函数实现
function makeCtx(stageValue, managerValue) {
    const calls = [];
    global.Xrm = { Utility: { getGlobalContext: () => ({ userSettings: { userId: "uid-1", userName: "邱正卫" } }) } };
    return {
        calls,
        getAttribute: (name) => {
            if (name === "mcs_fsm_manager") return {
                getValue: () => managerValue,
                setValue: (v) => calls.push(v)
            };
            if (name === "mcs_fsm_status") return { getValue: () => stageValue };
            return null;
        },
        data: { process: null } // 无 BPF 时 getBpfStageNumber 返回 null，走 status 字段
    };
}
function getBpfStageNumber(fc) { return null; } // 仿真无 BPF 场景
function getCurrentStage(fc) {
    var bpfNum = getBpfStageNumber(fc);
    if (bpfNum) return bpfNum;
    var a = fc.getAttribute("mcs_fsm_status");
    return (a && a.getValue()) ? a.getValue() : FSM_STATUS.DEMAND;
}
function defaultManager(formContext) {
    if (getCurrentStage(formContext) !== FSM_STATUS.INITIATION) return;
    var managerAttr = formContext.getAttribute("mcs_fsm_manager");
    if (!managerAttr || managerAttr.getValue()) return;
    var userSettings = Xrm.Utility.getGlobalContext().userSettings;
    managerAttr.setValue([{ id: userSettings.userId, entityType: "systemuser", name: userSettings.userName }]);
}

let pass = 0, fail = 0;
function check(name, cond) { cond ? pass++ : fail++; console.log((cond ? "✅" : "❌") + " " + name); }

// 场景1：阶段2 + 经理为空 → 带出当前登录人
let c1 = makeCtx(2, null); defaultManager(c1);
check("阶段2+空 → 带出当前登录人", c1.calls.length === 1 && c1.calls[0][0].id === "uid-1" && c1.calls[0][0].entityType === "systemuser");

// 场景2：阶段1（融资需求）→ 不带出
let c2 = makeCtx(1, null); defaultManager(c2);
check("阶段1 → 不带出", c2.calls.length === 0);

// 场景3：阶段3 → 不带出
let c3 = makeCtx(3, null); defaultManager(c3);
check("阶段3 → 不带出", c3.calls.length === 0);

// 场景4：阶段2 + 经理已有值（用户手改/存量）→ 不覆盖
let c4 = makeCtx(2, [{ id: "other", entityType: "systemuser", name: "张三" }]); defaultManager(c4);
check("阶段2+已有值 → 不覆盖", c4.calls.length === 0);

// 场景5：状态为空（新建）→ 不带出
let c5 = makeCtx(null, null); defaultManager(c5);
check("新建无状态 → 不带出", c5.calls.length === 0);

console.log(`\n结果: ${pass} 通过 / ${fail} 失败`);
process.exit(fail ? 1 : 0);
