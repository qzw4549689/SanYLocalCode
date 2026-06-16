"""
测试禅道登录 - 支持手机号登录
禅道手机号登录时，account字段直接填手机号即可
"""
import requests
import json
import getpass

BASE_URL = "https://peterqiu.chandao.net"
API_URL = f"{BASE_URL}/api.php/v1"

print("=" * 50)
print("禅道 API 登录测试")
print("=" * 50)
print(f"禅道地址: {BASE_URL}")
print()

# 输入手机号和密码
account = input("请输入手机号: ").strip()
password = getpass.getpass("请输入密码: ").strip()

print(f"\n尝试登录中...")

# 调用禅道 Token API
url = f"{API_URL}/tokens"
payload = {
    "account": account,
    "password": password
}

try:
    response = requests.post(url, json=payload, timeout=15)
    print(f"HTTP状态码: {response.status_code}")
    
    try:
        data = response.json()
    except:
        data = {"raw": response.text}
    
    if response.status_code in [200, 201] and data.get("token"):
        token = data["token"]
        print(f"\n✅ 登录成功!")
        print(f"Token: {token[:30]}...")
        
        # 测试查询产品列表
        headers = {"Authorization": f"Bearer {token}"}
        
        print("\n--- 查询产品列表 ---")
        products_resp = requests.get(f"{API_URL}/products", headers=headers, timeout=10)
        print(f"状态码: {products_resp.status_code}")
        if products_resp.status_code == 200:
            products_data = products_resp.json()
            products = products_data.get("products", [])
            print(f"产品数量: {len(products)}")
            for p in products:
                print(f"  - ID={p.get('id')}, 名称={p.get('name')}, 代号={p.get('code')}")
        
        # 查询项目列表
        print("\n--- 查询项目列表 ---")
        projects_resp = requests.get(f"{API_URL}/projects", headers=headers, timeout=10)
        print(f"状态码: {projects_resp.status_code}")
        if projects_resp.status_code == 200:
            projects_data = projects_resp.json()
            projects = projects_data.get("projects", [])
            print(f"项目数量: {len(projects)}")
            for p in projects:
                print(f"  - ID={p.get('id')}, 名称={p.get('name')}")
        
        # 查询执行列表
        print("\n--- 查询执行/迭代列表 ---")
        exec_resp = requests.get(f"{API_URL}/executions", headers=headers, timeout=10)
        print(f"状态码: {exec_resp.status_code}")
        if exec_resp.status_code == 200:
            exec_data = exec_resp.json()
            executions = exec_data.get("executions", [])
            print(f"执行数量: {len(executions)}")
            for e in executions:
                print(f"  - ID={e.get('id')}, 名称={e.get('name')}, 项目ID={e.get('project')}")
        
        print("\n" + "=" * 50)
        print("登录测试完成，请记录上面的产品ID和执行ID")
        print("=" * 50)
        
    else:
        print(f"\n❌ 登录失败")
        print(f"响应: {json.dumps(data, indent=2, ensure_ascii=False)[:500]}")
        
except requests.exceptions.Timeout:
    print("❌ 请求超时，请检查网络连接")
except requests.exceptions.ConnectionError:
    print("❌ 连接失败，请检查禅道地址是否正确")
except Exception as e:
    print(f"❌ 错误: {e}")
