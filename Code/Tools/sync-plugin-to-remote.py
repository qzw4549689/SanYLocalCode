#!/usr/bin/env python3
"""
D365 本地测试项目 → 远程主项目同步脚本

用法：
    cd Code/Tools
    python3 sync-plugin-to-remote.py

功能：
    1. 按 FILE_MAP / FILE_MAP_API 读取本地源文件
    2. 按 NAMESPACE_MAP 替换命名空间前缀
    3. 通过 scp 写入远程服务器目标路径（业务插件 → D365Extension.Sales，Custom API 插件 → D365ExtensionApi.Sales）
    4. 更新远程两个项目 csproj 的 Compile 引用
    5. 触发远程分别编译验证

配置：
    修改本文件顶部的 REMOTE_HOST / REMOTE_BASE_DIR / FILE_MAP / FILE_MAP_API 即可复用。
"""

import os
import re
import sys
import subprocess
import tempfile
from pathlib import Path, PureWindowsPath

# ==================== 用户可配置区域 ====================

# 远程服务器 SSH 别名（需在 ~/.ssh/config 中配置）
REMOTE_HOST = "tx-windows"

# 远程主项目根目录（Windows 路径）
REMOTE_PROJECT_DIR = r"C:\Projects\D365\D365\SanyD365.D365Extension.Sales"

# 远程 Custom API 项目根目录（Custom API 实现插件归属项目，如 TradeStPayTerm.Api）
REMOTE_PROJECT_DIR_API = r"C:\Projects\D365\D365\SanyD365.D365ExtensionApi.Sales"

# 本地项目根目录（相对于本脚本所在位置）
LOCAL_ROOT = Path(__file__).resolve().parent.parent.parent / "Code" / "Customizations" / "Plugins"

# 命名空间映射：本地前缀 -> 远程前缀（按从长到短排序，避免部分替换）
NAMESPACE_MAP = {
    "SanyD365.Plugins.CofaceIntegration.Api": "SanyD365.D365Extension.Sales.Application.Sales.CofaceIntegration.Api",
    "SanyD365.Plugins.CofaceIntegration.Parser": "SanyD365.D365Extension.Sales.Application.Sales.CofaceIntegration.Parser",
    "SanyD365.Plugins.CofaceIntegration.Token": "SanyD365.D365Extension.Sales.Application.Sales.CofaceIntegration.Token",
    "SanyD365.Plugins.CofaceIntegration.Plugin": "SanyD365.D365Extension.Sales.Plugins.CofaceIntegration",
    "SanyD365.Plugins.CofaceIntegration": "SanyD365.D365Extension.Sales.Application.Sales.CofaceIntegration",
    "SanyD365.Plugins.BppIntegration.Plugin": "SanyD365.D365Extension.Sales.Plugins.CreditRecord",
    "SanyD365.Plugins.Account": "SanyD365.D365Extension.Sales.Plugins.Account",
    "SanyD365.Plugins.CustomerMasterData.Validation": "SanyD365.D365Extension.Sales.Plugins.Account",
    "SanyD365.Plugins.TradeStPayTerm.Api": "SanyD365.D365ExtensionApi.Sales.Apis.TradeStPayTerm",
    "SanyD365.Plugins.FactoryCredit.Api": "SanyD365.D365ExtensionApi.Sales.Apis.FactoryCredit",
    "SanyD365.Plugins.TradeStPayTerm": "SanyD365.D365Extension.Sales.Plugins.TradeStPayTerm",
    "SanyD365.Plugins.CustomerTag": "SanyD365.D365Extension.Sales.Plugins.CustomerTag",
    "SanyD365.Plugins.FactoryCredit": "SanyD365.D365Extension.Sales.Plugins.FactoryCredit",
    "SanyD365.Plugins.FinancingManagement": "SanyD365.D365Extension.Sales.Plugins.FinancingManagement",
}

# 文件映射表：本地相对路径 -> 远程相对路径
# 远程路径使用 Windows 风格反斜杠，会写入 csproj
FILE_MAP = {
    # Coface Application 层
    "CofaceIntegration/CofaceApiConfig.cs": r"Application\Sales\CofaceIntegration\CofaceApiConfig.cs",
    "CofaceIntegration/CofaceConfigHelper.cs": r"Application\Sales\CofaceIntegration\CofaceConfigHelper.cs",
    "CofaceIntegration/JsonElementExtensions.cs": r"Application\Sales\CofaceIntegration\JsonElementExtensions.cs",
    "CofaceIntegration/CofaceCountryConfig.cs": r"Application\Sales\CofaceIntegration\CofaceCountryConfig.cs",
    "CofaceIntegration/CofaceCountryConfigHelper.cs": r"Application\Sales\CofaceIntegration\CofaceCountryConfigHelper.cs",
    "CofaceIntegration/CofaceExchangeRateHelper.cs": r"Application\Sales\CofaceIntegration\CofaceExchangeRateHelper.cs",
    "CofaceIntegration/CofaceNaceMappingHelper.cs": r"Application\Sales\CofaceIntegration\CofaceNaceMappingHelper.cs",
    "CofaceIntegration/CofaceQualitativeMappingHelper.cs": r"Application\Sales\CofaceIntegration\CofaceQualitativeMappingHelper.cs",
    "CofaceIntegration/CofaceOrderInfoHelper.cs": r"Application\Sales\CofaceIntegration\CofaceOrderInfoHelper.cs",
    "CofaceIntegration/Api/CofaceApiService.cs": r"Application\Sales\CofaceIntegration\CofaceApiService.cs",
    "CofaceIntegration/Parser/FullReportParser.cs": r"Application\Sales\CofaceIntegration\FullReportParser.cs",
    "CofaceIntegration/Parser/Urba360Parser.cs": r"Application\Sales\CofaceIntegration\Urba360Parser.cs",
    "CofaceIntegration/Token/CofaceTokenManager.cs": r"Application\Sales\CofaceIntegration\CofaceTokenManager.cs",

    # Coface Plugin 层
    "CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs": r"Plugins\CofaceIntegration\CofaceIntegrationDataSyncPlugin.cs",
    "CofaceIntegration/Plugin/CofaceSearchCompanyPlugin.cs": r"Plugins\CofaceIntegration\CofaceSearchCompanyPlugin.cs",
    "CofaceIntegration/Plugin/CofacePlaceOrderPlugin.cs": r"Plugins\CofaceIntegration\CofacePlaceOrderPlugin.cs",

    # BPP Callback Plugin
    "BppIntegration/Plugin/BppCallbackPlugin.cs": r"Plugins\CreditRecord\CreditRecordBppCallbackPlugin.cs",

    # Account 字段校验 Plugin（8 字段校验已移除，仅保留 blacklist/creditgrant）
    "Account/AutoNumber/AccountValidationPlugin.cs": r"Plugins\Account\AccountCreditValidationPlugin.cs",

    # 客户主数据字段校验 Plugin（新增）
    "CustomerMasterData/Validation/CustomerMasterDataValidationPlugin.cs": r"Plugins\Account\CustomerMasterDataCreditValidationPlugin.cs",

    # 成交条件样板库 Plugin
    "TradeStPayTerm/AutoNumber/TradeStPayTermAutoNumberPlugin.cs": r"Plugins\TradeStPayTerm\TradeStPayTermAutoNumberPlugin.cs",
    "TradeStPayTerm/Validation/TradeStPayTermValidationPlugin.cs": r"Plugins\TradeStPayTerm\TradeStPayTermValidationPlugin.cs",
    "TradeStPayTerm/Sharing/TradeStPayTermSharePlugin.cs": r"Plugins\TradeStPayTerm\TradeStPayTermSharePlugin.cs",

    # 客户信用标签 Plugin
    "CustomerTag/AutoNumber/CustomerTagInitPlugin.cs": r"Plugins\CustomerTag\CustomerTagInitPlugin.cs",

    # 厂端授信 Plugin
    "FactoryCredit/ProcActivation/FcaProcActivationPlugin.cs": r"Plugins\FactoryCredit\FcaProcActivationPlugin.cs",
    "FactoryCredit/Calculation/FcaProcCalculationPlugin.cs": r"Plugins\FactoryCredit\FcaProcCalculationPlugin.cs",
    "FactoryCredit/Bpp/FcaQuotaAppBppIntegrationPlugin.cs": r"Plugins\FactoryCredit\Bpp\FcaQuotaAppBppIntegrationPlugin.cs",
    "FactoryCredit/Bpp/FcaQuotaAppBppCallbackPlugin.cs": r"Plugins\FactoryCredit\Bpp\FcaQuotaAppBppCallbackPlugin.cs",
    # FinancingManagement BPP
    "FinancingManagement/Bpp/FsmDataBppIntegrationPlugin.cs": r"Plugins\FinancingManagement\Bpp\FsmDataBppIntegrationPlugin.cs",
    "FinancingManagement/Bpp/FsmDataBppCallbackPlugin.cs": r"Plugins\FinancingManagement\Bpp\FsmDataBppCallbackPlugin.cs",
    # FinancingManagement 融资资源（禅道 #1433）
    "FinancingManagement/Resource/FsmResourceStateSyncPlugin.cs": r"Plugins\FinancingManagement\Resource\FsmResourceStateSyncPlugin.cs",
    "FinancingManagement/Resource/FsmResourceDeleteGuardPlugin.cs": r"Plugins\FinancingManagement\Resource\FsmResourceDeleteGuardPlugin.cs",
    # FinancingManagement 融资资源机构代码重复校验（禅道 #1512）
    "FinancingManagement/Resource/FsmResourceDuplicationCheckPlugin.cs": r"Plugins\FinancingManagement\Resource\FsmResourceDuplicationCheckPlugin.cs",
    # FinancingManagement 融资落实订单号唯一校验（禅道 #1511）
    "FinancingManagement/Detail/FsmDetailDataDuplicationCheckPlugin.cs": r"Plugins\FinancingManagement\Detail\FsmDetailDataDuplicationCheckPlugin.cs",
    "FactoryCredit/Bpp/Services/QuotaActivationService.cs": r"Plugins\FactoryCredit\Bpp\Services\QuotaActivationService.cs",
    "FactoryCredit/Bpp/Services/QuotaRecordService.cs": r"Plugins\FactoryCredit\Bpp\Services\QuotaRecordService.cs",
    "FactoryCredit/Calculation/Services/CalculationLogService.cs": r"Plugins\FactoryCredit\Calculation\Services\CalculationLogService.cs",
    "FactoryCredit/Calculation/Services/CreditRejectCheckService.cs": r"Plugins\FactoryCredit\Calculation\Services\CreditRejectCheckService.cs",
    "FactoryCredit/Calculation/Services/CurrencyConversionHelper.cs": r"Plugins\FactoryCredit\Calculation\Services\CurrencyConversionHelper.cs",
    "FactoryCredit/Calculation/Services/CustomerAccountResolver.cs": r"Plugins\FactoryCredit\Calculation\Services\CustomerAccountResolver.cs",
    "FactoryCredit/Calculation/Services/CustomerCategoryService.cs": r"Plugins\FactoryCredit\Calculation\Services\CustomerCategoryService.cs",
    "FactoryCredit/Calculation/Services/ModelParameterInfo.cs": r"Plugins\FactoryCredit\Calculation\Services\ModelParameterInfo.cs",
    "FactoryCredit/Calculation/Services/ModelParameterService.cs": r"Plugins\FactoryCredit\Calculation\Services\ModelParameterService.cs",
    "FactoryCredit/Calculation/Services/ModelVersionService.cs": r"Plugins\FactoryCredit\Calculation\Services\ModelVersionService.cs",
    "FactoryCredit/Calculation/Services/OverdueAdjustmentService.cs": r"Plugins\FactoryCredit\Calculation\Services\OverdueAdjustmentService.cs",
    "FactoryCredit/Calculation/Services/ThreeFactorCalculationService.cs": r"Plugins\FactoryCredit\Calculation\Services\ThreeFactorCalculationService.cs",
}

# Custom API 文件映射表：本地相对路径 -> D365ExtensionApi.Sales 项目相对路径
# Custom API 实现插件归属 D365ExtensionApi.Sales 项目（McsCustomAPI 解决方案），
# 与业务插件（D365Extension.Sales / McsPlugin 解决方案）分开同步，避免跨解决方案依赖。
FILE_MAP_API = {
    # 成交条件样板库查询 Custom API
    "TradeStPayTerm.Api/QueryTradeStPayTermPlugin.cs": r"Apis\TradeStPayTerm\QueryTradeStPayTermPlugin.cs",
    "TradeStPayTerm.Api/TradeStPayTermQueryService.cs": r"Apis\TradeStPayTerm\TradeStPayTermQueryService.cs",
    # 厂端授信余额调整 Custom API
    "FactoryCredit.Api/AdjustFcaQuotaBalancePlugin.cs": r"Apis\FactoryCredit\AdjustFcaQuotaBalancePlugin.cs",
    "FactoryCredit.Api/FcaQuotaAdjustService.cs": r"Apis\FactoryCredit\FcaQuotaAdjustService.cs",
}

# csproj 中 Compile 引用的排序分组（可选，保持 csproj 可读性）
# 键：远程相对路径前缀；值：该组在 csproj 中的插入位置描述
# 实际脚本使用简单追加 + 去重

# 插件类名重命名映射：本地类名 -> 远程类名（仅远程命名规范要求改名的类）
PLUGIN_RENAME_MAP = {
    "CofaceDataSyncPlugin": "CofaceIntegrationDataSyncPlugin",
    "AccountValidationPlugin": "AccountCreditValidationPlugin",
    "CustomerMasterDataValidationPlugin": "CustomerMasterDataCreditValidationPlugin",
}

# 远程保持原生 IPlugin 运行的插件（不做 PluginBase 转换）
# 这些 BPP 框架相关插件已以 IPlugin 形态随 uat 发布并运行正常，未经 PluginBase 化验证，保持现状
PLUGINBASE_SKIP_FILES = {
    "FactoryCredit/Bpp/FcaQuotaAppBppIntegrationPlugin.cs",
    "FactoryCredit/Bpp/FcaQuotaAppBppCallbackPlugin.cs",
    "FinancingManagement/Bpp/FsmDataBppIntegrationPlugin.cs",
    "FinancingManagement/Bpp/FsmDataBppCallbackPlugin.cs",
}

# ==================== 工具函数 ====================

def run(cmd, check=True, capture_output=True):
    """执行本地 shell 命令。"""
    print(f"$ {' '.join(cmd)}")
    result = subprocess.run(cmd, capture_output=capture_output, text=True)
    if capture_output:
        if result.stdout:
            print(result.stdout)
        if result.stderr:
            print(result.stderr, file=sys.stderr)
    if check and result.returncode != 0:
        raise RuntimeError(f"命令失败: {' '.join(cmd)} (exit {result.returncode})")
    return result


def transform_content(content: str) -> str:
    """按 NAMESPACE_MAP 替换命名空间前缀。"""
    # 按长度降序，避免短前缀先替换导致长前缀无法匹配
    for local_ns, remote_ns in sorted(NAMESPACE_MAP.items(), key=lambda x: -len(x[0])):
        content = content.replace(local_ns, remote_ns)
    return content


def transform_iplugin_to_pluginbase(content: str, local_class_name: str, remote_class_name: str = None) -> str:
    """
    本地 IPlugin 框架 → 远程 PluginBase 框架转换。
    保持业务逻辑一致，只转换入口、类名和依赖注入方式。
    """
    if remote_class_name is None:
        remote_class_name = local_class_name

    # 1. 添加 MSLibrary using
    if "using MSLibrary.D365.Common;" not in content:
        content = content.replace(
            "using Microsoft.Xrm.Sdk;",
            "using MSLibrary.D365.Common;\nusing MSLibrary.D365.Common.Context;\nusing MSLibrary.D365.Common.Plugins;\nusing Microsoft.Xrm.Sdk;"
        )

    # 2. 类声明 IPlugin → PluginBase
    content = content.replace(
        f"public class {local_class_name} : IPlugin",
        f"public class {remote_class_name} : PluginBase"
    )

    # 3. 方法签名 Execute → InnerExecute
    content = content.replace(
        "public void Execute(IServiceProvider serviceProvider)",
        "public override void InnerExecute(IPluginExecutionContext context)"
    )

    # 4. 替换 IPlugin Execute 入口初始化代码（多种写法兼容）
    # 匹配从方法体开始到第一个 tracer.Trace 或 try 之前的 serviceProvider 初始化代码
    import re

    # 模式1: 直接声明四行 serviceProvider 获取
    pattern1 = re.compile(
        r"public override void InnerExecute\(IPluginExecutionContext context\)\s*\{\s*"
        r"(?:var|IPluginExecutionContext)\s+context\s*=\s*\(IPluginExecutionContext\)serviceProvider\.GetService\(typeof\(IPluginExecutionContext\)\);\s*"
        r"(?:var|IOrganizationServiceFactory)\s+\w+\s*=\s*\(IOrganizationServiceFactory\)serviceProvider\.GetService\(typeof\(IOrganizationServiceFactory\)\);\s*"
        r"(?:var|IOrganizationService)\s+service\s*=\s*\w+\.CreateOrganizationService\(context\.UserId\);\s*"
        r"(?:var|ITracingService)\s+tracer\s*=\s*\(ITracingService\)serviceProvider\.GetService\(typeof\(ITracingService\)\);\s*"
    )

    replacement = """public override void InnerExecute(IPluginExecutionContext context)
        {
            var service = ContextContainer.GetValue<IOrganizationService>(ContextTypes.OrgService);
            var tracer = ContextContainer.GetValue<ITracingService>(ContextTypes.TracingService);
"""
    content = pattern1.sub(replacement, content)

    # 5. 系统身份服务获取方式转换（2026-07-20 新增）
    # 本地 IPlugin 风格用 factory 变量，远程 PluginBase 风格从 ContextContainer 取 OrgServiceFactory
    content = content.replace(
        "IOrganizationService systemService = factory.CreateOrganizationService(null);",
        "IOrganizationService systemService = ContextContainer.GetValue<IOrganizationServiceFactory>(ContextTypes.OrgServiceFactory).CreateOrganizationService(null);"
    )

    return content


def assert_pluginbase_transform(local_rel: str, content: str):
    """
    PluginBase 转换结果 fail-fast 校验（2026-07-20 新增）。
    转换静默失效是历史主要踩坑点（写法略变导致正则不命中，远程编译才暴露），
    此处本地立即报错并指出残留行，不再等远程编译。
    """
    problems = []
    if re.search(r":\s*IPlugin\b", content):
        problems.append("类声明未转换为 PluginBase")
    for i, line in enumerate(content.splitlines(), 1):
        if "serviceProvider" in line:
            problems.append(f"第 {i} 行残留 serviceProvider: {line.strip()}")
        if re.search(r"\bfactory\.CreateOrganizationService", line):
            problems.append(f"第 {i} 行残留 factory.CreateOrganizationService: {line.strip()}")
    if "ContextContainer.GetValue<IOrganizationService>(ContextTypes.OrgService)" not in content:
        problems.append(
            "入口初始化代码未转换为 ContextContainer（入口正则不命中）："
            "请检查入口是否为固定连续 4 行（context/factory/service/tracer，显式类型、变量名固定），"
            "其他初始化代码必须放在这 4 行之后"
        )
    if problems:
        msg = "\n  - ".join(problems)
        raise SystemExit(f"❌ PluginBase 转换校验失败: {local_rel}\n  - {msg}")


def maybe_transform_plugin(content: str, local_rel: str, project_dir: str) -> str:
    """
    自动识别 Plugin 文件并做 IPlugin → PluginBase 框架转换（2026-07-20 新增，替代原 if/elif 硬编码清单）。
    判定规则：同步目标是 Sales 主项目、本地类声明为 ": IPlugin"、且不在 PLUGINBASE_SKIP_FILES 中。
    类名重命名集中在 PLUGIN_RENAME_MAP，默认类名不变。
    """
    if project_dir != REMOTE_PROJECT_DIR or local_rel in PLUGINBASE_SKIP_FILES:
        return content

    m = re.search(r"public\s+class\s+(\w+)\s*:\s*IPlugin\b", content)
    if not m:
        return content

    local_class = m.group(1)
    remote_class = PLUGIN_RENAME_MAP.get(local_class, local_class)
    transformed = transform_iplugin_to_pluginbase(content, local_class, remote_class)
    assert_pluginbase_transform(local_rel, transformed)
    return transformed


def to_unix_path(win_path: str) -> str:
    """把 Windows 反斜杠路径转成 ssh/scp 可用的正斜杠。"""
    return win_path.replace("\\", "/")


def remote_exists(host: str, remote_path: str) -> bool:
    """检查远程文件是否存在。"""
    unix_path = to_unix_path(remote_path)
    result = subprocess.run(
        ["ssh", host, f"if exist \"{remote_path}\" (exit 0) else (exit 1)"],
        capture_output=True, text=True
    )
    return result.returncode == 0


def upload_file(local_path: Path, host: str, remote_path: str):
    """上传单个文件到远程服务器，必要时创建目录。"""
    unix_path = to_unix_path(remote_path)
    remote_dir = "/".join(unix_path.split("/")[:-1])
    # 创建远程目录
    run(["ssh", host, f"mkdir \"{remote_dir.replace('/', '\\')}\" 2>nul || exit 0"], check=False)
    # 上传文件
    run(["scp", str(local_path), f"{host}:{unix_path}"])


def update_csproj(host: str, remote_dir: str, file_entries: list, csproj_name: str = "SanyD365.D365Extension.Sales.csproj"):
    """
    更新远程 csproj，确保 FILE_MAP 中的文件都有 <Compile Include="..." /> 引用。
    使用 PowerShell 在远程执行，避免本地解析 XML 出错。
    """
    entries_xml = ",".join([f'"{e}"' for e in file_entries])
    ps_script = f"""
$csproj = Join-Path "{remote_dir}" "{csproj_name}"
$entries = @({entries_xml})
$xml = [xml](Get-Content $csproj -Encoding UTF8)
$ns = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
$ns.AddNamespace("ns", "http://schemas.microsoft.com/developer/msbuild/2003")

foreach ($entry in $entries) {{
    $found = $xml.Project.SelectSingleNode("//ns:Compile[@Include='$entry']", $ns)
    if (-not $found) {{
        $compile = $xml.CreateElement("Compile", "http://schemas.microsoft.com/developer/msbuild/2003")
        $compile.SetAttribute("Include", $entry)
        $itemGroup = $xml.Project.SelectSingleNode("//ns:ItemGroup[ns:Compile]", $ns)
        if (-not $itemGroup) {{
            $itemGroup = $xml.CreateElement("ItemGroup", "http://schemas.microsoft.com/developer/msbuild/2003")
            $xml.Project.AppendChild($itemGroup)
        }}
        $itemGroup.AppendChild($compile)
        Write-Host "Added: $entry"
    }} else {{
        Write-Host "Exists: $entry"
    }}
}}

$xml.Save($csproj)
Write-Host "csproj updated."
"""
    # 把 PowerShell 脚本写入临时文件并上传到远程执行
    with tempfile.NamedTemporaryFile(mode="w", suffix=".ps1", delete=False) as f:
        f.write(ps_script)
        tmp_ps1 = f.name
    try:
        remote_ps1 = f"{remote_dir}\\update_csproj_temp.ps1"
        upload_file(Path(tmp_ps1), host, remote_ps1)
        run(["ssh", host, f"powershell -ExecutionPolicy Bypass -File \"{remote_ps1}\""])
        run(["ssh", host, f"del \"{remote_ps1}\""], check=False)
    finally:
        os.unlink(tmp_ps1)


def build_remote(host: str, remote_dir: str, csproj_name: str = "SanyD365.D365Extension.Sales.csproj") -> bool:
    """触发远程单独编译。"""
    print("\n=== 远程编译验证 ===")
    result = subprocess.run(
        ["ssh", host,
         f"cd \"{remote_dir}\" && msbuild {csproj_name} /p:Configuration=Release /p:Platform=AnyCPU /verbosity:minimal"],
        capture_output=True, text=True
    )
    print(result.stdout)
    if result.stderr:
        print(result.stderr, file=sys.stderr)
    return result.returncode == 0


def sync(dry_run: bool = False, output_to_local: Path = None, only: set = None):
    """执行同步主流程。only 非空时只同步指定的本地相对路径文件。"""
    print(f"本地根目录: {LOCAL_ROOT}")
    print(f"远程主机: {REMOTE_HOST}")
    print(f"远程项目目录: {REMOTE_PROJECT_DIR}")
    print(f"远程 Custom API 项目目录: {REMOTE_PROJECT_DIR_API}\n")

    # 任务清单：(本地相对路径, 远程相对路径, 目标项目目录, csproj 文件名)
    tasks = [
        (l, r, REMOTE_PROJECT_DIR, "SanyD365.D365Extension.Sales.csproj")
        for l, r in FILE_MAP.items()
    ] + [
        (l, r, REMOTE_PROJECT_DIR_API, "SanyD365.D365ExtensionApi.Sales.csproj")
        for l, r in FILE_MAP_API.items()
    ]

    # 按项目分组收集 csproj 条目
    csproj_entries_by_project = {}

    for local_rel, remote_rel, project_dir, csproj_name in tasks:
        if only and local_rel not in only:
            continue
        local_path = LOCAL_ROOT / local_rel
        if not local_path.exists():
            print(f"⚠️ 本地文件不存在，跳过: {local_path}")
            continue

        content = local_path.read_text(encoding="utf-8")
        transformed = transform_content(content)

        # Plugin 文件自动识别并做 IPlugin → PluginBase 框架转换
        transformed = maybe_transform_plugin(transformed, local_rel, project_dir)

        if dry_run:
            print(f"[DRY-RUN] {local_rel} -> {project_dir}\\{remote_rel}")
            csproj_entries_by_project.setdefault((project_dir, csproj_name), []).append(remote_rel)
            continue

        if output_to_local:
            # 输出到本地目录（用于调试）
            out_path = output_to_local / remote_rel.replace("\\", "/")
            out_path.parent.mkdir(parents=True, exist_ok=True)
            out_path.write_text(transformed, encoding="utf-8")
            print(f"✅ {local_rel} -> {out_path}")
        else:
            # 上传到远程
            remote_path = f"{project_dir}\\{remote_rel}"
            with tempfile.NamedTemporaryFile(mode="w", suffix=".cs", delete=False) as f:
                f.write(transformed)
                tmp_cs = f.name
            try:
                upload_file(Path(tmp_cs), REMOTE_HOST, remote_path)
                print(f"✅ {local_rel} -> {remote_path}")
            finally:
                os.unlink(tmp_cs)

        csproj_entries_by_project.setdefault((project_dir, csproj_name), []).append(remote_rel)

    if dry_run:
        print("\n[DRY-RUN] csproj 待添加条目：")
        for (project_dir, csproj_name), entries in csproj_entries_by_project.items():
            print(f"  [{csproj_name}]")
            for e in entries:
                print(f"    <Compile Include=\"{e}\" />")
        return

    if output_to_local:
        print("\n已输出到本地目录，跳过 csproj 更新和远程编译。")
        return

    # 更新远程 csproj（按项目分别处理）
    print("\n=== 更新远程 csproj ===")
    for (project_dir, csproj_name), entries in csproj_entries_by_project.items():
        update_csproj(REMOTE_HOST, project_dir, entries, csproj_name)

    # 远程编译（按项目分别验证）
    for (project_dir, csproj_name) in csproj_entries_by_project.keys():
        if not build_remote(REMOTE_HOST, project_dir, csproj_name):
            print(f"\n❌ 远程编译失败: {csproj_name}，请查看上方日志。")
            sys.exit(1)

    print("\n🎉 同步并编译成功。")
    print(f"远程 DLL: {REMOTE_PROJECT_DIR}\\bin\\Release\\SanyD365.D365Extension.Sales.dll")
    print(f"远程 DLL: {REMOTE_PROJECT_DIR_API}\\bin\\Release\\SanyD365.D365ExtensionApi.Sales.dll")


def pull_dll(local_dll_path: Path):
    """把远程编译好的 DLL 拉回本地。"""
    remote_dll = f"{REMOTE_PROJECT_DIR}\\bin\\Release\\SanyD365.D365Extension.Sales.dll"
    print(f"\n=== 拉回 DLL ===")
    run(["scp", f"{REMOTE_HOST}:{to_unix_path(remote_dll)}", str(local_dll_path)])
    print(f"✅ DLL 已保存到: {local_dll_path}")


# ==================== 入口 ====================

if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="同步本地 D365 测试项目代码到远程主项目")
    parser.add_argument("--dry-run", action="store_true", help="只打印同步计划，不执行")
    parser.add_argument("--output-to-local", type=Path, help="输出转换后的文件到本地目录（用于调试）")
    parser.add_argument("--pull-dll", type=Path, help="同步并编译成功后，把 DLL 拉回指定路径")
    parser.add_argument("--only", type=str, help="只同步指定文件（本地相对路径，逗号分隔），用于绕过无关历史文件校验失败")
    args = parser.parse_args()

    only_set = set(s.strip() for s in args.only.split(",") if s.strip()) if args.only else None
    sync(dry_run=args.dry_run, output_to_local=args.output_to_local, only=only_set)

    if args.pull_dll and not args.dry_run and not args.output_to_local:
        pull_dll(args.pull_dll)
