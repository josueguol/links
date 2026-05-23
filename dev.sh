#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PID_FILE="$SCRIPT_DIR/.dev-pids"

cleanup() {
  echo ""
  echo "Shutting down..."
  if [ -f "$PID_FILE" ]; then
    while IFS= read -r pid; do
      kill "$pid" 2>/dev/null || true
    done < "$PID_FILE"
    rm -f "$PID_FILE"
  fi
  echo "✔ Stopped"
  exit 0
}

trap cleanup SIGINT SIGTERM

echo "Starting backend (dotnet run)..."
dotnet run --project "$SCRIPT_DIR/src/Links.Api/Links.Api.csproj" &
API_PID=$!

echo "Starting frontend (vite dev)..."
npm --prefix "$SCRIPT_DIR/frontend" run dev &
FRONTEND_PID=$!

echo "$API_PID" > "$PID_FILE"
echo "$FRONTEND_PID" >> "$PID_FILE"

echo ""
echo "✔ Both running — press Ctrl+C to stop"
echo ""

wait
