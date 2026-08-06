/**
 * 融资资源管理（mcs_fsm_resource）表单脚本
 * 功能：
 * 1. 机构类型=银行时，选择银行（mcs_bank_id）自动带出机构代码（mcs_bank.mcs_bankno）
 *    与机构名称（mcs_bank.mcs_name），并锁定只读；保险/其他时手工输入
 * 2. 金融产品名称（mcs_fsm_institution_products）多选选项集按机构类型筛选：
 *    银行(1)/保险(2) → 选项 1-11（禅道 #1572：保险与银行同一代码表）；其他(9) → 仅 Others(11)
 * 3. 国家→洲省→城市级联过滤（禅道 #1528）：
 *    洲省弹窗按所选国家过滤（mcs_state.mcs_countryid）；
 *    城市弹窗按所选洲省过滤（mcs_city.mcs_stateid）；
 *    国家变更清空洲省/城市，洲省变更清空城市
 * 触发：主窗体 onLoad（字段 onChange 在 onLoad 中程序化注册）
 */

var FsmResourceForm = (function () {
    "use strict";

    var self = {};

    // mcs_fsm_resource.mcs_fsm_institution_type 选项集值
    var INSTITUTION_TYPE = {
        BANK: 1,        // 银行
        INSURANCE: 2,   // 保险
        OTHER: 9        // 其他
    };

    // mcs_fsm_institution_products 多选选项集允许值
    // （禅道 #1572：保险与银行使用同一代码表 1-11；101-104 保险专属选项不再展示）
    var BANK_PRODUCT_VALUES = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
    var OTHER_PRODUCT_VALUES = [11]; // 其他类型仅允许 Others

    // 完整选项缓存（onLoad 时快照，避免 removeOption 后丢失标签）
    var allProductOptions = null;

    // 省市区级联字段（禅道 #1528）
    var FIELD_COUNTRY = "mcs_fsm_institution_country";   // 所在国家 → mcs_country
    var FIELD_PROVINCE = "mcs_fsm_institution_province"; // 洲省 → mcs_state
    var FIELD_CITY = "mcs_fsm_institution_city";         // 所在城市 → mcs_city
    var EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

    /**
     * 表单 onLoad 入口
     */
    self.onLoad = function (executionContext) {
        try {
            var formContext = executionContext.getFormContext();

            var typeAttr = formContext.getAttribute("mcs_fsm_institution_type");
            if (typeAttr) {
                typeAttr.addOnChange(onInstitutionTypeChange);
            }
            var bankAttr = formContext.getAttribute("mcs_bank_id");
            if (bankAttr) {
                bankAttr.addOnChange(onBankChange);
            }

            // 省市区级联：父级变更清空子级（禅道 #1528）
            var countryAttr = formContext.getAttribute(FIELD_COUNTRY);
            if (countryAttr) {
                countryAttr.addOnChange(function () {
                    clearLookup(formContext, FIELD_PROVINCE);
                    clearLookup(formContext, FIELD_CITY);
                });
            }
            var provinceAttr = formContext.getAttribute(FIELD_PROVINCE);
            if (provinceAttr) {
                provinceAttr.addOnChange(function () {
                    clearLookup(formContext, FIELD_CITY);
                });
            }

            // 省市区级联：弹窗按父级过滤
            var provinceControl = formContext.getControl(FIELD_PROVINCE);
            if (provinceControl) {
                provinceControl.addPreSearch(function () {
                    filterProvinceByCountry(formContext);
                });
            }
            var cityControl = formContext.getControl(FIELD_CITY);
            if (cityControl) {
                cityControl.addPreSearch(function () {
                    filterCityByProvince(formContext);
                });
            }

            // 快照完整产品选项（在筛选之前）
            var productAttr = formContext.getAttribute("mcs_fsm_institution_products");
            if (productAttr) {
                allProductOptions = productAttr.getOptions();
            }

            applyInstitutionTypeRules(formContext);
        } catch (e) {
            console.error("FsmResourceForm.onLoad 执行失败:", e);
        }
    };

    /**
     * 机构类型变更
     */
    function onInstitutionTypeChange(executionContext) {
        var formContext = executionContext.getFormContext();
        applyInstitutionTypeRules(formContext);
    }

    /**
     * 银行变更：带出/清空机构代码与机构名称
     */
    function onBankChange(executionContext) {
        var formContext = executionContext.getFormContext();
        populateFromBank(formContext);
    }

    /**
     * 按机构类型应用表单规则
     */
    function applyInstitutionTypeRules(formContext) {
        var typeAttr = formContext.getAttribute("mcs_fsm_institution_type");
        var type = typeAttr ? typeAttr.getValue() : null;
        var isBank = (type === INSTITUTION_TYPE.BANK);

        // 1. Bank 查找字段：银行时显示且必填；非银行时隐藏并清空
        var bankAttr = formContext.getAttribute("mcs_bank_id");
        var bankControl = formContext.getControl("mcs_bank_id");
        if (bankControl) {
            bankControl.setVisible(isBank);
        }
        if (bankAttr) {
            bankAttr.setRequiredLevel(isBank ? "required" : "none");
        }
        if (!isBank && bankAttr && bankAttr.getValue()) {
            // 从银行切换到其他类型：清空 Bank 及银行带出的机构代码/名称
            bankAttr.setValue(null);
            var codeAttr = formContext.getAttribute("mcs_fsm_institution_code");
            var nameAttr = formContext.getAttribute("mcs_fsm_institution_name");
            if (codeAttr) {
                codeAttr.setValue(null);
            }
            if (nameAttr) {
                nameAttr.setValue(null);
            }
        }

        // 2. 机构代码/名称：银行时只读（自动带出）；非银行时可编辑（手工输入）
        var codeControl = formContext.getControl("mcs_fsm_institution_code");
        var nameControl = formContext.getControl("mcs_fsm_institution_name");
        if (codeControl) {
            codeControl.setDisabled(isBank);
        }
        if (nameControl) {
            nameControl.setDisabled(isBank);
        }

        if (isBank) {
            // 已选银行则补齐带出值（如编辑已有记录打开表单）
            populateFromBank(formContext);
        }

        // 3. 金融产品多选按类型筛选
        var allowedValues = null; // null = 不筛选（类型未选时显示全部）
        if (type === INSTITUTION_TYPE.BANK || type === INSTITUTION_TYPE.INSURANCE) {
            allowedValues = BANK_PRODUCT_VALUES;
        } else if (type === INSTITUTION_TYPE.OTHER) {
            allowedValues = OTHER_PRODUCT_VALUES;
        }
        filterProductOptions(formContext, allowedValues);
    }

    /**
     * 从所选银行带出机构代码与机构名称
     * 银行清空时同步清空两个字段
     */
    function populateFromBank(formContext) {
        var bankAttr = formContext.getAttribute("mcs_bank_id");
        var codeAttr = formContext.getAttribute("mcs_fsm_institution_code");
        var nameAttr = formContext.getAttribute("mcs_fsm_institution_name");
        if (!bankAttr || !codeAttr || !nameAttr) {
            return;
        }

        var bankRef = bankAttr.getValue();
        if (!bankRef || bankRef.length === 0) {
            codeAttr.setValue(null);
            nameAttr.setValue(null);
            return;
        }

        var bankId = bankRef[0].id.replace(/[{}]/g, "");
        Xrm.WebApi.retrieveRecord("mcs_bank", bankId, "?$select=mcs_bankno,mcs_name").then(
            function (bank) {
                // 防御：异步返回时银行可能已被用户改选/清空
                var current = bankAttr.getValue();
                if (!current || current.length === 0 ||
                    current[0].id.replace(/[{}]/g, "").toLowerCase() !== bankId.toLowerCase()) {
                    return;
                }
                codeAttr.setValue(bank.mcs_bankno || null);
                nameAttr.setValue(bank.mcs_name || null);
            },
            function (error) {
                console.error("读取银行信息失败:", error.message);
            }
        );
    }

    /**
     * 清空 Lookup 字段值
     */
    function clearLookup(formContext, fieldName) {
        var attr = formContext.getAttribute(fieldName);
        if (attr && attr.getValue()) {
            attr.setValue(null);
        }
    }

    /**
     * 取 Lookup 字段的 GUID（无花括号小写），未选返回 null
     */
    function getLookupId(formContext, fieldName) {
        var attr = formContext.getAttribute(fieldName);
        var ref = attr ? attr.getValue() : null;
        if (!ref || ref.length === 0) {
            return null;
        }
        return ref[0].id.replace(/[{}]/g, "").toLowerCase();
    }

    /**
     * 洲省弹窗按所选国家过滤（mcs_state.mcs_countryid）；未选国家时显示空结果
     */
    function filterProvinceByCountry(formContext) {
        var countryId = getLookupId(formContext, FIELD_COUNTRY) || EMPTY_GUID;
        var fetchXml = "<fetch><entity name='mcs_state'>" +
            "<filter type='and'><condition attribute='mcs_countryid' operator='eq' value='" + countryId + "' /></filter>" +
            "</entity></fetch>";
        var ctrl = formContext.getControl(FIELD_PROVINCE);
        if (ctrl) {
            ctrl.addCustomFilter(fetchXml, "mcs_state");
        }
    }

    /**
     * 城市弹窗按所选洲省过滤（mcs_city.mcs_stateid）；未选洲省时显示空结果
     */
    function filterCityByProvince(formContext) {
        var provinceId = getLookupId(formContext, FIELD_PROVINCE) || EMPTY_GUID;
        var fetchXml = "<fetch><entity name='mcs_city'>" +
            "<filter type='and'><condition attribute='mcs_stateid' operator='eq' value='" + provinceId + "' /></filter>" +
            "</entity></fetch>";
        var ctrl = formContext.getControl(FIELD_CITY);
        if (ctrl) {
            ctrl.addCustomFilter(fetchXml, "mcs_city");
        }
    }

    /**
     * addOption 兼容封装：标准 OptionSet 控件签名为 addOption(text, value)；
     * FluentUI 多选选项集控件（multiselectoptionset）要求 addOption({text, value})
     */
    function addOptionCompat(control, text, value) {
        try {
            control.addOption(text, value);
        } catch (e) {
            control.addOption({ text: text, value: value });
        }
    }

    /**
     * 筛选金融产品多选选项
     * @param {object} formContext 表单上下文
     * @param {number[]|null} allowedValues 允许显示的选项值；null 表示显示全部
     */
    function filterProductOptions(formContext, allowedValues) {
        var productAttr = formContext.getAttribute("mcs_fsm_institution_products");
        var productControl = formContext.getControl("mcs_fsm_institution_products");
        if (!productAttr || !productControl || !allProductOptions) {
            return;
        }

        // 先移除全部选项，再按允许值加回（保证顺序与元数据一致）
        allProductOptions.forEach(function (opt) {
            productControl.removeOption(opt.value);
        });
        allProductOptions.forEach(function (opt) {
            if (allowedValues === null || allowedValues.indexOf(opt.value) !== -1) {
                addOptionCompat(productControl, opt.text, opt.value);
            }
        });

        // 清理已选值中不再可见的选项
        if (allowedValues !== null) {
            var selected = productAttr.getValue();
            if (selected && selected.length > 0) {
                var kept = selected.filter(function (v) {
                    return allowedValues.indexOf(v) !== -1;
                });
                if (kept.length !== selected.length) {
                    productAttr.setValue(kept);
                }
            }
        }
    }

    return self;
})();
