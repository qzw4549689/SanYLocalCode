"""
禅道 Web 页面客户端（2026-07-30 新增）
背景：公司禅道（开源版16.0 定制部署）REST API 路由不支持 query string，
导致产品 Bug 列表无法翻页、无 /my/bugs 接口。因此「我的待处理 Bug 列表」
改用 Web 会话 + 页面解析实现；Bug 详情/解决仍走 REST API（zentao_client.py）。
"""
import re
import hashlib
import requests
from html import unescape


class ZentaoWebClient:
    def __init__(self, base_url, account, password):
        self.base_url = base_url.rstrip('/')
        self.account = account
        self.password = password
        self.session = requests.Session()
        self.headers = {
            'X-Requested-With': 'XMLHttpRequest',
            'Accept': 'application/json, text/javascript, */*; q=0.01',
            'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36',
        }

    def login(self):
        """Web 表单登录：refreshRandom 拿随机数，密码 md5(md5(pwd)+rand) 提交"""
        login_url = f"{self.base_url}/user-login.html"
        self.headers['Referer'] = login_url
        r = self.session.get(f"{self.base_url}/user-refreshrandom.html",
                             headers=self.headers, timeout=15)
        r.raise_for_status()
        rand = r.text.strip()
        pwd_md5 = hashlib.md5(self.password.encode()).hexdigest()
        pwd = hashlib.md5((pwd_md5 + rand).encode()).hexdigest()
        r2 = self.session.post(login_url, headers=self.headers, data={
            'account': self.account, 'password': pwd, 'passwordStrength': '3',
            'referer': '', 'verifyRand': rand, 'keepLogin': '0', 'captcha': '',
        }, timeout=15)
        r2.raise_for_status()
        if '"result":"success"' not in r2.text:
            raise RuntimeError(f"Web 登录失败: {r2.text[:200]}")
        print("✅ Web 登录成功")

    def get_my_bugs(self, browse_type="assignedTo"):
        """
        读取「地盘-我的Bug」列表（默认 assignedTo=指派给我=待处理）。
        自动翻页。返回 list[dict]：id/severity/pri/confirmed/title/product/type/openedBy/url
        """
        bugs = []
        page = 1
        while True:
            if page == 1:
                url = f"{self.base_url}/my-bug.html"
            else:
                url = f"{self.base_url}/my-bug-{browse_type}-id_desc-{page}-20-1.html"
            r = self.session.get(url, headers=self.headers, timeout=15)
            r.raise_for_status()
            page_bugs = self._parse_bug_rows(r.text)
            bugs.extend(page_bugs)
            # 检查是否还有下一页
            if not re.search(
                    rf"/my-bug-(?:bug-)?{browse_type}-id_desc-{page + 1}-20-1\.html",
                    r.text):
                break
            page += 1
        return bugs

    def resolve_bug(self, bug_id, resolution="fixed", resolved_build="trunk",
                    assigned_to=None, comment=""):
        """
        通过 Web 表单标记 Bug 已解决（16.0 REST API 无 resolve 动作端点）。

        Args:
            bug_id: Bug ID
            resolution: 解决方案，默认 fixed（已解决）
            resolved_build: 解决版本（版本 ID 或 trunk），默认取 Bug 影响版本
            assigned_to: 解决后指派人账号；None 时用表单默认（即提出人）
            comment: 处理结果说明
        """
        form_url = f"{self.base_url}/bug-resolve-{bug_id}.html"
        r = self.session.get(form_url, headers={
            'Referer': f"{self.base_url}/my-bug.html",
            'User-Agent': self.headers['User-Agent'],
        }, timeout=15)
        r.raise_for_status()
        html = r.text
        if "bug-resolve" not in html and "解决方案" not in html:
            raise RuntimeError("无法打开解决表单（Bug 不存在或状态不允许解决）")

        # 提取表单默认值
        if not assigned_to:
            m = re.search(r"name='assignedTo'.*?</select>", html, re.S)
            sel = re.search(r"<option value='([^']+)' selected='selected'",
                            m.group(0)) if m else None
            assigned_to = sel.group(1) if sel else ""
        m = re.search(r"name='resolvedDate'[^>]*value='([^']*)'", html)
        resolved_date = m.group(1) if m else ""
        if not resolved_build:
            # 默认取影响版本同款解决版本列表中与 openedBuild 同 id 的项，退化为 trunk
            m = re.search(r"name='openedBuild'.*?</select>", html, re.S)
            opened = re.search(r"<option value='([^']+)' selected='selected'",
                               m.group(0)) if m else None
            resolved_build = opened.group(1) if opened and opened.group(1) else "trunk"

        data = {
            'resolution': resolution,
            'duplicateBug': '',
            'resolvedBuild': resolved_build,
            'buildName': '',
            'createBuild': '',
            'resolvedDate': resolved_date,
            'assignedTo': assigned_to,
            'status': 'resolved',
            'comment': comment,
        }
        r2 = self.session.post(form_url, data=data, files={'files[]': (None, '')},
                               headers={
                                   'Referer': form_url,
                                   'User-Agent': self.headers['User-Agent'],
                               }, timeout=15)
        r2.raise_for_status()
        # 失败时响应包含 alert/错误提示；成功时 hiddenwin 输出跳转脚本
        if 'alert(' in r2.text and 'self.location' not in r2.text:
            m = re.search(r"alert\(['\"]?(.*?)['\"]?\)", r2.text)
            raise RuntimeError(f"解决失败: {m.group(1) if m else r2.text[:200]}")
        return {"assignedTo": assigned_to, "resolvedBuild": resolved_build}

    @staticmethod
    def _parse_bug_rows(html):
        """解析 my-bug 列表页表格行"""
        rows = []
        tbody = re.search(r"<tbody>(.*?)</tbody>", html, re.S)
        if not tbody:
            return rows
        for tr in re.findall(r"<tr>(.*?)</tr>", tbody.group(1), re.S):
            m_id = re.search(r"name='bugIDList\[\]' value='(\d+)'", tr)
            m_title = re.search(
                r"<a href='(/bug-view-\d+\.html)'[^>]*title=([^>]*)>(.*?)</a>", tr, re.S)
            if not m_id or not m_title:
                continue
            sev = re.search(r"data-severity='(\d+)'[^>]*>([^<]+)</span>", tr)
            pri = re.search(r"label-pri-\d+' title='([^']+)'", tr)
            confirmed = re.search(r"class='(?:un)?confirmed' title='([^']+)'", tr)
            product = re.search(r"<a href='/product-all\.html'[^>]*>([^<]*)</a>", tr)
            # 纯文本单元格依次：类型/提出人/解决人/方案（产品、级别等含嵌套标签不匹配）
            cells = [unescape(t).strip()
                     for t in re.findall(r"<td[^>]*>([^<]*)</td>", tr)]
            rows.append({
                "id": int(m_id.group(1)),
                "severity": sev.group(2) if sev else "",
                "pri": pri.group(1) if pri else "",
                "confirmed": confirmed.group(1) if confirmed else "",
                "title": unescape(re.sub(r"<[^>]+>", "", m_title.group(3))).strip(),
                "url": m_title.group(1),
                "product": unescape(product.group(1)).strip() if product else "",
                "type": cells[0] if len(cells) > 0 else "",
                "openedBy": cells[1] if len(cells) > 1 else "",
            })
        return rows
