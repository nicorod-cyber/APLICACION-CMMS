# PostgreSQL runtime and controlled operational reset

PostgreSQL is the only operational source of truth. Excel is accepted only as the input file of an explicit import; import state, rows, errors, events and original-file metadata are persisted in PostgreSQL.

## Local startup

1. Copy `.env.example` to `.env` and replace every `<...>` placeholder with a local secret. In PowerShell, `-join ((48..57)+(65..90)+(97..122) | Get-Random -Count 48 | ForEach-Object {[char]$_})` creates a suitable random value.
2. Start the stack: `docker compose up --build -d`.
3. Verify: `docker compose ps`, `curl http://localhost:5041/api/health`, `curl http://localhost:5041/api/system/database-health`, and open `http://localhost:5041/swagger`.

Docker Compose requires every database, JWT and initial-admin variable. PostgreSQL has a health check and the API waits for it. Do not change a password in `.env` while retaining an initialized `postgres-data` volume: either keep the original credential or perform a separately approved database/volume migration.

## EF Core and integration tests

For local EF tools set `CMMS_POSTGRES_CONNECTION`, then run `dotnet ef database update --project backend/src/MaintenanceCMMS.Infrastructure --startup-project backend/src/MaintenanceCMMS.Api`.

Integration tests use Testcontainers and create isolated databases; they do not require PostgreSQL at `127.0.0.1:5432`. Docker must be available for those tests.

## Legacy operational JSONB data

Before applying the relational migration to a database that still has `conjuntos_datos_operacionales`, make a verifiable backup and run:

```powershell
psql "$env:CMMS_POSTGRES_CONNECTION" -v ON_ERROR_STOP=1 -f backend/scripts/ReportLegacyOperationalDataSets.sql
```

The report classifies codes as reviewed-transformable, test data requiring explicit approval, or unknown. Unknown codes block retirement; no script removes them automatically. The relational migration itself refuses to drop a nonempty legacy table.

## Operational reset

Never run this against production. First make and verify a backup, then run the read-only preview:

```powershell
psql "$env:CMMS_POSTGRES_CONNECTION" -v ON_ERROR_STOP=1 -f backend/scripts/PreviewDevelopmentOperationalReset.sql
```

After reviewing the output, explicitly opt in for the reset:

```powershell
$env:PGOPTIONS = '-c app.cmms_reset_operational_data=true -c app.cmms_backup_confirmed=true'
psql "$env:CMMS_POSTGRES_CONNECTION" -v ON_ERROR_STOP=1 -f backend/scripts/ResetDevelopmentOperationalData.sql
Remove-Item Env:PGOPTIONS
```

The reset uses one transaction, avoids `CASCADE`, removes operational records and leaves identities, roles, permissions, catalogues, faenas, warehouses, spare parts, families and approved templates intact.
