#!/usr/bin/env bash
# Generate nginx.conf from DOMAIN, HTTPS_ONLY, CERTS_READY, USE_SELF_SIGNED_CERT (env vars).
# When DOMAIN is set but no certs: use USE_SELF_SIGNED_CERT=1 to enable 443 with self-signed cert (e.g. production before Let's Encrypt).
# After running setup-https.sh, regenerate with CERTS_READY=1 to use Let's Encrypt in the 443 block.
# Staging: HTTPS_ONLY=false → HTTP + HTTPS. Production: HTTPS_ONLY=true → redirect HTTP to HTTPS only.

DOMAIN="${DOMAIN:-}"
HTTPS_ONLY="${HTTPS_ONLY:-false}"
CERTS_READY="${CERTS_READY:-0}"
USE_SELF_SIGNED_CERT="${USE_SELF_SIGNED_CERT:-0}"

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
    upstream payment_service {
        server payment-service:5004;
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
        # BFF routes: case-insensitive on Unix (MobileBff vs mobilebff)
        location ~* ^/api/WebBff/ {
            proxy_pass http://web_bff;
            proxy_http_version 1.1;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
        }
        location ~* ^/api/MobileBff/ {
            proxy_pass http://mobile_bff;
            proxy_http_version 1.1;
            proxy_set_header Host $host;
            proxy_set_header X-Real-IP $remote_addr;
            proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto $scheme;
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
        # SignalR Vendor Chat Hub (ChatService)
        location /vendorChatHub {
            proxy_pass http://chat_service/vendorChatHub;
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
        # Stripe webhooks → payment-service (case-insensitive; raw body preserved for signature verification)
        location ~* ^/api/webhooks/ {
            proxy_pass http://payment_service;
            proxy_http_version 1.1;
            proxy_set_header Host \$host;
            proxy_set_header X-Real-IP \$remote_addr;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;
            proxy_request_buffering off;
            proxy_buffering off;
            client_max_body_size 10m;
        }
        location /health {
            access_log off;
            return 200 "ok\n";
            add_header Content-Type text/plain;
        }
    }
NGINX_80_SERVE
fi

# Server 443: when DOMAIN is set AND (Let's Encrypt certs ready OR self-signed cert enabled).
# USE_SELF_SIGNED_CERT=1: use /etc/nginx/ssl (same as staging); deploy creates these on production so HTTPS works before Let's Encrypt.
if [ -n "$DOMAIN" ] && { [ "${CERTS_READY:-0}" = "1" ] || [ "$USE_SELF_SIGNED_CERT" = "1" ]; }; then
  if [ "${CERTS_READY:-0}" = "1" ]; then
    SSL_CERT="/etc/letsencrypt/live/$DOMAIN/fullchain.pem"
    SSL_KEY="/etc/letsencrypt/live/$DOMAIN/privkey.pem"
  else
    SSL_CERT="/etc/nginx/ssl/cert.pem"
    SSL_KEY="/etc/nginx/ssl/key.pem"
  fi
  cat << NGINX_443
    server {
        listen 443 ssl default_server;
        server_name $DOMAIN _;
        ssl_certificate     $SSL_CERT;
        ssl_certificate_key $SSL_KEY;
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
        # BFF routes: case-insensitive on Unix (MobileBff vs mobilebff)
        location ~* ^/api/WebBff/ {
            proxy_pass http://web_bff;
            proxy_http_version 1.1;
            proxy_set_header Host \$host;
            proxy_set_header X-Real-IP \$remote_addr;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;
        }
        location ~* ^/api/MobileBff/ {
            proxy_pass http://mobile_bff;
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
        # SignalR Vendor Chat Hub (ChatService)
        location /vendorChatHub {
            proxy_pass http://chat_service/vendorChatHub;
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
        # Stripe webhooks → payment-service (case-insensitive; raw body preserved for signature verification)
        location ~* ^/api/webhooks/ {
            proxy_pass http://payment_service;
            proxy_http_version 1.1;
            proxy_set_header Host \$host;
            proxy_set_header X-Real-IP \$remote_addr;
            proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
            proxy_set_header X-Forwarded-Proto \$scheme;
            proxy_request_buffering off;
            proxy_buffering off;
            client_max_body_size 10m;
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
