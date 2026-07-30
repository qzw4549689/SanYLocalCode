/**
 * 多语言帮助类
 * 从 WebResource 语言包加载文本，支持按当前用户语言切换。
 *
 * 使用公共语言包 ms_languagefile_<LCID>
 */
var LanguageHelper = LanguageHelper || {};

LanguageHelper._cache = LanguageHelper._cache || {};

/**
 * 加载指定语言包
 * @param {string} [webResourceName] - 可选，指定 WebResource 完整名称；默认按当前用户语言自动构造
 * @param {function} callback - 加载完成回调，参数为语言包字典
 */
LanguageHelper.loadLanguagePack = function (webResourceName, callback) {
    if (typeof webResourceName === "function") {
        callback = webResourceName;
        webResourceName = null;
    }

    var langId = Xrm.Utility.getGlobalContext().userSettings.languageId;
    var langCode = langId === 1033 ? "1033" : "2052";
    var resourceName = webResourceName || ("ms_languagefile_" + langCode);

    if (LanguageHelper._cache[resourceName]) {
        if (callback) callback(LanguageHelper._cache[resourceName]);
        return;
    }

    var req = new XMLHttpRequest();
    req.open("GET", Xrm.Utility.getGlobalContext().getClientUrl() + "/WebResources/" + resourceName, true);
    req.setRequestHeader("Accept", "application/json");
    req.onreadystatechange = function () {
        if (req.readyState === 4) {
            if (req.status === 200) {
                try {
                    LanguageHelper._cache[resourceName] = JSON.parse(req.responseText);
                    if (callback) callback(LanguageHelper._cache[resourceName]);
                } catch (e) {
                    console.error("加载语言包失败:", resourceName, e);
                    LanguageHelper._cache[resourceName] = {};
                    if (callback) callback({});
                }
            } else {
                console.warn("语言包未找到或加载失败:", resourceName, req.status);
                LanguageHelper._cache[resourceName] = {};
                if (callback) callback({});
            }
        }
    };
    req.send();
};

/**
 * 获取指定 key 的本地化文本
 * 如果语言包尚未加载，会同步返回 key 本身；建议在 onLoad 中先调用 loadLanguagePack。
 * @param {string} key - 语言键
 * @returns {string} 本地化文本，未找到时返回 key
 */
LanguageHelper.getLabel = function (key) {
    var langId = Xrm.Utility.getGlobalContext().userSettings.languageId;
    var langCode = langId === 1033 ? "1033" : "2052";
    var resourceName = "ms_languagefile_" + langCode;
    var dict = LanguageHelper._cache[resourceName];
    return dict && dict[key] ? dict[key] : key;
};

/**
 * 切换语言包（用于测试或强制指定语言）
 * @param {string} langCode - "1033" 或 "2052"
 */
LanguageHelper.setLanguage = function (langCode) {
    LanguageHelper._currentLangCode = langCode;
};

/**
 * 获取当前语言代码
 */
LanguageHelper.getCurrentLangCode = function () {
    if (LanguageHelper._currentLangCode) {
        return LanguageHelper._currentLangCode;
    }
    var langId = Xrm.Utility.getGlobalContext().userSettings.languageId;
    return langId === 1033 ? "1033" : "2052";
};
