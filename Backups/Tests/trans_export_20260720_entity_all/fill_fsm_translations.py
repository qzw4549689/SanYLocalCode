#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""FSM 融资管理 3 实体翻译补齐（2026-07-20），基于 extracted_v2/CrmTranslations.xml"""
import re, json
import xml.etree.ElementTree as ET

NS = {'ss': 'urn:schemas-microsoft-com:office:spreadsheet'}
SS = '{%s}' % NS['ss']
FSM = {'mcs_fsm_data', 'mcs_fsm_detail_data', 'mcs_fsm_resource'}

def has_chinese(s):
    return bool(s) and bool(re.search(r'[\u4e00-\u9fff]', s))

# 按 /tmp/fsm_need_translate.json 打印索引的翻译（zh_in_1033→英文，miss_2052→中文）
T = {
0: '融资管理',
1: 'Product name, auto-populated after selecting a lead',
2: 'Financing product description',
3: 'BPP callback status code',
4: 'Current BPP approver',
5: 'Associated customer master data',
6: 'System default: Yes',
7: '可提交方案审批',
8: 'Other conditions',
9: 'Financing amount in business currency; the currency is the financing currency',
10: 'BPP approval link',
11: 'Auto-populated after selecting an institution',
12: 'Whether the project is initiated',
13: '是否立项',
14: 'Subsidiary name, auto-populated after selecting a lead',
15: 'Fixed USD value, automatically converted from the credit amount by exchange rate',
16: 'Financing interest rate',
17: 'Associated Financing Resource',
18: 'Customer SAP code, auto-populated after selecting a lead',
19: 'Associated business unit master data, auto-populated after selecting a lead',
20: 'Financing manager, a system user',
21: 'Financing term, unit: month',
22: 'Credit amount in business currency, defaults to the financing amount',
23: '融资管理编号',
24: 'Associated country region master data, auto-populated after selecting a contract',
25: 'Number of equipment units, auto-populated after selecting a lead',
26: 'Associated region master data, auto-populated after selecting a lead',
27: 'Identifies the current BPP approval type: 1 Project Initiation Approval / 2 Financing Solution Approval',
28: '审批类型',
29: 'Project Initiation Approval',
30: 'Financing Solution Approval',
31: 'Interest subsidy amount in business currency',
32: 'Multi-select financial products, stored as comma-separated codes',
33: 'Repurchase conditions',
34: 'Associated quote, filtered by lead',
35: '可提交立项审批',
36: 'Down payment ratio, between 0 and 1, displayed as a percentage',
37: 'Associated D365 transaction currency',
38: 'Associated country master data, auto-populated after selecting a lead',
39: 'System default: No; changed to Yes after the financing solution is approved',
40: '是否有效',
41: 'Associated contract, filtered by lead/quote',
42: 'Associated lead; at least one of lead/quote/contract must be filled',
43: 'Financing stage status: 1 Financing Request / 2 Financing Initiation / 3 Financing Solution / 4 Financing Implementation',
44: '状态',
45: 'Financing Request',
46: 'Financing Initiation',
47: 'Financing Solution',
48: 'Financing Implementation',
49: 'Financing fee in business currency',
50: 'Lead',
51: 'Quote',
52: 'Region',
53: 'Country Region',
54: 'Financing Resource',
55: 'Currency',
56: 'User',
57: 'Six Financing Elements',
58: 'Financing Request Management',
59: 'Approval Information',
60: '融资资源管理',
61: 'Detailed address',
62: 'Contact email',
63: 'Institution profile',
64: 'Enabled status, true = enabled / false = disabled',
65: 'Required when the institution type is Bank; used to bring out the bank code and name',
66: 'Contact person',
67: 'Enter the specific product name when Others is selected',
68: 'Other Financial Product Name',
69: 'For banks, brought out from mcs_bank.mcs_bankno; for insurance/others, entered manually',
70: 'Contact person title',
71: 'Institution type: 1 Bank / 2 Insurance / 9 Others',
72: 'Institution Type',
73: '机构类型',
74: 'Bank',
75: 'Insurance',
76: 'Others',
77: 'Financial products multi-select option set; bank product codes 1-11, insurance product codes 101-104; the front end filters and displays by institution type',
78: 'Financial Product Name',
79: '金融产品',
80: 'Special Risk Insurance',
81: 'Project Insurance',
82: 'Investment Insurance',
83: 'Trade Insurance',
84: '资源编号',
85: 'Associated country master data',
86: 'Country',
87: 'Contact phone',
88: 'Cannot be deleted once enabled',
89: '曾经启用',
90: 'For banks, brought out from mcs_bank.mcs_name; for insurance/others, entered manually',
91: 'Associated state/province master data',
92: 'State / Province',
93: 'Associated city master data',
94: 'City',
95: 'Financial product feature description',
96: 'State / Province',
97: 'City',
98: 'Address Information',
99: '国家',
100: '州/省',
101: '城市',
102: 'Contact Information',
103: 'Product Information',
104: 'Other Information',
105: '融资落实',
106: 'Associated Financing Management main record',
107: 'Associated sales order, filtered by the contract number of the main record',
108: 'Equipment number taken from the executed orders under the contract',
109: 'Disbursement amount in business currency; same currency as the financing currency of the main record',
110: 'Time when the financing funds were received',
111: '落实编号',
112: 'Taken from the financing solution, editable',
113: 'Financing Management',
}

def get_row_values(row, expected_cols=16):
    vals = [None] * expected_cols
    idx = 1
    for cell in row.findall('ss:Cell', NS):
        si = cell.get(SS + 'Index')
        if si:
            idx = int(si)
        data = cell.find('ss:Data', NS)
        if idx <= expected_cols:
            vals[idx - 1] = data.text if data is not None else None
        idx += 1
    return vals

def set_cell_value(row, col_idx, value):
    cells = list(row.findall('ss:Cell', NS))
    current_idx = 1
    target = None
    for cell in cells:
        si = cell.get(SS + 'Index')
        if si:
            current_idx = int(si)
        if current_idx == col_idx:
            target = cell
            break
        current_idx += 1
    if target is None:
        target = ET.Element(SS + 'Cell')
        target.set(SS + 'Index', str(col_idx))
        row.append(target)
    for old in list(target.findall('ss:Data', NS)):
        target.remove(old)
    data = ET.Element(SS + 'Data')
    data.set(SS + 'Type', 'String')
    data.text = value
    target.append(data)

def main():
    uniq = json.load(open('/tmp/fsm_unmatched.json'))
    need = [u for u in uniq if 'en' not in u and 'zh' not in u]  # 待人工翻译，顺序与打印一致
    key2idx = {}
    for i, u in enumerate(need):
        key2idx[(u['type'], u['v1033'], u['v2052'])] = i
    auto = {}
    for u in uniq:
        if 'en' in u or 'zh' in u:
            auto[(u['type'], u['v1033'], u['v2052'])] = u

    tree = ET.parse('extracted_v2/CrmTranslations.xml')
    root = tree.getroot()
    sheet = root.find('.//ss:Worksheet[@ss:Name="Localized Labels"]', NS)
    table = sheet.find('.//ss:Table', NS)

    stat = {'auto': 0, 'new': 0}
    leftover, changes = [], []

    for row in table.findall('ss:Row', NS):
        vals = get_row_values(row)
        if vals[0] not in FSM:
            continue
        v1033, v2052 = vals[3], vals[14]
        col = vals[2] or ''
        if has_chinese(v1033):
            k = ('zh_in_1033', v1033, v2052)
            if k in auto:
                en, src = auto[k]['en'], '复用'
            else:
                i = key2idx.get(k)
                if i is None or i not in T:
                    leftover.append((vals[0], col, v1033)); continue
                en, src = T[i], '新翻译'
            set_cell_value(row, 4, en)
            set_cell_value(row, 15, v1033)
            changes.append((vals[0], col, v1033, en, v1033, src))
            stat['auto' if src == '复用' else 'new'] += 1
        elif v1033 and not v2052:
            k = ('miss_2052', v1033, v2052)
            if k in auto:
                zh, src = auto[k]['zh'], '复用'
            else:
                i = key2idx.get(k)
                if i is None or i not in T:
                    leftover.append((vals[0], col, v1033)); continue
                zh, src = T[i], '新翻译'
            set_cell_value(row, 15, zh)
            changes.append((vals[0], col, v1033, v1033, zh, src))
            stat['auto' if src == '复用' else 'new'] += 1

    tree.write('extracted_v2/CrmTranslations.xml', encoding='utf-8', xml_declaration=True)
    print(f"复用: {stat['auto']}  新翻译: {stat['new']}  遗留: {len(leftover)}")
    for l in leftover[:20]:
        print('  ', l)
    json.dump(changes, open('/tmp/fsm_changes.json', 'w'), ensure_ascii=False, indent=1)
    print(f"变更明细 /tmp/fsm_changes.json ({len(changes)} 条)")

if __name__ == '__main__':
    main()
