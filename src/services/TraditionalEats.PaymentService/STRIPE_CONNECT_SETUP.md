# Stripe Connect (Vendor Onboarding) Setup

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

## Development / testing without completing the questionnaire

Use **test** keys instead of live keys in `appsettings.Development.json`:

- `Stripe:SecretKey`: `sk_test_...` (from [API keys](https://dashboard.stripe.com/apikeys))
- `Stripe:PublishableKey`: `pk_test_...`

In **test mode**, Stripe does not require the platform profile to create connected accounts, so vendor Connect onboarding works immediately.
