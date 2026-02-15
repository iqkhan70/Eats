# Assets Directory

The app uses **`logo.png`** (Kram logo) as the app icon, splash screen, Android adaptive icon, and web favicon.

- **`logo.png`** – Kram logo; used for icon, splash, adaptive-icon, and favicon (1024×1024px recommended for best quality)
- `icon.png`, `splash.png`, `adaptive-icon.png`, `favicon.png` – optional; created as placeholders by `npm run create-assets` if you need them separately

## Quick setup

1. **Use the WebApp logo**  
   If the Kram logo is at `src/apps/TraditionalEats.WebApp/wwwroot/images/logo.png`, run from the mobile app directory:
   ```bash
   npm run create-assets
   ```
   This copies that logo into `assets/logo.png`.

2. **Or add your own**  
   Place your Kram logo at `assets/logo.png` (1024×1024px recommended for the app icon).

3. **Placeholders**  
   Running `npm run create-assets` also creates minimal placeholder PNGs for any missing files so the app runs.
