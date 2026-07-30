var FcaQuotaAppForm = FcaQuotaAppForm || {};
(function (self) {
    "use strict";

    // 客户等级选项集值 → 标签映射（mcs_customermasterdata.mcs_creditgrade）
    var CREDIT_GRADE_MAP = {
        100000000: "A0",
        100000001: "A1",
        100000002: "A2",
        100000003: "A3",
        100000004: "A4"
    };

    /**
     * 表单加载事件
     * 1. 注册字段变更事件
     * 2. 新建记录时设置默认值
     */
    self.onLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();

        registerFieldEvents(formContext);

        if (formContext.ui.getFormType() === 1) {
            setDefaultValues(formContext);
            // 新建记录时，从当前系统用户带出申请组织信息
            loadCurrentUserOrgInfo(formContext);
        }

        // 生成并回填 D365 记录链接（用于 BPP 审批页面跳转）
        generateRecordUrl(formContext);

        // 初始化附件页签（通用上传组件）
        self.initAttachmentTab(formContext);
    };

    /**
     * 表单保存前校验
     */
    self.onSave = function (executionContext) {
        var formContext = executionContext.getFormContext();
        var eventArgs = executionContext.getEventArgs();

        // 自动保存/刷新保存不做校验
        if (eventArgs.getSaveMode() === 2 || eventArgs.getSaveMode() === 70) return;

        validateOnSave(formContext, eventArgs);
    };

    // ==================== 事件注册 ====================

    function registerFieldEvents(formContext) {
        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (accountAttr) {
            accountAttr.addOnChange(function () {
                onAccountChanged(formContext);
            });
        }

        var doidAttr = formContext.getAttribute("mcs_doid");
        if (doidAttr) {
            doidAttr.addOnChange(function () {
                onDoidChanged(formContext);
            });
        }

        var tobeGrantAttr = formContext.getAttribute("mcs_tobegrant");
        if (tobeGrantAttr) {
            tobeGrantAttr.addOnChange(function () {
                calculateAdjustedBalance(formContext);
            });
        }
    }

    // ==================== 默认值设置 ====================

    function setDefaultValues(formContext) {
        // 审批状态 = 申请
        setPicklistIfNull(formContext, "mcs_bppstatus", 1);

        // 调整后额度、调整后余额默认 0
        setMoneyIfNull(formContext, "mcs_tobegrant", 0);
        setMoneyIfNull(formContext, "mcs_tobebalance", 0);
    }

    function setPicklistIfNull(formContext, field, value) {
        var attr = formContext.getAttribute(field);
        if (attr && attr.getValue() === null) {
            attr.setValue(value);
        }
    }

    function setMoneyIfNull(formContext, field, value) {
        var attr = formContext.getAttribute(field);
        if (attr && attr.getValue() === null) {
            attr.setValue(value);
        }
    }

    // ==================== 客户编码变更 ====================

    /**
     * 客户编码变更时：
     * 1. 带出客户名称、客户等级
     * 2. 查询厂端授信额度表，带出当前额度和余额
     * 3. 自动带出该客户最新生效的模型计算序列号和模型计算额度
     */
    function onAccountChanged(formContext) {
        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (!accountAttr || accountAttr.getValue() === null) {
            clearAccountRelatedFields(formContext);
            return;
        }

        var accountRef = accountAttr.getValue()[0];
        var accountId = accountRef.id.replace(/[{}]/g, "");

        // 带出客户名称、客户等级、中信保买方代码
        retrieveCustomerInfo(formContext, accountId);

        // 带出当前厂端授信额度和余额
        retrieveCurrentQuota(formContext, accountId);

        // 带出中信保额度信息
        retrieveSinosureQuota(formContext, accountId);

        // 自动带出该客户最新生效的模型计算序列号和额度
        retrieveLatestEffectiveProc(formContext, accountId);
    }

    function clearAccountRelatedFields(formContext) {
        setStringValue(formContext, "mcs_custname", "");
        setStringValue(formContext, "mcs_creditgrade", "");
        setMoneyValue(formContext, "mcs_sellergrant", 0);
        setMoneyValue(formContext, "mcs_sellerbalance", 0);
        setStringValue(formContext, "mcs_applygenre", "");
        setMoneyValue(formContext, "mcs_quotasum", 0);
        setMoneyValue(formContext, "mcs_quotabalance", 0);
        clearLookup(formContext, "mcs_doid");
        setMoneyValue(formContext, "mcs_initigrant", 0);
        calculateAdjustedBalance(formContext);
    }

    /**
     * 查询客户主数据：客户编码、客户等级、中信保买方代码
     * 注：mcs_custname 当前用于存放客户编码（mcs_sapnumber）
     */
    function retrieveCustomerInfo(formContext, accountId) {
        Xrm.WebApi.retrieveRecord("mcs_customermasterdata", accountId, "?$select=mcs_sapnumber,mcs_creditgrade,mcs_sinosurecode").then(
            function (result) {
                if (result.mcs_sapnumber !== null && result.mcs_sapnumber !== undefined) {
                    setStringValue(formContext, "mcs_custname", result.mcs_sapnumber);
                }
                if (result.mcs_creditgrade !== null && result.mcs_creditgrade !== undefined) {
                    var gradeLabel = CREDIT_GRADE_MAP[result.mcs_creditgrade] || "";
                    setStringValue(formContext, "mcs_creditgrade", gradeLabel);
                }
            },
            function (error) {
                console.error("查询客户主数据失败:", error);
            }
        );
    }

    /**
     * 查询中信保限额信息
     * 按中信保买方代码匹配，限额状态=1有效，汇总额度和余额
     */
    function retrieveSinosureQuota(formContext, accountId) {
        // 先清空
        setStringValue(formContext, "mcs_applygenre", "");
        setMoneyValue(formContext, "mcs_quotasum", 0);
        setMoneyValue(formContext, "mcs_quotabalance", 0);

        Xrm.WebApi.retrieveRecord("mcs_customermasterdata", accountId, "?$select=mcs_sinosurecode").then(
            function (result) {
                var sinosureCode = result.mcs_sinosurecode;
                if (!sinosureCode) {
                    return;
                }

                var filter = "mcs_buyerno eq '" + sinosureCode.replace(/'/g, "''") + "' and mcs_quotastate eq 1";
                var query = "?$select=mcs_paymodeapply,mcs_quotasum,mcs_quotabalance&$filter=" + filter;

                Xrm.WebApi.retrieveMultipleRecords("mcs_approvedquota", query).then(
                    function (quotaResult) {
                        var entities = quotaResult.entities;
                        if (entities.length === 0) {
                            return;
                        }

                        var quotaSum = 0;
                        var quotaBalance = 0;
                        var hasLc = false;
                        var hasNonLc = false;

                        for (var i = 0; i < entities.length; i++) {
                            var e = entities[i];

                            // 汇总信保额度
                            if (e.mcs_quotasum !== null && e.mcs_quotasum !== undefined) {
                                quotaSum += e.mcs_quotasum;
                            }

                            // 汇总信保余额（String 类型，尝试解析为数值）
                            if (e.mcs_quotabalance) {
                                var balanceNum = parseFloat(e.mcs_quotabalance.replace(/,/g, ""));
                                if (!isNaN(balanceNum)) {
                                    quotaBalance += balanceNum;
                                }
                            }

                            // 额度类型映射：PRD 规则 1,2,3,7 为非信用证，其余为信用证
                            var payMode = e.mcs_paymodeapply;
                            if (payMode !== null && payMode !== undefined) {
                                if ([1, 2, 3, 7].indexOf(payMode) >= 0) {
                                    hasNonLc = true;
                                } else {
                                    hasLc = true;
                                }
                            }
                        }

                        setMoneyValue(formContext, "mcs_quotasum", quotaSum);
                        setMoneyValue(formContext, "mcs_quotabalance", quotaBalance);

                        // 额度类型显示：PRD 要求同时存在时逗号组合
                        var genreParts = [];
                        if (hasNonLc) genreParts.push("非信用证");
                        if (hasLc) genreParts.push("信用证");
                        setStringValue(formContext, "mcs_applygenre", genreParts.join(","));
                    },
                    function (error) {
                        console.error("查询中信保限额信息失败:", error);
                    }
                );
            },
            function (error) {
                console.error("查询客户主数据中信保代码失败:", error);
            }
        );
    }

    /**
     * 查询厂端授信额度表：当前额度和余额
     */
    function retrieveCurrentQuota(formContext, accountId) {
        var filter = "_mcs_accountid_value eq " + accountId + " and mcs_isactive eq 1";
        var query = "?$select=mcs_sellergrant,mcs_sellerbalance&$filter=" + filter + "&$orderby=createdon desc&$top=1";

        Xrm.WebApi.retrieveMultipleRecords("mcs_fca_quota", query).then(
            function (result) {
                if (result.entities.length > 0) {
                    var quota = result.entities[0];
                    setMoneyValue(formContext, "mcs_sellergrant", quota.mcs_sellergrant || 0);
                    setMoneyValue(formContext, "mcs_sellerbalance", quota.mcs_sellerbalance || 0);
                } else {
                    setMoneyValue(formContext, "mcs_sellergrant", 0);
                    setMoneyValue(formContext, "mcs_sellerbalance", 0);
                }
                calculateAdjustedBalance(formContext);
            },
            function (error) {
                console.error("查询厂端授信额度失败:", error);
            }
        );
    }

    // ==================== 模型计算序列号变更 ====================

    /**
     * 模型计算序列号变更时：
     * 带出模型计算额度（mcs_fca_proc.mcs_initigrant）
     */
    function onDoidChanged(formContext) {
        var doidAttr = formContext.getAttribute("mcs_doid");
        if (!doidAttr || doidAttr.getValue() === null) {
            setMoneyValue(formContext, "mcs_initigrant", 0);
            return;
        }

        var doidRef = doidAttr.getValue()[0];
        var procId = doidRef.id.replace(/[{}]/g, "");

        Xrm.WebApi.retrieveRecord("mcs_fca_proc", procId, "?$select=mcs_initigrant,mcs_accountid").then(
            function (result) {
                if (result.mcs_initigrant !== null && result.mcs_initigrant !== undefined) {
                    setMoneyValue(formContext, "mcs_initigrant", result.mcs_initigrant);
                }

                // 校验模型计算记录的客户是否与当前申请单客户一致
                var accountAttr = formContext.getAttribute("mcs_accountid");
                if (accountAttr && accountAttr.getValue() !== null && result._mcs_accountid_value) {
                    var currentAccountId = accountAttr.getValue()[0].id.replace(/[{}]/g, "").toLowerCase();
                    var procAccountId = result._mcs_accountid_value.replace(/[{}]/g, "").toLowerCase();
                    if (currentAccountId !== procAccountId) {
                        Xrm.Navigation.openAlertDialog({ text: "所选模型计算序列号的客户与当前申请单客户不一致，请重新选择。" });
                        clearLookup(formContext, "mcs_doid");
                        setMoneyValue(formContext, "mcs_initigrant", 0);
                    }
                }
            },
            function (error) {
                console.error("查询模型计算记录失败:", error);
            }
        );
    }

    // ==================== 调整后余额计算 ====================

    /**
     * 调整后厂端授信余额 = 调整厂端授信额度 - 厂端授信额度 + 厂端授信余额
     */
    function calculateAdjustedBalance(formContext) {
        var tobeGrant = getMoneyValue(formContext, "mcs_tobegrant") || 0;
        var sellerGrant = getMoneyValue(formContext, "mcs_sellergrant") || 0;
        var sellerBalance = getMoneyValue(formContext, "mcs_sellerbalance") || 0;

        var adjustedBalance = tobeGrant - sellerGrant + sellerBalance;
        setMoneyValue(formContext, "mcs_tobebalance", adjustedBalance);
    }

    // ==================== 保存前校验 ====================

    function validateOnSave(formContext, eventArgs) {
        // 客户编码必填
        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (!accountAttr || accountAttr.getValue() === null) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: "客户编码必填。" });
            return;
        }

        // 调整厂端授信额度必填且 ≥ 0
        var tobeGrantAttr = formContext.getAttribute("mcs_tobegrant");
        if (!tobeGrantAttr || tobeGrantAttr.getValue() === null) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: "调整厂端授信额度必填。" });
            return;
        }
        if (tobeGrantAttr.getValue() < 0) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: "调整厂端授信额度不能小于 0。" });
            return;
        }

        // 账期（天）必须是 30 的倍数
        var payTermAttr = formContext.getAttribute("mcs_payterm");
        if (payTermAttr && payTermAttr.getValue() !== null) {
            var payTerm = payTermAttr.getValue();
            if (payTerm % 30 !== 0) {
                eventArgs.preventDefault();
                Xrm.Navigation.openAlertDialog({ text: "账期（天）必须是 30 的倍数。" });
                return;
            }
        }

        // 校验：如果当前额度为 0/空，必须选择模型计算序列号
        var sellerGrant = getMoneyValue(formContext, "mcs_sellergrant") || 0;
        var doidAttr = formContext.getAttribute("mcs_doid");
        if (sellerGrant === 0 && (!doidAttr || doidAttr.getValue() === null)) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: "当前厂端授信额度为 0，必须选择模型计算序列号。" });
            return;
        }
    }

    // ==================== 辅助方法 ====================

    function getMoneyValue(formContext, field) {
        var attr = formContext.getAttribute(field);
        if (!attr) return null;
        return attr.getValue();
    }

    function setMoneyValue(formContext, field, value) {
        var attr = formContext.getAttribute(field);
        if (attr) {
            attr.setValue(value);
        }
    }

    function setStringValue(formContext, field, value) {
        var attr = formContext.getAttribute(field);
        if (attr) {
            attr.setValue(value);
        }
    }

    function clearLookup(formContext, field) {
        var attr = formContext.getAttribute(field);
        if (attr) {
            attr.setValue(null);
        }
    }

    // ==================== 最新生效模型计算序列号带出 ====================

    /**
     * 查询客户最新生效的厂端授信模型计算记录
     * 条件：mcs_accountid = 当前客户 AND mcs_status = 3（生效启用）
     * 取 createdon 最新的一条，回填 mcs_doid 和 mcs_initigrant
     */
    function retrieveLatestEffectiveProc(formContext, accountId) {
        var filter = "_mcs_accountid_value eq " + accountId + " and mcs_status eq 3";
        var query = "?$select=mcs_fca_procid,mcs_doid,mcs_initigrant&$filter=" + filter + "&$orderby=createdon desc&$top=1";

        Xrm.WebApi.retrieveMultipleRecords("mcs_fca_proc", query).then(
            function (result) {
                if (result.entities.length > 0) {
                    var proc = result.entities[0];
                    var doidAttr = formContext.getAttribute("mcs_doid");
                    if (doidAttr) {
                        doidAttr.setValue([{
                            id: proc.mcs_fca_procid,
                            name: proc.mcs_doid || "",
                            entityType: "mcs_fca_proc"
                        }]);
                    }
                    setMoneyValue(formContext, "mcs_initigrant", proc.mcs_initigrant || 0);
                } else {
                    clearLookup(formContext, "mcs_doid");
                    setMoneyValue(formContext, "mcs_initigrant", 0);
                }
                calculateAdjustedBalance(formContext);
            },
            function (error) {
                console.error("查询最新生效模型计算记录失败:", error);
                clearLookup(formContext, "mcs_doid");
                setMoneyValue(formContext, "mcs_initigrant", 0);
                calculateAdjustedBalance(formContext);
            }
        );
    }

    // ==================== 申请组织信息带出 ====================

    /**
     * 从当前系统用户带出申请组织信息
     * 取数链路：systemuser -> mcs_useraccount -> mcs_org
     * 沿 mcs_org 的【上级组织】链逐级取：组织 -> 事业部(10) -> 大区(20)
     * 再通过 mcs_regioncountryrelation 取国区、国家
     */
    function loadCurrentUserOrgInfo(formContext) {
        try {
            var globalContext = Xrm.Utility.getGlobalContext();
            var userId = globalContext.userSettings.userId;
            if (!userId) {
                console.warn("loadCurrentUserOrgInfo: 无法获取当前用户ID");
                return;
            }

            userId = userId.replace(/[{}]/g, "");

            // 若已存在值，不再覆盖
            if (getStringValue(formContext, "mcs_orgid") || getStringValue(formContext, "mcs_buid")) {
                console.log("loadCurrentUserOrgInfo: 组织字段已有值，跳过");
                return;
            }

            var filter = "_mcs_systemuserid_value eq " + userId + " and statecode eq 0";
            var query = "?$select=mcs_useraccountid,_mcs_orgid_value&$filter=" + filter + "&$top=1";

            Xrm.WebApi.retrieveMultipleRecords("mcs_useraccount", query).then(
                function (uaResult) {
                    if (!uaResult.entities || uaResult.entities.length === 0) {
                        console.warn("loadCurrentUserOrgInfo: 未找到当前用户对应的 mcs_useraccount 记录");
                        return;
                    }

                    var orgId = getLookupGuidFromResult(uaResult.entities[0], "mcs_orgid");
                    if (!orgId) {
                        console.warn("loadCurrentUserOrgInfo: mcs_useraccount.mcs_orgid 为空");
                        return;
                    }

                    loadOrgHierarchy(orgId).then(
                        function (orgInfo) {
                            setOrgValues(formContext, orgInfo);
                        },
                        function (error) {
                            console.error("loadCurrentUserOrgInfo: 加载组织层级失败:", error);
                        }
                    );
                },
                function (error) {
                    console.error("loadCurrentUserOrgInfo: 查询 mcs_useraccount 失败:", error);
                }
            );
        } catch (ex) {
            console.error("loadCurrentUserOrgInfo 异常:", ex);
        }
    }

    /**
     * 沿 mcs_org 上级组织链递归加载完整组织层级
     */
    function loadOrgHierarchy(orgId) {
        return new Promise(function (resolve, reject) {
            var orgs = [];

            function loadNext(id) {
                if (!id) {
                    resolve(buildOrgInfo(orgs));
                    return;
                }

                Xrm.WebApi.retrieveRecord("mcs_org", id, "?$select=mcs_name,mcs_organizationtype,mcs_parentorganization,mcs_buid,mcs_regionid,mcs_countryid").then(
                    function (orgResult) {
                        orgs.push(orgResult);
                        var parentId = getLookupGuidFromResult(orgResult, "mcs_parentorganization");
                        // 防止循环或到达根节点
                        if (!parentId || parentId === id) {
                            resolve(buildOrgInfo(orgs));
                            return;
                        }
                        loadNext(parentId);
                    },
                    function (error) {
                        reject(error);
                    }
                );
            }

            loadNext(orgId);
        });
    }

    /**
     * 根据组织链构建待回填信息，并查询大区-国家关系补全国区、国家
     */
    function buildOrgInfo(orgs) {
        return new Promise(function (resolve, reject) {
            var orgInfo = {
                orgName: "",
                buName: "",
                regionName: "",
                countryRegionName: "",
                countryName: ""
            };

            if (!orgs || orgs.length === 0) {
                resolve(orgInfo);
                return;
            }

            // 申请组织 = 当前组织名称
            orgInfo.orgName = orgs[0].mcs_name || "";

            // 沿链找事业部(10)、大区(20)
            var buOrg = null;
            var regionOrg = null;
            for (var i = 0; i < orgs.length; i++) {
                var type = orgs[i].mcs_organizationtype;
                if (!buOrg && type === 10) {
                    buOrg = orgs[i];
                }
                if (!regionOrg && type === 20) {
                    regionOrg = orgs[i];
                }
                if (buOrg && regionOrg) {
                    break;
                }
            }

            // 当前组织就是事业部时，直接用当前组织
            if (!buOrg && orgs[0].mcs_organizationtype === 10) {
                buOrg = orgs[0];
            }

            if (buOrg) {
                orgInfo.buName = buOrg.mcs_name || "";
                orgInfo.buId = getLookupGuidFromResult(buOrg, "mcs_buid");
            }

            if (regionOrg) {
                orgInfo.regionName = regionOrg.mcs_name || "";
                orgInfo.regionId = getLookupGuidFromResult(regionOrg, "mcs_regionid");
            }

            // 沿链查找直接维护的国家 lookup（备用）
            for (var j = 0; j < orgs.length; j++) {
                var countryIdFromOrg = getLookupGuidFromResult(orgs[j], "mcs_countryid");
                if (countryIdFromOrg) {
                    orgInfo.countryIdFromOrg = countryIdFromOrg;
                    break;
                }
            }

            // 通过大区-国家关系取国家和国区
            resolveCountryAndRegion(orgInfo, resolve, reject);
        });
    }

    /**
     * 查询 mcs_regioncountryrelation 补全国区、国家名称
     */
    function resolveCountryAndRegion(orgInfo, resolve, reject) {
        if (!orgInfo.regionId || !orgInfo.buId) {
            // 没有大区或事业部时，尝试用组织上维护的国家
            if (orgInfo.countryIdFromOrg) {
                Xrm.WebApi.retrieveRecord("mcs_country", orgInfo.countryIdFromOrg, "?$select=mcs_name").then(
                    function (countryResult) {
                        orgInfo.countryName = countryResult.mcs_name || "";
                        resolve(orgInfo);
                    },
                    function () {
                        resolve(orgInfo);
                    }
                );
            } else {
                resolve(orgInfo);
            }
            return;
        }

        var filter = "_mcs_regionid_value eq " + orgInfo.regionId + " and _mcs_buid_value eq " + orgInfo.buId;
        Xrm.WebApi.retrieveMultipleRecords("mcs_regioncountryrelation", "?$select=_mcs_countryid_value,_mcs_nrplatform_value&$filter=" + filter + "&$top=1").then(
            function (result) {
                if (result.entities && result.entities.length > 0) {
                    var rel = result.entities[0];
                    var countryId = getLookupGuidFromResult(rel, "mcs_countryid");
                    var nrId = getLookupGuidFromResult(rel, "mcs_nrplatform");

                    var promises = [];
                    if (countryId) {
                        promises.push(Xrm.WebApi.retrieveRecord("mcs_country", countryId, "?$select=mcs_name"));
                    } else {
                        promises.push(Promise.resolve(null));
                    }
                    if (nrId) {
                        promises.push(Xrm.WebApi.retrieveRecord("mcs_nationalregion", nrId, "?$select=mcs_name"));
                    } else {
                        promises.push(Promise.resolve(null));
                    }

                    Promise.all(promises).then(function (results) {
                        if (results[0]) {
                            orgInfo.countryName = results[0].mcs_name || "";
                        }
                        if (results[1]) {
                            orgInfo.countryRegionName = results[1].mcs_name || "";
                        }
                        resolve(orgInfo);
                    });
                } else {
                    // 关系表未命中，回退到组织上维护的国家
                    fallbackCountryFromOrg(orgInfo, resolve);
                }
            },
            function (error) {
                console.error("loadCurrentUserOrgInfo: 查询 mcs_regioncountryrelation 失败:", error);
                fallbackCountryFromOrg(orgInfo, resolve);
            }
        );
    }

    function fallbackCountryFromOrg(orgInfo, resolve) {
        if (orgInfo.countryIdFromOrg) {
            Xrm.WebApi.retrieveRecord("mcs_country", orgInfo.countryIdFromOrg, "?$select=mcs_name").then(
                function (countryResult) {
                    orgInfo.countryName = countryResult.mcs_name || "";
                    resolve(orgInfo);
                },
                function () {
                    resolve(orgInfo);
                }
            );
        } else {
            resolve(orgInfo);
        }
    }

    /**
     * 回填申请组织各字段
     * 表单上可见字段（id/code 结尾）与对应 name 字段均回填名称
     */
    function setOrgValues(formContext, orgInfo) {
        setOrgStringValue(formContext, "mcs_orgid", orgInfo.orgName);
        setOrgStringValue(formContext, "mcs_orgname", orgInfo.orgName);
        setOrgStringValue(formContext, "mcs_buid", orgInfo.buName);
        setOrgStringValue(formContext, "mcs_buname", orgInfo.buName);
        setOrgStringValue(formContext, "mcs_regionid", orgInfo.regionName);
        setOrgStringValue(formContext, "mcs_regionname", orgInfo.regionName);
        setOrgStringValue(formContext, "mcs_countryregionid", orgInfo.countryRegionName);
        setOrgStringValue(formContext, "mcs_countryregionname", orgInfo.countryRegionName);
        setOrgStringValue(formContext, "mcs_countrycode", orgInfo.countryName);
        setOrgStringValue(formContext, "mcs_countryname", orgInfo.countryName);
    }

    function getStringValue(formContext, field) {
        var attr = formContext.getAttribute(field);
        if (!attr) return null;
        var value = attr.getValue();
        return value === null || value === "" ? null : value;
    }

    function setOrgStringValue(formContext, field, value) {
        var attr = formContext.getAttribute(field);
        if (!attr) return;

        var maxLengthMap = {
            "mcs_orgid": 20,
            "mcs_orgname": 100,
            "mcs_buid": 20,
            "mcs_buname": 100,
            "mcs_regionid": 20,
            "mcs_regionname": 100,
            "mcs_countryregionid": 20,
            "mcs_countryregionname": 100,
            "mcs_countrycode": 20,
            "mcs_countryname": 100
        };

        value = value || "";
        var maxLength = maxLengthMap[field];
        if (maxLength && value.length > maxLength) {
            value = value.substring(0, maxLength);
            console.warn("setOrgStringValue: " + field + " 值超长，已截断至 " + maxLength + " 字符");
        }

        // 禁用字段仍需随表单提交
        attr.setSubmitMode("always");

        var currentValue = attr.getValue();
        if (currentValue === null || currentValue === "") {
            attr.setValue(value);
        }
    }

    /**
     * 从 Web API 结果中提取 Lookup GUID
     * 兼容 {id,name} / string / _field_value 三种返回格式
     */
    function getLookupGuidFromResult(result, field) {
        if (!result) return null;
        var val = result[field];
        if (val) {
            if (typeof val === "string") return val.replace(/[{}]/g, "");
            if (val.id) return val.id.replace(/[{}]/g, "");
        }
        var altField = "_" + field + "_value";
        if (result[altField]) return result[altField].replace(/[{}]/g, "");
        return null;
    }

    // ==================== BPP 提交审批 ====================

    /**
     * 生成 D365 记录链接，用于 BPP 审批页面打开申请单
     */
    function generateRecordUrl(formContext) {
        try {
            var recordId = formContext.data.entity.getId();
            if (!recordId) {
                return;
            }

            recordId = recordId.replace(/[{}]/g, "");
            var globalContext = Xrm.Utility.getGlobalContext();
            var clientUrl = globalContext.getClientUrl();
            var recordUrl = clientUrl + "/main.aspx?pagetype=entityrecord&etn=mcs_fca_quotaapp&id=" + recordId;

            setStringValue(formContext, "mcs_fca_quotaapp_url", recordUrl);
            console.log("已生成记录链接: " + recordUrl);
        } catch (ex) {
            console.error("生成记录链接失败:", ex);
        }
    }

    /**
     * 提交审批按钮入口
     * 校验必填后，将审批状态设为 2（审批中），触发后端 BPP 提交 Plugin
     */
    self.submitToBpp = function (primaryControl) {
        var formContext = primaryControl;

        // 基本校验
        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (!accountAttr || accountAttr.getValue() === null) {
            Xrm.Navigation.openAlertDialog({ text: "客户编码必填，无法提交审批。" });
            return;
        }

        var tobeGrantAttr = formContext.getAttribute("mcs_tobegrant");
        if (!tobeGrantAttr || tobeGrantAttr.getValue() === null || tobeGrantAttr.getValue() < 0) {
            Xrm.Navigation.openAlertDialog({ text: "调整厂端授信额度必填且不能小于 0。" });
            return;
        }

        var bppStatusAttr = formContext.getAttribute("mcs_bppstatus");
        var currentStatus = bppStatusAttr ? bppStatusAttr.getValue() : null;
        if (currentStatus !== null && currentStatus !== 1) {
            Xrm.Navigation.openAlertDialog({ text: "当前审批状态不是【申请】，无法重复提交。" });
            return;
        }

        var recordId = formContext.data.entity.getId();
        if (!recordId) {
            Xrm.Navigation.openAlertDialog({ text: "请先保存记录后再提交审批。" });
            return;
        }

        // 更新审批状态为 2（审批中），触发后端 Plugin 调用 mcs_bppstartapi
        Xrm.WebApi.updateRecord("mcs_fca_quotaapp", recordId, {
            "mcs_bppstatus": 2
        }).then(
            function () {
                Xrm.Navigation.openAlertDialog({ text: "审批已提交。" }).then(function () {
                    formContext.data.refresh(false);
                });
            },
            function (error) {
                console.error("提交审批失败:", error);
                Xrm.Navigation.openAlertDialog({ text: "提交审批失败: " + (error.message || JSON.stringify(error)) });
            }
        );
    };

    // ==================== 附件页签初始化 ====================

    /**
     * 初始化附件页签
     * 表单嵌入通用上传组件 Uploader.html，动态传入当前申请单上下文
     */
    self.initAttachmentTab = function (formContext) {
        try {
            var recordId = formContext.data.entity.getId();
            if (!recordId) {
                // 新建未保存记录，暂不初始化
                return;
            }

            recordId = recordId.replace(/[{}]/g, "");

            var uploaderControl = formContext.getControl("mcs_fca_quotaapp_uploader");
            if (!uploaderControl || typeof uploaderControl.getObject !== "function") {
                return;
            }

            var uploaderObj = uploaderControl.getObject();
            if (!uploaderObj) {
                return;
            }

            var data = {
                entityName: "mcs_fca_quotaapp",
                entityId: recordId
            };
            var src = "/WebResources/mcs_/CommonCore/Html/Uploader.html?data=" +
                encodeURIComponent(JSON.stringify(data));

            // 避免重复加载导致附件页签闪烁
            if (uploaderObj.src && uploaderObj.src.indexOf(src) === -1) {
                uploaderObj.src = src;
            }

            console.log("附件页签已初始化，关联申请单: " + recordId);
        } catch (ex) {
            console.error("初始化附件页签失败:", ex);
        }
    };

})(FcaQuotaAppForm);
