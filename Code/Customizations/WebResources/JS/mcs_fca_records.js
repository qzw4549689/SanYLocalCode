var FcaRecordsForm = FcaRecordsForm || {};
(function (self) {
    "use strict";

    // 流程环节选项集值
    var PROCESS_STAGE_MAP = {
        1: "厂端授信模型计算",
        2: "厂端授信限额额度申请",
        3: "厂端授信限额归零调整"
    };

    // 额度调整动作选项集值
    var ADJUST_ACTION_MAP = {
        1: "初始化",
        2: "预占",
        3: "占用",
        4: "释放"
    };

    /**
     * 表单加载事件
     * 台账记录由系统自动生成，所有字段锁定为只读
     */
    self.onLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();

        // 锁定所有字段为只读
        lockAllFields(formContext);

        // 显示流程环节/调整动作的中文提示（如需要）
        showStageAndActionLabels(formContext);
    };

    /**
     * 锁定表单上所有字段为只读
     */
    function lockAllFields(formContext) {
        formContext.ui.controls.forEach(function (control) {
            if (control && typeof control.setDisabled === "function") {
                try {
                    control.setDisabled(true);
                } catch (ex) {
                    console.warn("锁定字段失败:", control.getName(), ex);
                }
            }
        });
    }

    /**
     * 在控制台输出流程环节/调整动作的中文标签（便于调试）
     */
    function showStageAndActionLabels(formContext) {
        var stageAttr = formContext.getAttribute("mcs_proccess");
        var actionAttr = formContext.getAttribute("mcs_adjust");

        if (stageAttr) {
            var stageValue = stageAttr.getValue();
            console.log("流程环节:", PROCESS_STAGE_MAP[stageValue] || stageValue);
        }
        if (actionAttr) {
            var actionValue = actionAttr.getValue();
            console.log("调整动作:", ADJUST_ACTION_MAP[actionValue] || actionValue);
        }
    }

})(FcaRecordsForm);
