#!/usr/bin/env bash
# Fix CatalogService migration state when tables exist but __EFMigrationsHistory is missing the record.
# Run from /opt/kram on the droplet: bash deploy/digitalocean/scripts/fix-catalog-migrations.sh
#
# Symptom: "Table 'Categories' already exists" - EF tries to apply InitialCreate but tables were
# created by a previous run or manually.
set -e
cd "$(dirname "$0")/.."
source .env 2>/dev/null || true
export MYSQL_ROOT_PASSWORD="${MYSQL_ROOT_PASSWORD:?Set MYSQL_ROOT_PASSWORD in .env}"

echo "Inserting missing CatalogService migration record into __EFMigrationsHistory..."
docker compose -f deploy/digitalocean/docker-compose.prod.yml exec -T mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" traditional_eats_catalog -e "
INSERT IGNORE INTO \`__EFMigrationsHistory\` (\`MigrationId\`, \`ProductVersion\`)
VALUES ('20260119200651_InitialCreate', '8.0.0');
"
echo "Done. Restart catalog-service: docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env up -d --force-recreate catalog-service"
