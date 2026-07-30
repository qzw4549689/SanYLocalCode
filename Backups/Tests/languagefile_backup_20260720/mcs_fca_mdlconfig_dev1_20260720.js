var FcaMdlConfigForm = FcaMdlConfigForm || {};
(function (self) {
    "use strict";

    var ALL_CREDIT_GRADE = 6;
    var MIN_BENCHMARK_AMOUNT = 1000;

    self.onLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();
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
                text: "客户等级不等于 ALL 时，以下字段必填：" + missingLabels.join("、") + "。"
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
                text: "客户等级为 ALL 时，历史基准额度必须大于 " + MIN_BENCHMARK_AMOUNT + "。"
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
                        text: "已存在相同的客户分类与客户等级组合，请修改后保存。"
                    });
                }
            },
            function (error) {
                console.error("校验客户分类+客户等级组合唯一失败:", error);
            }
        );
    }
})(FcaMdlConfigForm);
