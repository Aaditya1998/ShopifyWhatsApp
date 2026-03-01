using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ShopifyWhatsApp.Data;
using ShopifyWhatsApp.Models;

namespace ShopifyWhatsApp.Services;

/// <summary>
/// Background service that polls the database for scheduled abandoned cart messages
/// and sends them when their delay has elapsed.
/// </summary>
public class AbandonedCartSenderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AbandonedCartSettings _settings;
    private readonly ILogger<AbandonedCartSenderService> _logger;

    public AbandonedCartSenderService(
        IServiceScopeFactory scopeFactory,
        IOptions<AbandonedCartSettings> settings,
        ILogger<AbandonedCartSenderService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("⏰ AbandonedCartSenderService started — poll: {Poll}s, delay: {Delay}m",
            _settings.PollIntervalSeconds, _settings.DelayMinutes);

        // Wait for app to fully start (DB, etc.)
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💀 AbandonedCartSenderService loop error");
            }

            await Task.Delay(TimeSpan.FromSeconds(_settings.PollIntervalSeconds), stoppingToken);
        }

        _logger.LogInformation("⏰ AbandonedCartSenderService stopped");
    }

    private async Task ProcessDueMessagesAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var whatsApp = scope.ServiceProvider.GetRequiredService<IWhatsAppService>();
        var consent = scope.ServiceProvider.GetRequiredService<IConsentService>();

        var now = DateTime.UtcNow;

        // Fetch due messages in batches
        var dueMessages = await db.ScheduledMessages
            .Where(sm => sm.Status == "pending" && sm.ScheduledFor <= now)
            .OrderBy(sm => sm.ScheduledFor)
            .Take(20)
            .ToListAsync(stoppingToken);

        if (dueMessages.Count == 0) return;

        _logger.LogInformation("📬 Found {Count} due abandoned cart message(s) to send", dueMessages.Count);

        foreach (var message in dueMessages)
        {
            if (stoppingToken.IsCancellationRequested) break;

            try
            {
                // Re-check consent (customer may have opted out during delay)
                if (!await consent.HasConsentAsync(message.PhoneNumber))
                {
                    message.Status = "cancelled";
                    message.ProcessedAt = DateTime.UtcNow;
                    message.CancellationReason = "no_consent";
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("🚫 Skipped {Phone} — no consent", message.PhoneNumber);
                    continue;
                }

                // Deserialize stored template
                var templateMessage = JsonConvert.DeserializeObject<TemplateMessage>(
                    message.TemplatePayloadJson);

                if (templateMessage == null)
                {
                    message.Status = "failed";
                    message.ProcessedAt = DateTime.UtcNow;
                    message.CancellationReason = "invalid_template_payload";
                    await db.SaveChangesAsync(stoppingToken);
                    _logger.LogError("❌ Failed to deserialize template for scheduled message {Id}", message.Id);
                    continue;
                }

                _logger.LogInformation("📤 Sending scheduled abandoned cart to {Phone} (id: {Id})",
                    message.PhoneNumber, message.Id);

                // Send via WhatsApp
                var result = await whatsApp.SendTemplateMessageAsync(
                    message.PhoneNumber, templateMessage);

                // Update status
                message.ProcessedAt = DateTime.UtcNow;
                message.Status = result != null ? "sent" : "failed";
                if (result == null)
                    message.CancellationReason = "whatsapp_api_failed";
                await db.SaveChangesAsync(stoppingToken);

                // Log the message
                await consent.LogMessageAsync(
                    message.PhoneNumber,
                    templateMessage.TemplateName,
                    "checkout_abandoned",
                    whatsAppMessageId: result?.Messages?.FirstOrDefault()?.Id,
                    errorMessage: result == null ? "API call failed after retries" : null);

                if (result != null)
                    _logger.LogInformation("✅ Sent abandoned cart to {Phone} (id: {Id})", message.PhoneNumber, message.Id);
                else
                    _logger.LogWarning("⚠️ Failed to send abandoned cart to {Phone} (id: {Id})", message.PhoneNumber, message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💀 Error processing scheduled message {Id}", message.Id);
                message.Status = "failed";
                message.ProcessedAt = DateTime.UtcNow;
                message.CancellationReason = $"exception: {ex.Message[..Math.Min(ex.Message.Length, 80)]}";
                try { await db.SaveChangesAsync(stoppingToken); }
                catch { /* don't crash the loop */ }
            }
        }
    }
}
