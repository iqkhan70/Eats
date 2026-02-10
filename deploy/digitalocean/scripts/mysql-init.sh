#!/usr/bin/env bash
# Create all TraditionalEats databases if missing. Run from /opt/traditionaleats on the server.
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/.."
source .env 2>/dev/null || true
export MYSQL_ROOT_PASSWORD="${MYSQL_ROOT_PASSWORD:?Set MYSQL_ROOT_PASSWORD in .env}"
docker compose -f docker-compose.prod.yml exec -T mysql mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -e "
CREATE DATABASE IF NOT EXISTS traditional_eats_identity;
CREATE DATABASE IF NOT EXISTS traditional_eats_customer;
CREATE DATABASE IF NOT EXISTS traditional_eats_order;
CREATE DATABASE IF NOT EXISTS traditional_eats_catalog;
CREATE DATABASE IF NOT EXISTS traditional_eats_notification;
CREATE DATABASE IF NOT EXISTS traditional_eats_restaurant;
CREATE DATABASE IF NOT EXISTS traditional_eats_payment;
CREATE DATABASE IF NOT EXISTS traditional_eats_chat;
CREATE DATABASE IF NOT EXISTS traditional_eats_ai;
CREATE DATABASE IF NOT EXISTS traditional_eats_review;
CREATE DATABASE IF NOT EXISTS traditional_eats_document;
"
echo "MySQL databases ready."
