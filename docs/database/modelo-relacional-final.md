# Modelo relacional final

Esta iteracion agrega la base EF Core PostgreSQL en `MaintenanceCMMS.Infrastructure` con tablas tipadas para identidad, auditoria, faenas, familias, estados operacionales, activos y documentos compartidos.

## Implementado en EF

- `usuarios`, `roles`, `permisos`, `usuario_roles`, `rol_permisos`, `usuario_faenas`
- `faenas`
- `estados_operacionales_activo`
- `familias_equipo`
- `activos`
- `eventos_estado_activo`
- `documentos`, `versiones_documento`, `archivos`, `documento_activos`
- `audit_log`

## Reglas aplicadas

- UUID como clave tecnica.
- Codigos operacionales unicos.
- Fechas con hora en `timestamptz`.
- Nombres de tablas y columnas en `snake_case`.
- Relaciones con `RESTRICT`; no se usa borrado en cascada.
- Estados operacionales de activo cerrados por check constraint.
- Binarios fuera de SQL; `archivos` guarda solo metadata.
- Relaciones historicas con indicador `vigente` y campos de asignacion/desasignacion.

## Pendiente de completar

Faltan migraciones tipadas para inventario, OT, preventivos, programacion, abastecimiento, avisos, alertas, notificaciones, plantillas PDF y jerarquia tecnica. Mientras esos servicios no migren a EF, el runtime PostgreSQL bloqueara accesos por `DataRow` para evitar persistencia falsa.
