#!/usr/bin/env bash
# Run on the server to obtain a Let's Encrypt cert and enable HTTPS for the edge container.
# Usage: ./setup-https.sh <domain>
# Example: ./setup-https.sh www.caseflowstage.store
# Prereqs: (1) Domain DNS points to this server's IP. (2) Stack is running (edge on port 80). (3) .env has DOMAIN set (or pass domain as arg).

set -e

DOMAIN="${1:-}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# Script is at: /opt/kram/deploy/digitalocean/scripts/setup-https.sh
# So APP_DIR should be /opt/kram (go up 3 levels: scripts -> digitalocean -> deploy -> kram)
APP_DIR="${SCRIPT_DIR}/../../.."

if [ -z "$DOMAIN" ]; then
  if [ -f "$APP_DIR/.env" ]; then
    DOMAIN=$(grep -E '^DOMAIN=' "$APP_DIR/.env" 2>/dev/null | cut -d= -f2- || true)
  fi
fi

if [ -z "$DOMAIN" ] || [ "$DOMAIN" = "http" ] || [ "$DOMAIN" = "https" ] || [[ "$DOMAIN" != *.* ]]; then
  echo "Usage: $0 <domain>"
  echo "Example: $0 www.caseflowstage.store"
  echo "Ensure your domain DNS points to this server's IP, then run certbot and regenerate nginx."
  exit 1
fi

cd "$APP_DIR"
APP_DIR_ABS="$(pwd)"

# Compose project name (used for volume names). Default: kram. If certbot fails with "volume not found", run: docker compose -f deploy/digitalocean/docker-compose.prod.yml ps and check the project name (e.g. digitalocean), then: export COMPOSE_PROJECT=digitalocean; ./deploy/digitalocean/scripts/setup-https.sh <domain>
COMPOSE_PROJECT="${COMPOSE_PROJECT:-kram}"
VOLUME_WWW="${COMPOSE_PROJECT}_certbot_www"
VOLUME_CERTS="${COMPOSE_PROJECT}_certbot_certs"

echo "=========================================="
echo "Setting up HTTPS for $DOMAIN"
echo "=========================================="

# Step 1: Get certificate
echo "Step 1: Obtaining Let's Encrypt certificate..."
docker run --rm \
  -v "$VOLUME_WWW:/var/www/certbot:rw" \
  -v "$VOLUME_CERTS:/etc/letsencrypt:rw" \
  certbot/certbot certonly --webroot -w /var/www/certbot \
  -d "$DOMAIN" \
  --non-interactive --agree-tos --register-unsafely-without-email

echo "✓ Certificate obtained"

# Step 2: Verify certificates exist
echo ""
echo "Step 2: Verifying certificates..."
if ! docker run --rm -v "$VOLUME_CERTS:/certs:ro" alpine ls "/certs/live/$DOMAIN/fullchain.pem" >/dev/null 2>&1; then
  echo "ERROR: Certificates not found at /etc/letsencrypt/live/$DOMAIN/"
  exit 1
fi
echo "✓ Certificates verified"

# Step 3: Generate nginx.conf with HTTPS
echo ""
echo "Step 3: Generating nginx.conf with HTTPS..."
# Create nginx directory in the location docker-compose expects (relative to compose file)
mkdir -p nginx
mkdir -p deploy/digitalocean/nginx

# Load .env and set required vars
source .env 2>/dev/null || true
export DOMAIN
export HTTPS_ONLY="${HTTPS_ONLY:-false}"
export CERTS_READY=1

# Generate config to both locations (for compatibility)
bash deploy/digitalocean/scripts/generate-nginx-conf.sh > nginx/nginx.conf
cp nginx/nginx.conf deploy/digitalocean/nginx/nginx.conf

# Verify HTTPS block exists
if ! grep -q "listen 443 ssl" nginx/nginx.conf; then
  echo "ERROR: Generated nginx.conf does not contain 'listen 443 ssl'"
  exit 1
fi
echo "✓ nginx.conf generated with HTTPS block"

# Step 4: Force recreate edge container with new config
echo ""
echo "Step 4: Restarting edge container with HTTPS config..."
docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" up -d --force-recreate edge

# Wait for container to start
sleep 3

# Step 5: Verify nginx.conf inside container and fix if needed
echo ""
echo "Step 5: Verifying nginx.conf inside container..."
sleep 2
if ! docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" exec -T edge grep -q "listen 443 ssl" /etc/nginx/nginx.conf 2>/dev/null; then
  echo "WARNING: Container nginx.conf doesn't have HTTPS block. Copying directly..."
  docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" exec -T edge sh -c "cat > /etc/nginx/nginx.conf" < nginx/nginx.conf
  docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" exec edge nginx -s reload
  sleep 2
fi

# Verify it's there now
if docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" exec -T edge grep -q "listen 443 ssl" /etc/nginx/nginx.conf 2>/dev/null; then
  echo "✓ HTTPS block confirmed in container"
else
  echo "ERROR: Failed to update nginx.conf in container"
  exit 1
fi

# Step 6: Test nginx config
echo ""
echo "Step 6: Testing nginx configuration..."
if ! docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" exec -T edge nginx -t >/dev/null 2>&1; then
  echo "ERROR: nginx configuration test failed"
  docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" exec edge nginx -t
  exit 1
fi
echo "✓ nginx configuration valid"

# Step 7: Reload nginx to apply changes
echo ""
echo "Step 7: Reloading nginx..."
docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" exec edge nginx -s reload || docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" restart edge
sleep 2

# Step 8: Verify port 443 is listening
echo ""
echo "Step 8: Verifying HTTPS is listening..."
if docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" exec -T edge sh -c "netstat -tlnp 2>/dev/null | grep -q ':443 ' || ss -tlnp 2>/dev/null | grep -q ':443 '" 2>/dev/null; then
  echo "✓ Port 443 is listening"
else
  echo "Checking again..."
  sleep 2
  docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" exec -T edge sh -c "ss -tlnp | grep 443 || netstat -tlnp | grep 443" || echo "Port check inconclusive, but nginx should be running"
fi

echo ""
echo "=========================================="
echo "HTTPS setup complete!"
echo "=========================================="
echo ""
echo "Test: https://$DOMAIN"
echo ""
echo "If HTTPS doesn't work:"
echo "  1. Check DNS: dig $DOMAIN +short (should return your server IP)"
echo "  2. Check firewall: sudo ufw allow 443"
echo "  3. Check logs: docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p $COMPOSE_PROJECT logs edge"
