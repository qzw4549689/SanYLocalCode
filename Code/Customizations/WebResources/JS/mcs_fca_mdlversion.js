/**
 * 厂端授信模型版本表表单脚本
 * 功能：
 * 1. 新建时设置默认值：是否生效=是、开始日期=今天、结束日期=9999/12/31
 * 2. 保存前校验：同一时间段内不能存在多个生效中的模型版本
 */

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

var FactoryCreditModelVersionForm = FactoryCreditModelVersionForm || {};

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

    /**
     * 表单加载事件
     */
    self.onLoad = function (executionContext) {
        var formContext = executionContext.getFormContext();

        // 预加载语言包（异步，不阻塞后续逻辑）
        if (typeof LanguageHelper !== "undefined") {
            LanguageHelper.loadLanguagePack();
        }

        // 仅新建记录时设置默认值
        if (formContext.ui.getFormType() !== 1) {
            return;
        }

        self.setDefaults(formContext);
    };

    /**
     * 设置默认值
     */
    self.setDefaults = function (formContext) {
        var today = new Date();
        today.setHours(0, 0, 0, 0);

        var endDate = new Date(9999, 11, 31); // 月份从 0 开始，11 表示 12 月
        endDate.setHours(0, 0, 0, 0);

        // 是否生效：默认"是"(1)
        var isActiveAttr = formContext.getAttribute("mcs_isactive");
        if (isActiveAttr && isActiveAttr.getValue() === null) {
            isActiveAttr.setValue(1);
        }

        // 开始日期：默认今天
        var validFromAttr = formContext.getAttribute("mcs_validfrom");
        if (validFromAttr && validFromAttr.getValue() === null) {
            validFromAttr.setValue(today);
        }

        // 结束日期：默认 9999/12/31
        var validEndAttr = formContext.getAttribute("mcs_validend");
        if (validEndAttr && validEndAttr.getValue() === null) {
            validEndAttr.setValue(endDate);
        }
    };

    // 标记是否正在执行手动保存，避免异步校验后再次触发 onSave 造成循环
    var _isManualSaving = false;

    /**
     * 表单保存前事件：校验是否存在重叠的生效版本
     * 注意：异步校验必须在查询前阻止默认保存，查询无冲突后再手动调用 save()
     */
    self.onSave = function (executionContext) {
        var formContext = executionContext.getFormContext();
        var eventArgs = executionContext.getEventArgs();

        // 手动保存触发时不再拦截，避免循环
        if (_isManualSaving) {
            return;
        }

        var isActiveAttr = formContext.getAttribute("mcs_isactive");
        if (!isActiveAttr || isActiveAttr.getValue() !== 1) {
            return; // 未勾选"生效"，不需要校验重叠
        }

        var validFromAttr = formContext.getAttribute("mcs_validfrom");
        var validEndAttr = formContext.getAttribute("mcs_validend");
        if (!validFromAttr || !validEndAttr) {
            return;
        }

        var validFrom = validFromAttr.getValue();
        var validEnd = validEndAttr.getValue();
        if (!validFrom || !validEnd) {
            return;
        }

        // 标准化为仅日期
        var newFrom = self.toDateOnly(validFrom);
        var newEnd = self.toDateOnly(validEnd);

        if (newEnd < newFrom) {
            eventArgs.preventDefault();
            Xrm.Navigation.openErrorDialog({ message: t("FcaMdlVersion_EndBeforeStart", "结束日期不能早于开始日期。") });
            return;
        }

        // 需要异步查询，先阻止默认保存
        eventArgs.preventDefault();

        var currentId = formContext.data.entity.getId();
        var filter = `mcs_isactive eq 1 and mcs_validfrom ne null and mcs_validend ne null`;
        if (currentId) {
            filter += ` and mcs_fca_mdlversionid ne ${currentId.replace("{", "").replace("}", "")}`;
        }

        Xrm.WebApi.retrieveMultipleRecords("mcs_fca_mdlversion", `?$select=mcs_fca_mdlversionid,mcs_versionid,mcs_validfrom,mcs_validend&$filter=${filter}`).then(
            function (result) {
                var overlap = null;
                for (var i = 0; i < result.entities.length; i++) {
                    var item = result.entities[i];
                    var existingFrom = self.parseDate(item.mcs_validfrom);
                    var existingEnd = self.parseDate(item.mcs_validend);

                    if (existingFrom && existingEnd && self.isOverlapping(newFrom, newEnd, existingFrom, existingEnd)) {
                        overlap = item;
                        break;
                    }
                }

                if (overlap) {
                    var versionName = overlap.mcs_versionid || overlap.mcs_fca_mdlversionid;
                    Xrm.Navigation.openErrorDialog({
                        message: t("FcaMdlVersion_OverlapVersion", "存在重叠的生效模型版本（{0}），请调整开始/结束日期或先停用其他版本。").replace("{0}", versionName)
                    });
                } else {
                    // 无冲突，执行手动保存
                    _isManualSaving = true;
                    formContext.data.save().then(
                        function () {
                            _isManualSaving = false;
                        },
                        function (error) {
                            _isManualSaving = false;
                            console.error("手动保存失败: ", error.message);
                        }
                    );
                }
            },
            function (error) {
                console.error("校验模型版本重叠失败: ", error.message);
                // 查询失败时不自动保存，避免在未知状态下写入数据
            }
        );
    };

    /**
     * 判断两个日期区间是否重叠
     */
    self.isOverlapping = function (start1, end1, start2, end2) {
        return start1 <= end2 && end1 >= start2;
    };

    /**
     * 将日期对象转换为仅日期（去除时间部分）
     */
    self.toDateOnly = function (date) {
        var d = new Date(date.getTime());
        d.setHours(0, 0, 0, 0);
        return d;
    };

    /**
     * 解析 Web API 返回的日期字符串
     */
    self.parseDate = function (dateString) {
        if (!dateString) {
            return null;
        }
        var d = new Date(dateString);
        d.setHours(0, 0, 0, 0);
        return d;
    };

})(FactoryCreditModelVersionForm);
