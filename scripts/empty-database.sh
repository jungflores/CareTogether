#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(cd -- "$SCRIPT_DIR/.." && pwd)"
DATABASE_NAME="${DATABASE_NAME:-CareTogether}"
DATABASE_PASSWORD="${DB_PASSWORD:-CareTogether!2026}"

usage() {
  cat <<'EOF'
Usage: ./scripts/empty-database.sh --confirm

Deletes every application row from the CareTogether database while preserving:
  - the database itself
  - tables, indexes, and other schema objects
  - Entity Framework migration history

The API and background worker are stopped before deletion and remain stopped.
This action cannot be undone unless you have a database backup.
EOF
}

if [[ "${1:-}" != "--confirm" || $# -ne 1 ]]; then
  usage
  exit 2
fi

if ! command -v docker >/dev/null 2>&1; then
  echo "Error: Docker is not installed or is not available in PATH." >&2
  exit 1
fi

cd "$PROJECT_DIR"

echo "Stopping services that can write to the database..."
docker compose stop api worker >/dev/null

echo "Starting SQL Server if necessary..."
docker compose up -d sqlserver >/dev/null

echo "Waiting for SQL Server..."
database_ready=false
for _attempt in {1..30}; do
  if docker compose exec -T sqlserver \
    /opt/mssql-tools18/bin/sqlcmd \
    -S localhost -U sa -P "$DATABASE_PASSWORD" -C \
    -d "$DATABASE_NAME" -Q "SELECT 1" >/dev/null 2>&1; then
    database_ready=true
    break
  fi
  sleep 2
done

if [[ "$database_ready" != true ]]; then
  echo "Error: SQL Server or database '$DATABASE_NAME' was not ready after 60 seconds." >&2
  exit 1
fi

echo "Deleting all application data from '$DATABASE_NAME'..."
docker compose exec -T sqlserver \
  /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$DATABASE_PASSWORD" -C \
  -d "$DATABASE_NAME" -b <<'SQL'
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET QUOTED_IDENTIFIER ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRANSACTION;

DECLARE @statement nvarchar(max) = N'';

SELECT @statement +=
    N'ALTER TABLE ' + QUOTENAME(schema_name(schema_id)) + N'.' + QUOTENAME(name) +
    N' NOCHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables
WHERE name <> N'__EFMigrationsHistory';

EXEC sys.sp_executesql @statement;

SET @statement = N'';

SELECT @statement +=
    N'DELETE FROM ' + QUOTENAME(schema_name(schema_id)) + N'.' + QUOTENAME(name) +
    N';' + CHAR(10)
FROM sys.tables
WHERE name <> N'__EFMigrationsHistory';

EXEC sys.sp_executesql @statement;

SET @statement = N'';

SELECT @statement +=
    N'ALTER TABLE ' + QUOTENAME(schema_name(schema_id)) + N'.' + QUOTENAME(name) +
    N' WITH CHECK CHECK CONSTRAINT ALL;' + CHAR(10)
FROM sys.tables
WHERE name <> N'__EFMigrationsHistory';

EXEC sys.sp_executesql @statement;

COMMIT TRANSACTION;
SQL

echo "Database emptied successfully. Schema and migration history were preserved."
echo "The API and worker are still stopped. Restart them with:"
echo "  docker compose up -d api worker"
