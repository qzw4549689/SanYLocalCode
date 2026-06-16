using Microsoft.Xrm.Sdk;
using System;
using System.Linq;

namespace SanyD365.Plugins.CustomerMasterData.Validation
{
    /// <summary>
    /// 客户主数据表(mcs_customermasterdata) - 信用评估字段校验Plugin
    /// 触发时机：Create前 / Update前
    /// 功能：
    /// 1. 校验Coface ID格式（icon#数字）
    /// 2. 校验客户等级值范围（A0-A4）
    /// 3. 校验信用评估有效状态值范围
    /// 4. 校验经销商分级值范围
    /// 5. 校验重点尽调字段
    /// 影响范围：仅限mcs_customermasterdata实体
    /// </summary>
    public class CustomerMasterDataValidationPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = factory.CreateOrganizationService(context.UserId);
            ITracingService tracer = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            tracer.Trace("CustomerMasterDataValidationPlugin 开始执行");

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

            if (target.LogicalName != "mcs_customermasterdata")
            {
                tracer.Trace($"实体不匹配: {target.LogicalName}");
                return;
            }

            try
            {
                // 校验Coface ID格式
                if (target.Contains("mcs_cofaceid"))
                {
                    ValidateCofaceId(target, tracer);
                }

                // 校验客户等级（Picklist）
                if (target.Contains("mcs_creditgrade"))
                {
                    ValidateCreditGrade(target, tracer);
                }

                // 校验信用评估有效状态（Boolean）
                if (target.Contains("mcs_creditvalid"))
                {
                    ValidateCreditValid(target, tracer);
                }

                // 校验经销商分级（Picklist）
                if (target.Contains("mcs_dealerrank"))
                {
                    ValidateDealerRank(target, tracer);
                }

                // 校验重点尽调（Boolean）
                if (target.Contains("mcs_isdd"))
                {
                    ValidateBooleanField(target, "mcs_isdd", "重点尽调", tracer);
                }

                tracer.Trace("CustomerMasterDataValidationPlugin 校验通过");
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
        /// 校验Coface ID格式
        /// 格式：icon#数字，如 icon#164031501
        /// </summary>
        private void ValidateCofaceId(Entity target, ITracingService tracer)
        {
            string cofaceId = target.GetAttributeValue<string>("mcs_cofaceid");

            if (string.IsNullOrWhiteSpace(cofaceId))
            {
                // 空值允许（未关联Coface的客户）
                tracer.Trace("Coface ID为空，跳过校验");
                return;
            }

            // 校验格式：icon#数字
            if (!System.Text.RegularExpressions.Regex.IsMatch(cofaceId, @"^icon#\d+$"))
            {
                throw new InvalidPluginExecutionException(
                    $"科法斯客户代码格式不正确: '{cofaceId}'，正确格式应为 icon#数字，如 icon#164031501"
                );
            }

            tracer.Trace($"Coface ID格式校验通过: {cofaceId}");
        }

        /// <summary>
        /// 校验客户等级
        /// 有效值：A0=100000000, A1=100000001, A2=100000002, A3=100000003, A4=100000004
        /// </summary>
        private void ValidateCreditGrade(Entity target, ITracingService tracer)
        {
            var gradeValue = target.GetAttributeValue<OptionSetValue>("mcs_creditgrade");

            if (gradeValue == null)
            {
                tracer.Trace("客户等级为空，跳过校验");
                return;
            }

            int[] validValues = { 100000000, 100000001, 100000002, 100000003, 100000004 };

            if (!validValues.Contains(gradeValue.Value))
            {
                throw new InvalidPluginExecutionException(
                    $"客户等级值 '{gradeValue.Value}' 不正确，有效值为: A0=100000000, A1=100000001, A2=100000002, A3=100000003, A4=100000004"
                );
            }

            tracer.Trace($"客户等级校验通过: {gradeValue.Value}");
        }

        /// <summary>
        /// 校验信用评估有效状态
        /// 有效值：false=失效, true=有效, null=未评估
        /// </summary>
        private void ValidateCreditValid(Entity target, ITracingService tracer)
        {
            bool? valid = target.GetAttributeValue<bool?>("mcs_creditvalid");

            if (!valid.HasValue)
            {
                tracer.Trace("信用评估有效状态为空，跳过校验");
                return;
            }

            tracer.Trace($"信用评估有效状态校验通过: {valid.Value}");
        }

        /// <summary>
        /// 校验经销商分级
        /// 有效值：1=钻石, 2=铂金, 3=白银, 4=认证, 5=意向
        /// </summary>
        private void ValidateDealerRank(Entity target, ITracingService tracer)
        {
            var rankValue = target.GetAttributeValue<OptionSetValue>("mcs_dealerrank");

            if (rankValue == null)
            {
                tracer.Trace("经销商分级为空，跳过校验");
                return;
            }

            int[] validValues = { 1, 2, 3, 4, 5 };

            if (!validValues.Contains(rankValue.Value))
            {
                throw new InvalidPluginExecutionException(
                    $"经销商分级值 '{rankValue.Value}' 不正确，有效值为: 1=钻石, 2=铂金, 3=白银, 4=认证, 5=意向"
                );
            }

            tracer.Trace($"经销商分级校验通过: {rankValue.Value}");
        }

        /// <summary>
        /// 校验布尔类型字段
        /// </summary>
        private void ValidateBooleanField(Entity target, string fieldName, string fieldLabel, ITracingService tracer)
        {
            bool? value = target.GetAttributeValue<bool?>(fieldName);

            if (!value.HasValue)
            {
                tracer.Trace($"{fieldLabel}为空，跳过校验");
                return;
            }

            tracer.Trace($"{fieldLabel}校验通过: {value.Value}");
        }
    }
}
