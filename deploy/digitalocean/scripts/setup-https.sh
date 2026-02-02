#!/usr/bin/env bash
# Run on the server to obtain a Let's Encrypt cert and enable HTTPS for the edge container.
# Usage: ./setup-https.sh <domain>
# Example: ./setup-https.sh www.caseflowstage.store
# Prereqs: (1) Domain DNS points to this server's IP. (2) Stack is running (edge on port 80). (3) .env has DOMAIN set (or pass domain as arg).

set -e

DOMAIN="${1:-}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="${SCRIPT_DIR}/../.."

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

# Compose project name (used for volume names). Default: traditionaleats. If certbot fails with "volume not found", run: docker compose -f deploy/digitalocean/docker-compose.prod.yml ps and check the project name (e.g. digitalocean), then: export COMPOSE_PROJECT=digitalocean; ./deploy/digitalocean/scripts/setup-https.sh <domain>
COMPOSE_PROJECT="${COMPOSE_PROJECT:-traditionaleats}"
VOLUME_WWW="${COMPOSE_PROJECT}_certbot_www"
VOLUME_CERTS="${COMPOSE_PROJECT}_certbot_certs"

echo "Obtaining certificate for $DOMAIN (webroot: $VOLUME_WWW, certs: $VOLUME_CERTS)..."

docker run --rm \
  -v "$VOLUME_WWW:/var/www/certbot:rw" \
  -v "$VOLUME_CERTS:/etc/letsencrypt:rw" \
  certbot/certbot certonly --webroot -w /var/www/certbot \
  -d "$DOMAIN" \
  --non-interactive --agree-tos --register-unsafely-without-email

echo "Certificate obtained. Regenerating nginx.conf and restarting edge..."

# Regenerate nginx with DOMAIN and HTTPS_ONLY from .env
source .env 2>/dev/null || true
export DOMAIN
export HTTPS_ONLY="${HTTPS_ONLY:-false}"
bash deploy/digitalocean/scripts/generate-nginx-conf.sh > deploy/digitalocean/nginx/nginx.conf

# Restart edge so nginx loads the new config with 443
docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env up -d edge

echo "Done. Test https://$DOMAIN (ensure DNS points to this server)."
