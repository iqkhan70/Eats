# TraditionalEats – Production Architecture (DigitalOcean)

## Pattern: HTTPS at the edge, HTTP inside private network

```
                    PUBLIC INTERNET (HTTPS only)
                                    │
                                    ▼
                    ┌───────────────────────────────┐
                    │   Edge: Nginx (or DO LB)      │
                    │   - TLS termination           │
                    │   - /        → WebApp (static) │
                    │   - /api     → Api Gateway    │
                    └───────────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    │   PRIVATE NETWORK (HTTP)       │
                    │   (Docker network / VPC)       │
                    │                                │
                    │  Api Gateway ──► Web BFF       │
                    │       │            │          │
                    │       └────────────┼───────────┼──► IdentityService
                    │                    │           ├──► CustomerService
                    │                    │           ├──► OrderService
                    │                    └───────────┼──► RestaurantService
                    │                                ├──► CatalogService
                    │  Mobile BFF ──────────────────┼──► NotificationService
                    │                                ├──► PaymentService
                    │                                └──► … (others)
                    │                                │
                    │  MySQL │ Redis │ RabbitMQ      │
                    └───────────────────────────────┘
```

## Why this is great

- **Simple cert management**: Only the edge (Nginx / load balancer) needs certificates.
- **Less overhead**: No mTLS or per-service certs; internal traffic is HTTP.
- **Still secure**: Internal services are not exposed to the internet; only edge and (optionally) WebApp are public.

## Must-haves

1. **Do not expose microservices to the internet.** Only Nginx (or DO App Platform ingress) and static WebApp are public.
2. **Restrict inbound** to the edge / BFF / gateway only; no direct access to Identity, Order, etc.
3. **Service-to-service**: Use JWT or API keys for BFF → services if required; treat internal network as trusted but still apply auth where needed.
4. **Authn/Authz**: Enforce at BFF/Gateway and in services that need it.

## When to use this in prod

- Services run in a **private network** (Docker network, VPC, private subnets).
- Internal traffic is not exposed (security groups / firewall / ingress rules).
- You are okay with **HTTP inside** the private network (no mTLS).

## Optional: HTTPS/mTLS everywhere

Use when compliance or zero-trust requires encryption in transit for every hop. Higher complexity (certs, rotation, debugging). Not needed for the pattern above if the private network is locked down.
