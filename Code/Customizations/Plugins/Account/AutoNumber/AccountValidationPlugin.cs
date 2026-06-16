using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Linq;

namespace SanyD365.Plugins.Account
{
    /// <summary>
    /// 客户主数据表(Account) - 信用评估扩展校验Plugin
    /// 触发时机：Create前 / Update前
    /// 功能：
    /// 1. 校验黑名单客户字段
    /// 2. 校验不予授信客户字段
    /// 影响范围：仅限account实体（信用评估相关8个字段已迁移到mcs_customermasterdata）
    /// </summary>
    public class AccountValidationPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("AccountValidationPlugin 开始执行");

            // 只处理Create前或Update前事件
            if ((context.MessageName != "Create" && context.MessageName != "Update") || context.Stage != 20)
            {
                tracer.Trace("非Create/Update前事件，跳过");
                return;
            }

            if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
            {
                tracer.Trace("未找到Target实体");
                return;
            }

            Entity target = (Entity)context.InputParameters["Target"];
            
            if (target.LogicalName != "account")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            try
            {
                // 校验布尔类型字段（F7.1 客户画像模块，字段保留在 account 上）
                if (target.Contains("mcs_blacklist"))
                {
                    ValidateBooleanField(target, "mcs_blacklist", "黑名单客户", tracer);
                }

                if (target.Contains("mcs_creditgrant"))
                {
                    ValidateBooleanField(target, "mcs_creditgrant", "不予授信客户", tracer);
                }

                tracer.Trace("AccountValidationPlugin 校验通过");
            }
            catch (InvalidPluginExecutionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                tracer.Trace($"校验失败: {ex.Message}");
                throw new InvalidPluginExecutionException($"客户主数据校验失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 校验布尔类型字段
        /// </summary>
        private void ValidateBooleanField(Entity target, string fieldName, string fieldLabel, ITracingService tracer)
        {
            int? value = target.GetAttributeValue<int?>(fieldName);
            
            if (!value.HasValue)
            {
                tracer.Trace($"{fieldLabel}为空，跳过校验");
                return;
            }

            if (value != 0 && value != 1)
            {
                throw new InvalidPluginExecutionException(
                    $"{fieldLabel} '{value}' 不正确，有效值为: 0-否, 1-是"
                );
            }

            tracer.Trace($"{fieldLabel}校验通过: {value}");
        }
    }
}
