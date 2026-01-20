# Troubleshooting: Restaurants Not Showing

## Quick Fixes Applied ✅

1. ✅ Added CORS support to Web.Bff
2. ✅ Added detailed error logging to WebApp
3. ✅ Added JSON deserialization with case-insensitive matching
4. ✅ Added better error messages

## Step-by-Step Debugging

### Step 1: Check Browser Console
1. Open the WebApp in browser: `http://localhost:5300/restaurants`
2. Open DevTools (F12) → Console tab
3. Look for these messages:
   - `Calling API: WebBff/restaurants`
   - `Response status: 200` (or error code)
   - `Response content length: X`
   - `Deserialized X restaurants`

**If you see errors**, note them down.

### Step 2: Verify All Services Are Running

**Terminal 1 - RestaurantService:**
```bash
cd src/services/TraditionalEats.RestaurantService
dotnet run
```
Should show: `Now listening on: http://localhost:5007`

**Terminal 2 - Web.Bff:**
```bash
cd src/bff/TraditionalEats.Web.Bff
dotnet run
```
Should show: `Now listening on: http://localhost:5101`

**Terminal 3 - WebApp:**
```bash
cd src/apps/TraditionalEats.WebApp
dotnet run
```
Should show: `Now listening on: http://localhost:5300`

### Step 3: Test API Endpoints

**Test RestaurantService directly:**
```bash
curl http://localhost:5007/api/restaurant
```
Expected: JSON array with 10 restaurants

**Test Web.Bff:**
```bash
curl http://localhost:5101/api/WebBff/restaurants
```
Expected: Same JSON array

### Step 4: Check Database

```bash
mysql -u root -pUthmanBasima70 -e "USE traditional_eats_restaurant; SELECT COUNT(*) as Count FROM Restaurants;"
```

Expected: `Count: 10`

If count is 0:
- Restart RestaurantService (it seeds on startup if DB is empty)
- Check RestaurantService logs for seed messages

### Step 5: Common Issues & Fixes

#### Issue: CORS Error in Browser Console
**Error**: `Access to fetch at 'http://localhost:5101/api/WebBff/restaurants' from origin 'http://localhost:5300' has been blocked by CORS policy`

**Fix**: 
- CORS is now enabled in Web.Bff
- **Restart Web.Bff** for changes to take effect

#### Issue: 404 Not Found
**Error**: `Response status: 404`

**Fix**:
- Check RestaurantService is running: `curl http://localhost:5007/api/restaurant`
- Check Web.Bff service URL in `appsettings.Development.json`:
  ```json
  {
    "Services": {
      "RestaurantService": "http://localhost:5007"
    }
  }
  ```

#### Issue: Empty Array `[]`
**Response**: `[]` instead of restaurants

**Fix**:
- Check database has data (Step 4)
- If empty, restart RestaurantService
- Check RestaurantService logs for seed errors

#### Issue: JSON Deserialization Error
**Error**: `JSON deserialization error: ...`

**Fix**:
- Check browser console for the actual JSON response
- Verify RestaurantDto properties match backend DTO
- The code now uses case-insensitive matching, so this should work

#### Issue: Network Error / Connection Refused
**Error**: `HTTP error: Connection refused` or `Failed to fetch`

**Fix**:
- Verify Web.Bff is running on port 5101
- Check `appsettings.json` in WebApp has correct URL:
  ```json
  {
    "ApiBaseUrl": "http://localhost:5101/api/"
  }
  ```

### Step 6: Verify Configuration Files

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
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5101"
      }
    }
  }
}
```

**RestaurantService `appsettings.Development.json`:**
```json
{
  "ConnectionStrings": {
    "RestaurantDb": "server=localhost;port=3306;database=traditional_eats_restaurant;user=root;password=YOUR_PASSWORD"
  }
}
```

## What to Check Next

1. **Browser Console** - Look for error messages
2. **Network Tab** - Check the actual HTTP request/response
3. **Service Logs** - Check each service's console output
4. **Database** - Verify data exists

## Expected Console Output

When working correctly, you should see in browser console:
```
Calling API: WebBff/restaurants
Response status: 200
Response content length: 2500
Response preview: [{"restaurantId":"...","name":"Traditional Kitchen",...
Deserialized 10 restaurants
```

If you see different output, share it and we can debug further!
