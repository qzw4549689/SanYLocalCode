/**
 * Account 表单 - 客户画像 Tab 显示控制（已停用）
 * 实体: account
 * 历史: 2026-07-13 上线过渡期用于在生产环境隐藏"客户画像" Tab(tab_16)；
 *       2026-09-03 信用评估已正式上线，按用户指示移除隐藏逻辑，客户画像 Tab 全环境正常显示。
 * 说明: 本文件保留为空操作，避免 account 表单 onload 绑定(AccountTagTabHelper.onLoad)报函数不存在；
 *       后续如需彻底清理，可同步移除 account 表单上的 onload 绑定并从 McsWebResource 移除本文件。
 */

var AccountTagTabHelper = AccountTagTabHelper || {};

/**
 * 表单加载事件入口（空操作：不再隐藏任何 Tab）
 */
AccountTagTabHelper.onLoad = function (executionContext) {
    // 空操作
};
