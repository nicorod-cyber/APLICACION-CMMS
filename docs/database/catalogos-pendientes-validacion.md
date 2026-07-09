# Catalogos pendientes de validacion

Los siguientes catalogos se conservan temporalmente con los valores existentes en codigo y Excel. No se agregan valores de negocio nuevos sin validacion del equipo de mantenimiento.

## Cerrados ya definidos

Estados operacionales de activo:

| Codigo | Nombre |
|---|---|
| OPERATIVO_FAENA | Operativo en Faena |
| ALERTA_FAENA | Con alerta en Faena |
| FUERA_SERVICIO_FAENA | Fuera de servicio en Faena |
| FUERA_SERVICIO_TALLER | Fuera de servicio en Taller |

## Pendientes

| Catalogo | Fuente actual | Riesgo |
|---|---|---|
| estados de OT | `ordenes_trabajo.xlsx`, `ot_estado_historial.xlsx`, enums actuales | Debe validarse antes de imponer checks fuertes. |
| prioridades OT/avisos | `ordenes_trabajo.xlsx`, `avisos_trabajo.xlsx` | Puede variar por faena/cliente. |
| criticidades | activos, OT, avisos, alertas | Requiere acuerdo operacional. |
| tipos de activo | `activos.xlsx` | Hoy se conserva texto existente. |
| tipos documentales | `document_types.xlsx` | Debe migrarse a maestro tipado. |
| estados documentales | `documentos.xlsx` | Debe validar bloqueo de disponibilidad. |
| estados de inventario | stock, reservas, transferencias | Deben alinearse con bodega. |
| tipos de movimiento stock | `stock_movements.xlsx` | Requiere reglas contables/logisticas. |
| causas de indisponibilidad | `disponibilidad_eventos.xlsx` | Debe validarse con contratos. |
| tipos de mantenimiento | OT y preventivos | Debe validarse con planificacion. |
| estados de alertas/notificaciones | alerts, notifications | Debe validarse con SLA de atencion. |

## Decision temporal

Hasta completar la validacion, PostgreSQL no debe usar checks que rompan los flujos existentes para estos catalogos. La excepcion son los cuatro estados operacionales de activos, que si quedan cerrados por regla obligatoria.
