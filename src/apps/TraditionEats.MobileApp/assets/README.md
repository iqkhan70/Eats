# Assets Directory

This directory should contain the following files:

- `icon.png` - App icon (1024x1024px recommended)
- `splash.png` - Splash screen image
- `adaptive-icon.png` - Android adaptive icon (foreground image)
- `favicon.png` - Web favicon

## Quick Setup

For now, you can create placeholder images or use Expo's default assets. To generate proper assets:

1. Use a tool like [Expo Asset Generator](https://www.npmjs.com/package/expo-asset-generator)
2. Or create simple placeholder images using any image editor
3. Or temporarily comment out asset references in `app.json` for development

## Temporary Workaround

If you want to run the app without assets, you can temporarily remove or comment out the asset references in `app.json`:

```json
// "icon": "./assets/icon.png",
// "splash": { ... },
// etc.
```

However, for production, you'll need proper assets.
