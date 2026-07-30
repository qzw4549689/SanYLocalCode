/**
 * 成交条件产品分类关系表单脚本
 * 功能：
 * 1. 选择产品线 Lookup 后自动带出产品线编码(mcs_groupid)和名称(mcs_groupname)
 * 2. 选择成交条件产品分类 Lookup 后自动带出产品分类编码(mcs_typeid)和名称(mcs_typename)
 */
var TradePtGroupTypeForm = TradePtGroupTypeForm || {};

(function (self) {
    "use strict";

    /**
     * 表单加载事件
     */
    self.onLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();

        // 产品线 Lookup 变更监听
        var productLineAttr = formContext.getAttribute("mcs_productlineid");
        if (productLineAttr) {
            productLineAttr.addOnChange(self.onProductLineChanged);
            // 初始同步一次（处理打开已有记录的场景）
            self.syncProductLineInfo(formContext);
        }

        // 成交条件产品分类 Lookup 变更监听
        var tradePtTypeAttr = formContext.getAttribute("mcs_trade_pttypeid");
        if (tradePtTypeAttr) {
            tradePtTypeAttr.addOnChange(self.onTradePtTypeChanged);
            // 初始同步一次
            self.syncTradePtTypeInfo(formContext);
        }
    };

    /**
     * 产品线 Lookup 变更事件
     */
    self.onProductLineChanged = function (executionContext) {
        var formContext = executionContext.getFormContext();
        self.syncProductLineInfo(formContext);
    };

    /**
     * 根据产品线 Lookup 同步编码和名称
     */
    self.syncProductLineInfo = function (formContext) {
        var productLineAttr = formContext.getAttribute("mcs_productlineid");
        var groupIdAttr = formContext.getAttribute("mcs_groupid");
        var groupNameAttr = formContext.getAttribute("mcs_groupname");

        if (!productLineAttr || !groupIdAttr || !groupNameAttr) {
            return;
        }

        var lookupValue = productLineAttr.getValue();
        if (!lookupValue || lookupValue.length === 0) {
            groupIdAttr.setValue(null);
            groupNameAttr.setValue(null);
            return;
        }

        var productLineId = lookupValue[0].id.replace("{", "").replace("}", "");

        Xrm.WebApi.retrieveRecord("mcs_productline", productLineId, "?$select=mcs_code,mcs_name").then(
            function (result) {
                if (result.mcs_code) {
                    groupIdAttr.setValue(result.mcs_code);
                }
                if (result.mcs_name) {
                    groupNameAttr.setValue(result.mcs_name);
                }
            },
            function (error) {
                console.error("查询产品线信息失败: ", error.message);
            }
        );
    };

    /**
     * 成交条件产品分类 Lookup 变更事件
     */
    self.onTradePtTypeChanged = function (executionContext) {
        var formContext = executionContext.getFormContext();
        self.syncTradePtTypeInfo(formContext);
    };

    /**
     * 根据成交条件产品分类 Lookup 同步编码和名称
     */
    self.syncTradePtTypeInfo = function (formContext) {
        var tradePtTypeAttr = formContext.getAttribute("mcs_trade_pttypeid");
        var typeIdAttr = formContext.getAttribute("mcs_typeid");
        var typeNameAttr = formContext.getAttribute("mcs_typename");

        if (!tradePtTypeAttr || !typeIdAttr || !typeNameAttr) {
            return;
        }

        var lookupValue = tradePtTypeAttr.getValue();
        if (!lookupValue || lookupValue.length === 0) {
            typeIdAttr.setValue(null);
            typeNameAttr.setValue(null);
            return;
        }

        var tradePtTypeId = lookupValue[0].id.replace("{", "").replace("}", "");

        Xrm.WebApi.retrieveRecord("mcs_trade_pttype", tradePtTypeId, "?$select=mcs_typeid,mcs_trade_pttypename").then(
            function (result) {
                if (result.mcs_typeid) {
                    typeIdAttr.setValue(result.mcs_typeid);
                }
                if (result.mcs_trade_pttypename) {
                    typeNameAttr.setValue(result.mcs_trade_pttypename);
                }
            },
            function (error) {
                console.error("查询成交条件产品分类信息失败: ", error.message);
            }
        );
    };

})(TradePtGroupTypeForm);
