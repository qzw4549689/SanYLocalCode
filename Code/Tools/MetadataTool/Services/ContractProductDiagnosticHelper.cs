using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Xml;

namespace D365MetadataTool;

/// <summary>
/// 诊断客户画像"已签待执行合同"产品名称取数问题
/// </summary>
public class ContractProductDiagnosticHelper
{
    private readonly ServiceClient _service;

    public ContractProductDiagnosticHelper(ServiceClient service)
    {
        _service = service;
    }

    public void DiagnoseByAccountId(string accountId)
    {
        Console.WriteLine($"\n=== 诊断合同产品明细: accountId={accountId} ===\n");

        // 1. 查询该客户作为buyer的合同
        var contractQuery = new QueryExpression("mcs_contract")
        {
            ColumnSet = new ColumnSet("mcs_contractid", "mcs_name", "mcs_contractnumber", "mcs_contractstatus", "mcs_contractbuyer", "mcs_customermaster",
                "mcs_advanceratio", "mcs_remainingpaymentdays", "mcs_riskcovervalue", "mcs_paymentterms", "mcs_paymenttermunit",
                "mcs_riskexposureamount", "mcs_riskcover"),
            Criteria = new FilterExpression
            {
                FilterOperator = LogicalOperator.Or,
                Conditions =
                {
                    new ConditionExpression("mcs_contractbuyer", ConditionOperator.Equal, new Guid(accountId)),
                    new ConditionExpression("mcs_customermaster", ConditionOperator.Equal, new Guid(accountId))
                }
            }
        };
        var contracts = _service.RetrieveMultiple(contractQuery).Entities;
        Console.WriteLine($"找到 {contracts.Count} 条关联合同（mcs_contractbuyer 或 mcs_customermaster）");

        if (contracts.Count == 0)
        {
            Console.WriteLine("❌ 未找到关联合同，产品名称自然为空");
            return;
        }

        foreach (var c in contracts)
        {
            var cid = c.Id;
            var name = c.GetAttributeValue<string>("mcs_name") ?? "(无)";
            var status = c.GetAttributeValue<OptionSetValue>("mcs_contractstatus")?.Value.ToString() ?? "(无)";
            var advanceRatio = c.GetAttributeValue<decimal?>("mcs_advanceratio");
            var remainingDays = c.GetAttributeValue<OptionSetValue>("mcs_remainingpaymentdays")?.Value;
            var riskValue = c.GetAttributeValue<decimal?>("mcs_riskcovervalue");
            var paymentTerms = c.GetAttributeValue<string>("mcs_paymentterms");
            var paymentTermUnit = c.GetAttributeValue<OptionSetValue>("mcs_paymenttermunit")?.Value;
            Console.WriteLine($"\n合同: {name}, 状态={status}, ID={cid}");
            Console.WriteLine($"  首付款比例(mcs_advanceratio): {advanceRatio?.ToString() ?? "(空)"}");
            Console.WriteLine($"  账期天数选项(mcs_remainingpaymentdays): {remainingDays?.ToString() ?? "(空)"}");
            Console.WriteLine($"  风险敞口率(mcs_riskcovervalue): {riskValue?.ToString() ?? "(空)"}");
            Console.WriteLine($"  付款条款文本(mcs_paymentterms): {paymentTerms ?? "(空)"}");
            Console.WriteLine($"  付款期限单位(mcs_paymenttermunit): {paymentTermUnit?.ToString() ?? "(空)"}");
            var riskExposureAmount = c.GetAttributeValue<Money>("mcs_riskexposureamount")?.Value;
            var riskCover = c.GetAttributeValue<bool?>("mcs_riskcover");
            Console.WriteLine($"  风险敞口额(mcs_riskexposureamount): {riskExposureAmount?.ToString() ?? "(空)"}");
            Console.WriteLine($"  是否覆盖风险(mcs_riskcover): {riskCover?.ToString() ?? "(空)"}");

            // 2. 使用 FetchXML 查询合同明细（与画像页面一致）
            var fetchXml = $@"
<fetch>
  <entity name=""mcs_contractdetail"">
    <attribute name=""mcs_contractdetailid"" />
    <attribute name=""mcs_product"" />
    <attribute name=""mcs_productnamecn"" />
    <attribute name=""mcs_productnamenew"" />
    <attribute name=""mcs_productmodel"" />
    <attribute name=""mcs_productcode"" />
    <attribute name=""mcs_contractdetailtype"" />
    <attribute name=""mcs_name"" />
    <filter>
      <condition attribute=""mcs_contract"" operator=""eq"" value=""{cid}"" />
    </filter>
  </entity>
</fetch>";

            var details = _service.RetrieveMultiple(new FetchExpression(fetchXml)).Entities;
            Console.WriteLine($"  合同明细数: {details.Count}");

            foreach (var d in details)
            {
                var productRef = d.GetAttributeValue<EntityReference>("mcs_product");
                var productNameCn = d.GetAttributeValue<string>("mcs_productnamecn");
                var productNameNew = d.GetAttributeValue<string>("mcs_productnamenew");
                var productModel = d.GetAttributeValue<string>("mcs_productmodel");
                var productCode = d.GetAttributeValue<string>("mcs_productcode");
                // 与画像页面一致的优先级：mcs_productnamenew → mcs_productnamecn → mcs_productmodel
                var displayName = productNameNew ?? productNameCn ?? productModel ?? "(无可用名称)";

                Console.WriteLine($"    明细ID: {d.Id}");
                Console.WriteLine($"      mcs_product (Lookup): {productRef?.Id ?? Guid.Empty} / name={productRef?.Name}");
                Console.WriteLine($"      mcs_productnamecn: {productNameCn ?? "(空)"}");
                Console.WriteLine($"      mcs_productnamenew: {productNameNew ?? "(空)"}");
                Console.WriteLine($"      mcs_productmodel: {productModel ?? "(空)"}");
                Console.WriteLine($"      mcs_productcode: {productCode ?? "(空)"}");
                Console.WriteLine($"      → 画像页面将展示: {displayName}");
            }
        }

        Console.WriteLine("\n=== 完成 ===");
    }

    /// <summary>
    /// 为测试目的，给指定客户下的已签待执行合同赋测试值：
    /// - mcs_remainingpaymentdays = 90
    /// - mcs_riskcovervalue = 10.5
    /// </summary>
    public void SetTestValuesByAccountId(string accountId)
    {
        Console.WriteLine($"\n=== 为合同设置测试值: accountId={accountId} ===\n");

        var contractQuery = new QueryExpression("mcs_contract")
        {
            ColumnSet = new ColumnSet("mcs_contractid", "mcs_name"),
            Criteria = new FilterExpression
            {
                FilterOperator = LogicalOperator.Or,
                Conditions =
                {
                    new ConditionExpression("mcs_contractbuyer", ConditionOperator.Equal, new Guid(accountId)),
                    new ConditionExpression("mcs_customermaster", ConditionOperator.Equal, new Guid(accountId))
                }
            }
        };
        var contracts = _service.RetrieveMultiple(contractQuery).Entities;
        Console.WriteLine($"找到 {contracts.Count} 条关联合同");

        var dayOptions = new[] { 30, 60, 90, 120, 150, 180, 210, 270, 360, 420, 450, 540, 720, 900, 1080 };
        var riskValues = new[] { 5.0m, 8.5m, 10.5m, 15.0m, 20.0m, 25.0m, 30.0m, 35.0m, 40.0m, 45.0m, 50.0m, 55.0m, 60.0m, 65.0m, 70.0m };

        for (int i = 0; i < contracts.Count; i++)
        {
            var c = contracts[i];
            var days = dayOptions[i % dayOptions.Length];
            var risk = riskValues[i % riskValues.Length];
            var update = new Entity("mcs_contract", c.Id);
            update["mcs_remainingpaymentdays"] = new OptionSetValue(days);
            update["mcs_riskcovervalue"] = risk;
            _service.Update(update);
            Console.WriteLine($"  已设置: {c.GetAttributeValue<string>("mcs_name") ?? "(无)"} -> 账期={days}天, 风险敞口率={risk}%");
        }

        Console.WriteLine("\n=== 完成 ===");
    }
}
