// Node 仿真：验证 mcs_fca_quotaapp.js 「模型计算额度默认带到厂端授信额度调整为」改动
// 运行：node sim_test.js
"use strict";
const fs = require("fs");
const vm = require("vm");

// ============ 全局桩 ============
let alerts = [];
let canned = {}; // 可配置的查询结果
let deferredQuota = null; // 手动控制的 quota 查询 promise（用于乱序场景）

global.Xrm = {
    Utility: { getGlobalContext: () => ({ getClientUrl: () => "https://stub", userSettings: { userId: null } }) },
    Navigation: { openAlertDialog: (o) => { alerts.push(o.text); } },
    WebApi: {
        retrieveRecord: (entity, id, query) => {
            if (entity === "mcs_customermasterdata") return Promise.resolve({ mcs_sapnumber: "C001", mcs_creditgrade: "A2" });
            if (entity === "mcs_fca_proc") return Promise.resolve(canned.procRecord);
            return Promise.resolve({});
        },
        retrieveMultipleRecords: (entity, query) => {
            if (entity === "mcs_fca_quota") {
                if (deferredQuota) return deferredQuota.promise;
                return Promise.resolve({ entities: canned.quotaEntities });
            }
            if (entity === "mcs_fca_proc") return Promise.resolve({ entities: canned.procEntities });
            return Promise.resolve({ entities: [] });
        }
    }
};

// 加载被测 JS（顶层 IIFE 引用 XMLHttpRequest 会抛错但被其 try/catch 吞掉）
vm.runInThisContext(fs.readFileSync(__dirname + "/../../../Code/Customizations/WebResources/JS/mcs_fca_quotaapp.js", "utf8"), { filename: "mcs_fca_quotaapp.js" });

// ============ formContext 桩 ============
function makeAttr(v) {
    return {
        value: v === undefined ? null : v,
        handlers: [],
        getValue() { return this.value; },
        setValue(x) { this.value = x; }, // D365 行为：setValue 不触发 onChange
        addOnChange(fn) { this.handlers.push(fn); },
        fire() { this.handlers.forEach(h => h()); } // 模拟用户界面修改
    };
}

const FIELD_NAMES = ["mcs_accountid", "mcs_doid", "mcs_initigrant", "mcs_tobegrant", "mcs_tobebalance",
    "mcs_sellergrant", "mcs_sellerbalance", "mcs_bppstatus", "mcs_custname", "mcs_creditgrade",
    "mcs_applygenre", "mcs_quotasum", "mcs_quotabalance", "mcs_reason"];

function newFormContext() {
    const attrs = {};
    FIELD_NAMES.forEach(n => attrs[n] = makeAttr());
    const formContext = {
        getAttribute: n => attrs[n] || null,
        getControl: () => null,
        ui: { getFormType: () => 1, controls: { get: () => [] } },
        data: { entity: { getId: () => "" } },
        _attrs: attrs
    };
    return { formContext, executionContext: { getFormContext: () => formContext } };
}

const ACCOUNT_ID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
const PROC_ID = "11111111-2222-3333-4444-555555555555";
const ACCOUNT_LOOKUP = [{ id: ACCOUNT_ID, name: "TCE", entityType: "mcs_customermasterdata" }];
const PROC_LOOKUP = [{ id: PROC_ID, name: "FCM001", entityType: "mcs_fca_proc" }];

function flush(times = 10) {
    let p = Promise.resolve();
    for (let i = 0; i < times; i++) p = p.then(() => { });
    return p;
}

let pass = 0, fail = 0;
function check(name, actual, expected) {
    const ok = JSON.stringify(actual) === JSON.stringify(expected);
    if (ok) { pass++; console.log(`✅ ${name}`); }
    else { fail++; console.log(`❌ ${name}  期望=${JSON.stringify(expected)} 实际=${JSON.stringify(actual)}`); }
}

(async function main() {
    // ---------- 场景1：选客户（有额度10万/余额8万 + 有生效proc 50万）→ 调整为默认=模型额度50万 ----------
    {
        canned = {
            quotaEntities: [{ mcs_sellergrant: 100000, mcs_sellerbalance: 80000 }],
            procEntities: [{ mcs_fca_procid: PROC_ID, mcs_doid: "FCM001", mcs_initigrant: 500000 }],
            procRecord: { mcs_initigrant: 500000, _mcs_accountid_value: ACCOUNT_ID }
        };
        const { formContext, executionContext } = newFormContext();
        FcaQuotaAppForm.onLoad(executionContext);
        formContext._attrs["mcs_accountid"].setValue(ACCOUNT_LOOKUP);
        formContext._attrs["mcs_accountid"].fire();
        await flush();
        check("S1 模型计算额度带出", formContext._attrs["mcs_initigrant"].getValue(), 500000);
        check("S1 调整为默认=模型额度", formContext._attrs["mcs_tobegrant"].getValue(), 500000);
        check("S1 调整后余额=50万-10万+8万", formContext._attrs["mcs_tobebalance"].getValue(), 480000);

        // ---------- 场景2：用户手工调额 60万 → 余额重算，不覆盖 ----------
        formContext._attrs["mcs_tobegrant"].setValue(600000);
        formContext._attrs["mcs_tobegrant"].fire();
        await flush();
        check("S2 手工调额保留", formContext._attrs["mcs_tobegrant"].getValue(), 600000);
        check("S2 调整后余额=60万-10万+8万", formContext._attrs["mcs_tobebalance"].getValue(), 580000);

        // ---------- 场景3：手工换序列号（proc 30万）→ 调整为被新模型额度覆盖 ----------
        canned.procRecord = { mcs_initigrant: 300000, _mcs_accountid_value: ACCOUNT_ID };
        formContext._attrs["mcs_doid"].setValue([{ id: "99999999-8888-7777-6666-555555555555", name: "FCM002", entityType: "mcs_fca_proc" }]);
        formContext._attrs["mcs_doid"].fire();
        await flush();
        check("S3 换序列号后模型额度", formContext._attrs["mcs_initigrant"].getValue(), 300000);
        check("S3 调整为覆盖=新模型额度", formContext._attrs["mcs_tobegrant"].getValue(), 300000);
        check("S3 调整后余额=30万-10万+8万", formContext._attrs["mcs_tobebalance"].getValue(), 280000);

        // ---------- 场景4：手工清空序列号 → 调整为回退=当前厂端授信额度 ----------
        formContext._attrs["mcs_doid"].setValue(null);
        formContext._attrs["mcs_doid"].fire();
        await flush();
        check("S4 清空后模型额度归0", formContext._attrs["mcs_initigrant"].getValue(), 0);
        check("S4 调整为回退=当前额度10万", formContext._attrs["mcs_tobegrant"].getValue(), 100000);
        check("S4 调整后余额=当前余额8万", formContext._attrs["mcs_tobebalance"].getValue(), 80000);
    }

    // ---------- 场景5：序列号客户不一致 → 拦截+清空+调整为回退 ----------
    {
        alerts = [];
        canned = {
            quotaEntities: [{ mcs_sellergrant: 100000, mcs_sellerbalance: 80000 }],
            procEntities: [],
            procRecord: { mcs_initigrant: 500000, _mcs_accountid_value: "ffffffff-0000-0000-0000-000000000000" }
        };
        const { formContext, executionContext } = newFormContext();
        FcaQuotaAppForm.onLoad(executionContext);
        formContext._attrs["mcs_accountid"].setValue(ACCOUNT_LOOKUP);
        formContext._attrs["mcs_accountid"].fire();
        await flush();
        formContext._attrs["mcs_doid"].setValue(PROC_LOOKUP);
        formContext._attrs["mcs_doid"].fire();
        await flush();
        check("S5 弹出客户不一致提示", alerts.length > 0 && alerts[0].indexOf("不一致") >= 0, true);
        check("S5 序列号已清空", formContext._attrs["mcs_doid"].getValue(), null);
        check("S5 模型额度归0", formContext._attrs["mcs_initigrant"].getValue(), 0);
        check("S5 调整为回退=当前额度10万", formContext._attrs["mcs_tobegrant"].getValue(), 100000);
    }

    // ---------- 场景6：无额度记录 + 无生效 proc（截图场景的客户形态）→ 维持 0 ----------
    {
        canned = { quotaEntities: [], procEntities: [], procRecord: {} };
        const { formContext, executionContext } = newFormContext();
        FcaQuotaAppForm.onLoad(executionContext);
        formContext._attrs["mcs_accountid"].setValue(ACCOUNT_LOOKUP);
        formContext._attrs["mcs_accountid"].fire();
        await flush();
        check("S6 无额度无proc 调整为=0", formContext._attrs["mcs_tobegrant"].getValue(), 0);
        check("S6 无额度无proc 余额=0", formContext._attrs["mcs_tobebalance"].getValue(), 0);
    }

    // ---------- 场景7：异步乱序——proc 回调先返回、quota 回调后返回 → 调整为不被覆盖回当前额度 ----------
    {
        canned = {
            quotaEntities: [{ mcs_sellergrant: 100000, mcs_sellerbalance: 80000 }],
            procEntities: [{ mcs_fca_procid: PROC_ID, mcs_doid: "FCM001", mcs_initigrant: 500000 }],
            procRecord: {}
        };
        deferredQuota = {};
        deferredQuota.promise = new Promise(res => { deferredQuota.resolve = res; });
        const { formContext, executionContext } = newFormContext();
        FcaQuotaAppForm.onLoad(executionContext);
        formContext._attrs["mcs_accountid"].setValue(ACCOUNT_LOOKUP);
        formContext._attrs["mcs_accountid"].fire();
        await flush(); // proc 回调已执行（doid/initigrant/tobegrant=50万），quota 仍挂起
        check("S7 proc先回调 调整为=50万", formContext._attrs["mcs_tobegrant"].getValue(), 500000);
        deferredQuota.resolve({ entities: canned.quotaEntities });
        deferredQuota = null;
        await flush();
        check("S7 quota后回调 调整为仍=50万", formContext._attrs["mcs_tobegrant"].getValue(), 500000);
        check("S7 当前额度正常带出", formContext._attrs["mcs_sellergrant"].getValue(), 100000);
        check("S7 调整后余额=50万-10万+8万", formContext._attrs["mcs_tobebalance"].getValue(), 480000);
    }

    console.log(`\n结果: ${pass} 通过, ${fail} 失败`);
    process.exit(fail > 0 ? 1 : 0);
})().catch(e => { console.error("仿真异常:", e); process.exit(1); });
