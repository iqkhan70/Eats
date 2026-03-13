# Navigation Guide – start-all.sh

When you run `./start-all.sh`, services start in a **tmux** session. Use these URLs and shortcuts to reach each one.

---

## Tmux shortcuts

| Action | Keys |
|--------|------|
| Switch to next window | `Ctrl+b` then `n` |
| Switch to previous window | `Ctrl+b` then `p` |
| Switch by window number | `Ctrl+b` then `0`–`17` |
| List all windows | `Ctrl+b` then `w` |
| Detach (leave session running) | `Ctrl+b` then `d` |
| Reattach | `tmux attach -t eats` |

---

## User-facing apps (browser / mobile)

| App | URL | Use |
|-----|-----|-----|
| **Web App** | http://localhost:5300 | Web UI (customer, vendor, admin) |
| **Mobile BFF** | http://localhost:5102 | API for mobile app (no UI) |

**Mobile app:** Run `npx expo start` in `src/apps/TraditionalEats.MobileApp`. It talks to Mobile BFF at `http://localhost:5102` (or your IP when testing on a device).

---

## BFFs (API gateways)

| BFF | URL | Swagger |
|-----|-----|---------|
| **Web BFF** | http://localhost:5101 | http://localhost:5101/swagger |
| **Mobile BFF** | http://localhost:5102 | http://localhost:5102/swagger |

---

## Backend services (direct access)

| Service | Port | Swagger / health |
|---------|------|-------------------|
| IdentityService | 5000 | http://localhost:5000/swagger |
| CustomerService | 5001 | http://localhost:5001/swagger |
| OrderService | 5002 | http://localhost:5002/swagger |
| CatalogService | 5003 | http://localhost:5003/swagger |
| PaymentService | 5004 | http://localhost:5004/swagger |
| DeliveryService | 5005 | http://localhost:5005/swagger |
| NotificationService | 5006 | http://localhost:5006/swagger |
| RestaurantService | 5007 | http://localhost:5007/swagger |
| PromotionService | 5008 | http://localhost:5008/swagger |
| ReviewService | 5009 | http://localhost:5009/swagger |
| SupportService | 5010 | http://localhost:5010/swagger |
| AIService | 5011 | http://localhost:5011/swagger |
| ChatService | 5012 | http://localhost:5012 |
| DocumentService | 5014 | http://localhost:5014/swagger |

---

## Tmux window order (start-all.sh)

| # | Window name |
|---|-------------|
| 0 | AIService |
| 1 | CatalogService |
| 2 | ChatService |
| 3 | CustomerService |
| 4 | DeliveryService |
| 5 | DocumentService |
| 6 | IdentityService |
| 7 | NotificationService |
| 8 | OrderService |
| 9 | PaymentService |
| 10 | PromotionService |
| 11 | RestaurantService |
| 12 | ReviewService |
| 13 | SupportService |
| 14 | WebBff |
| 15 | MobileBff |
| 16 | WebApp |

---

## Quick start

1. Run `./start-all.sh`
2. Open **Web App**: http://localhost:5300
3. Or run **Mobile app**: `cd src/apps/TraditionalEats.MobileApp && npx expo start`
4. Use `Ctrl+b` then `w` to see and switch between service windows
