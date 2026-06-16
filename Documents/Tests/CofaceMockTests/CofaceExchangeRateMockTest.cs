using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

class Program
{
    static void Main()
    {
        var assembly = Assembly.LoadFrom("/Users/peterqiu/Work/AIWorkSpace/SanYi/Code/Customizations/Plugins/CofaceIntegration/bin/Release/net462/SanyD365.Plugins.CofaceIntegration.dll");
        Console.WriteLine($"Loaded: {assembly.FullName}");

        var tracer = new ConsoleTracer();
        var service = new MockOrganizationService();
        var parserType = assembly.GetType("SanyD365.Plugins.CofaceIntegration.Parser.Urba360Parser");
        Console.WriteLine($"Type found: {parserType != null}");

        // 正确的构造函数签名：(ITracingService, IOrganizationService, String)
        var parser = Activator.CreateInstance(parserType, tracer, service, "mock-test");
        var method = parserType.GetMethod("ConvertCurrency", BindingFlags.NonPublic | BindingFlags.Instance);
        Console.WriteLine($"Method found: {method != null}");

        // 2026 Budget rates (LC -> USD)
        TestCurrency(parser, method, tracer, "EUR", 1000m, 1084.00m);   // 1 EUR = 1.084 USD
        TestCurrency(parser, method, tracer, "CNY", 10000m, 1375.00m);  // 1 CNY = 0.1375 USD
        TestCurrency(parser, method, tracer, "PLN", 1000m, 260.00m);    // 1 PLN = 0.260 USD
        TestCurrency(parser, method, tracer, "USD", 1000m, 1000.00m);
        TestCurrency(parser, method, tracer, "XYZ", 1000m, 1000.00m);
    }

    static void TestCurrency(object parser, MethodInfo method, ITracingService tracer, string currencyCode, decimal amount, decimal expected)
    {
        var json = $"{{\"currency\":{{\"value\":\"{currencyCode}\",\"name\":\"Test\"}},\"dimension\":{{\"value\":\"0\",\"name\":\"Blank\"}}}}";
        var doc = JsonDocument.Parse(json);
        var result = (decimal)method.Invoke(parser, new object[] { doc.RootElement, amount });
        var status = Math.Abs(result - expected) < 0.01m ? "✅" : "❌";
        Console.WriteLine($"{status} {currencyCode} {amount:N2} => {result:N2} (expected {expected:N2})");
    }
}

class ConsoleTracer : ITracingService
{
    public void Trace(string format, params object[] args)
    {
        Console.WriteLine($"[TRACE] {string.Format(format, args)}");
    }
}

class MockOrganizationService : IOrganizationService
{
    private readonly Dictionary<string, decimal> _rates = new(StringComparer.OrdinalIgnoreCase)
    {
        { "EUR", 1.084m },
        { "CNY", 0.1375m },
        { "PLN", 0.260m },
        { "GBP", 1.273m },
        { "JPY", 0.0067m },
    };

    public EntityCollection RetrieveMultiple(QueryBase query)
    {
        if (query is QueryExpression qe && qe.EntityName == "mcs_coface_exchange_rate")
        {
            var conditions = qe.Criteria.Conditions;
            var codeCond = conditions.FirstOrDefault(c => c.AttributeName == "mcs_currencycode");
            if (codeCond != null && codeCond.Values.Count > 0)
            {
                var code = codeCond.Values[0]?.ToString();
                if (!string.IsNullOrEmpty(code) && _rates.TryGetValue(code, out var rate))
                {
                    var entity = new Entity("mcs_coface_exchange_rate");
                    entity["mcs_rate_to_usd"] = rate;
                    return new EntityCollection(new List<Entity> { entity });
                }
            }
        }
        return new EntityCollection();
    }

    public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { }
    public Guid Create(Entity entity) => Guid.NewGuid();
    public void Delete(string entityName, Guid id) { }
    public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) { }
    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => new Entity(entityName);
    public void Update(Entity entity) { }
    public OrganizationResponse Execute(OrganizationRequest request) => throw new NotImplementedException();
}
