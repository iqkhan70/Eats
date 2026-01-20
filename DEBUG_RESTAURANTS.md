# Debugging Restaurant Display Issue

## Steps to Debug

### 1. Check Browser Console
Open browser DevTools (F12) and check the Console tab. You should see:
- `Calling API: WebBff/restaurants`
- `Response status: 200` (or error code)
- `Response content length: X`
- `Deserialized X restaurants`

### 2. Verify Services Are Running

**RestaurantService** (Port 5007):
```bash
cd src/services/TraditionalEats.RestaurantService
dotnet run
```
Check: `http://localhost:5007/swagger` - Should show API docs
Test: `http://localhost:5007/api/restaurant` - Should return JSON array

**Web.Bff** (Port 5101):
```bash
cd src/bff/TraditionalEats.Web.Bff
dotnet run
```
Check: `http://localhost:5101/` - Should show available endpoints
Test: `http://localhost:5101/api/WebBff/restaurants` - Should return JSON array

**WebApp** (Port 5300):
```bash
cd src/apps/TraditionalEats.WebApp
dotnet run
```
Open: `http://localhost:5300/restaurants`

### 3. Test API Endpoints Directly

**Test RestaurantService directly:**
```bash
curl http://localhost:5007/api/restaurant
```
Should return JSON array of restaurants.

**Test Web.Bff:**
```bash
curl http://localhost:5101/api/WebBff/restaurants
```
Should return the same JSON array.

### 4. Check Database

Verify restaurants exist in database:
```sql
mysql -u root -p
USE traditional_eats_restaurant;
SELECT COUNT(*) FROM Restaurants;
SELECT Name, Address, CuisineType FROM Restaurants LIMIT 5;
```

### 5. Common Issues

#### Issue: CORS Error
**Symptom**: Browser console shows CORS error
**Fix**: CORS is now enabled in Web.Bff. Make sure Web.Bff is restarted.

#### Issue: 404 Not Found
**Symptom**: API returns 404
**Fix**: 
- Check that RestaurantService is running on port 5007
- Check that Web.Bff has correct service URL in `appsettings.Development.json`

#### Issue: Empty Array
**Symptom**: API returns `[]`
**Fix**: 
- Check database has data: `SELECT COUNT(*) FROM Restaurants;`
- If empty, restart RestaurantService (it seeds on startup if DB is empty)

#### Issue: JSON Deserialization Error
**Symptom**: Console shows JSON parsing error
**Fix**: Check that RestaurantDto properties match backend DTO structure

### 6. Verify Configuration

**WebApp `appsettings.json`:**
```json
{
  "ApiBaseUrl": "http://localhost:5101/api/"
}
```

**Web.Bff `appsettings.Development.json`:**
```json
{
  "Services": {
    "RestaurantService": "http://localhost:5007"
  }
}
```

### 7. Check Logs

**RestaurantService logs:**
- Should show: "Created restaurant..." when seeding
- Should show database queries when API is called

**Web.Bff logs:**
- Should show: "Successfully fetched X restaurants"
- Or error messages if RestaurantService is unreachable

## Quick Test Script

```bash
# 1. Check RestaurantService
curl http://localhost:5007/api/restaurant | jq '.[0]'

# 2. Check Web.Bff
curl http://localhost:5101/api/WebBff/restaurants | jq '.[0]'

# 3. Check database
mysql -u root -p -e "USE traditional_eats_restaurant; SELECT COUNT(*) FROM Restaurants;"
```

## Expected Flow

1. User opens `/restaurants` page
2. WebApp calls `http://localhost:5101/api/WebBff/restaurants`
3. Web.Bff calls `http://localhost:5007/api/restaurant`
4. RestaurantService queries database
5. Response flows back: RestaurantService → Web.Bff → WebApp
6. UI displays restaurants

If any step fails, check the logs/console for that step.
