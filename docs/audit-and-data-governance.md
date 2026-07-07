# Auditoria y gobierno de datos

## Objetivo

La auditoria del CMMS registra operaciones criticas con suficiente contexto para trazabilidad operacional, seguridad, aprobaciones y migracion futura a SQL.

## Campos auditados

Cada evento registra:

- Usuario.
- Accion.
- Modulo.
- Entidad afectada.
- Id del registro.
- Valor anterior.
- Valor nuevo.
- Fecha/hora UTC.
- IP.
- Dispositivo o User-Agent.
- Motivo cuando aplica.
- Faena.
- Criticidad.
- Correlation id.
- Resultado de exito o fallo.

## Modulos cubiertos

La taxonomia inicial incluye:

- Activos.
- Documentos.
- Bodega.
- Stock.
- Repuestos.
- OT.
- HH.
- Evidencias.
- Firmas.
- Costos.
- Usuarios.
- Importaciones.
- Alertas.
- Configuracion.
- SharePoint.
- PDFs.
- Correos.
- Autenticacion.

## Implementacion actual

- Contratos: `MaintenanceCMMS.Application.Auditing`.
- Implementacion Excel: `MaintenanceCMMS.Infrastructure.Auditing.ExcelAuditService`.
- Contexto HTTP: `AuditContextMiddleware` captura IP, dispositivo y correlation id.
- Endpoint: `GET /api/audit`.
- Pantalla frontend: `/auditoria`, visible para administradores.
- Almacenamiento inicial: `data/excel/audit_log.xlsx`.

El servicio de auditoria esta desacoplado de Excel mediante `IAuditService`. Para SQL se debe crear otra implementacion de ese contrato y cambiar el registro de infraestructura.

## Filtros API y frontend

`GET /api/audit` acepta:

- `userId`
- `module`
- `entityName`
- `action`
- `faenaCodigo`
- `severity`
- `fromUtc`
- `toUtc`
- `skip`
- `take`

## Estados de gobierno de datos

Los estados transversales son:

- `Draft`
- `PendingValidation`
- `Validated`
- `Rejected`
- `Locked`
- `Replaced`
- `Annulled`

Estos estados viven en `DataGovernanceState` y se usan para reglas comunes entre documentos, stock, OT y futuros modulos.

## Reglas

- No se eliminan fisicamente documentos, movimientos de stock ni OT.
- Para esos registros se usa anulacion o reemplazo.
- Ajustes de stock requieren motivo obligatorio.
- Cambios criticos requieren aprobacion y luego se auditan con criticidad alta o critica.
- Campos validados quedan bloqueados.
- Correcciones de datos validados requieren motivo y auditoria.
- Modificar fecha de vencimiento validada requiere permiso explicito.

## Primeras operaciones auditadas

- Login.
- Logout.
- Login fallido.
- Creacion de usuario.
- Cambio de roles.
- Cambio de faenas.
- Bloqueo/desbloqueo.
- Creacion de activo desde servicio de gobierno.
- Ajuste de stock desde servicio de gobierno.
- Cambio de documento validado desde servicio de gobierno.

## Contratos principales

- `IAuditService`: registra y consulta eventos.
- `AuditLog`: modelo de evento auditado con usuario, modulo, entidad, valores, IP, dispositivo, motivo y criticidad.
- `IDataGovernanceService`: centraliza reglas de no eliminacion fisica, motivo obligatorio, bloqueo de campos validados y aprobacion de cambios criticos.

## SQL-ready

La tabla SQL futura debe mantener como minimo las mismas columnas que `audit_log.xlsx`. Los modulos no deben escribir directamente al archivo Excel; deben depender de `IAuditService` o de servicios de aplicacion que lo usen.
