#!/bin/bash
set -e
cd /Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Tools/MetadataTool

# mcs_fca_quota
dotnet run --no-build update-field-description mcs_fca_quota mcs_quotano "系统自动生成的额度编码，作为额度表主键。"
dotnet run --no-build update-field-description mcs_fca_quota mcs_accountid "Lookup搜索，从客户管理模块/客户主数据带入，关联客户主数据表的客户编码。"
dotnet run --no-build update-field-description mcs_fca_quota mcs_custname "从客户管理模块/客户主数据带入，关联客户主数据表的客户名称。"
dotnet run --no-build update-field-description mcs_fca_quota mcs_sellergrant "正式生效额度。场景一：第一次系统计算和调整获得初始系统授信额度，计算生效后用厂端授信模型计算表的调整模型额度USD更新；场景二：人工调整审批后获得正式额度。"
dotnet run --no-build update-field-description mcs_fca_quota mcs_sellerbalance "当前可用余额。计算生效后用调整模型额度USD初始化；后续在出运发货、收款等环节被扣减和释放。"
dotnet run --no-build update-field-description mcs_fca_quota mcs_doid "关联厂端授信模型计算表的模型计算序列号。"
dotnet run --no-build update-field-description mcs_fca_quota mcs_isactive "人工单选，默认新增时就生效。1是0否。"
dotnet run --no-build update-field-description mcs_fca_quota mcs_validfrom "系统自动更新，一般在授信额度审批后连同额度和余额更新，默认系统当日。"

# mcs_fca_records
dotnet run --no-build update-field-description mcs_fca_records mcs_recordid "CRM系统生成唯一码，前缀FCR+YYYYMMDD+5位序列号。FCR=Factory-side credit record。"
dotnet run --no-build update-field-description mcs_fca_records mcs_accountid "人工或接口传入。Lookup搜索，从客户管理模块/客户主数据带入，关联客户主数据表的客户编码。"
dotnet run --no-build update-field-description mcs_fca_records mcs_custname "从客户管理模块/客户主数据带入，关联客户主数据表的客户名称。"
dotnet run --no-build update-field-description mcs_fca_records mcs_contractid "人工或接口传入。Lookup搜索合同。流程环节>=3时必选合同，然后才能选订单。"
dotnet run --no-build update-field-description mcs_fca_records mcs_orderid "人工或接口传入。Lookup搜索合同下的执行订单（先由合同编码过滤）。订单发货及后续环节必选。"
dotnet run --no-build update-field-description mcs_fca_records mcs_proccess "流程环节。1厂端授信模型计算/2厂端授信限额额度申请/3厂端授信限额归零调整/4交合同评审/5合同变更信用类别/6合同取消/7订单执行验收/8订单变更信用类别/9回款解款/10订单退货/11订单取消/12其他。"
dotnet run --no-build update-field-description mcs_fca_records mcs_adjust "额度调整动作。1初始化（限厂端授信模型计算和额度申请模块使用）/2预占/3占用/4释放。"
dotnet run --no-build update-field-description mcs_fca_records mcs_sellergrant "授信限额USD。通过客户编码搜索厂端授信额度表获得。"
dotnet run --no-build update-field-description mcs_fca_records mcs_asisbalance "现有授信余额USD。通过客户编码搜索厂端授信额度表获得。"
dotnet run --no-build update-field-description mcs_fca_records mcs_adjustamt "调整金额USD，必须>=0。厂端授信模型计算初始化额度台账时调整金额=0。"
dotnet run --no-build update-field-description mcs_fca_records mcs_tobebalance "调整后授信余额USD = 现有授信余额USD - 调整金额USD，允许出现负数表示超额。"
dotnet run --no-build update-field-description mcs_fca_records modifiedby "记录修改人。"
dotnet run --no-build update-field-description mcs_fca_records modifiedon "记录修改时间。"
