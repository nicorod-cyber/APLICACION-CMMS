# Preventivos automaticos

El prompt 19 agrega el motor de preventivos en `/api/preventive` y la pagina `/preventivos`.

## Alcance

- Planes por activo especifico o por familia de equipo.
- Marca y modelo opcionales para acotar planes por familia.
- Frecuencia por horas, kilometros, calendario o combinada.
- Tolerancias por horas, kilometros y dias.
- Checklist asociado mediante `checklists.xlsx`.
- Repuestos sugeridos en formato `CODIGO:CANTIDAD:UNIDAD;CODIGO2:1:UN`.
- HH estimadas para crear la tarea base de la OT preventiva.
- Lecturas de horometro y kilometraje con fecha, usuario y evidencia opcional.
- Bloqueo de lecturas menores salvo correccion autorizada y auditada.
- Deteccion de saltos anomalos para revision.
- Estados: `Vigente`, `ProximoAVencer`, `EnVentana`, `Vencido`, `OTGenerada`, `Ejecutado`, `Reprogramado`.
- Generacion directa de OT preventiva usando `IWorkOrderService.CreatePreventiveAsync`.
- Notificaciones mediante `IAlertService` y reglas `preventive-created` / `preventive-overdue`.

## Hojas Excel

- `planes_preventivos.xlsx`: configuracion del plan, frecuencias, tolerancias, checklist, repuestos, HH y estado.
- `preventivo_lecturas.xlsx`: lecturas de horometro/km con validacion.
- `preventivo_evaluaciones.xlsx`: resultado historico de evaluaciones por plan y activo.
- `preventivo_historial.xlsx`: cambios de estado, reprogramaciones y OT generadas.
- `ordenes_trabajo.xlsx`: recibe `PlanPreventivoCodigo` y `EsPreventivaAutomatica`.

## Job programado

El host API registra Quartz con `PreventiveMaintenanceJob`.

Configuracion:

```json
"PreventiveMaintenance": {
  "JobsEnabled": true,
  "JobCron": "0 0/30 * * * ?"
}
```

El job invoca `IPreventiveMaintenanceService.RunAutomaticEvaluationAsync`, evalua vencimientos, crea OT cuando el plan esta en ventana o vencido y genera alertas. La logica queda en el servicio de aplicacion/infraestructura, no en Quartz, para mantener migracion SQL-ready.

## Migracion a SQL

La implementacion actual persiste con `IDataProvider`. Para SQL se deben mapear las cuatro hojas preventivas a tablas equivalentes y mantener los contratos `IPreventiveMaintenanceService`, modelos de aplicacion y endpoints sin cambios.
