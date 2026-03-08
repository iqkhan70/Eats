#!/usr/bin/env bash
# Fix "Table 'Customers' already exists" - mark InitialCreate migration as applied
# Usage: ./fix-customer-migrations.sh
# Requires: .env with MYSQL_ROOT_PASSWORD, mysql container running

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
SQL_FILE="$SCRIPT_DIR/fix-customer-migrations.sql"

cd "$DEPLOY_DIR"

if [ -f .env ]; then
    set -a
    source .env
    set +a
fi

if [ -z "$MYSQL_ROOT_PASSWORD" ]; then
    echo "Error: MYSQL_ROOT_PASSWORD not set in .env"
    exit 1
fi

echo "Fixing CustomerService migration history..."
docker compose -f docker-compose.prod.yml --env-file .env exec -T mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" traditional_eats_customer < "$SQL_FILE"
echo "✓ Done. Restart customer-service: docker compose -f docker-compose.prod.yml up -d customer-service"
