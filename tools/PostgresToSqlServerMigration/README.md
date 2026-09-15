# Herramienta de migración PostgreSQL → SQL Server

Este es el único componente que mantiene una referencia a `Npgsql`.

La herramienta nunca modifica la base de datos PostgreSQL de origen. Para un destino SQL Server ya inicializado, el reemplazo de datos requiere explícitamente `--replace-target-data`: deshabilita constraints y triggers dentro de una transacción, limpia solamente las tablas funcionales comunes, las repuebla en orden de dependencias calculado desde las FK y restaura y valida constraints y triggers antes del commit. `__EFMigrationsHistory` nunca se copia ni se limpia.

La opción `--validate-only` realiza validaciones de paridad e integridad sobre un destino ya migrado, sin modificar sus datos.

## Requisitos previos

1. Crear la base de datos de destino utilizando la baseline de SQL Server del CMMS.
2. Verificar que `20260830050450_SqlServerBaseline` aparezca en `dbo.__EFMigrationsHistory`.
3. Crear y verificar un backup recuperable de SQL Server antes de ejecutar un reemplazo de datos.

## Ejecución de prueba

La ejecución de prueba valida únicamente el esquema, los conteos de registros y los checksums de la fuente, sin realizar modificaciones:

```powershell
$env:CMMS_POSTGRESQL_SOURCE = '<cadena de conexión PostgreSQL>'
$env:CMMS_SQLSERVER_DESTINATION = '<cadena de conexión SQL Server>'

dotnet run --project tools/PostgresToSqlServerMigration -- --dry-run --report reports/cmms-dry-run.json
```

## Ejecutar la migración

```powershell
dotnet run --project tools/PostgresToSqlServerMigration -- --report reports/cmms-migration.json
```

Para copiar a un destino ya inicializado y repetir la carga sin duplicados:

```text
--replace-target-data
```

La operación es transaccional y debe ejecutarse por separado para `cmms` y `cmms_pilot`.

Para repetir únicamente las validaciones después de completar una transferencia, agregar la opción:

```text
--validate-only
```

El reporte JSON generado contiene:

* conteos de registros por tabla;
* checksums normalizados y determinísticos;
* validación de restricciones;
* validación de ausencia de solapamientos;
* validación de componentes críticos;
* validación de `rowversion`.

El campo `row_version` no se copia de forma intencional durante la migración.

Las secuencias de SQL Server se reajustan utilizando el sufijo numérico máximo existente en las columnas correspondientes a números de negocio.
