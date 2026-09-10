#!/usr/bin/env python3
# 只读分析：发版增量 diff —— 基线 Solution zip vs DEV1 元数据 dump（_DeltaScan 输出）
# 判断「805 后新增/修改」的系统层面依据，覆盖：字段（类型/必填/格式/标签/选项/范围/长度/Lookup目标）、
# 实体显示名、窗体 formxml、视图 fetchxml/layoutxml、RibbonDiffXml。
# 用法: python3 delta_scan.py <基线包.zip> <dev1_meta.json> [--all]
#   默认只看我方自定义字段（mcs_ 前缀）；--all 含系统字段（噪声多，调试用）
import re, os, sys, json, zipfile
import xml.etree.ElementTree as ET
from html import unescape

TYPE_MAP = {
    'nvarchar': 'String', 'ntext': 'Memo', 'picklist': 'Picklist', 'virtual': 'Virtual',
    'lookup': 'Lookup', 'decimal': 'Decimal', 'int': 'Integer', 'datetime': 'DateTime',
    'bit': 'Boolean', 'money': 'Money', 'bigint': 'BigInteger', 'state': 'State',
    'status': 'Status', 'owner': 'Owner', 'uniqueidentifier': 'Uniqueidentifier', 'primarykey': 'Uniqueidentifier',
    'customer': 'Customer', 'multiselectpicklist': 'Virtual', 'partylist': 'PartyList',
    'image': 'Image', 'file': 'File', 'managedproperty': 'ManagedProperty',
}
REQ_MAP = {'none': 'None', 'required': 'BusinessRequired', 'recommended': 'Recommended',
           'applicationrequired': 'ApplicationRequired', 'systemrequired': 'SystemRequired'}

def norm_type(t):
    return TYPE_MAP.get((t or '').lower(), t)

def norm_req(r):
    if r is None: return None
    v = REQ_MAP.get(r.lower(), r)
    # 实锤：Solution 导出 XML 的 required 不区分 BusinessRequired/ApplicationRequired（有损），双侧归一类
    if v in ('BusinessRequired', 'ApplicationRequired'):
        return 'Required'
    return v

def norm_num(v):
    if v is None: return None
    try:
        from decimal import Decimal
        return str(Decimal(str(v)))
    except Exception:
        return str(v)

def norm_fmt(v):
    return v.lower() if isinstance(v, str) else v

def norm_xml(s):
    """XML 规范化：去 labels 元素（导出会补全平台语言，噪声）+ 去白文本 + 重序列化。None→''。"""
    if not s: return ''
    try:
        el = ET.fromstring(s)
        for parent in el.iter():
            for child in list(parent):
                if child.tag == 'labels':
                    parent.remove(child)
        for e in el.iter():
            if e.text and not e.text.strip(): e.text = ''
            if e.tail and not e.tail.strip(): e.tail = ''
        return ET.tostring(el, encoding='unicode')
    except Exception:
        return re.sub(r'\s+', ' ', s).strip()

def fetch_summary(s):
    """fetchxml 语义摘要：实体名 + 排序后的字段集合/条件集合/排序规则（消除平台导出噪声）。"""
    if not s: return ''
    try:
        el = ET.fromstring(s)
        ent = el.find('entity')
        if ent is None: return norm_xml(s)
        attrs = sorted(a.get('name', '') for a in ent.iter('attribute'))
        conds = sorted((c.get('attribute', ''), c.get('operator', ''), c.get('value', ''))
                       for c in ent.iter('condition'))
        orders = sorted((o.get('attribute', ''), o.get('descending', 'false')) for o in ent.iter('order'))
        links = sorted(l.get('name', '') for l in ent.findall('link-entity'))
        return repr((ent.get('name'), attrs, conds, orders, links))
    except Exception:
        return re.sub(r'\s+', ' ', s).strip()

def layout_summary(s):
    """layoutxml 语义摘要：jump + 列(name,width) 有序列表（去 object 等噪声属性）。"""
    if not s: return ''
    try:
        el = ET.fromstring(s)
        grid = el if el.tag == 'grid' else el.find('.//grid')
        if grid is None: return norm_xml(s)
        cells = [(c.get('name', ''), c.get('width', '')) for c in grid.iter('cell')]
        return repr((grid.get('name'), grid.get('jump'), cells))
    except Exception:
        return re.sub(r'\s+', ' ', s).strip()

def labels_from(elem, tag1, tag2, desc_attr='description'):
    """从 <tag1><tag2 description=.. languagecode=../></tag1> 提取 {1033:.., 2052:..}"""
    out = {}
    if elem is None: return out
    for lb in elem.findall(f'{tag1}/{tag2}'):
        lc = lb.get('languagecode')
        if lc in ('1033', '2052'):
            out[lc] = lb.get(desc_attr) or ''
    return out

# ---------------- 基线 zip 解析 ----------------
def parse_zip(zip_path):
    with zipfile.ZipFile(zip_path) as z:
        xml = z.read('customizations.xml').decode('utf-8-sig')
    result = {}
    for m in re.finditer(r'<entity Name="([^"]+)">(.*?)\n    </Entity>', xml, re.S):
        name, block = m.group(1), m.group(2)
        ent = {'attributes': {}, 'forms': {}, 'views': {}, 'ribbon': ''}
        # 实体显示名
        for desc, lc in re.findall(r'<LocalizedName description="([^"]*)" languagecode="(\d+)"', block[:6000]):
            if lc in ('1033', '2052'): ent[f'label{lc}'] = unescape(desc)
        for desc, lc in re.findall(r'<LocalizedCollectionName description="([^"]*)" languagecode="(\d+)"', block[:6000]):
            if lc in ('1033', '2052'): ent[f'collectionLabel{lc}'] = unescape(desc)
        # 字段
        for am in re.finditer(r'<attribute PhysicalName="([^"]+)">(.*?)</attribute>', block, re.S):
            aname, ab = am.group(1).lower(), am.group(2)
            def sub(tag):
                mm = re.search(rf'<{tag}>([^<]*)</{tag}>', ab)
                return mm.group(1) if mm else None
            ztype = norm_type(sub('Type'))
            zfmt = norm_fmt(sub('Format'))
            if ztype == 'Memo' and zfmt == 'textarea':
                zfmt = None
            if zfmt == 'none':
                zfmt = None
            attr = {
                'type': ztype,
                'required': norm_req(sub('RequiredLevel')),
                'maxLength': sub('MaxLength'),
                'format': zfmt,
                'min': norm_num(sub('MinValue')),
                'max': norm_num(sub('MaxValue')),
            }
            for desc, lc in re.findall(r'<displayname description="([^"]*)" languagecode="(\d+)"', ab):
                if lc in ('1033', '2052'): attr[f'label{lc}'] = unescape(desc)
            # Lookup 目标
            lt = re.search(r'<LookupTypes>(.*?)</LookupTypes>', ab, re.S)
            if lt:
                attr['targets'] = sorted(re.findall(r'<LookupType>([^<]+)</LookupType>', lt.group(1)))
            # 选项集（含布尔）
            opts = {}
            for om in re.finditer(r'<option value="(-?\d+)"[^>]*>(.*?)</option>', ab, re.S):
                oval, ob = om.group(1), om.group(2)
                ol = {lc: unescape(d) for d, lc in
                      re.findall(r'<label description="([^"]*)" languagecode="(1033|2052)"', ob)}
                opts[oval] = {'1033': ol.get('1033', ''), '2052': ol.get('2052', '')}
            if opts:
                attr['options'] = opts
            ent['attributes'][aname] = attr
        # 窗体
        for fm in re.finditer(r'<systemform>\s*<formid>\{?([0-9a-fA-F-]+)\}?</formid>(.*?)</systemform>', block, re.S):
            fid, fb = fm.group(1).lower(), fm.group(2)
            fx = re.search(r'(<form\b[^>]*>.*</form>)', fb, re.S)
            ent['forms'][fid] = {'formXml': fx.group(1) if fx else ''}
        # 视图
        for vm in re.finditer(r'<savedquery>(.*?)</savedquery>', block, re.S):
            vb = vm.group(1)
            vidm = re.search(r'<savedqueryid>\{?([0-9a-fA-F-]+)\}?</savedqueryid>', vb)
            if not vidm: continue
            vid = vidm.group(1).lower()
            def xcol(tag):
                mm = re.search(rf'<{tag}[^>]*>(.*?)</{tag}>', vb, re.S)
                return mm.group(1) if mm else ''
            ent['views'][vid] = {'fetchxml': xcol('fetchxml'), 'layoutxml': xcol('layoutxml'),
                                 'name': xcol('name')}
        # Ribbon
        rm = re.search(r'<RibbonDiffXml>(.*?)</RibbonDiffXml>', block, re.S)
        if rm:
            ent['ribbon'] = rm.group(1)
        result[name] = ent
    return result

# ---------------- DEV1 dump 加载 ----------------
def load_dev1(json_path):
    raw = json.load(open(json_path, encoding='utf-8'))
    base = os.path.dirname(os.path.abspath(json_path))
    out = {}
    for name, ent in raw['entities'].items():
        e = {'attributes': {}, 'forms': {}, 'views': {}, 'ribbon': None}
        for k in ('label1033', 'label2052', 'collectionLabel1033', 'collectionLabel2052'):
            e[k] = ent.get(k)
        for a in ent['attributes']:
            if a.get('isLogical'): continue
            aname = a['name']
            fmt = a.get('format')
            if a.get('type') == 'DateTime':
                fmt = {'DateOnly': 'date', 'DateAndTime': 'datetime'}.get(a.get('dateTimeFormat'), a.get('dateTimeFormat'))
            if a.get('type') == 'Memo':
                fmt = None
            attr = {
                'type': a.get('type'),
                'required': norm_req(a.get('required')),
                'maxLength': str(a['maxLength']) if a.get('maxLength') is not None else None,
                'format': norm_fmt(fmt),
                'min': norm_num(a.get('min')),
                'max': norm_num(a.get('max')),
                'label1033': a.get('label1033'),
                'label2052': a.get('label2052'),
            }
            if a.get('targets'):
                attr['targets'] = sorted(a['targets'])
            if a.get('options'):
                attr['options'] = {str(o['value']): {'1033': o.get('label1033') or '', '2052': o.get('label2052') or ''}
                                   for o in a['options']}
            e['attributes'][aname] = attr
        for f in ent.get('forms', []):
            fx = ''
            if f.get('formXmlFile'):
                fx = open(os.path.join(base, f['formXmlFile']), encoding='utf-8').read()
            e['forms'][f['id'].lower()] = {'name': f.get('name'), 'type': f.get('type'), 'formXml': fx}
        for v in ent.get('views', []):
            d = {'name': v.get('name'), 'modifiedonUtc': v.get('modifiedonUtc')}
            for key, tag in (('fetchXmlFile', 'fetchxml'), ('layoutXmlFile', 'layoutxml')):
                d[tag] = open(os.path.join(base, v[key]), encoding='utf-8').read() if v.get(key) else ''
            e['views'][v['id'].lower()] = d
        out[name] = e
    return out, raw

# ---------------- diff ----------------
PROP_LABEL = {'type': '类型', 'required': '必填性', 'label1033': '英文标签', 'label2052': '中文标签',
              'format': '格式', 'maxLength': '最大长度', 'min': '最小值', 'max': '最大值'}

def diff_attrs(old, new, prefix_filter):
    added, removed, changed = [], [], []
    for a in sorted(set(new) - set(old)):
        if prefix_filter(a): added.append(a)
    for a in sorted(set(old) - set(new)):
        if prefix_filter(a): removed.append(a)
    for a in sorted(set(old) & set(new)):
        if not prefix_filter(a): continue
        o, n = old[a], new[a]
        diffs = []
        for p, label in PROP_LABEL.items():
            if p == 'format' and o.get('type') == 'Memo':
                continue
            ov, nv = o.get(p), n.get(p)
            if ov is None and nv is None: continue
            if ov != nv:
                diffs.append(f"{label}: {ov!r} → {nv!r}")
        # 选项集
        oo, no = o.get('options') or {}, n.get('options') or {}
        if oo != no:
            det = []
            for v in sorted(set(no) - set(oo), key=int): det.append(f"新增选项 {v}={no[v]}")
            for v in sorted(set(oo) - set(no), key=int): det.append(f"删除选项 {v}={oo[v]}")
            for v in sorted(set(oo) & set(no), key=int):
                if oo[v] != no[v]: det.append(f"选项 {v} 标签: {oo[v]} → {no[v]}")
            if det: diffs.append('选项集: ' + '；'.join(det))
        if diffs: changed.append((a, diffs))
    return added, removed, changed

def main():
    old_zip, new_json = sys.argv[1], sys.argv[2]
    show_all = '--all' in sys.argv
    prefix = (lambda a: True) if show_all else (lambda a: a.startswith('mcs_'))
    if old_zip.lower().endswith('.json'):
        old, _ = load_dev1(old_zip)
        base_desc = os.path.basename(old_zip)
    else:
        old = parse_zip(old_zip)
        base_desc = os.path.basename(old_zip)
    new, raw_new = load_dev1(new_json)
    print(f"基线: {base_desc}（{len(old)} 实体）  vs  目标 dump（{len(new)} 实体）")

    common = [e for e in sorted(new) if e in old]
    only_new = [e for e in sorted(new) if e not in old]
    print(f"基线覆盖实体: {len(common)}；基线未覆盖（无法本地裁决）: {only_new}")

    for ent in common:
        o, n = old[ent], new[ent]
        lines = []
        # 实体标签
        for k, lbl in (('label1033', '实体英文名'), ('label2052', '实体中文名'),
                       ('collectionLabel1033', '集合英文名'), ('collectionLabel2052', '集合中文名')):
            if o.get(k) is not None and n.get(k) is not None and o[k] != n[k]:
                lines.append(f"  ✏️ {lbl}: {o[k]!r} → {n[k]!r}")
        added, removed, changed = diff_attrs(o['attributes'], n['attributes'], prefix)
        for a in added:
            t = n['attributes'][a]
            lines.append(f"  ➕ 新字段 {a}（{t.get('type')}，必填={t.get('required')}，标签={t.get('label2052') or t.get('label1033')}）")
        for a in removed:
            lines.append(f"  ➖ 已删字段 {a}")
        for a, diffs in changed:
            for d in diffs:
                mark = '🚨' if d.startswith('类型') else '✏️'
                lines.append(f"  {mark} 字段 {a} — {d}")
        # 窗体
        for fid in sorted(set(n['forms']) - set(o['forms'])):
            nm = n['forms'][fid].get('name')
            if nm and nm.startswith('App for Outlook'): continue
            lines.append(f"  ➕ 新窗体 {nm}（{fid[:8]}）")
        for fid in sorted(set(o['forms']) - set(n['forms'])):
            lines.append(f"  ➖ 已删窗体 {fid[:8]}")
        for fid in sorted(set(o['forms']) & set(n['forms'])):
            if norm_xml(o['forms'][fid].get('formXml')) != norm_xml(n['forms'][fid].get('formXml')):
                lines.append(f"  ✏️ 窗体变更 {n['forms'][fid].get('name')}（{fid[:8]}）")
        # 视图
        for vid in sorted(set(n['views']) - set(o['views'])):
            lines.append(f"  ➕ 新视图 {n['views'][vid].get('name')}（{vid[:8]}）")
        for vid in sorted(set(o['views']) - set(n['views'])):
            lines.append(f"  ➖ 已删视图 {o['views'][vid].get('name')}（{vid[:8]}）")
        for vid in sorted(set(o['views']) & set(n['views'])):
            dv = []
            if fetch_summary(o['views'][vid].get('fetchxml')) != fetch_summary(n['views'][vid].get('fetchxml')): dv.append('fetchxml')
            if layout_summary(o['views'][vid].get('layoutxml')) != layout_summary(n['views'][vid].get('layoutxml')): dv.append('layoutxml')
            if dv:
                lines.append(f"  ✏️ 视图变更 {n['views'][vid].get('name')}（{vid[:8]}，{','.join(dv)}）")
        # Ribbon：基线有非空 CustomActions 才提示（DEV1 ribbon 内容见 ribboncustomization publishedon 分析）
        if norm_xml(o.get('ribbon')) and '<CustomAction' in o.get('ribbon', ''):
            lines.append("  ℹ️ 基线包含非空 RibbonDiffXml（ribbon 增量以 publishedon 扫描为准）")
        if lines:
            print(f"\n【{ent}】")
            print('\n'.join(lines))

if __name__ == '__main__':
    main()
