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
[Route("api/webhooks/whatsapp")]
public class WhatsAppWebhookController : ControllerBase
{
    private readonly IWhatsAppService _whatsApp;
    private readonly IConsentService _consent;
    private readonly AppDbContext _db;
    private readonly WhatsAppSettings _settings;
    private readonly ILogger<WhatsAppWebhookController> _logger;

    public WhatsAppWebhookController(
        IWhatsAppService whatsApp,
        IConsentService consent,
        AppDbContext db,
        IOptions<WhatsAppSettings> settings,
        ILogger<WhatsAppWebhookController> logger)
    {
        _whatsApp = whatsApp;
        _consent = consent;
        _db = db;
        _settings = settings.Value;
        _logger = logger;
    }

    // ──────────────────────────────────────────────
    // GET /api/webhooks/whatsapp
    // Meta verification endpoint (called once during webhook setup)
    // ──────────────────────────────────────────────
    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        _logger.LogInformation("🔐 WhatsApp webhook verification request received");

        if (mode == "subscribe" && verifyToken == _settings.VerifyToken)
        {
            _logger.LogInformation("✅ WhatsApp webhook verified successfully");
            return Ok(challenge);
        }

        _logger.LogWarning("⚠️ WhatsApp webhook verification failed. Token mismatch.");
        return Forbid();
    }

    // ──────────────────────────────────────────────
    // POST /api/webhooks/whatsapp
    // Receives incoming messages from customers
    // ──────────────────────────────────────────────
    [HttpPost]
    [WhatsAppSignatureValidation]
    public async Task<IActionResult> ReceiveMessage()
    {
        string body;
        using (var reader = new StreamReader(Request.Body))
        {
            body = await reader.ReadToEndAsync();
        }

        var payload = JsonConvert.DeserializeObject<WhatsAppWebhookPayload>(body);
        if (payload == null) return Ok(); // Always return 200 to Meta

        foreach (var entry in payload.Entry)
        {
            foreach (var change in entry.Changes)
            {
                // Handle incoming messages
                if (change.Value.Messages != null)
                {
                    foreach (var message in change.Value.Messages)
                    {
                        await HandleIncomingMessage(message, change.Value.Contacts);
                    }
                }

                // Handle status updates (delivered, read, etc.)
                if (change.Value.Statuses != null)
                {
                    foreach (var status in change.Value.Statuses)
                    {
                        await HandleStatusUpdate(status);
                    }
                }
            }
        }

        // IMPORTANT: Always return 200 OK to Meta, otherwise they'll retry
        return Ok();
    }

    // ──────────────────────────────────────────────
    // Message Handlers
    // ──────────────────────────────────────────────

    private async Task HandleIncomingMessage(
        WhatsAppIncomingMessage message,
        List<WhatsAppIncomingContact>? contacts)
    {
        var from = message.From;
        var contactName = contacts?.FirstOrDefault()?.Profile?.Name ?? "Unknown";
        var messageText = message.Text?.Body ?? "[non-text message]";

        _logger.LogInformation("💬 Incoming message from {From} ({Name}): {Text}",
            from, contactName, messageText);

        // 1. Mark the message as read (blue ticks)
        await _whatsApp.MarkMessageAsReadAsync(message.Id);

        // 2. Update the 24-hour conversation window
        await _consent.UpdateConversationWindowAsync(from);

        // 3. Auto-record consent (customer initiated contact = implicit consent)
        await _consent.RecordConsentAsync(from, "customer_initiated");

        // 4. Handle specific commands
        var lowerText = messageText.ToLower().Trim();

        if (lowerText == "stop" || lowerText == "unsubscribe")
        {
            await _consent.RevokeConsentAsync(from);
            await _whatsApp.SendTextMessageAsync(from,
                "You've been unsubscribed from our WhatsApp notifications. " +
                "Reply START anytime to re-subscribe.");
            return;
        }

        if (lowerText == "start" || lowerText == "subscribe")
        {
            await _consent.RecordConsentAsync(from, "customer_opted_in");
            await _whatsApp.SendTextMessageAsync(from,
                "Welcome! ✅ You'll now receive order updates via WhatsApp. " +
                "Reply STOP anytime to unsubscribe.");
            return;
        }

        // 5. Check for pending COD confirmation
        var pendingCod = await _db.CodConfirmations
            .Where(c => c.PhoneNumber == from && c.Status == "pending")
            .OrderByDescending(c => c.SentAt)
            .FirstOrDefaultAsync();

        if (pendingCod != null)
        {
            if (lowerText == "yes" || lowerText == "confirm")
            {
                pendingCod.Status = "confirmed";
                pendingCod.RespondedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await _whatsApp.SendTextMessageAsync(from,
                    $"✅ Your order {pendingCod.OrderName} is confirmed! We'll prepare it for delivery. Thank you!");
                _logger.LogInformation("✅ COD order {OrderName} CONFIRMED by {Phone}", pendingCod.OrderName, from);
                return;
            }

            if (lowerText == "no" || lowerText == "cancel")
            {
                pendingCod.Status = "declined";
                pendingCod.RespondedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                await _whatsApp.SendTextMessageAsync(from,
                    $"Your order {pendingCod.OrderName} cancellation request has been noted. Our team will process it shortly.");
                _logger.LogWarning("🚫 COD order {OrderName} DECLINED by {Phone}", pendingCod.OrderName, from);
                return;
            }

            // Customer sent something else — remind them
            await _whatsApp.SendTextMessageAsync(from,
                $"You have a pending COD order {pendingCod.OrderName}. Please reply YES to confirm or NO to cancel.");
            _logger.LogInformation("❓ Unrecognized reply from {Phone} for COD order {OrderName}: {Text}",
                from, pendingCod.OrderName, messageText);
            return;
        }

        // 6. Default auto-reply
        // TODO: Expand this with AI-powered responses, FAQ lookup, or
        // route to a human agent via your support system.
        await _whatsApp.SendTextMessageAsync(from,
            $"Hi {contactName}! 👋 Thanks for reaching out. " +
            "Our team will get back to you shortly. " +
            "If you have an order question, please share your order number.");

        _logger.LogInformation("✅ Auto-reply sent to {From}", from);
    }

    private async Task HandleStatusUpdate(WhatsAppStatus status)
    {
        _logger.LogInformation("📊 Message {Id} status: {Status} for {Recipient}",
            status.Id, status.Status, status.RecipientId);

        // Log detailed error info for failed messages
        if (status.Status == "failed" && status.Errors?.Any() == true)
        {
            foreach (var error in status.Errors)
            {
                _logger.LogError(
                    "❌ DELIVERY FAILED - Code: {Code}, Title: {Title}, Message: {Msg}, Details: {Details}",
                    error.Code, error.Title, error.Message, error.ErrorData?.Details ?? "none");
            }
        }

        // Update message log status in database
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ShopifyWhatsApp.Data.AppDbContext>();
            var log = await db.MessageLogs
                .FirstOrDefaultAsync(m => m.WhatsAppMessageId == status.Id);
            if (log != null)
            {
                log.Status = status.Status;
                if (status.Status == "failed" && status.Errors?.Any() == true)
                    log.ErrorMessage = string.Join("; ", status.Errors.Select(e => $"{e.Code}: {e.Title} - {e.Message}"));
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update message log status");
        }
    }
}
