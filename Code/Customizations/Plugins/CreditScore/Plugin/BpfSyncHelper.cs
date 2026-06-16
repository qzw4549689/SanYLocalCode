using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;

namespace SanyD365.Plugins.CreditScore.Plugin
{
    /// <summary>
    /// BPF实例同步辅助类
    /// 当mcs_status更新时，同步更新BPF的stageid
    /// 新BPF直接使用主实体mcs_credit_record的processid和stageid字段
    /// </summary>
    public static class BpfSyncHelper
    {
        // BPF流程ID (信用评估)
        private static readonly Guid BpfId = new Guid("824edb28-b460-f111-a826-000d3aa333b3");
        
        // 状态值到StageId的映射
        private static readonly Dictionary<int, Guid> StatusToStageMap = new Dictionary<int, Guid>
        {
            { 9, new Guid("69b1a432-b963-4112-879d-a8f89e0c78e4") },   // 发起信用评估
            { 10, new Guid("391ff60e-7aa4-4aa8-beea-ee679a2d5f11") },  // 关联客户代码
            { 11, new Guid("a98cb93e-72ad-4ac0-be65-8fee8d525b04") },  // 内外部数据集成
            { 12, new Guid("abf076d1-f3c1-4144-9c15-ca3dda62ed39") },  // 人工复核
            { 13, new Guid("f1de9ef1-2f9f-44ff-bc34-f6732eb330ab") },  // 信用分计算
            { 14, new Guid("64382bb7-2e3f-4b56-a4be-922924141250") },  // 审核申请
            { 15, new Guid("1158618a-a0c3-4030-8bef-4b3af5740062") },  // 审批通过
            { 16, new Guid("00fe3244-c92a-4108-b733-6a824fac0dae") }   // 审批未通过
        };

        /// <summary>
        /// 同步BPF的stageid到对应状态的阶段
        /// 直接更新mcs_credit_record的processid和stageid字段
        /// </summary>
        public static void SyncBpfStage(IOrganizationService service, ITracingService tracer, Guid creditRecordId, int statusValue)
        {
            tracer.Trace($"===== BPF同步开始: creditRecordId={creditRecordId}, status={statusValue} =====");

            try
            {
                // 获取目标StageId
                if (!StatusToStageMap.TryGetValue(statusValue, out Guid targetStageId))
                {
                    tracer.Trace($"状态值 {statusValue} 未映射到StageId，跳过同步");
                    return;
                }

                // 直接更新mcs_credit_record的processid和stageid字段
                var updateRecord = new Entity("mcs_credit_record")
                {
                    Id = creditRecordId
                };
                
                // 设置BPF流程ID
                updateRecord["processid"] = BpfId;
                
                // 设置当前阶段ID
                updateRecord["stageid"] = targetStageId;
                
                tracer.Trace($"更新BPF: processid={BpfId}, stageid={targetStageId}");

                service.Update(updateRecord);
                tracer.Trace("BPF stageid已更新");
            }
            catch (Exception ex)
            {
                tracer.Trace($"BPF同步失败: {ex.Message}");
                // BPF同步失败不应阻塞主流程，只记录日志
            }

            tracer.Trace("===== BPF同步结束 =====");
        }
    }
}
