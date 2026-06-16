/**
 * 客户主数据表(Account) - 信用评估扩展表单逻辑
 * 实体: account
 * 功能: Coface字段展示、信用评估信息展示、校验
 * 影响范围: 仅限account实体的信用评估相关字段
 */

var AccountCreditForm = AccountCreditForm || {};

/**
 * 表单加载事件
 */
AccountCreditForm.onLoad = function (executionContext) {
    var formContext = executionContext.getFormContext();
    var formType = formContext.ui.getFormType();
    
    // 设置信用评估相关字段只读
    AccountCreditForm.setCreditFieldsReadOnly(formContext);
    
    // 注册字段变更事件
    AccountCreditForm.registerEvents(formContext);
    
    // 显示信用评估状态提示
    AccountCreditForm.showCreditStatusHint(formContext);
};

/**
 * 设置信用评估相关字段只读
 * 这些字段由评估流程自动更新，不允许手工修改
 */
AccountCreditForm.setCreditFieldsReadOnly = function (formContext) {
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
AccountCreditForm.registerEvents = function (formContext) {
    // Coface ID变更 - 校验格式
    var cofaceField = formContext.getAttribute("mcs_cofaceid");
    if (cofaceField) {
        cofaceField.addOnChange(AccountCreditForm.onCofaceIdChange);
    }
};

/**
 * Coface ID变更事件
 * 校验格式：icon#数字
 */
AccountCreditForm.onCofaceIdChange = function (executionContext) {
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
            "科法斯客户代码格式应为 icon#数字，如 icon#164031501",
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
AccountCreditForm.showCreditStatusHint = function (formContext) {
    var creditValid = formContext.getAttribute("mcs_creditvalid");
    var creditScore = formContext.getAttribute("mcs_creditscore");
    var creditGrade = formContext.getAttribute("mcs_creditgrade");
    
    if (!creditValid) return;
    
    var validValue = creditValid.getValue();
    
    formContext.ui.clearFormNotification("credit_status");
    
    if (validValue === 1) {
        // 有效
        var score = creditScore ? creditScore.getValue() : null;
        var grade = creditGrade ? creditGrade.getValue() : "";
        var msg = "信用评估有效";
        if (score !== null) msg += " | 信用分：" + score;
        if (grade) msg += " | 等级：" + grade;
        formContext.ui.setFormNotification(msg, "SUCCESS", "credit_status");
    } else if (validValue === 0) {
        // 失效
        formContext.ui.setFormNotification(
            "信用评估已失效，请重新发起评估",
            "WARNING",
            "credit_status"
        );
    } else {
        // 未评估
        formContext.ui.setFormNotification(
            "该客户尚未进行信用评估",
            "INFO",
            "credit_status"
        );
    }
};

/**
 * 保存前校验
 */
AccountCreditForm.onSave = function (executionContext) {
    var formContext = executionContext.getFormContext();
    
    // 校验Coface ID格式
    var cofaceField = formContext.getAttribute("mcs_cofaceid");
    if (cofaceField) {
        var cofaceId = cofaceField.getValue();
        if (cofaceId) {
            var pattern = /^icon#\d+$/;
            if (!pattern.test(cofaceId)) {
                Xrm.Utility.alertDialog("科法斯客户代码格式不正确，应为 icon#数字");
                executionContext.getEventArgs().preventDefault();
                return;
            }
        }
    }
};
