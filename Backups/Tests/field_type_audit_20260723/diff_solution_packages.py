#!/usr/bin/env python3
# 只读分析：对比两个 Solution 包（zip）中的实体字段差异
# 用法: python3 diff_solution_packages.py <旧包.zip> <新包.zip> [--all]
# 默认只对比 mcs_ 自定义字段；--all 显示全部字段
import re, os, sys, zipfile, tempfile

TYPE_MAP = {
    'nvarchar': 'String', 'ntext': 'Memo', 'picklist': 'Picklist', 'virtual': 'Virtual(多选/计算)',
    'lookup': 'Lookup', 'decimal': 'Decimal', 'int': 'Integer', 'datetime': 'DateTime',
    'bit': 'Boolean', 'money': 'Money', 'bigint': 'BigInt', 'state': 'State',
    'status': 'Status', 'owner': 'Owner', 'uniqueidentifier': 'Uniqueidentifier',
    'primarykey': 'Uniqueidentifier', 'entityname': 'EntityName', 'multiselectpicklist': 'Virtual',
}
def norm(t): return TYPE_MAP.get(t.lower(), t)

def parse_package(zip_path):
    with zipfile.ZipFile(zip_path) as z:
        with z.open('customizations.xml') as f:
            xml = f.read().decode('utf-8-sig')
    result = {}
    for m in re.finditer(r'<Entity>(.*?)</Entity>', xml, re.S):
        block = m.group(1)
        en = re.search(r'<entity Name="([^"]+)"', block)
        if not en:
            continue
        entity = en.group(1)
        attrs = {}
        for am in re.finditer(r'<attribute[^>]*>(.*?)</attribute>', block, re.S):
            ab = am.group(1)
            nm = re.search(r'<Name>([^<]+)</Name>', ab)
            tp = re.search(r'<Type>([^<]+)</Type>', ab)
            if nm and tp:
                attrs[nm.group(1)] = tp.group(1)
        result[entity] = attrs
    return result

def main():
    old_zip, new_zip = sys.argv[1], sys.argv[2]
    show_all = '--all' in sys.argv
    old = parse_package(old_zip)
    new = parse_package(new_zip)
    print(f"旧包: {os.path.basename(old_zip)}（{len(old)} 个实体）")
    print(f"新包: {os.path.basename(new_zip)}（{len(new)} 个实体）")

    print("\n=== 🚨 同名字段类型不一致（会导致导入失败 80041A06）===")
    found = False
    for ent in sorted(set(old) & set(new)):
        for attr in sorted(set(old[ent]) & set(new[ent])):
            if not show_all and not attr.startswith('mcs_'): continue
            t_old, t_new = norm(old[ent][attr]), norm(new[ent][attr])
            if t_old != t_new:
                print(f"  {ent}.{attr}: {t_old} -> {t_new}")
                found = True
    if not found: print("  (无)")

    print("\n=== ➖ 仅旧包有的字段（新包已移除，update 模式导入不报错但建议清理）===")
    found = False
    for ent in sorted(set(old) & set(new)):
        for attr in sorted(set(old[ent]) - set(new[ent])):
            if not show_all and not attr.startswith('mcs_'): continue
            print(f"  {ent}.{attr} (旧包类型={norm(old[ent][attr])})")
            found = True
    if not found: print("  (无)")

    print("\n=== ➕ 仅新包有的字段（本次新增发布，不冲突）===")
    found = False
    for ent in sorted(set(old) & set(new)):
        for attr in sorted(set(new[ent]) - set(old[ent])):
            if not show_all and not attr.startswith('mcs_'): continue
            print(f"  {ent}.{attr} (类型={norm(new[ent][attr])})")
            found = True
    for ent in sorted(set(new) - set(old)):
        n_mcs = sum(1 for a in new[ent] if a.startswith('mcs_'))
        print(f"  [新实体] {ent}（{n_mcs} 个自定义字段）")
        found = True
    if not found: print("  (无)")

    print("\n=== 仅旧包有的实体（本次不再发布）===")
    for ent in sorted(set(old) - set(new)):
        print(f"  {ent}")

if __name__ == '__main__':
    main()
