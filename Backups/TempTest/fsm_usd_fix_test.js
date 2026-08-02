// mcs_fsm_data.js 修复逻辑 Node 仿真验证（validateRequiredFields 缓存回退 + updateCreditAmountUsd 折算）
// 临时验证脚本，验证后可删除
const fs = require('fs');
const file = '/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/WebResources/JS/mcs_fsm_data.js';
let src = fs.readFileSync(file, 'utf8');

// ---- 全局桩 ----
const updates = [];   // 记录 Xrm.WebApi.updateRecord 调用
let serverUsd = null; // 服务端 usd 值（模拟）
const RATE = 0.3769;  // BHD 汇率

global.window = {};
global.document = { createElement: () => ({ setAttribute(){} }), head: { appendChild(){} }, getElementsByTagName: () => [] };
global.Xrm = {
    Utility: { getGlobalContext: () => ({ userSettings: { userId: 'u1', userName: 'Tester' } }) },
    Navigation: { openAlertDialog: (o) => { console.log('  [AlertDialog]', o.text); return Promise.resolve(); }, openConfirmDialog: () => Promise.resolve({ confirmed: false }) },
    WebApi: {
        retrieveRecord: (etn, id, select) => {
            if (etn === 'transactioncurrency') return Promise.resolve({ exchangerate: RATE });
            if (etn === 'mcs_fsm_data') {
                const r = {};
                if (select.includes('mcs_fsm_credit_amount_usd')) r['mcs_fsm_credit_amount_usd'] = serverUsd;
                return Promise.resolve(r);
            }
            return Promise.reject(new Error('unexpected retrieve ' + etn));
        },
        updateRecord: (etn, id, data) => {
            updates.push(data);
            if (data.mcs_fsm_credit_amount_usd !== undefined) serverUsd = data.mcs_fsm_credit_amount_usd;
            return Promise.resolve();
        }
    }
};

// ---- 表单上下文桩：授信金额/币种在表单上，usd 不在表单上 ----
function makeFormContext(amount, currencyId) {
    const attrs = {
        'mcs_fsm_credit_amount': { getValue: () => amount },
        'mcs_fsm_currency': { getValue: () => currencyId ? [{ id: currencyId, entityType: 'transactioncurrency', name: 'BHD' }] : null },
    };
    return {
        getAttribute: (f) => attrs[f] || null,   // usd 字段返回 null（不在表单上）
        getControl: () => null,
        data: { entity: { getId: () => '{28b766de-4ab9-4af6-872d-176aa3348af9}' } }
    };
}

// ---- 加载脚本：在 return self 前导出内部函数供测试 ----
src = src.replace('    return self;', '    self.__test = { validateRequiredFields, updateCreditAmountUsd, refreshFieldCache, REQUIRED_SOLUTION_EXTRA, getFieldValue };\n    return self;');
eval(src);

(async () => {
    const T = FsmDataForm.__test;
    const fc = makeFormContext(234234234, '7e7f9444-6fc5-ee11-9079-000d3aa3f731');

    console.log('== TC1: usd 服务端有值 + 不在表单上 -> 校验应通过 ==');
    serverUsd = 621475813.21;
    await T.refreshFieldCache(fc);
    let missing = T.validateRequiredFields(fc, ['mcs_fsm_credit_amount_usd']);
    console.log('  missing =', JSON.stringify(missing), missing.length === 0 ? 'PASS' : 'FAIL');

    console.log('== TC2: usd 服务端为空 -> 校验应拦截（兜底仍生效）==');
    serverUsd = null;
    await T.refreshFieldCache(fc);
    missing = T.validateRequiredFields(fc, ['mcs_fsm_credit_amount_usd']);
    console.log('  missing =', JSON.stringify(missing), missing.length === 1 ? 'PASS' : 'FAIL');

    console.log('== TC3: 授信金额 234234234 BHD(0.3769) -> 自动折算写入 621475813.21 ==');
    updates.length = 0;
    T.updateCreditAmountUsd(fc);
    await new Promise(r => setTimeout(r, 200));
    console.log('  updateRecord =', JSON.stringify(updates), updates.length === 1 && updates[0].mcs_fsm_credit_amount_usd === 621475813.21 ? 'PASS' : 'FAIL');

    console.log('== TC4: 值未变化 -> 不重复写入 ==');
    updates.length = 0;
    T.updateCreditAmountUsd(fc);            // 缓存已有 621475813.21
    await new Promise(r => setTimeout(r, 200));
    console.log('  updateRecord 次数 =', updates.length, updates.length === 0 ? 'PASS' : 'FAIL');

    console.log('== TC5: 金额改为 1000000 -> 重算 2653807.38 ==');
    const fc2 = makeFormContext(1000000, '7e7f9444-6fc5-ee11-9079-000d3aa3f731');
    updates.length = 0;
    T.updateCreditAmountUsd(fc2);
    await new Promise(r => setTimeout(r, 200));
    console.log('  updateRecord =', JSON.stringify(updates), updates.length === 1 && Math.abs(updates[0].mcs_fsm_credit_amount_usd - 2653807.38) < 0.01 ? 'PASS' : 'FAIL');

    console.log('== TC6: 币种为空 -> 不写不报错 ==');
    const fc3 = makeFormContext(1000000, null);
    updates.length = 0;
    T.updateCreditAmountUsd(fc3);
    await new Promise(r => setTimeout(r, 100));
    console.log('  updateRecord 次数 =', updates.length, updates.length === 0 ? 'PASS' : 'FAIL');
})();
