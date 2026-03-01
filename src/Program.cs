using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using ShopifyWhatsApp.Data;
using ShopifyWhatsApp.Services;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// Serilog Configuration
// ──────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// ──────────────────────────────────────────────
// Database
// ──────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ──────────────────────────────────────────────
// Configuration Binding
// ──────────────────────────────────────────────
builder.Services.Configure<WhatsAppSettings>(builder.Configuration.GetSection("WhatsApp"));
builder.Services.Configure<ShopifySettings>(builder.Configuration.GetSection("Shopify"));
builder.Services.Configure<AdminSettings>(builder.Configuration.GetSection("Admin"));

// ──────────────────────────────────────────────
// HttpClient for WhatsApp Cloud API (with retry policy)
// ──────────────────────────────────────────────
builder.Services.AddHttpClient("WhatsAppApi", client =>
{
    client.BaseAddress = new Uri("https://graph.facebook.com/v21.0/");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddTransientHttpErrorPolicy(policy =>
    policy.WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
.AddTransientHttpErrorPolicy(policy =>
    policy.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

// ──────────────────────────────────────────────
// HttpClient for Shopify Admin API
// ──────────────────────────────────────────────
builder.Services.AddHttpClient("ShopifyApi", (sp, client) =>
{
    var config = builder.Configuration.GetSection("Shopify");
    var shopDomain = config["ShopDomain"];
    client.BaseAddress = new Uri($"https://{shopDomain}/admin/api/2024-01/");
    client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", config["AccessToken"]);
});

// ──────────────────────────────────────────────
// Application Services
// ──────────────────────────────────────────────
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();
builder.Services.AddScoped<IShopifyService, ShopifyService>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddScoped<IConsentService, ConsentService>();

builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
    });

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// ──────────────────────────────────────────────
// Auto-run migrations on startup
// ──────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// ──────────────────────────────────────────────
// Middleware Pipeline
// ──────────────────────────────────────────────

// Global exception handler - app NEVER crashes from unhandled exceptions
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "💀 Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        context.Response.StatusCode = 200; // Return 200 so Shopify/Meta don't retry endlessly
        await context.Response.WriteAsync("{\"status\":\"error_handled\"}");
    }
});

app.UseSerilogRequestLogging();
app.MapControllers();
app.MapHealthChecks("/health");

// ──────────────────────────────────────────────
// Register Shopify Webhooks on startup (background)
// ──────────────────────────────────────────────
_ = Task.Run(async () =>
{
    await Task.Delay(5000); // wait for app to fully start
    using var scope = app.Services.CreateScope();
    var shopifyService = scope.ServiceProvider.GetRequiredService<IShopifyService>();
    try
    {
        await shopifyService.RegisterAllWebhooksAsync();
        Log.Information("✅ Shopify webhooks registered successfully");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Failed to register Shopify webhooks");
    }
});

Log.Information("🚀 ShopifyWhatsApp Integration Server started");
app.Run();

// ──────────────────────────────────────────────
// Settings Classes
// ──────────────────────────────────────────────
public class WhatsAppSettings
{
    public string PhoneNumberId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string VerifyToken { get; set; } = string.Empty;
    public string AppSecret { get; set; } = string.Empty;
}

public class ShopifySettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string ShopDomain { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
    public string WebhookBaseUrl { get; set; } = string.Empty;
}

public class AdminSettings
{
    public string ApiKey { get; set; } = string.Empty;
}
