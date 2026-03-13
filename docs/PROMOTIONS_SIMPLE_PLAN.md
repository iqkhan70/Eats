# Simple Promotions Plan (Minimal Approach)

Based on your current design, this is the **simplest** way to add deals without complicating things.

**Status: Implemented** (2025-03-12)

**Discount at checkout:** OrderService applies the restaurant's active deal discount when placing an order. Cart shows "X% off applied at checkout" when the vendor has a deal.

---

## Current State

- **RestaurantService** – has Restaurant entity, returns restaurants for home & list
- **PromotionService** – exists but is **code-based** (SAVE20, etc.) for checkout discounts, not display deals
- **Home screen** – shows categories + "Nearby Vendors" (restaurant cards)
- **Restaurants list** – shows restaurant cards with name, cuisine, address, rating

---

## Recommended: Restaurant-Level Deal (No New Table)

Add **3 optional fields** to Restaurant. When set, the restaurant "has a deal."

| Field | Type | Example |
|-------|------|---------|
| `ActiveDealTitle` | string? | "50% off today" |
| `ActiveDealDiscountPercent` | int? | 50 |
| `ActiveDealEndTime` | DateTime? | 2025-03-10 21:00 |

**Why this is simplest:**
- No new service
- No new table
- Reuses existing restaurant fetch
- One deal per restaurant (enough for early stage)

---

## Implementation Steps

### 1. Backend (RestaurantService)

- Add migration for 3 columns on `Restaurants` table
- Add fields to `Restaurant` entity and DTOs
- Add vendor endpoint: `PATCH /api/restaurant/{id}/deal` – body: `{ title, discountPercent, endTime }` or `null` to clear
- Include deal fields in `GetRestaurants` and `GetRestaurant` responses

### 2. Mobile BFF

- Pass through – restaurant DTOs already flow from RestaurantService
- Add proxy for `PATCH vendor/restaurants/{id}/deal` if needed

### 3. Mobile App – Feature 1: Home Deals Banner

- **Location:** Above "Nearby Vendors" on home screen
- **Content:** "Today's Deals" section
- **Data:** Filter `nearbyRestaurants` where `activeDealTitle != null` and `activeDealEndTime > now`
- **Display:** Simple list: "50% off – Tokyo Sushi" etc. Tap → `router.push(/restaurants/{id}/catalog)`
- **Empty state:** Hide section when no deals

### 4. Mobile App – Feature 2: Restaurant Deal Badge

- **Location:** On restaurant cards (home + Vendors list)
- **Display:** When `activeDealTitle` is set, show badge: `🔥 50% off` (or use `activeDealTitle`)
- **Style:** Small pill/badge next to name or below rating

### 5. Vendor Dashboard

- **Location:** Restaurant edit or a simple "Deals" card
- **Form:** Title, Discount %, End time. "Save" / "Clear deal"
- **API:** PATCH to set or clear the 3 fields

---

## What We're NOT Doing (Yet)

- Menu-item-specific deals (would need new table)
- Multiple deals per restaurant
- Push notifications for deals
- "Deals near you" sorting

---

## API Shape (Minimal)

**Restaurant response** (existing, extended):

```json
{
  "restaurantId": "...",
  "name": "Tokyo Sushi",
  "activeDealTitle": "50% off today",
  "activeDealDiscountPercent": 50,
  "activeDealEndTime": "2025-03-10T21:00:00Z"
}
```

**Vendor set deal:** `PATCH /api/restaurant/{id}` with `{ activeDealTitle, activeDealDiscountPercent, activeDealEndTime }` – or a dedicated `/deal` endpoint if you prefer.

---

## Effort Estimate

| Component | Effort |
|-----------|--------|
| Restaurant migration + entity | ~30 min |
| RestaurantService deal logic | ~30 min |
| Mobile BFF pass-through | ~15 min |
| Home deals banner | ~45 min |
| Restaurant badge | ~20 min |
| Vendor deal form | ~1 hr |

**Total:** ~3–4 hours for both features.

---

## Alternative: Use Existing PromotionService

The existing Promotion has `RestaurantId`, `Name`, `Type`, `Value`, `StartDate`, `EndDate`. You could:

- Add `MenuItemId` (nullable) and `IsDisplayDeal` (bool)
- Add endpoint `GET /api/promotion/active-deals?latitude=&longitude=&radiusMiles=` that returns display deals
- This supports item-specific deals but requires more backend work and joining with Restaurant/Catalog data.

**Recommendation:** Start with restaurant-level fields. Add PromotionService display deals later if you need item-specific offers.
