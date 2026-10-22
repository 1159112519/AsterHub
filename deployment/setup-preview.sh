#!/usr/bin/env bash
# Managed by AQ Server: public website preview only.
set -euo pipefail
version='2026.10.3'
expected='053b80b0a23031a038fdbf3c073f59f82b8f1f152aa67971d7383ffafc0dbab0'
url="https://github.com/cloudflare/cloudflared/releases/download/$version/cloudflared-linux-amd64"
binary='/usr/local/lib/aqserver/cloudflared'
config='/etc/nginx/conf.d/aq-server-preview.conf'
unit='/etc/systemd/system/aqserver-web-preview.service'
for path in "$config" "$unit"; do
 if [[ -e "$path" ]] && ! grep -q 'Managed by AQ Server' "$path"; then echo "Refusing to replace unrelated file: $path" >&2; exit 1; fi
done
install -d -m 0755 /usr/local/lib/aqserver
if [[ ! -f "$binary" ]] || [[ "$(sha256sum "$binary" | cut -d ' ' -f 1)" != "$expected" ]]; then
 temp=$(mktemp /tmp/aqserver-cloudflared.XXXXXX)
 trap 'rm -f -- "$temp"' EXIT
 curl --fail --silent --show-error --location --retry 1 --connect-timeout 10 --max-time 55 "$url" -o "$temp"
 echo "$expected  $temp" | sha256sum -c -
 install -m 0755 "$temp" "$binary"
fi
"$binary" --version
cat > "$config" <<'NGINX'
# Managed by AQ Server: loopback origin exposes only the public website.
server {
    listen 127.0.0.1:18770;
    server_name _;
    access_log /var/log/nginx/aq-server-preview.access.log;
    error_log /var/log/nginx/aq-server-preview.error.log;
    location = / { return 302 /aqserver/; }
    location = /aqserver { return 301 /aqserver/; }
    location = /aqserver/releases/stable.json {
        alias /var/www/aqserver-web/current/releases/stable.json;
        default_type application/json;
        add_header Cache-Control "no-store" always;
        add_header X-Content-Type-Options "nosniff" always;
        limit_except GET { deny all; }
    }
    location ^~ /aqserver/assets/ {
        alias /var/www/aqserver-web/current/assets/;
        expires 1y;
        add_header Cache-Control "public, immutable";
        add_header X-Content-Type-Options "nosniff" always;
        limit_except GET { deny all; }
    }
    location ^~ /aqserver/ {
        alias /var/www/aqserver-web/current/;
        index index.html;
        autoindex off;
        add_header X-Content-Type-Options "nosniff" always;
        limit_except GET { deny all; }
    }
    location / { return 404; }
}
NGINX
nginx -t
systemctl reload nginx
curl --fail --silent --show-error --max-time 5 http://127.0.0.1:18770/aqserver/ -o /dev/null -w 'PREVIEW_ORIGIN_HTTP=%{http_code}\n'
cat > "$unit" <<'UNIT'
# Managed by AQ Server: public website preview only; hostname may change on restart.
[Unit]
Description=AQ Server public HTTPS website preview
After=network-online.target nginx.service
Wants=network-online.target nginx.service
[Service]
Type=simple
ExecStart=/usr/local/lib/aqserver/cloudflared --no-autoupdate tunnel --protocol http2 --edge-ip-version 4 --metrics 127.0.0.1:18771 --url http://127.0.0.1:18770
Restart=on-failure
RestartSec=10
DynamicUser=true
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ProtectHome=true
CapabilityBoundingSet=
MemoryMax=180M
CPUQuota=25%
TasksMax=64
[Install]
WantedBy=multi-user.target
UNIT
systemctl daemon-reload
systemctl enable --now aqserver-web-preview.service
systemctl is-active aqserver-web-preview.service
