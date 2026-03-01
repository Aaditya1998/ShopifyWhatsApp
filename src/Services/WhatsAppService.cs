using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ShopifyWhatsApp.Models;
using System.Text;

namespace ShopifyWhatsApp.Services;

public interface IWhatsAppService
{
    Task<WhatsAppSendResponse?> SendTemplateMessageAsync(string phoneNumber, string templateName, List<string> parameters);
    Task<WhatsAppSendResponse?> SendTemplateMessageAsync(string phoneNumber, TemplateMessage templateMessage);
    Task<WhatsAppSendResponse?> SendTextMessageAsync(string phoneNumber, string text);
    Task MarkMessageAsReadAsync(string messageId);
}

public class WhatsAppService : IWhatsAppService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WhatsAppSettings _settings;
    private readonly ILogger<WhatsAppService> _logger;

    public WhatsAppService(
        IHttpClientFactory httpClientFactory,
        IOptions<WhatsAppSettings> settings,
        ILogger<WhatsAppService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Send a pre-approved template message (works anytime, even outside 24hr window)
    /// </summary>
    public async Task<WhatsAppSendResponse?> SendTemplateMessageAsync(
        string phoneNumber, string templateName, List<string> parameters)
    {
        var phone = NormalizePhone(phoneNumber);

        var request = new WhatsAppTemplateRequest
        {
            To = phone,
            Template = new WhatsAppTemplate
            {
                Name = templateName,
                Language = new WhatsAppLanguage { Code = "en_US" },
                Components = new List<WhatsAppComponent>
                {
                    new()
                    {
                        Type = "body",
                        Parameters = parameters.Select(p => new WhatsAppParameter
                        {
                            Type = "text",
                            Text = p
                        }).ToList()
                    }
                }
            }
        };

        _logger.LogInformation("📤 Sending template '{Template}' to {Phone} with {ParamCount} params",
            templateName, phone, parameters.Count);

        // Retry up to 3 times with exponential backoff for API failures
        WhatsAppSendResponse? result = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            result = await SendRequestAsync<WhatsAppSendResponse>(
                $"{_settings.PhoneNumberId}/messages", request);

            if (result != null)
            {
                _logger.LogInformation("📨 Message accepted on attempt {Attempt}", attempt);
                return result;
            }

            if (attempt < 3)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning("🔄 Retry attempt {Attempt}/3 in {Delay}s for {Phone}",
                    attempt, delay.TotalSeconds, phone);
                await Task.Delay(delay);
            }
        }

        _logger.LogError("💀 All 3 retry attempts failed for template '{Template}' to {Phone}",
            templateName, phone);
        return null;
    }

    /// <summary>
    /// Send a template message with optional URL button parameters.
    /// </summary>
    public async Task<WhatsAppSendResponse?> SendTemplateMessageAsync(
        string phoneNumber, TemplateMessage templateMessage)
    {
        var phone = NormalizePhone(phoneNumber);

        var components = new List<WhatsAppComponent>();

        // Body component
        if (templateMessage.BodyParameters.Count > 0)
        {
            components.Add(new WhatsAppComponent
            {
                Type = "body",
                Parameters = templateMessage.BodyParameters.Select(p => new WhatsAppParameter
                {
                    Type = "text",
                    Text = p
                }).ToList()
            });
        }

        // Button components (one per button)
        foreach (var button in templateMessage.ButtonParameters)
        {
            components.Add(new WhatsAppComponent
            {
                Type = "button",
                SubType = "url",
                Index = button.Index.ToString(),
                Parameters = new List<WhatsAppParameter>
                {
                    new() { Type = "text", Text = button.DynamicUrlSuffix }
                }
            });
        }

        var request = new WhatsAppTemplateRequest
        {
            To = phone,
            Template = new WhatsAppTemplate
            {
                Name = templateMessage.TemplateName,
                Language = new WhatsAppLanguage { Code = "en_US" },
                Components = components
            }
        };

        _logger.LogInformation(
            "📤 Sending template '{Template}' to {Phone} with {BodyParams} body params and {ButtonCount} buttons",
            templateMessage.TemplateName, phone,
            templateMessage.BodyParameters.Count, templateMessage.ButtonParameters.Count);

        // Retry up to 3 times with exponential backoff
        WhatsAppSendResponse? result = null;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            result = await SendRequestAsync<WhatsAppSendResponse>(
                $"{_settings.PhoneNumberId}/messages", request);

            if (result != null)
            {
                _logger.LogInformation("📨 Message accepted on attempt {Attempt}", attempt);
                return result;
            }

            if (attempt < 3)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning("🔄 Retry attempt {Attempt}/3 in {Delay}s for {Phone}",
                    attempt, delay.TotalSeconds, phone);
                await Task.Delay(delay);
            }
        }

        _logger.LogError("💀 All 3 retry attempts failed for template '{Template}' to {Phone}",
            templateMessage.TemplateName, phone);
        return null;
    }

    /// <summary>
    /// Send a free-form text message (only works within 24hr conversation window)
    /// </summary>
    public async Task<WhatsAppSendResponse?> SendTextMessageAsync(string phoneNumber, string text)
    {
        var phone = NormalizePhone(phoneNumber);

        var request = new WhatsAppTextRequest
        {
            To = phone,
            Text = new WhatsAppTextBody { Body = text }
        };

        _logger.LogInformation("📤 Sending text message to {Phone}", phone);

        return await SendRequestAsync<WhatsAppSendResponse>(
            $"{_settings.PhoneNumberId}/messages", request);
    }

    /// <summary>
    /// Mark an incoming message as read (shows blue ticks)
    /// </summary>
    public async Task MarkMessageAsReadAsync(string messageId)
    {
        var request = new WhatsAppMarkReadRequest { MessageId = messageId };

        _logger.LogInformation("👁️ Marking message {MessageId} as read", messageId);

        await SendRequestAsync<object>($"{_settings.PhoneNumberId}/messages", request);
    }

    // ──────────────────────────────────────────────
    // Private Helpers
    // ──────────────────────────────────────────────

    private async Task<T?> SendRequestAsync<T>(string endpoint, object payload) where T : class
    {
        var client = _httpClientFactory.CreateClient("WhatsAppApi");
        var json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Add auth header per request (not on the factory, so token refreshes work)
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = content
        };
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _settings.AccessToken);

        try
        {
            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("❌ WhatsApp API error {StatusCode}: {Body}",
                    (int)response.StatusCode, responseBody);

                if ((int)response.StatusCode == 429)
                {
                    _logger.LogWarning("⏳ Rate limited by WhatsApp API. Retrying handled by Polly.");
                }

                return null;
            }

            _logger.LogInformation("✅ WhatsApp API success: {Body}", responseBody);
            return JsonConvert.DeserializeObject<T>(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ WhatsApp API request failed for endpoint {Endpoint}", endpoint);
            return null;
        }
    }

    /// <summary>
    /// Normalize phone number: remove spaces, dashes, ensure country code prefix
    /// </summary>
    private static string NormalizePhone(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return phone;

        // Remove common formatting characters
        phone = phone.Replace(" ", "")
                      .Replace("-", "")
                      .Replace("(", "")
                      .Replace(")", "")
                      .Replace("+", "");

        return phone;
    }
}
