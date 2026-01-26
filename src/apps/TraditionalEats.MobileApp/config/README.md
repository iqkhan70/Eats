# Configuration Files

## Setup

1. **Copy the example config:**
   ```bash
   cp config/app.config.example.ts config/app.config.ts
   ```

2. **Update `app.config.ts` with your settings:**
   - For phone testing: Set `DEV_IP` to your computer's IP address
   - For production: Update the production API URL

## Files

- `app.config.example.ts` - Example configuration (committed to git)
- `app.config.ts` - Your actual configuration (gitignored, customize as needed)

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
