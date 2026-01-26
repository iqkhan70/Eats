# Mobile App API Configuration

## Issue: Axios Error When Testing on Phone

When running the mobile app on a physical device, `localhost` won't work because the phone can't access your computer's localhost.

## Solution: Configure API URL via Config File

The API URL is now configured via `config/app.config.ts` instead of being hardcoded.

### Step 1: Set Up Configuration File

1. **Copy the example config:**
   ```bash
   cp config/app.config.example.ts config/app.config.ts
   ```

2. **Or create `config/app.config.ts` manually** (it's gitignored, so you can customize it)

### Step 2: Find Your Computer's IP Address

**On macOS:**
```bash
ipconfig getifaddr en0
```

You'll get something like: `192.168.1.100`

**On Windows:**
```bash
ipconfig
```
Look for "IPv4 Address" under your active network adapter.

**On Linux:**
```bash
hostname -I
```

### Step 3: Update API Base URL in Config

Edit `config/app.config.ts` and update the `DEV_IP` constant:

```typescript
// Replace '192.168.1.100' with your computer's IP
const DEV_IP = '192.168.1.100';
```

The config file will automatically use this IP to build the API URL:
- Development: `http://${DEV_IP}:5102/api`
- Production: `https://api.traditionaleats.com/api`

### Step 4: Make Sure Mobile BFF is Running

The Mobile BFF must be running and accessible:

```bash
cd src/bff/TraditionalEats.Mobile.Bff
dotnet run
```

It should show:
```
Now listening on: http://0.0.0.0:5102
```

**Important:** The BFF must listen on `0.0.0.0` (not `localhost`) to accept connections from other devices.

### Step 5: Check Firewall

Make sure your firewall allows connections on port 5102:

**macOS:**
- System Settings → Network → Firewall → Options
- Allow incoming connections for .NET apps
- Or temporarily disable firewall to test

**Windows:**
- Windows Defender Firewall → Allow an app
- Add .NET or allow port 5102

### Step 6: Verify Network

- Phone and computer must be on the **same WiFi network**
- Some corporate/public networks block device-to-device communication

## Alternative: Use Environment Variable

You can also set the IP address via environment variable:

1. Create `.env` file in the mobile app root:
```
EXPO_PUBLIC_DEV_IP=192.168.1.100
```

2. The config file will automatically use this (see `config/app.config.ts`)

**Note:** The `.env` file should be gitignored (already configured)

## Testing

After updating the IP address:

1. Restart the Expo development server
2. Reload the app on your phone
3. Check the console logs for detailed error messages

## Common Errors

### `ECONNREFUSED` or `ENOTFOUND`
- **Cause:** Can't reach the server
- **Fix:** Use IP address instead of localhost, ensure BFF is running

### `Network Error`
- **Cause:** No response from server
- **Fix:** Check firewall, ensure same WiFi network

### `401 Unauthorized`
- **Cause:** Authentication required (this is normal if not logged in)
- **Fix:** Some endpoints may work without auth, check BFF logs

## Debugging

The API client now logs detailed error information:
- Connection errors show the base URL and hint
- Network errors show request details
- Response errors show status and data

Check the Expo console or React Native debugger for these logs.
