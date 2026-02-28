# Xcode Archive Crash Troubleshooting (no EAS)

Expo Go works with production, but the Xcode-archived app crashes when building locally (bypassing EAS). Common causes and fixes:

---

## 1. Ensure `EXPO_PUBLIC_ENV=production` for the bundle

**This is the most common cause.** When you use EAS, `eas.json` sets `EXPO_PUBLIC_ENV=production`. When you build with Xcode directly, you must set it yourself.

The JS bundle is built during the Xcode "Bundle React Native code" phase. If `EXPO_PUBLIC_ENV` is not set, the app defaults to `ip` and tries `http://192.168.x.x:5102/api`, which fails on a real device and can crash.

**Fix:** Add to `ios/.xcode.env.local` (create if missing):

```bash
export EXPO_PUBLIC_ENV=production
```

This file is gitignored. For production Xcode archives, keep this in place. **Do not** put `EXPO_PUBLIC_ENV=ip` or anything else in `.xcode.env.local` when archiving for TestFlight.

---

## 2. Run a production build locally first

Verify the bundle is created correctly:

```bash
cd src/apps/TraditionalEats.MobileApp
EXPO_PUBLIC_ENV=production npx expo export --platform ios
```

If this fails, fix the error before archiving.

---

## 3. Check crash logs

**On device/simulator:**
- Xcode → Window → Devices and Simulators → select device → View Device Logs
- Or: Settings → Privacy → Analytics & Improvements → Analytics Data → find your app crash

**Look for:**
- `Fatal Exception` / `NSException`
- `JavaScript` errors (if the crash is in JS)
- `EXPO_PUBLIC` or `API_BASE_URL` (wrong config)
- `expo-apple-authentication` / `expo-location` (missing entitlements/permissions)

---

## 4. Common crash causes

| Cause | Symptom | Fix |
|-------|---------|-----|
| Wrong API URL | App loads then crashes on first API call, or white screen | Set `EXPO_PUBLIC_ENV=production` in `.xcode.env.local` |
| Bundle not embedded | Immediate crash, "Unable to load script" | Ensure Archive uses **Release**; Debug skips bundling |
| Apple Sign-In | Crash when tapping "Continue with Apple" | Add Sign in with Apple capability in Xcode; regenerate provisioning profile |
| Hermes / minification | Odd JS errors in release | Try disabling Hermes temporarily to isolate |
| Missing native module | Crash on specific screen (e.g. maps, image picker) | Run `npx expo prebuild --clean` and rebuild |

---

## 5. Clean rebuild

```bash
cd src/apps/TraditionalEats.MobileApp

# Clean
rm -rf ios/build
rm -rf ~/Library/Developer/Xcode/DerivedData/Kram-*
cd ios && pod install && cd ..

# Ensure production env
echo 'export EXPO_PUBLIC_ENV=production' >> ios/.xcode.env.local

# Rebuild in Xcode: Product → Clean Build Folder, then Product → Archive
```

---

## 6. Build number for TestFlight

App Store Connect requires a unique build number for each upload. Manually increment `app.json` → `expo.ios.buildNumber` before each archive, or use `npx expo prebuild` with `--no-install` to sync version from app.json.
