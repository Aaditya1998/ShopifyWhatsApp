using System.ComponentModel.DataAnnotations;

namespace ShopifyWhatsApp.Models;

// ──────────────────────────────────────────────
// Customer Consent (WhatsApp opt-in tracking)
// ──────────────────────────────────────────────
public class CustomerConsent
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    public bool OptedIn { get; set; } = true;

    public DateTime OptedInDate { get; set; } = DateTime.UtcNow;

    public DateTime? OptedOutDate { get; set; }

    [MaxLength(50)]
    public string Source { get; set; } = string.Empty; // "checkout", "website", "manual"

    public long? ShopifyCustomerId { get; set; }
}

// ──────────────────────────────────────────────
// Message Log (track all sent messages)
// ──────────────────────────────────────────────
public class MessageLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string Status { get; set; } = "sent"; // sent, delivered, read, failed

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public long? ShopifyOrderId { get; set; }

    [MaxLength(100)]
    public string? WhatsAppMessageId { get; set; }

    public string? ErrorMessage { get; set; }

    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty; // order_created, order_fulfilled, etc.
}

// ──────────────────────────────────────────────
// Conversation Window (24-hour tracking)
// ──────────────────────────────────────────────
public class ConversationWindow
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    public DateTime LastCustomerMessageAt { get; set; }

    /// <summary>
    /// Whether we're within the 24-hour free-form messaging window
    /// </summary>
    public bool IsWindowOpen => (DateTime.UtcNow - LastCustomerMessageAt).TotalHours < 24;
}
