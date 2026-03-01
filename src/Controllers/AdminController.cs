using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopifyWhatsApp.Data;
using ShopifyWhatsApp.Middleware;
using ShopifyWhatsApp.Models;
using ShopifyWhatsApp.Services;

namespace ShopifyWhatsApp.Controllers;

[ApiController]
[Route("api/admin")]
[AdminApiKey]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IShopifyService _shopify;
    private readonly IConsentService _consent;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext db,
        IShopifyService shopify,
        IConsentService consent,
        ILogger<AdminController> logger)
    {
        _db = db;
        _shopify = shopify;
        _consent = consent;
        _logger = logger;
    }

    // ──────────────────────────────────────────────
    // GET /api/admin/dashboard
    // ──────────────────────────────────────────────
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardStatsResponse>> GetDashboard()
    {
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var weekStart = now.AddDays(-7);

        // Message stats by status
        var statusCounts = await _db.MessageLogs
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var totalSent = statusCounts.Sum(s => s.Count);
        var delivered = statusCounts.FirstOrDefault(s => s.Status == "delivered")?.Count ?? 0;
        var read = statusCounts.FirstOrDefault(s => s.Status == "read")?.Count ?? 0;
        var failed = statusCounts.FirstOrDefault(s => s.Status == "failed")?.Count ?? 0;

        var sentToday = await _db.MessageLogs.CountAsync(m => m.SentAt >= todayStart);
        var sentThisWeek = await _db.MessageLogs.CountAsync(m => m.SentAt >= weekStart);

        // Consent stats
        var totalConsents = await _db.CustomerConsents.CountAsync();
        var optedIn = await _db.CustomerConsents.CountAsync(c => c.OptedIn);

        // Recent messages
        var recent = await _db.MessageLogs
            .OrderByDescending(m => m.SentAt)
            .Take(10)
            .Select(m => new RecentMessageDto
            {
                PhoneNumber = m.PhoneNumber,
                TemplateName = m.TemplateName,
                Status = m.Status,
                SentAt = m.SentAt,
                EventType = m.EventType
            })
            .ToListAsync();

        return Ok(new DashboardStatsResponse
        {
            Messages = new MessageStats
            {
                TotalSent = totalSent,
                Delivered = delivered,
                Read = read,
                Failed = failed,
                DeliveryRate = totalSent > 0 ? Math.Round((delivered + read) * 100.0 / totalSent, 1) : 0,
                ReadRate = totalSent > 0 ? Math.Round(read * 100.0 / totalSent, 1) : 0,
                SentToday = sentToday,
                SentThisWeek = sentThisWeek
            },
            Consents = new ConsentStats
            {
                TotalConsents = totalConsents,
                OptedIn = optedIn,
                OptedOut = totalConsents - optedIn
            },
            RecentMessages = recent
        });
    }

    // ──────────────────────────────────────────────
    // GET /api/admin/messages
    // ──────────────────────────────────────────────
    [HttpGet("messages")]
    public async Task<ActionResult<PagedResult<MessageLogDto>>> GetMessages(
        [FromQuery] MessageLogQuery query)
    {
        var q = _db.MessageLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Phone))
            q = q.Where(m => m.PhoneNumber.Contains(query.Phone));
        if (!string.IsNullOrWhiteSpace(query.TemplateName))
            q = q.Where(m => m.TemplateName == query.TemplateName);
        if (!string.IsNullOrWhiteSpace(query.Status))
            q = q.Where(m => m.Status == query.Status);
        if (!string.IsNullOrWhiteSpace(query.EventType))
            q = q.Where(m => m.EventType == query.EventType);
        if (query.From.HasValue)
            q = q.Where(m => m.SentAt >= query.From.Value);
        if (query.To.HasValue)
            q = q.Where(m => m.SentAt <= query.To.Value);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(query.Page, 1);

        var totalCount = await q.CountAsync();
        var items = await q
            .OrderByDescending(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new MessageLogDto
            {
                Id = m.Id,
                PhoneNumber = m.PhoneNumber,
                TemplateName = m.TemplateName,
                Status = m.Status,
                SentAt = m.SentAt,
                ShopifyOrderId = m.ShopifyOrderId,
                WhatsAppMessageId = m.WhatsAppMessageId,
                ErrorMessage = m.ErrorMessage,
                EventType = m.EventType
            })
            .ToListAsync();

        return Ok(new PagedResult<MessageLogDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    // ──────────────────────────────────────────────
    // GET /api/admin/messages/{id}
    // ──────────────────────────────────────────────
    [HttpGet("messages/{id:int}")]
    public async Task<ActionResult<MessageLogDto>> GetMessage(int id)
    {
        var message = await _db.MessageLogs.FindAsync(id);
        if (message == null) return NotFound(new { error = "Message not found" });

        return Ok(new MessageLogDto
        {
            Id = message.Id,
            PhoneNumber = message.PhoneNumber,
            TemplateName = message.TemplateName,
            Status = message.Status,
            SentAt = message.SentAt,
            ShopifyOrderId = message.ShopifyOrderId,
            WhatsAppMessageId = message.WhatsAppMessageId,
            ErrorMessage = message.ErrorMessage,
            EventType = message.EventType
        });
    }

    // ──────────────────────────────────────────────
    // GET /api/admin/consents
    // ──────────────────────────────────────────────
    [HttpGet("consents")]
    public async Task<ActionResult<PagedResult<ConsentDto>>> GetConsents(
        [FromQuery] bool? optedIn,
        [FromQuery] string? phone,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25)
    {
        var q = _db.CustomerConsents.AsQueryable();

        if (optedIn.HasValue)
            q = q.Where(c => c.OptedIn == optedIn.Value);
        if (!string.IsNullOrWhiteSpace(phone))
            q = q.Where(c => c.PhoneNumber.Contains(phone));

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var totalCount = await q.CountAsync();
        var items = await q
            .OrderByDescending(c => c.OptedInDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ConsentDto
            {
                Id = c.Id,
                PhoneNumber = c.PhoneNumber,
                OptedIn = c.OptedIn,
                OptedInDate = c.OptedInDate,
                OptedOutDate = c.OptedOutDate,
                Source = c.Source,
                ShopifyCustomerId = c.ShopifyCustomerId
            })
            .ToListAsync();

        return Ok(new PagedResult<ConsentDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    // ──────────────────────────────────────────────
    // PUT /api/admin/consents/{phoneNumber}
    // ──────────────────────────────────────────────
    [HttpPut("consents/{phoneNumber}")]
    public async Task<ActionResult<ConsentDto>> ToggleConsent(
        string phoneNumber,
        [FromBody] ConsentToggleRequest request)
    {
        if (request.OptIn)
        {
            await _consent.RecordConsentAsync(phoneNumber, "admin_manual");
        }
        else
        {
            await _consent.RevokeConsentAsync(phoneNumber);
        }

        var consent = await _db.CustomerConsents
            .FirstOrDefaultAsync(c => c.PhoneNumber == phoneNumber);

        if (consent == null)
            return NotFound(new { error = $"No consent record for {phoneNumber}" });

        _logger.LogInformation("Admin toggled consent for {Phone} to {OptIn}", phoneNumber, request.OptIn);

        return Ok(new ConsentDto
        {
            Id = consent.Id,
            PhoneNumber = consent.PhoneNumber,
            OptedIn = consent.OptedIn,
            OptedInDate = consent.OptedInDate,
            OptedOutDate = consent.OptedOutDate,
            Source = consent.Source,
            ShopifyCustomerId = consent.ShopifyCustomerId
        });
    }

    // ──────────────────────────────────────────────
    // GET /api/admin/health
    // ──────────────────────────────────────────────
    [HttpGet("health")]
    public async Task<ActionResult<SystemHealthResponse>> GetSystemHealth()
    {
        var dbConnected = false;
        try { dbConnected = await _db.Database.CanConnectAsync(); }
        catch { /* swallow */ }

        var webhooks = new List<WebhookStatusDto>();
        try
        {
            var shopifyWebhooks = await _shopify.ListWebhooksAsync();
            webhooks = shopifyWebhooks.Select(w => new WebhookStatusDto
            {
                Id = w.Id,
                Topic = w.Topic,
                Address = w.Address
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch webhooks for health check");
        }

        var recentErrors = await _db.MessageLogs
            .Where(m => m.Status == "failed")
            .OrderByDescending(m => m.SentAt)
            .Take(10)
            .Select(m => new RecentErrorDto
            {
                Id = m.Id,
                PhoneNumber = m.PhoneNumber,
                TemplateName = m.TemplateName,
                ErrorMessage = m.ErrorMessage,
                SentAt = m.SentAt
            })
            .ToListAsync();

        return Ok(new SystemHealthResponse
        {
            Status = dbConnected ? "ok" : "degraded",
            DatabaseConnected = dbConnected,
            Webhooks = webhooks,
            RecentErrors = recentErrors,
            ServerTime = DateTime.UtcNow
        });
    }
}
