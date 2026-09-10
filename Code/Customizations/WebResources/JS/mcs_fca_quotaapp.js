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

var FcaQuotaAppForm = FcaQuotaAppForm || {};
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

    // 「安全交易基线额度调整为」的默认基准值（禅道 #1637）：带出默认值时记录，
    // 用户手工改为其他值视为「发生调整」，此时调整原因必填；与基准值一致视为未调整
    var defaultTobeGrant = null;

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

        // 预加载语言包（异步，不阻塞后续逻辑）
        if (typeof LanguageHelper !== "undefined") {
            LanguageHelper.loadLanguagePack();
        }

        registerFieldEvents(formContext);

        // 禅道 #1855：保存时后端可能回填序列号/模型额度/当前额度并默认「调整为」，
        // 保存完成后刷新调整基准值，保证 #1637 调整原因必填校验基准准确
        formContext.data.entity.addOnPostSave(function () {
            defaultTobeGrant = getMoneyValue(formContext, "mcs_tobegrant") || 0;
            updateReasonRequired(formContext);
        });

        // 调整后安全交易基线余额为公式计算字段，始终只读（禅道 #1637，值随「调整为」自动计算）
        setControlReadOnly(formContext, "mcs_tobebalance");

        if (formContext.ui.getFormType() === 1) {
            setDefaultValues(formContext);
            // 新建记录时，从当前系统用户带出申请组织信息
            loadCurrentUserOrgInfo(formContext);
        } else {
            // 已有记录：审批中/审批通过时全表单只读（禅道 #1283）
            applyBppStatusLockAsync(formContext);
            // 已有记录以保存值为调整基准：用户打开后未改动视为未调整（禅道 #1637）
            defaultTobeGrant = getMoneyValue(formContext, "mcs_tobegrant") || 0;
            updateReasonRequired(formContext);
        }

        // 初始化附件页签（通用上传组件）
        self.initAttachmentTab(formContext);
    };

    // ==================== 审批状态锁定（禅道 #1283） ====================

    /**
     * 按审批状态锁定表单：审批中(2)/审批通过(3) 全表单只读；申请(1)/驳回(4) 可编辑
     * 注意：mcs_bppstatus 未放在表单上，getAttribute 读到 null 时需从服务端异步读取
     * （与 submitToBpp 中 L821 注释记录的坑一致）
     */
    function applyBppStatusLockAsync(formContext) {
        var attr = formContext.getAttribute("mcs_bppstatus");
        if (attr && attr.getValue() !== null && attr.getValue() !== undefined) {
            lockFormIfApproving(formContext, attr.getValue());
            return;
        }

        var recordId = formContext.data.entity.getId();
        if (!recordId) return;
        recordId = recordId.replace(/[{}]/g, "");

        Xrm.WebApi.retrieveRecord("mcs_fca_quotaapp", recordId, "?$select=mcs_bppstatus").then(
            function (result) {
                lockFormIfApproving(formContext, result.mcs_bppstatus);
            },
            function (error) {
                console.error("读取审批状态失败(锁定判断):", error);
            }
        );
    }

    function lockFormIfApproving(formContext, bppStatus) {
        if (bppStatus === 2 || bppStatus === 3) {
            lockAllFields(formContext);
        }
    }

    /**
     * 全表单字段只读（复用 mcs_credit_record.js lockAllFields 模式）
     * 控件判空容错：WebResource/HTML 控件无 setDisabled 自动跳过
     */
    function lockAllFields(formContext) {
        var allControls = formContext.ui.controls.get();
        allControls.forEach(function (control) {
            if (control && control.setDisabled) {
                control.setDisabled(true);
            }
        });
    }

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

        var tobeGrantAttr = formContext.getAttribute("mcs_tobegrant");
        if (tobeGrantAttr) {
            tobeGrantAttr.addOnChange(function () {
                calculateAdjustedBalance(formContext);
                // 禅道 #1637：手工改动「调整为」后联动调整原因必填
                updateReasonRequired(formContext);
            });
        }
    }

    // ==================== 默认值设置 ====================

    function setDefaultValues(formContext) {
        // 审批状态 = 申请
        setPicklistIfNull(formContext, "mcs_bppstatus", 1);

        // 禅道 #1855：「调整为/调整后余额」不再默认 0，保持空值由后端保存时按基准值回填
        // （空 = 用户未手工调整；若默认 0，后端无法区分「未动」与「故意调 0」）
    }

    function setPicklistIfNull(formContext, field, value) {
        var attr = formContext.getAttribute(field);
        if (attr && attr.getValue() === null) {
            attr.setValue(value);
        }
    }

    // ==================== 客户编码变更 ====================

    /**
     * 客户编码变更时：
     * 1. 带出客户名称、客户等级
     * 2. 带出中信保额度信息
     * 禅道 #1855：厂端授信额度(mcs_fca_quota)/模型计算(mcs_fca_proc)属基础数据，
     * 前端不再实时带出，统一在保存时由后端 FcaQuotaAppProcSyncPlugin 以系统身份回填
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

        // 带出中信保额度信息
        retrieveSinosureQuota(formContext, accountId);
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

    // ==================== 模型计算/额度数据带出（禅道 #1855） ====================
    // 厂端授信额度/余额（mcs_fca_quota）与模型计算（mcs_fca_proc）属基础数据，前端不再直查，
    // 统一保存时由后端 FcaQuotaAppProcSyncPlugin 以系统身份回填（序列号/模型额度/当前额度/余额），
    // 无模型计算/额度表读权限的角色保存后即可看到回填值

    // ==================== 调整后余额计算 ====================

    /**
     * 调整后安全交易基线余额 = 安全交易基线额度调整为 - 安全交易基线额度 + 安全交易基线余额
     */
    function calculateAdjustedBalance(formContext) {
        var tobeGrant = getMoneyValue(formContext, "mcs_tobegrant") || 0;
        var sellerGrant = getMoneyValue(formContext, "mcs_sellergrant") || 0;
        var sellerBalance = getMoneyValue(formContext, "mcs_sellerbalance") || 0;

        var adjustedBalance = tobeGrant - sellerGrant + sellerBalance;
        setMoneyValue(formContext, "mcs_tobebalance", adjustedBalance);
    }

    // ==================== 调整原因必填联动（禅道 #1637） ====================

    /**
     * 调整原因必填联动：「安全交易基线额度调整为」≠ 默认基准值（模型计算额度或当前安全交易基线额度）
     * 视为发生调整，调整原因设为必填；一致（未调整）时非必填。字段始终显示，仅控必填星号。
     */
    function updateReasonRequired(formContext) {
        var reasonAttr = formContext.getAttribute("mcs_reason");
        if (!reasonAttr) return;
        var tobeGrant = getMoneyValue(formContext, "mcs_tobegrant") || 0;
        var adjusted = defaultTobeGrant !== null && tobeGrant !== defaultTobeGrant;
        reasonAttr.setRequiredLevel(adjusted ? "required" : "none");
    }

    /**
     * 设置字段只读且随表单提交（禁用控件默认不提交，公式计算字段需始终提交）
     */
    function setControlReadOnly(formContext, field) {
        var control = formContext.getControl(field);
        if (control && control.setDisabled) {
            control.setDisabled(true);
        }
        var attr = formContext.getAttribute(field);
        if (attr) {
            attr.setSubmitMode("always");
        }
    }

    // ==================== 保存前校验 ====================

    function validateOnSave(formContext, eventArgs) {
        // 客户编码必填
        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (!accountAttr || accountAttr.getValue() === null) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_CustCodeRequired", "客户编码必填。") });
            return;
        }

        // 安全交易基线额度调整为 ≥ 0（为空时由后端按基准值默认回填，禅道 #1855）
        var tobeGrantAttr = formContext.getAttribute("mcs_tobegrant");
        if (tobeGrantAttr && tobeGrantAttr.getValue() !== null && tobeGrantAttr.getValue() < 0) {
            eventArgs.preventDefault();
            Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_AdjustQuotaNegative", "安全交易基线额度调整为不能小于 0。") });
            return;
        }

        // 禅道 #1855：「额度为 0 必须选序列号」校验移至服务端 FcaQuotaAppProcSyncPlugin
        // （无额度/模型计算读权限的角色前端取不到数据，前端校验会在后端回填前误拦截）
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
     * 提交审批按钮入口
     * 校验必填后，将审批状态设为 2（审批中），触发后端 BPP 提交 Plugin
     */
    self.submitToBpp = function (primaryControl) {
        var formContext = primaryControl;

        // 基本校验
        var accountAttr = formContext.getAttribute("mcs_accountid");
        if (!accountAttr || accountAttr.getValue() === null) {
            Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_CustCodeRequiredSubmit", "客户编码必填，无法提交审批。") });
            return;
        }

        var tobeGrantAttr = formContext.getAttribute("mcs_tobegrant");
        if (!tobeGrantAttr || tobeGrantAttr.getValue() === null || tobeGrantAttr.getValue() < 0) {
            Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_AdjustQuotaRequiredSubmit", "安全交易基线额度调整为必填且不能小于 0。") });
            return;
        }

        // 禅道 #1637：发生调整（调整为 ≠ 默认基准值）时调整原因必填
        var submitAdjusted = defaultTobeGrant !== null && (tobeGrantAttr.getValue() || 0) !== defaultTobeGrant;
        var reasonAttr = formContext.getAttribute("mcs_reason");
        var reasonValue = reasonAttr ? reasonAttr.getValue() : null;
        if (submitAdjusted && (!reasonValue || !reasonValue.trim())) {
            Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_AdjustReasonRequired", "已调整安全交易基线额度，请填写调整原因。") });
            return;
        }

        var recordId = formContext.data.entity.getId();
        if (!recordId) {
            Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_SaveBeforeSubmit", "请先保存记录后再提交审批。") });
            return;
        }

        // 从服务端读取最新审批状态（mcs_bppstatus 可能未放在表单上，getAttribute 读到 null 会导致校验失效）
        Xrm.WebApi.retrieveRecord("mcs_fca_quotaapp", recordId, "?$select=mcs_bppstatus").then(
            function (result) {
                var currentStatus = result.mcs_bppstatus;
                // 允许【申请】(1) 和【驳回】(4) 状态下重新提交
                if (currentStatus !== null && currentStatus !== undefined && currentStatus !== 1 && currentStatus !== 4) {
                    Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_InvalidBppStatus", "当前审批状态不是【申请】或【驳回】，无法提交审批。") });
                    return;
                }

                // 更新审批状态为 2（审批中），触发后端 Plugin 调用 mcs_bppstartapi
                Xrm.WebApi.updateRecord("mcs_fca_quotaapp", recordId, {
                    "mcs_bppstatus": 2
                }).then(
                    function () {
                        Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_Submitted", "审批已提交。") }).then(function () {
                            // 提交成功后立即锁定全表单（data.refresh 不会重新触发 onLoad，禅道 #1283）
                            lockAllFields(formContext);
                            formContext.data.refresh(false);
                        });
                    },
                    function (error) {
                        console.error("提交审批失败:", error);
                        Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_SubmitFailed", "提交审批失败: ") + (error.message || JSON.stringify(error)) });
                    }
                );
            },
            function (error) {
                console.error("读取审批状态失败:", error);
                Xrm.Navigation.openAlertDialog({ text: t("FcaQuotaApp_ReadStatusFailed", "读取审批状态失败，请刷新后重试。") });
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
