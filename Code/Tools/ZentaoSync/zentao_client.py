"""
禅道 API 客户端
支持：登录认证、创建Bug、创建任务、创建测试用例、查询产品/项目列表
API版本：RESTful API v1（禅道开源版16.5+）
"""

import requests
import json
from urllib.parse import urljoin


class ZentaoClient:
    def __init__(self, base_url, account, password):
        """
        初始化禅道客户端
        
        Args:
            base_url: 禅道地址，如 https://peterqiu.chandao.net
            account: 用户名
            password: 密码
        """
        self.base_url = base_url.rstrip('/')
        self.api_url = f"{self.base_url}/api.php/v1"
        self.account = account
        self.password = password
        self.token = None
        self.headers = {}
    
    def login(self):
        """登录获取Token"""
        url = f"{self.api_url}/tokens"
        payload = {
            "account": self.account,
            "password": self.password
        }
        response = requests.post(url, json=payload)
        response.raise_for_status()
        data = response.json()
        self.token = data.get("token")
        self.headers = {
            "Token": self.token,
            "Content-Type": "application/json"
        }
        print(f"✅ 登录成功，Token: {self.token[:20]}...")
        return self.token
    
    def get_products(self):
        """获取产品列表"""
        url = f"{self.api_url}/products"
        response = requests.get(url, headers=self.headers)
        response.raise_for_status()
        return response.json()
    
    def get_projects(self):
        """获取项目列表"""
        url = f"{self.api_url}/projects"
        response = requests.get(url, headers=self.headers)
        response.raise_for_status()
        return response.json()
    
    def get_executions(self, project_id=None):
        """获取执行/迭代列表"""
        if project_id:
            url = f"{self.api_url}/projects/{project_id}/executions"
        else:
            url = f"{self.api_url}/executions"
        response = requests.get(url, headers=self.headers)
        response.raise_for_status()
        return response.json()
    
    def create_bug(self, product_id, bug_data):
        """
        创建Bug
        
        Args:
            product_id: 产品ID
            bug_data: {
                "title": "Bug标题",
                "openedBuild": "trunk",
                "severity": "3",  # 1-严重, 2-高, 3-中, 4-低
                "pri": "2",       # 优先级 1-4
                "type": "bug",    # bug/codeerror/design...
                "steps": "复现步骤",
                "assignedTo": ""
            }
        """
        url = f"{self.api_url}/products/{product_id}/bugs"
        response = requests.post(url, headers=self.headers, json=bug_data)
        response.raise_for_status()
        return response.json()
    
    def create_task(self, execution_id, task_data):
        """
        创建任务
        
        Args:
            execution_id: 执行/迭代ID
            task_data: {
                "name": "任务名称",
                "type": "devel",  # devel/design/test/study...
                "pri": "1",       # 优先级 1-4
                "assignedTo": "",
                "estStarted": "2026-06-06",
                "deadline": "2026-06-13",
                "desc": "任务描述"
            }
        """
        url = f"{self.api_url}/executions/{execution_id}/tasks"
        response = requests.post(url, headers=self.headers, json=task_data)
        response.raise_for_status()
        return response.json()
    
    def create_case(self, product_id, case_data):
        """
        创建测试用例
        
        Args:
            product_id: 产品ID
            case_data: {
                "title": "用例标题",
                "type": "feature",  # feature/performance/security...
                "pri": "2",
                "precondition": "前置条件",
                "steps": [
                    {"step": "步骤1", "expect": "预期结果1"},
                    {"step": "步骤2", "expect": "预期结果2"}
                ]
            }
        """
        url = f"{self.api_url}/products/{product_id}/cases"
        response = requests.post(url, headers=self.headers, json=case_data)
        response.raise_for_status()
        return response.json()
    
    def get_product_bugs(self, product_id, page=1, limit=100):
        """获取产品的Bug列表"""
        url = f"{self.api_url}/products/{product_id}/bugs"
        params = {"page": page, "limit": limit}
        response = requests.get(url, headers=self.headers, params=params)
        response.raise_for_status()
        return response.json()
    
    def get_execution_tasks(self, execution_id, page=1, limit=100):
        """获取执行的任务列表"""
        url = f"{self.api_url}/executions/{execution_id}/tasks"
        params = {"page": page, "limit": limit}
        response = requests.get(url, headers=self.headers, params=params)
        response.raise_for_status()
        return response.json()

    # ========== Bug 读取/解决（2026-07-30 新增） ==========

    def get_my_profile(self):
        """获取当前登录用户信息（含 account）"""
        url = f"{self.api_url}/user"
        response = requests.get(url, headers=self.headers)
        response.raise_for_status()
        return response.json()

    def get_bug(self, bug_id):
        """获取单个 Bug 详情"""
        url = f"{self.api_url}/bugs/{bug_id}"
        response = requests.get(url, headers=self.headers)
        response.raise_for_status()
        return response.json()

    def get_my_bugs(self, product_ids=None, status="active", max_pages=5, limit=100):
        """
        获取指派给我的 Bug 列表。
        优先尝试 /my/bugs 接口（部分版本支持）；不支持时遍历产品 Bug 列表按 assignedTo 过滤。

        Args:
            product_ids: 指定产品 ID 列表；None 时自动取全部产品
            status: 过滤状态，默认 active（未解决）；传 None 或 'all' 不过滤
        Returns:
            list: Bug 字典列表
        """
        # 方案 1：/my/bugs（禅道 18+ 支持）
        try:
            url = f"{self.api_url}/my/bugs"
            params = {"limit": limit}
            if status and status != "all":
                params["status"] = status
            response = requests.get(url, headers=self.headers, params=params)
            if response.status_code == 200:
                data = response.json()
                bugs = data.get("bugs", data) if isinstance(data, dict) else data
                if isinstance(bugs, list):
                    return self._filter_bugs(bugs, status)
        except Exception:
            pass

        # 方案 2：遍历产品 Bug 列表按 assignedTo 过滤
        if product_ids is None:
            products = self.get_products()
            product_list = products.get("products", products) if isinstance(products, dict) else products
            product_ids = [p["id"] for p in product_list]

        my_bugs = []
        for pid in product_ids:
            for page in range(1, max_pages + 1):
                data = self.get_product_bugs(pid, page=page, limit=limit)
                bugs = data.get("bugs", []) if isinstance(data, dict) else data
                if not bugs:
                    break
                for bug in bugs:
                    assigned = bug.get("assignedTo")
                    account = assigned.get("account") if isinstance(assigned, dict) else assigned
                    if account == self.account:
                        my_bugs.append(bug)
                if len(bugs) < limit:
                    break
        return self._filter_bugs(my_bugs, status)

    @staticmethod
    def _filter_bugs(bugs, status):
        if not status or status == "all":
            return bugs
        return [b for b in bugs if str(b.get("status")) == status]

    def resolve_bug(self, bug_id, resolution="fixed", resolved_build="trunk",
                    assigned_to=None, comment=None):
        """
        标记 Bug 为已解决。

        Args:
            bug_id: Bug ID
            resolution: 解决方案，默认 fixed（已修复）。其他：notrepro/wontfix/bydesign/duplicate 等
            resolved_build: 解决版本，默认 trunk
            assigned_to: 解决后指派人账号（通常为 Bug 提出人，用于验证关闭）
            comment: 处理结果说明
        """
        url = f"{self.api_url}/bugs/{bug_id}/resolve"
        payload = {"resolution": resolution, "resolvedBuild": resolved_build}
        if assigned_to:
            payload["assignedTo"] = assigned_to
        if comment:
            payload["comment"] = comment
        response = requests.post(url, headers=self.headers, json=payload)
        response.raise_for_status()
        return response.json()


if __name__ == "__main__":
    # 示例用法
    client = ZentaoClient(
        base_url="https://peterqiu.chandao.net",
        account="your_username",
        password="your_password"
    )
    client.login()
    
    # 查询产品列表
    products = client.get_products()
    print(f"\n产品列表: {json.dumps(products, indent=2, ensure_ascii=False)[:1000]}")
