# GitHub Actions Workflows for TraditionalEats

This directory contains CI/CD workflows for automated deployment of TraditionalEats.

## Workflows

### `deploy-staging.yml`

Automatically deploys TraditionalEats to staging when code is pushed to the `dev` branch.

**What it does:**

1. Validates the branch is `dev`
2. Loads staging server IP from `deploy/digitalocean/DROPLET_IP_STAGING`
3. Builds all 12 service images (10 services + 2 BFFs + edge)
4. Pushes images to DigitalOcean Container Registry (DOCR)
5. Syncs deployment files to the server
6. Updates `docker-compose.prod.yml` to use staging image tags
7. Pulls images and restarts containers on the staging server
8. Performs health check

**Required GitHub Secrets:**

- `DOCR_TOKEN` - DigitalOcean API token with registry access
- `SSH_PRIVATE_KEY` - Private SSH key for accessing the staging server
- `STAGING_SERVER_IP` - (Optional) Staging server IP address (if `DROPLET_IP_STAGING` file is not committed)

**Image Tagging:**

- Images are tagged with both `service-name-staging` (for deployment) and `service-name-staging-{commit-sha}` (for versioning)
- Example: `identity-service-staging` and `identity-service-staging-abc1234`

**Setup Instructions:**

1. **Create GitHub Secrets:**
   - Go to your repository → Settings → Secrets and variables → Actions
   - Add `DOCR_TOKEN` with your DigitalOcean API token
   - Add `SSH_PRIVATE_KEY` with your private SSH key (the public key should be on the server)

2. **Ensure staging server is set up:**
   - The server should have Docker and Docker Compose installed
   - The `.env` file should exist (run `./deploy/digitalocean/deploy.sh staging` manually once to generate it)
   - The server should be accessible via SSH

3. **Staging Server IP (choose one):**
   - **Option A:** Commit `deploy/digitalocean/DROPLET_IP_STAGING` file with the staging server IP
   - **Option B:** Set `STAGING_SERVER_IP` GitHub secret with the staging server IP address
   
   Note: The workflow will use the file if it exists, otherwise it will use the secret.

**Workflow Triggers:**

- Push to `dev` branch (when files in `src/**`, `deploy/digitalocean/**`, or `.github/workflows/**` change)
- Manual trigger via `workflow_dispatch`

**Future: Production Workflow**

- A production workflow (`deploy-production.yml`) will be created for the `main` branch
- It will follow the same pattern but deploy to production servers
