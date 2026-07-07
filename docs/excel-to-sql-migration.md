# Migracion Excel-first a SQL-ready

## Implementacion Excel actual

La implementacion Excel vive en:

- `backend/src/MaintenanceCMMS.Infrastructure/Data/Excel/ExcelDataProvider.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Data/Excel/ExcelRepository.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Data/Excel/ExcelSchemaRegistry.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Data/Excel/ExcelRowValidator.cs`

Usa ClosedXML para crear, leer, validar y escribir archivos `.xlsx`. Cada archivo base vive bajo `data/excel` y debe tener una hoja `Data`.

## Interfaces que se mantienen

La capa Application define los contratos que no deben cambiar al migrar:

- `IDataProvider`
- `IRepository<T>`
- `IUnitOfWork`
- `IExcelRepository<T>`
- `ISqlRepository<T>`
- `IImportService`
- `IExcelSchemaRegistry`

Los servicios de aplicacion y controllers deben depender de estas interfaces, no de ClosedXML ni de EF Core directamente.

## Clases que se reemplazan para SQL

Para SQL Server o PostgreSQL se reemplazan o completan estas clases:

- `SqlDataProvider`
- `SqlRepository<T>`
- Repositorios especificos por agregado cuando cada modulo tenga casos de uso.
- Configuracion de `DbContext`, entidades EF Core y migraciones dentro de `MaintenanceCMMS.Infrastructure`.

La implementacion SQL actual es un placeholder funcionalmente documentado: valida configuracion y falla explicitamente si se intenta usar sin configurar EF Core.

## Activar SQL Server

Configurar:

```json
{
  "DataProvider": {
    "Provider": "SqlServer",
    "SqlServerConnectionString": "Server=...;Database=MaintenanceCMMS;..."
  }
}
```

En Docker o variables de entorno:

```text
DataProvider__Provider=SqlServer
DataProvider__SqlServerConnectionString=Server=...;Database=MaintenanceCMMS;...
```

Luego registrar el `DbContext` y completar `SqlRepository<T>` con EF Core.

## Activar PostgreSQL

Configurar:

```json
{
  "DataProvider": {
    "Provider": "PostgreSql",
    "PostgreSqlConnectionString": "Host=...;Database=maintenance_cmms;Username=...;Password=..."
  }
}
```

En Docker o variables de entorno:

```text
DataProvider__Provider=PostgreSql
DataProvider__PostgreSqlConnectionString=Host=...;Database=maintenance_cmms;Username=...;Password=...
```

Luego registrar el provider EF Core de PostgreSQL y aplicar migraciones.

## Modulos que no se deben modificar al migrar

No se deben reescribir por cambiar Excel a SQL:

- `MaintenanceCMMS.Domain`
- Casos de uso en `MaintenanceCMMS.Application`
- Controllers de API que consumen interfaces
- Frontend React
- Validaciones de negocio
- Reglas de auditoria y permisos

La migracion debe concentrarse en `MaintenanceCMMS.Infrastructure`.

## Riesgos de operar productivamente con Excel

- Concurrencia limitada y riesgo de bloqueo de archivos.
- Menor integridad referencial que una base SQL.
- Validaciones dependientes de una capa de importacion estricta.
- Menor rendimiento con volumen alto.
- Mayor riesgo si usuarios editan archivos directamente.
- Recuperacion transaccional limitada ante fallas parciales.

## Endpoints de diagnostico

- `GET /api/system/data-provider`
- `GET /api/system/excel-health`
- `GET /api/system/excel-schemas`

Estos endpoints ayudan a verificar proveedor activo, estado de archivos Excel y definiciones de schema antes de migrar.

