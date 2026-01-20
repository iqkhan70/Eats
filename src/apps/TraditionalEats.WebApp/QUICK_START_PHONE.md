# Quick Start: Test on Phone

## Step-by-Step Instructions

### 1. Find Your Computer's IP Address

Run this command:
```bash
ipconfig getifaddr en0
```

You'll get something like: `192.168.1.100`

### 2. Update API Configuration

Edit `appsettings.Development.json` and replace `localhost` with your IP:

```json
{
  "ApiBaseUrl": "http://192.168.1.100:5101/api/",
  ...
}
```

Replace `192.168.1.100` with your actual IP from step 1.

### 3. Start Web BFF (Required for API)

In Terminal 1:
```bash
cd src/bff/TraditionalEats.Web.Bff
dotnet run
```

Wait until you see: `Now listening on: http://localhost:5101`

### 4. Start WebApp

In Terminal 2:
```bash
cd src/apps/TraditionalEats.WebApp
dotnet run
```

Or use the helper script:
```bash
cd src/apps/TraditionalEats.WebApp
./run-for-phone.sh
```

### 5. Access from Phone

1. Make sure your phone is on the **same WiFi network**
2. Open browser on phone
3. Go to: `http://YOUR_IP:5300`
   - Example: `http://192.168.1.100:5300`

## Important Notes

- The app is configured to listen on all interfaces (`0.0.0.0:5300`)
- API calls will use the IP you set in `appsettings.Development.json`
- If API calls fail, double-check the IP address matches your computer's IP

## Troubleshooting

**Can't connect?**
- Check firewall settings (allow port 5300)
- Verify phone and computer are on same WiFi
- Try accessing from computer first: `http://localhost:5300`

**API calls fail?**
- Make sure Web BFF is running
- Verify the IP in `appsettings.Development.json` is correct
- Check browser console on phone for errors
