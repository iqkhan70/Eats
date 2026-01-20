# Deployment Guide

## Local Development Setup

### Option 1: Use Local MySQL (Recommended for Development)

1. **Use your local MySQL instance** - Services connect to `localhost:3306`
2. **Start only Redis and RabbitMQ via Docker** (optional):
   ```bash
   docker-compose up -d redis rabbitmq
   ```
3. **Run services locally** - They'll connect to:
   - MySQL: `localhost:3306` (your local instance)
   - Redis: `localhost:6379` (Docker or local)
   - RabbitMQ: `localhost:5672` (Docker or local)

### Option 2: Use Docker for Everything

1. **Start all infrastructure**:
   ```bash
   docker-compose up -d
   ```
2. **Run services locally** - They'll connect to Docker services:
   - MySQL: `localhost:3306` (Docker container)
   - Redis: `localhost:6379` (Docker container)
   - RabbitMQ: `localhost:5672` (Docker container)

## Docker/Production Setup

### Environment Variables

Create a `.env` file in the `deploy` directory:

```bash
# MySQL Configuration
MYSQL_ROOT_PASSWORD=your_secure_password
MYSQL_PORT=3306

# Redis Configuration
REDIS_PORT=6379

# RabbitMQ Configuration
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
RABBITMQ_PORT=5672
RABBITMQ_MANAGEMENT_PORT=15672
```

### Running Services in Docker

When running services in Docker containers, they need to use Docker service names:

- **MySQL**: `mysql:3306` (not `localhost`)
- **Redis**: `redis:6379` (not `localhost`)
- **RabbitMQ**: `rabbitmq:5672` (not `localhost`)

Set environment variables for each service:

```yaml
environment:
  ASPNETCORE_ENVIRONMENT: "Production"
  ConnectionStrings__IdentityDb: "server=mysql;port=3306;database=traditional_eats_identity;user=root;password=${MYSQL_ROOT_PASSWORD};"
  ConnectionStrings__CustomerDb: "server=mysql;port=3306;database=traditional_eats_customer;user=root;password=${MYSQL_ROOT_PASSWORD};"
  # ... etc for each service
  Redis__ConnectionString: "redis:6379"
  RabbitMQ__HostName: "rabbitmq"
```

## Connection String Patterns

### Local Development
```json
{
  "ConnectionStrings": {
    "IdentityDb": "server=localhost;port=3306;database=traditional_eats_identity;user=root;password=UthmanBasima70"
  }
}
```

### Docker/Production
```bash
ConnectionStrings__IdentityDb="server=mysql;port=3306;database=traditional_eats_identity;user=root;password=${MYSQL_ROOT_PASSWORD};"
```

## Port Configuration

By default, Docker Compose exposes:
- **MySQL**: `3306` (can be changed via `MYSQL_PORT`)
- **Redis**: `6379` (can be changed via `REDIS_PORT`)
- **RabbitMQ**: `5672` (can be changed via `RABBITMQ_PORT`)
- **RabbitMQ Management**: `15672` (can be changed via `RABBITMQ_MANAGEMENT_PORT`)

If you need to change ports (e.g., local MySQL already uses 3306), update the `.env` file or docker-compose.yml.

## Health Checks

All services include health checks:
- MySQL: Checks every 10s
- Redis: Checks every 10s
- RabbitMQ: Checks every 10s

Services should wait for dependencies to be healthy before starting.
