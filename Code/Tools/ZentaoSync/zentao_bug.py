"""
禅道 Bug 处理 CLI（2026-07-30 新增）

子命令：
  list     读取指派给我的待处理 Bug 列表
  get      读取单个 Bug 详情
  resolve  标记 Bug 为已解决，填写处理结果，并指派给 Bug 提出人（需 --yes 确认）

认证配置（优先级从高到低）：
  1. 环境变量 ZENTAO_URL / ZENTAO_ACCOUNT / ZENTAO_PASSWORD
  2. 本目录下 .zentao.json（已加入 .gitignore，格式：{"url": "...", "account": "...", "password": "..."）
"""
import sys
import os
import json
import argparse
import html
import re

sys.path.insert(0, os.path.dirname(__file__))
from zentao_client import ZentaoClient

CONFIG_PATH = os.path.join(os.path.dirname(__file__), ".zentao.json")


def load_config(args):
    """按优先级加载禅道连接配置"""
    url = os.environ.get("ZENTAO_URL")
    account = os.environ.get("ZENTAO_ACCOUNT")
    password = os.environ.get("ZENTAO_PASSWORD")

    if os.path.exists(CONFIG_PATH):
        with open(CONFIG_PATH, encoding="utf-8") as f:
            cfg = json.load(f)
        url = url or cfg.get("url")
        account = account or cfg.get("account")
        password = password or cfg.get("password")

    # 命令行参数最优先
    url = args.url or url
    account = args.account or account
    password = args.password or password

    if not all([url, account, password]):
        print("❌ 缺少禅道连接配置。请设置环境变量 ZENTAO_URL/ZENTAO_ACCOUNT/ZENTAO_PASSWORD，"
              "或创建 .zentao.json，或通过 --url/--account/--password 传入")
        sys.exit(1)
    return url, account, password


def strip_html(text):
    """把 Bug steps 等富文本字段转为纯文本"""
    if not text:
        return ""
    text = re.sub(r"<br\s*/?>", "\n", text)
    text = re.sub(r"</(p|div|li|tr|h\d)>", "\n", text)
    text = re.sub(r"<li>", "- ", text)
    text = re.sub(r"<[^>]+>", "", text)
    return html.unescape(text).strip()


def cmd_list(args, url, account, password):
    """读取我的待处理 Bug 列表（Web 会话方式，兼容 16.0 定制部署）"""
    from zentao_web import ZentaoWebClient
    web = ZentaoWebClient(url, account, password)
    web.login()
    bugs = web.get_my_bugs()
    if args.status == "resolved":
        bugs = [b for b in bugs if b.get("resolution")]
    if not bugs:
        print("（当前没有指派给我的待处理 Bug）")
        return
    print(f"共 {len(bugs)} 个待处理 Bug：\n")
    print(f"{'ID':<8}{'级别':<8}{'优先级':<8}{'确认':<6}{'类型':<10}{'提出人':<10}{'标题'}")
    print("-" * 110)
    for b in bugs:
        print(f"{b['id']:<8}{b['severity']:<8}{b['pri']:<8}{b['confirmed']:<6}"
              f"{b['type']:<10}{b['openedBy']:<10}{b['title']}")


def cmd_get(client, args):
    bug = client.get_bug(args.bug_id)
    print(f"Bug #{bug.get('id')}  {bug.get('title')}")
    print("-" * 100)
    fields = [
        ("产品", "product"), ("模块", "module"), ("状态", "status"),
        ("严重程度", "severity"), ("优先级", "pri"), ("类型", "type"),
        ("提出人", "openedBy"), ("提出时间", "openedDate"),
        ("指派给", "assignedTo"), ("解决状态", "resolution"),
        ("影响版本", "openedBuild"), ("解决版本", "resolvedBuild"),
    ]
    for label, key in fields:
        val = bug.get(key)
        if isinstance(val, dict):
            val = val.get("name") or val.get("account") or json.dumps(val, ensure_ascii=False)
        elif isinstance(val, list):
            val = ",".join(str(v.get("name", v)) if isinstance(v, dict) else str(v) for v in val)
        if val not in (None, "", []):
            print(f"{label}: {val}")
    print("\n--- 重现步骤/描述 ---")
    print(strip_html(bug.get("steps")) or "（空）")


def cmd_resolve(args, url, account, password):
    """标记 Bug 已解决：API 读取预览 → Web 表单提交 → API 回读验证"""
    from zentao_web import ZentaoWebClient

    client = ZentaoClient(url, account, password)
    client.login()
    bug = client.get_bug(args.bug_id)
    title = bug.get("title")
    opened_by = bug.get("openedBy")
    opener = opened_by.get("account") if isinstance(opened_by, dict) else opened_by
    status = bug.get("status")
    status = status.get("code") if isinstance(status, dict) else status

    print(f"即将解决 Bug #{args.bug_id}：{title}")
    print(f"  当前状态: {status}  提出人: {opener}")
    print(f"  解决方案: {args.resolution}  解决后指派给: {opener}（提出人验证）")
    print(f"  处理结果: {args.comment}")

    if not args.yes:
        print("\n⚠️ 未执行。确认无误后请加 --yes 参数重新执行。")
        return

    if str(status) != "active":
        print(f"\n❌ Bug 当前状态为 {status}，仅 active 状态可标记已解决，已取消。")
        sys.exit(1)

    web = ZentaoWebClient(url, account, password)
    web.login()
    result = web.resolve_bug(
        args.bug_id,
        resolution=args.resolution,
        resolved_build=args.build,
        assigned_to=opener,
        comment=args.comment,
    )

    # 回读验证
    bug2 = client.get_bug(args.bug_id)
    new_status = bug2.get("status")
    new_status = new_status.get("code") if isinstance(new_status, dict) else new_status
    assigned = bug2.get("assignedTo")
    assigned = assigned.get("account") if isinstance(assigned, dict) else assigned
    if str(new_status) == "resolved":
        print(f"\n✅ Bug #{args.bug_id} 已标记解决，状态=resolved，指派给={assigned}（提出人验证）")
    else:
        print(f"\n⚠️ 已提交但回读状态为 {new_status}，请人工在禅道页面确认")
        print(f"   解决表单回显: {result}")


def main():
    parser = argparse.ArgumentParser(description="禅道 Bug 处理工具")
    parser.add_argument("--url", help="禅道地址（也可用 ZENTAO_URL）")
    parser.add_argument("--account", help="账号（也可用 ZENTAO_ACCOUNT）")
    parser.add_argument("--password", help="密码（也可用 ZENTAO_PASSWORD）")
    sub = parser.add_subparsers(dest="command", required=True)

    p_list = sub.add_parser("list", help="读取指派给我的待处理 Bug 列表")
    p_list.add_argument("--status", default="active", help="保留参数，默认 active（Web 列表本身即待处理）")

    p_get = sub.add_parser("get", help="读取单个 Bug 详情")
    p_get.add_argument("bug_id", type=int, help="Bug 编号")

    p_res = sub.add_parser("resolve", help="标记 Bug 已解决并指派给提出人")
    p_res.add_argument("bug_id", type=int, help="Bug 编号")
    p_res.add_argument("--comment", required=True, help="处理结果说明")
    p_res.add_argument("--resolution", default="fixed", help="解决方案，默认 fixed")
    p_res.add_argument("--build", default=None, help="解决版本 ID（默认取 Bug 影响版本）")
    p_res.add_argument("--yes", action="store_true", help="确认执行（不加则仅预览）")

    args = parser.parse_args()
    url, account, password = load_config(args)

    if args.command == "list":
        cmd_list(args, url, account, password)
        return
    if args.command == "resolve":
        cmd_resolve(args, url, account, password)
        return

    client = ZentaoClient(url, account, password)
    client.login()

    if args.command == "get":
        cmd_get(client, args)


if __name__ == "__main__":
    main()
