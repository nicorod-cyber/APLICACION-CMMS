# Importador Excel con validacion y aprobacion

## Flujo

El flujo implementado es:

1. Carga de archivo Excel.
2. Validacion de columnas.
3. Validacion de tipos y datos requeridos.
4. Deteccion de claves naturales duplicadas.
5. Validacion contra maestros existentes.
6. Vista previa con resumen.
7. Aprobacion o rechazo.
8. Aplicacion al maestro oficial solo si fue aprobado.
9. Auditoria del upload, aprobacion, aplicacion y rechazo.

## Endpoints

- `POST /api/imports/upload`
- `GET /api/imports/{id}/preview`
- `POST /api/imports/{id}/approve`
- `POST /api/imports/{id}/reject`
- `GET /api/imports`
- `GET /api/imports/templates/{entity}`

Todos requieren la politica `Importaciones`, que se resuelve por rol/permisos.

## Entidades soportadas

- `activos`
- `faenas`
- `ubicaciones_tecnicas`
- `usuarios`
- `bodegas`
- `repuestos`
- `stock_bodegas`
- `document_types`
- `documentos`
- `proveedores`
- `sistemas_componentes`
- `planes_preventivos`
- `checklists`
- `ot_historicas`

Tambien se aceptan alias de negocio como `stock_por_almacen`, `tipos_documento`, `tipos_documentales`, `sistemas`, `subsistemas`, `componentes`, `subcomponentes` y `ordenes_trabajo`.

## Reglas

- Una importacion valida queda en `PendingApproval`.
- Una importacion con errores queda en `Validating` y no puede aprobarse.
- Una simulacion no se puede aprobar ni aplicar al maestro oficial.
- La aprobacion hace upsert por clave natural.
- El rechazo no modifica datos oficiales.
- El archivo original se guarda en `data/imports/originals`.
- La metadata y preview se guardan en `data/imports/metadata`.

## Validaciones contra maestros

Se validan referencias cuando las columnas existen:

- `FaenaCodigo` contra `faenas`.
- `BodegaCodigo` contra `bodegas`.
- `UbicacionTecnicaCodigo` y `CodigoPadre` contra `ubicaciones_tecnicas`.
- `CodigoPadre` en `sistemas_componentes` mantiene la relacion sistema/subsistema/componente/subcomponente.
- `RepuestoCodigo` contra `repuestos`.
- `ActivoCodigo` contra `activos`.
- `Familia` contra familias presentes en el maestro de repuestos.
- `TipoDocumento` en `documentos` contra `document_types` cuando exista catalogo.
- Documentos con `EntidadTipo=Activo` contra `activos`.
- Documentos con `EntidadTipo=OT` contra `ordenes_trabajo`.
- Documentos con `EntidadTipo=Faena` contra `faenas`.

## Frontend

La pantalla `/importaciones` permite:

- Seleccionar entidad.
- Descargar plantilla.
- Cargar archivo por drag/drop.
- Ejecutar simulacion.
- Ver resumen de nuevos, actualizados, sin cambios, errores y duplicados.
- Revisar errores por fila.
- Aprobar o rechazar.

## SQL-ready

El frontend y los endpoints dependen de `IExcelImportWorkflowService`. Para SQL se debe implementar una variante persistente que use tablas de importacion y el `IDataProvider` SQL, manteniendo el contrato de aplicacion.
