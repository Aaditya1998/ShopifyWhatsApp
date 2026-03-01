#!/bin/bash
# ──────────────────────────────────────────────
# Deployment Script
# Usage: chmod +x deploy.sh && ./deploy.sh
# ──────────────────────────────────────────────

set -e

APP_DIR="/opt/shopify-whatsapp"
COMPOSE_CMD="docker compose"

echo "🚀 Deploying ShopifyWhatsApp Integration..."

# ──────────────────────────────────────────────
# 1. Pull latest code
# ──────────────────────────────────────────────
echo "📥 Pulling latest code..."
cd $APP_DIR
git pull origin main

# ──────────────────────────────────────────────
# 2. Check .env exists
# ──────────────────────────────────────────────
if [ ! -f .env ]; then
    echo "❌ .env file not found! Copy .env.example to .env and fill in values."
    exit 1
fi

# ──────────────────────────────────────────────
# 3. Build and deploy
# ──────────────────────────────────────────────
echo "🔨 Building containers..."
$COMPOSE_CMD build --no-cache app

echo "🚀 Starting services..."
$COMPOSE_CMD up -d

# ──────────────────────────────────────────────
# 4. Wait for health check
# ──────────────────────────────────────────────
echo "⏳ Waiting for app to start..."
sleep 10

HEALTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/health 2>/dev/null || echo "000")

if [ "$HEALTH_STATUS" = "200" ]; then
    echo "✅ App is healthy!"
else
    echo "⚠️ Health check returned: $HEALTH_STATUS"
    echo "📋 Checking logs..."
    $COMPOSE_CMD logs --tail=30 app
fi

# ──────────────────────────────────────────────
# 5. Show status
# ──────────────────────────────────────────────
echo ""
echo "📊 Container status:"
$COMPOSE_CMD ps
echo ""
echo "✅ Deployment complete!"
echo ""
echo "Useful commands:"
echo "  View logs:     $COMPOSE_CMD logs -f app"
echo "  Restart:       $COMPOSE_CMD restart app"
echo "  Stop all:      $COMPOSE_CMD down"
echo "  DB shell:      $COMPOSE_CMD exec db psql -U postgres -d shopify_whatsapp"
