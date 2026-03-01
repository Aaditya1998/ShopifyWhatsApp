# 🛒💬 ShopifyWhatsApp Integration

A .NET 8 middleware server that connects your Shopify store with the official WhatsApp Business Cloud API. Automatically sends customers WhatsApp notifications for order confirmations, shipping updates, cancellations, and abandoned cart recovery.

## Architecture

```
Shopify Store
    ↓ (webhooks)
This Server (.NET 8 API)
    ↓ (Cloud API)
WhatsApp Business API
    ↓
Customer's WhatsApp
```

## Features

- ✅ **Order Confirmation** — Instant WhatsApp message when order is placed
- 🚚 **Shipping Updates** — Tracking info sent when order ships
- ❌ **Cancellation Notices** — Customer notified of cancellations
- 🛒 **Abandoned Cart Recovery** — Remind customers to complete purchase
- 💬 **Incoming Messages** — Receive and auto-reply to customer messages
- 🔐 **Secure** — HMAC validation on all webhooks
- 📊 **Message Logging** — Track all sent messages and delivery status
- ✋ **Consent Management** — STOP/START opt-out/in support

## Prerequisites

1. **Shopify Store** with admin access
2. **Meta Developer Account** with WhatsApp Business API access
3. **VPS** (DigitalOcean, Hostinger, etc.) with Ubuntu 22.04+
4. **Domain** pointed to your VPS (e.g., `api.yourstore.com`)

## Quick Start

### 1. Clone & Configure

```bash
git clone <your-repo-url>
cd ShopifyWhatsApp
cp .env.example .env
nano .env  # Fill in all values
```

### 2. Get Your API Credentials

**Shopify:**
- Go to Shopify Admin → Settings → Apps → Develop apps
- Create a new app, grant scopes: `read_orders`, `read_checkouts`, `read_customers`
- Copy API Key, API Secret, and Access Token

**WhatsApp:**
- Go to [developers.facebook.com](https://developers.facebook.com)
- Create app → Add WhatsApp product
- Copy Phone Number ID, Access Token, and App Secret
- Set a custom Verify Token (any string you choose)

### 3. Create WhatsApp Templates

In [Meta Business Manager](https://business.facebook.com) → WhatsApp Manager → Message Templates:

| Template Name | Category | Body |
|---|---|---|
| `order_confirmation` | UTILITY | `Hi {{1}}, your order {{2}} is confirmed! Items: {{3}}. Total: {{4}} {{5}}` |
| `shipping_update` | UTILITY | `Your order {{1}} shipped! Carrier: {{2}}, Tracking: {{3}}. Track: {{4}}` |
| `order_cancelled` | UTILITY | `Hi {{1}}, order {{2}} cancelled. Reason: {{3}}. Reply with questions.` |
| `abandoned_cart` | MARKETING | `Hi {{1}}, you left items in cart: {{2}}. Total: {{3}} {{4}}. Buy now: {{5}}` |

Wait for approval (24-48 hours).

### 4. Deploy

```bash
# First time on VPS:
sudo ./first-time-setup.sh

# Get SSL certificate:
sudo certbot certonly --standalone -d api.yourdomain.com

# Update nginx.conf — replace YOUR_DOMAIN with your domain

# Deploy:
./deploy.sh
```

### 5. Verify

```bash
# Check health
curl https://api.yourdomain.com/health

# Check logs
docker compose logs -f app
```

## Local Development

```bash
# Install .NET 8 SDK
# Install PostgreSQL locally or use Docker

# Run database
docker run -d --name postgres -e POSTGRES_PASSWORD=dev -p 5432:5432 postgres:16

# Update appsettings.json with your credentials

# Run the app
dotnet run --project src/ShopifyWhatsApp.csproj

# Expose via ngrok for webhook testing
ngrok http 5000
```

## Using Claude Code

This project includes a `CLAUDE.md` file optimized for Claude Code. Open the project in Claude Code for AI-assisted development:

```bash
npm install -g @anthropic-ai/claude-code
cd ShopifyWhatsApp
claude
```

Example prompts:
- "Add a new webhook handler for refunds"
- "Add rate limiting per customer phone number"  
- "Create an admin dashboard endpoint to view message stats"
- "Add support for WhatsApp interactive buttons in order confirmations"

## License

MIT
