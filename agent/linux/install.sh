#!/bin/sh
# Run on the selected, authorized Linux host as root; retains all recordings on upgrades.
set -eu
[ "$(id -u)" = 0 ] || { echo "需要管理员权限安装系统服务" >&2; exit 1; }
source_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
python3 -m py_compile "$source_dir/asterhub_agent.py"
if ! id -u asterhub-agent >/dev/null 2>&1; then
 useradd --system --user-group --home-dir /var/lib/asterhub-agent --shell /usr/sbin/nologin asterhub-agent
fi
install -d -o root -g root -m 0755 /opt/asterhub-agent
install -d -o asterhub-agent -g asterhub-agent -m 0750 /var/lib/asterhub-agent
install -o root -g root -m 0755 "$source_dir/asterhub_agent.py" /opt/asterhub-agent/asterhub_agent.py
install -o root -g root -m 0644 "$source_dir/asterhub-agent.service" /etc/systemd/system/asterhub-agent.service
systemctl daemon-reload
systemctl enable asterhub-agent.service
systemctl restart asterhub-agent.service
