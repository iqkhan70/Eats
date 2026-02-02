#!/usr/bin/env bash
# ============================================================================
# TraditionalEats – Consolidated deploy script (aligned with mental health app)
# ============================================================================
# One-command deploy: put your Droplet IP in DROPLET_IP (or DROPLET_IP_STAGING /
# DROPLET_IP_PRODUCTION), then run ./deploy.sh. No .env editing required for
# first run – the script installs Docker on the droplet, generates .env on the
# server (or reuses existing), copies files, starts the stack, and creates DBs.
#
# Usage:
#   ./deploy.sh                    – Full deploy (DROPLET_IP from DROPLET_IP file)
#   ./deploy.sh staging            – Deploy to staging (DROPLET_IP from DROPLET_IP_STAGING)
#   ./deploy.sh production         – Deploy to production (DROPLET_IP from DROPLET_IP_PRODUCTION)
#   ./deploy.sh build              – Build and push images to DOCR (needs REGISTRY in secrets.env)
#   ./deploy.sh build-on-server    – Sync repo to Droplet and build there (fast on amd64; no 80-min Mac build)
#   ./deploy.sh app-platform       – Deploy via doctl App Platform
#
# First time: Create DROPLET_IP with your Droplet IP:  echo '1.2.3.4' > DROPLET_IP
# Like mental health app: we never build on your Mac. Build-on-server builds on the Droplet, pushes to DOCR, then pull+up.
# Optional: Create secrets.env with REGISTRY=..., DIGITALOCEAN_ACCESS_TOKEN=... so build-on-server can push and deploy can pull.
# ============================================================================

set -e

GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
cd "$SCRIPT_DIR"

export COMPOSE_FILE="$SCRIPT_DIR/docker-compose.prod.yml"

# Optional: load secrets for build/push (REGISTRY, DIGITALOCEAN_ACCESS_TOKEN, etc.)
if [ -f "$SCRIPT_DIR/secrets.env" ]; then
  set -a
  source "$SCRIPT_DIR/secrets.env"
  set +a
fi
if [ -f .env ]; then
  set -a
  source .env
  set +a
fi

export ENV_FILE="$SCRIPT_DIR/.env"

usage() {
  echo "Usage: $0 [staging|production|build|build-on-server|app-platform]"
  echo "  (no args)        – Full deploy using DROPLET_IP file"
  echo "  staging          – Full deploy using DROPLET_IP_STAGING file"
  echo "  production       – Full deploy using DROPLET_IP_PRODUCTION file"
  echo "  build            – Build all images and push to DOCR (slow on Mac/ARM)"
  echo "  build-on-server  – Sync repo to Droplet, build there, then up (fast; no DOCR needed)"
  echo "  app-platform     – Deploy via doctl (create/update from app-spec.yaml)"
  exit 1
}

# ----- Build (optional, for pushing to DOCR) -----
cmd_build() {
  echo -e "${BLUE}Building images (from repo root: $REPO_ROOT)...${NC}"
  cd "$REPO_ROOT"
  export DOCKER_BUILDKIT=1
  if [ -z "$REGISTRY" ]; then
    echo -e "${YELLOW}REGISTRY not set; building with default tag. Set REGISTRY in secrets.env for push.${NC}"
  fi
  TAG="${IMAGE_TAG:-latest}"
  if [ -f "$ENV_FILE" ]; then
    docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" build
  else
    REGISTRY="${REGISTRY:-traditionaleats}" IMAGE_TAG="${IMAGE_TAG:-latest}" docker compose -f "$COMPOSE_FILE" build
  fi
  if [ -n "$REGISTRY" ] && [ -n "$DIGITALOCEAN_ACCESS_TOKEN" ]; then
    echo "Logging in to $REGISTRY..."
    echo "$DIGITALOCEAN_ACCESS_TOKEN" | docker login "$REGISTRY" -u "$DIGITALOCEAN_ACCESS_TOKEN" --password-stdin 2>/dev/null || true
    echo "Pushing images..."
    if [ -f "$ENV_FILE" ]; then
      docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" push
    else
      REGISTRY="$REGISTRY" IMAGE_TAG="${IMAGE_TAG:-latest}" docker compose -f "$COMPOSE_FILE" push
    fi
  fi
  echo -e "${GREEN}Build done.${NC}"
}

# ----- Build on Droplet (avoids slow amd64 emulation on Mac) -----
cmd_build_on_server() {
  local ENV_ARG="${1:-default}"
  source "$SCRIPT_DIR/load-droplet-ip.sh" "$ENV_ARG"
  DROPLET_USER="${DROPLET_USER:-root}"
  SSH_KEY_PATH="${SSH_KEY_PATH:-$HOME/.ssh/id_rsa}"
  SSH_CMD="ssh -o StrictHostKeyChecking=accept-new"
  if [ -f "$SSH_KEY_PATH" ]; then
    SSH_CMD="ssh -i $SSH_KEY_PATH -o StrictHostKeyChecking=accept-new"
  fi

  echo -e "${GREEN}========================================${NC}"
  echo -e "${GREEN}TraditionalEats – Build on server${NC}"
  echo -e "${GREEN}========================================${NC}"
  echo -e "  Droplet: ${YELLOW}$DROPLET_IP${NC}"
  echo ""

  echo -e "${BLUE}Testing SSH...${NC}"
  if ! $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "echo 'SSH OK'" >/dev/null 2>&1; then
    echo -e "${RED}Cannot connect to $DROPLET_IP. Check DROPLET_IP file and SSH key.${NC}"
    exit 1
  fi
  echo -e "${GREEN}SSH OK${NC}"
  echo ""

  echo -e "${GREEN}Step 0: Ensuring Docker on droplet${NC}"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" 'bash -s' << 'ENDSSH'
    set -e
    export DEBIAN_FRONTEND=noninteractive
    if ! command -v docker &>/dev/null; then
      echo "Installing Docker..."
      apt-get update -y
      apt-get install -y ca-certificates curl gnupg lsb-release
      install -m 0755 -d /etc/apt/keyrings
      curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
      chmod a+r /etc/apt/keyrings/docker.gpg
      echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null
      apt-get update -y
      apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
      systemctl enable docker
      systemctl start docker
      echo "Docker installed."
    else
      echo "Docker already installed."
    fi
    if ! docker compose version &>/dev/null && ! command -v docker-compose &>/dev/null; then
      apt-get install -y docker-compose-plugin 2>/dev/null || true
    fi
ENDSSH
  echo ""

  echo -e "${GREEN}Step 0b: Ensuring swap (prevents OOM lockup on 4GB)${NC}"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" 'bash -s' << 'ENDSWAP'
    set -e
    NEED_SWAP=0
    if [ ! -f /swapfile ]; then NEED_SWAP=1; fi
    if [ -f /swapfile ] && [ $(stat -c%s /swapfile 2>/dev/null || echo 0) -lt 2147483648 ]; then NEED_SWAP=1; fi
    if [ "$NEED_SWAP" = "1" ]; then
      echo "Creating 2GB swap file..."
      swapoff /swapfile 2>/dev/null || true
      rm -f /swapfile
      fallocate -l 2G /swapfile 2>/dev/null || dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
      chmod 600 /swapfile
      mkswap /swapfile
      swapon /swapfile
      grep -q '/swapfile' /etc/fstab 2>/dev/null || echo '/swapfile none swap sw 0 0' >> /etc/fstab
      echo "Swap enabled."
    else
      echo "Swap already present."
    fi
ENDSWAP
  echo ""

  echo -e "${GREEN}Step 1: Creating .env on server${NC}"
  EXISTING_ENV=$($SSH_CMD "$DROPLET_USER@$DROPLET_IP" "cat /opt/traditionaleats/.env 2>/dev/null" || echo "")
  get_var() { echo "$EXISTING_ENV" | grep "^$1=" | cut -d'=' -f2- | tr -d '"' | tr -d "'" || echo ""; }
  if [ -n "$EXISTING_ENV" ]; then
    MYSQL_ROOT_PASSWORD=$(get_var MYSQL_ROOT_PASSWORD)
    JWT_SECRET=$(get_var JWT_SECRET)
    RABBITMQ_PASSWORD=$(get_var RABBITMQ_PASSWORD)
    APP_BASE_URL=$(get_var APP_BASE_URL)
    REGISTRY=$(get_var REGISTRY)
    REPO_NAME=$(get_var REPO_NAME)
  fi
  [ -z "$MYSQL_ROOT_PASSWORD" ] && MYSQL_ROOT_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
  [ -z "$JWT_SECRET" ] && JWT_SECRET=$(openssl rand -base64 48 | tr -d "=+/" | cut -c1-48)
  [ -z "$RABBITMQ_PASSWORD" ] && RABBITMQ_PASSWORD=$(openssl rand -base64 24 | tr -d "=+/" | cut -c1-24)
  if [ -z "$APP_BASE_URL" ]; then
    [ "$ENV_ARG" = "staging" ] && APP_BASE_URL="${STAGING_BASE_URL:-https://www.caseflowstage.store}" || APP_BASE_URL="${PRODUCTION_BASE_URL:-http://$DROPLET_IP}"
  fi
  APP_BASE_URL="${APP_BASE_URL%/}"
  REGISTRY="${REGISTRY:-registry.digitalocean.com/cha-registry}"
  REPO_NAME="${REPO_NAME:-traditionaleats}"
  [ "$ENV_ARG" = "staging" ] && ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Staging}" || ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
  [ -z "$STRIPE_CONNECT_RETURN_URL" ] && STRIPE_CONNECT_RETURN_URL="$APP_BASE_URL"
  HTTPS_ONLY="false"
  DOMAIN="${STAGING_DOMAIN:-}"
  [[ "$APP_BASE_URL" =~ ^https:// ]] && [ -z "$DOMAIN" ] && DOMAIN=$(echo "$APP_BASE_URL" | sed -e 's|https\?://||' -e 's|/.*||' -e 's|:.*||')
  ENV_CONTENT="# TraditionalEats – build-on-server
ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT
APP_BASE_URL=$APP_BASE_URL
MYSQL_ROOT_PASSWORD=$MYSQL_ROOT_PASSWORD
RABBITMQ_USER=${RABBITMQ_USER:-admin}
RABBITMQ_PASSWORD=$RABBITMQ_PASSWORD
JWT_SECRET=$JWT_SECRET
JWT_ISSUER=${JWT_ISSUER:-TraditionalEats}
JWT_AUDIENCE=${JWT_AUDIENCE:-TraditionalEats}
EMAIL_ENABLED=${EMAIL_ENABLED:-true}
EMAIL_PROVIDER=${EMAIL_PROVIDER:-Mailgun}
EMAIL_MAILGUN_API_KEY=${EMAIL_MAILGUN_API_KEY:-}
EMAIL_MAILGUN_DOMAIN=${EMAIL_MAILGUN_DOMAIN:-}
EMAIL_FROM_EMAIL=${EMAIL_FROM_EMAIL:-noreply@traditionaleats.com}
EMAIL_FROM_NAME=${EMAIL_FROM_NAME:-TraditionalEats}
EMAIL_SMTP_HOST=${EMAIL_SMTP_HOST:-smtp.gmail.com}
EMAIL_SMTP_PORT=${EMAIL_SMTP_PORT:-587}
EMAIL_SMTP_USERNAME=${EMAIL_SMTP_USERNAME:-}
EMAIL_SMTP_PASSWORD=${EMAIL_SMTP_PASSWORD:-}
EMAIL_ENABLE_SSL=${EMAIL_ENABLE_SSL:-true}
VONAGE_ENABLED=${VONAGE_ENABLED:-false}
VONAGE_API_KEY=${VONAGE_API_KEY:-}
VONAGE_API_SECRET=${VONAGE_API_SECRET:-}
VONAGE_FROM_NUMBER=${VONAGE_FROM_NUMBER:-}
STRIPE_SECRET_KEY=${STRIPE_SECRET_KEY:-}
STRIPE_WEBHOOK_SECRET=${STRIPE_WEBHOOK_SECRET:-}
STRIPE_PUBLISHABLE_KEY=${STRIPE_PUBLISHABLE_KEY:-}
STRIPE_CONNECT_RETURN_URL=${STRIPE_CONNECT_RETURN_URL:-$APP_BASE_URL}
OLLAMA_BASE_URL=${OLLAMA_BASE_URL:-http://ollama:11434}
OPENAI_API_KEY=${OPENAI_API_KEY:-}
REGISTRY=$REGISTRY
REPO_NAME=$REPO_NAME
DOMAIN=$DOMAIN
HTTPS_ONLY=$HTTPS_ONLY
"
  echo "$ENV_CONTENT" | $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "mkdir -p /opt/traditionaleats && cat > /opt/traditionaleats/.env && chmod 600 /opt/traditionaleats/.env"
  echo ""

  echo -e "${GREEN}Step 2: Syncing repo to server (this may take a few minutes)${NC}"
  if [ -f "$SSH_KEY_PATH" ]; then
    rsync -az -e "ssh -i $SSH_KEY_PATH -o StrictHostKeyChecking=accept-new" \
      --exclude='.git' --exclude='node_modules' --exclude='bin' --exclude='obj' --exclude='.env' --exclude='.vs' --exclude='*.user' \
      "$REPO_ROOT/" "$DROPLET_USER@$DROPLET_IP:/opt/traditionaleats/"
  else
    rsync -az -e "ssh -o StrictHostKeyChecking=accept-new" \
      --exclude='.git' --exclude='node_modules' --exclude='bin' --exclude='obj' --exclude='.env' --exclude='.vs' --exclude='*.user' \
      "$REPO_ROOT/" "$DROPLET_USER@$DROPLET_IP:/opt/traditionaleats/"
  fi
  echo -e "${GREEN}Sync done${NC}"
  echo ""

  echo -e "${GREEN}Step 2b: Generating nginx.conf (DOMAIN=$DOMAIN, HTTPS_ONLY=$HTTPS_ONLY)${NC}"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "cd /opt/traditionaleats && source .env 2>/dev/null; export DOMAIN HTTPS_ONLY; bash deploy/digitalocean/scripts/generate-nginx-conf.sh > deploy/digitalocean/nginx/nginx.conf"
  echo ""

  echo -e "${GREEN}Step 3: Building images on server (one at a time to avoid OOM, ~10–25 min)${NC}"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "cd /opt/traditionaleats && COMPOSE_PARALLEL_LIMIT=1 docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env build"
  echo ""

  # Step 3b: Push to registry from server (like mental health app – images in DOCR, no build on Mac)
  if echo "$REGISTRY" | grep -q registry.digitalocean.com && [ -n "$DIGITALOCEAN_ACCESS_TOKEN" ]; then
    echo -e "${GREEN}Step 3b: Pushing images to DOCR from server${NC}"
    if ! echo "$DIGITALOCEAN_ACCESS_TOKEN" | $SSH_CMD "$DROPLET_USER@$DROPLET_IP" 'read -r token; echo "$token" | docker login registry.digitalocean.com -u "$token" --password-stdin'; then
      echo -e "${YELLOW}DOCR login failed; skipping push. Images stay on server only. Set DIGITALOCEAN_ACCESS_TOKEN in secrets.env to push.${NC}"
    else
      $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "cd /opt/traditionaleats && docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env push"
      echo -e "${GREEN}Push done. Images are in the registry.${NC}"
    fi
    echo ""
  fi

  echo -e "${GREEN}Step 4: Pull and start containers (from registry when available)${NC}"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "cd /opt/traditionaleats && docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env pull && docker compose -f deploy/digitalocean/docker-compose.prod.yml --env-file .env up -d"
  echo ""

  echo -e "${GREEN}Step 5: Creating databases${NC}"
  sleep 10
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "cp /opt/traditionaleats/.env /opt/traditionaleats/deploy/digitalocean/.env 2>/dev/null; cd /opt/traditionaleats && bash deploy/digitalocean/scripts/mysql-init.sh" 2>/dev/null && echo -e "${GREEN}Databases ready${NC}" || echo -e "${YELLOW}If needed, on server run: cd /opt/traditionaleats && bash deploy/digitalocean/scripts/mysql-init.sh${NC}"
  echo ""
  echo -e "${GREEN}Done. App: http://$DROPLET_IP (ports 80/443).${NC}"
}

# ----- App Platform -----
cmd_app_platform() {
  if [ ! -f app-spec.yaml ]; then
    echo -e "${RED}app-spec.yaml not found. Use 'droplet' flow or create app-spec.yaml.${NC}"
    exit 1
  fi
  if ! command -v doctl >/dev/null 2>&1; then
    echo -e "${RED}Install doctl: https://docs.digitalocean.com/reference/doctl/how-to/install/${NC}"
    exit 1
  fi
  doctl apps create --spec app-spec.yaml 2>/dev/null || doctl apps update "${APP_ID}" --spec app-spec.yaml
}

# ----- Full Droplet deploy (like mental health app: Docker install, .env on server, compose up, DB init) -----
cmd_droplet() {
  local ENV_ARG="${1:-default}"
  source "$SCRIPT_DIR/load-droplet-ip.sh" "$ENV_ARG"
  DROPLET_USER="${DROPLET_USER:-root}"
  SSH_KEY_PATH="${SSH_KEY_PATH:-$HOME/.ssh/id_rsa}"
  SSH_CMD="ssh -o StrictHostKeyChecking=accept-new"
  SCP_CMD="scp -o StrictHostKeyChecking=accept-new"
  if [ -f "$SSH_KEY_PATH" ]; then
    SSH_CMD="ssh -i $SSH_KEY_PATH -o StrictHostKeyChecking=accept-new"
    SCP_CMD="scp -i $SSH_KEY_PATH -o StrictHostKeyChecking=accept-new"
  fi

  echo -e "${GREEN}========================================${NC}"
  echo -e "${GREEN}TraditionalEats – Deploy to Droplet${NC}"
  echo -e "${GREEN}========================================${NC}"
  echo -e "  Droplet: ${YELLOW}$DROPLET_IP${NC}"
  echo ""

  # Test SSH
  echo -e "${BLUE}Testing SSH...${NC}"
  if ! $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "echo 'SSH OK'" >/dev/null 2>&1; then
    echo -e "${RED}Cannot connect to $DROPLET_IP. Check DROPLET_IP file and SSH key (e.g. $SSH_KEY_PATH).${NC}"
    exit 1
  fi
  echo -e "${GREEN}SSH OK${NC}"
  echo ""

  # Step 0: Ensure Docker is installed on droplet
  echo -e "${GREEN}Step 0: Ensuring Docker on droplet${NC}"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" 'bash -s' << 'ENDSSH'
    set -e
    export DEBIAN_FRONTEND=noninteractive
    if ! command -v docker &>/dev/null; then
      echo "Installing Docker..."
      apt-get update -y
      apt-get install -y ca-certificates curl gnupg lsb-release
      install -m 0755 -d /etc/apt/keyrings
      curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
      chmod a+r /etc/apt/keyrings/docker.gpg
      echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null
      apt-get update -y
      apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
      systemctl enable docker
      systemctl start docker
      echo "Docker installed."
    else
      echo "Docker already installed."
    fi
    if ! docker compose version &>/dev/null && ! command -v docker-compose &>/dev/null; then
      apt-get install -y docker-compose-plugin 2>/dev/null || true
    fi
ENDSSH
  echo -e "${GREEN}Docker ready${NC}"
  echo ""

  echo -e "${GREEN}Step 0b: Ensuring swap (prevents OOM lockup on 4GB)${NC}"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" 'bash -s' << 'ENDSWAP'
    set -e
    NEED_SWAP=0
    if [ ! -f /swapfile ]; then NEED_SWAP=1; fi
    if [ -f /swapfile ] && [ $(stat -c%s /swapfile 2>/dev/null || echo 0) -lt 2147483648 ]; then NEED_SWAP=1; fi
    if [ "$NEED_SWAP" = "1" ]; then
      echo "Creating 2GB swap file..."
      swapoff /swapfile 2>/dev/null || true
      rm -f /swapfile
      fallocate -l 2G /swapfile 2>/dev/null || dd if=/dev/zero of=/swapfile bs=1M count=2048 status=none
      chmod 600 /swapfile
      mkswap /swapfile
      swapon /swapfile
      grep -q '/swapfile' /etc/fstab 2>/dev/null || echo '/swapfile none swap sw 0 0' >> /etc/fstab
      echo "Swap enabled."
    else
      echo "Swap already present."
    fi
ENDSWAP
  echo ""

  # Deploy never builds on your Mac (like mental health app). Images come from: build-on-server (build on droplet + push) or CI. Step 1 is create .env and copy files only.
  # Step 2: Create .env on server (reuse existing or use defaults from appsettings / secrets.env)
  echo -e "${GREEN}Step 2: Creating .env on server${NC}"
  EXISTING_ENV=$($SSH_CMD "$DROPLET_USER@$DROPLET_IP" "cat /opt/traditionaleats/.env 2>/dev/null" || echo "")
  get_var() { echo "$EXISTING_ENV" | grep "^$1=" | cut -d'=' -f2- | tr -d '"' | tr -d "'" || echo ""; }
  if [ -n "$EXISTING_ENV" ]; then
    MYSQL_ROOT_PASSWORD=$(get_var MYSQL_ROOT_PASSWORD)
    JWT_SECRET=$(get_var JWT_SECRET)
    RABBITMQ_PASSWORD=$(get_var RABBITMQ_PASSWORD)
    APP_BASE_URL=$(get_var APP_BASE_URL)
    EMAIL_MAILGUN_API_KEY=$(get_var EMAIL_MAILGUN_API_KEY)
    EMAIL_MAILGUN_DOMAIN=$(get_var EMAIL_MAILGUN_DOMAIN)
    EMAIL_SMTP_USERNAME=$(get_var EMAIL_SMTP_USERNAME)
    EMAIL_SMTP_PASSWORD=$(get_var EMAIL_SMTP_PASSWORD)
    VONAGE_API_KEY=$(get_var VONAGE_API_KEY)
    VONAGE_API_SECRET=$(get_var VONAGE_API_SECRET)
    STRIPE_SECRET_KEY=$(get_var STRIPE_SECRET_KEY)
    STRIPE_PUBLISHABLE_KEY=$(get_var STRIPE_PUBLISHABLE_KEY)
    STRIPE_CONNECT_RETURN_URL=$(get_var STRIPE_CONNECT_RETURN_URL)
    OLLAMA_BASE_URL=$(get_var OLLAMA_BASE_URL)
    OPENAI_API_KEY=$(get_var OPENAI_API_KEY)
    REGISTRY=$(get_var REGISTRY)
    REPO_NAME=$(get_var REPO_NAME)
  fi
  [ -z "$MYSQL_ROOT_PASSWORD" ] && MYSQL_ROOT_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)
  [ -z "$JWT_SECRET" ] && JWT_SECRET=$(openssl rand -base64 48 | tr -d "=+/" | cut -c1-48)
  [ -z "$RABBITMQ_PASSWORD" ] && RABBITMQ_PASSWORD=$(openssl rand -base64 24 | tr -d "=+/" | cut -c1-24)
  # APP_BASE_URL = public URL (DROPLET_IP or domain); port 80/443 used by Nginx
  # APP_BASE_URL: staging defaults to domain (https://www.caseflowstage.store), prod/default to IP; override in secrets.env
  if [ -z "$APP_BASE_URL" ]; then
    if [ "$ENV_ARG" = "staging" ]; then
      APP_BASE_URL="${STAGING_BASE_URL:-https://www.caseflowstage.store}"
    else
      APP_BASE_URL="${PRODUCTION_BASE_URL:-http://$DROPLET_IP}"
    fi
  fi
  APP_BASE_URL="${APP_BASE_URL%/}"
  # STRIPE_CONNECT_RETURN_URL = where Stripe redirects after vendor onboarding; default = APP_BASE_URL
  [ -z "$STRIPE_CONNECT_RETURN_URL" ] && STRIPE_CONNECT_RETURN_URL="$APP_BASE_URL"

  # Defaults from appsettings (staging); override via secrets.env or existing .env
  RABBITMQ_USER="${RABBITMQ_USER:-admin}"
  JWT_ISSUER="${JWT_ISSUER:-TraditionalEats}"
  JWT_AUDIENCE="${JWT_AUDIENCE:-TraditionalEats}"
  EMAIL_ENABLED="${EMAIL_ENABLED:-true}"
  EMAIL_PROVIDER="${EMAIL_PROVIDER:-Mailgun}"
  EMAIL_MAILGUN_API_KEY="${EMAIL_MAILGUN_API_KEY:-}"
  EMAIL_MAILGUN_DOMAIN="${EMAIL_MAILGUN_DOMAIN:-}"
  EMAIL_FROM_EMAIL="${EMAIL_FROM_EMAIL:-noreply@traditionaleats.com}"
  EMAIL_FROM_NAME="${EMAIL_FROM_NAME:-TraditionalEats}"
  EMAIL_SMTP_HOST="${EMAIL_SMTP_HOST:-smtp.gmail.com}"
  EMAIL_SMTP_PORT="${EMAIL_SMTP_PORT:-587}"
  EMAIL_SMTP_USERNAME="${EMAIL_SMTP_USERNAME:-}"
  EMAIL_SMTP_PASSWORD="${EMAIL_SMTP_PASSWORD:-}"
  EMAIL_ENABLE_SSL="${EMAIL_ENABLE_SSL:-true}"
  VONAGE_ENABLED="${VONAGE_ENABLED:-false}"
  VONAGE_API_KEY="${VONAGE_API_KEY:-}"
  VONAGE_API_SECRET="${VONAGE_API_SECRET:-}"
  VONAGE_FROM_NUMBER="${VONAGE_FROM_NUMBER:-}"
  STRIPE_SECRET_KEY="${STRIPE_SECRET_KEY:-}"
  STRIPE_WEBHOOK_SECRET="${STRIPE_WEBHOOK_SECRET:-}"
  STRIPE_PUBLISHABLE_KEY="${STRIPE_PUBLISHABLE_KEY:-}"
  STRIPE_CONNECT_RETURN_URL="${STRIPE_CONNECT_RETURN_URL:-$APP_BASE_URL}"
  OLLAMA_BASE_URL="${OLLAMA_BASE_URL:-http://ollama:11434}"
  OPENAI_API_KEY="${OPENAI_API_KEY:-}"
  # Single repo (DOCR 1-repo limit): registry.digitalocean.com/cha-registry/traditionaleats:service-name
  REGISTRY="${REGISTRY:-registry.digitalocean.com/cha-registry}"
  REPO_NAME="${REPO_NAME:-traditionaleats}"

  # Require DOCR token when using DigitalOcean registry (otherwise pull gets 401)
  if echo "$REGISTRY" | grep -q registry.digitalocean.com; then
    if [ -z "$DIGITALOCEAN_ACCESS_TOKEN" ]; then
      echo -e "${RED}Error: REGISTRY is DigitalOcean but DIGITALOCEAN_ACCESS_TOKEN is not set.${NC}"
      echo "Add DIGITALOCEAN_ACCESS_TOKEN to deploy/digitalocean/secrets.env (create from secrets.env.example)."
      echo "Get a token: DigitalOcean Control Panel → API → Tokens/Keys."
      exit 1
    fi
  fi

  # Staging vs production: set ASPNETCORE_ENVIRONMENT
  if [ "$ENV_ARG" = "staging" ]; then
    ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Staging}"
  else
    ASPNETCORE_ENVIRONMENT="${ASPNETCORE_ENVIRONMENT:-Production}"
  fi

  # HTTPS: staging = HTTP + HTTPS, production = HTTPS only. DOMAIN required for HTTPS (Let's Encrypt).
  if [ "$ENV_ARG" = "production" ]; then
    HTTPS_ONLY="true"
    DOMAIN="${PRODUCTION_DOMAIN:-}"
    [ -z "$DOMAIN" ] && [[ "$APP_BASE_URL" =~ ^https:// ]] && DOMAIN=$(echo "$APP_BASE_URL" | sed -e 's|https\?://||' -e 's|/.*||' -e 's|:.*||')
  else
    HTTPS_ONLY="false"
    DOMAIN="${STAGING_DOMAIN:-}"
    [ -z "$DOMAIN" ] && [[ "$APP_BASE_URL" =~ ^https:// ]] && DOMAIN=$(echo "$APP_BASE_URL" | sed -e 's|https\?://||' -e 's|/.*||' -e 's|:.*||')
  fi
  # DOMAIN must be a real hostname (has a dot); "http"/"https" from bad APP_BASE_URL would break nginx
  if [ "$DOMAIN" = "http" ] || [ "$DOMAIN" = "https" ] || [ -z "$DOMAIN" ] || [[ "$DOMAIN" != *.* ]]; then
    DOMAIN=""
  fi

  ENV_CONTENT="# TraditionalEats – generated by deploy.sh ($ENV_ARG) – from appsettings + secrets.env
ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT
APP_BASE_URL=$APP_BASE_URL
MYSQL_ROOT_PASSWORD=$MYSQL_ROOT_PASSWORD
RABBITMQ_USER=$RABBITMQ_USER
RABBITMQ_PASSWORD=$RABBITMQ_PASSWORD
JWT_SECRET=$JWT_SECRET
JWT_ISSUER=$JWT_ISSUER
JWT_AUDIENCE=$JWT_AUDIENCE
EMAIL_ENABLED=$EMAIL_ENABLED
EMAIL_PROVIDER=$EMAIL_PROVIDER
EMAIL_MAILGUN_API_KEY=$EMAIL_MAILGUN_API_KEY
EMAIL_MAILGUN_DOMAIN=$EMAIL_MAILGUN_DOMAIN
EMAIL_FROM_EMAIL=$EMAIL_FROM_EMAIL
EMAIL_FROM_NAME=$EMAIL_FROM_NAME
EMAIL_SMTP_HOST=$EMAIL_SMTP_HOST
EMAIL_SMTP_PORT=$EMAIL_SMTP_PORT
EMAIL_SMTP_USERNAME=$EMAIL_SMTP_USERNAME
EMAIL_SMTP_PASSWORD=$EMAIL_SMTP_PASSWORD
EMAIL_ENABLE_SSL=$EMAIL_ENABLE_SSL
VONAGE_ENABLED=$VONAGE_ENABLED
VONAGE_API_KEY=$VONAGE_API_KEY
VONAGE_API_SECRET=$VONAGE_API_SECRET
VONAGE_FROM_NUMBER=$VONAGE_FROM_NUMBER
STRIPE_SECRET_KEY=$STRIPE_SECRET_KEY
STRIPE_WEBHOOK_SECRET=$STRIPE_WEBHOOK_SECRET
STRIPE_PUBLISHABLE_KEY=$STRIPE_PUBLISHABLE_KEY
STRIPE_CONNECT_RETURN_URL=$STRIPE_CONNECT_RETURN_URL
OLLAMA_BASE_URL=$OLLAMA_BASE_URL
OPENAI_API_KEY=$OPENAI_API_KEY
REGISTRY=$REGISTRY
REPO_NAME=$REPO_NAME
DOMAIN=$DOMAIN
HTTPS_ONLY=$HTTPS_ONLY
"
  echo "$ENV_CONTENT" | $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "mkdir -p /opt/traditionaleats && cat > /opt/traditionaleats/.env && chmod 600 /opt/traditionaleats/.env && echo '.env created'"
  echo -e "${GREEN}.env ready on server${NC}"
  echo ""

  # Step 3: Copy compose, nginx (generated), scripts
  echo -e "${GREEN}Step 3: Copying files to server${NC}"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "mkdir -p /opt/traditionaleats/nginx /opt/traditionaleats/scripts"
  $SCP_CMD "$COMPOSE_FILE" "$DROPLET_USER@$DROPLET_IP:/opt/traditionaleats/docker-compose.prod.yml"
  DOMAIN="$DOMAIN" HTTPS_ONLY="$HTTPS_ONLY" bash "$SCRIPT_DIR/scripts/generate-nginx-conf.sh" > "$SCRIPT_DIR/nginx/nginx.generated.conf" 2>/dev/null || true
  if [ -f "$SCRIPT_DIR/nginx/nginx.generated.conf" ] && [ -s "$SCRIPT_DIR/nginx/nginx.generated.conf" ]; then
    $SCP_CMD "$SCRIPT_DIR/nginx/nginx.generated.conf" "$DROPLET_USER@$DROPLET_IP:/opt/traditionaleats/nginx/nginx.conf"
  else
    $SCP_CMD "$SCRIPT_DIR/nginx/nginx.conf" "$DROPLET_USER@$DROPLET_IP:/opt/traditionaleats/nginx/nginx.conf"
  fi
  $SCP_CMD "$SCRIPT_DIR/scripts/mysql-init.sh" "$DROPLET_USER@$DROPLET_IP:/opt/traditionaleats/scripts/mysql-init.sh"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "chmod +x /opt/traditionaleats/scripts/mysql-init.sh"
  echo -e "${GREEN}Files copied${NC}"
  echo ""

  # Step 3b: Log Droplet into DigitalOcean Container Registry (required for pull; we already checked token is set)
  if echo "$REGISTRY" | grep -q registry.digitalocean.com; then
    echo -e "${GREEN}Logging Droplet into DOCR...${NC}"
    if ! echo "$DIGITALOCEAN_ACCESS_TOKEN" | $SSH_CMD "$DROPLET_USER@$DROPLET_IP" 'read -r token; echo "$token" | docker login registry.digitalocean.com -u "$token" --password-stdin'; then
      echo -e "${RED}DOCR login failed. Check DIGITALOCEAN_ACCESS_TOKEN in secrets.env and registry access.${NC}"
      exit 1
    fi
    echo -e "${GREEN}DOCR login OK${NC}"
    echo ""
  fi

  # Step 4: Pull and start containers
  echo -e "${GREEN}Step 4: Starting containers${NC}"
  $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "cd /opt/traditionaleats && docker compose -f docker-compose.prod.yml --env-file .env pull && docker compose -f docker-compose.prod.yml --env-file .env up -d"
  echo ""

  # Step 5: MySQL init (create DBs)
  echo -e "${GREEN}Step 5: Creating databases${NC}"
  sleep 10
  if $SSH_CMD "$DROPLET_USER@$DROPLET_IP" "cd /opt/traditionaleats && ./scripts/mysql-init.sh" 2>/dev/null; then
    echo -e "${GREEN}Databases ready${NC}"
  else
    echo -e "${YELLOW}If a service fails to start, on the server run: cd /opt/traditionaleats && ./scripts/mysql-init.sh${NC}"
  fi

  echo ""
  echo -e "${GREEN}Done. App: $APP_BASE_URL (ports 80/443). Allow firewall 80, 443, 22.${NC}"
}

# Parse first argument
case "${1:-}" in
  staging)   cmd_droplet staging ;;
  production) cmd_droplet production ;;
  build)     cmd_build ;;
  build-on-server) cmd_build_on_server default ;;
  app-platform) cmd_app_platform ;;
  "")        cmd_droplet default ;;
  *)         usage ;;
esac
