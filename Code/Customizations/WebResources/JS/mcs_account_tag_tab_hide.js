/**
 * Account 表单 - 隐藏客户标签 Tab
 * 实体: account
 * 用途: 仅在 DEV1 环境隐藏"客户标签" Tab，测试稳定后切换为生产环境判断
 */

var AccountTagTabHelper = AccountTagTabHelper || {};

/**
 * 表单加载事件入口
 * 兼容"传递执行上下文"和"不传递"两种绑定方式
 */
AccountTagTabHelper.onLoad = function (executionContext) {
    try {
        var formContext;

        // 方式1：表单事件正确传递了 executionContext（推荐）
        if (executionContext && typeof executionContext.getFormContext === "function") {
            formContext = executionContext.getFormContext();
        }
        // 方式2：未传递执行上下文，使用 Xrm.Page 兜底
        else if (typeof Xrm !== "undefined" && Xrm.Page) {
            formContext = Xrm.Page;
        }
        else {
            console.error("AccountTagTabHelper.onLoad: 无法获取 formContext");
            return;
        }

        // 当前仅在生产环境生效
        if (AccountTagTabHelper.isProductionEnvironment()) {
            AccountTagTabHelper.hideCustomerTagTab(formContext);
        }
    } catch (e) {
        console.error("AccountTagTabHelper.onLoad 执行异常: " + e.message);
    }
};

/**
 * 判断当前是否为 DEV1 环境
 */
AccountTagTabHelper.isDevEnvironment = function () {
    try {
        var globalContext = typeof Xrm !== "undefined" && Xrm.Utility ? Xrm.Utility.getGlobalContext() : null;
        if (!globalContext) return false;

        var clientUrl = globalContext.getClientUrl().toLowerCase();
        return clientUrl.indexOf("dev1.crm5.dynamics.com") >= 0;
    } catch (e) {
        console.error("环境判断失败: " + e.message);
        return false;
    }
};

/**
 * 判断当前是否为生产环境
 * 正式上线时，把 onLoad 中调用的 isDevEnvironment 替换为此方法
 */
AccountTagTabHelper.isProductionEnvironment = function () {
    try {
        var globalContext = typeof Xrm !== "undefined" && Xrm.Utility ? Xrm.Utility.getGlobalContext() : null;
        if (!globalContext) return false;

        var clientUrl = globalContext.getClientUrl().toLowerCase();
        // 生产: sany.crm5.dynamics.com，排除 UAT: sany-uat.crm5.dynamics.com
        return clientUrl.indexOf("sany.crm5.dynamics.com") >= 0 &&
               clientUrl.indexOf("sany-uat") < 0;
    } catch (e) {
        console.error("环境判断失败: " + e.message);
        return false;
    }
};

/**
 * 隐藏客户标签 Tab 页
 */
AccountTagTabHelper.hideCustomerTagTab = function (formContext) {
    try {
        if (!formContext || !formContext.ui || !formContext.ui.tabs) {
            console.error("formContext.ui.tabs 不可用");
            return;
        }

        // 先输出所有 Tab 名称，便于调试
        console.log("===== Account 表单 Tab 列表 =====");
        formContext.ui.tabs.forEach(function (tab) {
            console.log("Tab name=" + tab.getName() + ", label=" + tab.getLabel());
        });
        console.log("================================");

        var found = false;
        formContext.ui.tabs.forEach(function (tab) {
            var name = tab.getName() || "";
            var label = tab.getLabel() || "";

            // 优先按 Tab name 匹配，避免多语言环境下 label 不一致问题
            if (name === "tab_16") {
                tab.setVisible(false);
                found = true;
                console.log("已隐藏客户画像 Tab, name=" + name + ", label=" + label);
            }
        });

        if (!found) {
            console.log("未找到 name='tab_16' 的 Tab");
        }
    } catch (e) {
        console.error("隐藏客户标签 Tab 失败: " + e.message);
    }
};
