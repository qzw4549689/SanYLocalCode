#!/usr/bin/env python3
"""单文件同步：本地 FactoryCredit 计算插件 → 远程主项目（复用 sync-plugin-to-remote.py 的转换函数）。"""
import importlib.util
import sys
import tempfile
from pathlib import Path

TOOLS_DIR = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("sync_tool", TOOLS_DIR / "sync-plugin-to-remote.py")
st = importlib.util.module_from_spec(spec)
spec.loader.exec_module(st)

LOCAL_REL = "FactoryCredit/Calculation/FcaProcCalculationPlugin.cs"
REMOTE_REL = r"Plugins\FactoryCredit\FcaProcCalculationPlugin.cs"

local_path = st.LOCAL_ROOT / LOCAL_REL
content = local_path.read_text(encoding="utf-8")
transformed = st.transform_content(content)
transformed = st.maybe_transform_plugin(transformed, LOCAL_REL, st.REMOTE_PROJECT_DIR)

if len(sys.argv) > 1 and sys.argv[1] == "--out":
    # 仅输出转换结果到本地，供 diff
    Path(sys.argv[2]).write_text(transformed, encoding="utf-8")
    print(f"✅ 已输出: {sys.argv[2]}")
    sys.exit(0)

remote_path = f"{st.REMOTE_PROJECT_DIR}\\{REMOTE_REL}"
with tempfile.NamedTemporaryFile(mode="w", suffix=".cs", delete=False) as f:
    f.write(transformed)
    tmp = f.name
st.upload_file(Path(tmp), st.REMOTE_HOST, remote_path)
print(f"✅ {LOCAL_REL} -> {remote_path}")

if not st.build_remote(st.REMOTE_HOST, st.REMOTE_PROJECT_DIR, "SanyD365.D365Extension.Sales.csproj"):
    print("❌ 远程编译失败")
    sys.exit(1)
print("🎉 远程编译成功")
