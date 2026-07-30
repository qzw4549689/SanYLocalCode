#!/usr/bin/env python3
# 只读分析：对比 6/3 与 6/21 两版 Solution XML 及 DEV1 字段清单中的字段类型
import re, os, sys

BASE = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Backups/Tests/field_type_audit_20260723"
XML_0603 = os.path.join(BASE, "xml_0603/customizations.xml")
XML_0621 = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Backups/Solutions/entity_20260603_peter/customizations.xml"
DEV1_DIR = os.path.join(BASE, "dev1")

def parse_xml(path):
    """返回 {entity: {attr: type}}"""
    xml = open(path, encoding="utf-8").read()
    result = {}
    # 按 <Entity> 块切分
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

def parse_dev1(path):
    """解析 list-fields 输出: '  - mcs_xxx    String   ...'"""
    attrs = {}
    for line in open(path, encoding="utf-8"):
        m = re.match(r'\s+- (\S+)\s{2,}(\S+)\s', line)
        if m:
            attrs[m.group(1)] = m.group(2)
    return attrs

TYPE_MAP = {
    'nvarchar': 'String', 'ntext': 'Memo', 'picklist': 'Picklist', 'virtual': 'Virtual',
    'lookup': 'Lookup', 'decimal': 'Decimal', 'int': 'Integer', 'datetime': 'DateTime',
    'bit': 'Boolean', 'money': 'Money', 'bigint': 'BigInt', 'state': 'State',
    'status': 'Status', 'owner': 'Owner', 'uniqueidentifier': 'Uniqueidentifier',
    'primarykey': 'Uniqueidentifier', 'entityname': 'EntityName', 'multiselectpicklist': 'Virtual',
}

def norm(t):
    return TYPE_MAP.get(t.lower(), t)

x0603 = parse_xml(XML_0603)
x0621 = parse_xml(XML_0621)

print("=== 对比 1：6/3 XML vs 6/21 XML（类型变化 + 删除的 mcs_ 字段）===")
changes1 = []
for ent in sorted(set(x0603) & set(x0621)):
    for attr, t_old in sorted(x0603[ent].items()):
        if not attr.startswith('mcs_'): continue
        if attr in x0621[ent]:
            t_new = x0621[ent][attr]
            if norm(t_old) != norm(t_new):
                changes1.append(f"类型变化: {ent}.{attr}: {t_old} -> {t_new}")
        else:
            changes1.append(f"6/21 已删除: {ent}.{attr} (6/3 类型={t_old})")
for c in changes1: print(" ", c)
if not changes1: print("  (无变化)")

print()
print("=== 对比 2：6/21 XML vs DEV1 当前（类型变化 + 删除的 mcs_ 字段）===")
changes2 = []
for ent in sorted(set(x0621)):
    dev1_file = os.path.join(DEV1_DIR, ent + ".txt")
    if not os.path.exists(dev1_file): continue
    dev1 = parse_dev1(dev1_file)
    for attr, t_old in sorted(x0621[ent].items()):
        if not attr.startswith('mcs_'): continue
        if attr in dev1:
            t_new = dev1[attr]
            if norm(t_old) != norm(t_new):
                changes2.append(f"类型变化: {ent}.{attr}: {t_old} -> {t_new}")
        else:
            changes2.append(f"DEV1 已删除: {ent}.{attr} (6/21 类型={t_old})")
for c in changes2: print(" ", c)
if not changes2: print("  (无变化)")

print()
print("=== 6/3 XML 中的实体清单 ===")
print(" ", ", ".join(sorted(x0603.keys())))
print("=== 6/21 XML 中的实体清单 ===")
print(" ", ", ".join(sorted(x0621.keys())))
