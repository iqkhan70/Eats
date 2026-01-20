# TraditionEats - Traditional Food Delivery Platform

A robust, Uber Eats-style food delivery platform built with microservices architecture, designed for scale, reliability, and seamless UX across desktop and mobile.

## Architecture Overview

### Microservices (12 Services)
- **IdentityService** (Port 5000): Authentication, authorization, JWT tokens, MFA, roles, refresh tokens
- **CustomerService** (Port 5001): Customer profiles, addresses (PII-encrypted), preferences
- **OrderService** (Port 5002): Cart management, order orchestration, state machine, idempotency
- **CatalogService** (Port 5003): Menu items, categories, pricing, dietary tags
- **PaymentService** (Port 5004): Stripe integration, payment intents, refunds
- **DeliveryService** (Port 5005): Driver management, dispatch, real-time tracking
- **NotificationService** (Port 5006): Multi-channel notifications (push, SMS, email)
- **RestaurantService** (Port 5007): Restaurant management, hours, delivery zones
- **PromotionService** (Port 5008): Coupons, credits, loyalty programs
- **ReviewService** (Port 5009): Ratings, reviews, moderation
- **SupportService** (Port 5010): Tickets, disputes, refund coordination
- **AIService** (Port 5011): Ollama-powered agentic AI for recommendations, support, fraud detection

### Infrastructure Components
- **API Gateway** (Port 5200): YARP reverse proxy for routing and rate limiting
- **Web.Bff** (Port 5101): Backend-for-Frontend for Blazor WebAssembly app
- **Mobile.Bff** (Port 5102): Backend-for-Frontend for React Native mobile app
- **Redis**: Caching, distributed locks, rate limiting, idempotency keys
- **RabbitMQ**: Event-driven messaging with outbox pattern
- **MySQL**: Database-per-service with EF Core migrations
- **OpenTelemetry**: Observability (tracing, metrics, logs)

## Tech Stack

- **Backend**: .NET 8, ASP.NET Core Web API
- **Database**: MySQL 8.0 (Pomelo EF Core provider)
- **Cache/Messaging**: Redis, RabbitMQ
- **Web Client**: Blazor WebAssembly + Radzen Blazor components
- **Mobile**: React Native (Expo) with TypeScript
- **Payments**: Stripe
- **AI**: Ollama (local LLM)
- **API Gateway**: YARP (Yet Another Reverse Proxy)

## Project Structure

```
TraditionEats/
├── src/
│   ├── services/          # 12 microservices
│   ├── bff/              # Backend-for-Frontend services
│   │   ├── TraditionEats.Web.Bff/
│   │   └── TraditionEats.Mobile.Bff/
│   ├── gateway/          # API Gateway (YARP)
│   │   └── TraditionEats.ApiGateway/
│   ├── apps/             # Client applications
│   │   ├── TraditionEats.WebApp/      # Blazor WebAssembly
│   │   └── TraditionEats.MobileApp/    # React Native (Expo)
│   └── shared/            # Shared libraries
│       ├── TraditionEats.BuildingBlocks/
│       └── TraditionEats.Contracts/
├── deploy/               # Docker Compose configuration
└── README.md
```

## Getting Started

### Prerequisites
- .NET 8 SDK
- Docker & Docker Compose (for infrastructure services)
- MySQL 8.0 (or use Docker)
- Redis (or use Docker)
- RabbitMQ (or use Docker)
- Node.js 18+ (for mobile app)
- Expo CLI (for mobile app)

### Local Development Setup

#### 1. Start Infrastructure Services (Optional)

If you prefer using Docker for infrastructure:

```bash
cd deploy
docker-compose up -d
```

This starts:
- MySQL (port 3306)
- Redis (port 6379)
- RabbitMQ (port 5672, management UI on 15672)

#### 2. Database Migrations

Run migrations for each service:

```bash
# Identity Service
cd src/services/TraditionEats.IdentityService
dotnet ef database update

# Customer Service
cd ../TraditionEats.CustomerService
dotnet ef database update

# Order Service
cd ../TraditionEats.OrderService
dotnet ef database update

# Catalog Service
cd ../TraditionEats.CatalogService
dotnet ef database update

# Payment Service
cd ../TraditionEats.PaymentService
dotnet ef database update

# Delivery Service
cd ../TraditionEats.DeliveryService
dotnet ef database update

# Notification Service
cd ../TraditionEats.NotificationService
dotnet ef database update

# Restaurant Service
cd ../TraditionEats.RestaurantService
dotnet ef database update

# Promotion Service
cd ../TraditionEats.PromotionService
dotnet ef database update

# Review Service
cd ../TraditionEats.ReviewService
dotnet ef database update

# Support Service
cd ../TraditionEats.SupportService
dotnet ef database update
```

#### 3. Run Services

**Option A: Run all services individually**

Each service can be run from its directory:

```bash
# Terminal 1 - Identity Service
cd src/services/TraditionEats.IdentityService
dotnet run

# Terminal 2 - Customer Service
cd src/services/TraditionEats.CustomerService
dotnet run

# ... and so on for each service
```

**Option B: Use Docker Compose (Recommended for production-like setup)**

See `deploy/docker-compose.yml` for full orchestration.

#### 4. Run BFFs

```bash
# Terminal - Web BFF
cd src/bff/TraditionEats.Web.Bff
dotnet run

# Terminal - Mobile BFF
cd src/bff/TraditionEats.Mobile.Bff
dotnet run
```

#### 5. Run API Gateway

```bash
cd src/gateway/TraditionEats.ApiGateway
dotnet run
```

The API Gateway will be available at `http://localhost:5200`

#### 6. Run Web Application (Blazor)

```bash
cd src/apps/TraditionEats.WebApp
dotnet run
```

The web app will be available at `http://localhost:5000` (or the configured port)

#### 7. Run Mobile Application (React Native)

```bash
cd src/apps/TraditionEats.MobileApp
npm install
npm start
```

Then:
- Press `i` for iOS simulator
- Press `a` for Android emulator
- Scan QR code with Expo Go app on your phone

## Service Ports Reference

| Service | Port | Description |
|---------|------|-------------|
| IdentityService | 5000 | Authentication & authorization |
| CustomerService | 5001 | Customer management |
| OrderService | 5002 | Order processing |
| CatalogService | 5003 | Menu & catalog |
| PaymentService | 5004 | Payment processing |
| DeliveryService | 5005 | Delivery management |
| NotificationService | 5006 | Notifications |
| RestaurantService | 5007 | Restaurant management |
| PromotionService | 5008 | Promotions & discounts |
| ReviewService | 5009 | Reviews & ratings |
| SupportService | 5010 | Customer support |
| AIService | 5011 | AI services |
| Web.Bff | 5101 | Web BFF |
| Mobile.Bff | 5102 | Mobile BFF |
| ApiGateway | 5200 | API Gateway |

## API Gateway Routes

All services are accessible through the API Gateway at `http://localhost:5200`:

- `/api/identity/*` → IdentityService
- `/api/customer/*` → CustomerService
- `/api/order/*` → OrderService
- `/api/catalog/*` → CatalogService
- `/api/payment/*` → PaymentService
- `/api/delivery/*` → DeliveryService
- `/api/notification/*` → NotificationService
- `/api/restaurant/*` → RestaurantService
- `/api/promotion/*` → PromotionService
- `/api/review/*` → ReviewService
- `/api/support/*` → SupportService
- `/api/ai/*` → AIService
- `/api/web/*` → Web.Bff
- `/api/mobile/*` → Mobile.Bff

## Configuration

### Shared Configuration

Common configuration is stored in `src/shared/TraditionEats.BuildingBlocks/Configuration/appsettings.Shared.json` and loaded by all services. Service-specific overrides are in each service's `appsettings.Development.json`.

### Environment Variables

For production, use environment variables for sensitive data:
- Database connection strings
- JWT secrets
- Encryption keys
- API keys (Stripe, etc.)

See `CONFIGURATION.md` for detailed configuration documentation.

## Key Features

### Security
- JWT-based authentication with refresh tokens
- PII encryption (AES-256-GCM) for sensitive customer data
- Role-based access control (RBAC)
- Multi-factor authentication (MFA) support
- Stripe tokenization for payment data

### Scalability
- Microservices architecture with independent scaling
- Redis caching for performance
- Database-per-service pattern
- Event-driven architecture with RabbitMQ
- Horizontal scaling support

### Reliability
- Idempotency keys for critical operations
- Outbox pattern for reliable messaging
- Distributed locks via Redis
- Circuit breakers and retry policies
- Comprehensive error handling

### Observability
- OpenTelemetry integration
- Structured logging
- Distributed tracing
- Metrics collection
- Health checks

## Development Workflow

1. **Make changes** to service code
2. **Create/update migrations** if database schema changed:
   ```bash
   dotnet ef migrations add MigrationName
   dotnet ef database update
   ```
3. **Test locally** by running the service
4. **Check Swagger UI** at `http://localhost:{port}/swagger` for API documentation

## Testing

### Run All Services

To test the full stack:

1. Start infrastructure (MySQL, Redis, RabbitMQ)
2. Run all microservices
3. Run both BFFs
4. Run API Gateway
5. Run web/mobile clients

### API Testing

Use Swagger UI at each service's endpoint:
- IdentityService: `http://localhost:5000/swagger`
- CustomerService: `http://localhost:5001/swagger`
- ... and so on

## Deployment

See `deploy/README.md` for Docker Compose deployment instructions.

## Next Steps

- [ ] Implement authentication flows in web/mobile apps
- [ ] Connect frontend apps to BFF endpoints
- [ ] Add comprehensive unit/integration tests
- [ ] Set up CI/CD pipelines
- [ ] Configure production environment variables
- [ ] Set up monitoring and alerting
- [ ] Implement rate limiting in API Gateway
- [ ] Add API versioning
- [ ] Set up load balancing

## Contributing

1. Create a feature branch
2. Make your changes
3. Run migrations if needed
4. Test thoroughly
5. Submit a pull request

## License

[Your License Here]

## Support

For issues and questions, please open an issue in the repository.
