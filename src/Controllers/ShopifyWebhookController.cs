using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ShopifyWhatsApp.Data;
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
    private readonly IShopifyService _shopify;
    private readonly AppDbContext _db;
    private readonly AbandonedCartSettings _abandonedCartSettings;
    private readonly ILogger<ShopifyWebhookController> _logger;

    public ShopifyWebhookController(
        IWhatsAppService whatsApp,
        ITemplateService templates,
        IConsentService consent,
        IShopifyService shopify,
        AppDbContext db,
        IOptions<AbandonedCartSettings> abandonedCartSettings,
        ILogger<ShopifyWebhookController> logger)
    {
        _whatsApp = whatsApp;
        _templates = templates;
        _consent = consent;
        _shopify = shopify;
        _db = db;
        _abandonedCartSettings = abandonedCartSettings.Value;
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

            // Cancel any pending abandoned cart messages for this customer
            var pendingMessages = await _db.ScheduledMessages
                .Where(sm => sm.PhoneNumber == phone && sm.Status == "pending")
                .ToListAsync();

            if (pendingMessages.Count > 0)
            {
                foreach (var pending in pendingMessages)
                {
                    pending.Status = "cancelled";
                    pending.ProcessedAt = DateTime.UtcNow;
                    pending.CancellationReason = "order_completed";
                }
                await _db.SaveChangesAsync();
                _logger.LogInformation("🚫 Cancelled {Count} pending abandoned cart message(s) for {Phone} — order completed",
                    pendingMessages.Count, phone);
            }

            // Build and send template message with button
            var templateMessage = _templates.GetOrderConfirmation(order);
            _logger.LogInformation("📋 Sending {Template} to {Phone} | Params: {Params}, Buttons: {Buttons}",
                templateMessage.TemplateName, phone,
                string.Join(", ", templateMessage.BodyParameters),
                templateMessage.ButtonParameters.Count);

            var result = await _whatsApp.SendTemplateMessageAsync(phone, templateMessage);

            // Log the message
            await _consent.LogMessageAsync(
                phone, templateMessage.TemplateName, "order_created",
                shopifyOrderId: order.Id,
                whatsAppMessageId: result?.Messages?.FirstOrDefault()?.Id,
                errorMessage: result == null ? "API call failed after retries" : null);

            return Ok(new { status = result != null ? "sent" : "failed", template = templateMessage.TemplateName });
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

            var templateMessage = _templates.GetShippingUpdate(order);
            _logger.LogInformation("📋 Sending {Template} to {Phone} | Params: {Params}, Buttons: {Buttons}",
                templateMessage.TemplateName, phone,
                string.Join(", ", templateMessage.BodyParameters),
                templateMessage.ButtonParameters.Count);

            var result = await _whatsApp.SendTemplateMessageAsync(phone, templateMessage);

            await _consent.LogMessageAsync(
                phone, templateMessage.TemplateName, "order_fulfilled",
                shopifyOrderId: order.Id,
                whatsAppMessageId: result?.Messages?.FirstOrDefault()?.Id,
                errorMessage: result == null ? "API call failed after retries" : null);

            return Ok(new { status = result != null ? "sent" : "failed", template = templateMessage.TemplateName });
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

            // Idempotency: skip if already scheduled for this checkout
            var alreadyScheduled = await _db.ScheduledMessages
                .AnyAsync(sm => sm.ShopifyCheckoutId == checkout.Id && sm.Status == "pending");

            if (alreadyScheduled)
            {
                _logger.LogInformation("⏭️ Checkout {CheckoutId} already has a pending message, skipping", checkout.Id);
                return Ok(new { status = "skipped", reason = "already_scheduled" });
            }

            // Build template NOW (payload won't be available later)
            var templateMessage = _templates.GetAbandonedCart(checkout);
            var templateJson = JsonConvert.SerializeObject(templateMessage);

            // Schedule instead of sending immediately
            var scheduledMessage = new ScheduledMessage
            {
                PhoneNumber = phone,
                ShopifyCheckoutId = checkout.Id,
                CheckoutToken = checkout.Token ?? string.Empty,
                MessageType = "abandoned_cart",
                TemplatePayloadJson = templateJson,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                ScheduledFor = DateTime.UtcNow.AddMinutes(_abandonedCartSettings.DelayMinutes)
            };

            _db.ScheduledMessages.Add(scheduledMessage);
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "⏰ Scheduled abandoned cart for {Phone} at {ScheduledFor} ({DelayMin}m delay, checkout {CheckoutId})",
                phone, scheduledMessage.ScheduledFor, _abandonedCartSettings.DelayMinutes, checkout.Id);

            return Ok(new { status = "scheduled", scheduledFor = scheduledMessage.ScheduledFor });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💀 CRITICAL: CheckoutAbandoned handler crashed");
            return Ok(new { status = "error", message = ex.Message });
        }
    }

    // ──────────────────────────────────────────────
    // POST /api/webhooks/shopify/order-refund-created
    // ──────────────────────────────────────────────
    [HttpPost("order-refund-created")]
    public async Task<IActionResult> OrderRefundCreated()
    {
        try
        {
            var refund = await ReadBody<ShopifyRefundPayload>();
            if (refund == null) return Ok(new { status = "skipped", reason = "invalid_payload" });

            _logger.LogInformation("💰 Refund received: refund {RefundId} for order {OrderId}",
                refund.Id, refund.OrderId);

            // Fetch full order details from Shopify API (refund webhook doesn't include them)
            var order = await _shopify.GetOrderByIdAsync(refund.OrderId);
            if (order == null)
            {
                _logger.LogError("❌ Could not fetch order {OrderId} for refund {RefundId}",
                    refund.OrderId, refund.Id);
                return Ok(new { status = "failed", reason = "order_fetch_failed" });
            }

            var phone = GetCustomerPhone(order.Customer, order.ShippingAddress);
            if (phone == null)
            {
                _logger.LogWarning("⚠️ No phone number for refund on order {OrderName}", order.Name);
                return Ok(new { status = "skipped", reason = "no_phone" });
            }

            // Auto-consent: customer provided phone at checkout
            if (!await _consent.HasConsentAsync(phone))
            {
                await _consent.RecordConsentAsync(phone, "checkout", order.Customer?.Id);
                _logger.LogInformation("✅ Auto-consented {Phone} via checkout", phone);
            }

            // Calculate refund amount from transactions
            var refundAmount = refund.Transactions
                .Where(t => t.Kind.Equals("refund", StringComparison.OrdinalIgnoreCase))
                .Sum(t => decimal.TryParse(t.Amount, out var amt) ? amt : 0m);

            // Fallback: if no refund transactions, try summing line item subtotals
            if (refundAmount == 0)
            {
                refundAmount = refund.RefundLineItems
                    .Sum(rli => decimal.TryParse(rli.Subtotal, out var sub) ? sub : 0m);
            }

            var currency = refund.Transactions.FirstOrDefault()?.Currency ?? order.Currency;
            var reason = refund.Note ?? "Refund processed";

            var templateMessage = _templates.GetOrderRefund(order, refundAmount.ToString("F2"), currency, reason);
            _logger.LogInformation("📋 Sending {Template} to {Phone} | Params: {Params}, Buttons: {Buttons}",
                templateMessage.TemplateName, phone,
                string.Join(", ", templateMessage.BodyParameters),
                templateMessage.ButtonParameters.Count);

            var result = await _whatsApp.SendTemplateMessageAsync(phone, templateMessage);

            await _consent.LogMessageAsync(
                phone, templateMessage.TemplateName, "order_refund_created",
                shopifyOrderId: order.Id,
                whatsAppMessageId: result?.Messages?.FirstOrDefault()?.Id,
                errorMessage: result == null ? "API call failed after retries" : null);

            return Ok(new { status = result != null ? "sent" : "failed", template = templateMessage.TemplateName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💀 CRITICAL: OrderRefundCreated handler crashed");
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
