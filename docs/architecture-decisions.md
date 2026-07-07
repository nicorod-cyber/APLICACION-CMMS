# Decisiones de arquitectura

## Decisiones tecnicas

| Area | Decision | Motivo | Implicancia |
| --- | --- | --- | --- |
| Backend | ASP.NET Core Web API en C# | Stack robusto para aplicaciones empresariales e industriales. | API versionable, tipada y preparada para integraciones. |
| Arquitectura | Clean Architecture | Protege dominio y casos de uso de detalles de infraestructura. | Excel, SQL, correo y SharePoint quedan fuera del dominio. |
| Dominio | Entidades y reglas en capa Domain | Evita que las reglas queden dispersas entre controllers, Excel o frontend. | La logica se mantiene portable al migrar de datos. |
| Aplicacion | Casos de uso y servicios de aplicacion | Orquesta validaciones, permisos, auditoria y repositorios. | Cada flujo critico queda testeable. |
| Infraestructura | Providers, repositorios concretos y adaptadores | Encapsula Excel, SQL, SharePoint, correo y PDF. | Se puede reemplazar proveedor sin tocar reglas de negocio. |
| Persistencia inicial | Excel en `/data/excel/` | Permite comenzar con una fuente simple y cercana al usuario. | Debe tratarse como backend temporal, no como diseno definitivo. |
| Persistencia futura | SQL Server o PostgreSQL con EF Core | Escalabilidad, concurrencia, integridad y reporting. | `DbContext` y migraciones viviran en Infrastructure. |
| Acceso a datos | `IDataProvider` + `IRepository<T>` | Contratos estables para desacoplar origen de datos. | Los servicios consumen interfaces, no archivos ni tablas. |
| Configuracion de provider | `DataProvider: Excel | SqlServer | PostgreSql` | Permite seleccionar proveedor por ambiente. | La migracion parte desde configuracion y DI. |
| Validacion | FluentValidation | Reglas expresivas y testeables. | Validaciones compartidas por comandos/importadores. |
| Seguridad | JWT + permisos por rol y faena | Control operacional granular. | Las consultas deben filtrar por rol, usuario y faena. |
| Auditoria | Auditoria transversal | Trazabilidad completa de acciones criticas. | Cada cambio aprobado o sensible registra evidencia. |
| Observabilidad | Serilog | Diagnostico estructurado. | Logs aptos para consola, archivo y colectores futuros. |
| Jobs | Quartz.NET o Hangfire | Ejecucion confiable de preventivos y alertas. | Los jobs invocan servicios de aplicacion. |
| API | OpenAPI / Swagger | Contratos visibles para frontend e integraciones. | Facilita pruebas, soporte y documentacion. |
| Frontend | React + TypeScript + Vite | UI moderna, tipada y rapida de desarrollar. | Base preparada para modulos por feature. |
| Estado remoto | TanStack Query | Cache, sincronizacion y estados de carga de API. | Reduce logica manual de fetching. |
| Tablas | TanStack Table | Grillas potentes para activos, OT, bodega y reportes. | Soporta filtros, ordenamiento y columnas configurables. |
| Estado local | Zustand o Redux Toolkit | Manejo de preferencias, sesion UI y modo offline. | Se definira en prompt de frontend base. |
| Estilos | Tailwind CSS | Velocidad y consistencia visual. | Debe respetar diseno tipo Fracttal, responsive y claro/oscuro. |
| Offline | IndexedDB | Soporte para tecnicos y supervisores en terreno. | Requiere estrategia de sincronizacion y conflictos. |
| PDF | Plantillas HTML configurables | Permite adaptar formatos sin recompilar. | El motor PDF debe recibir datos estructurados. |
| Correo | Adaptador configurable | Cambia entre simulador y servicio corporativo. | No se hardcodean destinatarios ni credenciales. |
| SharePoint | Adaptador configurable | Permite `ManualLink`, `LocalSimulation` y `GraphApiReady`. | Excel conserva metadata en `sharepoint_files.xlsx`, no binarios. |
| Infraestructura | Docker Compose + Nginx | Ejecucion local y despliegue Linux-ready. | Servicios versionables y repetibles. |

## Decisiones de dominio

- Los activos son el eje tecnico: se relacionan con jerarquia, documentacion, OT, preventivos, evidencias y costos.
- Las ordenes de trabajo son el eje operativo: agrupan tareas, tecnicos, HH, repuestos, checklists, evidencias, firmas y estados.
- Bodega es un dominio integrado pero independiente: administra repuestos, bodegas, stock, reservas, transferencias, recepciones, ajustes y material no codificado.
- Abastecimiento se conecta con bodega mediante solicitudes, OC, proveedores y lead time.
- Disponibilidad contractual se calcula desde eventos de indisponibilidad, causas, tiempos y reglas del contrato.
- Costos se calculan por repuestos, HH, servicios externos y estados de pago, visibles solo para roles autorizados.
- Power BI consumira vistas/datasets de reporting preparados, no tablas operacionales ni archivos Excel sin gobierno.

## Politicas transversales

- Todo modulo debe considerar permisos por rol y faena desde su diseno inicial.
- Todo flujo critico debe registrar auditoria.
- Toda importacion Excel debe pasar por validacion, previsualizacion, aprobacion y aplicacion auditada.
- Las entidades con estados criticos deben usar catalogos configurables.
- Las integraciones externas deben exponerse por interfaces y adaptadores.
- Las rutas, credenciales, correos, tenant IDs y secretos deben vivir en configuracion o variables de entorno.

## Riesgos y mitigaciones

| Riesgo | Impacto | Mitigacion |
| --- | --- | --- |
| Acoplar reglas de negocio a Excel | Migracion costosa a SQL y reglas duplicadas. | Servicios de aplicacion consumen `IRepository<T>` e `IDataProvider`; Excel queda en Infrastructure. |
| Usar Excel con concurrencia alta | Corrupcion, bloqueos o perdida de cambios. | Controlar escrituras, auditar importaciones y limitar Excel a etapa inicial. |
| Permisos incompletos por rol/faena | Exposicion de OT, costos o datos de otra faena. | Autorizacion centralizada y pruebas por rol. |
| Auditoria incompleta | Falta de trazabilidad ante cambios y aprobaciones. | Servicio transversal de auditoria integrado a comandos criticos. |
| Importaciones sin gobierno | Datos invalidos o cambios no aprobados. | Flujo obligatorio de validacion, previsualizacion y aprobacion. |
| Documentos guardados en Excel | Archivos pesados, fragiles y no auditables. | Guardar documentos en SharePoint o simulador local; persistir solo metadatos. |
| Reportes leyendo tablas operacionales | Carga excesiva y cambios quebrando BI. | Crear vistas/datasets de reporting estables. |
| Offline sin control de conflictos | Perdida o duplicacion de trabajo en terreno. | Sincronizacion por lotes, identificadores cliente y reglas de resolucion. |
| Estados hardcodeados | Dificultad para adaptar procesos reales. | Catalogos de estado configurables por modulo. |
| Integraciones corporativas prematuras | Bloqueo por credenciales o dependencias externas. | Adaptadores con simuladores locales desde el inicio. |
