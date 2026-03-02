using Newtonsoft.Json;

namespace ShopifyWhatsApp.Models;

// ──────────────────────────────────────────────
// Shopify Order Webhook Payload
// ──────────────────────────────────────────────
public class ShopifyOrderPayload
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("order_number")]
    public long OrderNumber { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty; // e.g., "#1001"

    [JsonProperty("total_price")]
    public string TotalPrice { get; set; } = "0.00";

    [JsonProperty("currency")]
    public string Currency { get; set; } = "USD";

    [JsonProperty("financial_status")]
    public string FinancialStatus { get; set; } = string.Empty;

    [JsonProperty("fulfillment_status")]
    public string? FulfillmentStatus { get; set; }

    [JsonProperty("cancel_reason")]
    public string? CancelReason { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("customer")]
    public ShopifyCustomer? Customer { get; set; }

    [JsonProperty("line_items")]
    public List<ShopifyLineItem> LineItems { get; set; } = new();

    [JsonProperty("shipping_address")]
    public ShopifyAddress? ShippingAddress { get; set; }

    [JsonProperty("fulfillments")]
    public List<ShopifyFulfillment> Fulfillments { get; set; } = new();

    [JsonProperty("order_status_url")]
    public string? OrderStatusUrl { get; set; }
}

public class ShopifyCustomer
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("last_name")]
    public string LastName { get; set; } = string.Empty;

    [JsonProperty("email")]
    public string? Email { get; set; }

    [JsonProperty("phone")]
    public string? Phone { get; set; }

    public string FullName => $"{FirstName} {LastName}".Trim();
}

public class ShopifyLineItem
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("price")]
    public string Price { get; set; } = "0.00";

    [JsonProperty("sku")]
    public string? Sku { get; set; }
}

public class ShopifyAddress
{
    [JsonProperty("first_name")]
    public string FirstName { get; set; } = string.Empty;

    [JsonProperty("address1")]
    public string Address1 { get; set; } = string.Empty;

    [JsonProperty("city")]
    public string City { get; set; } = string.Empty;

    [JsonProperty("province")]
    public string Province { get; set; } = string.Empty;

    [JsonProperty("country")]
    public string Country { get; set; } = string.Empty;

    [JsonProperty("zip")]
    public string Zip { get; set; } = string.Empty;

    [JsonProperty("phone")]
    public string? Phone { get; set; }
}

public class ShopifyFulfillment
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("tracking_number")]
    public string? TrackingNumber { get; set; }

    [JsonProperty("tracking_url")]
    public string? TrackingUrl { get; set; }

    [JsonProperty("tracking_company")]
    public string? TrackingCompany { get; set; }
}

// ──────────────────────────────────────────────
// Shopify Abandoned Checkout Payload
// ──────────────────────────────────────────────
public class ShopifyCheckoutPayload
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("token")]
    public string Token { get; set; } = string.Empty;

    [JsonProperty("abandoned_checkout_url")]
    public string AbandonedCheckoutUrl { get; set; } = string.Empty;

    [JsonProperty("total_price")]
    public string TotalPrice { get; set; } = "0.00";

    [JsonProperty("currency")]
    public string Currency { get; set; } = "USD";

    [JsonProperty("customer")]
    public ShopifyCustomer? Customer { get; set; }

    [JsonProperty("line_items")]
    public List<ShopifyLineItem> LineItems { get; set; } = new();

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }
}

// ──────────────────────────────────────────────
// Shopify Webhook Registration
// ──────────────────────────────────────────────
// ──────────────────────────────────────────────
// Shopify Refund Webhook Payload
// ──────────────────────────────────────────────
public class ShopifyRefundPayload
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("order_id")]
    public long OrderId { get; set; }

    [JsonProperty("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonProperty("note")]
    public string? Note { get; set; }

    [JsonProperty("restock")]
    public bool Restock { get; set; }

    [JsonProperty("transactions")]
    public List<ShopifyRefundTransaction> Transactions { get; set; } = new();

    [JsonProperty("refund_line_items")]
    public List<ShopifyRefundLineItem> RefundLineItems { get; set; } = new();
}

public class ShopifyRefundTransaction
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("amount")]
    public string Amount { get; set; } = "0.00";

    [JsonProperty("currency")]
    public string Currency { get; set; } = "USD";

    [JsonProperty("kind")]
    public string Kind { get; set; } = string.Empty; // "refund", "void", etc.

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;
}

public class ShopifyRefundLineItem
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("subtotal")]
    public string Subtotal { get; set; } = "0.00";

    [JsonProperty("line_item")]
    public ShopifyLineItem? LineItem { get; set; }
}

// ──────────────────────────────────────────────
// Shopify Order API Response Wrapper
// ──────────────────────────────────────────────
public class ShopifyOrderWrapper
{
    [JsonProperty("order")]
    public ShopifyOrderPayload? Order { get; set; }
}

// ──────────────────────────────────────────────
// Shopify Webhook Registration
// ──────────────────────────────────────────────
public class ShopifyWebhookRegistration
{
    [JsonProperty("webhook")]
    public ShopifyWebhookDetail Webhook { get; set; } = new();
}

public class ShopifyWebhookDetail
{
    [JsonProperty("id")]
    public long Id { get; set; }

    [JsonProperty("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonProperty("address")]
    public string Address { get; set; } = string.Empty;

    [JsonProperty("format")]
    public string Format { get; set; } = "json";
}

public class ShopifyWebhookListResponse
{
    [JsonProperty("webhooks")]
    public List<ShopifyWebhookDetail> Webhooks { get; set; } = new();
}
