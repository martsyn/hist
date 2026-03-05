#!/bin/bash
# Starts ClickHouse in Docker and runs the .NET app natively for fast iteration.
# Set TIINGO_TOKEN in your environment or .env before running.
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

# Load .env if present
if [ -f "$SCRIPT_DIR/.env" ]; then
  while IFS='=' read -r key value; do
    [[ -z "$key" || "$key" == \#* ]] && continue
    export "$key=$value"
  done < "$SCRIPT_DIR/.env"
fi

# Start ClickHouse if not already running
if ! docker ps --format '{{.Names}}' | grep -q '^hist-ch$'; then
  echo "Starting ClickHouse..."
  docker run -d --name hist-ch --rm \
    -p 9000:9000 -p 8123:8123 \
    -v "$SCRIPT_DIR/clickhouse/config.d:/etc/clickhouse-server/config.d:ro" \
    -v "$SCRIPT_DIR/clickhouse/users.d:/etc/clickhouse-server/users.d:ro" \
    clickhouse/clickhouse-server:24
  echo "Waiting for ClickHouse..."
  until docker exec hist-ch clickhouse-client --query "SELECT 1" &>/dev/null; do sleep 1; done
  echo "ClickHouse ready."
else
  echo "ClickHouse already running."
fi

# Run the app with hot reload
cd "$SCRIPT_DIR/src/Hist.Server"
exec dotnet watch run --launch-profile dev
