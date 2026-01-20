# Setup Instructions

## Configuration Files

**Important**: The following files contain sensitive information and are NOT committed to the repository:

- All `appsettings.Development.json` files in each service
- `src/shared/TraditionEats.BuildingBlocks/Configuration/appsettings.Shared.json`

## Initial Setup

1. **Copy example files** to create your local configuration:

```bash
# For each service, copy the example file:
cp src/services/TraditionEats.CatalogService/appsettings.Development.json.example \
   src/services/TraditionEats.CatalogService/appsettings.Development.json

# Repeat for all services:
# - TraditionEats.IdentityService
# - TraditionEats.CustomerService
# - TraditionEats.OrderService
# - TraditionEats.CatalogService
# - TraditionEats.PaymentService
# - TraditionEats.DeliveryService
# - TraditionEats.NotificationService
# - TraditionEats.RestaurantService
# - TraditionEats.PromotionService
# - TraditionEats.ReviewService
# - TraditionEats.SupportService
# - TraditionEats.AIService

# For shared configuration:
cp src/shared/TraditionEats.BuildingBlocks/Configuration/appsettings.Shared.json.example \
   src/shared/TraditionEats.BuildingBlocks/Configuration/appsettings.Shared.json
```

2. **Update the configuration files** with your actual:
   - Database passwords
   - JWT secrets
   - Encryption keys
   - API keys (Stripe, OpenAI, Mailgun, Vonage, etc.)

3. **Never commit** these files to Git - they are in `.gitignore`

## Security Best Practices

- Use environment variables for production
- Use Azure Key Vault or AWS Secrets Manager for production secrets
- Never commit real API keys or passwords
- Rotate secrets regularly
