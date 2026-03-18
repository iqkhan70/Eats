# Build and push Kram to Google Play

Use these steps to build the Android app against **https://www.kram.tech** and ship to Google Play. You need a **Google Play Developer account** ($25 one-time), an **Expo account** (free tier is enough), and a **Google Service Account** for automated submissions.

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

### 1.2 Google Play Developer account

1. Sign up at [Google Play Console](https://play.google.com/apps/publish/signup/) ($25 one-time fee).
2. Create an app: **Create app** → enter app name (e.g. **Kram**), default language, app/game, free/paid → **Create app**.

### 1.3 Create app in Google Play Console

1. In [Google Play Console](https://play.google.com/apps/publish/), go to your app’s Dashboard.
2. Complete the required setup (store listing, content rating, privacy policy, etc.). You can do this in parallel with building.
3. Under **Testing** → **Internal testing**, create a testers list (add your email) and save.

### 1.4 Google Service Account (for EAS Submit)

EAS needs a Google Service Account key to submit builds automatically. Create it once:

1. **Google Cloud Console**: Go to [Google Cloud Console](https://console.cloud.google.com/) and create or select a project.
2. **Enable API**: APIs & Services → **Library** → search for **Google Play Android Developer API** → **Enable**.
3. **Service Account**: APIs & Services → **Credentials** → **Create Credentials** → **Service Account**.
   - Name: e.g. `eas-play-submit`
   - Click **Create and Continue** → **Done**
4. **Create key**: Click the new service account → **Keys** tab → **Add Key** → **Create new key** → **JSON** → **Create**. Save the downloaded `.json` file securely.
5. **Invite to Play Console**: Copy the service account email (e.g. `eas-play-submit@your-project.iam.gserviceaccount.com`).
   - In [Google Play Console](https://play.google.com/apps/publish/) → **Users and permissions** → **Invite new users**
   - Add the service account email
   - Role: **Admin** (or at least **Release to production, exclude devices, and use Play App Signing**)
   - Save
6. **Upload key to EAS**: [expo.dev](https://expo.dev) → your project → **Credentials** → **Android** → **com.kram.mobile** → **Google Service Account Key** → **Upload new key** → upload the JSON file.

### 1.5 Package name

Your app uses `com.kram.mobile` (in `app.json` → `android.package`). The app in Play Console must use the same package name.

---

## 2. First submission (manual – required by Google)

Google requires the **first** upload to be done manually through Play Console. After that, you can use `eas submit` for all future releases.

### 2.1 Build the Android app

```bash
cd src/apps/TraditionalEats.MobileApp
eas build --platform android --profile production
```

- First time: EAS will create/store Android credentials (keystore). Choose **production** when asked.
- Build runs in the cloud (~10–20 minutes). When done, download the `.aab` file from the build page.

### 2.2 Upload manually in Play Console

1. In [Google Play Console](https://play.google.com/apps/publish/) → your app → **Testing** → **Internal testing**.
2. **Create new release**.
3. **App integrity**: Choose **Google-generated key** (recommended) if prompted.
4. **App bundles**: **Upload** → select the `.aab` file you downloaded.
5. Add a **Release name** (e.g. `1.0.0 (1)`) → **Save** → **Review release** → **Start rollout to Internal testing**.

After this first manual upload, you can use `eas submit` for all future releases.

---

## 3. Submit to Google Play (after first manual upload)

### Option A: Submit the last build

```bash
cd src/apps/TraditionalEats.MobileApp
eas submit --platform android --latest --profile production
```

EAS will use the Google Service Account key you uploaded. By default, builds go to the **internal** testing track. Use `--track production` for production.

### Option B: Build and submit in one step

```bash
eas build --platform android --profile production --auto-submit
```

### Option C: Submit a specific build

```bash
eas submit --platform android --id <build-id> --profile production
```

Use the build ID from the EAS build page or `eas build:list`.

---

## 4. Track options

You can control which track the build goes to in `eas.json` or via CLI:

| Track       | Use case                          |
|------------|------------------------------------|
| `internal` | Internal testers (default)         |
| `alpha`   | Closed testing                     |
| `beta`    | Open testing                       |
| `production` | Live on Play Store              |

Example for production:

```bash
eas submit --platform android --latest --profile production --track production
```

---

## 5. Rebuild and resubmit

For a new build after code changes:

> Note: Each release needs a unique **version code** (`versionCode`). This repo uses `appVersionSource: "remote"` and `autoIncrement: true` in the production profile, so EAS auto-increments the version code on each build.

```bash
cd src/apps/TraditionalEats.MobileApp
eas build --platform android --profile production
# when build completes:
eas submit --platform android --latest --profile production
```

---

## 6. Checklist before going to production

- [ ] Store listing (screenshots, description, icon)
- [ ] Privacy policy URL
- [ ] Content rating questionnaire
- [ ] Target audience and content
- [ ] Data safety form
- [ ] App signing (Google Play App Signing recommended)

---

## Troubleshooting

| Issue | What to do |
|-------|------------|
| **"You need to upload manually first"** | Google requires the first upload via Play Console. Follow section 2. |
| **Service account / 403 errors** | Ensure the service account is invited in Play Console with Admin (or release) role, and the JSON key is uploaded in EAS Credentials. |
| **Wrong API (staging or local)** | Use `--profile production` so the app uses www.kram.tech. |
| **Package name mismatch** | App in Play Console must use `com.kram.mobile` (same as `app.json`). |
| **Build fails** | Run `npm install` and `npx expo doctor` locally; check `eas build:list` for build logs. |

For more: [EAS Submit for Android](https://docs.expo.dev/submit/android/), [First Android submission](https://expo.fyi/first-android-submission).
