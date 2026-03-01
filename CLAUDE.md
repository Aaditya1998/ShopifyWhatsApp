# ShopifyWhatsApp Integration - Claude Code Instructions

## Project Overview
This is a .NET 8 ASP.NET Core Web API that integrates Shopify with the official WhatsApp Business Cloud API. It receives Shopify webhook events (new orders, fulfillments, cancellations, abandoned carts) and sends WhatsApp notifications to customers.

## Tech Stack
- **Runtime**: .NET 8 / ASP.NET Core Web API
- **Language**: C#
- **Database**: PostgreSQL via Entity Framework Core
- **Logging**: Serilog (console + file)
- **HTTP Resilience**: Polly (retry + circuit breaker)
- **Deployment**: Docker + Nginx + Let's Encrypt

## Project Structure
```
src/
├── Controllers/          # Webhook endpoints (Shopify + WhatsApp)
├── Services/             # Business logic (WhatsApp API, Shopify API, Templates, Consent)
├── Models/               # DTOs and database entities
├── Middleware/            # HMAC validation for webhooks
├── Data/                 # EF Core DbContext
├── Program.cs            # App entry point, DI configuration
└── appsettings.json      # Configuration (use env vars in production)
```

## Key Design Decisions
1. **HMAC Validation**: All Shopify webhooks are validated using HMAC-SHA256 before processing
2. **Consent-first**: Messages are only sent to customers who have opted in
3. **Template-based messaging**: Uses WhatsApp approved templates (required by Meta outside 24hr window)
4. **24-hour window tracking**: Tracks when customers last messaged to know if free-form text is allowed
5. **Message logging**: Every sent message is logged for debugging and analytics

## Development Commands
```bash
# Restore packages
dotnet restore src/ShopifyWhatsApp.csproj

# Run locally
dotnet run --project src/ShopifyWhatsApp.csproj

# Create EF migration
dotnet ef migrations add <MigrationName> --project src/ShopifyWhatsApp.csproj

# Apply migrations
dotnet ef database update --project src/ShopifyWhatsApp.csproj

# Build Docker
docker compose build

# Run with Docker
docker compose up -d
```

## Testing Webhooks Locally
Use ngrok to expose local server:
```bash
ngrok http 5000
# Then use the ngrok URL as webhook address
```

## API Endpoints
| Method | Path | Purpose |
|--------|------|---------|
| POST | /api/webhooks/shopify/order-created | New order notification |
| POST | /api/webhooks/shopify/order-fulfilled | Shipping notification |
| POST | /api/webhooks/shopify/order-cancelled | Cancellation notification |
| POST | /api/webhooks/shopify/checkout-abandoned | Abandoned cart recovery |
| GET  | /api/webhooks/whatsapp | Meta webhook verification |
| POST | /api/webhooks/whatsapp | Receive customer messages |
| GET  | /health | Health check |

## WhatsApp Templates Required
Create these in Meta Business Manager BEFORE deploying:
1. `order_confirmation` (UTILITY) - 5 params: name, order#, items, total, currency
2. `shipping_update` (UTILITY) - 4 params: order#, carrier, tracking#, trackingURL
3. `order_cancelled` (UTILITY) - 3 params: name, order#, reason
4. `abandoned_cart` (MARKETING) - 5 params: name, items, total, currency, cartURL

## Important Notes for Claude Code
- Always use `IOptions<T>` pattern for configuration
- Use `IHttpClientFactory` with named clients (never new HttpClient())
- Return 200 OK to Meta webhooks IMMEDIATELY, even on errors (or Meta will retry)
- Phone numbers must be in E.164 format without the + prefix
- Shopify webhook payloads should be validated with HMAC before processing
- Keep Polly retry policies for WhatsApp API calls (rate limiting is common)
