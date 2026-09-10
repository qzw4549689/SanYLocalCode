/**
 * 融资落实（mcs_fsm_detail_data）表单脚本
 * 禅道 #1816（来源用例-577）：融资管理主单 BPF 点「完成」（mcs_fmprocess statuscode=2）后，
 * 落实记录界面信息不可以编辑——表单全字段只读 + 顶部提示。
 * 服务端兜底：FsmDetailDataCompletedGuardPlugin（Create/Update/Delete/SetState PreOp 拦截）。
 * 附件只读：mcs_/CommonCore/Html/Uploader.html 按实体名自行判定（本脚本不负责）。
 * 触发：主窗体 onLoad（MetadataTool bind-js 绑定 FsmDetailDataForm.onLoad/onSave）
 *
 * 2026-09-02（禅道无编号，用户会话拍板）：「订单编号」放大镜按主表融资管理的合同编号过滤
 * （PRD 外部融资额度管理：页面订单编号弹窗数据需要根据合同号进行过滤）。
 * 新字段 mcs_orderid → 自定义订单 mcs_order（其 mcs_contract Lookup 关联合同）；
 * 旧字段 mcs_order_id 误指标准订单 salesorder，已表单隐藏停用。
 * 实现参照 mcs_fsm_data.js filterContractByLead：addPreSearch + addCustomFilter。
 *
 * 2026-09-07（禅道 #2150）：主表合同改多选（mcs_contract_ids，GUID 逗号分隔），
 * 订单过滤改 mcs_order.mcs_contract IN 多选合同；主表无合同时按客户兜底过滤
 * （mcs_order.mcs_contractbuyer→account.mcs_customermasterdata = 主表客户主数据，与判新老客户同口径）。
 */

var FsmDetailDataForm = (function () {
    "use strict";

    var self = {};

    var BPF_ENTITY = "mcs_fmprocess";
    var BPF_FSM_LOOKUP_VALUE = "_bpf_mcs_fsm_dataid_value";
    var BPF_STATUS_FINISHED = 2;
    var NOTIFY_ID = "FsmCompletedReadOnly";

    // 订单按主表合同过滤相关常量
    var ORDER_CTRL = "mcs_orderid";            // 订单编号（新字段，Lookup → mcs_order）
    var ORDER_ENTITY = "mcs_order";            // 自定义订单实体
    var ORDER_CONTRACT_FIELD = "mcs_contract"; // 订单上的合同 Lookup
    var ORDER_BUYER_FIELD = "mcs_contractbuyer"; // 订单上的客户 Lookup → account
    var FSM_ENTITY = "mcs_fsm_data";           // 主表：融资管理
    // 禅道 #2150：主表合同改多选（GUID 逗号分隔 Memo）；客户主数据 Lookup 展开属性（按客户过滤兜底用）
    var FSM_CONTRACT_IDS = "mcs_contract_ids";
    var FSM_CUSTOMER_VALUE = "_mcs_customer_name_value";
    var EMPTY_GUID = "00000000-0000-0000-0000-000000000000"; // 空结果兜底（同 filterQuoterByLead 模式）

    // 主表合同/客户缓存：onLoad 异步解析，放大镜 addPreSearch 为同步回调只能读缓存
    var contractCache = { resolved: false, contractIds: [], customerMasterId: null };

    /**
     * 主单 BPF 是否已完成（statuscode=2 存在任一完成实例即视为完成）
     */
    function isParentFinished(parentFsmDataId) {
        var guid = (parentFsmDataId || "").replace(/[{}]/g, "");
        var query = "?$select=businessprocessflowinstanceid&$top=1&$filter="
            + BPF_FSM_LOOKUP_VALUE + " eq " + guid
            + " and statuscode eq " + BPF_STATUS_FINISHED;
        return Xrm.WebApi.retrieveMultipleRecords(BPF_ENTITY, query).then(function (res) {
            return res.entities.length > 0;
        }, function (e) {
            console.warn("[FsmDetail] 主单完成状态查询失败，按未完成放行:", e);
            return false;
        });
    }

    /**
     * 全字段只读 + 表单提示（子网格/WebResource 无 setDisabled，跳过即保持可查）
     */
    function lockForm(formContext) {
        formContext.ui.controls.forEach(function (ctrl) {
            try {
                if (ctrl && ctrl.setDisabled) ctrl.setDisabled(true);
            } catch (e) { /* 单个控件锁定失败不影响整体 */ }
        });
        formContext.ui.setFormNotification("融资管理单据已完成，本落实记录只读。", "INFO", NOTIFY_ID);
    }

    /**
     * 取主单（融资管理）记录 Id：mcs_fsm_data_id 已作为隐藏字段上表单（2026-09-02 起，
     * 子网格新建时平台自动映射父值），getAttribute 取不到时回读服务端 _mcs_fsm_data_id_value
     */
    function getParentFsmDataId(formContext) {
        var attr = formContext.getAttribute("mcs_fsm_data_id");
        var val = attr ? attr.getValue() : null;
        if (val && val.length && val[0].id) return Promise.resolve(("" + val[0].id).replace(/[{}]/g, ""));
        var recId = ("" + formContext.data.entity.getId()).replace(/[{}]/g, "");
        if (!recId) return Promise.resolve(null);
        return Xrm.WebApi.retrieveRecord("mcs_fsm_detail_data", recId, "?$select=_mcs_fsm_data_id_value")
            .then(function (r) { return r ? (r["_mcs_fsm_data_id_value"] || null) : null; },
                function (e) { console.warn("[FsmDetail] 主单 Lookup 回读失败:", e); return null; });
    }

    /**
     * 解析主表多选合同与客户并写入缓存（幂等，可重复调用刷新）
     * 禅道 #2150：mcs_contract_ids 逗号分隔 GUID 数组 + 客户主数据 Id（按客户过滤兜底）
     */
    function resolveParentContract(formContext) {
        return getParentFsmDataId(formContext).then(function (pid) {
            if (!pid) { contractCache.resolved = true; contractCache.contractIds = []; contractCache.customerMasterId = null; return; }
            return Xrm.WebApi.retrieveRecord(FSM_ENTITY, pid, "?$select=" + FSM_CONTRACT_IDS + "," + FSM_CUSTOMER_VALUE)
                .then(function (r) {
                    contractCache.resolved = true;
                    var raw = r ? r[FSM_CONTRACT_IDS] : null;
                    contractCache.contractIds = raw
                        ? String(raw).split(",").map(function (x) { return x.trim().replace(/[{}]/g, ""); }).filter(function (x) { return x.length > 0; })
                        : [];
                    contractCache.customerMasterId = r ? (r[FSM_CUSTOMER_VALUE] || null) : null;
                }, function (e) {
                    console.warn("[FsmDetail] 主表合同/客户回读失败:", e);
                    contractCache.resolved = true;
                    contractCache.contractIds = [];
                    contractCache.customerMasterId = null;
                });
        });
    }

    /**
     * 订单编号放大镜过滤（禅道 #2150）：
     * ①主表有多选合同 → mcs_order.mcs_contract IN 多选合同；
     * ②主表无合同 → 按客户过滤（mcs_contractbuyer→account.mcs_customermasterdata = 主表客户主数据）；
     * ③合同/客户均无或缓存未就绪 → 空结果兜底（防止误选无关订单）
     */
    function filterOrderByContract(formContext) {
        var fetchXml;
        if (contractCache.contractIds.length > 0) {
            var values = contractCache.contractIds.map(function (id) { return "<value>" + id + "</value>"; }).join("");
            fetchXml = "<fetch><entity name='" + ORDER_ENTITY + "'>" +
                "<filter type='and'><condition attribute='" + ORDER_CONTRACT_FIELD + "' operator='in'>" + values + "</condition></filter>" +
                "</entity></fetch>";
        } else if (contractCache.customerMasterId) {
            // 按客户：订单客户（mcs_contractbuyer→account）关联的客户主数据 = 主表客户名称（mcs_customermasterdata）
            fetchXml = "<fetch><entity name='" + ORDER_ENTITY + "'>" +
                "<link-entity name='account' from='accountid' to='" + ORDER_BUYER_FIELD + "' link-type='inner'>" +
                "<filter type='and'><condition attribute='mcs_customermasterdata' operator='eq' value='" + contractCache.customerMasterId + "' /></filter>" +
                "</link-entity></entity></fetch>";
        } else {
            fetchXml = "<fetch><entity name='" + ORDER_ENTITY + "'>" +
                "<filter type='and'><condition attribute='" + ORDER_CONTRACT_FIELD + "' operator='eq' value='" + EMPTY_GUID + "' /></filter>" +
                "</entity></fetch>";
        }
        var ctrl = formContext.getControl(ORDER_CTRL);
        if (ctrl) ctrl.addCustomFilter(fetchXml, ORDER_ENTITY);
    }

    self.onLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();
        var checkAndLock = function () {
            getParentFsmDataId(formContext).then(function (pid) {
                if (!pid) return; // 无主单（异常数据）不锁
                isParentFinished(pid).then(function (finished) {
                    if (!finished) return;
                    lockForm(formContext);
                    // UCI 初始渲染后平台会按表单 XML 重置控件禁用态（2026-08-13 DEV1 实锤：
                    // onLoad 异步查询回来时锁被冲掉），延时补锁两次兜底；lockForm 幂等
                    setTimeout(function () { lockForm(formContext); }, 1000);
                    setTimeout(function () { lockForm(formContext); }, 3000);
                });
            });
        };
        checkAndLock();
        // 数据级 onLoad（首次数据就绪 + 「刷新」重载后）再跑一次，覆盖表单重渲染场景
        try { formContext.data.addOnLoad(checkAndLock); } catch (e) { /* 注册失败不阻塞 */ }

        // 订单编号放大镜按主表合同过滤：注册 PreSearch + 异步解析主表合同
        var orderCtrl = formContext.getControl(ORDER_CTRL);
        if (orderCtrl) orderCtrl.addPreSearch(function () { filterOrderByContract(formContext); });
        resolveParentContract(formContext);
        // 数据级 onLoad 时刷新一次合同缓存（覆盖表单重载 / 保存后场景）
        try { formContext.data.addOnLoad(function () { resolveParentContract(formContext); }); } catch (e) { /* 注册失败不阻塞 */ }
    };

    // bind-js 会同时登记 onSave 处理器，占位防空函数报错（编辑拦截由服务端插件兜底）
    self.onSave = function (executionContext) { };

    return self;
})();
