/**
 * 客户主数据表(mcs_customermasterdata) - 信用评估扩展表单逻辑
 * 实体: mcs_customermasterdata
 * 功能: Coface字段展示、信用评估信息展示、校验
 * 影响范围：仅限mcs_customermasterdata实体的信用评估相关字段
 */

// 同步加载多语言帮助类（实验阶段，验证通过后可改为窗体依赖库）
(function () {
    if (typeof LanguageHelper !== "undefined") return;
    try {
        var req = new XMLHttpRequest();
        req.open("GET", Xrm.Utility.getGlobalContext().getClientUrl() + "/WebResources/mcs_language_helper.js", false);
        req.send();
        if (req.status === 200) {
            // 通过 script 标签注入，确保 LanguageHelper 定义在全局作用域
            var script = document.createElement("script");
            script.type = "text/javascript";
            script.text = req.responseText;
            document.getElementsByTagName("head")[0].appendChild(script);
        } else {
            console.warn("mcs_language_helper.js 加载失败，状态码:", req.status);
        }
    } catch (e) {
        console.error("加载 mcs_language_helper.js 异常:", e);
    }
})();

var CustomerMasterDataCreditForm = CustomerMasterDataCreditForm || {};

/**
 * 多语言取词（带中文兜底）
 * 语言包已加载时返回对应语言文本；未加载/未找到时返回原中文，保证中文用户不受影响
 */
CustomerMasterDataCreditForm.L = function (key, defaultText) {
    if (typeof LanguageHelper !== "undefined") {
        var v = LanguageHelper.getLabel(key);
        if (v && v !== key) return v;
    }
    return defaultText;
};

/**
 * 表单加载事件
 */
CustomerMasterDataCreditForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();

    // 预加载语言包；信用状态提示在语言包就绪后的回调中渲染，避免英文用户在 onLoad 阶段看到中文兜底
    if (typeof LanguageHelper !== "undefined") {
        LanguageHelper.loadLanguagePack(function () {
            CustomerMasterDataCreditForm.showCreditStatusHint(formContext);
        });
    }

    var formType = formContext.ui.getFormType();

    // 设置信用评估相关字段只读
    CustomerMasterDataCreditForm.setCreditFieldsReadOnly(formContext);

    // 注册字段变更事件
    CustomerMasterDataCreditForm.registerEvents(formContext);

    // 显示信用评估状态提示（无语言包时立即渲染；有语言包时由上方回调渲染）
    if (typeof LanguageHelper === "undefined") {
        CustomerMasterDataCreditForm.showCreditStatusHint(formContext);
    }
};

/**
 * 设置信用评估相关字段只读
 * 这些字段由评估流程自动更新，不允许手工修改
 */
CustomerMasterDataCreditForm.setCreditFieldsReadOnly = function (formContext) {
    // 信用评估相关字段（由评估流程自动更新）
    var creditFields = [
        "mcs_creditscore",      // 客户信用评分
        "mcs_creditgrade",      // 客户等级
        "mcs_creditvalid",      // 信用评估有效状态
        "mcs_externalrate"      // 客户信用外部评级
    ];

    creditFields.forEach(function (fieldName) {
        var control = formContext.getControl(fieldName);
        if (control) {
            control.setDisabled(true);
        }
    });
};

/**
 * 注册字段变更事件
 */
CustomerMasterDataCreditForm.registerEvents = function (formContext) {
    // Coface ID变更 - 校验格式
    var cofaceField = formContext.getAttribute("mcs_cofaceid");
    if (cofaceField) {
        cofaceField.addOnChange(CustomerMasterDataCreditForm.onCofaceIdChange);
    }
};

/**
 * Coface ID变更事件
 * 校验格式：icon#数字
 */
CustomerMasterDataCreditForm.onCofaceIdChange = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var cofaceField = formContext.getAttribute("mcs_cofaceid");

    if (!cofaceField) return;

    var cofaceId = cofaceField.getValue();

    if (!cofaceId) {
        formContext.ui.clearFormNotification("cofaceid_format");
        return;
    }

    // 校验格式：icon#数字
    var pattern = /^icon#\d+$/;
    if (!pattern.test(cofaceId)) {
        formContext.ui.setFormNotification(
            CustomerMasterDataCreditForm.L("AccountCredit_CofaceIdFormatHint", "科法斯客户代码格式应为 icon#数字，如 icon#164031501"),
            "WARNING",
            "cofaceid_format"
        );
    } else {
        formContext.ui.clearFormNotification("cofaceid_format");
    }
};

/**
 * 显示信用评估状态提示
 */
CustomerMasterDataCreditForm.showCreditStatusHint = function (formContext) {
    var creditValid = formContext.getAttribute("mcs_creditvalid");
    var creditScore = formContext.getAttribute("mcs_creditscore");
    var creditGrade = formContext.getAttribute("mcs_creditgrade");

    if (!creditValid) return;

    var validValue = creditValid.getValue();

    formContext.ui.clearFormNotification("credit_status");

    if (validValue === true || validValue === 1) {
        // 有效
        var score = creditScore ? creditScore.getValue() : null;
        var grade = creditGrade ? creditGrade.getValue() : "";
        var msg = CustomerMasterDataCreditForm.L("AccountCredit_CreditValid", "信用评估有效");
        if (score !== null) msg += CustomerMasterDataCreditForm.L("AccountCredit_ScorePrefix", " | 信用分：") + score;
        if (grade) msg += CustomerMasterDataCreditForm.L("AccountCredit_GradePrefix", " | 等级：") + grade;
        formContext.ui.setFormNotification(msg, "INFO", "credit_status");
    } else if (validValue === false || validValue === 0) {
        // 失效
        formContext.ui.setFormNotification(
            CustomerMasterDataCreditForm.L("AccountCredit_CreditInvalid", "信用评估已失效，请重新发起评估"),
            "WARNING",
            "credit_status"
        );
    } else {
        // 未评估
        formContext.ui.setFormNotification(
            CustomerMasterDataCreditForm.L("AccountCredit_NotAssessed", "该客户尚未进行信用评估"),
            "INFO",
            "credit_status"
        );
    }
};

/**
 * 保存前校验
 */
CustomerMasterDataCreditForm.onSave = function (executionContext) {
    var formContext = executionContext.getFormContext();

    // 校验Coface ID格式
    var cofaceField = formContext.getAttribute("mcs_cofaceid");
    if (cofaceField) {
        var cofaceId = cofaceField.getValue();
        if (cofaceId) {
            var pattern = /^icon#\d+$/;
            if (!pattern.test(cofaceId)) {
                Xrm.Utility.alertDialog(CustomerMasterDataCreditForm.L("AccountCredit_CofaceIdFormatError", "科法斯客户代码格式不正确，应为 icon#数字"));
                executionContext.getEventArgs().preventDefault();
                return;
            }
        }
    }
};
