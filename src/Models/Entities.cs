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
// Scheduled Message (deferred sending queue)
// ──────────────────────────────────────────────
public class ScheduledMessage
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(20)]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>Shopify checkout ID — used for deduplication and cancellation.</summary>
    public long ShopifyCheckoutId { get; set; }

    [MaxLength(100)]
    public string CheckoutToken { get; set; } = string.Empty;

    /// <summary>Message type, e.g. "abandoned_cart". Future-proofing for other delayed types.</summary>
    [MaxLength(50)]
    public string MessageType { get; set; } = "abandoned_cart";

    /// <summary>Serialized TemplateMessage JSON — captured at scheduling time.</summary>
    public string TemplatePayloadJson { get; set; } = string.Empty;

    /// <summary>pending → sent | cancelled | failed</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "pending";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>When the message should be sent (CreatedAt + delay).</summary>
    public DateTime ScheduledFor { get; set; }

    /// <summary>When the message was actually processed.</summary>
    public DateTime? ProcessedAt { get; set; }

    [MaxLength(100)]
    public string? CancellationReason { get; set; }
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
