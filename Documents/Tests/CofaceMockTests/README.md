# Coface 汇率配置化 Mock 测试

## 用途
验证 F6.8 汇率表配置化改造后，`Urba360Parser.ConvertCurrency` 对非 USD 货币的转换路径是否正确。

## 运行方式

```bash
cd /tmp/coface-mock-test
dotnet run
```

项目文件 `coface-mock-test.csproj` 为临时构建文件，保留在 `/tmp` 下；核心测试代码已复制到本目录。

## 测试覆盖

| 货币 | 输入 | 预期 | 结果 |
|------|------|------|------|
| EUR | 1,000 | 1,084.00 USD | ✅ |
| CNY | 10,000 | 1,375.00 USD | ✅ |
| PLN | 1,000 | 260.00 USD | ✅ |
| USD | 1,000 | 1,000.00 USD | ✅ |
| XYZ | 1,000 | 1,000.00 USD（未配置，保持原值） | ✅ |

## 关键实现

- 通过反射加载本地 Plugin DLL：`SanyD365.Plugins.CofaceIntegration.dll`
- 使用 `MockOrganizationService` 模拟 `mcs_coface_exchange_rate` 查询
- 直接调用私有方法 `ConvertCurrency(JsonElement, decimal)`

## 执行时间

2026-06-13

## Coface NACE 行业映射配置化 Mock 测试

### 用途
验证 F6.x NACE 行业映射配置化改造后，`Urba360Parser.ParseNaceCodes` 是否正确从配置表读取映射。

### 运行方式

```bash
cd /tmp/coface-nace-mock-test
dotnet run
```

### 测试覆盖

| NACE Codes | 预期结果 | 结果 |
|---|---|---|
| 0111 | 农业 | ✅ |
| 0210 | 林业 | ✅ |
| 0510, 0710 | 矿业 | ✅ |
| 1011, 2910 | 制造业 | ✅ |
| 2311 | 商混（精确匹配优先于 10-33 制造业） | ✅ |
| 4110, 4210 | 建工 | ✅ |
| 4310 | 吊装 | ✅ |
| 4910, 5010, 5110 | 集装箱运力 | ✅ |
| 5210 | 港务 | ✅ |
| 7710 | 租赁 | ✅ |
| 9910 | O（未配置） | ✅ |
| 0111, 2910 | 农业,制造业（多行业去重拼接） | ✅ |

### 执行时间

2026-06-13
