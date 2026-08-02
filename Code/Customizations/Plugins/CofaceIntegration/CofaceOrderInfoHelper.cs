using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace SanyD365.Plugins.CofaceIntegration
{
    /// <summary>
    /// Coface 订单信息提取帮助类
    /// 从 CofaceDataSyncPlugin 抽取的「订单列表 JSON → 就绪判定 + publicationId 提取」公共逻辑，
    /// 供数据集成插件与下单插件（CofacePlaceOrderPlugin）复用
    /// </summary>
    internal static class CofaceOrderInfoHelper
    {
        /// <summary>
        /// URBA360订单状态
        /// </summary>
        internal enum UrbaOrderStatus
        {
            NotFound,       // 未找到订单
            NotReady,       // 订单存在但状态未就绪
            Ready           // 订单就绪可取数
        }

        /// <summary>
        /// URBA360订单信息
        /// </summary>
        internal class UrbaOrderInfo
        {
            public string OrderId { get; set; }
            public UrbaOrderStatus Status { get; set; }
            public string StatusDetail { get; set; }
        }

        /// <summary>
        /// Full Report订单状态
        /// </summary>
        internal enum ReportOrderStatus
        {
            NotFound,       // 未找到订单
            NotReady,       // 订单存在但状态未就绪
            Ready           // 订单就绪可取数
        }

        /// <summary>
        /// Full Report订单信息
        /// </summary>
        internal class ReportOrderInfo
        {
            public string OrderId { get; set; }
            public string PublicationId { get; set; }
            public ReportOrderStatus Status { get; set; }
            public string StatusDetail { get; set; }
        }

        /// <summary>
        /// 从订单列表中提取URBA360订单信息（包含状态判断）
        /// </summary>
        internal static UrbaOrderInfo ExtractUrbaOrderInfo(JsonDocument ordersDoc, ITracingService tracer)
        {
            var result = new UrbaOrderInfo { Status = UrbaOrderStatus.NotFound };

            try
            {
                var root = ordersDoc.RootElement;
                List<JsonElement> orders = new List<JsonElement>();

                // 收集所有订单
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var order in root.EnumerateArray())
                        orders.Add(order);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("orders", out var ordersArray))
                    {
                        foreach (var order in ordersArray.EnumerateArray())
                            orders.Add(order);
                    }
                    else if (root.TryGetProperty("id", out _))
                    {
                        orders.Add(root);
                    }
                }

                if (orders.Count == 0)
                {
                    tracer.Trace("订单列表为空，该客户尚未下单");
                    return result;
                }

                // 检查每个订单的状态
                foreach (var order in orders)
                {
                    string orderId = null;
                    string statusStr = null;

                    if (order.TryGetProperty("id", out var idProp))
                        orderId = idProp.GetString();

                    // URBA360 订单状态字段是 "urbaStatus"，不是 "status"
                    if (order.TryGetProperty("urbaStatus", out var status))
                        statusStr = status.GetString();
                    else if (order.TryGetProperty("status", out var status2))
                        statusStr = status2.GetString();

                    tracer.Trace($"检查订单: id={orderId}, status={statusStr}");

                    // 状态为ready或partially_ready → 订单就绪
                    if (statusStr == "ready" || statusStr == "partially_ready")
                    {
                        result.OrderId = orderId;
                        result.Status = UrbaOrderStatus.Ready;
                        result.StatusDetail = statusStr;
                        return result;
                    }
                    // 状态为空或不存在，但有id → 兼容处理，视为就绪（Coface某些环境不返回status）
                    else if (string.IsNullOrEmpty(statusStr) && !string.IsNullOrEmpty(orderId))
                    {
                        tracer.Trace($"订单status为空，按兼容逻辑视为就绪");
                        result.OrderId = orderId;
                        result.Status = UrbaOrderStatus.Ready;
                        result.StatusDetail = "empty_status";
                        return result;
                    }
                    // 状态存在但不为ready → 未就绪
                    else if (!string.IsNullOrEmpty(orderId))
                    {
                        result.OrderId = orderId;
                        result.Status = UrbaOrderStatus.NotReady;
                        result.StatusDetail = statusStr ?? "unknown";
                    }
                }

                // 有订单但都不是就绪状态
                if (result.Status == UrbaOrderStatus.NotReady)
                {
                    tracer.Trace($"找到{orders.Count}个订单，但状态均未就绪");
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"提取URBA订单信息异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 从订单列表中提取Full Report订单信息（包含状态判断）
        /// 只匹配指定 reportSlug 和 customReportId 的 publication
        /// </summary>
        internal static ReportOrderInfo ExtractReportOrderInfo(JsonDocument ordersDoc, ITracingService tracer, string expectedSlug, string expectedProductCode)
        {
            var result = new ReportOrderInfo { Status = ReportOrderStatus.NotFound };

            try
            {
                var root = ordersDoc.RootElement;
                List<JsonElement> orders = new List<JsonElement>();

                // 收集所有订单
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var order in root.EnumerateArray())
                        orders.Add(order);
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("orders", out var ordersArray))
                    {
                        foreach (var order in ordersArray.EnumerateArray())
                            orders.Add(order);
                    }
                    else if (root.TryGetProperty("id", out _))
                    {
                        orders.Add(root);
                    }
                }

                if (orders.Count == 0)
                {
                    tracer.Trace("订单列表为空，该客户尚未下单");
                    return result;
                }

                string normalizedExpectedSlug = expectedSlug?.ToUpperInvariant();
                string normalizedExpectedProductCode = expectedProductCode?.ToUpperInvariant();

                // 检查每个订单的状态
                foreach (var order in orders)
                {
                    string statusStr = null;
                    string orderId = null;
                    string matchedPubId = null;

                    if (order.TryGetProperty("status", out var status))
                        statusStr = status.GetString();

                    // 获取订单ID
                    if (order.TryGetProperty("id", out var idProp))
                    {
                        orderId = idProp.GetString();
                    }

                    // 从publications数组中匹配符合 expectedSlug + expectedProductCode 的 Publication
                    if (order.TryGetProperty("publications", out var publications))
                    {
                        foreach (var pub in publications.EnumerateArray())
                        {
                            string pubSlug = null;
                            if (pub.TryGetProperty("reportSlug", out var slugProp))
                                pubSlug = slugProp.GetString();

                            string pubProductCode = null;
                            if (pub.TryGetProperty("customReportId", out var codeProp))
                                pubProductCode = codeProp.ToString();

                            // 匹配规则：slug 必须一致；如果期望 productCode 不为空，则 productCode 也必须一致
                            bool slugMatch = !string.IsNullOrEmpty(pubSlug) &&
                                             pubSlug.ToUpperInvariant() == normalizedExpectedSlug;
                            bool codeMatch = string.IsNullOrEmpty(normalizedExpectedProductCode) ||
                                             (!string.IsNullOrEmpty(pubProductCode) &&
                                              pubProductCode.ToUpperInvariant() == normalizedExpectedProductCode);

                            if (slugMatch && codeMatch)
                            {
                                if (pub.TryGetProperty("id", out var pubIdProp))
                                {
                                    matchedPubId = pubIdProp.GetString();
                                    tracer.Trace($"匹配到Publication: slug={pubSlug}, customReportId={pubProductCode}, pubId={matchedPubId}");
                                    break;
                                }
                            }
                        }
                    }

                    // 兼容旧格式：如果订单本身没有 publications，但 reportSlug / customReportId 在 order 层级
                    if (string.IsNullOrEmpty(matchedPubId))
                    {
                        string orderSlug = null;
                        string orderProductCode = null;
                        if (order.TryGetProperty("reportSlug", out var orderSlugProp))
                            orderSlug = orderSlugProp.GetString();
                        if (order.TryGetProperty("customReportId", out var orderCodeProp))
                            orderProductCode = orderCodeProp.ToString();

                        bool slugMatch = !string.IsNullOrEmpty(orderSlug) &&
                                         orderSlug.ToUpperInvariant() == normalizedExpectedSlug;
                        bool codeMatch = string.IsNullOrEmpty(normalizedExpectedProductCode) ||
                                         (!string.IsNullOrEmpty(orderProductCode) &&
                                          orderProductCode.ToUpperInvariant() == normalizedExpectedProductCode);

                        if (slugMatch && codeMatch && !string.IsNullOrEmpty(orderId))
                        {
                            matchedPubId = orderId;
                            tracer.Trace($"订单层级匹配到产品: slug={orderSlug}, customReportId={orderProductCode}, pubId={matchedPubId}");
                        }
                    }

                    // 如果都没匹配到，跳过该订单
                    if (string.IsNullOrEmpty(matchedPubId))
                    {
                        tracer.Trace($"订单 {orderId} 未匹配到指定产品，跳过");
                        continue;
                    }

                    tracer.Trace($"检查订单: orderId={orderId}, pubId={matchedPubId}, status={statusStr}");

                    // 状态为ready或delivered → 订单就绪
                    if (statusStr == "ready" || statusStr == "delivered")
                    {
                        result.OrderId = orderId;
                        result.PublicationId = matchedPubId;
                        result.Status = ReportOrderStatus.Ready;
                        result.StatusDetail = statusStr;
                        return result;
                    }
                    // 状态为空或不存在，但有orderId → 兼容处理，视为就绪
                    else if (string.IsNullOrEmpty(statusStr) && !string.IsNullOrEmpty(orderId))
                    {
                        tracer.Trace($"订单status为空，按兼容逻辑视为就绪");
                        result.OrderId = orderId;
                        result.PublicationId = matchedPubId;
                        result.Status = ReportOrderStatus.Ready;
                        result.StatusDetail = "empty_status";
                        return result;
                    }
                    // 状态存在但不为ready → 未就绪
                    else if (!string.IsNullOrEmpty(orderId))
                    {
                        result.OrderId = orderId;
                        result.PublicationId = matchedPubId;
                        result.Status = ReportOrderStatus.NotReady;
                        result.StatusDetail = statusStr ?? "unknown";
                    }
                }

                // 有订单但都不是就绪状态，或没有匹配产品的订单
                if (result.Status == ReportOrderStatus.NotReady)
                {
                    tracer.Trace($"找到{orders.Count}个订单，但状态均未就绪");
                }
                else
                {
                    tracer.Trace($"找到{orders.Count}个订单，但没有匹配指定产品的就绪订单");
                }
            }
            catch (Exception ex)
            {
                tracer.Trace($"提取Report订单信息异常: {ex.Message}");
            }

            return result;
        }
    }
}
