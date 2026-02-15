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

# Step 4: Force recreate edge container with new config (nginx.conf is bind-mounted from host)
echo ""
echo "Step 4: Restarting edge container with HTTPS config..."
docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p "$COMPOSE_PROJECT" up -d --force-recreate edge

# Wait for edge to be running (avoid "Container is restarting" when we exec)
echo "Waiting for edge container to be running..."
COMPOSE_CMD="docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p $COMPOSE_PROJECT"
for i in 1 2 3 4 5 6 7 8 9 10 11 12 13 14 15; do
  if $COMPOSE_CMD exec -T edge true 2>/dev/null; then
    break
  fi
  if [ "$i" -eq 15 ]; then
    echo "WARNING: Edge container not ready after 30s (may be restarting). Stopping and starting once..."
    $COMPOSE_CMD stop edge 2>/dev/null || true
    sleep 2
    $COMPOSE_CMD up -d edge
    sleep 5
    for j in 1 2 3 4 5 6 7 8 9 10; do
      if $COMPOSE_CMD exec -T edge true 2>/dev/null; then break; fi
      sleep 2
    done
  fi
  sleep 2
done

# Step 5: Verify nginx.conf inside container (config is mounted from host; exec may fail if container still restarting)
echo ""
echo "Step 5: Verifying nginx.conf inside container..."
if $COMPOSE_CMD exec -T edge grep -q "listen 443 ssl" /etc/nginx/nginx.conf 2>/dev/null; then
  echo "✓ HTTPS block confirmed in container"
else
  echo "WARNING: Could not verify inside container (container may still be starting). nginx.conf was updated on host at nginx/nginx.conf and is bind-mounted; edge will use it when running."
  if ! $COMPOSE_CMD exec -T edge true 2>/dev/null; then
    echo "  Run: docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p $COMPOSE_PROJECT logs edge"
    echo "  Then: docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env -p $COMPOSE_PROJECT restart edge"
  fi
fi

# Step 6: Test nginx config (only if we can exec)
echo ""
echo "Step 6: Testing nginx configuration..."
if $COMPOSE_CMD exec -T edge true 2>/dev/null; then
  if $COMPOSE_CMD exec -T edge nginx -t 2>/dev/null; then
    echo "✓ nginx configuration valid"
  else
    echo "WARNING: nginx -t failed in container:"
    $COMPOSE_CMD exec -T edge nginx -t 2>&1 || true
    echo "  Check: $COMPOSE_CMD logs edge"
  fi
else
  echo "  (Container not ready to run nginx -t; check logs and restart edge if needed)"
fi

# Step 7: Reload nginx (if container is running)
echo ""
echo "Step 7: Reloading nginx..."
if $COMPOSE_CMD exec -T edge true 2>/dev/null; then
  $COMPOSE_CMD exec edge nginx -s reload 2>/dev/null || $COMPOSE_CMD restart edge
else
  echo "  (Container not ready; config is on host. Restart edge when it is running: $COMPOSE_CMD restart edge)"
fi
sleep 2

# Step 8: Verify port 443 is listening
echo ""
echo "Step 8: Verifying HTTPS is listening..."
if $COMPOSE_CMD exec -T edge true 2>/dev/null && $COMPOSE_CMD exec -T edge sh -c "ss -tlnp 2>/dev/null | grep -q ':443 ' || netstat -tlnp 2>/dev/null | grep -q ':443 '" 2>/dev/null; then
  echo "✓ Port 443 is listening"
else
  echo "  (Run: $COMPOSE_CMD logs edge  to debug if HTTPS is not working)"
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
