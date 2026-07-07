# Programacion, calendario, Kanban y Gantt

## Alcance

El modulo de programacion implementa planificacion diaria, semanal y mensual sobre las OT existentes.

Incluye:

- Talleres por faena.
- Capacidad diaria de HH y equipos.
- Programacion y reprogramacion de OT con motivo auditado.
- Calendario visual por taller/dia.
- Kanban por estado de programacion.
- Carta Gantt y dependencias entre OT.
- Alertas de capacidad y atraso.

## Reglas

- Programar una OT actualiza tambien sus fechas en el modulo OT.
- La programacion puede exceder capacidad, pero siempre emite advertencia y alerta.
- La reprogramacion conserva el mismo `ProgramacionId` y registra motivo.
- Las vistas respetan permisos por rol y faena.
- Los trabajos atrasados se calculan comparando fecha fin programada contra la fecha actual y estado de la OT.
- Las dependencias no permiten que una OT dependa de si misma.

## API

Base: `/api/scheduling`

- `GET /api/scheduling/board`
- `GET /api/scheduling/workshops`
- `POST /api/scheduling/workshops`
- `POST /api/scheduling/work-orders/{numeroOt}`
- `POST /api/scheduling/dependencies`
- `GET /api/scheduling/alerts`

## Hojas Excel

- `programacion_talleres.xlsx`: talleres, faena, capacidad, horario y especialidad.
- `programacion_ot.xlsx`: asignacion de OT a taller, inicio, fin, HH estimadas, tecnico y motivo.
- `programacion_dependencias.xlsx`: dependencias Gantt entre OT.
- `programacion_alertas.xlsx`: alertas operativas de programacion.

## Frontend

La pagina `/programacion` expone:

- Filtros por vista, fecha, faena, taller y cerradas.
- Alta/edicion de talleres.
- Programacion y reprogramacion de OT.
- Drag/drop basico desde tarjetas programadas hacia dias/talleres.
- Calendario, Kanban, Gantt y tabla de alertas.
