using Microsoft.EntityFrameworkCore;
using ShopifyWhatsApp.Data;
using ShopifyWhatsApp.Models;

namespace ShopifyWhatsApp.Services;

public interface IConsentService
{
    Task<bool> HasConsentAsync(string phoneNumber);
    Task RecordConsentAsync(string phoneNumber, string source, long? shopifyCustomerId = null);
    Task RevokeConsentAsync(string phoneNumber);
    Task LogMessageAsync(string phoneNumber, string templateName, string eventType,
        long? shopifyOrderId = null, string? whatsAppMessageId = null, string? errorMessage = null);
    Task UpdateConversationWindowAsync(string phoneNumber);
    Task<bool> IsWithin24HourWindowAsync(string phoneNumber);
}

public class ConsentService : IConsentService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ConsentService> _logger;

    public ConsentService(AppDbContext db, ILogger<ConsentService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> HasConsentAsync(string phoneNumber)
    {
        var consent = await _db.CustomerConsents
            .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);
        return consent?.OptedIn ?? false;
    }

    public async Task RecordConsentAsync(string phoneNumber, string source, long? shopifyCustomerId = null)
    {
        var existing = await _db.CustomerConsents
            .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);

        if (existing != null)
        {
            existing.OptedIn = true;
            existing.OptedInDate = DateTime.UtcNow;
            existing.Source = source;
            existing.OptedOutDate = null;
            if (shopifyCustomerId.HasValue)
                existing.ShopifyCustomerId = shopifyCustomerId;
        }
        else
        {
            _db.CustomerConsents.Add(new CustomerConsent
            {
                PhoneNumber = phoneNumber,
                OptedIn = true,
                OptedInDate = DateTime.UtcNow,
                Source = source,
                ShopifyCustomerId = shopifyCustomerId
            });
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("✅ Consent recorded for {Phone} via {Source}", phoneNumber, source);
    }

    public async Task RevokeConsentAsync(string phoneNumber)
    {
        var consent = await _db.CustomerConsents
            .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);

        if (consent != null)
        {
            consent.OptedIn = false;
            consent.OptedOutDate = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogInformation("🚫 Consent revoked for {Phone}", phoneNumber);
        }
    }

    public async Task LogMessageAsync(string phoneNumber, string templateName, string eventType,
        long? shopifyOrderId = null, string? whatsAppMessageId = null, string? errorMessage = null)
    {
        _db.MessageLogs.Add(new MessageLog
        {
            PhoneNumber = phoneNumber,
            TemplateName = templateName,
            EventType = eventType,
            ShopifyOrderId = shopifyOrderId,
            WhatsAppMessageId = whatsAppMessageId,
            ErrorMessage = errorMessage,
            Status = errorMessage != null ? "failed" : "sent",
            SentAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task UpdateConversationWindowAsync(string phoneNumber)
    {
        var window = await _db.ConversationWindows
            .FirstOrDefaultAsync(w => w.PhoneNumber == phoneNumber);

        if (window != null)
        {
            window.LastCustomerMessageAt = DateTime.UtcNow;
        }
        else
        {
            _db.ConversationWindows.Add(new ConversationWindow
            {
                PhoneNumber = phoneNumber,
                LastCustomerMessageAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();
    }

    public async Task<bool> IsWithin24HourWindowAsync(string phoneNumber)
    {
        var window = await _db.ConversationWindows
            .FirstOrDefaultAsync(w => w.PhoneNumber == phoneNumber);

        return window?.IsWindowOpen ?? false;
    }
}
