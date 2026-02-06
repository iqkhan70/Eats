# Reset password from mobile browser (phone testing)

Reset works on the computer but not on the phone because of **HTTPS certificates**:

- The dev HTTPS certificate is for **localhost** only.
- On the phone, the reset page loads from `https://YOUR_IP:5301`, but when it calls the BFF at `https://YOUR_IP:5143`, the browser sees a certificate for "localhost" and **blocks the request** (certificate hostname mismatch). That’s why it works on the computer (localhost) but not on the phone.

**Use HTTP for phone testing** so there’s no certificate check:

1. **Reset link in email**  
   IdentityService `AppSettings:BaseUrl` must be **HTTP** so the link is:
   - `http://YOUR_IP:5300/reset-password?token=...&email=...`  
     In `appsettings.Development.json` (IdentityService) set:
   - `"BaseUrl": "http://192.168.86.227:5300"` (replace with your machine IP).

2. **Run WebApp with HTTP**  
   So it listens on `0.0.0.0:5300` and the phone can open the link:

   ```bash
   cd src/apps/TraditionalEats.WebApp
   dotnet run --launch-profile http
   ```

3. **Web BFF**  
   Must be running and reachable on HTTP (e.g. `http://0.0.0.0:5101`). WebApp `ApiBaseUrl` should be `http://YOUR_IP:5101/api/` so the reset API call goes to the BFF over HTTP (no cert).

4. **WebApp config**  
   In `appsettings.Development.json` (WebApp):
   - `ApiBaseUrl`: `http://192.168.86.227:5101/api/`  
     When the page is loaded over HTTP, the app uses this and calls the BFF over HTTP, which works on the phone.

**Summary:** For reset from phone, use **HTTP** (BaseUrl `http://IP:5300`, WebApp `http` profile, ApiBaseUrl `http://IP:5101/api/`). For production you’ll use HTTPS and a proper domain/cert.
