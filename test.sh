#!/usr/bin/env bash
# Manueller Smoke-Test für den laufenden MCP-Server (init -> tools/list -> get_random_number).
set -euo pipefail

HOST="${1:-http://localhost:13001}"

extract_session_id() {
  grep -i '^Mcp-Session-Id:' | tr -d '\r' | awk '{print $2}'
}

extract_body() {
  grep '^data:' | sed 's/^data: //'
}

echo "==> initialize"
INIT_RESPONSE=$(curl -sS -i -X POST "$HOST/" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"test.sh","version":"1.0"}}}')

SESSION_ID=$(echo "$INIT_RESPONSE" | extract_session_id)
if [ -z "$SESSION_ID" ]; then
  echo "Keine Mcp-Session-Id erhalten. Antwort:"
  echo "$INIT_RESPONSE"
  exit 1
fi
echo "$INIT_RESPONSE" | extract_body
echo "Session-Id: $SESSION_ID"

echo
echo "==> notifications/initialized"
curl -sS -X POST "$HOST/" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -H "Mcp-Session-Id: $SESSION_ID" \
  -d '{"jsonrpc":"2.0","method":"notifications/initialized"}'

echo
echo "==> tools/list"
curl -sS -X POST "$HOST/" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -H "Mcp-Session-Id: $SESSION_ID" \
  -d '{"jsonrpc":"2.0","id":2,"method":"tools/list"}' | extract_body

echo
echo "==> tools/call get_random_number (min=1, max=50)"
curl -sS -X POST "$HOST/" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json, text/event-stream" \
  -H "Mcp-Session-Id: $SESSION_ID" \
  -d '{"jsonrpc":"2.0","id":3,"method":"tools/call","params":{"name":"get_random_number","arguments":{"min":1,"max":50}}}' | extract_body
