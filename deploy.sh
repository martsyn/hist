#!/bin/bash
# Builds and deploys to the Proxmox LXC container.
# Usage: ./deploy.sh [proxmox-host] [lxc-id]
#   proxmox-host  SSH target for the Proxmox node (default: root@10.0.1.3)
#   lxc-id        LXC container ID (default: 201)
set -e

PROXMOX_HOST="${1:-root@10.0.1.3}"
LXC_ID="${2:-201}"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "Building frontend..."
cd "$SCRIPT_DIR/frontend"
npm run build --silent

echo "Publishing .NET app..."
cd "$SCRIPT_DIR"
dotnet publish src/Hist.Server/Hist.Server.csproj -c Release -o /tmp/hist-publish -nologo -v quiet

cp -r frontend/dist /tmp/hist-publish/wwwroot

echo "Packaging..."
tar czf /tmp/hist-app.tar.gz -C /tmp/hist-publish .
rm -rf /tmp/hist-publish

echo "Uploading to $PROXMOX_HOST (LXC $LXC_ID)..."
scp -q /tmp/hist-app.tar.gz "$PROXMOX_HOST":/tmp/hist-app.tar.gz
rm /tmp/hist-app.tar.gz

ssh "$PROXMOX_HOST" "
  pct push $LXC_ID /tmp/hist-app.tar.gz /tmp/hist-app.tar.gz
  rm /tmp/hist-app.tar.gz
  pct exec $LXC_ID -- bash -c '
    tar xzf /tmp/hist-app.tar.gz -C /app/hist
    rm /tmp/hist-app.tar.gz
    systemctl restart hist
  '
"

echo "Done. Running at http://\$(ssh $PROXMOX_HOST pct exec $LXC_ID -- hostname -I | awk \"{print \\\$1}\"):8088"
