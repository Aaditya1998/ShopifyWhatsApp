using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace ShopifyWhatsApp.Middleware;

/// <summary>
/// Validates admin API requests using a static API key in the X-Admin-Api-Key header.
/// Apply as [AdminApiKey] attribute on controller or action.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class AdminApiKeyAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        var config = httpContext.RequestServices.GetRequiredService<IOptions<AdminSettings>>();
        var logger = httpContext.RequestServices.GetRequiredService<ILogger<AdminApiKeyAttribute>>();

        if (!httpContext.Request.Headers.TryGetValue("X-Admin-Api-Key", out var providedKey))
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Missing API key" });
            return;
        }

        if (!CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(config.Value.ApiKey),
            Encoding.UTF8.GetBytes(providedKey.ToString())))
        {
            logger.LogWarning("Admin API key validation failed from {IP}",
                httpContext.Connection.RemoteIpAddress);
            context.Result = new UnauthorizedObjectResult(new { error = "Invalid API key" });
            return;
        }

        await next();
    }
}
