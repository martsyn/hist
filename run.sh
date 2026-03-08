#!/bin/bash
# Runs the full production container (ClickHouse + app combined).
# CH data is persisted in CH_DATA_DIR (default: /var/lib/hist-clickhouse).
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
CH_DATA_DIR="${CH_DATA_DIR:-/var/lib/hist-clickhouse}"

docker stop hist 2>/dev/null || true

docker run -d --name hist --rm \
  -p 8088:8088 \
  -v "$CH_DATA_DIR:/var/lib/clickhouse" \
  --env-file "$SCRIPT_DIR/.env" \
  hist

echo "Running at http://localhost:8088"
