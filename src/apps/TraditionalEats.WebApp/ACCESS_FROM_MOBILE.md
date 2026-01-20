# Accessing Blazor WebApp from Mobile Browser

## Yes, it works!

The Blazor WebAssembly app works perfectly in mobile browsers (Safari, Chrome, Firefox, etc.). It's just a web app that runs in any browser.

## Quick Steps

### 1. Find Your Computer's IP Address

**On macOS:**
```bash
ipconfig getifaddr en0
```

You'll get something like: `192.168.1.100`

### 2. Make Sure WebApp is Running

```bash
cd src/apps/TraditionalEats.WebApp
dotnet run
```

You should see:
```
Now listening on: http://0.0.0.0:5300
```

### 3. Make Sure Web BFF is Running (Required for API calls)

In a separate terminal:
```bash
cd src/bff/TraditionalEats.Web.Bff
dotnet run
```

This should run on port 5101.

### 4. Update API URL for Mobile Access

Edit `appsettings.Development.json` and replace `localhost` with your IP:

```json
{
  "ApiBaseUrl": "http://192.168.1.100:5101/api/",
  ...
}
```

Replace `192.168.1.100` with your actual IP from step 1.

**Important:** After changing this, restart the WebApp!

### 5. Access from Your Phone

1. **Make sure your phone is on the same WiFi network** as your computer
2. Open your phone's browser (Safari, Chrome, etc.)
3. Go to: `http://YOUR_IP:5300`
   - Example: `http://192.168.1.100:5300`

## What You'll See

- The Blazor WebApp will load in your mobile browser
- It will be responsive and mobile-friendly (we already fixed the mobile layout!)
- You can navigate, search, and use all features
- API calls will go through the Web BFF

## Troubleshooting

### Can't connect?

1. **Check firewall:**
   - macOS: System Settings → Network → Firewall → Allow .NET apps
   - Or temporarily disable firewall to test

2. **Verify IP address:**
   - Make sure you're using the correct local IP (not 127.0.0.1)
   - The IP should be like: `192.168.x.x` or `10.x.x.x`

3. **Check network:**
   - Phone and computer must be on the **same WiFi network**
   - Some corporate/public networks block device-to-device communication

4. **API calls failing?**
   - Make sure Web BFF is running on port 5101
   - Make sure you updated `appsettings.Development.json` with your IP
   - Restart the WebApp after changing the config

### Page loads but shows errors?

- Check browser console (if available on mobile)
- Make sure Web BFF is running
- Verify the API URL in `appsettings.Development.json` is correct

## Quick Test Script

Save this as `test-mobile.sh`:

```bash
#!/bin/bash
IP=$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || echo "localhost")
echo "Your local IP: $IP"
echo ""
echo "📱 Access from phone:"
echo "   http://$IP:5300"
echo ""
echo "⚠️  Make sure:"
echo "   1. WebApp is running (port 5300)"
echo "   2. Web BFF is running (port 5101)"
echo "   3. API URL in appsettings.Development.json uses your IP"
```

## Benefits of Using Mobile Browser

✅ **No app installation needed** - Just open browser  
✅ **Works on any phone** - iOS, Android, etc.  
✅ **Easy to test** - No need to build mobile app  
✅ **Responsive design** - Already optimized for mobile  
✅ **Same features** - Full functionality in browser  

## Next Steps

Once you confirm it works in the mobile browser, you can:
1. Test the mobile app (React Native) separately
2. Compare the experience
3. Decide which approach works best for your use case
