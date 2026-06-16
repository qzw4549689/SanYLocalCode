using SanyD365.Plugins.CofaceIntegration.Api;
using System;

namespace CheckCofaceOrders
{
    class Program
    {
        static void Main(string[] args)
        {
            string cofaceId = "icon#5415240";
            string countryCode = "PL";
            
            Console.WriteLine($"查询 Coface 订单: cofaceId={cofaceId}, countryCode={countryCode}");
            
            // 这里需要实际调用 API，但 Token 管理比较复杂
            // 先输出查询参数，确认是否正确
            Console.WriteLine("请确认查询参数是否正确");
        }
    }
}
