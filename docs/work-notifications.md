# Avisos de trabajo

El modulo de avisos implementa el flujo inicial de mantenimiento operativo:

- Crear aviso dentro del CMMS con faena, activo opcional, sistema/subsistema/componente, descripcion, prioridad, criticidad, evidencia inicial y clasificacion de falla.
- Bandeja de supervisores para revisar avisos creados, en evaluacion y aprobados.
- Evaluar, aprobar, rechazar o anular con motivo obligatorio y auditoria.
- Convertir un aviso aprobado a OT en `ordenes_trabajo.xlsx`, conservando `AvisoId`, faena, prioridad, criticidad y clasificacion de falla.

## Estados

- `Creado`
- `EnEvaluacion`
- `Aprobado`
- `Rechazado`
- `ConvertidoOT`
- `Anulado`

## Datos Excel

- `avisos_trabajo.xlsx`: fuente operativa inicial de avisos.
- `ordenes_trabajo.xlsx`: recibe las OT generadas desde avisos.

La logica usa `IDataProvider`; para migrar a SQL se reemplaza la implementacion de provider/repositorio sin mover las reglas del servicio `IWorkNotificationService`.

## API

- `GET /api/work-notifications`
- `POST /api/work-notifications`
- `POST /api/work-notifications/{id}/evaluate`
- `POST /api/work-notifications/{id}/approve`
- `POST /api/work-notifications/{id}/reject`
- `POST /api/work-notifications/{id}/convert-to-work-order`
- `POST /api/work-notifications/{id}/annul`

## Frontend

La pagina `/avisos` permite crear avisos, filtrar bandeja, revisar detalle y ejecutar acciones supervisadas.
