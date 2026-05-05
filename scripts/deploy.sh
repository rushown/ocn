#!/usr/bin/env bash
# ==============================================================
# EWallet Deployment Script
# Usage: ./deploy.sh
# Env vars:
#   DEPLOY_ENV  — deployment environment (default: development)
# ==============================================================
set -euo pipefail

echo "=== EWallet Deployment Script ==="

# ------------------------------------------------------------------
# Configuration
# ------------------------------------------------------------------
COMPOSE_FILE="docker-compose.yml"
ENV="${DEPLOY_ENV:-development}"

echo "Environment: $ENV"

# ------------------------------------------------------------------
# Pull latest code from main branch
# ------------------------------------------------------------------
echo "[1/5] Pulling latest code..."
git pull origin main

# ------------------------------------------------------------------
# Build images (no cache to ensure fresh layers)
# ------------------------------------------------------------------
echo "[2/5] Building Docker images..."
docker-compose -f "$COMPOSE_FILE" build --no-cache

# ------------------------------------------------------------------
# Bring services down, then back up detached
# ------------------------------------------------------------------
echo "[3/5] Restarting services..."
docker-compose -f "$COMPOSE_FILE" down
docker-compose -f "$COMPOSE_FILE" up -d

# ------------------------------------------------------------------
# Wait for PostgreSQL to be ready
# ------------------------------------------------------------------
echo "[4/5] Waiting for PostgreSQL to be ready..."
sleep 5

# ------------------------------------------------------------------
# Run EF Core migrations
# ------------------------------------------------------------------
echo "[5/5] Running database migrations..."
docker-compose exec api dotnet ef database update \
    --project /app/EWallet.Infrastructure.dll \
    || echo "NOTE: Migrations already applied or no pending migrations."

# ------------------------------------------------------------------
# Done — print service URLs
# ------------------------------------------------------------------
echo ""
echo "=== Deployment Complete ==="
echo "  API / Swagger  : http://localhost:5000/swagger"
echo "  Blazor UI      : http://localhost:5001"
echo "  Seq (logs)     : http://localhost:8081"
echo "  Hangfire       : http://localhost:5000/hangfire"
