#!/usr/bin/env bash
#
# deploy-prod.sh — ship the Cult of Comfort game server to the shared Hetzner host (same flow as PurePrep).
#
# The server keeps a copy of the source (not a git checkout): this syncs what the image is built from, rebuilds the
# container in place, then checks /health from inside the Docker network (works before DNS exists) and, when the
# public name resolves, through Caddy as well.
#
# Usage:
#   ./deploy/deploy-prod.sh --confirm
#
set -euo pipefail

HOST="${COC_HOST:-coldstart-prod}"
PUBLIC_URL="${COC_PUBLIC_URL:-https://api.cultofcomfort.lechdigital.nl}"
REMOTE_DIR="${COC_REMOTE_DIR:-/opt/cultofcomfort}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

if [ "${1:-}" != "--confirm" ]; then
  echo "This deploys to production ($HOST:$REMOTE_DIR)." >&2
  echo "Re-run with --confirm to proceed." >&2
  exit 2
fi

# ---- Interlock: tests must pass ---------------------------------------------------------------
echo "==> Running tests"
dotnet test "$ROOT/CultOfComfort.slnx" -c Release --nologo -v q >/dev/null || {
  echo "REFUSING TO DEPLOY: tests are failing." >&2; exit 1; }
echo "    Tests green."

# ---- Sync ------------------------------------------------------------------------------------
echo "==> Syncing source to $HOST:$REMOTE_DIR"
ssh "$HOST" "mkdir -p $REMOTE_DIR/src $REMOTE_DIR/deploy"
rsync -az --delete \
  --exclude 'bin/' --exclude 'obj/' \
  "$ROOT/src/CultOfComfort.Simulation" "$ROOT/src/CultOfComfort.Contracts" \
  "$ROOT/src/CultOfComfort.Server.Application" "$ROOT/src/CultOfComfort.Server.Infrastructure" "$ROOT/src/CultOfComfort.Server" \
  "$HOST:$REMOTE_DIR/src/"
rsync -az "$ROOT/Dockerfile" "$ROOT/.dockerignore" "$ROOT/Directory.Build.props" "$ROOT/nuget.config" "$HOST:$REMOTE_DIR/"
rsync -az "$ROOT/deploy/docker-compose.prod.yml" "$HOST:$REMOTE_DIR/deploy/"

# ---- Build & restart -------------------------------------------------------------------------
echo "==> Rebuilding container"
ssh "$HOST" "cd $REMOTE_DIR && docker compose -f deploy/docker-compose.prod.yml up -d --build"

# ---- Verify ----------------------------------------------------------------------------------
# The container only exposes 8080 on the Docker network, so the first check runs a throwaway curl on that network.
echo "==> Waiting for health (inside the Docker network)"
for _ in $(seq 1 30); do
  sleep 2
  if ssh "$HOST" "docker run --rm --network coldstart_default curlimages/curl:8.10.1 -sf -m 5 http://cultofcomfort-api:8080/health" >/dev/null 2>&1; then
    echo "    Healthy inside the network."
    if curl -sf -m 10 "$PUBLIC_URL/health" >/dev/null 2>&1; then
      echo "    Healthy at $PUBLIC_URL: $(curl -s -m 10 "$PUBLIC_URL/health")"
    else
      echo "    $PUBLIC_URL not reachable yet (DNS record or Caddy block missing?)."
    fi
    exit 0
  fi
done

echo "DEPLOY FAILED: the container did not become healthy." >&2
ssh "$HOST" "cd $REMOTE_DIR && docker compose -f deploy/docker-compose.prod.yml logs --tail 30 cultofcomfort-api" >&2
exit 1
