using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;
using System;
using System.IO;

namespace D365ToolCommon.Translation
{
    /// <summary>
    /// D365 多语言翻译导入导出通用服务。
    /// 封装 ImportTranslation / ExportTranslation 标准消息，供 MetadataTool、DeployTool 等复用。
    /// </summary>
    public class TranslationService
    {
        private readonly ServiceClient _service;

        public TranslationService(ServiceClient service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// 导入 translations ZIP（等效于 D365 UI 的 Import Translations）。
        /// </summary>
        public void ImportTranslations(string translationZipPath)
        {
            if (!File.Exists(translationZipPath))
            {
                throw new FileNotFoundException("Translations ZIP 文件不存在", translationZipPath);
            }

            Console.WriteLine($"导入 translations: {translationZipPath}");
            var request = new ImportTranslationRequest
            {
                TranslationFile = File.ReadAllBytes(translationZipPath)
            };
            _service.Execute(request);
            Console.WriteLine("  ✓ translations 导入完成");
        }

        /// <summary>
        /// 导出 translations ZIP（等效于 D365 UI 的 Export Translations）。
        /// </summary>
        public void ExportTranslations(string solutionUniqueName, string outputZipPath)
        {
            if (string.IsNullOrWhiteSpace(solutionUniqueName))
            {
                throw new ArgumentException("Solution 唯一名称不能为空", nameof(solutionUniqueName));
            }

            Console.WriteLine($"导出 translations: Solution={solutionUniqueName}, 输出={outputZipPath}");
            var request = new ExportTranslationRequest
            {
                SolutionName = solutionUniqueName
            };
            var response = (ExportTranslationResponse)_service.Execute(request);
            File.WriteAllBytes(outputZipPath, response.ExportTranslationFile);
            Console.WriteLine($"  ✓ translations 导出完成，大小 {response.ExportTranslationFile.Length} bytes");
        }
    }
}
