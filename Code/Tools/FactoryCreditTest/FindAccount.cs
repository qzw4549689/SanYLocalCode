using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace FactoryCreditTest
{
    public static class FindAccount
    {
        public static Guid? ByMasterDataId(IOrganizationService service, Guid masterDataId)
        {
            QueryExpression query = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("accountid", "name"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("mcs_customermasterdata", ConditionOperator.Equal, masterDataId)
                    }
                },
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                Guid accountId = result.Entities[0].Id;
                Console.WriteLine($"找到关联Account: {accountId}, Name={result.Entities[0].GetAttributeValue<string>("name")}");
                return accountId;
            }

            Console.WriteLine("未找到关联Account");
            return null;
        }

        public static Guid? FirstAvailable(IOrganizationService service)
        {
            QueryExpression query = new QueryExpression("account")
            {
                ColumnSet = new ColumnSet("accountid", "name"),
                TopCount = 1
            };

            EntityCollection result = service.RetrieveMultiple(query);
            if (result.Entities.Count > 0)
            {
                Guid accountId = result.Entities[0].Id;
                Console.WriteLine($"找到可用Account: {accountId}, Name={result.Entities[0].GetAttributeValue<string>("name")}");
                return accountId;
            }

            Console.WriteLine("未找到可用Account");
            return null;
        }
    }
}
