#!/usr/bin/env python3
# 只读分析：6/8 pac 解包快照 vs 6/21 XML vs DEV1 当前，字段类型/删除对比
import re, os

BASE = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Backups/Tests/field_type_audit_20260723"
XML_0621 = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Backups/Solutions/entity_20260603_peter/customizations.xml"
PAC_0608 = "/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/SanyD365Project/solutions/unpacked_latest/Entities"
DEV1_DIR = os.path.join(BASE, "dev1")

def parse_entity_block(block):
    attrs = {}
    for am in re.finditer(r'<attribute[^>]*>(.*?)</attribute>', block, re.S):
        ab = am.group(1)
        nm = re.search(r'<Name>([^<]+)</Name>', ab)
        tp = re.search(r'<Type>([^<]+)</Type>', ab)
        if nm and tp:
            attrs[nm.group(1)] = tp.group(1)
    return attrs

def parse_xml(path):
    xml = open(path, encoding="utf-8").read()
    result = {}
    for m in re.finditer(r'<Entity>(.*?)</Entity>', xml, re.S):
        block = m.group(1)
        en = re.search(r'<entity Name="([^"]+)"', block)
        if en:
            result[en.group(1)] = parse_entity_block(block)
    return result

def parse_pac(dirpath):
    result = {}
    for ent in os.listdir(dirpath):
        f = os.path.join(dirpath, ent, "Entity.xml")
        if os.path.isfile(f):
            xml = open(f, encoding="utf-8").read()
            en = re.search(r'<entity Name="([^"]+)"', xml)
            if en:
                result[en.group(1)] = parse_entity_block(xml)
    return result

def parse_dev1(path):
    attrs = {}
    for line in open(path, encoding="utf-8"):
        m = re.match(r'\s+- (\S+)\s+(\S+)\s', line)
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
def norm(t): return TYPE_MAP.get(t.lower(), t)

def diff(old, new, label_old, label_new):
    out = []
    for ent in sorted(set(old) & set(new)):
        for attr, t_old in sorted(old[ent].items()):
            if not attr.startswith('mcs_'): continue
            if attr in new[ent]:
                t_new = new[ent][attr]
                if norm(t_old) != norm(t_new):
                    out.append(f"类型变化: {ent}.{attr}: {t_old}({label_old}) -> {t_new}({label_new})")
            else:
                out.append(f"{label_new}已删除: {ent}.{attr} ({label_old}类型={t_old})")
    return out

x0621 = parse_xml(XML_0621)
p0608 = parse_pac(PAC_0608)
dev1 = {}
for f in os.listdir(DEV1_DIR):
    if f.endswith(".txt"):
        dev1[f[:-4]] = parse_dev1(os.path.join(DEV1_DIR, f))

print("=== 6/8 pac 快照实体:", ", ".join(sorted(p0608.keys())))
print()
print("=== 对比 A：6/8 vs 6/21 ===")
r = diff(p0608, x0621, "6/8", "6/21")
print("\n".join("  "+x for x in r) if r else "  (无变化)")
print()
print("=== 对比 B：6/8 vs DEV1 当前 ===")
r = diff(p0608, dev1, "6/8", "DEV1")
print("\n".join("  "+x for x in r) if r else "  (无变化)")
print()
print("=== 对比 C：6/21 vs DEV1 当前（修正解析后重跑） ===")
r = diff(x0621, dev1, "6/21", "DEV1")
print("\n".join("  "+x for x in r) if r else "  (无变化)")
