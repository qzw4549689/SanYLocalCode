/**
 * 融资落实子网格显隐仿真（2026-08-06 用户需求：未到融资落实阶段不允许新增融资落实）
 * 逻辑复制自 mcs_fsm_data.js applyStageControl 新增段：
 *   implGrid.setVisible(status === FSM_STATUS.IMPLEMENTATION)
 * 期望：状态 1/2/3 隐藏；状态 4 显示；审批中（bppstatus=2）不改变显隐口径（仍按阶段）
 */
"use strict";

var FSM_STATUS = { DEMAND: 1, INITIATION: 2, SOLUTION: 3, IMPLEMENTATION: 4 };

function subgridVisible(status) {
    return status === FSM_STATUS.IMPLEMENTATION;
}

var pass = 0, fail = 0;
function check(name, actual, expected) {
    if (actual === expected) { pass++; console.log("PASS " + name); }
    else { fail++; console.log("FAIL " + name + " 期望=" + expected + " 实际=" + actual); }
}

check("状态1 融资需求 隐藏", subgridVisible(FSM_STATUS.DEMAND), false);
check("状态2 融资立项 隐藏", subgridVisible(FSM_STATUS.INITIATION), false);
check("状态3 融资解决方案 隐藏", subgridVisible(FSM_STATUS.SOLUTION), false);
check("状态4 融资落实 显示", subgridVisible(FSM_STATUS.IMPLEMENTATION), true);

console.log("结果: " + pass + " 通过, " + fail + " 失败");
process.exit(fail === 0 ? 0 : 1);
