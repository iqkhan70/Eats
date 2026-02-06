# Running the WebApp with HTTPS

The default launch profile is **https**. The app will open at **https://localhost:5301**.

## First-time setup

Trust the ASP.NET Core HTTPS development certificate (once per machine):

```bash
dotnet dev-certs https --trust
```

If the browser shows a certificate warning, accept it or run the command above.

## Launch profiles

- **https** (default): `https://localhost:5301` — use this for Stripe Connect and production-like local testing.
- **http**: `http://localhost:5300` — use when you need HTTP only (e.g. some network setups).

Run with a specific profile:

```bash
dotnet run --launch-profile https
dotnet run --launch-profile http
```

## API calls

- **HTTP profile** (http://localhost:5300): the app calls the BFF at `ApiBaseUrl` (http://localhost:5101/api/).
- **HTTPS profile** (https://localhost:5301): the app calls the BFF at `ApiBaseUrlHttps` (https://localhost:5143/api/) to avoid mixed-content blocking.

Ensure the **Web BFF** is running with HTTPS enabled (Kestrel endpoint on port 5143). Run `dotnet dev-certs https --trust` once so the BFF’s HTTPS endpoint uses a trusted certificate.
