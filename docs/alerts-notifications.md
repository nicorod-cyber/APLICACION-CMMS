# Alertas, correos, PDF y plantillas HTML

## Alcance

El modulo de alertas administra eventos operacionales, notificaciones por correo, PDF generados desde HTML y plantillas configurables.

Eventos cubiertos por reglas iniciales:

- Documento por vencer.
- Documento vencido.
- Repuesto bajo stock.
- Repuesto critico sin stock.
- OT vencida.
- Preventivo creado automaticamente.
- Solicitud pendiente de aprobacion.
- Repuesto pendiente de entrega.
- Stock reservado sin retiro.
- Transferencia pendiente.
- Recepcion vencida.
- Cierre de OT incompleto.
- Disponibilidad afectada.

## Archivos Excel

- `alert_rules.xlsx`: reglas, severidad, repeticion, correo, PDF, plantilla y destinatarios.
- `alerts.xlsx`: alertas abiertas, reconocidas y resueltas.
- `notifications.xlsx`: historial de correos, PDF, ruta, proveedor, estado y error.
- `pdf_templates.xlsx`: plantillas HTML y asunto para PDF/correo.

## Reglas de estado

- Leer o reconocer una alerta no la cierra.
- `acknowledge` marca la alerta como reconocida.
- `resolve` exige motivo y marca la alerta como resuelta.
- Las alertas criticas con `RepeatUntilResolved=true` se reiteran sobre la misma causa abierta e incrementan `RepeatCount`.
- Cuando se implemente el job de deteccion de causa corregida, debe llamar al mismo flujo de resolucion para conservar auditoria.

## Correo

Contrato: `IEmailService`.

Adaptadores implementados:

- `Development` o `Local`: simula envio exitoso sin servidor externo.
- `Mailhog` o `Smtp`: usa `SmtpClient` con `Mail:Host` y `Mail:Port`.
- `MicrosoftGraph`: placeholder controlado para integracion futura.

Remitente:

- `Mail:PlanningEmail` si existe.
- `Mail:From` como fallback.

## PDF

Contrato: `IPdfService`.

La implementacion inicial genera un PDF simple y valido desde HTML renderizado a texto. El archivo se guarda mediante `IDocumentStorageService` con proposito `AlertPdf`, usando el modo SharePoint configurado.

Las plantillas se administran desde `pdf_templates.xlsx` y se materializan tambien en `/data/templates/{TemplateId}.html`.

## Endpoints

- `GET /api/alerts`
- `GET /api/alerts/rules`
- `PUT /api/alerts/rules/{code}`
- `POST /api/alerts/{id}/acknowledge`
- `POST /api/alerts/{id}/resolve`
- `POST /api/alerts/{id}/send-test`
- `GET /api/notifications`
- `GET /api/pdf/templates`
- `PUT /api/pdf/templates/{id}`
- `POST /api/pdf/templates/{id}/preview`

## Frontend

La pantalla `/alertas` incluye:

- Bandeja de alertas.
- Reconocimiento y resolucion con motivo.
- Envio de notificacion de prueba.
- Configuracion de reglas.
- Historial de notificaciones.
- Editor HTML basico.
- Vista previa de plantilla.

## SQL-ready

La logica depende de `IAlertService`, `IEmailService`, `IPdfService` e `IPdfTemplateService`. Para SQL se deben reemplazar las persistencias Excel por repositorios/tablas equivalentes sin cambiar los contratos publicos.
