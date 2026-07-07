# Mapa de modulos

## 1. Identidad, acceso y organizacion

**Incluye:** usuarios, roles, permisos, faenas, perfiles operativos y reglas de visibilidad.

**Reglas clave:**

- Tecnicos solo ven OT asignadas.
- Supervisores ven su faena.
- Bodega ve todas las bodegas.
- Costos solo se muestran a roles autorizados.

**Dependencias:** auditoria, configuracion, frontend de administracion, JWT.

## 2. Auditoria y gobierno de datos

**Incluye:** bitacora de cambios, usuario responsable, fecha/hora, entidad, accion, valores antes/despues, aprobaciones y correcciones auditadas.

**Reglas clave:**

- Todo cambio critico debe quedar trazado.
- Campos validados quedan bloqueados y se corrigen solo mediante flujo auditado.
- Importaciones Excel requieren validacion y aprobacion.

**Dependencias:** identidad, repositorios, importadores, todos los modulos criticos.

## 2.1 Alertas, correos, PDF y plantillas

**Incluye:** bandeja de alertas, reglas configurables, notificaciones, correo, PDF y plantillas HTML.

**Reglas clave:**

- Reconocer una alerta no la cierra.
- Alertas criticas repetitivas siguen abiertas hasta resolver causa.
- Cada correo registra destinatarios, fecha, asunto, PDF, ruta y estado.
- Las plantillas HTML se versionan en `/data/templates` y se persisten como configuracion.

**Dependencias:** auditoria, identidad, documentos, bodega, OT, SharePoint simulado, correo y PDF.

## 3. Activos, jerarquia tecnica y documentacion

**Incluye:** activos, fichas tecnicas, sistemas, subsistemas, componentes, subcomponentes, documentos con vencimiento y alertas documentales.

**Reglas clave:**

- Documentos fisicos se almacenan en SharePoint o simulador local.
- Excel solo almacena datos operativos/metadatos cuando se use como provider inicial.
- Los tipos documentales se configuran en `document_types` con obligatoriedad, criticidad, bloqueo, roles responsables y alerta.
- Los documentos pueden pertenecer a activo, OT o faena y conservan historico por reemplazo/anulacion.
- Vencimientos generan alertas por jobs programados.
- El codigo de activo es unico y queda bloqueado despues de la validacion.
- Documentos criticos vencidos bloquean disponibilidad documental del activo.
- La jerarquia tecnica mantiene maestros cerrados, alias historicos y fusion auditada de duplicados.
- Los nodos usados no se eliminan fisicamente; se marcan como obsoletos.

**Dependencias:** SharePoint adapter, correo adapter, PDF adapter, jobs, auditoria.

## 4. Mantenimiento operativo

**Incluye:** avisos de trabajo, ordenes de trabajo, tareas internas, varios tecnicos por tarea, HH por tarea/tecnico, evidencias, checklists y firma digital.

**Reglas clave:**

- Los avisos se crean dentro del CMMS, se evaluan por supervision, pueden aprobarse/rechazarse y los aprobados se convierten a OT auditada.
- Una OT puede contener multiples tareas.
- Una tarea puede asignarse a varios tecnicos.
- Las HH se registran por tarea y tecnico.
- La firma digital combina dibujo en pantalla y validacion por usuario autenticado.
- Las evidencias pertenecen a tarea u OT y sus archivos viven en almacenamiento documental.
- El prompt 17 agrega flujo OT completo en `/api/work-orders` y pagina `/ot` con listado, Kanban, detalle, tareas, tecnicos, HH, evidencias, repuestos, checklist y firmas.
- El cierre tecnico queda bloqueado si faltan evidencias obligatorias, HH requeridas, checklist obligatorio, repuestos entregados sin usar/devolver o firma requerida.
- El prompt 18 agrega ejecucion tecnica: HH calculadas por hora inicio/termino y validadas por supervisor, evidencias antes/despues con storage local/SharePoint/offline, checklist tipado desde plantillas y firma dibujada por OT o tarea.

**Dependencias:** activos, identidad, auditoria, SharePoint adapter, PDF adapter.

## 5. Preventivos y programacion

**Incluye:** preventivos automaticos, programacion diaria/semanal/mensual, calendario, carta Gantt y Kanban.

**Reglas clave:**

- Preventivos se generan por jobs programados.
- La programacion debe respetar tecnico, faena, activo, prioridad y ventanas operacionales.
- Calendario, Gantt y Kanban comparten fuente de verdad de programacion.

**Dependencias:** OT, activos, jobs, permisos, frontend.

## 6. Disponibilidad contractual

**Incluye:** disponibilidad contractual, causas de indisponibilidad, eventos, tiempos, reglas de calculo y reportabilidad.

**Reglas clave:**

- Las causas de indisponibilidad deben estar catalogadas y ser configurables.
- Los calculos deben conservar trazabilidad hacia OT, activo, fecha y causa.
- Los indicadores deben exponerse para dashboards y Power BI.

**Dependencias:** OT, activos, reportes, auditoria.

## 7. Bodega e inventario

**Incluye:** bodegas, repuestos, stock por bodega, stock minimo, reservas, transferencias, recepciones, ajustes de inventario y material no codificado.

**Reglas clave:**

- Todo movimiento de stock debe auditar origen, destino, cantidad, responsable y motivo.
- El stock disponible se calcula como stock fisico menos stock reservado.
- Reservas deben asociarse a OT o solicitud cuando aplique.
- Transferencias usan bodega de transito y luego recepcion final.
- Entregas y devoluciones deben conservar referencia a OT, activo, faena o centro de costo cuando corresponda.
- Ajustes y bajas requieren motivo y quedan auditados.
- Stock minimo genera alertas o solicitudes de abastecimiento segun configuracion.
- El prompt 12 implementa `/api/inventory`, paginas `/bodega` y `/repuestos`, y el historial `stock_movements.xlsx`.
- El prompt 13 agrega operaciones explicitas de reserva, entrega, transferencia, recepcion, devolucion, ajuste, baja e historial consultable.

**Dependencias:** mantenimiento operativo, abastecimiento, auditoria, permisos.

## 8. Abastecimiento y proveedores

**Incluye:** solicitudes a abastecimiento, numero de solicitud, orden de compra, proveedor y lead time.

**Reglas clave:**

- Las solicitudes pueden originarse por stock minimo, OT, material no codificado o requerimiento manual.
- Lead time debe medirse por proveedor y tipo de repuesto/material.
- Las OC y estados deben quedar auditados.

**Dependencias:** bodega, costos, auditoria, permisos.

## 9. Costos y estados de pago

**Incluye:** costos de repuestos, HH, servicios externos y estados de pago.

**Reglas clave:**

- Costos visibles solo para roles autorizados.
- Costos por OT deben poder agregarse por activo, faena, proveedor, periodo y tipo.
- Servicios externos y estados de pago deben conservar respaldo documental.

**Dependencias:** OT, bodega, abastecimiento, documentos, auditoria.

## 10. Reportes, dashboards y Power BI

**Incluye:** dashboard interno por rol, widgets configurables, datasets/vistas para Power BI e indicadores operacionales.

**Reglas clave:**

- Cada rol ve widgets coherentes con sus permisos.
- Power BI consume datasets o vistas de reporting, no tablas operacionales.
- Los indicadores deben distinguir datos operativos de datos validados/cerrados.

**Dependencias:** todos los dominios, permisos, reporting.

## 11. Importadores Excel

**Incluye:** importacion desde Excel, validacion, previsualizacion, aprobacion, aplicacion y auditoria.

**Reglas clave:**

- Ninguna importacion se aplica sin validacion y aprobacion.
- Los errores deben ser visibles por fila, columna y regla incumplida.
- Las importaciones aprobadas deben ser reproducibles y auditables.

**Dependencias:** data provider, auditoria, permisos.

## 12. Integraciones y documentos

**Incluye:** adaptador de correo, adaptador de SharePoint, generador PDF desde HTML configurable, storage documental y simuladores locales.

**Reglas clave:**

- Los adaptadores deben tener interfaces estables.
- Los simuladores locales deben permitir desarrollo sin credenciales corporativas.
- Las plantillas HTML se versionan y configuran fuera de la logica de negocio.
- `IDocumentStorageService` soporta `ManualLink`, `LocalSimulation` y `GraphApiReady`.

**Dependencias:** infraestructura, configuracion, auditoria.

## 13. Offline y busqueda global

**Incluye:** offline para tecnicos y supervisores, IndexedDB, sincronizacion y busqueda global.

**Reglas clave:**

- Tecnicos deben poder trabajar con OT asignadas sin conexion.
- Supervisores deben poder revisar informacion de su faena en modo offline segun permisos.
- La sincronizacion debe manejar conflictos y auditoria.
- La busqueda global respeta permisos por rol y faena.

**Dependencias:** frontend, API, permisos, auditoria.

## 14. Experiencia, despliegue y documentacion

**Incluye:** modo claro/oscuro, diseno tipo Fracttal, responsive, PWA-ready, Docker, despliegue Linux, documentacion tecnica y manual de usuario.

**Reglas clave:**

- La interfaz debe priorizar operacion diaria y lectura rapida.
- El despliegue debe poder ejecutarse con Docker Compose.
- La documentacion debe mantenerse junto al codigo y actualizarse por modulo.

**Dependencias:** frontend, infraestructura, docs.
