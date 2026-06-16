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
        var method = parserType.GetMethod("ParseNaceCodes", BindingFlags.NonPublic | BindingFlags.Instance);
        Console.WriteLine($"Method found: {method != null}");

        // 测试用例
        TestNace(parser, method, tracer, "农业", new[] { "0111" });        // Division 01
        TestNace(parser, method, tracer, "林业", new[] { "0210" });        // Division 02
        TestNace(parser, method, tracer, "矿业", new[] { "0510", "0710" }); // Division 05, 07
        TestNace(parser, method, tracer, "制造业", new[] { "1011", "2910" }); // Division 10, 29
        TestNace(parser, method, tracer, "商混", new[] { "2311" });        // Division 23 精确匹配，应优先于制造业
        TestNace(parser, method, tracer, "建工", new[] { "4110", "4210" }); // Division 41, 42
        TestNace(parser, method, tracer, "吊装", new[] { "4310" });        // Division 43
        TestNace(parser, method, tracer, "集装箱运力", new[] { "4910", "5010", "5110" }); // Division 49-51
        TestNace(parser, method, tracer, "港务", new[] { "5210" });        // Division 52
        TestNace(parser, method, tracer, "租赁", new[] { "7710" });        // Division 77
        TestNace(parser, method, tracer, "O", new[] { "9910" });           // Division 99 未配置
        TestNace(parser, method, tracer, "农业,制造业", new[] { "0111", "2910" }); // 多个不同行业
    }

    static void TestNace(object parser, MethodInfo method, ITracingService tracer, string expected, string[] naceCodes)
    {
        var codes = string.Join(",", naceCodes.Select(c => $"{{\"code\":\"{c}\"}}"));
        var json = $"{{\"companyGeneralInformation\":{{\"naceCodes\":[{codes}]}}}}";
        var doc = JsonDocument.Parse(json);
        var result = (string)method.Invoke(parser, new object[] { doc.RootElement });
        var status = result == expected ? "✅" : "❌";
        Console.WriteLine($"{status} [{string.Join(",", naceCodes)}] => {result} (expected {expected})");
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
    private readonly List<(int From, int To, string DivisionName, string SanyIndustry)> _mappings = new()
    {
        (1, 1, "Crop and animal production", "农业"),
        (2, 2, "Forestry and logging", "林业"),
        (5, 9, "Mining and quarrying", "矿业"),
        (10, 33, "Manufacturing", "制造业"),
        (23, 23, "Manufacture of other non-metallic mineral products", "商混"),
        (41, 42, "Construction", "建工"),
        (43, 43, "Specialised construction", "吊装"),
        (49, 51, "Transportation", "集装箱运力"),
        (52, 52, "Warehousing", "港务"),
        (77, 77, "Rental and leasing", "租赁")
    };

    public EntityCollection RetrieveMultiple(QueryBase query)
    {
        if (query is QueryExpression qe && qe.EntityName == "mcs_coface_nace_mapping")
        {
            var conditions = qe.Criteria.Conditions;
            var fromCond = conditions.FirstOrDefault(c => c.AttributeName == "mcs_nacedivisionfrom" && c.Operator == ConditionOperator.LessEqual);
            var toCond = conditions.FirstOrDefault(c => c.AttributeName == "mcs_nacedivisionto" && c.Operator == ConditionOperator.GreaterEqual);

            if (fromCond != null && fromCond.Values.Count > 0 &&
                toCond != null && toCond.Values.Count > 0)
            {
                int division = Convert.ToInt32(fromCond.Values[0]);

                var match = _mappings
                    .Where(m => m.From <= division && m.To >= division)
                    .OrderBy(m => m.To - m.From) // 精确匹配优先
                    .ThenBy(m => m.From)
                    .FirstOrDefault();

                if (match != default)
                {
                    var entity = new Entity("mcs_coface_nace_mapping");
                    entity["mcs_nacedivisionfrom"] = match.From;
                    entity["mcs_nacedivisionto"] = match.To;
                    entity["mcs_nacedivisionname"] = match.DivisionName;
                    entity["mcs_sanyindustry"] = match.SanyIndustry;
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
