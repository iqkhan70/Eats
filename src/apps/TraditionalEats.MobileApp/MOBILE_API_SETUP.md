# Mobile App API Configuration

## Issue: Axios Error When Testing on Phone

When running the mobile app on a physical device, `localhost` won't work because the phone can't access your computer's localhost.

## Solution: Configure API URL via Config File

The API URL is now configured via `config/api.config.ts` instead of being hardcoded.

### Step 1: Set Up Configuration File

1. **Copy the example config:**
   ```bash
   cp config/api.config.example.ts config/api.config.ts
   ```

2. **Or create `config/api.config.ts` manually** (optional override; defaults are in the committed file)

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

Edit `config/api.config.ts` and update the `DEV_IP` constant:

```typescript
// Replace '192.168.1.100' with your computer's IP
const DEV_IP = '192.168.1.100';
```

The config file will automatically use this IP to build the API URL:
- Development: `http://${DEV_IP}:5102/api`
- Production (TestFlight/App Store): `https://www.kram.tech/api`

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

## TestFlight with production backend (www.kram.tech)

To ship a build that uses the live API at **https://www.kram.tech** (no ngrok, no local backend):

1. Build with production env so the app uses `https://www.kram.tech/api` and `https://www.kram.tech/chatHub`:
   ```bash
   EXPO_PUBLIC_ENV=production npx expo prebuild
   # Then build for iOS (EAS or Xcode) and upload to TestFlight
   ```
   Or in EAS Build: set environment variable **EXPO_PUBLIC_ENV** = **production** for the production profile.

2. No need to run a local BFF or ngrok; TestFlight testers will hit www.kram.tech directly.

3. Optional: override the production host with **EXPO_PUBLIC_PRODUCTION_URL** (e.g. `www.kram.tech`) if you use a different domain.

## TestFlight / External testers: ngrok (local backend)

For **TestFlight** while still using your **local** backend (e.g. for quick iteration), the phone cannot reach your machine by IP. Use **ngrok** to expose your local backend over a public HTTPS URL.

### Services to expose

| Service        | Port | Purpose              |
|----------------|------|----------------------|
| **Mobile BFF** | 5102 | All app API calls    |
| **ChatService**| 5012 | Order chat (SignalR) |

### Steps

1. **Install ngrok** (e.g. `brew install ngrok` or from [ngrok.com](https://ngrok.com)).

2. **Start your local stack** (e.g. `./start-all.sh` or at least Mobile BFF on 5102 and ChatService on 5012).

3. **Run two ngrok tunnels** (two terminals):
   ```bash
   # Terminal 1 – Mobile BFF (required for all API)
   ngrok http 5102
   ```
   Copy the **https** URL (e.g. `https://abc123.ngrok-free.app`).

   ```bash
   # Terminal 2 – Chat (required for order chat)
   ngrok http 5012
   ```
   Copy the **https** URL (e.g. `https://def456.ngrok-free.app`).

4. **Set env and build for TestFlight** (use the URLs from step 3, no trailing slash):
   ```bash
   export EXPO_PUBLIC_ENV=ngrok
   export EXPO_PUBLIC_NGROK_API_URL=https://abc123.ngrok-free.app
   export EXPO_PUBLIC_NGROK_CHAT_URL=https://def456.ngrok-free.app
   npx expo prebuild
   # then build for iOS and upload to TestFlight
   ```
   Or put the same in `.env` before building.

5. **Keep ngrok and your backend running** while testers use the app. If you stop ngrok or restart it, the URL changes and you must rebuild the app with the new URLs (or use ngrok’s reserved domains if you have a paid plan).

**Note:** With only one tunnel (BFF), the app works except **order chat**; set `EXPO_PUBLIC_NGROK_CHAT_URL` for chat.

## Alternative: Use Environment Variable

You can also set the IP address via environment variable:

1. Create `.env` file in the mobile app root:
```
EXPO_PUBLIC_DEV_IP=192.168.1.100
```

2. The config file will automatically use this (see `config/api.config.ts`)

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
