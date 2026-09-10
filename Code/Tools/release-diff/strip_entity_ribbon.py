#!/usr/bin/env python3
# 只改导出物：从 Solution zip 的 customizations.xml 中剥除指定实体的 RibbonDiffXml 节点。
# 用途：增量包中共享实体（如 account）仅需带单字段，绝不能把 DEV1 上他人团队的 ribbon 整段带进包覆盖生产。
# 用法: python3 strip_entity_ribbon.py <包.zip> <实体名1> [实体名2 ...]
# 直接原地改写 zip（会先备份为 <包>.bak.zip）
import re, sys, zipfile, shutil, os

zip_path, entities = sys.argv[1], sys.argv[2:]
if not entities:
    print("必须指定至少一个实体名"); sys.exit(1)

bak = zip_path + ".bak.zip"
if not os.path.exists(bak):
    shutil.copy2(zip_path, bak)

with zipfile.ZipFile(bak) as z:
    entries = {i.filename: z.read(i.filename) for i in z.infolist()}

xml = entries["customizations.xml"].decode("utf-8-sig")
for ent in entities:
    # 定位 <Entity>...<entity Name="ent" 所在的大 Entity 块，剥其中的 RibbonDiffXml
    m = re.search(r'<entity Name="' + re.escape(ent) + r'"[^>]*>', xml)
    if not m:
        print(f"⚠️ 未找到实体 {ent}，跳过"); continue
    start = xml.rfind("<Entity>", 0, m.start())
    end = xml.find("</Entity>", m.start())
    block = xml[start:end]
    new_block, n = re.subn(r"\s*<RibbonDiffXml>.*?</RibbonDiffXml>", "", block, flags=re.S)
    if n:
        xml = xml[:start] + new_block + xml[end:]
        print(f"✅ 已剥除 {ent} 的 RibbonDiffXml（{len(block) - len(new_block)} 字符）")
    else:
        print(f"ℹ️ {ent} 无 RibbonDiffXml 节点")

entries["customizations.xml"] = xml.encode("utf-8")
with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
    for name, data in entries.items():
        z.writestr(name, data)
print(f"✅ 已回写 {zip_path}（备份 {bak}）")
