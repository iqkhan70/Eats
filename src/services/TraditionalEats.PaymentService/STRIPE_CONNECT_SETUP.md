# Stripe Connect (Vendor Onboarding) Setup

## Why the app still shows "Stripe setup incomplete" after you finished in Stripe

The platform **only** knows about connected accounts that **it** creates and that are completed via **the app’s “Finish Stripe setup” link**. It checks `details_submitted` and `charges_enabled` (and `payouts_enabled`) for **that** account.

- **You completed onboarding in the Stripe Dashboard manually**  
  Then Stripe has a different connected account than the one our app created and stored. The app never received that account ID and never marks it as complete.  
  **Fix:** Use **“Finish Stripe setup”** in the app. That opens our Stripe Connect Account Link; when you complete that flow, our app has the correct `acct_xxx` and (via webhook or refresh) can set status to Complete.

- **Test vs live mismatch**  
  If the app uses **test** keys (`sk_test_...`) but you completed onboarding in the **live** Stripe Dashboard (or the other way around), the account we have is for the other mode.  
  **Fix:** Use the same mode everywhere: app config must use test keys for test, live keys for live, and complete onboarding via the app’s link so the account is created with that key.

- **Webhook not updating our DB**  
  We update status when Stripe sends `account.updated`. If the webhook URL isn’t set for Connect, or is for the wrong mode, we don’t get the event.  
  **Fix:** We now **sync from Stripe when you open the Vendor Dashboard** (Web and Mobile) and on pull-to-refresh (Mobile). So after you complete onboarding via the app’s link, open the Vendor Dashboard again (or pull to refresh); we call Stripe, update our DB, and the banner should disappear. Ensure **Stripe:WebhookSecret** is set for the same mode (test/live) as **Stripe:SecretKey** if you want webhook updates as well.

## If you see: "You must complete your platform profile to use Connect..."

You are using **live** Stripe keys (`sk_live_...`). Stripe requires your platform to complete a one-time questionnaire before you can create **live** connected accounts (vendor onboarding).

**Fix:** Open **[Stripe Connect Accounts Overview](https://dashboard.stripe.com/connect/accounts/overview)** and complete the platform profile questionnaire. After that, "Finish Stripe setup" in the app will work with live keys.

## If you get 400 Bad Request ("Why you see 400")

After you click **Finish Stripe setup**, the Vendor Dashboard shows the **exact Stripe error** in a red box under the banner ("Why you see 400"). Use that message to fix the issue.

**Common causes and fixes:**

1. **Redirect URL not allowed (live mode)**  
   In **live mode**, Stripe may require redirect URLs to be allow-listed:
   - Open **[Stripe Dashboard → Connect → Settings](https://dashboard.stripe.com/settings/connect)** (or [Connect onboarding options](https://dashboard.stripe.com/settings/connect/onboarding)).
   - Add your **redirect URLs** for the URL you actually use to open the app:
     - **If you use the app at localhost:**  
       `https://localhost:5301/vendor?stripe=return` and `https://localhost:5301/vendor?stripe=refresh`
     - **If you use the app at your machine’s IP** (e.g. from phone):  
       `https://YOUR_IP:5301/vendor?stripe=return` and `https://YOUR_IP:5301/vendor?stripe=refresh`  
       (Replace `YOUR_IP` with your Mac’s IP, e.g. `192.168.1.5`.)
   - You can add both localhost and your IP if you use both.

2. **Return URL must be HTTPS and match how you open the app**  
   Set `Stripe:ConnectReturnUrl` (or `AppBaseUrl`) in PaymentService `appsettings.Development.json` to the **same** base URL you use in the browser: `https://localhost:5301` if you use localhost, or `https://YOUR_MAC_IP:5301` if you use your Mac’s IP (e.g. from phone). Must be HTTPS.

3. **Account needs remediation link**  
   If Stripe says the connected account must use a remediation link, use the **paste remediation link** box on the Vendor Dashboard and open the link Stripe gave you (e.g. from the Dashboard).

4. **Other**  
   Use the exact error text shown in the banner and look it up in [Stripe’s API errors](https://docs.stripe.com/api/errors) or contact Stripe support.

### Using a Stripe remediation link

If Stripe (e.g. in the Dashboard) gives you a **remediation link** for your connected account (“Send this remediation link to your connected account…”), you can still complete setup:

1. In the **Vendor Dashboard** in the app, under the “Stripe setup incomplete” banner you’ll see: **“If you got a remediation link from Stripe, paste it below and open it”**.
2. Paste the full link (e.g. `https://connect.stripe.com/d/setup/e/...`) into the box and click **Open link**.
3. Complete the Stripe-hosted form. When done, Stripe will redirect you back; our webhook will update onboarding status when Stripe notifies us.

This works when the connected account has outstanding requirements and Stripe provides a one-off link instead of a new account link from our API.

## Testing Stripe Connect locally

1. **Use test keys (recommended for local)**  
   In PaymentService `appsettings.Development.json` (or User Secrets):
   - `Stripe:SecretKey`: `sk_test_...` (from [API keys](https://dashboard.stripe.com/apikeys) — switch to **Test mode** in the Dashboard)
   - `Stripe:PublishableKey`: `pk_test_...`  
   In test mode you don’t need the platform questionnaire and redirect URLs are easier.

2. **Set the return URL to match how you open the app**  
   In PaymentService `appsettings.Development.json`:
   - **Same machine (browser at localhost):**  
     `"Stripe": { "ConnectReturnUrl": "https://localhost:5301" }`
   - **Phone or another device (browser at your machine’s IP):**  
     `"Stripe": { "ConnectReturnUrl": "https://YOUR_IP:5301" }`  
     Replace `YOUR_IP` with your machine’s IP (e.g. macOS: `ipconfig getifaddr en0`).  
     Run the WebApp so it listens on all interfaces (e.g. launch profile with `https://0.0.0.0:5301`) and open the WebApp on the phone at `https://YOUR_IP:5301`.

3. **Allow redirect URLs in Stripe (test mode)**  
   In **[Stripe Dashboard → Connect → Settings](https://dashboard.stripe.com/settings/connect)** (with **Test mode** on), add the redirect URLs you use:
   - Localhost: `https://localhost:5301/vendor?stripe=return` and `https://localhost:5301/vendor?stripe=refresh`
   - Or your IP: `https://YOUR_IP:5301/vendor?stripe=return` and `https://YOUR_IP:5301/vendor?stripe=refresh`

4. **Run the stack and test**  
   - Start services (e.g. `./start-all.sh` or run PaymentService, Web BFF, WebApp, IdentityService, etc.).
   - Open the **WebApp** at the same URL you set in `ConnectReturnUrl` (e.g. `https://localhost:5301`).
   - Sign in with a **Vendor** account.
   - Go to **Vendor Dashboard** → click **Finish Stripe setup**. You’ll be sent to Stripe’s onboarding.
   - Complete the form (in test mode you can use [Stripe test data](https://docs.stripe.com/connect/account-tokens)); Stripe will redirect back to `https://localhost:5301/vendor?stripe=return` (or your IP).
   - The Vendor Dashboard will call the refresh endpoint and the “Stripe setup incomplete” banner should disappear.

5. **Mobile app locally**  
   If you’re testing from the **mobile app** (e.g. Expo), “Finish Stripe setup” opens the browser. When onboarding is done, Stripe redirects to the **WebApp** return URL (e.g. `https://localhost:5301/vendor?stripe=return` or your IP). So that URL must be reachable from the device (localhost won’t work from a phone — use your machine’s IP and add those redirect URLs in Stripe). After finishing, return to the app and open Vendor Dashboard again (or pull to refresh); we sync status from Stripe and the banner updates.

## Development / testing without completing the questionnaire

Use **test** keys instead of live keys in `appsettings.Development.json`:

- `Stripe:SecretKey`: `sk_test_...` (from [API keys](https://dashboard.stripe.com/apikeys))
- `Stripe:PublishableKey`: `pk_test_...`

In **test mode**, Stripe does not require the platform profile to create connected accounts, so vendor Connect onboarding works immediately.
