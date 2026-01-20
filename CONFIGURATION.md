# Shared Configuration Architecture

## Overview

To avoid duplicating configuration across all services, we use a **shared configuration file** that all services inherit from. Each service only needs to define its service-specific overrides.

## How It Works

1. **Shared Configuration**: `src/shared/TraditionalEats.BuildingBlocks/Configuration/appsettings.Shared.json`
   - Contains all common settings: Redis, JWT, RabbitMQ, Encryption, Logging
   - Single source of truth for infrastructure settings
   - Uses `localhost` for local development

2. **Production Shared Config**: `appsettings.Shared.Production.json`
   - Uses Docker service names: `mysql`, `redis`, `rabbitmq`
   - Automatically loaded when `ASPNETCORE_ENVIRONMENT=Production`

3. **Service-Specific Configuration**: Each service's `appsettings.Development.json`
   - Only contains service-specific settings (database connection strings, service-specific APIs)
   - Uses `localhost` for local MySQL connection
   - Automatically inherits all shared settings
   - Can override any shared setting if needed

4. **Configuration Loading Order**:
   - Shared config loads first (lowest priority)
   - Service-specific configs load after (can override shared values)
   - Environment variables load last (highest priority)

## Local Development vs Docker

### Local Development
- **MySQL**: Uses `localhost:3306` (your local MySQL instance)
- **Redis**: Uses `localhost:6379` (local or Docker)
- **RabbitMQ**: Uses `localhost:5672` (local or Docker)
- All connection strings point to `localhost`

### Docker/Production
- **MySQL**: Uses `mysql:3306` (Docker service name)
- **Redis**: Uses `redis:6379` (Docker service name)
- **RabbitMQ**: Uses `rabbitmq:5672` (Docker service name)
- Connection strings use Docker service names
- Environment variables can override any setting

## Benefits

✅ **Single Source of Truth**: Change Redis connection once, all services update  
✅ **Reduced Duplication**: No need to copy the same config to 12+ files  
✅ **Easier Maintenance**: Update shared settings in one place  
✅ **Service Flexibility**: Services can still override shared settings when needed  
✅ **Environment Aware**: Automatically uses correct hostnames for local vs Docker

## Example

### Shared Config (`appsettings.Shared.json`) - Development
```json
{
  "Redis": {
    "ConnectionString": "localhost:6379"
  },
  "Jwt": {
    "Secret": "YourSuperSecretKey..."
  }
}
```

### Shared Config (`appsettings.Shared.Production.json`) - Docker
```json
{
  "Redis": {
    "ConnectionString": "redis:6379"
  }
}
```

### Service-Specific Config (`IdentityService/appsettings.Development.json`)
```json
{
  "ConnectionStrings": {
    "IdentityDb": "server=localhost;port=3306;database=traditional_eats_identity;user=root;password=UthmanBasima70"
  }
}
```

### Docker Environment Variable Override
```yaml
environment:
  ConnectionStrings__IdentityDb: "server=mysql;port=3306;database=traditional_eats_identity;user=root;password=${MYSQL_ROOT_PASSWORD};"
```

## Updating Shared Settings

To update a shared setting (e.g., Redis connection string):

1. **For Local Development**: Edit `appsettings.Shared.json`
2. **For Docker/Production**: Edit `appsettings.Shared.Production.json` or set environment variables
3. All services will automatically use the new value
4. No need to update individual service configs

## Service-Specific Overrides

If a service needs to override a shared setting:

1. Add the setting to the service's `appsettings.Development.json` or `appsettings.Production.json`
2. The service-specific value will take precedence

## Running Services

### Local Development
```bash
# Start infrastructure (optional - if not using local MySQL)
cd deploy
docker-compose up -d mysql redis rabbitmq

# Run services locally (they connect to localhost)
cd src/services/TraditionalEats.IdentityService
dotnet run
```

### Docker/Production
```bash
# Set environment variables
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__IdentityDb="server=mysql;port=3306;database=traditional_eats_identity;user=root;password=rootpassword;"

# Or use docker-compose with environment variables
docker-compose up
```

## Adding New Services

When adding a new service:

1. Create minimal `appsettings.Development.json` with only service-specific settings (database connection)
2. Add `builder.Configuration.AddSharedConfiguration(builder.Environment);` to `Program.cs`
3. The service automatically inherits all shared configuration
4. For Docker, set the service-specific connection string via environment variable