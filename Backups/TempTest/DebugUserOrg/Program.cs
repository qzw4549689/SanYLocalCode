using D365ToolCommon.Connection;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

class Program
{
    static async Task Main(string[] args)
    {
        var url = Environment.GetEnvironmentVariable("D365_URL") ?? "https://dev1.crm5.dynamics.com";
        Console.WriteLine($"目标环境: {url}");

        using var service = await D365ConnectionFactory.CreateAsync(url);
        if (!service.IsReady)
        {
            Console.WriteLine("连接失败!");
            return;
        }

        // 通过 WhoAmI 获取当前用户ID
        var whoAmI = service.Execute(new WhoAmIRequest()) as WhoAmIResponse;
        if (whoAmI == null)
        {
            Console.WriteLine("WhoAmI 调用失败");
            return;
        }
        var userId = whoAmI.UserId;
        Console.WriteLine($"当前登录用户 systemuser.id: {userId}");

        // 1. 查询当前 systemuser 名称
        var user = service.Retrieve("systemuser", userId, new ColumnSet("fullname", "internalemailaddress", "domainname"));
        Console.WriteLine($"  fullname: {user.GetAttributeValue<string>("fullname")}");
        Console.WriteLine($"  email: {user.GetAttributeValue<string>("internalemailaddress")}");
        Console.WriteLine($"  domainname: {user.GetAttributeValue<string>("domainname")}");
        Console.WriteLine();

        // 2. 查询 mcs_useraccount
        var uaQuery = new QueryExpression("mcs_useraccount")
        {
            ColumnSet = new ColumnSet("mcs_useraccountid", "mcs_name", "mcs_orgid", "mcs_systemuserid"),
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("mcs_systemuserid", ConditionOperator.Equal, userId),
                    new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                }
            },
            TopCount = 1
        };
        var uaResult = service.RetrieveMultiple(uaQuery);
        if (uaResult.Entities.Count == 0)
        {
            Console.WriteLine("未找到当前用户对应的 mcs_useraccount 记录");
            return;
        }

        var ua = uaResult.Entities[0];
        Console.WriteLine($"mcs_useraccount:");
        Console.WriteLine($"  id: {ua.Id}");
        Console.WriteLine($"  mcs_name: {ua.GetAttributeValue<string>("mcs_name")}");
        PrintLookup(service, ua, "mcs_orgid", "mcs_org");
        Console.WriteLine();

        var orgId = ua.GetAttributeValue<EntityReference>("mcs_orgid")?.Id;
        if (!orgId.HasValue)
        {
            Console.WriteLine("mcs_useraccount.mcs_orgid 为空");
            return;
        }

        // 3. 沿 mcs_parentorganization 递归取组织链
        Console.WriteLine("组织链（从当前组织向上）：");
        var orgs = new List<Entity>();
        Guid? currentId = orgId.Value;
        var visited = new HashSet<Guid>();
        while (currentId.HasValue && currentId.Value != Guid.Empty && !visited.Contains(currentId.Value))
        {
            visited.Add(currentId.Value);
            var org = service.Retrieve("mcs_org", currentId.Value,
                new ColumnSet("mcs_name", "mcs_organizationtype", "mcs_parentorganization", "mcs_buid", "mcs_regionid", "mcs_countryid"));
            orgs.Add(org);
            Console.WriteLine($"  [{orgs.Count}] id={org.Id}");
            Console.WriteLine($"       name: {org.GetAttributeValue<string>("mcs_name")}");
            Console.WriteLine($"       type: {FormatOrgType(org.GetAttributeValue<OptionSetValue>("mcs_organizationtype")?.Value)}");
            PrintLookup(service, org, "mcs_parentorganization", "mcs_org", "       parent: ");
            PrintLookup(service, org, "mcs_buid", "mcs_bu", "       mcs_buid: ");
            PrintLookup(service, org, "mcs_regionid", "mcs_region", "       mcs_regionid: ");
            PrintLookup(service, org, "mcs_countryid", "mcs_country", "       mcs_countryid: ");

            currentId = org.GetAttributeValue<EntityReference>("mcs_parentorganization")?.Id;
        }
        Console.WriteLine();

        // 4. 识别事业部(10)和大区(20)
        var buOrg = orgs.FirstOrDefault(o => o.GetAttributeValue<OptionSetValue>("mcs_organizationtype")?.Value == 10);
        var regionOrg = orgs.FirstOrDefault(o => o.GetAttributeValue<OptionSetValue>("mcs_organizationtype")?.Value == 20);

        Console.WriteLine("识别结果：");
        Console.WriteLine($"  当前组织: {orgs.FirstOrDefault()?.GetAttributeValue<string>("mcs_name")}");
        Console.WriteLine($"  事业部(10): {(buOrg != null ? buOrg.GetAttributeValue<string>("mcs_name") + " (id=" + buOrg.Id + ")" : "未找到")}");
        Console.WriteLine($"  大区(20): {(regionOrg != null ? regionOrg.GetAttributeValue<string>("mcs_name") + " (id=" + regionOrg.Id + ")" : "未找到")}");
        Console.WriteLine();

        // 5. 查询 mcs_regioncountryrelation
        var buId = buOrg?.GetAttributeValue<EntityReference>("mcs_buid")?.Id ?? buOrg?.Id;
        var regionId = regionOrg?.GetAttributeValue<EntityReference>("mcs_regionid")?.Id ?? regionOrg?.Id;

        Console.WriteLine($"用于关系表查询: buId={buId}, regionId={regionId}");

        if (regionId.HasValue && buId.HasValue)
        {
            var relQuery = new QueryExpression("mcs_regioncountryrelation")
            {
                ColumnSet = new ColumnSet("mcs_regioncountryrelationid", "mcs_regionid", "mcs_nrplatform", "mcs_countryid", "mcs_buid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_regionid", ConditionOperator.Equal, regionId.Value),
                        new ConditionExpression("mcs_buid", ConditionOperator.Equal, buId.Value),
                        new ConditionExpression("statecode", ConditionOperator.Equal, 0)
                    }
                }
            };
            var relResult = service.RetrieveMultiple(relQuery);
            Console.WriteLine($"mcs_regioncountryrelation 命中 {relResult.Entities.Count} 条：");
            foreach (var rel in relResult.Entities)
            {
                Console.WriteLine($"  rel id: {rel.Id}");
                PrintLookup(service, rel, "mcs_regionid", "mcs_region", "    region: ");
                PrintLookup(service, rel, "mcs_buid", "mcs_bu", "    bu: ");
                PrintLookup(service, rel, "mcs_countryid", "mcs_country", "    country: ");
                PrintLookup(service, rel, "mcs_nrplatform", "mcs_nationalregion", "    national region: ");
            }
        }
        else
        {
            Console.WriteLine("缺少大区或事业部，跳过关系表查询。");
        }

        Console.WriteLine();
        Console.WriteLine("诊断完成。");
    }

    static void PrintLookup(ServiceClient service, Entity entity, string attributeName, string entityLogicalName, string prefix = "  ")
    {
        var lookup = entity.GetAttributeValue<EntityReference>(attributeName);
        if (lookup == null)
        {
            Console.WriteLine($"{prefix}{attributeName}: (null)");
            return;
        }

        string? name = lookup.Name;
        if (string.IsNullOrEmpty(name))
        {
            try
            {
                var related = service.Retrieve(entityLogicalName, lookup.Id, new ColumnSet("mcs_name", "name"));
                name = related.GetAttributeValue<string>("mcs_name") ?? related.GetAttributeValue<string>("name");
            }
            catch
            {
                name = "(无法检索)";
            }
        }
        Console.WriteLine($"{prefix}{attributeName}: {name} (id={lookup.Id})");
    }

    static string FormatOrgType(int? type)
    {
        return type switch
        {
            10 => "10-事业部",
            20 => "20-大区",
            50 => "50-区域",
            51 => "51-网点",
            80 => "80-经销商",
            90 => "90-经销商网点",
            _ => $"{type}(未知)"
        };
    }
}
