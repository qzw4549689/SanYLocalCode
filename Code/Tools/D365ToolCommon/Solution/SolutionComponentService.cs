using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace D365ToolCommon.Solution
{
    /// <summary>
    /// Solution 组件管理通用服务。
    /// 封装 WebResource、实体等组件添加到 Solution 的操作。
    /// </summary>
    public class SolutionComponentService
    {
        private readonly ServiceClient _service;

        public SolutionComponentService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 将 WebResource 添加到指定 Solution。
        /// </summary>
        public Guid AddWebResourceToSolution(string webResourceName, string solutionUniqueName)
        {
            var webResourceId = QueryWebResourceId(webResourceName);
            if (webResourceId == Guid.Empty)
                throw new InvalidOperationException($"未找到 WebResource: {webResourceName}");

            return AddComponentToSolution(webResourceId, 61, solutionUniqueName);
        }

        /// <summary>
        /// 将实体添加到指定 Solution。
        /// </summary>
        public Guid AddEntityToSolution(string entityLogicalName, string solutionUniqueName)
        {
            var entityId = QueryEntityId(entityLogicalName);
            if (entityId == Guid.Empty)
                throw new InvalidOperationException($"未找到实体: {entityLogicalName}");

            return AddComponentToSolution(entityId, 1, solutionUniqueName);
        }

        /// <summary>
        /// 通用：将组件添加到 Solution。
        /// </summary>
        public Guid AddComponentToSolution(Guid componentId, int componentType, string solutionUniqueName)
        {
            if (string.IsNullOrWhiteSpace(solutionUniqueName))
                throw new ArgumentException("Solution 唯一名称不能为空", nameof(solutionUniqueName));

            var solutionId = QuerySolutionId(solutionUniqueName);
            if (solutionId == Guid.Empty)
                throw new InvalidOperationException($"未找到 Solution: {solutionUniqueName}");

            if (IsComponentInSolution(componentId, componentType, solutionId))
            {
                Console.WriteLine($"  组件已在 Solution {solutionUniqueName} 中，跳过");
                return Guid.Empty;
            }

            var request = new AddSolutionComponentRequest
            {
                ComponentId = componentId,
                ComponentType = componentType,
                SolutionUniqueName = solutionUniqueName,
                AddRequiredComponents = false
            };

            var response = (AddSolutionComponentResponse)_service.Execute(request);
            Console.WriteLine($"  ✅ 已添加到 Solution {solutionUniqueName}: {response.id}");
            return response.id;
        }

        private Guid QuerySolutionId(string uniqueName)
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("solutionid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("uniquename", ConditionOperator.Equal, uniqueName) }
                }
            };
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        private Guid QueryWebResourceId(string name)
        {
            var query = new QueryExpression("webresource")
            {
                ColumnSet = new ColumnSet("webresourceid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, name) }
                }
            };
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        private Guid QueryEntityId(string logicalName)
        {
            var query = new QueryExpression("entity")
            {
                ColumnSet = new ColumnSet("entityid"),
                Criteria = new FilterExpression
                {
                    Conditions = { new ConditionExpression("name", ConditionOperator.Equal, logicalName) }
                }
            };
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            return result?.Id ?? Guid.Empty;
        }

        private bool IsComponentInSolution(Guid componentId, int componentType, Guid solutionId)
        {
            var query = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("solutioncomponentid"),
                Criteria = new FilterExpression
                {
                    Conditions =
                    {
                        new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                        new ConditionExpression("objectid", ConditionOperator.Equal, componentId),
                        new ConditionExpression("componenttype", ConditionOperator.Equal, componentType)
                    }
                }
            };
            return _service.RetrieveMultiple(query).Entities.Any();
        }
    }
}
