#!/bin/sh
# Regenerate the runtime config from the API_BASE_URL environment variable.
# The nginx base image runs every executable in /docker-entrypoint.d/ before
# starting the server, so this runs on each container start.
set -e

API_BASE_URL="${API_BASE_URL:-http://localhost:5000}"

cat > /usr/share/nginx/html/env.js <<EOF
window.__ENV__ = {
  API_BASE_URL: "${API_BASE_URL}",
};
EOF

echo "Generated env.js with API_BASE_URL=${API_BASE_URL}"
