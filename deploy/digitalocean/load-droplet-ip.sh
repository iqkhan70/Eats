#!/usr/bin/env bash
# Load DROPLET_IP from file (same pattern as mental health app).
# Usage: source load-droplet-ip.sh [staging|production|default]
#   staging   – use DROPLET_IP_STAGING file
#   production – use DROPLET_IP_PRODUCTION file
#   default   – use DROPLET_IP file (single environment)
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENVIRONMENT="${1:-default}"

case "$ENVIRONMENT" in
  staging)
    DROPLET_IP_FILE="${SCRIPT_DIR}/DROPLET_IP_STAGING"
    ;;
  production)
    DROPLET_IP_FILE="${SCRIPT_DIR}/DROPLET_IP_PRODUCTION"
    ;;
  default|"")
    DROPLET_IP_FILE="${SCRIPT_DIR}/DROPLET_IP"
    ;;
  *)
    echo "Error: Unknown environment '$ENVIRONMENT'. Use: staging | production | default" >&2
    exit 1
    ;;
esac

if [ -f "$DROPLET_IP_FILE" ]; then
  DROPLET_IP=$(cat "$DROPLET_IP_FILE" | tr -d '[:space:]')
  if [ -z "$DROPLET_IP" ] || [ "$DROPLET_IP" = "YOUR_DROPLET_IP" ]; then
    echo "Error: Put your Droplet IP in $DROPLET_IP_FILE (replace YOUR_DROPLET_IP or leave only the IP)." >&2
    exit 1
  fi
  export DROPLET_IP
  export SERVER_IP="$DROPLET_IP"
else
  echo "Error: Droplet IP file not found at $DROPLET_IP_FILE" >&2
  echo "Create it with your Droplet IP, e.g.: echo '1.2.3.4' > $DROPLET_IP_FILE" >&2
  exit 1
fi
