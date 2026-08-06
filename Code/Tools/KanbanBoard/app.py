#!/usr/bin/env python3
"""任务进度看板：纯 Python 标准库实现的轻量看板服务（无第三方依赖）。

功能：
- 三栏看板页面（待处理 / 开发中 / 待发布），已发布任务进入历史记录
- REST API 供 AI 通过 curl 录入任务、更新状态
- SQLite 单文件持久化（kanban.db 自动生成）

用法：
    python app.py              # 默认 http://0.0.0.0:8100
    python app.py --port 8100

REST API：
    GET    /api/tasks[?status=pending]     查询任务列表（可按状态过滤）
    POST   /api/tasks                      录入任务 {"title": "...", "description": "..."}
    GET    /api/tasks/T-0001               任务详情 + 状态历史
    PATCH  /api/tasks/T-0001               更新 {"status"/"title"/"description", "note"}
    DELETE /api/tasks/T-0001               删除误录任务

    GET    /api/release-items              发布清单（当前批次，按包聚合数据源）
    POST   /api/release-items              全量替换当前批次 {"items": [...]}（批量导入用）
    POST   /api/release-items/item         逐项登记（开发完成登记用，主入口）
    PATCH  /api/release-items/item/<rowid> 更新单项（改摘要/入包状态/状态等）
    DELETE /api/release-items/item/<rowid> 删除误登记项
    POST   /api/release-items/release      标记已发布；body {"section"/"package"} 时只归档对应分组

状态机：pending(待处理) -> in_progress(开发中) -> pending_release(待发布) -> released(已发布)
        允许任意状态间迁移（误操作可改回）。
"""
import argparse
import json
import os
import re
import sqlite3
from datetime import datetime, timezone, timedelta
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DB_PATH = os.path.join(BASE_DIR, "kanban.db")
INDEX_HTML = os.path.join(BASE_DIR, "index.html")

CST = timezone(timedelta(hours=8))  # 东八区

STATUSES = ("pending", "in_progress", "pending_release", "released")

TASK_ID_RE = re.compile(r"^T-\d{4,}$")


def now_str():
    return datetime.now(CST).strftime("%Y-%m-%d %H:%M:%S")


def get_db():
    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    return conn


def init_db():
    conn = get_db()
    conn.executescript(
        """
        CREATE TABLE IF NOT EXISTS tasks (
            seq         INTEGER PRIMARY KEY AUTOINCREMENT,
            id          TEXT UNIQUE NOT NULL,
            title       TEXT NOT NULL,
            description TEXT DEFAULT '',
            status      TEXT NOT NULL DEFAULT 'pending',
            created_at  TEXT NOT NULL,
            updated_at  TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS history (
            rowid       INTEGER PRIMARY KEY AUTOINCREMENT,
            task_id     TEXT NOT NULL,
            from_status TEXT,
            to_status   TEXT NOT NULL,
            note        TEXT DEFAULT '',
            changed_at  TEXT NOT NULL
        );
        CREATE TABLE IF NOT EXISTS release_items (
            rowid       INTEGER PRIMARY KEY AUTOINCREMENT,
            section     TEXT NOT NULL,   -- entity/webresource/plugin/customapi/config/manual
            component   TEXT NOT NULL,
            item_type   TEXT DEFAULT '',
            summary     TEXT DEFAULT '',
            ref         TEXT DEFAULT '',  -- 关联禅道号
            package     TEXT DEFAULT '',  -- 指定发版包名（空 = entity 默认按当天日期）
            in_package  INTEGER DEFAULT 0,
            status      TEXT NOT NULL DEFAULT 'pending',
            synced_at   TEXT NOT NULL,
            released_at TEXT
        );
        """
    )
    conn.commit()
    conn.close()


def row_to_task(row):
    return {
        "id": row["id"],
        "title": row["title"],
        "description": row["description"],
        "status": row["status"],
        "created_at": row["created_at"],
        "updated_at": row["updated_at"],
    }


def create_task(title, description=""):
    title = (title or "").strip()
    if not title:
        raise ValueError("title 不能为空")
    conn = get_db()
    ts = now_str()
    cur = conn.execute(
        "INSERT INTO tasks (id, title, description, status, created_at, updated_at) "
        "VALUES ('', ?, ?, 'pending', ?, ?)",
        (title, description or "", ts, ts),
    )
    task_id = "T-%04d" % cur.lastrowid
    conn.execute("UPDATE tasks SET id=? WHERE seq=?", (task_id, cur.lastrowid))
    conn.execute(
        "INSERT INTO history (task_id, from_status, to_status, note, changed_at) "
        "VALUES (?, NULL, 'pending', '任务录入', ?)",
        (task_id, ts),
    )
    conn.commit()
    row = conn.execute("SELECT * FROM tasks WHERE id=?", (task_id,)).fetchone()
    conn.close()
    return row_to_task(row)


def list_tasks(status=None):
    conn = get_db()
    if status:
        if status not in STATUSES:
            conn.close()
            raise ValueError("非法状态: %s" % status)
        rows = conn.execute(
            "SELECT * FROM tasks WHERE status=? ORDER BY seq", (status,)
        ).fetchall()
    else:
        rows = conn.execute("SELECT * FROM tasks ORDER BY seq").fetchall()
    conn.close()
    return [row_to_task(r) for r in rows]


def get_task(task_id):
    conn = get_db()
    row = conn.execute("SELECT * FROM tasks WHERE id=?", (task_id,)).fetchone()
    if not row:
        conn.close()
        return None
    task = row_to_task(row)
    hist = conn.execute(
        "SELECT from_status, to_status, note, changed_at FROM history "
        "WHERE task_id=? ORDER BY rowid",
        (task_id,),
    ).fetchall()
    conn.close()
    task["history"] = [dict(h) for h in hist]
    return task


def update_task(task_id, fields):
    conn = get_db()
    row = conn.execute("SELECT * FROM tasks WHERE id=?", (task_id,)).fetchone()
    if not row:
        conn.close()
        return None
    ts = now_str()
    new_status = fields.get("status")
    if new_status is not None and new_status not in STATUSES:
        conn.close()
        raise ValueError("非法状态: %s（允许: %s）" % (new_status, "/".join(STATUSES)))
    title = fields.get("title")
    description = fields.get("description")
    note = fields.get("note", "")

    if title is not None:
        title = title.strip()
        if not title:
            conn.close()
            raise ValueError("title 不能为空")
        conn.execute("UPDATE tasks SET title=?, updated_at=? WHERE id=?", (title, ts, task_id))
    if description is not None:
        conn.execute("UPDATE tasks SET description=?, updated_at=? WHERE id=?", (description, ts, task_id))
    if new_status is not None and new_status != row["status"]:
        conn.execute("UPDATE tasks SET status=?, updated_at=? WHERE id=?", (new_status, ts, task_id))
        conn.execute(
            "INSERT INTO history (task_id, from_status, to_status, note, changed_at) "
            "VALUES (?, ?, ?, ?, ?)",
            (task_id, row["status"], new_status, note, ts),
        )
    conn.commit()
    conn.close()
    return get_task(task_id)


def delete_task(task_id):
    conn = get_db()
    cur = conn.execute("DELETE FROM tasks WHERE id=?", (task_id,))
    conn.execute("DELETE FROM history WHERE task_id=?", (task_id,))
    conn.commit()
    deleted = cur.rowcount > 0
    conn.close()
    return deleted


# ---------------- 发布清单 ----------------

RELEASE_SECTIONS = ("entity", "webresource", "plugin", "customapi", "config", "manual")


def _validate_item_fields(section, component):
    if section not in RELEASE_SECTIONS:
        raise ValueError("非法分区: %s（允许: %s）" % (section, "/".join(RELEASE_SECTIONS)))
    if not (component or "").strip():
        raise ValueError("component 不能为空")


def add_release_item(fields):
    """逐项登记发布项（开发完成后登记的主入口）。"""
    section = fields.get("section", "")
    component = (fields.get("component") or "").strip()
    _validate_item_fields(section, component)
    conn = get_db()
    ts = now_str()
    cur = conn.execute(
        "INSERT INTO release_items (section, component, item_type, summary, ref, package, in_package, status, synced_at) "
        "VALUES (?, ?, ?, ?, ?, ?, ?, 'pending', ?)",
        (
            section,
            component,
            fields.get("item_type", ""),
            fields.get("summary", ""),
            fields.get("ref", ""),
            fields.get("package", ""),
            1 if fields.get("in_package") else 0,
            ts,
        ),
    )
    conn.commit()
    rowid = cur.lastrowid
    conn.close()
    return rowid


def update_release_item(rowid, fields):
    """更新单项：summary/ref/package/in_package/status(pending|released) 等。"""
    conn = get_db()
    row = conn.execute("SELECT * FROM release_items WHERE rowid=?", (rowid,)).fetchone()
    if not row:
        conn.close()
        return None
    ts = now_str()
    allowed = ("section", "component", "item_type", "summary", "ref", "package", "in_package")
    for key in allowed:
        if key in fields:
            val = fields[key]
            if key == "in_package":
                val = 1 if val else 0
            conn.execute(
                "UPDATE release_items SET %s=?, synced_at=? WHERE rowid=?" % key,
                (val, ts, rowid),
            )
    new_status = fields.get("status")
    if "section" in fields and fields["section"] not in RELEASE_SECTIONS:
        conn.close()
        raise ValueError("非法分区: %s" % fields["section"])
    if new_status is not None and new_status not in ("pending", "released"):
        conn.close()
        raise ValueError("非法状态: %s" % new_status)
    if new_status in ("pending", "released") and new_status != row["status"]:
        conn.execute(
            "UPDATE release_items SET status=?, released_at=?, synced_at=? WHERE rowid=?",
            (new_status, ts if new_status == "released" else None, ts, rowid),
        )
    conn.commit()
    conn.close()
    return rowid


def delete_release_item(rowid):
    conn = get_db()
    cur = conn.execute("DELETE FROM release_items WHERE rowid=?", (rowid,))
    conn.commit()
    deleted = cur.rowcount > 0
    conn.close()
    return deleted


def replace_release_items(items):
    """全量替换当前批次发布项（批量导入用；日常登记走 add_release_item）。"""
    conn = get_db()
    conn.execute("DELETE FROM release_items WHERE status='pending'")
    ts = now_str()
    for it in items:
        section = it.get("section", "")
        if section not in RELEASE_SECTIONS:
            conn.close()
            raise ValueError("非法分区: %s（允许: %s）" % (section, "/".join(RELEASE_SECTIONS)))
        component = (it.get("component") or "").strip()
        if not component:
            conn.close()
            raise ValueError("component 不能为空")
        conn.execute(
            "INSERT INTO release_items (section, component, item_type, summary, ref, package, in_package, status, synced_at) "
            "VALUES (?, ?, ?, ?, ?, ?, ?, 'pending', ?)",
            (
                section,
                component,
                it.get("item_type", ""),
                it.get("summary", ""),
                it.get("ref", ""),
                it.get("package", ""),
                1 if it.get("in_package") else 0,
                ts,
            ),
        )
    conn.commit()
    conn.close()
    return len(items), ts


def list_release_items(status="pending"):
    conn = get_db()
    rows = conn.execute(
        "SELECT * FROM release_items WHERE status=? ORDER BY rowid", (status,)
    ).fetchall()
    conn.close()
    items = [
        {
            "rowid": r["rowid"],
            "section": r["section"],
            "component": r["component"],
            "item_type": r["item_type"],
            "summary": r["summary"],
            "ref": r["ref"],
            "package": r["package"],
            "in_package": bool(r["in_package"]),
            "synced_at": r["synced_at"],
            "released_at": r["released_at"],
        }
        for r in rows
    ]
    synced_at = max((r["synced_at"] for r in rows), default=None)
    return {"synced_at": synced_at, "items": items}


def release_all_items(section=None, package=None):
    """标记发布项为已发布；指定 section/package 时只归档对应分组，否则全部。"""
    conn = get_db()
    ts = now_str()
    if section is None and package is None:
        cur = conn.execute(
            "UPDATE release_items SET status='released', released_at=? WHERE status='pending'",
            (ts,),
        )
    else:
        # 固定包 -> 分区映射；entity 包 -> entity 分区按包名过滤（默认新包匹配空 package）
        fixed = {"webresource": "McsWebResource", "plugin": "McsPlugin", "customapi": "McsCustomAPI"}
        if package in fixed.values():
            section = [k for k, v in fixed.items() if v == package][0]
            cur = conn.execute(
                "UPDATE release_items SET status='released', released_at=? "
                "WHERE status='pending' AND section=?",
                (ts, section),
            )
        elif section == "entity" or (package and package.startswith("entity_")):
            # package 为空 = 默认新包分组（清单未指定包名的项）
            cur = conn.execute(
                "UPDATE release_items SET status='released', released_at=? "
                "WHERE status='pending' AND section='entity' AND package=?",
                (ts, package or ""),
            )
        elif package:
            # config/manual 项指定了自定义包名（如 MessageHandler）时按包名归档
            cur = conn.execute(
                "UPDATE release_items SET status='released', released_at=? "
                "WHERE status='pending' AND package=?",
                (ts, package),
            )
        else:
            cur = conn.execute(
                "UPDATE release_items SET status='released', released_at=? "
                "WHERE status='pending' AND section=?",
                (ts, section),
            )
    conn.commit()
    count = cur.rowcount
    conn.close()
    return count, ts


class Handler(BaseHTTPRequestHandler):
    server_version = "KanbanBoard/1.0"

    def log_message(self, fmt, *args):
        pass  # 静默日志

    # ---------- 工具方法 ----------

    def _send_json(self, data, code=200):
        body = json.dumps(data, ensure_ascii=False, indent=2).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(body)

    def _send_error(self, code, message):
        self._send_json({"error": message}, code)

    def _read_body(self):
        length = int(self.headers.get("Content-Length") or 0)
        if length == 0:
            return {}
        raw = self.rfile.read(length).decode("utf-8")
        ctype = self.headers.get("Content-Type", "")
        if "application/x-www-form-urlencoded" in ctype:
            return {k: v[0] for k, v in parse_qs(raw).items()}
        try:
            return json.loads(raw)
        except ValueError:
            raise ValueError("请求体不是合法 JSON")

    # ---------- 路由 ----------

    def do_GET(self):
        parsed = urlparse(self.path)
        path = parsed.path
        query = parse_qs(parsed.query)
        if path in ("/", "/index.html"):
            self._serve_index()
        elif path == "/api/tasks":
            status = query.get("status", [None])[0]
            try:
                self._send_json({"tasks": list_tasks(status)})
            except ValueError as e:
                self._send_error(400, str(e))
        elif path == "/api/release-items":
            status = query.get("status", ["pending"])[0]
            if status not in ("pending", "released"):
                self._send_error(400, "非法状态: %s" % status)
            else:
                self._send_json(list_release_items(status))
        elif path.startswith("/api/tasks/"):
            task_id = path.rsplit("/", 1)[-1].upper()
            task = get_task(task_id)
            if task is None:
                self._send_error(404, "任务不存在: %s" % task_id)
            else:
                self._send_json(task)
        else:
            self._send_error(404, "Not Found")

    def do_POST(self):
        path = urlparse(self.path).path
        if path == "/api/tasks":
            try:
                body = self._read_body()
                task = create_task(body.get("title"), body.get("description", ""))
                self._send_json(task, 201)
            except ValueError as e:
                self._send_error(400, str(e))
        elif path == "/api/release-items":
            try:
                body = self._read_body()
                items = body.get("items")
                if not isinstance(items, list):
                    raise ValueError("body 需要 {\"items\": [...]}")
                count, ts = replace_release_items(items)
                self._send_json({"replaced": count, "synced_at": ts})
            except ValueError as e:
                self._send_error(400, str(e))
        elif path == "/api/release-items/item":
            try:
                body = self._read_body()
                rowid = add_release_item(body)
                self._send_json({"rowid": rowid}, 201)
            except ValueError as e:
                self._send_error(400, str(e))
        elif path == "/api/release-items/release":
            try:
                body = self._read_body()
            except ValueError as e:
                self._send_error(400, str(e))
                return
            count, ts = release_all_items(body.get("section"), body.get("package"))
            self._send_json({"released": count, "released_at": ts})
        else:
            self._send_error(404, "Not Found")

    def do_PATCH(self):
        path = urlparse(self.path).path
        if path.startswith("/api/release-items/item/"):
            rowid = path.rsplit("/", 1)[-1]
            if not rowid.isdigit():
                self._send_error(400, "非法 rowid: %s" % rowid)
                return
            try:
                body = self._read_body()
                result = update_release_item(int(rowid), body)
                if result is None:
                    self._send_error(404, "发布项不存在: rowid=%s" % rowid)
                else:
                    self._send_json({"updated": result})
            except ValueError as e:
                self._send_error(400, str(e))
            return
        if not path.startswith("/api/tasks/"):
            self._send_error(404, "Not Found")
            return
        task_id = path.rsplit("/", 1)[-1].upper()
        if not TASK_ID_RE.match(task_id):
            self._send_error(400, "非法任务编号: %s（格式 T-0001）" % task_id)
            return
        try:
            body = self._read_body()
            task = update_task(task_id, body)
            if task is None:
                self._send_error(404, "任务不存在: %s" % task_id)
            else:
                self._send_json(task)
        except ValueError as e:
            self._send_error(400, str(e))

    def do_DELETE(self):
        path = urlparse(self.path).path
        if path.startswith("/api/release-items/item/"):
            rowid = path.rsplit("/", 1)[-1]
            if rowid.isdigit() and delete_release_item(int(rowid)):
                self._send_json({"deleted": int(rowid)})
            else:
                self._send_error(404, "发布项不存在: rowid=%s" % rowid)
            return
        if not path.startswith("/api/tasks/"):
            self._send_error(404, "Not Found")
            return
        task_id = path.rsplit("/", 1)[-1].upper()
        if delete_task(task_id):
            self._send_json({"deleted": task_id})
        else:
            self._send_error(404, "任务不存在: %s" % task_id)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, PATCH, DELETE, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def _serve_index(self):
        try:
            with open(INDEX_HTML, "rb") as f:
                body = f.read()
            self.send_response(200)
            self.send_header("Content-Type", "text/html; charset=utf-8")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)
        except FileNotFoundError:
            self._send_error(500, "index.html 不存在")


def main():
    parser = argparse.ArgumentParser(description="任务进度看板服务")
    parser.add_argument("--port", type=int, default=8100, help="监听端口（默认 8100）")
    parser.add_argument("--host", default="0.0.0.0", help="监听地址（默认 0.0.0.0）")
    args = parser.parse_args()

    init_db()
    server = ThreadingHTTPServer((args.host, args.port), Handler)
    print("任务看板已启动: http://%s:%d  (Ctrl+C 停止)" % (args.host, args.port))
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\n已停止")


if __name__ == "__main__":
    main()
