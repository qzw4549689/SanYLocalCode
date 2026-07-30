#!/usr/bin/env python3
# 只读分析：Solution 包(zip) vs DEV1 list-fields 清单目录 的字段差异对比
# 用法: python3 diff_package_vs_dev1.py <旧包.zip> <dev1清单目录>
import re, os, sys, zipfile

TYPE_MAP = {
    'nvarchar': 'String', 'ntext': 'Memo', 'picklist': 'Picklist', 'virtual': 'Virtual',
    'lookup': 'Lookup', 'decimal': 'Decimal', 'int': 'Integer', 'datetime': 'DateTime',
    'bit': 'Boolean', 'money': 'Money', 'bigint': 'BigInt', 'state': 'State',
    'status': 'Status', 'owner': 'Owner', 'uniqueidentifier': 'Uniqueidentifier',
    'primarykey': 'Uniqueidentifier', 'entityname': 'EntityName', 'multiselectpicklist': 'Virtual',
}
def norm(t): return TYPE_MAP.get(t.lower(), t)

def parse_package(zip_path):
    with zipfile.ZipFile(zip_path) as z:
        xml = z.read('customizations.xml').decode('utf-8-sig')
    result = {}
    for m in re.finditer(r'<Entity>(.*?)</Entity>', xml, re.S):
        block = m.group(1)
        en = re.search(r'<entity Name="([^"]+)"', block)
        if not en: continue
        attrs = {}
        for am in re.finditer(r'<attribute[^>]*>(.*?)</attribute>', block, re.S):
            ab = am.group(1)
            nm = re.search(r'<Name>([^<]+)</Name>', ab)
            tp = re.search(r'<Type>([^<]+)</Type>', ab)
            if nm and tp: attrs[nm.group(1)] = tp.group(1)
        result[en.group(1)] = attrs
    return result

def parse_dev1_dir(dirpath):
    result = {}
    for f in os.listdir(dirpath):
        if not f.endswith('.txt'): continue
        attrs = {}
        for line in open(os.path.join(dirpath, f), encoding='utf-8'):
            m = re.match(r'\s+- (\S+)\s+(\S+)\s', line)
            if m: attrs[m.group(1)] = m.group(2)
        result[f[:-4]] = attrs
    return result

old_zip, dev1_dir = sys.argv[1], sys.argv[2]
old = parse_package(old_zip)
new = parse_dev1_dir(dev1_dir)
print(f"旧包: {os.path.basename(old_zip)}（{len(old)} 实体）  新: DEV1当前清单（{len(new)} 实体）")

print("\n=== 🚨 同名字段类型不一致（提前预警导入失败）===")
found = False
for ent in sorted(set(old) & set(new)):
    for attr in sorted(set(old[ent]) & set(new[ent])):
        if not attr.startswith('mcs_'): continue
        t_old, t_new = norm(old[ent][attr]), norm(new[ent][attr])
        if t_old != t_new:
            print(f"  {ent}.{attr}: {t_old} -> {t_new}")
            found = True
if not found: print("  (无)")

print("\n=== ➖ 旧包有、DEV1 已删除的字段 ===")
found = False
for ent in sorted(set(old) & set(new)):
    for attr in sorted(set(old[ent]) - set(new[ent])):
        if not attr.startswith('mcs_'): continue
        print(f"  {ent}.{attr} (旧类型={norm(old[ent][attr])})")
        found = True
if not found: print("  (无)")

print("\n=== ➕ DEV1 新增字段/实体（本次新发布，不冲突）===")
for ent in sorted(set(old) & set(new)):
    adds = sorted(a for a in set(new[ent]) - set(old[ent]) if a.startswith('mcs_'))
    if adds:
        print(f"  {ent}: {', '.join(adds)}")
for ent in sorted(set(new) - set(old)):
    print(f"  [新实体] {ent}")
