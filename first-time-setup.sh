#!/bin/bash
# ──────────────────────────────────────────────
# First-Time VPS Setup Script
# Run this ONCE on a fresh Ubuntu 22.04/24.04 VPS
# Usage: chmod +x first-time-setup.sh && sudo ./first-time-setup.sh
# ──────────────────────────────────────────────

set -e

echo "🚀 Starting first-time server setup..."

# ──────────────────────────────────────────────
# 1. System Updates
# ──────────────────────────────────────────────
echo "📦 Updating system packages..."
apt update && apt upgrade -y

# ──────────────────────────────────────────────
# 2. Install Docker
# ──────────────────────────────────────────────
echo "🐳 Installing Docker..."
curl -fsSL https://get.docker.com | sh
systemctl enable docker
systemctl start docker

# ──────────────────────────────────────────────
# 3. Install Docker Compose
# ──────────────────────────────────────────────
echo "🐳 Installing Docker Compose..."
apt install -y docker-compose-plugin

# Verify installation
docker --version
docker compose version

# ──────────────────────────────────────────────
# 4. Configure Firewall (UFW)
# ──────────────────────────────────────────────
echo "🔥 Configuring firewall..."
apt install -y ufw
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp    # SSH
ufw allow 80/tcp    # HTTP
ufw allow 443/tcp   # HTTPS
ufw --force enable
ufw status

# ──────────────────────────────────────────────
# 5. Install Certbot (for SSL)
# ──────────────────────────────────────────────
echo "🔒 Installing Certbot..."
apt install -y certbot

# ──────────────────────────────────────────────
# 6. Create app directory
# ──────────────────────────────────────────────
echo "📁 Creating app directory..."
mkdir -p /opt/shopify-whatsapp
chown $SUDO_USER:$SUDO_USER /opt/shopify-whatsapp

# ──────────────────────────────────────────────
# 7. Install Git
# ──────────────────────────────────────────────
echo "📦 Installing Git..."
apt install -y git

# ──────────────────────────────────────────────
# Done!
# ──────────────────────────────────────────────
echo ""
echo "✅ Server setup complete!"
echo ""
echo "Next steps:"
echo "  1. Clone your repo:    cd /opt/shopify-whatsapp && git clone <your-repo-url> ."
echo "  2. Create .env file:   cp .env.example .env && nano .env"
echo "  3. Update nginx.conf:  Replace YOUR_DOMAIN with your actual domain"
echo "  4. Get SSL cert:       certbot certonly --standalone -d api.yourdomain.com"
echo "  5. Deploy:             docker compose up -d --build"
echo ""
