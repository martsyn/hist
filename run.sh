#!/bin/bash
# Runs the full production container (ClickHouse + app combined).
# Data is persisted in the hist-ch-data named volume, shared with dev.sh.
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

docker stop hist 2>/dev/null || true

docker run -d --name hist --rm \
  -p 8088:8088 \
  -v hist-ch-data:/var/lib/clickhouse \
  --env-file "$SCRIPT_DIR/.env" \
  hist

echo "Running at http://localhost:8088"
