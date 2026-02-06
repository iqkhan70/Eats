#!/usr/bin/env bash
# Apply database migrations for ChatService
# Usage: ./apply-chat-migrations.sh

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APP_DIR="${SCRIPT_DIR}/../.."

cd "$APP_DIR"

if [ -f .env ]; then
    set -a
    source .env
    set +a
fi

echo "Applying migrations for ChatService..."
echo "Database: traditional_eats_chat"

if command -v docker &>/dev/null && docker ps | grep -q chat-service; then
    echo "Running migrations via docker exec..."
    docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env exec -T chat-service sh -c "
        cd /src/src/services/TraditionalEats.ChatService &&
        dotnet ef database update --no-build
    " || {
        echo "Migration via docker exec failed, trying alternative method..."
        docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env exec chat-service dotnet ef database update
    }
else
    echo "ChatService container not running. Start it first with:"
    echo "  docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env up -d chat-service"
    exit 1
fi

echo "✓ Migrations applied successfully"
