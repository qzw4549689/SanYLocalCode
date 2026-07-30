/**
 * 融资管理（mcs_fsm_data）表单脚本
 * 功能：BPP 立项审批 / 融资方案审批提交
 * 触发：表单命令栏【提交立项审批】【提交融资方案审批】App Action 按钮
 */
var FsmDataForm = (function () {
    "use strict";

    var self = {};

    // mcs_fsm_data.mcs_fsm_status 选项集值
    var FSM_STATUS = {
        DEMAND: 1,          // 融资需求
        INITIATION: 2,      // 融资立项
        SOLUTION: 3,        // 融资解决方案
        IMPLEMENTATION: 4   // 融资落实
    };

    // mcs_fsm_data.mcs_bppstatus 选项集值
    var BPP_STATUS = {
        APPLY: 1,       // 申请
        IN_REVIEW: 2,   // 审批中
        APPROVED: 3,    // 通过
        REJECTED: 4     // 驳回
    };

    // mcs_fsm_data.mcs_approve_type 选项集值
    var APPROVE_TYPE = {
        INITIATION: 1,  // 立项审批
        PROJECT: 2      // 融资方案审批
    };

    /**
     * 判断 BPP 状态是否为"进行中"
     */
    function isBppInProgress(bppStatusCode) {
        if (!bppStatusCode) {
            return false;
        }
        var status = ("" + bppStatusCode).toLowerCase();
        return status === "submitted" || status === "inreview" ||
               status === "pending" || status === "10" || status === "20";
    }

    /**
     * 提交 BPP 审批（公共逻辑）
     * @param {object} formContext 表单上下文
     * @param {number} approveType 审批类型（1 立项审批 / 2 融资方案审批）
     * @param {string} typeName 审批类型名称（用于提示）
     */
    function submitApproval(formContext, approveType, typeName) {
        // 记录必须已保存
        var recordId = formContext.data.entity.getId();
        if (!recordId) {
            Xrm.Navigation.openAlertDialog({ text: "请先保存记录后再提交审批。" });
            return;
        }
        recordId = recordId.replace(/[{}]/g, "");

        // 审批状态校验：已有审批在进行中则拦截
        var bppStatusAttr = formContext.getAttribute("mcs_bppstatus");
        var currentBppStatus = bppStatusAttr ? bppStatusAttr.getValue() : null;
        if (currentBppStatus === BPP_STATUS.IN_REVIEW) {
            Xrm.Navigation.openAlertDialog({ text: "已有审批在进行中，无法重复提交。" });
            return;
        }

        var bppStatusCodeAttr = formContext.getAttribute("mcs_bppstatuscode");
        var currentBppStatusCode = bppStatusCodeAttr ? bppStatusCodeAttr.getValue() : null;
        if (isBppInProgress(currentBppStatusCode)) {
            Xrm.Navigation.openAlertDialog({ text: "已有审批在进行中，无法重复提交。" });
            return;
        }

        // 更新审批类型 + 审批状态为 2（审批中），触发后端 Plugin 调用 mcs_bppstartapi
        Xrm.WebApi.updateRecord("mcs_fsm_data", recordId, {
            "mcs_approve_type": approveType,
            "mcs_bppstatus": BPP_STATUS.IN_REVIEW
        }).then(
            function () {
                Xrm.Navigation.openAlertDialog({ text: typeName + "已提交。" }).then(function () {
                    formContext.data.refresh(false);
                });
            },
            function (error) {
                console.error("提交" + typeName + "失败:", error);
                Xrm.Navigation.openAlertDialog({ text: "提交" + typeName + "失败: " + (error.message || JSON.stringify(error)) });
            }
        );
    }

    /**
     * 提交立项审批
     * 前置条件：融资状态 = 2（融资立项）且 mcs_can_initiated = 1
     * 入口：表单命令栏【提交立项审批】按钮，参数 PrimaryControl
     */
    self.submitInitiationApproval = function (primaryControl) {
        var formContext = primaryControl;

        var statusAttr = formContext.getAttribute("mcs_fsm_status");
        var fsmStatus = statusAttr ? statusAttr.getValue() : null;
        if (fsmStatus !== FSM_STATUS.INITIATION) {
            Xrm.Navigation.openAlertDialog({ text: "只有融资立项状态才能提交立项审批。" });
            return;
        }

        var canInitiatedAttr = formContext.getAttribute("mcs_can_initiated");
        var canInitiated = canInitiatedAttr ? canInitiatedAttr.getValue() : false;
        if (canInitiated !== true) {
            Xrm.Navigation.openAlertDialog({ text: "当前不允许提交立项审批。" });
            return;
        }

        submitApproval(formContext, APPROVE_TYPE.INITIATION, "立项审批");
    };

    /**
     * 提交融资方案审批
     * 前置条件：融资状态 = 3（融资解决方案）且 mcs_can_project = 1
     * 入口：表单命令栏【提交融资方案审批】按钮，参数 PrimaryControl
     */
    self.submitProjectApproval = function (primaryControl) {
        var formContext = primaryControl;

        var statusAttr = formContext.getAttribute("mcs_fsm_status");
        var fsmStatus = statusAttr ? statusAttr.getValue() : null;
        if (fsmStatus !== FSM_STATUS.SOLUTION) {
            Xrm.Navigation.openAlertDialog({ text: "只有融资解决方案状态才能提交融资方案审批。" });
            return;
        }

        var canProjectAttr = formContext.getAttribute("mcs_can_project");
        var canProject = canProjectAttr ? canProjectAttr.getValue() : false;
        if (canProject !== true) {
            Xrm.Navigation.openAlertDialog({ text: "当前不允许提交融资方案审批。" });
            return;
        }

        submitApproval(formContext, APPROVE_TYPE.PROJECT, "融资方案审批");
    };

    return self;
})();
