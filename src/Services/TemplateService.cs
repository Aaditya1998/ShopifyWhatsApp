using ShopifyWhatsApp.Models;

namespace ShopifyWhatsApp.Services;

public interface ITemplateService
{
    (string templateName, List<string> parameters) GetOrderConfirmation(ShopifyOrderPayload order);
    (string templateName, List<string> parameters) GetShippingUpdate(ShopifyOrderPayload order);
    (string templateName, List<string> parameters) GetOrderCancelled(ShopifyOrderPayload order);
    (string templateName, List<string> parameters) GetAbandonedCart(ShopifyCheckoutPayload checkout);
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
/// 4. Wait for approval (usually 24-48 hours)
/// </summary>
public class TemplateService : ITemplateService
{
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(ILogger<TemplateService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Template: order_confirmation
    /// Body: "Hi {{1}}, your order {{2}} has been confirmed! 🎉\nItems: {{3}}\nTotal: {{4}} {{5}}\nThank you for shopping with us!"
    /// Category: UTILITY
    /// </summary>
    public (string templateName, List<string> parameters) GetOrderConfirmation(ShopifyOrderPayload order)
    {
        var customerName = order.Customer?.FullName ?? "Customer";
        var orderName = order.Name; // e.g., "#1001"
        var itemSummary = GetItemSummary(order.LineItems);
        var total = order.TotalPrice;
        var currency = order.Currency;

        _logger.LogInformation("📋 Building order_confirmation template for order {Order}", orderName);

        return ("order_confirmation", new List<string>
        {
            customerName,
            orderName,
            itemSummary,
            total,
            currency
        });
    }

    /// <summary>
    /// Template: shipping_update
    /// Body: "Great news! Your order {{1}} has shipped! 📦\nCarrier: {{2}}\nTracking: {{3}}\nTrack your package here: {{4}}"
    /// Category: UTILITY
    /// </summary>
    public (string templateName, List<string> parameters) GetShippingUpdate(ShopifyOrderPayload order)
    {
        var fulfillment = order.Fulfillments.FirstOrDefault();
        var orderName = order.Name;
        var carrier = fulfillment?.TrackingCompany ?? "Carrier";
        var trackingNumber = fulfillment?.TrackingNumber ?? "N/A";
        var trackingUrl = fulfillment?.TrackingUrl ?? "Contact us for tracking info";

        _logger.LogInformation("📋 Building shipping_update template for order {Order}", orderName);

        return ("shipping_update", new List<string>
        {
            orderName,
            carrier,
            trackingNumber,
            trackingUrl
        });
    }

    /// <summary>
    /// Template: order_cancelled
    /// Body: "Hi {{1}}, your order {{2}} has been cancelled.\nReason: {{3}}\nIf you have questions, reply to this message."
    /// Category: UTILITY
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
    /// Template: abandoned_checkout
    /// Body: "Hi {{1}}, you left items in your cart. Complete your purchase: {{2}}"
    /// Category: MARKETING (requires marketing opt-in)
    /// </summary>
    public (string templateName, List<string> parameters) GetAbandonedCart(ShopifyCheckoutPayload checkout)
    {
        var customerName = checkout.Customer?.FullName ?? "there";
        var itemSummary = GetItemSummary(checkout.LineItems);
        var total = checkout.TotalPrice;
        var currency = checkout.Currency;
        var checkoutUrl = checkout.AbandonedCheckoutUrl;

        _logger.LogInformation("📋 Building abandoned_checkout template for checkout {CheckoutId}", checkout.Id);

        return ("abandoned_checkout", new List<string>
        {
            customerName,
            itemSummary,
            total,
            currency,
            checkoutUrl
        });
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

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
