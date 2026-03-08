-- Fix: "Table 'Customers' already exists" when EF migrations run
-- The database schema exists but __EFMigrationsHistory is out of sync.
-- Run this against the traditional_eats_customer database, then restart customer-service.
--
-- Docker Compose: docker compose -f deploy/digitalocean/docker-compose.prod.yml exec mysql mysql -uroot -p$MYSQL_ROOT_PASSWORD traditional_eats_customer < deploy/digitalocean/scripts/fix-customer-migrations.sql
-- Or connect to MySQL and run manually:

USE traditional_eats_customer;

-- Ensure __EFMigrationsHistory exists (EF creates it; if migrations never succeeded it might not)
CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Mark InitialCreate as applied (skip if already present)
INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260119200813_InitialCreate', '8.0.0');
