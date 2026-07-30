#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
为 mcs_customer_tag 和 mcs_credit_record 的 CrmTranslations.xml 填充中英双语。
规则：
- 1033 含中文：中文写入 2052，1033 改为英文（覆盖已有值）。
- 1033 为英文且 2052 为空：按字典填入 2052 中文。
"""
import re
import xml.etree.ElementTree as ET

NS = {'ss': 'urn:schemas-microsoft-com:office:spreadsheet'}
SS = '{%s}' % NS['ss']

def has_chinese(s):
    return bool(s) and bool(re.search(r'[\u4e00-\u9fff]', s))

# 中文 -> 英文
ZH_EN = {
    # Entity labels
    '客户信用标签表': 'Customer Credit Tag',
    '客户信用标签表列表': 'Customer Credit Tags',
    '客户信用标签表描述': 'Customer credit tag for credit assessment scoring.',
    '客户信用评估记录表': 'Customer Credit Assessment Record',
    '客户信用评估记录表列表': 'Customer Credit Assessment Records',
    '客户信用评估记录表描述': 'Customer credit assessment record.',

    # Fields - mcs_customer_tag
    '客户编码': 'Customer Code',
    '有效状态': 'Effective Status',
    '评分项目': 'Scoring Item',
    '信用评估': 'Credit Assessment',
    '复核定性指标': 'Review Qualitative Indicator',
    '数据类型': 'Data Type',
    '评分项目分类': 'Scoring Item Category',
    '是否评分': 'Is Scored',
    '指标编码': 'Indicator Code',
    '评分项目说明': 'Scoring Item Description',
    '集成定量指标': 'Integrated Quantitative Indicator',
    '复核定量指标': 'Review Quantitative Indicator',
    '评分项目名称': 'Scoring Item Name',
    '集成定性指标': 'Integrated Qualitative Indicator',
    '集成指标': 'Integrated Indicator',
    '复核指标': 'Review Indicator',
    '信用评估编码': 'Credit Assessment Code',
    '得分值': 'Score Value',
    '评分项目描述说明': 'Scoring Item Description',
    '评分项目简称': 'Scoring Item Short Name',
    '基于评分卡计算': 'Calculated Based on Scoring Card',
    '合并复核指标值': 'Merged Review Indicator Value',
    '合并定量定性指标值': 'Merged Quantitative and Qualitative Indicator Value',
    '是否参与评分卡计算': 'Whether to Participate in Scoring Card Calculation',
    '用于数据类型判断': 'Used for Data Type Determination',
    '人工编辑补充': 'Manual Edit Supplement',
    '内外部数据集成带入': 'Imported via Internal/External Data Integration',
    '关联到定性评分项目枚举值表': 'Associated with Qualitative Scoring Item Enum Value Table',
    '关联客户信用评估记录表的信用评估编码': 'Credit Assessment Code Associated with Customer Credit Assessment Record',

    # Fields - mcs_credit_record
    '数据集成日期': 'Data Integration Date',
    '客户': 'Customer',
    '接口返回信息': 'Interface Return Message',
    '接口名称': 'Interface Name',
    '接口返回状态': 'Interface Return Status',
    '评估发起人': 'Assessment Initiator',
    'BPP审批完成日期': 'BPP Approval Completion Date',
    '当前内部审批人': 'Current Internal Approver',
    'BPP错误信息': 'BPP Error Message',
    'BPP审批ID': 'BPP Approval ID',
    'BPP驳回原因': 'BPP Rejection Reason',
    '审批状态': 'Approval Status',
    '复核日期': 'Review Date',
    '科法斯客户代码': 'Coface Customer Code',
    '国家编码': 'Country Code',
    '客户信用评分': 'Customer Credit Score',
    '客户英文名称': 'Customer English Name',
    '发起评估日期': 'Assessment Initiation Date',
    '当前审批人': 'Current Approver',
    '逾期未回收率模型分': 'Overdue Uncollected Rate Model Score',
    '逾期未回收率模型分（0-100分），人工复核阶段录入。目前通过线下计算，后续可对接SAP模型自动获取。': 'Overdue uncollected rate model score (0-100), entered during manual review stage. Currently calculated offline, can be integrated with SAP model automatically in the future.',
    '备注说明': 'Remarks',
    'Report JSON': 'Report JSON',
    'Report订单ID': 'Report Order ID',
    'Report订单状态': 'Report Order Status',
    '信用分计算日期': 'Credit Score Calculation Date',
    '评估状态': 'Assessment Status',
    'URBA订单ID': 'URBA Order ID',
    'URBA JSON': 'URBA JSON',
    'URBA订单状态': 'URBA Order Status',
    'BPP流程实例ID': 'BPP Workflow Instance ID',
    'BPP状态': 'BPP Status',
    'BPP审批人': 'BPP Approver',
    'BPP审批信息': 'BPP Approval Information',
    'API消息': 'API Message',
    'API名称': 'API Name',
    'API状态': 'API Status',
    '申请人': 'Applicant',
    '系统登录用户姓名': 'System Login User Name',
    '从客户管理模块带入': 'Imported from Customer Management Module',
    '通过客户注册国家取代码': 'Get Code Through Customer Registration Country',
    '调用的API名称': 'API Name Called',
    '外部接口返回消息': 'External Interface Return Message',
    '外部接口返回状态': 'External Interface Return Status',
    'Report接口返回JSON': 'Report Interface Returned JSON',
    'URBA接口返回JSON': 'URBA Interface Returned JSON',
    'BPP下一节点审批人飞书账号': 'BPP Next Node Approver Feishu Account',
    '发起成功后BPP返回的流程实例ID，用于拼接审批地址': 'BPP workflow instance ID returned after initiation, used to compose approval URL',
    'BPP工作流ID': 'BPP Workflow ID',
    'BPP审批状态': 'BPP Approval Status',
    '关联客户主数据表，按PRD要求客户数据统一从客户主数据表读取': 'Associated with customer master data table; per PRD, customer data is uniformly read from customer master data table.',
    'Coface唯一企业编码': 'Coface Unique Enterprise Code',
    '科法斯URBA360产品订单ID': 'Coface URBA360 Product Order ID',
    '科法斯Publication产品订单ID': 'Coface Publication Product Order ID',
    '客户名称': 'Customer Name',
    '人工填写': 'Manual Entry',

    # Option values
    '定量': 'Quantitative',
    '定性': 'Qualitative',
    '客户实力': 'Customer Strength',
    '客户财务': 'Customer Financial',
    '宏观市场': 'Macro Market',
    '历史交易': 'Historical Transaction',
    '综合指标': 'Composite Indicator',
    '是': 'Yes',
    '否': 'No',
    '发起信用评估': 'Initiate Credit Assessment',
    '关联客户代码': 'Associate Customer Code',
    '内外部数据集成': 'Internal/External Data Integration',
    '人工复核': 'Manual Review',
    '信用分计算': 'Credit Score Calculation',
    '审核申请': 'Review Application',
    '审批通过': 'Approval Passed',
    '审批未通过': 'Approval Rejected',

    # Form/View labels - common
    'General': 'General',
    'Header': 'Header',
    'Details': 'Details',
    'Footer': 'Footer',
    'Information': 'Information',
    'ColorStrip': 'ColorStrip',
    'GENERAL': 'GENERAL',

    # Form/View labels - mcs_customer_tag
    '关联信息': 'Related Information',
    '指标值': 'Indicator Value',
    '评分与状态': 'Score and Status',
    '客户信用标签': 'Customer Credit Tags',
    'Inactive 客户信用标签表': 'Inactive Customer Credit Tags',
    '客户信用标签表 Advanced Find View': 'Customer Credit Tag Advanced Find View',
    '客户信用标签表 Associated View': 'Customer Credit Tag Associated View',
    'Quick Find Active 客户信用标签表': 'Quick Find Active Customer Credit Tags',
    '客户信用标签表 Lookup View': 'Customer Credit Tag Lookup View',
    'My 客户信用标签表': 'My Customer Credit Tags',
    'Active 客户信用标签表 owned by me': 'My Active Customer Credit Tags',

    # Form/View labels - mcs_credit_record
    '基本信息': 'Basic Information',
    '外部数据集成Coface接口返回信息': 'External Data Integration Coface Interface Return Info',
    '接口返回状态': 'Interface Return Status',
    '外部数据集成Coface订单状态': 'External Data Integration Coface Order Status',
    'Credit Tags': 'Credit Tags',
    '新建分区': 'New Section',
    'New SG control 1780908774214': 'New SG control 1780908774214',
    'Accessories': 'Accessories',
    'Attachment Upload': 'Attachment Upload',
    'Uploader': 'Uploader',
    'Approval': 'Approval',
    'BPP审批信息': 'BPP Approval Information',
    'BPP审批人': 'BPP Approver',
    'BPP流程实例ID': 'BPP Workflow Instance ID',
    '客户信用评估记录': 'Customer Credit Assessment Record',
    'Inactive 客户信用评估记录表': 'Inactive Customer Credit Assessment Records',
    '客户信用评估记录表 Advanced Find View': 'Customer Credit Assessment Record Advanced Find View',
    '客户信用评估记录表 Associated View': 'Customer Credit Assessment Record Associated View',
    'Quick Find Active 客户信用评估记录表': 'Quick Find Active Customer Credit Assessment Records',
    '客户信用评估记录表 Lookup View': 'Customer Credit Assessment Record Lookup View',
    'My 客户信用评估记录表': 'My Customer Credit Assessment Records',
    'Active 客户信用评估记录表 owned by me': 'My Active Customer Credit Assessment Records',

    # Cross-entity custom labels
    '客户信用评分项目表': 'Customer Credit Scoring Item',
    '定性评分项目枚举值': 'Qualitative Scoring Item Enum Value',
    '客户主数据': 'Customer Master Data',

    # Status/State common
    '状态': 'Status',
    '状态描述': 'Status Reason',
    'Status of the 客户信用标签表': 'Status of the Customer Credit Tag',
    'Reason for the status of the 客户信用标签表': 'Reason for the status of the Customer Credit Tag',
    'Status of the 客户信用评估记录表': 'Status of the Customer Credit Assessment Record',
    'Reason for the status of the 客户信用评估记录表': 'Reason for the status of the Customer Credit Assessment Record',
    'Active': 'Active',
    'Deactivate': 'Deactivate',
}

# 英文 -> 中文
EN_ZH = {
    'Created By': '创建者',
    'Modified By': '修改者',
    'Created On': '创建时间',
    'Modified On': '修改时间',
    'Owner': '负责人',
    'Owning User': '负责人用户',
    'Owning Team': '负责团队',
    'Owning Business Unit': '负责的业务部门',
    'Status': '状态',
    'Status Reason': '状态描述',
    'Active': '活动',
    'Deactivate': '非活动',
    'Version Number': '版本号',
    'Import Sequence Number': '导入序列号',
    'Time Zone Rule Version Number': '时区规则版本号',
    'UTC Conversion Time Zone Code': 'UTC 转换时区代码',
    'Record Created On': '记录创建时间',
    'Overridden Created On': '创建记录的时间',
    'Process Id': '流程 ID',
    '(Deprecated) Stage Id': '(已弃用) 阶段 ID',
    '(Deprecated) Traversed Path': '(已弃用) 遍历路径',
    'Name of the owner': '负责人的名称',
    'Unique identifier for the user that owns the record.': '拥有此记录的用户的唯一标识符。',
    'Unique identifier for the business unit that owns the record': '拥有此记录的部门的唯一标识符',
    'Unique identifier for the team that owns the record.': '拥有此记录的团队的唯一标识符。',
    'Unique identifier of the user who created the record.': '创建记录的用户的唯一标识符。',
    'Unique identifier of the user who modified the record.': '修改记录的用户的唯一标识符。',
    'Unique identifier of the delegate user who created the record.': '创建记录的代理用户的唯一标识符。',
    'Unique identifier of the delegate user who modified the record.': '修改记录的代理用户的唯一标识符。',
    'Date and time when the record was created.': '记录创建日期和时间。',
    'Date and time when the record was modified.': '记录修改日期和时间。',
    'Sequence number of the import that created this record.': '创建此记录的导入序列号。',
    'Time zone code that was in use when the record was created.': '创建记录时使用的时区代码。',
    'Date and time that the record was migrated.': '记录迁移日期和时间。',
    'For internal use only.': '仅供内部使用。',
    'Unique identifier for entity instances': '实体实例的唯一标识符',
    'Owner Id Type': '负责人 ID 类型',
    'Owner Id': '负责人 ID',
    'Contains the id of the process associated with the entity.': '包含与实体关联的流程 ID。',
    'Contains the id of the stage where the entity is located.': '包含实体所在阶段的 ID。',
    'A comma separated list of string values representing the unique identifiers of stages in a Business Process Flow Instance in the order that they occur.': '表示业务流程流实例中各阶段唯一标识符的逗号分隔字符串列表，按出现顺序排列。',
    'Yomi name of the owner': '负责人的 Yomi 名称',
    'Status of the Customer Credit Tag': '客户信用标签表的状态',
    'Reason for the status of the Customer Credit Tag': '客户信用标签表状态的原因',
    'Status of the Customer Credit Assessment Record': '客户信用评估记录表的状态',
    'Reason for the status of the Customer Credit Assessment Record': '客户信用评估记录表状态的原因',
    'A form for this entity.': '此实体的窗体。',
    'A card form for this entity.': '此实体的卡片窗体。',
    'General': '常规',
    'Header': '页眉',
    'Details': '详细信息',
    'Footer': '页脚',
    'Information': '信息',
    'ColorStrip': 'ColorStrip',
    'My Active Customer Credit Tags': '我拥有的活动客户信用标签表',
    'My Active Customer Credit Assessment Records': '我拥有的活动客户信用评估记录表',
    'Customer': '客户',
    'Publication ID': 'Publication ID',
    'Report JSON': 'Report JSON',
    'URBA JSON': 'URBA JSON',
    'New SG control 1780908774214': 'New SG control 1780908774214',
    'Credit Tags': '信用标签',
    'Uploader': '上传人',
    'Approval': '审批',
    'Accessories': '附件',
    'Attachment Upload': '附件上传',
}

def translate(zh):
    return ZH_EN.get(zh, None)

def translate_en(en):
    return EN_ZH.get(en, None)

def get_row_values(row, expected_cols=16):
    cells = row.findall('ss:Cell', NS)
    vals = [None] * expected_cols
    idx = 1
    for cell in cells:
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
    else:
        if target.get(SS + 'Index') is None:
            target.set(SS + 'Index', str(col_idx))

    for old in list(target.findall('ss:Data', NS)):
        target.remove(old)

    data = ET.Element(SS + 'Data')
    data.set(SS + 'Type', 'String')
    data.text = value
    target.append(data)

def main():
    tree = ET.parse('CrmTranslations.xml')
    root = tree.getroot()

    sheet = root.find('.//ss:Worksheet[@ss:Name="Localized Labels"]', NS)
    table = sheet.find('.//ss:Table', NS)
    rows = table.findall('ss:Row', NS)

    targets = {'mcs_customer_tag', 'mcs_credit_record'}
    modified = 0
    warnings = []

    for row in rows[1:]:
        vals = get_row_values(row)
        entity = vals[0]
        if entity not in targets:
            continue

        object_id = vals[1]
        col_name = vals[2] or ''
        v1033 = vals[3]
        v2052 = vals[14]

        if v1033 is None:
            continue

        if has_chinese(v1033):
            en = translate(v1033)
            if en is None:
                warnings.append(f'[{entity}] 未找到英文翻译: 1033="{v1033}" col={col_name} id={object_id}')
                set_cell_value(row, 15, v1033)
            else:
                set_cell_value(row, 4, en)
                set_cell_value(row, 15, v1033)
            modified += 1
        elif not v2052:
            zh = translate_en(v1033)
            if zh is None:
                warnings.append(f'[{entity}] 未找到中文翻译: 1033="{v1033}" col={col_name} id={object_id}')
            else:
                set_cell_value(row, 15, zh)
                modified += 1

    tree.write('CrmTranslations.xml', encoding='utf-8', xml_declaration=True)

    print(f'修改行数: {modified}')
    if warnings:
        print(f'警告数: {len(warnings)}')
        for w in warnings[:50]:
            print(w)

if __name__ == '__main__':
    main()
