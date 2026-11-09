#!/bin/sh
# Runs at container start (the official nginx image executes /docker-entrypoint.d/*.sh).
# Writes the backend URL the *browser* should call, so it can change without rebuilding the image:
#   API_BASE_URL=http://localhost:9090 docker compose up -d frontend
set -eu

API_BASE_URL="${API_BASE_URL:-http://localhost:8080}"

# Escape backslashes and double quotes so the value is always a valid JS string literal.
escaped=$(printf '%s' "$API_BASE_URL" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g')

cat > /usr/share/nginx/html/config.js <<EOF
window.__APP_CONFIG__ = { API_BASE_URL: "${escaped}" };
EOF

echo "runtime-config: API_BASE_URL=${API_BASE_URL}"
