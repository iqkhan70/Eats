# How to Run BFF Services

## What is BFF?

**BFF = Backend-for-Frontend**

It's a dedicated API layer that sits between your frontend apps and microservices. Instead of your frontend calling multiple microservices directly, it calls the BFF, which then aggregates data from multiple services.

See `BFF_EXPLANATION.md` for detailed explanation.

## Quick Start

### For Blazor WebApp (Web BFF)

**Port: 5101**

```bash
cd src/bff/TraditionalEats.Web.Bff
dotnet run
```

You should see:
```
Now listening on: http://localhost:5101
```

**Test it:**
```bash
curl http://localhost:5101/api/webbff/health
```

### For React Native Mobile App (Mobile BFF)

**Port: 5102**

```bash
cd src/bff/TraditionalEats.Mobile.Bff
dotnet run
```

You should see:
```
Now listening on: http://localhost:5102
```

**Test it:**
```bash
curl http://localhost:5102/api/mobilebff/health
```

## Running Both BFFs

You'll need **2 terminal windows**:

**Terminal 1 - Web BFF:**
```bash
cd src/bff/TraditionalEats.Web.Bff
dotnet run
```

**Terminal 2 - Mobile BFF:**
```bash
cd src/bff/TraditionalEats.Mobile.Bff
dotnet run
```

## Prerequisites

Before running BFFs, make sure the microservices are running:

- ✅ IdentityService (port 5000)
- ✅ CustomerService (port 5001)
- ✅ OrderService (port 5002)
- ✅ CatalogService (port 5003)
- ✅ RestaurantService (port 5007) - **Required for restaurant endpoints**

## API Endpoints

### Web BFF (Port 5101)

- `GET /api/webbff/health` - Health check
- `GET /api/webbff/restaurants` - Get all restaurants
- `GET /api/webbff/restaurants/{id}` - Get restaurant by ID
- `GET /api/webbff/orders` - Get orders

### Mobile BFF (Port 5102)

- `GET /api/mobilebff/health` - Health check
- `GET /api/mobilebff/restaurants` - Get all restaurants
- `GET /api/mobilebff/restaurants/{id}` - Get restaurant by ID
- `GET /api/mobilebff/orders` - Get orders

## Configuration

BFFs are configured in:
- `src/bff/TraditionalEats.Web.Bff/appsettings.Development.json`
- `src/bff/TraditionalEats.Mobile.Bff/appsettings.Development.json`

They point to microservices running on:
- IdentityService: `http://localhost:5000`
- CustomerService: `http://localhost:5001`
- OrderService: `http://localhost:5002`
- RestaurantService: `http://localhost:5007`
- etc.

## Troubleshooting

### "Connection refused" errors

Make sure the microservices are running first:
```bash
# Check if services are running
curl http://localhost:5007/api/restaurant
```

### Port already in use

If port 5101 or 5102 is already in use:
1. Check what's using it: `lsof -i :5101`
2. Kill the process or change the port in `appsettings.Development.json`

### BFF returns 500 errors

Check the BFF logs - it might be unable to reach the microservices. Make sure all required services are running.
