#!/bin/bash
# One-time setup of the Proxmox LXC container for hist.
# Run this on the Proxmox host (or via SSH) before the first deploy.
# Usage: ./setup-lxc.sh [proxmox-host] [lxc-id]
#
# Prerequisites on the Proxmox host:
#   - Debian 12 LXC template downloaded:
#       pveam download local debian-12-standard_12.12-1_amd64.tar.zst
#   - .env file present next to this script
set -e

PROXMOX_HOST="${1:-root@10.0.1.3}"
LXC_ID="${2:-201}"
LXC_HOSTNAME="hist"
CH_DATA_DIR="/var/lib/hist-clickhouse"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "=== Creating LXC container $LXC_ID on $PROXMOX_HOST ==="

ssh "$PROXMOX_HOST" "
  mkdir -p $CH_DATA_DIR
  pct create $LXC_ID local:vztmpl/debian-12-standard_12.12-1_amd64.tar.zst \
    --hostname $LXC_HOSTNAME \
    --memory 3072 \
    --swap 512 \
    --cores 2 \
    --rootfs local-lvm:16 \
    --net0 name=eth0,bridge=vmbr0,ip=dhcp \
    --mp0 $CH_DATA_DIR,mp=/var/lib/clickhouse \
    --unprivileged 0 \
    --features nesting=1 \
    --start 1
  sleep 3
  pct status $LXC_ID
"

echo "=== Installing packages ==="
ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- bash -c '
  apt-get update -qq
  apt-get install -y -q curl wget xz-utils
'"

echo "=== Installing ClickHouse ==="
ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- bash -c '
  curl -fsSL https://clickhouse.com/ | sh
  ./clickhouse install --noninteractive
  # Replace legacy /usr/sbin binary left by any Debian package with the new one
  ln -sf /usr/bin/clickhouse /usr/sbin/clickhouse-server
  rm -f clickhouse
'"

echo "=== Copying ClickHouse config ==="
for f in custom.xml; do
  ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- mkdir -p /etc/clickhouse-server/config.d"
  ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- bash -c 'cat > /etc/clickhouse-server/config.d/$f'" \
    < "$SCRIPT_DIR/clickhouse/config.d/$f"
done
for f in writer.xml reader.xml; do
  ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- mkdir -p /etc/clickhouse-server/users.d"
  ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- bash -c 'cat > /etc/clickhouse-server/users.d/$f'" \
    < "$SCRIPT_DIR/clickhouse/users.d/$f"
done

echo "=== Starting ClickHouse ==="
ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- bash -c '
  chown -R clickhouse:clickhouse /var/lib/clickhouse
  systemctl enable clickhouse-server
  systemctl start clickhouse-server
  sleep 4
  systemctl is-active clickhouse-server
'"

echo "=== Installing .NET 10 ASP.NET Core runtime ==="
ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- bash -c '
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  chmod +x /tmp/dotnet-install.sh
  /tmp/dotnet-install.sh --runtime aspnetcore --channel 10.0 --install-dir /usr/share/dotnet
  rm /tmp/dotnet-install.sh
  ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
'"

echo "=== Creating app directory and systemd service ==="
ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- bash -c 'mkdir -p /app/hist'"

# Copy .env
if [ -f "$SCRIPT_DIR/.env" ]; then
  ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- bash -c 'cat > /app/hist/.env'" \
    < "$SCRIPT_DIR/.env"
else
  echo "WARNING: no .env found — copy it manually to /app/hist/.env inside the container"
fi

ssh "$PROXMOX_HOST" "pct exec $LXC_ID -- bash -c '
cat > /etc/systemd/system/hist.service << EOF
[Unit]
Description=Hist financial data server
After=clickhouse-server.service
Requires=clickhouse-server.service

[Service]
Type=simple
User=root
WorkingDirectory=/app/hist
EnvironmentFile=/app/hist/.env
ExecStart=/usr/share/dotnet/dotnet /app/hist/Hist.Server.dll
Restart=on-failure
RestartSec=5

[Install]
WantedBy=multi-user.target
EOF
systemctl daemon-reload
systemctl enable hist
'"

echo ""
echo "=== Setup complete. Run ./deploy.sh to build and deploy the app. ==="
echo "    ClickHouse data: $CH_DATA_DIR (on Proxmox host, bind-mounted into LXC)"
