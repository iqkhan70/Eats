# Running Blazor WebApp on Your Phone

## Quick Start

### Step 1: Find Your Computer's IP Address

**On macOS:**
```bash
# For WiFi connection:
ipconfig getifaddr en0

# For Ethernet connection:
ipconfig getifaddr en1

# Or check all interfaces:
ifconfig | grep "inet " | grep -v 127.0.0.1
```

**On Windows:**
```bash
ipconfig
# Look for "IPv4 Address" under your active network adapter
```

**On Linux:**
```bash
hostname -I
# Or:
ip addr show
```

You'll get something like: `192.168.1.100` or `10.0.0.50`

### Step 2: Update API URL (if needed)

If your Web BFF is also running on the same machine, you may need to update the API URL to use your IP instead of localhost.

Edit `appsettings.Development.json`:
```json
{
  "ApiBaseUrl": "http://YOUR_IP_ADDRESS:5101/api/",
  ...
}
```

Replace `YOUR_IP_ADDRESS` with the IP you found in Step 1.

### Step 3: Start the WebApp

```bash
cd src/apps/TraditionalEats.WebApp
dotnet run
```

The app will start and show:
```
Now listening on: http://0.0.0.0:5300
```

### Step 4: Access from Your Phone

1. **Make sure your phone is on the same WiFi network** as your computer
2. Open your phone's browser (Chrome, Safari, etc.)
3. Navigate to: `http://YOUR_IP_ADDRESS:5300`
   - Example: `http://192.168.1.100:5300`

### Step 5: Start the Web BFF (Required for API calls)

In a separate terminal:
```bash
cd src/bff/TraditionalEats.Web.Bff
dotnet run
```

This should run on port 5101.

## Troubleshooting

### Can't access from phone?

1. **Check firewall:**
   - macOS: System Settings → Network → Firewall → Options → Allow incoming connections for .NET
   - Or temporarily disable firewall to test

2. **Verify IP address:**
   - Make sure you're using the correct local IP (not 127.0.0.1)
   - The IP should be in the format: `192.168.x.x` or `10.x.x.x`

3. **Check network:**
   - Phone and computer must be on the **same WiFi network**
   - Some corporate/public networks block device-to-device communication

4. **Try HTTPS:**
   - If HTTP doesn't work, try: `https://YOUR_IP:5301`
   - You'll need to accept the self-signed certificate warning

### API calls failing?

If the app loads but shows errors when making API calls:

1. Make sure Web BFF is running on port 5101
2. Update `appsettings.Development.json` to use your IP:
   ```json
   "ApiBaseUrl": "http://192.168.1.100:5101/api/"
   ```
3. Restart the WebApp after changing the config

### Alternative: Use ngrok for External Access

If you want to test from outside your local network or if local network access doesn't work:

```bash
# Install ngrok: https://ngrok.com/download
# Then run:
ngrok http 5300
```

This gives you a public URL like `https://abc123.ngrok.io` that works from anywhere.

## Quick Test Script

Save this as `run-for-phone.sh`:

```bash
#!/bin/bash
IP=$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo "localhost")
echo "Your local IP: $IP"
echo "Access the app at: http://$IP:5300"
echo ""
echo "Starting WebApp..."
cd src/apps/TraditionalEats.WebApp
dotnet run
```

Make it executable and run:
```bash
chmod +x run-for-phone.sh
./run-for-phone.sh
```
