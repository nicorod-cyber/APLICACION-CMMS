# Carpeta de datos Excel

Este directorio esta reservado para los archivos Excel operativos iniciales del CMMS.

## Proposito

- Servir como fuente de datos provisional para el arranque del sistema.
- Mantener los archivos de carga inicial separados de la logica de negocio.
- Facilitar la migracion futura a SQL Server o PostgreSQL.

## Convencion

- Los archivos deben ser versionados y documentados.
- No deben contener credenciales ni rutas locales sensibles.
- La importacion debe pasar por validacion y aprobacion antes de convertirse en datos operativos.

## Seguridad

- `usuarios.xlsx` almacena hashes de contrasena generados por la aplicacion; no se deben escribir contrasenas en texto plano.
- `roles.xlsx` contiene roles y permisos iniciales.
- `faenas.xlsx` contiene codigo, nombre, empresa, descripcion, ubicacion tecnica, centro de costes, tipo de faena, region, comuna, coordenadas, responsable y estado.
- `audit_log.xlsx` registra eventos criticos con usuario, modulo, entidad, valores anterior/nuevo, IP, dispositivo, motivo, faena y criticidad.
- `activos.xlsx` contiene el maestro de activos y ficha tecnica operacional inicial.
- `asset_state_events.xlsx` registra cambios de estado de activos con motivo y usuario responsable.
- `bodegas.xlsx` contiene bodegas centrales, taller, faena o material en transito con ubicaciones internas opcionales.
- `repuestos.xlsx` contiene el maestro de repuestos; `CodigoSap` es opcional y unico cuando exista, y sin SAP queda como material no codificado.
- `stock_bodegas.xlsx` contiene stock fisico, reservado, disponible calculado y parametros minimo/maximo/reposicion por bodega.
- `stock_movements.xlsx` contiene movimientos auditados; no se debe editar stock directo desde la UI operativa.
- `stock_reservations.xlsx` contiene reservas para OT con estado, cantidad reservada, entregada, liberada y saldo pendiente.
- `stock_transfers.xlsx` contiene transferencias entre bodegas con origen, transito, destino, estado y recepcion.
- `solicitudes_repuestos.xlsx` contiene solicitudes de repuestos y material no codificado desde OT, tarea o bodega, con aprobacion de mantenimiento, revision de bodega, reserva, entrega, recepcion y conversion a maestro.
- `proveedores.xlsx` contiene proveedores con contacto, email, telefono, direccion, lead time esperado y estado.
- `abastecimiento_solicitudes.xlsx` contiene solicitudes internas/externas a abastecimiento, OC, proveedor, costos, documentos de respaldo, fechas y estado.
- `ordenes_compra.xlsx` contiene referencias auditables de OC asociadas a solicitudes de abastecimiento.
- `recepciones_abastecimiento.xlsx` contiene recepciones y despachos directos a OT con movimientos de stock asociados.
- `avisos_trabajo.xlsx` contiene avisos creados dentro del CMMS, con tipo, estado, faena, activo, sistema/subsistema/componente, evidencia inicial, evaluacion, aprobacion/rechazo y conversion a OT.
- `ordenes_trabajo.xlsx` contiene OT operativas; cuando nacen de un aviso conservan `AvisoId`, prioridad, criticidad, clasificacion de falla, faena y auditoria de creacion.
- `tareas_ot.xlsx` contiene tareas internas de cada OT, requisitos de evidencia, HH, checklist y fechas programadas.
- `ot_tecnicos_tarea.xlsx` contiene asignaciones de varios tecnicos por tarea.
- `ot_hh.xlsx` contiene horas hombre por OT, tarea y tecnico, con fecha, hora inicio, hora termino, HH calculadas, comentario y validacion supervisor.
- `ot_evidencias.xlsx` contiene evidencias por OT o tarea; soporta foto antes/despues, archivo, comentario, carga offline futura, ruta SharePoint y ruta local simulada.
- `ot_repuestos.xlsx` contiene repuestos asociados por tarea, estado de entrega/uso/devolucion y cantidades.
- `ot_checklists.xlsx` contiene checklist obligatorio o informativo por tarea, tipo de respuesta, valor, texto, evidencia asociada, firma asociada y requisitos de foto/archivo/firma.
- `ot_firmas.xlsx` contiene firmas digitales por OT o tarea, usuario autenticado, fecha/hora, archivo o imagen dibujada en pantalla.
- `ot_estado_historial.xlsx` contiene el historial auditable de cambios de estado de cada OT.
- `planes_preventivos.xlsx` contiene planes por activo o familia, frecuencias por horas/km/calendario, tolerancias, checklist, repuestos sugeridos, HH estimadas y estado.
- `preventivo_lecturas.xlsx` contiene lecturas de horometro y kilometraje con fecha, usuario, evidencia, correcciones autorizadas y saltos anomalos.
- `preventivo_evaluaciones.xlsx` contiene resultados historicos de evaluacion por plan y activo, estado, remanentes y OT asociada.
- `preventivo_historial.xlsx` contiene cambios de estado, reprogramaciones y OT preventivas generadas.
- `programacion_talleres.xlsx` contiene talleres por faena, capacidad diaria HH, capacidad de equipos, horario, especialidad y estado.
- `programacion_ot.xlsx` contiene la programacion de OT por taller, fecha inicio/fin, HH estimadas, tecnico, prioridad, criticidad y motivo.
- `programacion_dependencias.xlsx` contiene dependencias Gantt entre OT.
- `programacion_alertas.xlsx` contiene alertas operativas de programacion: taller sobrecargado, OT vencida, capacidad excedida, equipo esperando cupo y trabajo critico atrasado.
- `sistemas_componentes.xlsx` contiene sistemas, subsistemas, componentes y subcomponentes; sus codigos son maestros cerrados y los duplicados se fusionan con auditoria.
- `document_types.xlsx` contiene tipos documentales configurables: obligatoriedad, criticidad, bloqueo de disponibilidad, dias de alerta, roles responsables y plantilla.
- `documentos.xlsx` contiene solo metadatos y links de documentos por activo, OT o faena; los archivos se guardan en SharePoint o simulador local.
- `alert_rules.xlsx` contiene reglas de alerta, correo, PDF, severidad, repeticion y destinatarios.
- `alerts.xlsx` contiene alertas abiertas, reconocidas y resueltas.
- `notifications.xlsx` contiene historial de correos, PDF generado, ruta, proveedor, estado y errores.
- `pdf_templates.xlsx` contiene plantillas HTML configurables para alertas y PDF.
- `sharepoint_files.xlsx` contiene metadata auditable de archivos, enlaces manuales, PDFs, evidencias y respaldos de importacion.

## Documentos

- No elimines filas documentales para corregir historico; usa estados `Reemplazado` o `Anulado`.
- Si `FechaVencimientoValidada` esta en `true`, el cambio de vencimiento debe pasar por la aplicacion con motivo y permiso.
- Los documentos vencidos con `Critico=true` o `BloqueaDisponibilidad=true` bloquean disponibilidad documental del activo.
- Para carga masiva, usa el flujo `/importaciones` con entidad `document_types` o `documentos`.
