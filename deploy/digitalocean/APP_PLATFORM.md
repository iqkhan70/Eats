# Deploy Kram to DigitalOcean App Platform

Use **app-spec.yaml** with `doctl` to deploy with **HTTPS at the edge** (App Platform ingress) and **HTTP** between your components.

## Prerequisites

- [doctl](https://docs.digitalocean.com/reference/doctl/how-to/install/) installed and authenticated: `doctl auth init --access-token YOUR_TOKEN`
- DigitalOcean Container Registry (DOCR) created; images built and pushed (see below)
- App spec filled with your registry name and secrets

## 1. Build and push images

From the repo root, build and push to your DOCR:

```bash
cd deploy/digitalocean
cp .env.example .env
# Edit .env: set REGISTRY=registry.digitalocean.com/your-registry-name, DIGITALOCEAN_ACCESS_TOKEN, MYSQL_ROOT_PASSWORD, JWT_SECRET, APP_BASE_URL, etc.
./deploy.sh build
```

This builds all images (edge, web-bff, mobile-bff, identity-service, etc.) and pushes them to DOCR.

## 2. Edit app-spec.yaml

- **registry**: Set to your DOCR name (e.g. `registry.digitalocean.com/your-registry-name`). Leave empty if your doctl/default registry is already set.
- **envs**: Fill secrets and URLs:
  - **edge**: `APP_BASE_URL` = your app’s public URL (e.g. `https://kram-xxx.ondigitalocean.app`) so the Blazor app and reset-password links use the right API base.
  - **web-bff / mobile-bff**: Set `Services__IdentityService`, `Services__CustomerService`, etc. to the **default URLs** of the corresponding App Platform services (e.g. `https://identity-service-xxx.ondigitalocean.app`). You can get these after the first deploy from the DO control panel, or use [bindable env vars](https://docs.digitalocean.com/products/app-platform/how-to/use-environment-variables/) if your account supports them.
  - **identity-service**: `ConnectionStrings__IdentityDb`, `Jwt__Secret`, `AppSettings__BaseUrl`, `Services__NotificationService` (notification-service URL).
  - **Other services**: Set `ConnectionStrings__*` (and any Redis/RabbitMQ URLs if you use managed DBs).

Use the DO dashboard to store **SECRET** values (JWT, connection strings) and reference them in the spec or set them after create.

## 3. Create or update the app

**First time:**

```bash
doctl apps create --spec app-spec.yaml
```

**Later (after editing app-spec.yaml):**

```bash
export APP_ID=your-app-id   # From doctl apps list
doctl apps update $APP_ID --spec app-spec.yaml
```

Or use the deploy script (set `APP_ID` in `.env` for updates):

```bash
./deploy.sh app-platform
```

## 4. Ingress and HTTPS

- App Platform terminates **HTTPS** at the edge.
- **ingress** in the spec routes:
  - `/` → **edge** (WebApp static)
  - `/api/WebBff` → **web-bff**
  - `/api/MobileBff` → **mobile-bff**
- Microservices (identity, customer, order, etc.) have **no ingress rules**; they get default `.ondigitalocean.app` URLs. Only the BFFs and edge are meant to be called by the browser/mobile app; the BFFs call the microservices over HTTP using those URLs (or internal networking when available).

## 5. Databases

The spec does not define **databases**; add them in the DO control panel (MySQL, Redis) or use external connection strings. Then set `ConnectionStrings__*` (and Redis/RabbitMQ if needed) in each service’s env in the spec or in the dashboard.

## Summary

1. Build and push images: `./deploy.sh build`
2. Fill `app-spec.yaml` (registry, envs, secrets).
3. Create app: `doctl apps create --spec app-spec.yaml`
4. Set service URLs on web-bff and mobile-bff (and any secrets) in the dashboard if not in the spec.
5. Point your domain at the app (optional) in the DO control panel.
