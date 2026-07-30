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

var FcaMdlConfigForm = FcaMdlConfigForm || {};
(function (self) {
    "use strict";

    /**
     * 多语言取词（带中文兜底）
     * 语言包已加载时返回对应语言文本；未加载/未找到时返回原中文，保证中文用户不受影响
     */
    function t(key, defaultText) {
        if (typeof LanguageHelper !== "undefined") {
            var v = LanguageHelper.getLabel(key);
            if (v && v !== key) return v;
        }
        return defaultText;
    }

    var ALL_CREDIT_GRADE = 6;
    var MIN_BENCHMARK_AMOUNT = 1000;

    self.onLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();

        // 预加载语言包（异步，不阻塞后续逻辑）
        if (typeof LanguageHelper !== "undefined") {
            LanguageHelper.loadLanguagePack();
        }

        if (formContext.ui.getFormType() === 1) {
            setDefaultDecimalValues(formContext);
        }
    };

    self.onSave = function (executionContext) {
        var formContext = executionContext.getFormContext();
        var eventArgs = executionContext.getEventArgs();
        if (eventArgs.getSaveMode() === 2) return;

        validateFactorFieldsRequired(formContext, eventArgs);
        validateBenchmarkAmount(formContext, eventArgs);
        validateUniqueCombination(formContext, eventArgs);
    };

    function setDefaultDecimalValues(formContext) {
        var fields = ["mcs_adjust1", "mcs_adjust2", "mcs_adjust3"];
        fields.forEach(function (field) {
            var attr = formContext.getAttribute(field);
            if (attr && attr.getValue() === null) {
                attr.setValue(0);
            }
        });
    }

    function validateFactorFieldsRequired(formContext, eventArgs) {
        var creditGradeAttr = formContext.getAttribute("mcs_creditgrade");
        if (!creditGradeAttr) return;

        var creditGrade = creditGradeAttr.getValue();
        if (creditGrade === null || creditGrade === ALL_CREDIT_GRADE) return;

        var factorFields = ["mcs_adjust1", "mcs_adjust2", "mcs_adjust3", "mcs_aggfunc"];
        var missingLabels = [];
        factorFields.forEach(function (field) {
            var attr = formContext.getAttribute(field);
            if (attr && attr.getValue() === null) {
                var control = formContext.getControl(field);
                var label = control ? control.getLabel() : field;
                missingLabels.push(label);
            }
        });

        if (missingLabels.length > 0) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({
                text: t("FcaMdlConfig_FactorRequiredPrefix", "客户等级不等于 ALL 时，以下字段必填：") + missingLabels.join(t("CreditForm_ListSeparator", "、")) + t("FcaMdlConfig_RequiredSuffix", "。")
            });
        }
    }

    function validateBenchmarkAmount(formContext, eventArgs) {
        var creditGradeAttr = formContext.getAttribute("mcs_creditgrade");
        var benchmarkAttr = formContext.getAttribute("mcs_countryname");
        if (!creditGradeAttr || !benchmarkAttr) return;

        var creditGrade = creditGradeAttr.getValue();
        var benchmark = benchmarkAttr.getValue();

        if (creditGrade === ALL_CREDIT_GRADE && (benchmark === null || benchmark <= MIN_BENCHMARK_AMOUNT)) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({
                text: t("FcaMdlConfig_BenchmarkRule", "客户等级为 ALL 时，历史基准额度必须大于 {0}。").replace("{0}", MIN_BENCHMARK_AMOUNT)
            });
        }
    }

    function validateUniqueCombination(formContext, eventArgs) {
        var buyerGradeAttr = formContext.getAttribute("mcs_buyergrade");
        var creditGradeAttr = formContext.getAttribute("mcs_creditgrade");
        if (!buyerGradeAttr || !creditGradeAttr) return;

        var buyerGrade = buyerGradeAttr.getValue();
        var creditGrade = creditGradeAttr.getValue();

        if (buyerGrade === null || creditGrade === null) return;

        var recordId = formContext.data.entity.getId();
        var idFilter = recordId ? " and mcs_fca_mdlconfigid ne " + recordId.replace(/[{}]/g, "") : "";
        var filter = "mcs_buyergrade eq " + buyerGrade + " and mcs_creditgrade eq " + creditGrade + idFilter;

        Xrm.WebApi.retrieveMultipleRecords("mcs_fca_mdlconfig", "?$select=mcs_fca_mdlconfigid&$filter=" + filter).then(
            function (result) {
                if (result.entities.length > 0) {
                    eventArgs.preventDefault();
                    Xrm.Navigation.openAlertDialog({
                        text: t("FcaMdlConfig_DuplicateCombination", "已存在相同的客户分类与客户等级组合，请修改后保存。")
                    });
                }
            },
            function (error) {
                console.error("校验客户分类+客户等级组合唯一失败:", error);
            }
        );
    }
})(FcaMdlConfigForm);
