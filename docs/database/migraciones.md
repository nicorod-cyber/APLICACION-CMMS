# Migraciones PostgreSQL

## Migracion inicial

Archivo:

```text
backend/src/MaintenanceCMMS.Infrastructure/Data/PostgreSql/Migrations/202607090001_InitialPostgreSqlSchema.cs
```

Incluye identidad, auditoria, faenas, familias, estados operacionales, activos y documentos compartidos.

## Aplicar migraciones

Con Docker:

```bash
docker compose up --build
```

La API ejecuta `Database.MigrateAsync` al iniciar cuando `DataProvider__Provider=PostgreSql`.

Con CLI local:

```bash
dotnet ef database update --project backend/src/MaintenanceCMMS.Infrastructure --startup-project backend/src/MaintenanceCMMS.Api
```

## Crear nueva migracion

```bash
dotnet ef migrations add NombreMigracion --project backend/src/MaintenanceCMMS.Infrastructure --startup-project backend/src/MaintenanceCMMS.Api --output-dir Data/PostgreSql/Migrations
```

Antes de aceptar una migracion, revisar que no contenga acciones de borrado en cascada.
