#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
解析 Coface 财务指标 Excel 为 JSON，供 CofaceConfigImporter 导入 D365。
规则与 Documents/Planning/Coface财务指标对照逻辑说明.md 保持一致。
"""
import json
import re
from pathlib import Path

try:
    import openpyxl
except ImportError:
    raise SystemExit("请先安装 openpyxl: pip install openpyxl")

EXCEL_PATH = Path("/Users/peterqiu/Work/AIWorkSpace/SanYi/Documents/BusinessAnalysis/coface/10Coface数据字典补充/SANY-financial indicators-combined-updated20160709按国家取财务指标Final version.xlsx")
OUTPUT_PATH = Path("/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Tools/CofaceConfigImporter/coface_financial_indicators.json")

INDICATOR_RE = re.compile(r"indicator\((\d+)\)")


def is_na(value) -> bool:
    if value is None:
        return True
    s = str(value).strip()
    return s == "" or s.upper() in ("N/A", "NA", "NONE", "NULL")


def normalize_text(text) -> str:
    if text is None:
        return ""
    # 把跨行断裂的 indicator(\n123) 合并成 indicator(123)
    s = str(text)
    s = re.sub(r"indicator\(\s*\n\s*(\d+)\s*\)", r"indicator(\1)", s, flags=re.I)
    # 统一换行符为空格，方便后续正则
    s = s.replace("\r", " ").replace("\n", " ")
    # 合并多余空格
    s = re.sub(r"\s+", " ", s).strip()
    return s


def extract_all_indicators(text: str) -> list[str]:
    return INDICATOR_RE.findall(text)


def extract_first_indicator(text: str) -> str | None:
    matches = extract_all_indicators(text)
    return matches[0] if matches else None


def extract_or_after_indicator(text: str) -> str | None:
    """
    针对 NetAssets：形如 "indicator(A)-indicator(B) OR indicator(C)"，
    取 OR 后面的直接编码。
    """
    parts = re.split(r"(?i)\s+OR\s+", text)
    if len(parts) >= 2:
        return extract_first_indicator(parts[-1]) or extract_first_indicator(parts[0])
    return extract_first_indicator(text)


def remove_sc_label(text: str) -> str:
    """移除 standalone / consolidated 及其引导词。"""
    text = re.sub(r"(?i)\s*for\s+standalone\s*", " ", text)
    text = re.sub(r"(?i)\s*standalone\s*financials\s*use\s*below\s*:?\s*", " ", text)
    text = re.sub(r"(?i)\s*standalone\s*:?\s*", " ", text)
    text = re.sub(r"(?i)\s*for\s+consolidated\s*", " ", text)
    text = re.sub(r"(?i)\s*consolidated\s*:?\s*", " ", text)
    text = re.sub(r"(?i)\s*if\s+standalone\s*", " ", text)
    text = re.sub(r"(?i)\s*if\s+consolidated\s*", " ", text)
    return re.sub(r"\s+", " ", text).strip()


def split_by_standalone_consolidated(text: str, formula_text: str):
    """
    按 standalone / consolidated 关键字把单元格拆成两段。
    支持三种写法：
      1) 数值和标记在同一行："34112 for standalone"
      2) 引导语 + 数值分行："if standalone financials use below:\nindicator(...)"
      3) 跨行断裂的 indicator(\n123) 自动合并
    返回 [(segment_text, segment_formula, priority), ...]。
    """
    # 先把跨行断裂的 indicator 合并，但保留其他换行用于分行识别
    text = str(text)
    text = re.sub(r"indicator\(\s*\n\s*(\d+)\s*\)", r"indicator(\1)", text, flags=re.I)
    formula_text = str(formula_text or "")
    formula_text = re.sub(r"indicator\(\s*\n\s*(\d+)\s*\)", r"indicator(\1)", formula_text, flags=re.I)

    value_lines = [ln.strip() for ln in text.split("\n") if ln.strip()]
    formula_lines = [ln.strip() for ln in formula_text.split("\n") if ln.strip()]

    def has_sc(ln: str) -> bool:
        # consolidated 可能有拼写错误（如 consoldiated），用 consol 前缀匹配
        return bool(re.search(r"(?i)standalone|consol", ln))

    def detect_priority(ln: str) -> int:
        if re.search(r"(?i)standalone", ln):
            return 1
        # 任何 consol 开头的变体都视为 consolidated
        return 2

    def has_value(ln: str) -> bool:
        """行中是否包含数字或 indicator"""
        return bool(re.search(r"\d", ln)) or bool(INDICATOR_RE.search(ln))

    # 提取数值段：引导语的下一行，或标记与数值同行的行
    segments = []
    pending_priority = None

    for idx, ln in enumerate(value_lines):
        if has_sc(ln):
            prio = detect_priority(ln)
            cleaned = remove_sc_label(ln)
            if has_value(cleaned):
                # 标记和数值在同一行
                segments.append((prio, cleaned))
                pending_priority = None
            else:
                # 引导语，等待下一行
                pending_priority = prio
        elif pending_priority is not None:
            segments.append((pending_priority, ln))
            pending_priority = None

    # 如果没有任何段，回退到整段文本
    if not segments:
        return [(normalize_text(text), normalize_text(formula_text), 1)]

    # 公式分段：同样按引导语/标记配对
    formula_segments = []
    pending_formula_priority = None
    for ln in formula_lines:
        if has_sc(ln):
            prio = detect_priority(ln)
            cleaned = remove_sc_label(ln)
            if has_value(cleaned):
                formula_segments.append((prio, cleaned))
                pending_formula_priority = None
            else:
                pending_formula_priority = prio
        elif pending_formula_priority is not None:
            formula_segments.append((pending_formula_priority, ln))
            pending_formula_priority = None

    formula_map = {prio: val for prio, val in formula_segments}
    results = []
    for priority, val in segments:
        formula = formula_map.get(priority, normalize_text(formula_text))
        results.append((val, formula, priority))
    return results


def parse_indicator_cell(text, formula_text, indicator_name: str):
    """
    解析单个指标单元格，返回 [(type_value, formula_fallback, priority), ...]。
    """
    if is_na(text):
        return []

    segments = split_by_standalone_consolidated(text, formula_text)
    results = []

    for seg_text, seg_formula, priority in segments:
        if indicator_name == "NetAssets":
            type_value = extract_or_after_indicator(seg_text)
        else:
            # 比率：先取第一个 indicator(N)
            type_value = extract_first_indicator(seg_text)
            if type_value is None:
                # 没有 indicator 时，尝试把整段当作纯数字
                seg_text_clean = re.sub(r"(?i)standalone|consolidated", "", seg_text)
                seg_text_clean = re.sub(r"[^\d]", "", seg_text_clean)
                if seg_text_clean.isdigit():
                    type_value = seg_text_clean

        if type_value:
            results.append((type_value, seg_formula, priority))

    return results


def main():
    wb = openpyxl.load_workbook(EXCEL_PATH, data_only=True)
    ws = wb.active

    countries = []
    total_records = 0

    for row in ws.iter_rows(min_row=2, values_only=True):
        country_code_raw = row[0]
        if country_code_raw is None:
            continue
        country_code = str(country_code_raw).strip().upper()
        country_name = str(row[2] or "").strip()

        indicators = []

        # NetAssets: D=3, formula E=4
        for tv, formula, priority in parse_indicator_cell(row[3], row[4], "NetAssets"):
            indicators.append({
                "name": "NetAssets",
                "typeValue": tv,
                "indicatorType": 1,
                "priority": priority,
                "formulaFallback": formula
            })

        # DebtRatio: F=5, formula G=6
        for tv, formula, priority in parse_indicator_cell(row[5], row[6], "DebtRatio"):
            indicators.append({
                "name": "DebtRatio",
                "typeValue": tv,
                "indicatorType": 2,
                "priority": priority,
                "formulaFallback": formula
            })

        # CurrentRatio: H=7, formula I=8
        for tv, formula, priority in parse_indicator_cell(row[7], row[8], "CurrentRatio"):
            indicators.append({
                "name": "CurrentRatio",
                "typeValue": tv,
                "indicatorType": 2,
                "priority": priority,
                "formulaFallback": formula
            })

        # NetProfitMargin: J=9, formula K=10
        for tv, formula, priority in parse_indicator_cell(row[9], row[10], "NetProfitMargin"):
            indicators.append({
                "name": "NetProfitMargin",
                "typeValue": tv,
                "indicatorType": 2,
                "priority": priority,
                "formulaFallback": formula
            })

        if indicators:
            countries.append({
                "countryCode": country_code,
                "countryName": country_name,
                "indicators": indicators
            })
            total_records += len(indicators)

    OUTPUT_PATH.write_text(
        json.dumps(countries, indent=2, ensure_ascii=False),
        encoding="utf-8"
    )

    print(f"✅ 解析完成")
    print(f"   国家数: {len(countries)}")
    print(f"   总记录数: {total_records}")
    print(f"   输出文件: {OUTPUT_PATH}")


if __name__ == "__main__":
    main()
