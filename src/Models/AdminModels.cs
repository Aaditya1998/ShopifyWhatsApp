namespace ShopifyWhatsApp.Models;

// ──────────────────────────────────────────────
// Dashboard Overview
// ──────────────────────────────────────────────
public class DashboardStatsResponse
{
    public MessageStats Messages { get; set; } = new();
    public ConsentStats Consents { get; set; } = new();
    public List<RecentMessageDto> RecentMessages { get; set; } = new();
}

public class MessageStats
{
    public int TotalSent { get; set; }
    public int Delivered { get; set; }
    public int Read { get; set; }
    public int Failed { get; set; }
    public double DeliveryRate { get; set; }
    public double ReadRate { get; set; }
    public int SentToday { get; set; }
    public int SentThisWeek { get; set; }
}

public class ConsentStats
{
    public int TotalConsents { get; set; }
    public int OptedIn { get; set; }
    public int OptedOut { get; set; }
}

public class RecentMessageDto
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public string EventType { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────
// Message Log Browsing
// ──────────────────────────────────────────────
public class MessageLogQuery
{
    public string? Phone { get; set; }
    public string? TemplateName { get; set; }
    public string? Status { get; set; }
    public string? EventType { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

public class MessageLogDto
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public long? ShopifyOrderId { get; set; }
    public string? WhatsAppMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public string EventType { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────
// Consent Management
// ──────────────────────────────────────────────
public class ConsentDto
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public bool OptedIn { get; set; }
    public DateTime OptedInDate { get; set; }
    public DateTime? OptedOutDate { get; set; }
    public string Source { get; set; } = string.Empty;
    public long? ShopifyCustomerId { get; set; }
}

public class ConsentToggleRequest
{
    public bool OptIn { get; set; }
}

// ──────────────────────────────────────────────
// System Health
// ──────────────────────────────────────────────
public class SystemHealthResponse
{
    public string Status { get; set; } = "ok";
    public bool DatabaseConnected { get; set; }
    public List<WebhookStatusDto> Webhooks { get; set; } = new();
    public List<RecentErrorDto> RecentErrors { get; set; } = new();
    public DateTime ServerTime { get; set; } = DateTime.UtcNow;
}

public class WebhookStatusDto
{
    public long Id { get; set; }
    public string Topic { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public class RecentErrorDto
{
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; }
}
