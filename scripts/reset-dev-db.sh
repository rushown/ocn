#!/usr/bin/env bash
# ==============================================================
# EWallet Dev Database Reset Script
#
# !! DANGER: This script destroys ALL data. !!
# !! It is intentionally blocked in production.  !!
#
# Usage: ./reset-dev-db.sh
# Env vars:
#   DEPLOY_ENV  — must NOT be "production"
# ==============================================================
set -euo pipefail

# ------------------------------------------------------------------
# Safety guard — refuse to run against production
# ------------------------------------------------------------------
if [ "${DEPLOY_ENV:-}" = "production" ]; then
    echo "ERROR: Cannot run reset-dev-db.sh in production." >&2
    exit 1
fi

echo "=== EWallet Dev DB Reset ==="
echo "WARNING: All data will be erased. Press Ctrl-C within 5 seconds to abort."
sleep 5

# ------------------------------------------------------------------
# Tear down all containers and remove named volumes
# ------------------------------------------------------------------
echo "[1/4] Removing containers and volumes..."
docker-compose down -v

# ------------------------------------------------------------------
# Bring up only the infrastructure services
# ------------------------------------------------------------------
echo "[2/4] Starting PostgreSQL and Redis..."
docker-compose up -d postgres redis
sleep 3

# ------------------------------------------------------------------
# Drop and recreate the application database
# ------------------------------------------------------------------
echo "[3/4] Dropping and recreating ewallet_db..."
docker-compose exec postgres psql -U ewallet -c "DROP DATABASE IF EXISTS ewallet_db;"
docker-compose exec postgres psql -U ewallet -c "CREATE DATABASE ewallet_db;"

# ------------------------------------------------------------------
# Done
# ------------------------------------------------------------------
echo ""
echo "[4/4] DB reset complete."
echo "Next steps:"
echo "  1. Run migrations : docker-compose exec api dotnet ef database update"
echo "  2. Seed data      : docker-compose exec postgres psql -U ewallet -d ewallet_db -f /seeds/init-db.sql"
