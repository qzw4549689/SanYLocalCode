using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;
using System.Text.Json;

namespace SanyD365.D365Extension.Sales.Application.Sales.CofaceIntegration
{
    /// <summary>
    /// Helper to load Coface API configuration from ms_systemconfiguration
    /// </summary>
    public static class CofaceConfigHelper
    {
        public const string ConfigName = "CofaceApiConfig";
        public const string ConfigEntityName = "ms_systemconfiguration";
        public const string ConfigNameField = "ms_name";
        public const string ConfigContentField = "ms_content";

        public static CofaceApiConfig GetConfig(IOrganizationService service)
        {
            if (service == null)
                throw new ArgumentNullException(nameof(service));

            var query = new QueryExpression(ConfigEntityName)
            {
                ColumnSet = new ColumnSet(ConfigContentField)
            };
            query.Criteria.AddCondition(ConfigNameField, ConditionOperator.Equal, ConfigName);

            var results = service.RetrieveMultiple(query);
            var entity = results.Entities.FirstOrDefault();
            if (entity == null)
            {
                throw new InvalidPluginExecutionException($"System configuration not found: {ConfigName}. Please create it in {ConfigEntityName}.");
            }

            var json = entity.GetAttributeValue<string>(ConfigContentField);
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidPluginExecutionException($"System configuration {ConfigName} content is empty.");
            }

            try
            {
                var config = JsonSerializer.Deserialize<CofaceApiConfig>(json);
                if (config == null)
                {
                    throw new InvalidPluginExecutionException($"Failed to deserialize system configuration {ConfigName}.");
                }
                return config;
            }
            catch (JsonException ex)
            {
                throw new InvalidPluginExecutionException($"Invalid JSON in system configuration {ConfigName}: {ex.Message}");
            }
        }
    }
}
