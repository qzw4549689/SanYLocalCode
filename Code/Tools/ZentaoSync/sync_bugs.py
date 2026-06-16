"""
将项目Bug同步到禅道
"""
import sys
import os
sys.path.insert(0, os.path.dirname(__file__))

from zentao_client import ZentaoClient

# Bug数据（从 测试Bug反馈记录.md 提取）
BUGS = [
    {
        "id": "Bug-1",
        "title": "客户信用评估记录表 Script Error — mcs_credit_record.js 方法不存在",
        "severity": "1",  # 严重
        "pri": "1",
        "steps": """1. 导航到客户信用评估记录表
2. 点击新建按钮
3. 弹出 Script Error：Web resource method does not exist: ScoringCardForm.onLoad

根因：复制评分卡窗体配置时，忘记修改绑定的JS方法名。""",
        "status": "已关闭"
    },
    {
        "id": "Bug-2",
        "title": "评分卡配置不完整 — SA级老客户只有1条配置",
        "severity": "2",  # 高
        "pri": "2",
        "steps": """1. 查询SA级老客户评分卡配置
2. 发现只有1条（外部评级，1分）
3. 总分不足100分，导致信用评分计算异常

修复：补充7条评分卡配置，总分=100。""",
        "status": "已关闭"
    },
    {
        "id": "Bug-3",
        "title": "批量处理命令缺失 — import/sync-all/report 命令不存在",
        "severity": "3",  # 中
        "pri": "3",
        "steps": """1. 执行批量导入测试
2. 命令行工具不支持 import、sync-all、report 命令
3. 该功能在开发计划中列为长期计划

结论：非Bug，待开发功能。""",
        "status": "待开发"
    },
    {
        "id": "Bug-4",
        "title": "选择客户后查询客户信息失败",
        "severity": "1",
        "pri": "1",
        "steps": """1. 在评估记录表选择客户编码
2. 弹出错误：查询客户信息失败，请重试
3. Account实体缺少信用评估所需的自定义字段

修复：在Account实体新增8个字段，修正JS查询逻辑。""",
        "status": "已关闭"
    },
    {
        "id": "Bug-5",
        "title": "信用分计算Plugin失败 — 缺少mcs_categoryid字段",
        "severity": "1",
        "pri": "1",
        "steps": """1. 修改评估状态为'信用分计算'并保存
2. 弹出Business Process Error
3. Plugin错误地从评估记录表查询mcs_categoryid字段

修复：实时从Account表查询客户属性匹配评分卡类型。""",
        "status": "已关闭"
    },
    {
        "id": "Bug-6",
        "title": "状态值无效：Invalid value 1 for OptionSetValue",
        "severity": "1",
        "pri": "1",
        "steps": """1. 新建客户信用评估记录表
2. 弹出Script Error：Invalid value 1 for OptionSetValue
3. JS代码将mcs_status设为1，但选项集实际值为9-16

修复：JS初始值改回9，Plugin触发状态改回13/14。""",
        "status": "已关闭"
    },
    {
        "id": "Bug-7",
        "title": "评估状态11保存后自动变为12",
        "severity": "2",
        "pri": "2",
        "steps": """1. 手动选择评估状态为"11-数据集成"并保存
2. 下拉框值自动变为"12-人工复核"
3. CofaceDataSyncPlugin自动修改状态

修复：移除Plugin中自动修改mcs_status的代码。""",
        "status": "已关闭"
    },
    {
        "id": "Bug-8",
        "title": "信用分计算提示不友好 — 系统错误样式显示业务提示",
        "severity": "3",
        "pri": "3",
        "steps": """1. 未配置评分卡时触发信用分计算
2. 弹出"Business Process Error"弹窗
3. 提示看起来像系统报错，用户无法理解

修复：将异常消息改为中文友好提示。""",
        "status": "已关闭"
    }
]


def sync_bugs_to_zentao(base_url, account, password, product_id):
    """同步Bug到禅道"""
    client = ZentaoClient(base_url, account, password)
    client.login()
    
    print(f"\n开始同步 {len(BUGS)} 个Bug到禅道产品 {product_id}...\n")
    
    for bug in BUGS:
        bug_data = {
            "title": f"[{bug['id']}] {bug['title']}",
            "openedBuild": "trunk",
            "severity": bug["severity"],
            "pri": bug["pri"],
            "type": "bug",
            "steps": bug["steps"]
        }
        
        try:
            result = client.create_bug(product_id, bug_data)
            print(f"✅ {bug['id']} 创建成功: {result.get('id', 'unknown')}")
        except Exception as e:
            print(f"❌ {bug['id']} 创建失败: {e}")
    
    print("\nBug同步完成！")


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description="同步Bug到禅道")
    parser.add_argument("--url", default="https://peterqiu.chandao.net", help="禅道地址")
    parser.add_argument("--account", required=True, help="用户名")
    parser.add_argument("--password", required=True, help="密码")
    parser.add_argument("--product-id", type=int, required=True, help="产品ID")
    args = parser.parse_args()
    
    sync_bugs_to_zentao(args.url, args.account, args.password, args.product_id)
