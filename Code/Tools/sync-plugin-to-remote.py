#!/usr/bin/env python3
"""
D365 本地测试项目 → 远程主项目同步脚本

用法：
    cd Code/Tools
    python3 sync-plugin-to-remote.py

功能：
    1. 按 FILE_MAP 读取本地源文件
    2. 按 NAMESPACE_MAP 替换命名空间前缀
    3. 通过 scp 写入远程服务器目标路径
    4. 更新远程 SanyD365.D365Extension.Sales.csproj 的 Compile 引用
    5. 触发远程单独编译验证

配置：
    修改本文件顶部的 REMOTE_HOST / REMOTE_BASE_DIR / FILE_MAP 即可复用。
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
    "CofaceIntegration/Api/CofaceApiService.cs": r"Application\Sales\CofaceIntegration\CofaceApiService.cs",
    "CofaceIntegration/Parser/FullReportParser.cs": r"Application\Sales\CofaceIntegration\FullReportParser.cs",
    "CofaceIntegration/Parser/Urba360Parser.cs": r"Application\Sales\CofaceIntegration\Urba360Parser.cs",
    "CofaceIntegration/Token/CofaceTokenManager.cs": r"Application\Sales\CofaceIntegration\CofaceTokenManager.cs",

    # Coface Plugin 层
    "CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs": r"Plugins\CofaceIntegration\CofaceIntegrationDataSyncPlugin.cs",
    "CofaceIntegration/Plugin/CofaceSearchCompanyPlugin.cs": r"Plugins\CofaceIntegration\CofaceSearchCompanyPlugin.cs",

    # BPP Callback Plugin
    "BppIntegration/Plugin/BppCallbackPlugin.cs": r"Plugins\CreditRecord\CreditRecordBppCallbackPlugin.cs",

    # Account 字段校验 Plugin（8 字段校验已移除，仅保留 blacklist/creditgrant）
    "Account/AutoNumber/AccountValidationPlugin.cs": r"Plugins\Account\AccountCreditValidationPlugin.cs",

    # 客户主数据字段校验 Plugin（新增）
    "CustomerMasterData/Validation/CustomerMasterDataValidationPlugin.cs": r"Plugins\Account\CustomerMasterDataCreditValidationPlugin.cs",
}

# csproj 中 Compile 引用的排序分组（可选，保持 csproj 可读性）
# 键：远程相对路径前缀；值：该组在 csproj 中的插入位置描述
# 实际脚本使用简单追加 + 去重

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

    return content


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


def update_csproj(host: str, remote_dir: str, file_entries: list):
    """
    更新远程 csproj，确保 FILE_MAP 中的文件都有 <Compile Include="..." /> 引用。
    使用 PowerShell 在远程执行，避免本地解析 XML 出错。
    """
    entries_xml = ",".join([f'"{e}"' for e in file_entries])
    ps_script = f"""
$csproj = Join-Path "{remote_dir}" "SanyD365.D365Extension.Sales.csproj"
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


def build_remote(host: str, remote_dir: str) -> bool:
    """触发远程单独编译。"""
    print("\n=== 远程编译验证 ===")
    result = subprocess.run(
        ["ssh", host,
         f"cd /d \"{remote_dir}\" && msbuild SanyD365.D365Extension.Sales.csproj /p:Configuration=Release /p:Platform=AnyCPU /verbosity:minimal"],
        capture_output=True, text=True
    )
    print(result.stdout)
    if result.stderr:
        print(result.stderr, file=sys.stderr)
    return result.returncode == 0


def sync(dry_run: bool = False, output_to_local: Path = None):
    """执行同步主流程。"""
    print(f"本地根目录: {LOCAL_ROOT}")
    print(f"远程主机: {REMOTE_HOST}")
    print(f"远程项目目录: {REMOTE_PROJECT_DIR}\n")

    csproj_entries = []

    for local_rel, remote_rel in FILE_MAP.items():
        local_path = LOCAL_ROOT / local_rel
        if not local_path.exists():
            print(f"⚠️ 本地文件不存在，跳过: {local_path}")
            continue

        content = local_path.read_text(encoding="utf-8")
        transformed = transform_content(content)

        # Plugin 文件需要额外做 IPlugin → PluginBase 框架转换
        if local_rel == "BppIntegration/Plugin/BppCallbackPlugin.cs":
            transformed = transform_iplugin_to_pluginbase(transformed, "CreditRecordBppCallbackPlugin")
        elif local_rel == "CofaceIntegration/Plugin/CofaceDataSyncPlugin.cs":
            transformed = transform_iplugin_to_pluginbase(transformed, "CofaceDataSyncPlugin", "CofaceIntegrationDataSyncPlugin")
        elif local_rel == "CofaceIntegration/Plugin/CofaceSearchCompanyPlugin.cs":
            transformed = transform_iplugin_to_pluginbase(transformed, "CofaceSearchCompanyPlugin")
        elif local_rel == "Account/AutoNumber/AccountValidationPlugin.cs":
            transformed = transform_iplugin_to_pluginbase(transformed, "AccountValidationPlugin", "AccountCreditValidationPlugin")
        elif local_rel == "CustomerMasterData/Validation/CustomerMasterDataValidationPlugin.cs":
            transformed = transform_iplugin_to_pluginbase(transformed, "CustomerMasterDataValidationPlugin", "CustomerMasterDataCreditValidationPlugin")

        if dry_run:
            print(f"[DRY-RUN] {local_rel} -> {remote_rel}")
            csproj_entries.append(remote_rel)
            continue

        if output_to_local:
            # 输出到本地目录（用于调试）
            out_path = output_to_local / remote_rel.replace("\\", "/")
            out_path.parent.mkdir(parents=True, exist_ok=True)
            out_path.write_text(transformed, encoding="utf-8")
            print(f"✅ {local_rel} -> {out_path}")
        else:
            # 上传到远程
            remote_path = f"{REMOTE_PROJECT_DIR}\\{remote_rel}"
            with tempfile.NamedTemporaryFile(mode="w", suffix=".cs", delete=False) as f:
                f.write(transformed)
                tmp_cs = f.name
            try:
                upload_file(Path(tmp_cs), REMOTE_HOST, remote_path)
                print(f"✅ {local_rel} -> {remote_path}")
            finally:
                os.unlink(tmp_cs)

        csproj_entries.append(remote_rel)

    if dry_run:
        print("\n[DRY-RUN] csproj 待添加条目：")
        for e in csproj_entries:
            print(f"  <Compile Include=\"{e}\" />")
        return

    if output_to_local:
        print("\n已输出到本地目录，跳过 csproj 更新和远程编译。")
        return

    # 更新远程 csproj
    print("\n=== 更新远程 csproj ===")
    update_csproj(REMOTE_HOST, REMOTE_PROJECT_DIR, csproj_entries)

    # 远程编译
    if not build_remote(REMOTE_HOST, REMOTE_PROJECT_DIR):
        print("\n❌ 远程编译失败，请查看上方日志。")
        sys.exit(1)

    print("\n🎉 同步并编译成功。")
    print(f"远程 DLL: {REMOTE_PROJECT_DIR}\\bin\\Release\\SanyD365.D365Extension.Sales.dll")


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
    args = parser.parse_args()

    sync(dry_run=args.dry_run, output_to_local=args.output_to_local)

    if args.pull_dll and not args.dry_run and not args.output_to_local:
        pull_dll(args.pull_dll)
