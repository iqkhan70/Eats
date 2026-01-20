# What is BFF (Backend-for-Frontend)?

## Overview

**BFF** stands for **Backend-for-Frontend**. It's an architectural pattern where you create a dedicated backend service for each frontend application (web and mobile).

## Why Use BFF?

### 1. **Client-Specific Optimization**
- **Web BFF** (`TraditionalEats.Web.Bff`): Optimized for Blazor WebAssembly
- **Mobile BFF** (`TraditionalEats.Mobile.Bff`): Optimized for React Native mobile app

### 2. **Data Aggregation**
Instead of making multiple API calls from the frontend:
```
Frontend → BFF → [Service1, Service2, Service3] → Aggregated Response
```

### 3. **Reduced Network Calls**
- Frontend makes **1 call** to BFF
- BFF makes **multiple calls** to microservices
- BFF combines and returns optimized data

### 4. **Client-Specific Transformations**
- Transform data format for each client
- Handle client-specific concerns (pagination, filtering, formatting)
- Reduce payload size for mobile

### 5. **Security & Authentication**
- Centralize authentication logic
- Handle token refresh
- Validate requests before forwarding to services

## Architecture Flow

```
┌─────────────────┐
│  Blazor WebApp  │
│   (Port 5300)    │
└────────┬────────┘
         │
         │ HTTP Calls
         │
┌────────▼────────┐
│   Web BFF       │
│  (Port 5101)    │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
┌───▼───┐ ┌──▼────┐
│Order  │ │Restaurant│
│Service│ │ Service  │
└───────┘ └─────────┘

┌─────────────────┐
│ React Native    │
│  Mobile App     │
└────────┬────────┘
         │
         │ HTTP Calls
         │
┌────────▼────────┐
│  Mobile BFF     │
│  (Port 5102)    │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
┌───▼───┐ ┌──▼────┐
│Order  │ │Restaurant│
│Service│ │ Service  │
└───────┘ └─────────┘
```

## Ports

- **Web BFF**: `http://localhost:5101`
- **Mobile BFF**: `http://localhost:5102`

## How to Run

### Start Web BFF (for Blazor WebApp)

```bash
cd src/bff/TraditionalEats.Web.Bff
dotnet run
```

This will start on port **5101**.

### Start Mobile BFF (for React Native App)

```bash
cd src/bff/TraditionalEats.Mobile.Bff
dotnet run
```

This will start on port **5102**.

## Example: Getting Restaurants

### Without BFF (Bad):
```
Frontend → RestaurantService (1 call)
Frontend → CatalogService (1 call)
Frontend → ReviewService (1 call)
Total: 3 network calls
```

### With BFF (Good):
```
Frontend → WebBff/restaurants (1 call)
  └─→ BFF calls RestaurantService
  └─→ BFF calls CatalogService  
  └─→ BFF calls ReviewService
  └─→ BFF combines and returns
Total: 1 network call from frontend
```

## Benefits

1. **Better Performance**: Fewer network round trips
2. **Simpler Frontend**: Frontend doesn't need to know about all microservices
3. **Flexibility**: Can change backend without affecting frontend
4. **Optimization**: Can cache, batch, and optimize responses per client
5. **Security**: Centralized authentication and authorization

## Current Implementation

Both BFFs currently:
- Proxy requests to downstream microservices
- Aggregate responses
- Handle errors gracefully
- Provide health check endpoints

Future enhancements:
- Caching
- Request batching
- Response transformation
- Authentication/authorization
- Rate limiting
