using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace D365ToolCommon.Plugin
{
    /// <summary>
    /// Plugin/Assembly/Step 查询通用服务，返回结构化数据，不直接写 Console。
    /// </summary>
    public class PluginQueryService
    {
        private readonly ServiceClient _service;

        public PluginQueryService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 查询所有 Plugin Assembly。
        /// </summary>
        public IEnumerable<Entity> QueryAssemblies(string? nameFilter = null)
        {
            var query = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid", "name", "version", "sourcetype", "isolationmode", "createdon", "modifiedon"),
                Orders = { new OrderExpression("modifiedon", OrderType.Descending) }
            };

            if (!string.IsNullOrWhiteSpace(nameFilter))
                query.Criteria.AddCondition("name", ConditionOperator.Like, $"%{nameFilter}%");

            return _service.RetrieveMultiple(query).Entities;
        }

        /// <summary>
        /// 根据名称查询 Plugin Assembly。
        /// </summary>
        public Entity? QueryAssembly(string assemblyName)
        {
            var query = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid", "name", "version", "createdon", "modifiedon"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, assemblyName) }
                }
            };
            return _service.RetrieveMultiple(query).Entities.FirstOrDefault();
        }

        /// <summary>
        /// 查询某个 Assembly 下的所有 Plugin Types。
        /// </summary>
        public IEnumerable<Entity> QueryTypesByAssembly(Guid assemblyId)
        {
            var query = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid", "name", "typename", "assemblyname"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("pluginassemblyid", ConditionOperator.Equal, assemblyId) }
                },
                Orders = { new OrderExpression("typename", OrderType.Ascending) }
            };
            return _service.RetrieveMultiple(query).Entities;
        }

        /// <summary>
        /// 根据 TypeName 查询 Plugin Type。
        /// </summary>
        public IEnumerable<Entity> QueryTypesByName(string typeName)
        {
            var query = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid", "name", "typename", "assemblyname"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("typename", ConditionOperator.Like, $"%{typeName}%") }
                }
            };
            return _service.RetrieveMultiple(query).Entities;
        }

        /// <summary>
        /// 根据命名空间前缀查询 Plugin Types。
        /// </summary>
        public IEnumerable<Entity> QueryTypesByNamespace(string namespacePrefix)
        {
            var query = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid", "name", "typename", "assemblyname"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("typename", ConditionOperator.BeginsWith, namespacePrefix) }
                },
                Orders = { new OrderExpression("typename", OrderType.Ascending) }
            };
            return _service.RetrieveMultiple(query).Entities;
        }

        /// <summary>
        /// 查询某个 Plugin Type 下的所有 Steps。
        /// </summary>
        public IEnumerable<Entity> QueryStepsByType(Guid pluginTypeId)
        {
            var query = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet(
                    "sdkmessageprocessingstepid",
                    "name",
                    "stage",
                    "filteringattributes",
                    "asyncautodelete",
                    "mode",
                    "rank",
                    "statecode",
                    "statuscode"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("plugintypeid", ConditionOperator.Equal, pluginTypeId) }
                },
                LinkEntities =
                {
                    new LinkEntity("sdkmessageprocessingstep", "sdkmessagefilter", "sdkmessagefilterid", "sdkmessagefilterid", JoinOperator.LeftOuter)
                    {
                        Columns = new ColumnSet("primaryobjecttypecode"),
                        EntityAlias = "filter"
                    },
                    new LinkEntity("sdkmessageprocessingstep", "sdkmessage", "sdkmessageid", "sdkmessageid", JoinOperator.LeftOuter)
                    {
                        Columns = new ColumnSet("name"),
                        EntityAlias = "msg"
                    }
                }
            };
            return _service.RetrieveMultiple(query).Entities;
        }

        /// <summary>
        /// 查询某个实体上注册的所有 Steps。
        /// </summary>
        public IEnumerable<Entity> QueryStepsByEntity(string entityLogicalName)
        {
            var query = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet("name", "sdkmessagefilterid", "plugintypeid", "filteringattributes", "stage", "mode", "statecode"),
                Criteria = new FilterExpression { Conditions = { new ConditionExpression("statecode", ConditionOperator.Equal, 0) } }
            };

            var linkFilter = new LinkEntity("sdkmessageprocessingstep", "sdkmessagefilter", "sdkmessagefilterid", "sdkmessagefilterid", JoinOperator.Inner)
            {
                Columns = new ColumnSet("primaryobjecttypecode"),
                EntityAlias = "filter"
            };
            linkFilter.LinkCriteria.AddCondition("primaryobjecttypecode", ConditionOperator.Equal, entityLogicalName);
            query.LinkEntities.Add(linkFilter);

            var linkType = new LinkEntity("sdkmessageprocessingstep", "plugintype", "plugintypeid", "plugintypeid", JoinOperator.Inner)
            {
                Columns = new ColumnSet("typename"),
                EntityAlias = "type"
            };
            query.LinkEntities.Add(linkType);

            return _service.RetrieveMultiple(query).Entities;
        }

        /// <summary>
        /// 查询最近 N 条 Plugin Trace Log。
        /// </summary>
        public IEnumerable<Entity> QueryPluginTraceLog(string? typeNameFilter = null, int hours = 2, int topCount = 10)
        {
            var query = new QueryExpression("plugintracelog")
            {
                ColumnSet = new ColumnSet("typename", "messagename", "primaryentity", "performanceexecutionduration", "exceptiondetails", "messageblock", "createdon"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("createdon", ConditionOperator.GreaterThan, DateTime.UtcNow.AddHours(-hours))
                    }
                },
                Orders = { new OrderExpression("createdon", OrderType.Descending) },
                TopCount = topCount
            };

            if (!string.IsNullOrWhiteSpace(typeNameFilter))
                query.Criteria.AddCondition("typename", ConditionOperator.Like, $"%{typeNameFilter}%");

            return _service.RetrieveMultiple(query).Entities;
        }
    }
}
