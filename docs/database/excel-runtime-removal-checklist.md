# Checklist de retiro de Excel en runtime

Fecha de verificacion: 2026-07-09.

## Completado

- `DataProvider` activo validado en runtime: `PostgreSql`.
- `FaenaService` migrado a `CmmsDbContext`; `GET /api/faenas` ya no lee Excel.
- `AssetService` migrado a `CmmsDbContext`; listar, crear, actualizar, consultar detalle, cambiar estado e historial de activos ya usan PostgreSQL.
- `GET /api/system/database-health` sano, con `canConnect=true`.
- `docker compose up --build -d` levanta `postgres`, `backend`, `frontend`, `nginx` y `mailhog`.
- `data/excel` montado solo lectura en Docker.
- Login, roles, permisos y auditoria usan implementaciones PostgreSQL cuando el proveedor activo es PostgreSQL.
- El stack inicia aun cuando `data/excel` se retira temporalmente y Docker recrea un directorio vacio de montaje.
- No se detectaron `ON DELETE CASCADE`, `ReferentialAction.Cascade` ni `DeleteBehavior.Cascade` en `backend`.
- `dotnet restore`, `dotnet build`, `dotnet test`, `npm.cmd ci` y `npm.cmd run build` fueron ejecutados con resultado correcto.

## Pendiente antes de declarar migracion completa

- Migrar `DocumentService` a persistencia EF tipada y asociaciones multi-activo en SQL.
- Migrar `AvailabilityService` a SQL tipado.
- Migrar `InventoryService` a SQL tipado.
- Migrar `MaterialRequestService` a SQL tipado.
- Migrar `ProcurementService` a SQL tipado.
- Migrar `WorkNotificationService` a SQL tipado.
- Migrar `WorkOrderService` a SQL tipado con regla de inmutabilidad de activo/faena.
- Migrar `SchedulingService` a SQL tipado.
- Migrar `PreventiveMaintenanceService` a SQL tipado.
- Migrar `TechnicalHierarchyService` a SQL tipado.
- Migrar `AlertService` y `PdfTemplateService` a SQL tipado.
- Migrar `SharePointStorageBase` para metadata en SQL.
- Reducir `IDataProvider`/`DataRow` a importacion-exportacion o retirarlos de contratos operacionales.

## Evidencia operativa

- `GET /api/health` respondio `Healthy`.
- `GET /api/system/data-provider` respondio `activeProvider=PostgreSql`.
- `GET /api/system/database-health` respondio `healthy=true` y `pendingMigrations=[]`.
- `POST /api/assets` creo un activo en PostgreSQL y `GET /api/assets/{id}` lo recupero correctamente.
- Las marcas de tiempo de los Excel operacionales no cambiaron durante la verificacion normal del runtime.
