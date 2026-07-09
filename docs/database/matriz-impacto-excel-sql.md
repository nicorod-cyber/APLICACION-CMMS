# Matriz de impacto Excel a PostgreSQL

Fecha de inspeccion: 2026-07-09.

Estado: inventario actualizado contra el codigo real el 2026-07-09 con busqueda en `backend/src`, `backend/tests`, `docs` y verificacion de runtime local. PostgreSQL ya es el proveedor activo validado en Docker/local, pero varios servicios operacionales siguen pendientes de migracion tipada.

| Excel | Schema | Columnas / clave natural | Servicio que lee/escribe | Endpoints relacionados | Pantallas | Tabla(s) PostgreSQL destino | Seed/importacion | Cambio contrato | Observaciones y riesgos |
|---|---|---|---|---|---|---|---|---|---|
| activos.xlsx | activos | Codigo, Nombre, FaenaCodigo, Familia, EstadoOperacional | AvailabilityService, WorkOrderService, WorkNotificationService, TechnicalHierarchyService | /api/assets | AssetsPage | activos, familias_equipo, estados_operacionales_activo, eventos_estado_activo | Seed minimo + import validado | Familia y estado deben ser selectores | `AssetService` migrado a EF/PostgreSQL. Quedan consumidores indirectos legacy. |
| asset_state_events.xlsx | asset_state_events | EventoId | Migrado a AssetService EF | /api/assets/{id}/state-events | AssetsPage | eventos_estado_activo | Import historico | Estado operacional por FK | Eventos de estado de activo ya escriben en PostgreSQL. |
| disponibilidad_contratos.xlsx | disponibilidad_contratos | ContractCode | AvailabilityService | /api/availability | AvailabilityPage | contratos_disponibilidad | Import validado | Sin cambio | Pendiente EF. |
| disponibilidad_activos_contrato.xlsx | disponibilidad_activos_contrato | AssignmentId | AvailabilityService | /api/availability | AvailabilityPage | disponibilidad_activos_contrato | Import validado | Sin cambio | Requiere historial sin borrado. |
| disponibilidad_eventos.xlsx | disponibilidad_eventos | EventId | AvailabilityService | /api/availability | AvailabilityPage | disponibilidad_eventos | Import validado | Sin cambio | Requiere transacciones. |
| disponibilidad_snapshots.xlsx | disponibilidad_snapshots | SnapshotId | AvailabilityService | /api/availability | AvailabilityPage | disponibilidad_snapshots | Recalculo/seed opcional | Sin cambio | Dato derivado historico. |
| faenas.xlsx | faenas | Codigo | multiples servicios legacy | /api/faenas | FaenaSelect | faenas | Seed minimo + import | Sin cambio | `FaenaService` migrado a EF/PostgreSQL. Quedan consumidores indirectos legacy. |
| ubicaciones_tecnicas.xlsx | ubicaciones_tecnicas | Codigo | AssetService | /api/assets | AssetsPage | ubicaciones_tecnicas/nodos_tecnicos | Import | Sin cambio | Pendiente normalizar jerarquia tecnica. |
| usuarios.xlsx | usuarios | Username | ExcelIdentityStore | /api/auth, /api/users | LoginPage, UsersAdminPage | usuarios, usuario_roles, usuario_faenas | Seed admin idempotente | Sin cambio | Implementado PostgreSqlIdentityStore. |
| roles.xlsx | roles | Codigo | ExcelIdentityStore | /api/auth, /api/users | UsersAdminPage | roles, permisos, rol_permisos | Seed roles idempotente | Agrega permiso familias_equipo.gestionar | Implementado store PostgreSQL. |
| bodegas.xlsx | bodegas | Codigo | InventoryService | /api/inventory | InventoryPage | bodegas | Seed minimo pendiente | Sin cambio | Pendiente EF e inventario transaccional. |
| repuestos.xlsx | repuestos | Codigo | InventoryService, AssetService | /api/inventory/spare-parts | SparePartsPage | repuestos | Seed minimo pendiente | Familia por FK futura | Pendiente EF. |
| stock_bodegas.xlsx | stock_bodegas | BodegaCodigo, RepuestoCodigo | InventoryService | /api/inventory | InventoryPage | stock_bodegas | Seed minimo pendiente | Sin cambio | Requiere concurrencia optimista. |
| stock_movements.xlsx | stock_movements | MovimientoId | InventoryService | /api/inventory | InventoryPage | movimientos_stock | Import/operacion | Sin cambio | Requiere transacciones. |
| stock_reservations.xlsx | stock_reservations | ReservaId | InventoryService | /api/inventory | InventoryPage | reservas_stock | Operacion | Sin cambio | Requiere liberacion logica. |
| stock_transfers.xlsx | stock_transfers | TransferenciaId | InventoryService | /api/inventory | InventoryPage | transferencias_stock | Operacion | Sin cambio | Requiere transacciones. |
| document_types.xlsx | document_types | Codigo | DocumentService | /api/documents/types | DocumentsPage | tipos_documento | Seed/import | Sin cambio | Pendiente EF. |
| documentos.xlsx | documentos | EntidadTipo, EntidadCodigo, TipoDocumento, DocumentoId | DocumentService, AssetService, AvailabilityService | /api/documents, /api/assets/{id}/documents | DocumentsPage | documentos, versiones_documento, archivos, documento_activos | Seed ejemplo pendiente | Debe aceptar multiples activos | Tablas base implementadas; servicio pendiente. |
| proveedores.xlsx | proveedores | Rut | ProcurementService | /api/procurement | ProcurementPage | proveedores | Import | Sin cambio | Pendiente EF. |
| abastecimiento_solicitudes.xlsx | abastecimiento_solicitudes | SolicitudId | ProcurementService | /api/procurement | ProcurementPage | solicitudes_abastecimiento | Import/operacion | Sin cambio | Costos avanzados fuera de alcance. |
| ordenes_compra.xlsx | ordenes_compra | OrdenCompraId | ProcurementService | /api/procurement | ProcurementPage | ordenes_compra | Import/operacion | Sin cambio | Pendiente EF. |
| recepciones_abastecimiento.xlsx | recepciones_abastecimiento | RecepcionId | ProcurementService | /api/procurement | ProcurementPage | recepciones_abastecimiento | Operacion | Sin cambio | Requiere movimiento stock transaccional. |
| solicitudes_repuestos.xlsx | solicitudes_repuestos | NumeroSolicitud | MaterialRequestService, ProcurementService | /api/material-requests | MaterialRequestsPage | solicitudes_repuesto | Operacion | Sin cambio | Pendiente EF. |
| avisos_trabajo.xlsx | avisos_trabajo | AvisoId | WorkNotificationService | /api/work-notifications | WorkNotificationsPage | avisos_trabajo | Operacion | Sin cambio | Conversion a OT debe ser transaccional. |
| ordenes_trabajo.xlsx | ordenes_trabajo | NumeroOT | WorkOrderService, WorkNotificationService, AvailabilityService | /api/work-orders | WorkOrdersPage | ordenes_trabajo | Seed demo pendiente | Update no debe exponer activo/faena | Pendiente EF y trigger de inmutabilidad. |
| tareas_ot.xlsx | tareas_ot | NumeroOT, CodigoTarea | WorkOrderService | /api/work-orders | WorkOrdersPage | tareas_ot | Operacion | Sin cambio | Pendiente EF. |
| ot_tecnicos_tarea.xlsx | ot_tecnicos_tarea | AsignacionId | WorkOrderService | /api/work-orders | WorkOrdersPage | ot_tecnicos_tarea | Operacion | Sin cambio | Relacion historica sin borrado. |
| ot_hh.xlsx | ot_hh | HHId | WorkOrderService | /api/work-orders | WorkOrdersPage | ot_hh | Operacion | Sin cambio | Pendiente EF. |
| ot_evidencias.xlsx | ot_evidencias | EvidenciaId | WorkOrderService | /api/work-orders | WorkOrdersPage | ot_evidencias, archivos | Operacion | Sin cambio | Binario fuera de SQL. |
| ot_repuestos.xlsx | ot_repuestos | ItemId | WorkOrderService | /api/work-orders | WorkOrdersPage | ot_repuestos | Operacion | Sin cambio | Requiere inventario transaccional. |
| ot_checklists.xlsx | ot_checklists | ItemId | WorkOrderService | /api/work-orders | WorkOrdersPage | ot_checklists | Operacion | Sin cambio | Pendiente EF. |
| ot_firmas.xlsx | ot_firmas | FirmaId | WorkOrderService | /api/work-orders | WorkOrdersPage | ot_firmas, archivos | Operacion | Sin cambio | Firma binaria fuera de SQL. |
| ot_estado_historial.xlsx | ot_estado_historial | HistorialId | WorkOrderService | /api/work-orders | WorkOrdersPage | historial_ot | Operacion | Sin cambio | No borrar; registrar anulaciones. |
| programacion_talleres.xlsx | programacion_talleres | TallerCodigo | SchedulingService | /api/scheduling | SchedulingPage | talleres_programacion | Seed/import | Sin cambio | Pendiente EF. |
| programacion_ot.xlsx | programacion_ot | ProgramacionId | SchedulingService | /api/scheduling | SchedulingPage | programacion_ot | Operacion | Sin cambio | Pendiente EF. |
| programacion_dependencias.xlsx | programacion_dependencias | DependenciaId | SchedulingService | /api/scheduling | SchedulingPage | programacion_dependencias | Operacion | Sin cambio | FK OT a OT sin cascade. |
| programacion_alertas.xlsx | programacion_alertas | AlertId | SchedulingService | /api/scheduling | SchedulingPage | programacion_alertas | Operacion | Sin cambio | Pendiente EF. |
| planes_preventivos.xlsx | planes_preventivos | Codigo | PreventiveMaintenanceService | /api/preventive-maintenance | PreventiveMaintenancePage | planes_preventivos | Seed/import | Familia por FK futura | Pendiente EF. |
| preventivo_lecturas.xlsx | preventivo_lecturas | ReadingId | PreventiveMaintenanceService | /api/preventive-maintenance | PreventiveMaintenancePage | preventivo_lecturas | Operacion | Sin cambio | Pendiente EF. |
| preventivo_evaluaciones.xlsx | preventivo_evaluaciones | EvaluacionId | PreventiveMaintenanceService | /api/preventive-maintenance | PreventiveMaintenancePage | preventivo_evaluaciones | Operacion | Sin cambio | Pendiente EF. |
| preventivo_historial.xlsx | preventivo_historial | HistoryId | PreventiveMaintenanceService | /api/preventive-maintenance | PreventiveMaintenancePage | preventivo_historial | Operacion | Sin cambio | Pendiente EF. |
| checklists.xlsx | checklists | Codigo | WorkOrderService | /api/work-orders | WorkOrdersPage | plantillas_checklist | Seed/import | Familia por FK futura | Pendiente EF. |
| alert_rules.xlsx | alert_rules | Code | AlertService | /api/alerts/rules | AlertsPage | reglas_alerta | Seed/import | Sin cambio | Pendiente EF. |
| alerts.xlsx | alerts | AlertId | AlertService | /api/alerts | AlertsPage | alertas | Operacion | Sin cambio | Pendiente EF. |
| notifications.xlsx | notifications | NotificationId | AlertService | /api/notifications | AlertsPage | notificaciones | Operacion | Sin cambio | Pendiente EF. |
| pdf_templates.xlsx | pdf_templates | TemplateId | PdfTemplateService | /api/pdf/templates | AlertsPage | plantillas_pdf | Seed/import | Sin cambio | HTML como texto, no binario. |
| sharepoint_files.xlsx | sharepoint_files | FileKey | SharePointStorageBase | /api/sharepoint/* | DocumentsPage, WorkOrdersPage | archivos | Import/operacion metadata | Sin cambio | Tabla `archivos` implementada. |
| sistemas_componentes.xlsx | sistemas_componentes | Codigo | TechnicalHierarchyService | /api/technical-hierarchy | TechnicalHierarchyPage | nodos_tecnicos, nodo_familias | Import | Familias por ID/codigo | Pendiente EF. |
| audit_log.xlsx | audit_log | AuditId | ExcelAuditService | /api/audit | AuditPage | audit_log | No importar por defecto | Sin cambio | Implementado PostgreSqlAuditService. |

## Lecturas/escrituras operacionales pendientes

La inspeccion encontro llamadas a `ReadRowsAsync` y `SaveRowsAsync` en Assets, Availability, Alerts, Documents, Imports, Inventory, Procurement, Scheduling, PreventiveMaintenance, TechnicalHierarchy, WorkNotifications, WorkOrders, SharePoint metadata y PDF templates. En PostgreSQL, `SqlDataProvider` bloquea el acceso por filas para impedir una falsa migracion a una tabla universal; esos modulos deben migrarse a repositorios/consultas tipadas.

## Clasificacion exacta de usos restantes

### 1. Uso operacional prohibido pendiente de migracion

- `backend/src/MaintenanceCMMS.Infrastructure/Alerts/AlertService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Alerts/PdfTemplateService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Availability/AvailabilityService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Documents/DocumentService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Imports/ExcelImportWorkflowService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Inventory/InventoryService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/MaterialRequests/MaterialRequestService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/PreventiveMaintenance/PreventiveMaintenanceService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Procurement/ProcurementService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Scheduling/SchedulingService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/SharePoint/SharePointStorageBase.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/TechnicalHierarchy/TechnicalHierarchyService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/WorkNotifications/WorkNotificationService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/WorkOrders/WorkOrderService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Data/ImportService.cs`

### Servicios migrados en esta iteracion

- `backend/src/MaintenanceCMMS.Infrastructure/Faenas/FaenaService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Assets/AssetService.cs`

Estos servicios ya no usan `IDataProvider`, `DataRow`, `ReadRowsAsync` ni `SaveRowsAsync`.

### 2. Uso permitido de Excel

- `backend/src/MaintenanceCMMS.Infrastructure/Data/Excel/ExcelDataProvider.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Data/Excel/ExcelRepository.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Data/Excel/ExcelRowValidator.cs`

Permitido solo para:

- lectura y validacion de archivos cargados por el usuario;
- generacion de plantillas;
- exportaciones;
- pruebas legacy especificas del proveedor Excel;
- semillas explicitas de desarrollo que no participen del runtime operacional.

### 3. Codigo legacy a aislar o retirar

- `backend/src/MaintenanceCMMS.Infrastructure/Auditing/ExcelAuditService.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Security/ExcelIdentityStore.cs`
- `backend/src/MaintenanceCMMS.Infrastructure/Data/Sql/SqlDataProvider.cs`
- `backend/src/MaintenanceCMMS.Application/Abstractions/Data/DataRow.cs`
- `backend/src/MaintenanceCMMS.Application/Abstractions/Data/IDataProvider.cs`
- `backend/src/MaintenanceCMMS.Application/Abstractions/Data/IImportService.cs`
- `backend/src/MaintenanceCMMS.Application/Abstractions/Data/IRepository.cs`

Notas:

- `ExcelAuditService` y `ExcelIdentityStore` ya no son el camino activo cuando el proveedor es PostgreSQL.
- `SqlDataProvider` sigue siendo una guarda legacy: evita que los modulos pendientes usen una falsa persistencia tabular generica.
- `DataRow` e `IDataProvider` siguen contaminando contratos de servicios operacionales y son la principal deuda de la migracion.

## Verificacion de runtime ejecutada

Comprobado el 2026-07-09:

- `GET /api/system/data-provider` devolvio `activeProvider=PostgreSql`.
- `GET /api/system/database-health` devolvio `healthy=true`, `canConnect=true`, `pendingMigrations=[]`.
- `docker compose up --build -d` levanto `postgres`, `backend`, `frontend`, `nginx` y `mailhog`.
- El sistema inicio incluso despues de retirar temporalmente `data/excel`; Docker recreo una carpeta vacia de montaje y el backend siguio sano con PostgreSQL.
- No se observaron cambios de `LastWriteTimeUtc` en los Excel operacionales durante estas verificaciones de runtime normal.
- `POST /api/assets` creo un activo nuevo en PostgreSQL y `GET /api/assets/{id}` lo recupero con `estadoOperacional=OPERATIVO_FAENA`.
