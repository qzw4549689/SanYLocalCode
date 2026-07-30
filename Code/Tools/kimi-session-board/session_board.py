#!/usr/bin/env python3
"""Kimi Code 会话看板：扫描本机 Kimi Code 会话，生成自刷新 HTML 展示各会话标题与运行状态。

数据源（自动合并、去重）：
- 新版（VS Code 扩展 0.6+）：~/.kimi-code/sessions/，索引见 session_index.jsonl / workspaces.json
- 旧版（kimi CLI 遗留）：~/.kimi/sessions/（已迁移到新版的会话自动跳过）

用法：
    python3 session_board.py --serve        # 启动本地服务（默认 http://127.0.0.1:8923），页面每 3 秒自动轮询
    python3 session_board.py                # 生成一次静态 HTML 快照
    python3 session_board.py --watch 3      # 每 3 秒重新生成静态 HTML（file:// 打开时用）
"""
import argparse
import hashlib
import html
import json
import os
import time
from datetime import datetime, timezone

NEW_DIR = os.path.expanduser("~/.kimi-code")
NEW_SESSIONS_DIR = os.path.join(NEW_DIR, "sessions")
NEW_INDEX = os.path.join(NEW_DIR, "session_index.jsonl")
NEW_WORKSPACES = os.path.join(NEW_DIR, "workspaces.json")

LEGACY_DIR = os.path.expanduser("~/.kimi")
LEGACY_SESSIONS_DIR = os.path.join(LEGACY_DIR, "sessions")
LEGACY_KIMI_JSON = os.path.join(LEGACY_DIR, "kimi.json")

OUT_HTML = os.path.join(os.path.dirname(os.path.abspath(__file__)), "session_board.html")

# 状态分档（按最后活动时间）：<10 分钟=运行中，<24 小时=近期活跃，其余=已结束
RUNNING_SEC = 600
IDLE_SEC = 86400


def parse_iso(ts):
    """ISO 时间字符串 -> epoch 秒；失败返回 None。"""
    if not ts:
        return None
    try:
        return datetime.fromisoformat(ts.replace("Z", "+00:00")).timestamp()
    except ValueError:
        return None


def status_of(mtime, now):
    age = now - mtime
    if age < RUNNING_SEC:
        return "running"
    if age < IDLE_SEC:
        return "idle"
    return "ended"


# ---------------- 新版（~/.kimi-code） ----------------

def load_new_workspaces():
    """工作区 id -> {root, name}。"""
    try:
        with open(NEW_WORKSPACES, encoding="utf-8") as f:
            data = json.load(f)
        return {k: v for k, v in (data.get("workspaces") or {}).items()}
    except Exception:
        return {}


def load_new_index():
    """session_index.jsonl 是顺序日志，逐条 fold：后面的记录覆盖前面的。"""
    index = {}
    try:
        with open(NEW_INDEX, encoding="utf-8") as f:
            for line in f:
                try:
                    rec = json.loads(line)
                except ValueError:
                    continue
                sid = rec.get("sessionId")
                if not sid:
                    continue
                if rec.get("deleted"):
                    index[sid] = {"deleted": True}
                else:
                    index[sid] = {
                        "deleted": False,
                        "sessionDir": rec.get("sessionDir"),
                        "workDir": rec.get("workDir"),
                    }
    except OSError:
        pass
    return index


def collect_new_sessions(now):
    """扫描 ~/.kimi-code/sessions，返回 (rows, migrated_legacy_ids)。"""
    rows = []
    migrated_legacy_ids = set()
    if not os.path.isdir(NEW_SESSIONS_DIR):
        return rows, migrated_legacy_ids
    workspaces = load_new_workspaces()
    index = load_new_index()

    for ws_id in sorted(os.listdir(NEW_SESSIONS_DIR)):
        ws_dir = os.path.join(NEW_SESSIONS_DIR, ws_id)
        if not os.path.isdir(ws_dir):
            continue
        ws = workspaces.get(ws_id) or {}
        for sess_name in sorted(os.listdir(ws_dir)):
            sess_dir = os.path.join(ws_dir, sess_name)
            state_file = os.path.join(sess_dir, "state.json")
            if not os.path.isdir(sess_dir) or not os.path.isfile(state_file):
                continue
            if index.get(sess_name, {}).get("deleted"):
                continue
            try:
                with open(state_file, encoding="utf-8") as f:
                    state = json.load(f)
            except Exception:
                continue

            custom = state.get("custom") or {}
            legacy_src = custom.get("kimi_cli_session_id")
            if legacy_src:
                migrated_legacy_ids.add(legacy_src)

            wire_file = os.path.join(sess_dir, "agents", "main", "wire.jsonl")
            mtime = None
            if os.path.isfile(wire_file):
                mtime = os.path.getmtime(wire_file)
            if mtime is None:
                mtime = parse_iso(state.get("updatedAt")) or parse_iso(state.get("createdAt"))
            if mtime is None:
                continue

            work_dir = state.get("workDir") or ws.get("root") or ""
            proj_name = ws.get("name") or (os.path.basename(work_dir) if work_dir else ws_id)
            archived = bool(custom.get("archived"))

            title = clean_title(state.get("title") or "")
            if not title:
                # 标题是 <git-context> 之类的占位文本：迁移会话尝试从旧版 wire 兜底
                legacy_wire = os.path.join(custom.get("kimi_cli_source_path") or "", "wire.jsonl")
                if os.path.isfile(legacy_wire):
                    title = clean_title(legacy_first_user_message(legacy_wire))
                elif os.path.isfile(wire_file):
                    title = clean_title(legacy_first_user_message(wire_file))
            if not title:
                title = "（无标题会话）"

            rows.append({
                "project_name": proj_name,
                "session_id": sess_name,
                "title": title,
                "status": status_of(mtime, now),
                "mtime": mtime,
                "mtime_str": datetime.fromtimestamp(mtime).strftime("%Y-%m-%d %H:%M:%S"),
                "archived": archived,
                "todo_text": "",
            })
    return rows, migrated_legacy_ids


# ---------------- 旧版（~/.kimi，遗留） ----------------

def load_legacy_project_paths():
    """md5(工作目录绝对路径) -> 项目路径，来自 ~/.kimi/kimi.json 的 work_dirs。"""
    mapping = {}
    try:
        with open(LEGACY_KIMI_JSON, encoding="utf-8") as f:
            data = json.load(f)
        for item in data.get("work_dirs", []):
            path = item.get("path")
            if path:
                mapping[hashlib.md5(path.encode()).hexdigest()] = path
    except Exception:
        pass
    return mapping


def legacy_first_user_message(wire_path):
    """从旧版 wire.jsonl 头部找第一条用户输入文本，作为标题兜底。流式读取，找到即停。"""
    try:
        with open(wire_path, encoding="utf-8", errors="replace") as f:
            for _ in range(200):  # 只扫前 200 行，避免大文件耗时
                line = f.readline()
                if not line:
                    break
                try:
                    obj = json.loads(line)
                except ValueError:
                    continue
                msg = obj.get("message") or {}
                if msg.get("type") != "TurnBegin":
                    continue
                user_input = (msg.get("payload") or {}).get("user_input")
                if isinstance(user_input, str):
                    # 旧格式：整条消息就是一个字符串
                    text = user_input.strip()
                    if text:
                        return text
                    continue
                for part in user_input or []:
                    if isinstance(part, str):
                        text = part.strip()
                    elif isinstance(part, dict) and part.get("type") == "text":
                        text = (part.get("text") or "").strip()
                    else:
                        continue
                    if text:
                        return text
    except OSError:
        pass
    return ""


def clean_title(title):
    """过滤无意义的自动标题（如 <git-context> 开头）。"""
    if not title:
        return ""
    title = title.strip()
    if title.startswith("<git-context>") or title.startswith("<system>"):
        return ""
    return title


def collect_legacy_sessions(now, migrated_ids):
    rows = []
    if not os.path.isdir(LEGACY_SESSIONS_DIR):
        return rows
    projects = load_legacy_project_paths()
    for proj_hash in sorted(os.listdir(LEGACY_SESSIONS_DIR)):
        proj_dir = os.path.join(LEGACY_SESSIONS_DIR, proj_hash)
        if not os.path.isdir(proj_dir):
            continue
        proj_path = projects.get(proj_hash, "")
        for sess_id in sorted(os.listdir(proj_dir)):
            if sess_id in migrated_ids:
                continue  # 已迁移到新版，避免重复
            sess_dir = os.path.join(proj_dir, sess_id)
            state_file = os.path.join(sess_dir, "state.json")
            wire_file = os.path.join(sess_dir, "wire.jsonl")
            if not os.path.isdir(sess_dir) or not os.path.isfile(state_file):
                continue
            try:
                with open(state_file, encoding="utf-8") as f:
                    state = json.load(f)
            except Exception:
                continue

            title = clean_title(state.get("custom_title") or "")
            if not title and os.path.isfile(wire_file):
                title = clean_title(legacy_first_user_message(wire_file))
            if not title:
                title = "（无标题会话）"

            try:
                mtime = os.path.getmtime(wire_file) if os.path.isfile(wire_file) else os.path.getmtime(state_file)
            except OSError:
                continue

            todos = state.get("todos") or []
            todo_done = sum(1 for t in todos if t.get("status") == "done")

            rows.append({
                "project_name": os.path.basename(proj_path) if proj_path else proj_hash[:8],
                "session_id": sess_id,
                "title": title,
                "status": status_of(mtime, now),
                "mtime": mtime,
                "mtime_str": datetime.fromtimestamp(mtime).strftime("%Y-%m-%d %H:%M:%S"),
                "archived": bool(state.get("archived")),
                "todo_text": f"{todo_done}/{len(todos)}" if todos else "",
            })
    return rows


def collect_sessions():
    now = time.time()
    rows, migrated_ids = collect_new_sessions(now)
    rows += collect_legacy_sessions(now, migrated_ids)
    rows.sort(key=lambda r: r["mtime"], reverse=True)
    return rows, now


PAGE_CSS = """
  body { font-family: -apple-system, "PingFang SC", sans-serif; margin: 24px; background: #1e1f24; color: #ddd; }
  h1 { font-size: 20px; margin: 0 0 4px; }
  .meta { color: #888; font-size: 12px; margin-bottom: 16px; }
  table { border-collapse: collapse; width: 100%; font-size: 13px; }
  th, td { text-align: left; padding: 8px 10px; border-bottom: 1px solid #333; }
  th { color: #999; font-weight: 600; position: sticky; top: 0; background: #1e1f24; }
  tr.row-running { background: rgba(46, 160, 67, 0.08); }
  .badge { padding: 2px 10px; border-radius: 10px; font-size: 12px; white-space: nowrap; }
  .badge.running { background: #2ea043; color: #fff; }
  .badge.idle { background: #9e6a03; color: #fff; }
  .badge.ended { background: #3a3d44; color: #aaa; }
  .badge.archived { background: #4a3b2a; color: #c9a06a; }
  .title { max-width: 480px; }
  .sid { color: #666; font-family: monospace; }
"""

# serve 模式的页面外壳：不带数据、不整页刷新，由 JS 每 3 秒轮询 /api/sessions 局部更新
PAGE_SHELL = """<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<title>Kimi Code 会话看板</title>
<style>__CSS__</style>
</head>
<body>
<h1>Kimi Code 会话看板</h1>
<div class="meta" id="meta">加载中…</div>
<table>
<thead><tr><th>状态</th><th>项目</th><th>标题</th><th>最后活动</th><th>待办</th><th>会话ID</th></tr></thead>
<tbody id="tbody"></tbody>
</table>
<script>
const BADGES = {
  running: ['badge running', '● 运行中'],
  idle:    ['badge idle', '近期活跃'],
  ended:   ['badge ended', '已结束'],
  archived:['badge archived', '已归档'],
};
function cell(parent, text, cls, title) {
  const td = document.createElement('td');
  if (cls) td.className = cls;
  if (title) td.title = title;
  td.textContent = text;
  parent.appendChild(td);
  return td;
}
function render(d) {
  document.getElementById('meta').textContent =
    `共 ${d.rows.length} 个会话，${d.running} 个运行中、${d.idle} 个近期活跃 · 更新于 ${d.gen_time} · 每 3 秒自动轮询`;
  const tbody = document.getElementById('tbody');
  tbody.replaceChildren();
  for (const r of d.rows) {
    const tr = document.createElement('tr');
    if (r.status === 'running' && !r.archived) tr.className = 'row-running';
    const [cls, text] = r.archived ? BADGES.archived : BADGES[r.status];
    const badgeTd = cell(tr, '', '');
    const badge = document.createElement('span');
    badge.className = cls;
    badge.textContent = text;
    badgeTd.appendChild(badge);
    cell(tr, r.project_name);
    const short = r.title.length > 80 ? r.title.slice(0, 80) + '…' : r.title;
    cell(tr, short, 'title', r.title);
    cell(tr, r.mtime_str);
    cell(tr, r.todo_text);
    cell(tr, r.session_id.replace(/^session_|^ses_/, '').slice(0, 8), 'sid');
    tbody.appendChild(tr);
  }
}
async function refresh() {
  try {
    const resp = await fetch('/api/sessions');
    if (resp.ok) render(await resp.json());
  } catch (e) { /* 服务暂时不可达时下个周期重试 */ }
}
refresh();
setInterval(refresh, 3000);
</script>
</body>
</html>"""


def render_html(rows, now):
    def esc(s):
        return html.escape(str(s), quote=True)

    trs = []
    for r in rows:
        if r["archived"]:
            badge = '<span class="badge archived">已归档</span>'
        elif r["status"] == "running":
            badge = '<span class="badge running">● 运行中</span>'
        elif r["status"] == "idle":
            badge = '<span class="badge idle">近期活跃</span>'
        else:
            badge = '<span class="badge ended">已结束</span>'
        title = esc(r["title"] if len(r["title"]) <= 80 else r["title"][:80] + "…")
        trs.append(
            f'<tr class="{"row-running" if r["status"] == "running" else ""}">'
            f"<td>{badge}</td>"
            f"<td>{esc(r['project_name'])}</td>"
            f'<td class="title" title="{esc(r["title"])}">{title}</td>'
            f"<td>{esc(r['mtime_str'])}</td>"
            f"<td>{esc(r['todo_text'])}</td>"
            f'<td class="sid">{esc(r["session_id"].replace("session_", "").replace("ses_", "")[:8])}</td>'
            f"</tr>"
        )
    running_count = sum(1 for r in rows if r["status"] == "running")
    idle_count = sum(1 for r in rows if r["status"] == "idle")
    gen_time = datetime.fromtimestamp(now).strftime("%Y-%m-%d %H:%M:%S")
    return f"""<!DOCTYPE html>
<html lang="zh-CN">
<head>
<meta charset="utf-8">
<meta http-equiv="refresh" content="3">
<title>Kimi Code 会话看板</title>
<style>{PAGE_CSS}</style>
</head>
<body>
<h1>Kimi Code 会话看板</h1>
<div class="meta">共 {len(rows)} 个会话，{running_count} 个运行中、{idle_count} 个近期活跃 · 生成于 {gen_time} · 每 3 秒自动刷新</div>
<table>
<thead><tr><th>状态</th><th>项目</th><th>标题</th><th>最后活动</th><th>待办</th><th>会话ID</th></tr></thead>
<tbody>
{"".join(trs)}
</tbody>
</table>
</body>
</html>"""


def sessions_payload():
    rows, now = collect_sessions()
    return {
        "rows": rows,
        "running": sum(1 for r in rows if r["status"] == "running"),
        "idle": sum(1 for r in rows if r["status"] == "idle"),
        "gen_time": datetime.fromtimestamp(now).strftime("%Y-%m-%d %H:%M:%S"),
    }


def serve(port):
    from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            if self.path in ("/", "/index.html"):
                body = PAGE_SHELL.replace("__CSS__", PAGE_CSS).encode()
                self.send_response(200)
                self.send_header("Content-Type", "text/html; charset=utf-8")
            elif self.path == "/favicon.ico":
                self.send_response(204)
                self.end_headers()
                return
            elif self.path == "/api/sessions":
                body = json.dumps(sessions_payload(), ensure_ascii=False).encode()
                self.send_response(200)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Cache-Control", "no-store")
            else:
                self.send_response(404)
                body = b"not found"
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def log_message(self, *args):
            pass

    server = ThreadingHTTPServer(("127.0.0.1", port), Handler)
    print(f"看板已启动：http://127.0.0.1:{port} （Ctrl+C 停止）")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass


def generate():
    rows, now = collect_sessions()
    with open(OUT_HTML, "w", encoding="utf-8") as f:
        f.write(render_html(rows, now))
    running = sum(1 for r in rows if r["status"] == "running")
    print(f"[{datetime.fromtimestamp(now).strftime('%H:%M:%S')}] 已生成 {OUT_HTML}（{len(rows)} 个会话，{running} 个运行中）")


def main():
    ap = argparse.ArgumentParser(description="Kimi Code 会话看板生成器")
    ap.add_argument("--watch", type=float, metavar="秒", help="循环生成静态 HTML 的间隔")
    ap.add_argument("--serve", type=int, metavar="端口", nargs="?", const=8923,
                    help="启动本地服务（默认端口 8923），页面自动轮询最新数据")
    args = ap.parse_args()
    if args.serve:
        serve(args.serve)
    elif args.watch:
        while True:
            generate()
            time.sleep(args.watch)
    else:
        generate()


if __name__ == "__main__":
    main()
