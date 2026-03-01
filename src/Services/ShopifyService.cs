using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ShopifyWhatsApp.Models;
using System.Text;

namespace ShopifyWhatsApp.Services;

public interface IShopifyService
{
    Task RegisterAllWebhooksAsync();
    Task<List<ShopifyWebhookDetail>> ListWebhooksAsync();
    Task DeleteWebhookAsync(long id);
}

public class ShopifyService : IShopifyService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ShopifySettings _settings;
    private readonly ILogger<ShopifyService> _logger;

    // Webhook topics and their corresponding local endpoint paths
    private readonly Dictionary<string, string> _webhookTopics = new()
    {
        { "orders/create", "/api/webhooks/shopify/order-created" },
        { "orders/fulfilled", "/api/webhooks/shopify/order-fulfilled" },
        { "orders/cancelled", "/api/webhooks/shopify/order-cancelled" },
        { "checkouts/create", "/api/webhooks/shopify/checkout-abandoned" }
    };

    public ShopifyService(
        IHttpClientFactory httpClientFactory,
        IOptions<ShopifySettings> settings,
        ILogger<ShopifyService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>
    /// Register all required webhooks with Shopify.
    /// Checks for existing webhooks to avoid duplicates.
    /// </summary>
    public async Task RegisterAllWebhooksAsync()
    {
        _logger.LogInformation("🔗 Registering Shopify webhooks...");

        // Get existing webhooks
        var existing = await ListWebhooksAsync();
        var existingTopics = existing.Select(w => w.Topic).ToHashSet();

        foreach (var (topic, path) in _webhookTopics)
        {
            if (existingTopics.Contains(topic))
            {
                _logger.LogInformation("⏭️ Webhook already exists for {Topic}", topic);
                continue;
            }

            var webhookUrl = $"{_settings.WebhookBaseUrl.TrimEnd('/')}{path}";

            var payload = new
            {
                webhook = new
                {
                    topic = topic,
                    address = webhookUrl,
                    format = "json"
                }
            };

            var client = _httpClientFactory.CreateClient("ShopifyApi");
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.PostAsync("webhooks.json", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("✅ Registered webhook: {Topic} → {Url}", topic, webhookUrl);
                }
                else
                {
                    _logger.LogError("❌ Failed to register webhook {Topic}: {StatusCode} - {Body}",
                        topic, (int)response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Exception registering webhook {Topic}", topic);
            }
        }
    }

    /// <summary>
    /// List all currently registered webhooks
    /// </summary>
    public async Task<List<ShopifyWebhookDetail>> ListWebhooksAsync()
    {
        var client = _httpClientFactory.CreateClient("ShopifyApi");

        try
        {
            var response = await client.GetAsync("webhooks.json");
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonConvert.DeserializeObject<ShopifyWebhookListResponse>(body);
                return result?.Webhooks ?? new List<ShopifyWebhookDetail>();
            }

            _logger.LogError("❌ Failed to list webhooks: {Body}", body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception listing webhooks");
        }

        return new List<ShopifyWebhookDetail>();
    }

    /// <summary>
    /// Delete a specific webhook by ID
    /// </summary>
    public async Task DeleteWebhookAsync(long id)
    {
        var client = _httpClientFactory.CreateClient("ShopifyApi");

        try
        {
            var response = await client.DeleteAsync($"webhooks/{id}.json");
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("🗑️ Deleted webhook {Id}", id);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("❌ Failed to delete webhook {Id}: {Body}", id, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Exception deleting webhook {Id}", id);
        }
    }
}
