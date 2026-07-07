# Ordenes de trabajo y tareas internas

## Alcance

El modulo de OT implementa el flujo operativo completo:

- OT manual desde CMMS, OT desde aviso y OT preventiva automatica.
- Tareas internas por OT.
- Varios tecnicos por tarea.
- HH por tarea y tecnico.
- Evidencias por OT o tarea.
- Repuestos asociados por tarea.
- Checklist obligatorio o informativo.
- Firma digital registrada contra usuario autenticado.
- Historial auditable de cambios de estado.

## Estados

Los estados soportados son:

- `OTCreada`
- `EnPlanificacion`
- `Programada`
- `PendienteRepuestos`
- `PendienteDocumentacion`
- `EnEjecucion`
- `Pausada`
- `FinalizadaTecnico`
- `EnRevisionSupervisor`
- `CerradaTecnicamente`
- `ValidadaPlanificacion`
- `Anulada`

## Reglas de cierre

El cierre tecnico por supervisor queda bloqueado cuando existe cualquiera de estas condiciones:

- Una tarea marcada con `RequiereEvidencia` no tiene evidencia obligatoria.
- Una tarea marcada con `RequiereHH` no tiene horas registradas.
- Un checklist obligatorio no esta completado.
- Un repuesto en estado `Entregado` no fue marcado como `Utilizado` o `Devuelto`.
- La OT requiere firma y no tiene firma registrada.

Despues del cierre tecnico, planificacion puede validar la OT. Las OT anuladas, cerradas o validadas se excluyen del listado por defecto, salvo que se use `includeClosed=true`.

## Permisos y visibilidad

- Administrador, planificador y gerencia ven todas las OT.
- Usuarios con faenas asignadas solo ven OT de sus faenas.
- Tecnicos solo ven OT donde estan asignados a una tarea.
- Supervisor de mantenimiento puede cerrar tecnicamente.
- Planificador puede validar planificacion.

## API principal

Base: `/api/work-orders`

- `GET /api/work-orders`
- `GET /api/work-orders/{numeroOt}`
- `POST /api/work-orders`
- `POST /api/work-orders/preventive`
- `POST /api/work-orders/{numeroOt}/tasks`
- `POST /api/work-orders/{numeroOt}/tasks/{codigoTarea}/technicians`
- `POST /api/work-orders/{numeroOt}/tasks/{codigoTarea}/labor`
- `POST /api/work-orders/{numeroOt}/evidences`
- `POST /api/work-orders/{numeroOt}/spare-parts`
- `PUT /api/work-orders/{numeroOt}/spare-parts/{itemId}`
- `POST /api/work-orders/{numeroOt}/checklist`
- `PUT /api/work-orders/{numeroOt}/checklist/{itemId}`
- `POST /api/work-orders/{numeroOt}/signatures`
- `POST /api/work-orders/{numeroOt}/schedule`
- `POST /api/work-orders/{numeroOt}/start`
- `POST /api/work-orders/{numeroOt}/pause`
- `POST /api/work-orders/{numeroOt}/finish-technician`
- `POST /api/work-orders/{numeroOt}/close-technical`
- `POST /api/work-orders/{numeroOt}/validate-planning`
- `POST /api/work-orders/{numeroOt}/annul`

## Hojas Excel

- `ordenes_trabajo.xlsx`: cabecera de OT.
- `tareas_ot.xlsx`: tareas internas y requisitos operativos.
- `ot_tecnicos_tarea.xlsx`: tecnicos asignados por tarea.
- `ot_hh.xlsx`: HH por tarea y tecnico.
- `ot_evidencias.xlsx`: evidencias y links documentales.
- `ot_repuestos.xlsx`: repuestos solicitados, entregados, usados o devueltos por tarea.
- `ot_checklists.xlsx`: checklist por tarea.
- `ot_firmas.xlsx`: firmas digitales de la OT.
- `ot_estado_historial.xlsx`: trazabilidad de estados.

## Frontend

La pagina `/ot` expone:

- Formulario de nueva OT manual o preventiva.
- Filtros por estado, faena, tecnico y activo.
- Listado tabular.
- Kanban por estados operativos.
- Detalle con acciones por rol, programacion, tareas, asignaciones, HH, evidencias, repuestos, checklist y firma.
