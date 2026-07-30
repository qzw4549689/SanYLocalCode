#!/usr/bin/env python3
"""
根据实体定义 JSON 批量更新融资管理 3 个实体的中英双语显示名。
- 实体显示名：使用 MetadataTool update-entity-displayname（SDK，同时写入 2052/1033）
- 字段显示名：使用 MetadataTool set-field-label（Web API PUT，解决 SDK 对中文标签不持久化的问题）
"""
import json
import subprocess
import sys
import time
from pathlib import Path

DEFINITIONS_DIR = Path(__file__).parent / "Definitions"
ENTITY_FILES = [
    "mcs_fsm_resource.json",
    "mcs_fsm_data.json",
    "mcs_fsm_detail_data.json",
]

LOCK_INDICATORS = [
    "CustomizationLockException",
    "another [Publish] running",
    "another solution at the same time",
    "Please try again later",
]
MAX_RETRIES = 5
RETRY_DELAY_SECONDS = 30


def is_lock_error(text):
    return any(indicator in text for indicator in LOCK_INDICATORS)


def run_cmd(args):
    full_args = ["dotnet", "run", "--no-build"] + args
    for attempt in range(1, MAX_RETRIES + 1):
        result = subprocess.run(full_args, cwd=Path(__file__).parent, capture_output=True, text=True)
        combined = result.stdout + result.stderr

        success = result.returncode == 0 and "错误:" not in combined and "Cannot start" not in combined
        if success:
            for line in combined.splitlines():
                if "更新实体显示名称" in line or "设置字段多语言显示名称" in line or "更新成功" in line or "设置成功" in line:
                    print(line.strip())
            return True

        print(f"{' '.join(full_args)} (attempt {attempt}/{MAX_RETRIES}) 失败")
        if "Cannot start" in combined:
            print("  检测到并发 Publish 锁")
        if attempt < MAX_RETRIES and is_lock_error(combined):
            print(f"  等待 {RETRY_DELAY_SECONDS} 秒后重试...")
            time.sleep(RETRY_DELAY_SECONDS)
        else:
            print(f"  错误: {combined[-500:]}")
            return False
    return False


def update_entity_labels(definition):
    entity = definition["entityName"]
    zh = definition.get("displayNameZh") or definition["displayName"]
    en = definition.get("displayNameEn") or definition["displayName"]
    print(f"\n[{entity}] 更新实体显示名: {zh} / {en}")
    return run_cmd(["set-entity-label", entity, zh, en])


def update_field_labels(definition):
    entity = definition["entityName"]
    ok = True

    primary = definition.get("primaryAttribute")
    primary_zh = definition.get("primaryAttributeDisplayNameZh") or definition.get("primaryAttributeDisplayName")
    primary_en = definition.get("primaryAttributeDisplayNameEn") or definition.get("primaryAttributeDisplayName")
    if primary and primary_zh and primary_en:
        print(f"[{entity}] 更新主字段: {primary}")
        ok = run_cmd(["set-field-label", entity, primary, primary_zh, primary_en]) and ok

    for field in definition.get("fields", []):
        schema = field["schemaName"]
        zh = field.get("displayNameZh") or field.get("displayName")
        en = field.get("displayNameEn") or field.get("displayName")
        if zh and en:
            print(f"[{entity}] 更新字段: {schema}")
            ok = run_cmd(["set-field-label", entity, schema, zh, en]) and ok

    return ok


def main():
    all_ok = True
    for filename in ENTITY_FILES:
        path = DEFINITIONS_DIR / filename
        if not path.exists():
            print(f"Skip missing file: {path}")
            continue
        with open(path, "r", encoding="utf-8") as f:
            definition = json.load(f)
        all_ok = update_entity_labels(definition) and all_ok
        all_ok = update_field_labels(definition) and all_ok

    print("\n========== 完成 ==========")
    if all_ok:
        print("融资管理 3 个实体的中英双语标签更新成功")
    else:
        print("部分更新失败，请检查日志")
        sys.exit(1)


if __name__ == "__main__":
    main()
