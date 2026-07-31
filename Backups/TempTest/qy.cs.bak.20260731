using MSLibrary;
using MSLibrary.DI;
using MSLibrary.MessageQueue;
using MSLibrary.Serializer;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using SanyD365.Main;
using SanyD365.Main.Application.Sales;
using SanyD365.Main.DTO;
using SanyD365.Main.DTO.BPP;
using SanyD365.Main.Entities.BPP;
using SanyD365.Main.Entities.BPP.BPPHandlerServices;
using SanyD365.Main.Entities.Endpoints.ESB;
using SanyD365.Main.MessageQueue;
using SanyD365.Main.MessageQueue.SMessageListeners;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SanyD365.Test.Sales
{
    public class qy
    {
        [SetUp]
        public void Setup()
        {
            StartUp.Execute();
        }

        [Test]
        public async Task DoFileUpload()
        {
            string entityId = "ab41c193-cb02-4398-8d0d-5934d1f8208c";
            string entityName = "mcs_lc_credit_limit_application";
            string userId = "d1026dee-2049-f111-bec6-000d3a091020";

            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.DoFileUpload(entityId, entityName, null);
        }

        [Test]
        public async Task DoBprQuotaApply()
        {
            string entityId = "736b30ea-f072-4aed-ba7d-37d8e7c124ab";
            string entityName = "mcs_nonlc_credit_limit_application";
            string userId = "d1026dee-2049-f111-bec6-000d3a091020";

            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.DoBprQuotaApply(entityId, entityName, userId);
        }

        [Test]
        public async Task DoBprQuotaGuaApply()
        {
            string entityId = "b5737db4-282b-48b5-91bf-6e8f6caafe4d";
            string entityName = "mcs_nonlc_credit_limit_application";
            string userId = "d1026dee-2049-f111-bec6-000d3a091020";

            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.DoBprQuotaGuaApply(entityId, entityName, userId);
        }

        [Test]
        public async Task DoBprQuotaBankApply()
        {
            string entityId = "79ca3283-c0ee-4cdf-80ee-15817414c0fb";
            string entityName = "mcs_nonlc_credit_limit_application";
            string userId = "d1026dee-2049-f111-bec6-000d3a091020";

            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.DoBprQuotaBankApply(entityId, entityName, userId);
        }

        [Test]
        public async Task DoBprQuotaGenApply()
        {
            string entityId = "ab41c193-cb02-4398-8d0d-5934d1f8208c";
            string entityName = "mcs_lc_credit_limit_application";
            string userId = "d1026dee-2049-f111-bec6-000d3a091020";

            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.DoBprQuotaGenApply(entityId, entityName, userId);
        }

        [Test]
        public async Task DoBprQuotaConApply()
        {
            string entityId = "e7005075-95e5-4b68-8dff-7c02e722c65a";
            string entityName = "mcs_lc_credit_limit_application";
            string userId = "d1026dee-2049-f111-bec6-000d3a091020";

            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.DoBprQuotaConApply(entityId, entityName, userId);
        }



        [Test]
        public async Task GetBprEdiApply()
        {
            string entityId = "e7005075-95e5-4b68-8dff-7c02e722c65a";
            string entityName = "mcs_lc_credit_limit_application";

            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.GetBprEdiApply(entityId, entityName);
        }       


        [Test]
        public async Task GetPolicyList()
        {
            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.GetPolicyList();
        }

        [Test]
        public async Task GetBprBuyerCodeApproveInfo()
        {
            string entityId = "33e0945d-e45f-f111-a825-000d3ac6ecbd";
            string entityName = "mcs_buyer_code_application";

            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.GetBprBuyerCodeApproveInfo(entityId, entityName);
        }



        [Test]
        public async Task DoBprCodeConfirm()
        {
            string entityId = "33E0945D-E45F-F111-A825-000D3AC6ECBD";
            string entityName = "mcs_buyer_code_application";
            string likeInfoId = "9C07CAFF-0B35-4EF1-B650-3B63D3B1EC68";

            var repository = DIContainerContainer.Get<INewEdiEndpointRepositoryCacheProxy>();
            var endpoint = await repository.QueryByName(EdiEndpointNames.Default);

            await endpoint.DoBprCodeConfirm(entityId, entityName,likeInfoId);
        }


        [Test]
        public async Task TestBppStart()
        {
            var repository = DIContainerContainer.Get<IBPPService>();
            JObject data1 = new JObject();
            JObject data2 = new JObject();
            data2.Add("EntityName", "mcs_nonlc_credit_limit_application");
            data2.Add("EntityID", "cd7d16ae-3292-4023-a899-b6f20ba286cd");
            data2.Add("UserAccount", "gw_chengzy7");
            data1.Add("Data", JsonSerializerHelper.Serializer(data2));
            var message = new SMessage()

            {
                ID = Guid.NewGuid(),
                Type = MessageTypes.BPPStartWorkflow,
                Data = JsonSerializerHelper.Serializer(data1),
                Key = SMessageHelper.GenerateKey(MessageTypes.BPPStartWorkflow, Guid.NewGuid().ToString()),
                ExpectationExecuteTime = DateTime.UtcNow
            };
            await repository.Start(message);
        }

        [Test]
        public async Task GetBppFormDataTest()
        {
            var service = DIContainerContainer.Get<BPPHandlerServiceForCreditLimitApplication>();
            ContextContainer.SetValue<string>(ContextExtensionTypes.CurrentAppUser, "Test");
            BPPFormData bPPFormData = await service.GetBppFormData(Guid.Parse("ec61301f-87e8-443e-9157-d3ee43b111d0"), "mcs_lc_credit_limit_application");
            Assert.That(bPPFormData, Is.Not.Null);
        }

        [Test]
        public async Task TestBppCallBack()
        {
            var repository = DIContainerContainer.Get<IBPPService>();
            BPPCallBackDTO bPPCallBackDTO = new BPPCallBackDTO();
            bPPCallBackDTO.FlowId = 852580524563701760;
            bPPCallBackDTO.Status = 30;
            bPPCallBackDTO.AbandonMode = 1;
            bPPCallBackDTO.EntityName = "mcs_lc_credit_limit_application";
            bPPCallBackDTO.EntityID = "86290f45-b384-4a6e-a832-1dd33d314520";
            bPPCallBackDTO.FinishTime = DateTime.Parse("2026-06-10T16:17:02");
            JObject data2 = new JObject();
            bPPCallBackDTO.Data = data2;
            await repository.CallBack(bPPCallBackDTO);
        }

        // ========== mcs_credit_record BPP 测试 ==========

        [Test]
        public void TestCreditRecordHandlerResolved()
        {
            var handler = DIContainerContainer.Get<BPPHandlerServiceForCreditRecord>();
            Assert.That(handler, Is.Not.Null, "DI 容器中无法获取 BPPHandlerServiceForCreditRecord");
        }

        [Test]
        public void TestCreditRecordHandlerRouting()
        {
            var mainHandler = DIContainerContainer.Get<IBPPHandlerService>();
            Assert.That(mainHandler, Is.Not.Null, "DI 容器中无法获取 IBPPHandlerService");
            Assert.That(BPPHandlerServiceMain.Handler.ContainsKey("mcs_credit_record"),
                Is.True, "Handler 字典中未注册 mcs_credit_record");
        }

        [Test]
        public async Task TestCreditRecordGetBppFormData()
        {
            var handler = DIContainerContainer.Get<BPPHandlerServiceForCreditRecord>();
            Guid entityId = Guid.Parse("2a0ff449-5227-ef11-be21-002248588e3d");
            
            BPPFormData formData = null;
            Exception caughtException = null;
            
            try
            {
                formData = await handler.GetBppFormData(entityId, "mcs_credit_record");
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }
            
            Assert.That(caughtException, Is.Null, 
                $"GetBppFormData 抛出异常: {caughtException?.Message}");
            Assert.That(formData, Is.Not.Null, 
                "GetBppFormData 返回 null");
            Assert.That(formData.TemplateCode, Is.Not.Null.Or.Empty, 
                "TemplateCode 为空");
            Assert.That(formData.TitleName, Is.Not.Null.Or.Empty, 
                "TitleName 为空");
            
            Console.WriteLine($"TemplateCode: {formData.TemplateCode}");
            Console.WriteLine($"TitleName: {formData.TitleName}");
            Console.WriteLine($"FormVarChaInfo: {formData.FormVarChaInfo}");
        }

        [Test]
        public async Task TestCreditRecordBppStart()
        {
            var listener = DIContainerContainer.Get<SMessageListenerForBPPStartWorkflow>();
            Guid entityId = Guid.Parse("2a0ff449-5227-ef11-be21-002248588e3d");
            string userAccount = "gw_qiuzw";
            
            JObject data2 = new JObject();
            data2.Add("EntityName", "mcs_credit_record");
            data2.Add("EntityID", entityId.ToString());
            data2.Add("UserAccount", userAccount);
            
            JObject data1 = new JObject();
            data1.Add("Data", JsonSerializerHelper.Serializer(data2));
            
            var message = new SMessage()
            {
                ID = Guid.NewGuid(),
                Type = MessageTypes.BPPStartWorkflow,
                Data = JsonSerializerHelper.Serializer(data1),
                Key = SMessageHelper.GenerateKey(MessageTypes.BPPStartWorkflow, Guid.NewGuid().ToString()),
                ExpectationExecuteTime = DateTime.UtcNow
            };
            
            Exception caughtException = null;
            ValidateResult result = null;
            
            try
            {
                result = await listener.Execute(message);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }
            
            Assert.That(caughtException, Is.Null,
                $"BPP Start 抛出异常: {caughtException?.Message}");
            Assert.That(result, Is.Not.Null,
                "BPP Start 返回 null");
            
            Console.WriteLine($"BPP Start Result: {result?.Result}, Description: {result?.Description}");
        }

        [Test]
        public async Task TestCreditRecordBppStartV2()
        {
            var bppService = DIContainerContainer.Get<IBPPService>();
            Guid entityId = Guid.Parse("2a0ff449-5227-ef11-be21-002248588e3d");
            string userAccount = "gw_qiuzw";
            
            var dto = new BPPDataDTO
            {
                EntityName = "mcs_credit_record",
                EntityID = entityId,
                UserAccount = userAccount
            };
            
            Exception caughtException = null;
            ValidateResult result = null;
            
            try
            {
                result = await bppService.Start_V2(dto);
            }
            catch (Exception ex)
            {
                caughtException = ex;
            }
            
            Assert.That(caughtException, Is.Null,
                $"BPPService.Start_V2 抛出异常: {caughtException?.Message}");
            Assert.That(result, Is.Not.Null,
                "BPPService.Start_V2 返回 null");
            
            Console.WriteLine($"Start_V2 Result: {result?.Result}, Description: {result?.Description}");
        }

    }
}
