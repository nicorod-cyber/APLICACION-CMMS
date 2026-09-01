# Herramienta de migración PostgreSQL → SQL Server

Este es el único componente que mantiene una referencia a `Npgsql`.

La herramienta nunca modifica la base de datos PostgreSQL de origen y se niega a escribir sobre una base de datos SQL Server que ya contenga datos, salvo que se proporcione explícitamente la opción `--allow-nonempty-target`.

La opción `--validate-only` realiza validaciones de paridad e integridad sobre un destino ya migrado, sin modificar sus datos.

## Requisitos previos

1. Crear la base de datos de destino utilizando la baseline de SQL Server del CMMS.
2. Verificar que `20260830050450_SqlServerBaseline` aparezca en `dbo.__EFMigrationsHistory`.
3. Crear un respaldo de la base PostgreSQL de origen antes de ejecutar una migración real.

## Ejecución de prueba

La ejecución de prueba valida únicamente el esquema, los conteos de registros y los checksums de la fuente, sin realizar modificaciones:

```powershell
$env:CMMS_POSTGRESQL_SOURCE = '<cadena de conexión PostgreSQL>'
$env:CMMS_SQLSERVER_DESTINATION = '<cadena de conexión SQL Server>'

dotnet run --project tools/PostgresToSqlServerMigration -- --dry-run --report reports/cmms-dry-run.json