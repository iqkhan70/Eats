#!/usr/bin/env bash
# Apply database migrations for IdentityService
# Usage: ./apply-migrations.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="${SCRIPT_DIR}/../.."

cd "$APP_DIR"

# Load .env
if [ -f .env ]; then
    set -a
    source .env
    set +a
fi

echo "Applying migrations for IdentityService..."
echo "Database: traditional_eats_identity"

# Run migrations using dotnet ef or docker exec
if command -v docker &>/dev/null && docker ps | grep -q identity-service; then
    echo "Running migrations via docker exec..."
    docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env exec -T identity-service sh -c "
        cd /src/src/services/TraditionalEats.IdentityService &&
        dotnet ef database update --no-build
    " || {
        echo "Migration via docker exec failed, trying alternative method..."
        docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env exec identity-service dotnet ef database update
    }
else
    echo "IdentityService container not running. Start it first with:"
    echo "  docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env up -d identity-service"
    exit 1
fi

echo "✓ Migrations applied successfully"
