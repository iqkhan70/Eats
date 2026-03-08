# Troubleshooting

## CustomerService: "Table 'Customers' already exists"

**Symptom:** customer-service fails to start with:
```
MySqlConnector.MySqlException: Table 'Customers' already exists
Failed to apply CustomerService migrations (attempt X/10)
```

**Cause:** The database schema exists (from a previous run or manual setup) but `__EFMigrationsHistory` is out of sync. EF Core tries to run the InitialCreate migration again.

**Fix (Docker Compose):**
```bash
cd deploy/digitalocean
chmod +x scripts/fix-customer-migrations.sh
./scripts/fix-customer-migrations.sh
docker compose -f docker-compose.prod.yml --env-file .env up -d customer-service
```

**Fix (manual SQL):** Connect to MySQL and run:
```sql
USE traditional_eats_customer;

CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
  `MigrationId` varchar(150) NOT NULL,
  `ProductVersion` varchar(32) NOT NULL,
  PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260119200813_InitialCreate', '8.0.0');
```

**App Platform:** Use the DO database connection (from dashboard) and run the SQL above against the `traditional_eats_customer` database.
