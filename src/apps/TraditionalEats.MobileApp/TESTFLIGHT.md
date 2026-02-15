# Build and push Kram to TestFlight

Use these steps to build the iOS app against **https://www.kram.tech** and ship to TestFlight. You need an **Apple Developer account** and an **Expo account** (free tier is enough).

---

## 1. One-time setup

### 1.1 Expo account and EAS CLI

1. Create an Expo account at [expo.dev](https://expo.dev) (free).
2. Install EAS CLI and log in:
   ```bash
   npm install -g eas-cli
   eas login
   ```
3. Link the project to your Expo account (from the **mobile app** directory):
   ```bash
   cd src/apps/TraditionalEats.MobileApp
   eas build:configure
   ```
   If prompted, confirm using the existing `eas.json`. The project will be linked to your Expo account.

### 1.2 Apple Developer and App Store Connect

1. In [App Store Connect](https://appstoreconnect.apple.com), create an app:
   - **Apps** → **+** → **New App**
   - Platform: iOS  
   - Name: **Kram** (or your chosen name)  
   - Primary language, bundle ID: **com.kram.mobile** (must match `app.json` → `ios.bundleIdentifier`)  
   - SKU: e.g. `kram-mobile`

2. Note your **Apple ID** (email) and **Team ID** (App Store Connect → **Users and Access** → **Team ID**). You’ll need them for EAS.

---

## 2. Build for production (TestFlight)

The **production** profile in `eas.json` sets `EXPO_PUBLIC_ENV=production`, so the app uses **https://www.kram.tech/api** and **https://www.kram.tech/chatHub**.

From the mobile app directory:

```bash
cd src/apps/TraditionalEats.MobileApp
eas build --platform ios --profile production
```

- First time: EAS will ask for **Apple ID** and **Team ID** and will create/store credentials (distribution certificate, provisioning profile). Choose **production** when asked.
- Build runs in the cloud (about 10–20 minutes). When it finishes, you get a build page URL and an `.ipa` link.

---

## 3. Submit to TestFlight

### Option A: Submit the last build (recommended)

```bash
eas submit --platform ios --latest --profile production
```

- **Apple ID**: your Apple Developer email  
- **App-specific password**: create one at [appleid.apple.com](https://appleid.apple.com) → Sign-In and Security → App-Specific Passwords  
- **Asc App ID**: the numeric App Store Connect app ID (App Store Connect → your app → **App Information** → **Apple ID**)

### Option B: Submit a specific build

```bash
eas submit --platform ios --id <build-id> --profile production
```

Use the build ID from the EAS build page or from `eas build:list`.

---

## 4. After submit

1. In **App Store Connect** → your app → **TestFlight**, wait for the build to finish processing (often 5–15 minutes).
2. Add **Internal** or **External** testers and send the build.
3. Testers install **TestFlight** from the App Store, accept your invite, and install **Kram**. The app will talk to **https://www.kram.tech**; no local backend or ngrok needed.

---

## 5. Rebuild and resubmit

For a new TestFlight build after code changes:

```bash
cd src/apps/TraditionalEats.MobileApp
eas build --platform ios --profile production
# when build completes:
eas submit --platform ios --latest --profile production
```

---

## Troubleshooting

| Issue | What to do |
|--------|------------|
| **Credentials / signing errors** | Run `eas credentials --platform ios` and fix or regenerate the production provisioning profile and certificate. |
| **Wrong API (staging or local)** | Ensure you used `--profile production` so `EXPO_PUBLIC_ENV=production` is set and the app uses www.kram.tech. |
| **“App not found” on submit** | Create the app in App Store Connect with bundle ID **com.kram.mobile** and use the correct Asc App ID when submitting. |
| **Build fails (Node/Expo)** | Ensure `package.json` and `app.json` are valid; run `npm install` and `npx expo doctor` locally. |

For more: [EAS Build for iOS](https://docs.expo.dev/build-reference/ios-builds/), [EAS Submit](https://docs.expo.dev/submit/ios/).
