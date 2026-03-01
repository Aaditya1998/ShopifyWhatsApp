using Microsoft.Extensions.Options;
using ShopifyWhatsApp.Models;

namespace ShopifyWhatsApp.Services;

public interface ITemplateService
{
    TemplateMessage GetOrderConfirmation(ShopifyOrderPayload order);
    TemplateMessage GetShippingUpdate(ShopifyOrderPayload order);
    (string templateName, List<string> parameters) GetOrderCancelled(ShopifyOrderPayload order);
    TemplateMessage GetAbandonedCart(ShopifyCheckoutPayload checkout);
}

/// <summary>
/// Maps Shopify events to WhatsApp message template names and parameters.
///
/// IMPORTANT: You must create these templates in Meta Business Manager
/// and get them approved BEFORE they'll work.
///
/// Template creation guide:
/// 1. Go to https://business.facebook.com → WhatsApp Manager → Message Templates
/// 2. Create templates with the exact names below
/// 3. Use {{1}}, {{2}}, etc. as placeholders matching the parameter order
/// 4. For URL buttons, set button type to "Dynamic" with URL {{1}}
/// 5. Wait for approval (usually 24-48 hours)
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly ILogger<TemplateService> _logger;
    private readonly string _storeBaseUrl;

    public TemplateService(ILogger<TemplateService> logger, IOptions<ShopifySettings> shopifySettings)
    {
        _logger = logger;
        _storeBaseUrl = $"https://{shopifySettings.Value.ShopDomain}/";
    }

    /// <summary>
    /// Template: order_confirmation_v2
    /// Body: "Hi {{1}}, your order {{2}} has been confirmed! Items: {{3}} Total: {{4}} {{5}}"
    /// Button[0]: URL "View Order" → dynamic {{1}}
    /// Category: UTILITY
    /// </summary>
    public TemplateMessage GetOrderConfirmation(ShopifyOrderPayload order)
    {
        var customerName = order.Customer?.FullName ?? "Customer";
        var orderName = order.Name;
        var itemSummary = GetItemSummary(order.LineItems);
        var total = order.TotalPrice;
        var currency = order.Currency;

        _logger.LogInformation("📋 Building order_confirmation_v2 template for order {Order}", orderName);

        var bodyParams = new List<string> { customerName, orderName, itemSummary, total, currency };

        var buttonParams = new List<TemplateButtonParameter>();
        var urlSuffix = GetUrlSuffix(order.OrderStatusUrl);
        if (urlSuffix != null)
        {
            buttonParams.Add(new TemplateButtonParameter(0, urlSuffix));
        }

        return new TemplateMessage("order_confirmation_v2", bodyParams, buttonParams);
    }

    /// <summary>
    /// Template: shipping_update_v2
    /// Body: "Your order {{1}} has shipped! Carrier: {{2}} Tracking: {{3}}"
    /// Button[0]: URL "Track Shipment" → dynamic {{1}}
    /// Category: UTILITY
    /// </summary>
    public TemplateMessage GetShippingUpdate(ShopifyOrderPayload order)
    {
        var fulfillment = order.Fulfillments.FirstOrDefault();
        var orderName = order.Name;
        var carrier = fulfillment?.TrackingCompany ?? "Carrier";
        var trackingNumber = fulfillment?.TrackingNumber ?? "N/A";
        var trackingUrl = fulfillment?.TrackingUrl;

        _logger.LogInformation("📋 Building shipping_update_v2 template for order {Order}", orderName);

        var bodyParams = new List<string> { orderName, carrier, trackingNumber };

        var buttonParams = new List<TemplateButtonParameter>();
        var urlSuffix = GetUrlSuffix(trackingUrl);
        if (urlSuffix != null)
        {
            buttonParams.Add(new TemplateButtonParameter(0, urlSuffix));
        }

        return new TemplateMessage("shipping_update_v2", bodyParams, buttonParams);
    }

    /// <summary>
    /// Template: order_cancelled
    /// Body: "Hi {{1}}, your order {{2}} has been cancelled. Reason: {{3}}. Reply with questions."
    /// Category: UTILITY (no buttons)
    /// </summary>
    public (string templateName, List<string> parameters) GetOrderCancelled(ShopifyOrderPayload order)
    {
        var customerName = order.Customer?.FullName ?? "Customer";
        var orderName = order.Name;
        var reason = order.CancelReason ?? "At your request";

        _logger.LogInformation("📋 Building order_cancelled template for order {Order}", orderName);

        return ("order_cancelled", new List<string>
        {
            customerName,
            orderName,
            reason
        });
    }

    /// <summary>
    /// Template: abandoned_checkout_v2
    /// Body: "Hi {{1}}, you left items in your cart! Items: {{2}} Total: {{3}} {{4}}"
    /// Button[0]: URL "Complete Purchase" → dynamic {{1}}
    /// Category: MARKETING (requires marketing opt-in)
    /// </summary>
    public TemplateMessage GetAbandonedCart(ShopifyCheckoutPayload checkout)
    {
        var customerName = checkout.Customer?.FullName ?? "there";
        var itemSummary = GetItemSummary(checkout.LineItems);
        var total = checkout.TotalPrice;
        var currency = checkout.Currency;
        var checkoutUrl = checkout.AbandonedCheckoutUrl;

        _logger.LogInformation("📋 Building abandoned_checkout_v2 template for checkout {CheckoutId}", checkout.Id);

        var bodyParams = new List<string> { customerName, itemSummary, total, currency };

        var buttonParams = new List<TemplateButtonParameter>();
        var urlSuffix = GetUrlSuffix(checkoutUrl);
        if (urlSuffix != null)
        {
            buttonParams.Add(new TemplateButtonParameter(0, urlSuffix));
        }

        return new TemplateMessage("abandoned_checkout_v2", bodyParams, buttonParams);
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    /// <summary>
    /// Strips the store base URL from a full URL to get just the dynamic suffix
    /// for WhatsApp template URL buttons.
    /// Template URL is "https://mulcart.myshopify.com/{{1}}" so we send only the path.
    /// </summary>
    private string? GetUrlSuffix(string? fullUrl)
    {
        if (string.IsNullOrWhiteSpace(fullUrl)) return null;

        // Strip store base URL to get just the path
        if (fullUrl.StartsWith(_storeBaseUrl, StringComparison.OrdinalIgnoreCase))
            return fullUrl[_storeBaseUrl.Length..];

        // For external URLs (e.g., carrier tracking), extract path+query
        if (Uri.TryCreate(fullUrl, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("Button URL {Url} doesn't match store base {Base}, extracting path",
                fullUrl, _storeBaseUrl);
            return uri.PathAndQuery.TrimStart('/');
        }

        return fullUrl;
    }

    private static string GetItemSummary(List<ShopifyLineItem> items)
    {
        if (items == null || items.Count == 0) return "Your items";

        var summaries = items.Take(3).Select(i => $"{i.Title} x{i.Quantity}");
        var summary = string.Join(", ", summaries);

        if (items.Count > 3)
            summary += $" +{items.Count - 3} more";

        // WhatsApp template parameters have a max length of 1024 chars
        if (summary.Length > 200)
            summary = summary[..197] + "...";

        return summary;
    }
}
