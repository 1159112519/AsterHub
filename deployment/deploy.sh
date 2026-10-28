#!/usr/bin/env bash
set -euo pipefail
[[ "$EUID" -eq 0 ]] || { echo "Run with sudo: sudo bash deploy.sh" >&2; exit 1; }
src="$(cd -- "$(dirname -- "$0")" && pwd)/site"
[[ -f "$src/index.html" && -f "$src/releases/stable.json" && -f "$src/releases/AQServer-Setup-0.2.0.exe" ]] || { echo 'Missing built website/release' >&2; exit 1; }
root='/var/www/aqserver-web'
stamp="$(date -u +%Y%m%dT%H%M%SZ)-$$"
version="$root/versions/$stamp"
install -d -m 0755 "$version"
cp -a -- "$src/." "$version/"
chown -R root:root "$version"
find "$version" -type d -exec chmod 0755 {} +
find "$version" -type f -exec chmod 0644 {} +
if [[ -e "$root/current" && ! -L "$root/current" ]]; then echo "$root/current is not a symlink; review manually" >&2; exit 1; fi
ln -s -- "$version" "$root/current.pending-$$"
mv -Tf -- "$root/current.pending-$$" "$root/current"
echo "Published static site: $version"
echo 'Nginx configuration is not changed by this script. Review nginx.conf, nginx -t, then reload.'
