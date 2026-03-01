using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ShopifyWhatsApp.Middleware;

/// <summary>
/// Validates incoming Shopify webhooks using HMAC-SHA256.
/// Apply as [ShopifyHmacValidation] attribute on controller actions.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class ShopifyHmacValidationAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var config = httpContext.RequestServices.GetRequiredService<IOptions<ShopifySettings>>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<ShopifyHmacValidationAttribute>>();

        // Read the HMAC header
        if (!httpContext.Request.Headers.TryGetValue("X-Shopify-Hmac-Sha256", out var hmacHeader))
        {
            logger.LogWarning("⚠️ Shopify webhook missing HMAC header");
            context.Result = new UnauthorizedObjectResult(new { error = "Missing HMAC header" });
            return;
        }

        // Read the request body
        httpContext.Request.EnableBuffering();
        using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        httpContext.Request.Body.Position = 0; // Reset so controller can read it

        // Compute HMAC
        var secret = config.Value.WebhookSecret;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var computedHmac = Convert.ToBase64String(hash);

        // Compare
        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHmac),
            Encoding.UTF8.GetBytes(hmacHeader.ToString())))
        {
            logger.LogWarning("⚠️ Shopify webhook HMAC validation failed");
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid HMAC signature" });
            return;
        }

        logger.LogInformation("✅ Shopify webhook HMAC validated");
        await next();
    }
}

/// <summary>
/// Validates incoming WhatsApp webhook signatures using X-Hub-Signature-256.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class WhatsAppSignatureValidationAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var config = httpContext.RequestServices.GetRequiredService<IOptions<WhatsAppSettings>>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<WhatsAppSignatureValidationAttribute>>();

        if (!httpContext.Request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader))
        {
            logger.LogWarning("⚠️ WhatsApp webhook missing signature header");
            context.Result = new UnauthorizedObjectResult(new { error = "Missing signature" });
            return;
        }

        httpContext.Request.EnableBuffering();
        using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        httpContext.Request.Body.Position = 0;

        var appSecret = config.Value.AppSecret;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var computedSignature = "sha256=" + BitConverter.ToString(hash).Replace("-", "").ToLower();

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(signatureHeader.ToString())))
        {
            logger.LogWarning("⚠️ WhatsApp webhook signature validation failed");
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid signature" });
            return;
        }

        logger.LogInformation("✅ WhatsApp webhook signature validated");
        await next();
    }
}
