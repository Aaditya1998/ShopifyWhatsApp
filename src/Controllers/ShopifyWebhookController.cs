using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using ShopifyWhatsApp.Middleware;
using ShopifyWhatsApp.Models;
using ShopifyWhatsApp.Services;

namespace ShopifyWhatsApp.Controllers;

[ApiController]
[Route("api/webhooks/shopify")]
[ShopifyHmacValidation]
public class ShopifyWebhookController : ControllerBase
{
    private readonly IWhatsAppService _whatsApp;
    private readonly ITemplateService _templates;
    private readonly IConsentService _consent;
    private readonly ILogger<ShopifyWebhookController> _logger;

    public ShopifyWebhookController(
        IWhatsAppService whatsApp,
        ITemplateService templates,
        IConsentService consent,
        ILogger<ShopifyWebhookController> logger)
    {
        _whatsApp = whatsApp;
        _templates = templates;
        _consent = consent;
        _logger = logger;
    }

    // ──────────────────────────────────────────────
    // POST /api/webhooks/shopify/order-created
    // ──────────────────────────────────────────────
    [HttpPost("order-created")]
    public async Task<IActionResult> OrderCreated()
    {
        try
        {
            var order = await ReadBody<ShopifyOrderPayload>();
            if (order == null) return Ok(new { status = "skipped", reason = "invalid_payload" });

            _logger.LogInformation("📦 New order received: {OrderName} from {Customer}",
                order.Name, order.Customer?.FullName);

            var phone = GetCustomerPhone(order.Customer, order.ShippingAddress);
            if (phone == null)
            {
                _logger.LogWarning("⚠️ No phone number for order {OrderName}", order.Name);
                return Ok(new { status = "skipped", reason = "no_phone" });
            }

            // Auto-consent: customer provided phone at checkout
            if (!await _consent.HasConsentAsync(phone))
            {
                await _consent.RecordConsentAsync(phone, "checkout", order.Customer?.Id);
                _logger.LogInformation("✅ Auto-consented {Phone} via checkout", phone);
            }

            // Build and send template message
            var (templateName, parameters) = _templates.GetOrderConfirmation(order);
            _logger.LogInformation("📋 Sending {Template} to {Phone} | Params: {Params}",
                templateName, phone, string.Join(", ", parameters));

            var result = await _whatsApp.SendTemplateMessageAsync(phone, templateName, parameters);

            // Log the message
            await _consent.LogMessageAsync(
                phone, templateName, "order_created",
                shopifyOrderId: order.Id,
                whatsAppMessageId: result?.Messages?.FirstOrDefault()?.Id,
                errorMessage: result == null ? "API call failed after retries" : null);

            return Ok(new { status = result != null ? "sent" : "failed", template = templateName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💀 CRITICAL: OrderCreated handler crashed");
            return Ok(new { status = "error", message = ex.Message });
        }
    }

    // ──────────────────────────────────────────────
    // POST /api/webhooks/shopify/order-fulfilled
    // ──────────────────────────────────────────────
    [HttpPost("order-fulfilled")]
    public async Task<IActionResult> OrderFulfilled()
    {
        try
        {
            var order = await ReadBody<ShopifyOrderPayload>();
            if (order == null) return Ok(new { status = "skipped", reason = "invalid_payload" });

            _logger.LogInformation("🚚 Order fulfilled: {OrderName}", order.Name);

            var phone = GetCustomerPhone(order.Customer, order.ShippingAddress);
            if (phone == null) return Ok(new { status = "skipped", reason = "no_phone" });
            if (!await _consent.HasConsentAsync(phone))
            {
                await _consent.RecordConsentAsync(phone, "checkout", order.Customer?.Id);
                _logger.LogInformation("✅ Auto-consented {Phone} via checkout", phone);
            }

            var (templateName, parameters) = _templates.GetShippingUpdate(order);
            _logger.LogInformation("📋 Sending {Template} to {Phone} | Params: {Params}",
                templateName, phone, string.Join(", ", parameters));

            var result = await _whatsApp.SendTemplateMessageAsync(phone, templateName, parameters);

            await _consent.LogMessageAsync(
                phone, templateName, "order_fulfilled",
                shopifyOrderId: order.Id,
                whatsAppMessageId: result?.Messages?.FirstOrDefault()?.Id,
                errorMessage: result == null ? "API call failed after retries" : null);

            return Ok(new { status = result != null ? "sent" : "failed", template = templateName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💀 CRITICAL: OrderFulfilled handler crashed");
            return Ok(new { status = "error", message = ex.Message });
        }
    }

    // ──────────────────────────────────────────────
    // POST /api/webhooks/shopify/order-cancelled
    // ──────────────────────────────────────────────
    [HttpPost("order-cancelled")]
    public async Task<IActionResult> OrderCancelled()
    {
        try
        {
            var order = await ReadBody<ShopifyOrderPayload>();
            if (order == null) return Ok(new { status = "skipped", reason = "invalid_payload" });

            _logger.LogInformation("❌ Order cancelled: {OrderName}", order.Name);

            var phone = GetCustomerPhone(order.Customer, order.ShippingAddress);
            if (phone == null) return Ok(new { status = "skipped", reason = "no_phone" });
            if (!await _consent.HasConsentAsync(phone))
            {
                await _consent.RecordConsentAsync(phone, "checkout", order.Customer?.Id);
                _logger.LogInformation("✅ Auto-consented {Phone} via checkout", phone);
            }

            var (templateName, parameters) = _templates.GetOrderCancelled(order);
            _logger.LogInformation("📋 Sending {Template} to {Phone} | Params: {Params}",
                templateName, phone, string.Join(", ", parameters));

            var result = await _whatsApp.SendTemplateMessageAsync(phone, templateName, parameters);

            await _consent.LogMessageAsync(
                phone, templateName, "order_cancelled",
                shopifyOrderId: order.Id,
                whatsAppMessageId: result?.Messages?.FirstOrDefault()?.Id,
                errorMessage: result == null ? "API call failed after retries" : null);

            return Ok(new { status = result != null ? "sent" : "failed", template = templateName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💀 CRITICAL: OrderCancelled handler crashed");
            return Ok(new { status = "error", message = ex.Message });
        }
    }

    // ──────────────────────────────────────────────
    // POST /api/webhooks/shopify/checkout-abandoned
    // ──────────────────────────────────────────────
    [HttpPost("checkout-abandoned")]
    public async Task<IActionResult> CheckoutAbandoned()
    {
        try
        {
            var checkout = await ReadBody<ShopifyCheckoutPayload>();
            if (checkout == null) return Ok(new { status = "skipped", reason = "invalid_payload" });

            _logger.LogInformation("🛒 Abandoned checkout: {CheckoutId}", checkout.Id);

            var phone = checkout.Customer?.Phone;
            if (string.IsNullOrWhiteSpace(phone))
            {
                _logger.LogWarning("⚠️ No phone for checkout {CheckoutId}", checkout.Id);
                return Ok(new { status = "skipped", reason = "no_phone" });
            }

            // Normalize phone
            phone = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
            if (phone.StartsWith("+")) phone = phone[1..];

            if (!await _consent.HasConsentAsync(phone))
            {
                await _consent.RecordConsentAsync(phone, "checkout");
                _logger.LogInformation("✅ Auto-consented {Phone} via checkout", phone);
            }

            var (templateName, parameters) = _templates.GetAbandonedCart(checkout);
            _logger.LogInformation("📋 Sending {Template} to {Phone} | Params: {Params}",
                templateName, phone, string.Join(", ", parameters));

            var result = await _whatsApp.SendTemplateMessageAsync(phone, templateName, parameters);

            await _consent.LogMessageAsync(
                phone, templateName, "checkout_abandoned",
                whatsAppMessageId: result?.Messages?.FirstOrDefault()?.Id,
                errorMessage: result == null ? "API call failed after retries" : null);

            return Ok(new { status = result != null ? "sent" : "failed", template = templateName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💀 CRITICAL: CheckoutAbandoned handler crashed");
            return Ok(new { status = "error", message = ex.Message });
        }
    }

    // ──────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────

    private async Task<T?> ReadBody<T>() where T : class
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();
            _logger.LogDebug("📥 Raw webhook payload: {Body}", body);
            return JsonConvert.DeserializeObject<T>(body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize webhook payload");
            return null;
        }
    }

    /// <summary>
    /// Try to get customer phone from multiple sources
    /// </summary>
    private static string? GetCustomerPhone(ShopifyCustomer? customer, ShopifyAddress? shippingAddress)
    {
        // Try customer phone first, then shipping address phone
        var phone = customer?.Phone ?? shippingAddress?.Phone;

        if (string.IsNullOrWhiteSpace(phone)) return null;

        // Normalize: remove spaces, dashes, ensure no leading +
        phone = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (phone.StartsWith("+")) phone = phone[1..];

        return phone;
    }
}
