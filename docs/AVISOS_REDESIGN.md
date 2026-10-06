# Rediseño de Avisos — fase 1

## Modelo y flujo

`WorkNotification` es el encabezado inmutablemente numerado (`AV-######`). Exige un activo o una unidad operativa existente; el servidor resuelve la faena y nunca toma esos datos ni el activo afectado desde snapshots del cliente.

Cada aviso contiene uno o más `WorkNotificationItem`. Un ítem conserva el activo afectado real y snapshots de código/nombre, opcionalmente nodos de jerarquía técnica y evidencias (`FileMetadata`) vinculadas al ítem. En unidades compuestas, el rol `Fabrica` o `Chasis` se resuelve contra la composición vigente en backend.

Estados de aviso: `Borrador → PendientePlanificacion → EnGestion`, con `PendientePlanificacion → DevueltoFaena → PendientePlanificacion`, y terminales `Rechazado` y `Anulado`. Devolver, rechazar y anular exigen motivo. No existe endpoint CRUD para cambiar estado.

Los ítems parten en `PendientePlanificacion`, pasan a `AprobadoParaGestion` al aceptar, y el modelo reserva `AsociadoOT`, `ResueltoSinOT`, `Rechazado` y `Anulado` para las decisiones posteriores.

## Permisos y seguridad

Todas las consultas y acciones validan la faena en backend. Crear/corregir requiere un rol operativo autorizado; la aceptación, devolución y rechazo exige `planificador` (o administrador). Registrar lectura exige el permiso de lecturas o administrador. Los IDs de activo, unidad, nodo técnico y archivo se vuelven a resolver en servidor; las evidencias deben existir, no estar eliminadas y pertenecer a la misma faena.

Las transiciones se escriben en historial y auditoría. Los archivos no se eliminan mediante Avisos. No se acepta un equipo por texto libre ni se exponen detalles internos de excepciones.

## API

- `GET /api/work-notifications/?status=&faenaCodigo=&equipoCodigo=&desdeUtc=&hastaUtc=&texto=`
- `GET /api/work-notifications/{numero}`
- `POST /api/work-notifications/` — crea borrador
- `PUT /api/work-notifications/{numero}/draft`
- `POST /api/work-notifications/{numero}/submit`
- `POST /api/work-notifications/{numero}/return`
- `POST /api/work-notifications/{numero}/accept`
- `POST /api/work-notifications/{numero}/reject`
- `POST /api/work-notifications/{numero}/annul`

## Migración e integración futura con OT

La migración `AvisosRedesignPhaseOne` es aditiva: preserva columnas heredadas, añade los estados de diseño, historial, trabajos y evidencias. Crea un ítem sintético para cada aviso heredado que pueda resolver un activo; los avisos sin activo ni componente vigente de unidad permanecen intactos para revisión manual. No elimina datos legacy.

La relación futura está en `WorkNotificationItem.WorkOrderId` y `WorkOrderTaskId`: permite que un aviso alimente varias OT y que una OT agrupe ítems del mismo activo afectado. La creación y asignación de OT no pertenece a esta fase.

## Verificación

Las pruebas de servicio cubren equipo obligatorio, aislamiento cross-faena, estado operacional, múltiples trabajos, envío, devolución y reenvío. Antes de liberar se deben ejecutar la suite completa, build Release, pruebas de frontend y las auditorías NuGet/npm indicadas en el procedimiento de entrega.

## Correcciones de robustez runtime

- La causa del falso HTTP 500 al crear era la serialización directa de entidades EF en auditoría: cada ítem mantiene la navegación inversa al aviso y el serializador recorría el ciclo `Aviso → Items → Notification`. Ahora todos los eventos de Avisos usan snapshots explícitos de valores escalares e ítems, sin navegaciones ni proxies.
- Crear, corregir borrador y cada transición se ejecutan en una única transacción SQL Server que incluye encabezado, ítems, lectura asociada, historial y auditoría. Un fallo de auditoría hace rollback de toda la operación; no se confirma un aviso parcial.
- La causa del conflicto falso en transiciones era que se modificaba solamente `StatusId`, dejando la navegación `Status` del grafo rastreado en el valor anterior. La transición ahora carga el catálogo destino y actualiza FK y navegación de forma coherente antes de persistir. La concurrencia optimista (`row_version`) se conserva; un conflicto real se registra de forma segura y se traduce en `409 WORK_NOTIFICATION_CONCURRENCY_CONFLICT`, sin reintento ciego ni sobrescritura.
- La secuencia de Avisos se repara mediante la migración `20261006160000_WorkNotificationNumberSequenceRepair`, que avanza solamente hacia adelante respecto del mayor `AV-######` existente y del valor actual de la secuencia. Durante la creación se usa un bloqueo transaccional SQL Server para serializar la comprobación de históricos y la reserva de número. No se renumeran ni reutilizan Avisos existentes.
- Se agregaron pruebas de snapshot de auditoría sin ciclos, rollback ante falla de auditoría y continuidad después de un número histórico. Las pruebas existentes mantienen cobertura de envío, devolución y reenvío; estos recorridos usan ahora el mismo flujo transaccional.
- La configuración de Serilog tenía salida a consola tanto por configuración como por código. Se conserva el sink declarado en configuración y se elimina el duplicado programático, sin reducir logs, correlación ni otros sinks.
