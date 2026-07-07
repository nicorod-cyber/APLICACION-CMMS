# Contexto maestro del proyecto CMMS

## Proyecto

**Nombre:** Mantenimiento [Nombre Empresa]

**Tipo de producto:** CMMS web industrial para gestion de mantenimiento, activos, bodega, disponibilidad, costos, documentacion y reportabilidad.

## Objetivo

Construir un sistema CMMS funcional, no una maqueta visual aislada. La primera fuente operativa de datos sera Excel, pero la arquitectura debe quedar preparada para migrar a SQL Server o PostgreSQL sin rehacer frontend, reglas de negocio ni flujos de aplicacion.

## Alcance inicial

El prompt 00 define el contexto maestro y la documentacion base para que los prompts siguientes trabajen con criterios consistentes. No implementa todavia el sistema completo ni crea logica funcional de los modulos.

## Principios rectores

- Excel es temporal y operativo; no es el corazon definitivo del sistema.
- La logica de negocio vive en dominio, servicios de aplicacion, interfaces y repositorios desacoplados.
- El frontend consume casos de uso y contratos API, no conoce si los datos vienen de Excel, SQL Server o PostgreSQL.
- La migracion a SQL debe realizarse cambiando configuracion e infraestructura, no reescribiendo servicios de negocio.
- Los documentos se almacenan en SharePoint o en un simulador local, no embebidos en Excel.
- Los PDF se generan desde plantillas HTML configurables.
- Correo, SharePoint, PDF y almacenamiento documental se integran mediante adaptadores configurables.
- Todo flujo critico debe mantener auditoria, estados configurables, permisos por rol y permisos por faena.
- No se deben hardcodear rutas, usuarios, correos ni credenciales; se usaran variables de entorno y archivos de configuracion.

## Stack obligatorio

### Backend

- C#.
- ASP.NET Core Web API.
- Clean Architecture.
- Entity Framework Core preparado para SQL.
- Repositorios desacoplados.
- Implementacion inicial con Excel.
- Implementacion SQL preparada.
- JWT Auth.
- Serilog.
- FluentValidation.
- OpenAPI / Swagger.
- Jobs programados con Quartz.NET o Hangfire.
- PDF generado desde plantillas HTML configurables.
- Adaptadores para correo y SharePoint.

### Frontend

- React.
- TypeScript.
- Vite.
- React Router.
- TanStack Query.
- TanStack Table.
- Zustand o Redux Toolkit.
- Tailwind CSS.
- Diseno tipo Fracttal.
- Modo claro y modo oscuro.
- Responsive.
- Preparado para PWA.
- IndexedDB para offline.

### Infraestructura

- Docker Compose.
- Servicios para backend, frontend, Nginx, Mailhog o simulador de correo.
- Carpeta local simulada para SharePoint.
- Scripts de seed e importacion.
- README tecnico.
- Despliegue preparado para Linux.

## Modulos obligatorios

1. Usuarios, roles, permisos y faenas.
2. Auditoria completa.
3. Activos y fichas tecnicas.
4. Jerarquia tecnica: sistema, subsistema, componente, subcomponente.
5. Documentacion con vencimiento.
6. Alertas documentales.
7. PDF de respaldo configurable por HTML.
8. Correo mediante adaptador configurable.
9. SharePoint mediante adaptador configurable.
10. Bodega.
11. Repuestos.
12. Stock por bodega.
13. Stock minimo.
14. Reservas.
15. Transferencias entre bodegas.
16. Recepciones.
17. Ajustes de inventario.
18. Material no codificado.
19. Solicitudes a abastecimiento.
20. Numero de solicitud, OC, proveedor y lead time.
21. Avisos de trabajo.
22. Ordenes de trabajo.
23. Tareas internas de OT.
24. Varios tecnicos por tarea.
25. HH por tarea y por tecnico.
26. Evidencias por tarea/OT.
27. Checklists digitales.
28. Firma digital: dibujo en pantalla y validacion por usuario.
29. Preventivos automaticos.
30. Programacion diaria, semanal y mensual.
31. Calendario.
32. Carta Gantt.
33. Kanban.
34. Disponibilidad contractual.
35. Causas de indisponibilidad.
36. Costos: repuestos, HH, servicios externos, estados de pago.
37. Dashboard interno por rol.
38. Widgets configurables.
39. Vistas/tablas para Power BI.
40. Importadores Excel con validacion y aprobacion.
41. Offline para tecnicos y supervisores.
42. Busqueda global.
43. Modo claro/oscuro.
44. Docker y despliegue Linux.
45. Documentacion tecnica y manual de usuario.

## Reglas criticas de negocio y seguridad

- Tecnicos solo ven sus OT asignadas.
- Supervisores ven la informacion de su faena.
- Bodega ve todas las bodegas.
- Costos solo son visibles para roles autorizados.
- Importaciones Excel requieren validacion y aprobacion antes de aplicar cambios.
- Campos validados quedan bloqueados y solo se corrigen con auditoria.
- Todo cambio relevante debe registrar usuario, fecha, faena, accion, entidad afectada y valores antes/despues cuando aplique.
- Los estados de OT, avisos, reservas, solicitudes y aprobaciones deben ser configurables.
- Power BI consumira datasets o vistas de reporting, no tablas operacionales.

## Estructura inicial de carpetas

```text
/backend
  /src
    /Api
    /Application
    /Domain
    /Infrastructure
  /tests
/frontend
  /src
    /app
    /features
    /shared
    /styles
/data
  /excel
  /templates
  /sharepoint-simulated
  /sharepoint-simulator
/docs
/infra
  /docker
  /nginx
  /scripts
/infrastructure
  /docker
  /nginx
  /scripts
/tests
```

`infra/` y `data/sharepoint-simulated/` son las rutas activas desde el prompt 01. `infrastructure/` y `data/sharepoint-simulator/` se conservan por compatibilidad con el contexto maestro inicial.

## Criterio de avance para prompts siguientes

Cada prompt posterior debe:

- Integrar los cambios al repositorio.
- Mantener separacion entre dominio, aplicacion, infraestructura y presentacion.
- Incluir validaciones, pruebas basicas y documentacion cuando aplique.
- Compilar antes de avanzar al siguiente prompt.
- No romper la estrategia Excel-first y SQL-ready definida en `docs/data-provider-strategy.md`.
