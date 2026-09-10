#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
离线导入仿真（预热导入）— 2026-08-20 新增（#1641 幽灵 Step 生产导入被拦事故防线）

用途：发版前模拟「按固定顺序把本批包导入目标环境」，提前发现依赖不闭合
（Step/CustomAPI 的实现类所在 Assembly 不在本批先发包、也不已在目标环境）。

原理：
- 包内容/依赖边：实时查 DEV1（solutioncomponent + step/customapi 元数据，只读）
- 目标环境状态：
  --target uat  → 在线精确查询 UAT（我方有只读权限）
  --target prod → 离线注册表（解析 Backups/Solutions 下全部归档 zip 的 solution.xml
                  RootComponents 构建；我方无生产权限，注册表为准入近似）
- 依赖边（v1）：
  Step → 实现类所在 Assembly；CustomAPI → 实现类所在 Assembly；Step → 主实体

判定：
  ✅ 闭合（在本批先发包 / 目标环境已有 / 平台内置实体）
  🚨 缺口（我方程序集不在本批也不在目标台账 → 导入必报缺少依赖项，#1641 同款）
  ⚠️ 待确认（他方组件，台账未覆盖，需 IT 确认生产是否已存在）

用法：
  python3 simulate_import.py                 # 默认批次 McsCustomAPI+McsPlugin，target=uat
  python3 simulate_import.py --target prod
  python3 simulate_import.py --batch McsCustomAPI McsPlugin entity_20260820_peter
  python3 simulate_import.py build-registry  # 重建生产注册表（扫 Backups/Solutions）

依赖：Code/Tools/MetadataTool 的 get-token（Device Code 缓存 token），全部只读。
"""
import json
import os
import subprocess
import sys
import urllib.parse
import urllib.request
import zipfile
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
METADATA_TOOL = os.path.join(ROOT, "Code", "Tools", "MetadataTool")
ARCHIVE_DIR = os.path.join(ROOT, "Backups", "Solutions")
REGISTRY_PATH = os.path.join(ARCHIVE_DIR, "registry_prod.json")
BASELINE_PATH = os.path.join(ARCHIVE_DIR, "prod_baseline_extra.json")  # 基线补充：UAT 镜像种子 + 人工确认项

DEV1 = "https://dev1.crm5.dynamics.com"
UAT = "https://sany-uat.crm5.dynamics.com"

DEFAULT_BATCH = ["McsCustomAPI", "McsPlugin"]  # 含插件类组件的包；实体/角色包每批不同，用 --batch 传入

BUILTIN_ENTITY_NOTE = "平台内置实体"


def get_token(env_url):
    env = dict(os.environ)
    env["D365_URL"] = env_url
    subprocess.run(["dotnet", "run", "--no-build", "--", "get-token"],
                   cwd=METADATA_TOOL, env=env, capture_output=True)
    return open("/tmp/d365_token.txt").read().strip()


class WebApi:
    def __init__(self, base, token):
        self.base = base
        self.token = token

    def _open(self, url):
        req = urllib.request.Request(url, headers={
            "Authorization": "Bearer " + self.token,
            "Accept": "application/json",
            "OData-MaxVersion": "4.0",
            "OData-Version": "4.0",
        })
        with urllib.request.urlopen(req) as resp:
            return json.loads(resp.read())

    @staticmethod
    def _qs(params):
        # Dataverse 不认 urlencode 默认的空格加号编码，必须 %20
        return urllib.parse.urlencode(params, quote_via=urllib.parse.quote)

    def get(self, path, params=None):
        url = self.base + path
        if params:
            url += "?" + self._qs(params)
        return self._open(url)

    def get_all(self, path, params=None):
        items = []
        url = self.base + path + ("?" + self._qs(params) if params else "")
        while url:
            data = self._open(url)
            items.extend(data.get("value", []))
            url = data.get("@odata.nextLink")
        return items


# ==================== 生产注册表（离线） ====================

def build_registry():
    """扫描归档目录全部 zip，提取 solution.xml RootComponents 构建生产组件注册表。"""
    registry = {"assemblies": {}, "entities": {}, "others": {}, "sources": []}
    zips = []
    for dirpath, _, files in os.walk(ARCHIVE_DIR):
        for f in files:
            if f.endswith(".zip"):
                zips.append(os.path.join(dirpath, f))
    for zpath in sorted(zips):
        try:
            with zipfile.ZipFile(zpath) as z:
                if "solution.xml" not in z.namelist():
                    continue
                xml = z.read("solution.xml")
        except Exception:
            continue
        try:
            root = ET.fromstring(xml)
        except ET.ParseError:
            continue
        rel = os.path.relpath(zpath, ARCHIVE_DIR)
        count = 0
        for rc in root.iter("RootComponent"):
            ctype = rc.get("type")
            oid = (rc.get("id") or "").strip("{}").lower()
            schema = rc.get("schemaName") or ""
            if ctype == "91" and oid:      # PluginAssembly
                registry["assemblies"].setdefault(oid, {"source": rel, "name": schema})
            elif ctype == "1" and schema:  # Entity
                registry["entities"].setdefault(schema.lower(), {"source": rel})
            elif oid:
                registry["others"].setdefault(f"{ctype}:{oid}", {"source": rel})
            count += 1
        registry["sources"].append({"zip": rel, "root_components": count})
    with open(REGISTRY_PATH, "w", encoding="utf-8") as f:
        json.dump(registry, f, ensure_ascii=False, indent=1)
    print(f"✅ 注册表已重建：{REGISTRY_PATH}")
    print(f"   来源包 {len(registry['sources'])} 个；Assembly {len(registry['assemblies'])} / 实体 {len(registry['entities'])} / 其他组件 {len(registry['others'])}")
    print("   ⚠️ 注意：历史归档缺 McsPlugin/McsCustomAPI 等 n8n 包，注册表对这些组件无覆盖 → 模拟时会报 ⚠️ 待确认而非误判 ✅")
    print("   建议：每批交付 IT 的包（含 n8n 产物）原样归档到 Backups/Solutions/Releases/，注册表随批自动变准")


def load_registry():
    if not os.path.exists(REGISTRY_PATH):
        print("⚠️ 注册表不存在，自动构建……")
        build_registry()
    with open(REGISTRY_PATH, encoding="utf-8") as f:
        reg = json.load(f)
    # 基线补充（UAT 镜像种子/人工确认）：只补实体，不补 Assembly（Assembly 是 #1641 事故类，必须靠归档或本批）
    if os.path.exists(BASELINE_PATH):
        with open(BASELINE_PATH, encoding="utf-8") as f:
            extra = json.load(f)
        for name in extra.get("entities", []):
            reg["entities"].setdefault(name.lower(), {"source": "baseline"})
        reg["baseline_entities"] = len(extra.get("entities", []))
    else:
        reg["baseline_entities"] = 0
    return reg


# ==================== 依赖边提取（DEV1 实时，只读） ====================

def collect_edges(api, solution_names):
    """返回 (edges, per_solution_components)
    edges: list of dict(kind, from_pkg, from_name, target_type, target_id/entity, desc)
    """
    edges = []
    for name in solution_names:
        sols = api.get_all("/api/data/v9.2/solutions",
                           {"$filter": f"uniquename eq '{name}'", "$select": "solutionid,friendlyname"})
        if not sols:
            print(f"  ⚠️ DEV1 找不到 Solution {name}，跳过")
            continue
        sid = sols[0]["solutionid"]
        comps = api.get_all("/api/data/v9.2/solutioncomponents",
                            {"$filter": f"_solutionid_value eq {sid} and (componenttype eq 92 or componenttype eq 10023)",
                             "$select": "componenttype,objectid", "$top": "5000"})
        step_ids = [str(c["objectid"]).lower() for c in comps if c["componenttype"] == 92]
        api_ids = [str(c["objectid"]).lower() for c in comps if c["componenttype"] == 10023]
        print(f"  包 {name}: Step {len(step_ids)} / CustomAPI {len(api_ids)}")

        # Step → 实现类 Assembly + 主实体
        for i in range(0, len(step_ids), 50):
            batch = step_ids[i:i + 50]
            cond = " or ".join(f"sdkmessageprocessingstepid eq {g}" for g in batch)
            steps = api.get_all("/api/data/v9.2/sdkmessageprocessingsteps",
                                {"$filter": cond, "$select": "name,_plugintypeid_value,_sdkmessagefilterid_value", "$top": "50"})
            type_ids = {str(s["_plugintypeid_value"]).lower() for s in steps if s.get("_plugintypeid_value")}
            filter_ids = {str(s["_sdkmessagefilterid_value"]).lower() for s in steps if s.get("_sdkmessagefilterid_value")}
            tmap, fmap = {}, {}
            if type_ids:
                cond2 = " or ".join(f"plugintypeid eq {g}" for g in type_ids)
                for t in api.get_all("/api/data/v9.2/plugintypes",
                                     {"$filter": cond2, "$select": "plugintypeid,typename,assemblyname,_pluginassemblyid_value", "$top": "50"}):
                    tmap[str(t["plugintypeid"]).lower()] = t
            if filter_ids:
                cond3 = " or ".join(f"sdkmessagefilterid eq {g}" for g in filter_ids)
                for flt in api.get_all("/api/data/v9.2/sdkmessagefilters",
                                       {"$filter": cond3, "$select": "sdkmessagefilterid,primaryobjecttypecode", "$top": "50"}):
                    fmap[str(flt["sdkmessagefilterid"]).lower()] = flt.get("primaryobjecttypecode")
            for s in steps:
                pt = str(s.get("_plugintypeid_value", "")).lower()
                tinfo = tmap.get(pt)
                if tinfo:
                    edges.append({
                        "kind": "Step→Assembly", "from_pkg": name, "from_name": s.get("name"),
                        "target_type": "assembly",
                        "target_id": str(tinfo.get("_pluginassemblyid_value", "")).lower(),
                        "desc": f"{tinfo.get('typename')}（Assembly: {tinfo.get('assemblyname')}）",
                        "asm_name": tinfo.get("assemblyname") or "",
                    })
                ent = fmap.get(str(s.get("_sdkmessagefilterid_value", "")).lower())
                if ent and ent != "none":
                    edges.append({
                        "kind": "Step→实体", "from_pkg": name, "from_name": s.get("name"),
                        "target_type": "entity", "target_id": ent.lower(), "desc": ent,
                    })

        # CustomAPI → 实现类 Assembly
        for i in range(0, len(api_ids), 50):
            batch = api_ids[i:i + 50]
            cond = " or ".join(f"customapiid eq {g}" for g in batch)
            apis = api.get_all("/api/data/v9.2/customapis",
                               {"$filter": cond, "$select": "uniquename,_plugintypeid_value", "$top": "50"})
            type_ids = {str(a["_plugintypeid_value"]).lower() for a in apis if a.get("_plugintypeid_value")}
            tmap = {}
            if type_ids:
                cond2 = " or ".join(f"plugintypeid eq {g}" for g in type_ids)
                for t in api.get_all("/api/data/v9.2/plugintypes",
                                     {"$filter": cond2, "$select": "plugintypeid,typename,assemblyname,_pluginassemblyid_value", "$top": "50"}):
                    tmap[str(t["plugintypeid"]).lower()] = t
            for a in apis:
                pt = str(a.get("_plugintypeid_value", "")).lower()
                tinfo = tmap.get(pt)
                if not tinfo:
                    continue
                edges.append({
                    "kind": "CustomAPI→Assembly", "from_pkg": name, "from_name": a.get("uniquename"),
                    "target_type": "assembly",
                    "target_id": str(tinfo.get("_pluginassemblyid_value", "")).lower(),
                    "desc": f"{tinfo.get('typename')}（Assembly: {tinfo.get('assemblyname')}）",
                    "asm_name": tinfo.get("assemblyname") or "",
                })
    return edges


def batch_assemblies(api, solution_names):
    """本批各包包含的 Assembly GUID → 包名（用于‘先发包已带’判定）。"""
    asm_owner = {}
    for name in solution_names:
        sols = api.get_all("/api/data/v9.2/solutions",
                           {"$filter": f"uniquename eq '{name}'", "$select": "solutionid"})
        if not sols:
            continue
        sid = sols[0]["solutionid"]
        comps = api.get_all("/api/data/v9.2/solutioncomponents",
                            {"$filter": f"_solutionid_value eq {sid} and componenttype eq 91",
                             "$select": "objectid", "$top": "100"})
        for c in comps:
            asm_owner[str(c["objectid"]).lower()] = name
    return asm_owner


def batch_entities(api, solution_names):
    """本批各包包含的实体（componenttype=1，objectid=MetadataId）→ LogicalName 集合。"""
    meta_ids = set()
    for name in solution_names:
        sols = api.get_all("/api/data/v9.2/solutions",
                           {"$filter": f"uniquename eq '{name}'", "$select": "solutionid"})
        if not sols:
            continue
        sid = sols[0]["solutionid"]
        comps = api.get_all("/api/data/v9.2/solutioncomponents",
                            {"$filter": f"_solutionid_value eq {sid} and componenttype eq 1",
                             "$select": "objectid", "$top": "500"})
        for c in comps:
            meta_ids.add(str(c["objectid"]).lower())
    names = set()
    ids = list(meta_ids)
    for i in range(0, len(ids), 50):
        cond = " or ".join(f"MetadataId eq {g}" for g in ids[i:i + 50])
        for e in api.get_all("/api/data/v9.2/EntityDefinitions",
                             {"$filter": cond, "$select": "LogicalName"}):
            names.add(e["LogicalName"].lower())
    return names


def seed_baseline_from_uat():
    """把 UAT 全部实体逻辑名写入基线文件（他方已上线模块与生产同基线；Assembly 不种子）。
    必须剔除我方实体（主清单 AllComponent_Peter_NoUAT 成员）——我方模块 8/30 才上线，
    不剔除会造成「本批忘带实体包也误绿」的假阳性。"""
    api = WebApi(UAT, get_token(UAT))
    names = set()
    for e in api.get_all("/api/data/v9.2/EntityDefinitions", {"$select": "LogicalName", "$filter": "IsCustomEntity eq true"}):
        names.add(e["LogicalName"].lower())
    # 我方实体 = 主清单成员，从基线剔除
    dev = WebApi(DEV1, get_token(DEV1))
    ours = batch_entities(dev, ["AllComponent_Peter_NoUAT"])
    names -= ours
    data = {"_note": "UAT 镜像实体基线（seed-from-uat 生成，已剔除主清单我方实体）+ 人工确认补充；Assembly 不入此文件（必须靠归档或本批同发）",
            "seeded_at": __import__("datetime").datetime.now().isoformat(timespec="seconds"),
            "entities": sorted(names)}
    with open(BASELINE_PATH, "w", encoding="utf-8") as f:
        json.dump(data, f, ensure_ascii=False, indent=1)
    print(f"✅ 基线已生成：{BASELINE_PATH}（实体 {len(names)} 个，已剔除我方实体 {len(ours)} 个）")


# ==================== 目标环境判定 ====================

class TargetState:
    def assembly_exists(self, asm_id):
        raise NotImplementedError

    def entity_exists(self, logical_name):
        raise NotImplementedError


class UatTarget(TargetState):
    def __init__(self, api):
        self.api = api
        self._asm_cache = {}
        self._ent_cache = {}

    def assembly_exists(self, asm_id):
        if asm_id not in self._asm_cache:
            r = self.api.get_all("/api/data/v9.2/pluginassemblies",
                                 {"$filter": f"pluginassemblyid eq {asm_id}", "$select": "pluginassemblyid", "$top": "1"})
            self._asm_cache[asm_id] = len(r) > 0
        return self._asm_cache[asm_id]

    def entity_exists(self, logical_name):
        if "_" not in logical_name:      # 无前缀 = 平台内置实体，免查
            return True
        if logical_name not in self._ent_cache:
            r = self.api.get_all("/api/data/v9.2/EntityDefinitions",
                                 {"$filter": f"LogicalName eq '{logical_name}'", "$select": "LogicalName"})
            self._ent_cache[logical_name] = len(r) > 0
        return self._ent_cache[logical_name]


class ProdRegistryTarget(TargetState):
    def __init__(self, registry):
        self.registry = registry

    def assembly_exists(self, asm_id):
        return asm_id in self.registry["assemblies"]

    def entity_exists(self, logical_name):
        if "_" not in logical_name:      # 无前缀 = 平台内置实体
            return True
        return logical_name in self.registry["entities"]


# ==================== 主流程 ====================

def main():
    args = sys.argv[1:]
    if args and args[0] == "build-registry":
        build_registry()
        return
    if args and args[0] == "seed-from-uat":
        seed_baseline_from_uat()
        return

    target = "uat"
    batch = None
    i = 0
    while i < len(args):
        if args[i] == "--target":
            target = args[i + 1]; i += 2
        elif args[i] == "--batch":
            batch = []
            i += 1
            while i < len(args) and not args[i].startswith("--"):
                batch.append(args[i]); i += 1
        else:
            i += 1
    batch = batch or DEFAULT_BATCH

    print("═══ 离线导入仿真（预热）═══")
    print(f"目标环境: {target}    批次(按导入顺序): {' → '.join(batch)}")

    print("\n[1/3] 从 DEV1 提取包内容与依赖边（只读）……")
    dev = WebApi(DEV1, get_token(DEV1))
    edges = collect_edges(dev, batch)
    asm_owner = batch_assemblies(dev, batch)
    ent_in_batch = batch_entities(dev, batch)
    print(f"  依赖边共 {len(edges)} 条；本批自带 Assembly {len(asm_owner)} 个 / 实体 {len(ent_in_batch)} 个")

    print(f"\n[2/3] 加载目标环境状态（{target}）……")
    if target == "uat":
        tstate = UatTarget(WebApi(UAT, get_token(UAT)))
    else:
        tstate = ProdRegistryTarget(load_registry())
        print(f"  注册表：Assembly {len(tstate.registry['assemblies'])} / 实体 {len(tstate.registry['entities'])}（来源包 {len(tstate.registry['sources'])} 个）")

    print("\n[3/3] 逐边判定闭合性……")
    ok, danger, warn = [], [], []
    for e in edges:
        if e["target_type"] == "assembly":
            if e["target_id"] in asm_owner:
                ok.append((e, f"随本批包 {asm_owner[e['target_id']]} 同发")); continue
            if tstate.assembly_exists(e["target_id"]):
                ok.append((e, "目标环境已有")); continue
            if e.get("asm_name", "").startswith("Microsoft."):
                ok.append((e, "第一方程序集，各环境内置")); continue
            if e.get("asm_name", "").startswith("SanyD365."):
                if isinstance(tstate, UatTarget):
                    danger.append((e, "我方程序集，既不在本批也未发到目标环境"))  # UAT 在线判定，精确
                else:
                    warn.append((e, "【重点确认】我方程序集，生产台账未覆盖（台账不完整期间宁误报不漏报）"))
                continue
            warn.append((e, "他方程序集，台账未覆盖，需确认目标环境是否存在"))
        else:  # entity
            if e["target_id"] in ent_in_batch:
                ok.append((e, "随本批实体包同发")); continue
            if tstate.entity_exists(e["target_id"]):
                ok.append((e, "目标环境已有/基线" if "_" in e["target_id"] else BUILTIN_ENTITY_NOTE)); continue
            warn.append((e, "实体不在台账（他方实体或未归档），需确认"))

    print(f"\n═══ 结果：✅ {len(ok)} / 🚨 {len(danger)} / ⚠️ {len(warn)} ═══")

    def dedupe(items):
        """按依赖目标聚合，避免同一实体/程序集被几十条 Step 重复刷屏。"""
        agg = {}
        for e, why in items:
            key = (e["target_type"], e["target_id"])
            if key not in agg:
                agg[key] = {"edge": e, "why": why, "count": 0, "samples": []}
            agg[key]["count"] += 1
            if len(agg[key]["samples"]) < 2:
                agg[key]["samples"].append(f"{e['from_pkg']}|{e['from_name']}")
        return sorted(agg.values(), key=lambda x: -x["count"])

    if danger:
        print(f"\n🚨 缺口（去重后 {len(dedupe(danger))} 个目标，导入必被拦，#1641 同款）：")
        for g in dedupe(danger):
            e = g["edge"]
            print(f"  {e['kind']} → {e['desc']}（{g['count']} 条引用，如 {g['samples'][0]}）")
            print(f"      → {g['why']}")
    if warn:
        print(f"\n⚠️ 待确认（去重后 {len(dedupe(warn))} 个目标）：")
        for g in dedupe(warn):
            e = g["edge"]
            print(f"  {e['kind']} → {e['desc']}（{g['count']} 条引用，如 {g['samples'][0]}）")
    if not danger and not warn:
        print("  全部依赖闭合，可以移交导入。")
    if danger:
        sys.exit(2)


if __name__ == "__main__":
    main()
