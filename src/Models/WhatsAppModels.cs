using Newtonsoft.Json;

namespace ShopifyWhatsApp.Models;

// ──────────────────────────────────────────────
// WhatsApp Cloud API - Send Message Request
// ──────────────────────────────────────────────
public class WhatsAppTemplateRequest
{
    [JsonProperty("messaging_product")]
    public string MessagingProduct { get; set; } = "whatsapp";

    [JsonProperty("to")]
    public string To { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "template";

    [JsonProperty("template")]
    public WhatsAppTemplate Template { get; set; } = new();
}

public class WhatsAppTemplate
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("language")]
    public WhatsAppLanguage Language { get; set; } = new();

    [JsonProperty("components")]
    public List<WhatsAppComponent> Components { get; set; } = new();
}

public class WhatsAppLanguage
{
    [JsonProperty("code")]
    public string Code { get; set; } = "en";
}

public class WhatsAppComponent
{
    [JsonProperty("type")]
    public string Type { get; set; } = "body";

    [JsonProperty("sub_type")]
    public string? SubType { get; set; }

    [JsonProperty("index")]
    public string? Index { get; set; }

    [JsonProperty("parameters")]
    public List<WhatsAppParameter> Parameters { get; set; } = new();
}

public class WhatsAppParameter
{
    [JsonProperty("type")]
    public string Type { get; set; } = "text";

    [JsonProperty("text")]
    public string? Text { get; set; }
}

// ──────────────────────────────────────────────
// Template Message Result (returned by TemplateService)
// ──────────────────────────────────────────────
public class TemplateMessage
{
    public string TemplateName { get; set; } = string.Empty;
    public List<string> BodyParameters { get; set; } = new();
    public List<TemplateButtonParameter> ButtonParameters { get; set; } = new();
    public bool HasButtons => ButtonParameters.Count > 0;

    public TemplateMessage(string templateName, List<string> bodyParameters)
    {
        TemplateName = templateName;
        BodyParameters = bodyParameters;
    }

    public TemplateMessage(string templateName, List<string> bodyParameters, List<TemplateButtonParameter> buttonParameters)
    {
        TemplateName = templateName;
        BodyParameters = bodyParameters;
        ButtonParameters = buttonParameters;
    }
}

public class TemplateButtonParameter
{
    public int Index { get; set; }
    public string DynamicUrlSuffix { get; set; } = string.Empty;

    public TemplateButtonParameter(int index, string dynamicUrlSuffix)
    {
        Index = index;
        DynamicUrlSuffix = dynamicUrlSuffix;
    }
}

// ──────────────────────────────────────────────
// WhatsApp Cloud API - Send Text Message
// ──────────────────────────────────────────────
public class WhatsAppTextRequest
{
    [JsonProperty("messaging_product")]
    public string MessagingProduct { get; set; } = "whatsapp";

    [JsonProperty("to")]
    public string To { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "text";

    [JsonProperty("text")]
    public WhatsAppTextBody Text { get; set; } = new();
}

public class WhatsAppTextBody
{
    [JsonProperty("body")]
    public string Body { get; set; } = string.Empty;

    [JsonProperty("preview_url")]
    public bool PreviewUrl { get; set; } = false;
}

// ──────────────────────────────────────────────
// WhatsApp Cloud API - Mark as Read
// ──────────────────────────────────────────────
public class WhatsAppMarkReadRequest
{
    [JsonProperty("messaging_product")]
    public string MessagingProduct { get; set; } = "whatsapp";

    [JsonProperty("status")]
    public string Status { get; set; } = "read";

    [JsonProperty("message_id")]
    public string MessageId { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────
// WhatsApp Cloud API - Send Response
// ──────────────────────────────────────────────
public class WhatsAppSendResponse
{
    [JsonProperty("messaging_product")]
    public string MessagingProduct { get; set; } = string.Empty;

    [JsonProperty("contacts")]
    public List<WhatsAppContact>? Contacts { get; set; }

    [JsonProperty("messages")]
    public List<WhatsAppMessageId>? Messages { get; set; }
}

public class WhatsAppContact
{
    [JsonProperty("input")]
    public string Input { get; set; } = string.Empty;

    [JsonProperty("wa_id")]
    public string WaId { get; set; } = string.Empty;
}

public class WhatsAppMessageId
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
}

// ──────────────────────────────────────────────
// WhatsApp Cloud API - Incoming Webhook Payload
// ──────────────────────────────────────────────
public class WhatsAppWebhookPayload
{
    [JsonProperty("object")]
    public string Object { get; set; } = string.Empty;

    [JsonProperty("entry")]
    public List<WhatsAppEntry> Entry { get; set; } = new();
}

public class WhatsAppEntry
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("changes")]
    public List<WhatsAppChange> Changes { get; set; } = new();
}

public class WhatsAppChange
{
    [JsonProperty("value")]
    public WhatsAppChangeValue Value { get; set; } = new();

    [JsonProperty("field")]
    public string Field { get; set; } = string.Empty;
}

public class WhatsAppChangeValue
{
    [JsonProperty("messaging_product")]
    public string MessagingProduct { get; set; } = string.Empty;

    [JsonProperty("metadata")]
    public WhatsAppMetadata? Metadata { get; set; }

    [JsonProperty("contacts")]
    public List<WhatsAppIncomingContact>? Contacts { get; set; }

    [JsonProperty("messages")]
    public List<WhatsAppIncomingMessage>? Messages { get; set; }

    [JsonProperty("statuses")]
    public List<WhatsAppStatus>? Statuses { get; set; }
}

public class WhatsAppMetadata
{
    [JsonProperty("display_phone_number")]
    public string DisplayPhoneNumber { get; set; } = string.Empty;

    [JsonProperty("phone_number_id")]
    public string PhoneNumberId { get; set; } = string.Empty;
}

public class WhatsAppIncomingContact
{
    [JsonProperty("profile")]
    public WhatsAppProfile? Profile { get; set; }

    [JsonProperty("wa_id")]
    public string WaId { get; set; } = string.Empty;
}

public class WhatsAppProfile
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;
}

public class WhatsAppIncomingMessage
{
    [JsonProperty("from")]
    public string From { get; set; } = string.Empty;

    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = string.Empty;

    [JsonProperty("text")]
    public WhatsAppIncomingText? Text { get; set; }
}

public class WhatsAppIncomingText
{
    [JsonProperty("body")]
    public string Body { get; set; } = string.Empty;
}

public class WhatsAppStatus
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    [JsonProperty("timestamp")]
    public string Timestamp { get; set; } = string.Empty;

    [JsonProperty("recipient_id")]
    public string RecipientId { get; set; } = string.Empty;

    [JsonProperty("errors")]
    public List<WhatsAppStatusError>? Errors { get; set; }
}

public class WhatsAppStatusError
{
    [JsonProperty("code")]
    public int Code { get; set; }

    [JsonProperty("title")]
    public string Title { get; set; } = string.Empty;

    [JsonProperty("message")]
    public string Message { get; set; } = string.Empty;

    [JsonProperty("error_data")]
    public WhatsAppErrorData? ErrorData { get; set; }
}

public class WhatsAppErrorData
{
    [JsonProperty("details")]
    public string Details { get; set; } = string.Empty;
}
