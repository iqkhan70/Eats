# Database Name Change Summary

## ✅ All Database References Updated

All database references have been changed from `tradition_eats*` to `traditional_eats*`.

## Changed Database Names

| Old Name | New Name |
|----------|----------|
| `tradition_eats` | `traditional_eats` |
| `tradition_eats_identity` | `traditional_eats_identity` |
| `tradition_eats_customer` | `traditional_eats_customer` |
| `tradition_eats_order` | `traditional_eats_order` |
| `tradition_eats_catalog` | `traditional_eats_catalog` |
| `tradition_eats_payment` | `traditional_eats_payment` |
| `tradition_eats_delivery` | `traditional_eats_delivery` |
| `tradition_eats_notification` | `traditional_eats_notification` |
| `tradition_eats_restaurant` | `traditional_eats_restaurant` |
| `tradition_eats_promotion` | `traditional_eats_promotion` |
| `tradition_eats_review` | `traditional_eats_review` |
| `tradition_eats_support` | `traditional_eats_support` |
| `tradition_eats_ai` | `traditional_eats_ai` |

## Files Updated

### Configuration Files
- ✅ `deploy/docker-compose.yml` - MYSQL_DATABASE and connection string examples
- ✅ All `appsettings.Development.json.example` files (12 services)

### Code Files
- ✅ All `DesignTimeDbContextFactory.cs` files (12 services)

### Documentation
- ✅ `CONFIGURATION.md` - Updated all examples
- ✅ `deploy/README.md` - Updated all examples

## Next Steps

### 1. Delete Old Databases

You mentioned you'll delete the old databases. You can do this via MySQL:

```sql
-- Connect to MySQL
mysql -u root -p

-- Drop old databases
DROP DATABASE IF EXISTS tradition_eats;
DROP DATABASE IF EXISTS tradition_eats_identity;
DROP DATABASE IF EXISTS tradition_eats_customer;
DROP DATABASE IF EXISTS tradition_eats_order;
DROP DATABASE IF EXISTS tradition_eats_catalog;
DROP DATABASE IF EXISTS tradition_eats_payment;
DROP DATABASE IF EXISTS tradition_eats_delivery;
DROP DATABASE IF EXISTS tradition_eats_notification;
DROP DATABASE IF EXISTS tradition_eats_restaurant;
DROP DATABASE IF EXISTS tradition_eats_promotion;
DROP DATABASE IF EXISTS tradition_eats_review;
DROP DATABASE IF EXISTS tradition_eats_support;
DROP DATABASE IF EXISTS tradition_eats_ai;
```

### 2. Create New Databases

The databases will be created automatically when you run migrations, or you can create them manually:

```sql
CREATE DATABASE IF NOT EXISTS traditional_eats;
CREATE DATABASE IF NOT EXISTS traditional_eats_identity;
CREATE DATABASE IF NOT EXISTS traditional_eats_customer;
CREATE DATABASE IF NOT EXISTS traditional_eats_order;
CREATE DATABASE IF NOT EXISTS traditional_eats_catalog;
CREATE DATABASE IF NOT EXISTS traditional_eats_payment;
CREATE DATABASE IF NOT EXISTS traditional_eats_delivery;
CREATE DATABASE IF NOT EXISTS traditional_eats_notification;
CREATE DATABASE IF NOT EXISTS traditional_eats_restaurant;
CREATE DATABASE IF NOT EXISTS traditional_eats_promotion;
CREATE DATABASE IF NOT EXISTS traditional_eats_review;
CREATE DATABASE IF NOT EXISTS traditional_eats_support;
CREATE DATABASE IF NOT EXISTS traditional_eats_ai;
```

### 3. Run Migrations

After creating the databases, run migrations for each service:

```bash
# Identity Service
cd src/services/TraditionalEats.IdentityService
dotnet ef database update

# Customer Service
cd ../TraditionalEats.CustomerService
dotnet ef database update

# Order Service
cd ../TraditionalEats.OrderService
dotnet ef database update

# Catalog Service
cd ../TraditionalEats.CatalogService
dotnet ef database update

# Payment Service
cd ../TraditionalEats.PaymentService
dotnet ef database update

# Delivery Service
cd ../TraditionalEats.DeliveryService
dotnet ef database update

# Notification Service
cd ../TraditionalEats.NotificationService
dotnet ef database update

# Restaurant Service
cd ../TraditionalEats.RestaurantService
dotnet ef database update

# Promotion Service
cd ../TraditionalEats.PromotionService
dotnet ef database update

# Review Service
cd ../TraditionalEats.ReviewService
dotnet ef database update

# Support Service
cd ../TraditionalEats.SupportService
dotnet ef database update

# AI Service
cd ../TraditionalEats.AIService
dotnet ef database update
```

### 4. Update Your Local appsettings.Development.json Files

If you have local `appsettings.Development.json` files (not the `.example` ones), update them with the new database names:

```json
{
  "ConnectionStrings": {
    "IdentityDb": "server=localhost;port=3306;database=traditional_eats_identity;user=root;password=YOUR_PASSWORD"
  }
}
```

## Verification

All references to `tradition_eats` have been replaced with `traditional_eats`. You can verify by searching:

```bash
grep -r "tradition_eats" . --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj
```

This should return no results (except in this summary file).

## Important Notes

- ⚠️ **Old databases will not be automatically migrated** - You'll need to manually migrate data if needed
- ⚠️ **Update your local `appsettings.Development.json` files** if they exist (they're gitignored)
- ✅ **All example files and code have been updated**
- ✅ **All documentation has been updated**
