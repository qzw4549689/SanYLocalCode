using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using System;

namespace DeployTool
{
    public class TestBppStartApi
    {
        public static void Test(ServiceClient service, Guid recordId)
        {
            Console.WriteLine($">>> 测试调用 mcs_bppstartapi (记录: {recordId})...");
            
            // 先将记录状态设为14(审核申请)，模拟真实流程
            Console.WriteLine("  先将记录状态设为14(审核申请)...");
            try
            {
                var setStatus = new Entity("mcs_credit_record") { Id = recordId };
                setStatus["mcs_status"] = new OptionSetValue(14);
                service.Update(setStatus);
                Console.WriteLine("  ✅ 状态已设为14");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 设状态失败: {ex.Message}");
            }
            
            try
            {
                // 获取当前用户ID
                var whoami = service.Execute(new OrganizationRequest("WhoAmI"));
                var userId = whoami["UserId"] as Guid?;
                Console.WriteLine($"  当前用户ID: {userId}");
                
                var request = new OrganizationRequest("mcs_bppstartapi");
                request["EntityId"] = recordId.ToString();
                request["EntityName"] = "mcs_credit_record";
                if (userId.HasValue)
                    request["UserId"] = userId.Value.ToString();
                
                var response = service.Execute(request);
                
                if (response.Results.Contains("Result"))
                {
                    var result = response["Result"] as string;
                    Console.WriteLine($"  ✅ 调用成功! Result={result}");
                }
                else
                {
                    Console.WriteLine($"  ⚠️ 调用返回但无Result字段");
                    foreach (var key in response.Results.Keys)
                    {
                        Console.WriteLine($"    {key} = {response[key]}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"     内部异常: {ex.InnerException.Message}");
                }
            }
            
            // 同时测试bppcheckapi看看记录当前状态
            Console.WriteLine($">>> 测试调用 mcs_bppcheckapi (记录: {recordId})...");
            try
            {
                var checkReq = new OrganizationRequest("mcs_bppcheckapi");
                checkReq["EntityId"] = recordId.ToString();
                checkReq["EntityName"] = "mcs_credit_record";
                
                var checkResp = service.Execute(checkReq);
                if (checkResp.Results.Contains("Result"))
                {
                    Console.WriteLine($"  ✅ Result={checkResp["Result"]}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ❌ 调用失败: {ex.Message}");
            }
        }
    }
}
