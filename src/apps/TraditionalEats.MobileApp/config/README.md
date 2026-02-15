# Configuration Files

## Setup

1. **Copy the example config:**
   ```bash
   cp config/api.config.example.ts config/api.config.ts
   ```

2. **Update `api.config.ts` with your settings (or use env vars):**
   - For phone testing: Set `DEV_IP` or `EXPO_PUBLIC_DEV_IP` to your computer's IP
   - For production: Uses `www.kram.tech`; override with `EXPO_PUBLIC_PRODUCTION_URL` if needed

## Files

- `api.config.ts` - API/base URL config (committed with defaults)
- `api.config.example.ts` - Example/template (copy to api.config.ts to override)

## Environment Variables

You can also use environment variables:

- `EXPO_PUBLIC_DEV_IP` - Overrides the `DEV_IP` in the config file

Create a `.env` file:
```
EXPO_PUBLIC_DEV_IP=192.168.1.100
```

## Finding Your IP Address

**macOS:**
```bash
ipconfig getifaddr en0
```

**Windows:**
```bash
ipconfig
# Look for "IPv4 Address"
```

**Linux:**
```bash
hostname -I
```
