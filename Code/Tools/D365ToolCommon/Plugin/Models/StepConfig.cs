namespace D365ToolCommon.Plugin.Models
{
    /// <summary>
    /// Plugin Step 配置模型
    /// </summary>
    public class StepConfig
    {
        /// <summary>消息名称，如 Update / Create / Delete</summary>
        public string MessageName { get; set; } = string.Empty;

        /// <summary>主实体逻辑名</summary>
        public string PrimaryEntity { get; set; } = string.Empty;

        /// <summary>阶段：10=PreValidation, 20=PreOperation, 40=PostOperation</summary>
        public int Stage { get; set; }

        /// <summary>模式：0=同步，1=异步</summary>
        public int Mode { get; set; }

        /// <summary>过滤字段，逗号分隔</summary>
        public string? FilteringAttributes { get; set; }

        /// <summary>执行顺序，默认 1</summary>
        public int Rank { get; set; } = 1;

        /// <summary>部署方式：0=ServerOnly, 1=MicrosoftDynamicsCRMClientOnly, 2=Both</summary>
        public int SupportedDeployment { get; set; } = 0;
    }
}
