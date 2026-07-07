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
- Ejecucion tecnica con HH calculadas por inicio/termino, evidencia antes/despues, checklist tipado y firma dibujada en pantalla.

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
- Las HH de una tarea requerida no fueron validadas por supervisor.
- Un checklist obligatorio no esta completado.
- Un checklist completado requiere foto, archivo o firma y no tiene evidencia/firma asociada.
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
- `POST /api/work-orders/{numeroOt}/labor/{hhId}/validate`
- `POST /api/work-orders/{numeroOt}/evidences`
- `POST /api/work-orders/{numeroOt}/spare-parts`
- `PUT /api/work-orders/{numeroOt}/spare-parts/{itemId}`
- `POST /api/work-orders/{numeroOt}/checklist`
- `PUT /api/work-orders/{numeroOt}/checklist/{itemId}`
- `POST /api/work-orders/{numeroOt}/checklist/apply-template`
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
- `ot_hh.xlsx`: HH por tarea y tecnico, fecha, inicio, termino, calculo y validacion supervisor.
- `ot_evidencias.xlsx`: evidencias, fotos antes/despues, comentarios, links documentales, ruta local y metadata offline.
- `ot_repuestos.xlsx`: repuestos solicitados, entregados, usados o devueltos por tarea.
- `ot_checklists.xlsx`: checklist por tarea con tipo de respuesta, valor, texto, evidencia, firma y requisitos de foto/archivo/firma.
- `ot_firmas.xlsx`: firmas digitales de la OT o tarea, con archivo o imagen dibujada.
- `ot_estado_historial.xlsx`: trazabilidad de estados.

## Frontend

La pagina `/ot` expone:

- Formulario de nueva OT manual o preventiva.
- Filtros por estado, faena, tecnico y activo.
- Listado tabular.
- Kanban por estados operativos.
- Detalle con acciones por rol, programacion, tareas, asignaciones, HH, evidencias, repuestos, checklist y firma.
- Pantalla de ejecucion tecnica en el detalle de OT para registrar HH, validar HH, subir evidencia, solicitar/asociar repuestos, completar checklist y firmar.

## Prompt 18

El prompt 18 profundiza la ejecucion tecnica:

- HH: registro por tecnico, tarea y dia; hora inicio/termino; HH calculadas; comentario; validacion supervisor.
- Evidencias: foto antes/despues, archivo, comentario, ruta SharePoint/local y metadata offline futura.
- Checklists: plantillas desde `checklists.xlsx`, items tipados y bloqueo por respuesta/evidencia/firma faltante.
- Firmas: firma por OT o tarea, validada por usuario autenticado, con archivo o dibujo en pantalla.
