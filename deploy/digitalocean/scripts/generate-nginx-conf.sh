#!/usr/bin/env bash
# Generate nginx.conf from DOMAIN, HTTPS_ONLY, and CERTS_READY (env vars).
# When DOMAIN is set but CERTS_READY is not 1, only HTTP (port 80) is generated so Nginx can start without certs.
# After running setup-https.sh, regenerate with CERTS_READY=1 to include the 443 SSL block.
# Staging: HTTPS_ONLY=false → HTTP + HTTPS. Production: HTTPS_ONLY=true → redirect HTTP to HTTPS only.

DOMAIN="${DOMAIN:-}"
HTTPS_ONLY="${HTTPS_ONLY:-false}"
CERTS_READY="${CERTS_READY:-0}"

# Only use DOMAIN for SSL when it looks like a hostname (has a dot); "http"/"https" are invalid
if [ "$DOMAIN" = "http" ] || [ "$DOMAIN" = "https" ] || [ -z "$DOMAIN" ] || [[ "$DOMAIN" != *.* ]]; then
  DOMAIN=""
fi

cat << 'NGINX_HEAD'
# Kram edge – generated (DOMAIN, HTTPS_ONLY)
# Staging: HTTP + HTTPS. Production: HTTPS only (HTTP redirects).

events {
    worker_connections 1024;
}

http {
    include       /etc/nginx/mime.types;
    default_type  application/octet-stream;
    sendfile      on;
    keepalive_timeout 65;

    upstream web_bff {
        server web-bff:5101;
    }
    upstream mobile_bff {
        server mobile-bff:5102;
    }
    upstream chat_service {
        server chat-service:5012;
    }

NGINX_HEAD

# Server 80: redirect to HTTPS when HTTPS_ONLY, else serve app
if [ "$HTTPS_ONLY" = "true" ]; then
  cat << 'NGINX_80_REDIRECT'
    server {
        listen 80 default_server;
        server_name _;
        location /.well-known/acme-challenge/ {
            root /var/www/certbot;
            try_files $uri =404;
        }
        location / {
            return 301 https://$host$request_uri;
        }
    }
NGINX_80_REDIRECT
else
  cat << 'NGINX_80_SERVE'
    server {
        listen 80 default_server;
        server_name _;
        root /usr/share/nginx/html;
        index index.html;
        location /.well-known/acme-challenge/ {
            root /var/www/certbot;
            try_files $uri =404;
        }
        location /_framework/ {
            add_header Cache-Control "no-store, no-cache, must-revalidate";
            try_files \$uri =404;
        }
        location / {
            try_files $uri $uri/ /index.html;
            add_header Cache-Control "no-store, no-cache, must-revalidate";
        }
        location /api/WebBff/ {
            proxy_pass http://web_bff/api/WebBff/;
            proxy_http_version 1.1;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
        location /api/MobileBff/ {
            proxy_pass http://mobile_bff/api/MobileBff/;
            proxy_http_version 1.1;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
        location /health {
            access_log off;
            return 200 "ok\n";
            add_header Content-Type text/plain;
        }
    }
NGINX_80_SERVE
fi

# Server 443: only when DOMAIN is set AND certs exist (CERTS_READY=1).
# Deploy generates HTTP-only so Nginx can start; run setup-https.sh to get certs then regenerate with CERTS_READY=1.
if [ -n "$DOMAIN" ] && [ "${CERTS_READY:-0}" = "1" ]; then
  cat << NGINX_443
    server {
        listen 443 ssl;
        server_name $DOMAIN;
        ssl_certificate     /etc/letsencrypt/live/$DOMAIN/fullchain.pem;
        ssl_certificate_key /etc/letsencrypt/live/$DOMAIN/privkey.pem;
        root /usr/share/nginx/html;
        index index.html;
        location /_framework/ {
            add_header Cache-Control "no-store, no-cache, must-revalidate";
            try_files \$uri =404;
        }
        location / {
            try_files \$uri \$uri/ /index.html;
            add_header Cache-Control "no-store, no-cache, must-revalidate";
        }
        location /api/WebBff/ {
            proxy_pass http://web_bff/api/WebBff/;
            proxy_http_version 1.1;
            proxy_set_header Host \$host;
            proxy_set_header X-Real-IP \$remote_addr;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;
        }
        location /api/MobileBff/ {
            proxy_pass http://mobile_bff/api/MobileBff/;
            proxy_http_version 1.1;
            proxy_set_header Host \$host;
            proxy_set_header X-Real-IP \$remote_addr;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;
        }
        # SignalR Chat Hub (ChatService) - WebSocket/LongPolling support
        location /chatHub {
            proxy_pass http://chat_service/chatHub;
            proxy_http_version 1.1;
            proxy_set_header Upgrade \$http_upgrade;
            proxy_set_header Connection "upgrade";
            proxy_set_header Host \$host;
            proxy_set_header X-Real-IP \$remote_addr;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;
            proxy_read_timeout 86400;
            proxy_send_timeout 86400;
        }
        # ChatService REST API endpoints
        location /api/Chat/ {
            proxy_pass http://chat_service/api/Chat/;
            proxy_http_version 1.1;
            proxy_set_header Host \$host;
            proxy_set_header X-Real-IP \$remote_addr;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;
        }
        location /health {
            access_log off;
            return 200 "ok\n";
            add_header Content-Type text/plain;
        }
    }
NGINX_443
fi

echo "}"
