# Revisión AppSec de APEX CMMS

Fecha: 15 de septiembre de 2026. Alcance: API ASP.NET Core 8, infraestructura y aplicación .NET, React, dependencias NuGet/npm, Docker/Nginx y herramienta PostgresToSqlServerMigration.

## Estado de entrega

Se implementaron controles de seguridad y pruebas de regresión. No se modificaron migraciones históricas, esquema de base de datos ni datos operacionales existentes. La validación de integración utilizó bases SQL de prueba y un proyecto Docker independiente, `cmms-security-7167012b`, accesible solamente mediante `127.0.0.1:18080`.

**El proyecto todavía no debe presentarse como libre de vulnerabilidades o aprobado para producción.** Quedan dos avisos npm moderados, ajustes de despliegue pendientes, pruebas E2E que requieren revisión y validaciones externas que este entorno no permite completar. No se ejecutó Checkmarx. No se agregaron suppressions, exclusiones SAST/SCA ni comentarios para ocultar hallazgos.

## Hallazgos y correcciones

Las severidades son aproximadas y representan el riesgo previo al cambio; no sustituyen una clasificación CVSS contextual.

| ID | Severidad | Problema y causa raíz | Corrección y archivos principales | Evidencia |
|---|---|---|---|---|
| A01 | High | Datos de alertas se insertaban directamente en HTML y llegaban a `dangerouslySetInnerHTML`. | `PdfTemplateService` codifica cada valor con `HtmlEncoder`, preserva estructura permitida mediante `SafeTemplateHtml`/HtmlSanitizer. `SafeHtml.tsx` aplica DOMPurify con allowlist antes del único sink React. Asuntos de correo se procesan como texto separado. | Pruebas de script, img/onerror, svg/onload, atributos, plantilla almacenada y preview HTTP. |
| A02 | High | Links documentales aceptaban protocolos peligrosos o HTTP en varios caminos. | `DocumentUrlPolicy` centraliza HTTPS, credenciales, controles y hosts; aplicada a almacenamiento, documentos, importación de metadatos, compras y evidencia heredada. `documentLinks.ts` valida también antes de abrir enlaces. | Tests de javascript/data/file/vbscript, barras, userinfo y hosts. |
| A03 | High | Uploads dependían de extensión/MIME suministrados por cliente. | `UploadPolicy`, `UploadValidationMiddleware` y proveedores comprueban tamaño, nombre, MIME, firmas y estructura básica; OOXML se inspecciona con límites, XML sin DTD ni resolver externo, sin macros/embebidos. | Tests de payload ejecutable/HTML disfrazado, MIME discordante, archivo vacío, tamaño, PNG/PDF/XLSX válidos. |
| A04 | High | Rutas persistidas/importadas podían conducir lecturas fuera del almacenamiento autorizado. | `StoragePathPolicy` resuelve ruta canónica, comprueba separación del root y rechaza traversal, UNC, controles, codificación `%` y enlaces simbólicos bajo root. `ImportPathFilter` confina las rutas suministradas por HTTP y valida XLSX antes del parser. | Tests de traversal Unix/Windows, ruta absoluta, UNC, prefijos hermanos y solicitud HTTP. |
| A05 | High | `fileKey` y metadatos de faena enviados por cliente no constituían autorización suficiente. | `FileAccessPolicy`/`SharePointAccessFilter` consultan documento vinculado y recurso real; filtran listados y exigen gestión documental para mutaciones. Descargas/links verifican acceso antes de servir contenido. | Tests con dos faenas, fileKey ajeno, metadatos de faena falsificados y rol de consulta. |
| A06 | High | JWT emitidos podían sobrevivir a logout/cambio de estado o contraseña. | `SessionSecurity` firma un sello HMAC del estado actual de la cuenta; validación online verifica sello, usuario activo/no bloqueado y permisos efectivos. Logout cambia timestamp mediante actualización SQL dirigida. | Tests HTTP de logout, contraseña, bloqueo, desactivación y cambio de roles. |
| A07 | Medium | Login permitía enumeración por mensajes y no tenía un límite de intentos. | Mensaje genérico, hash ficticio para usuario inexistente, límites de entrada y rate limiter oficial ASP.NET Core por IP de conexión. | Login inválido/SQL payload, 429 y spoof de X-Forwarded-For. |
| A08 | Medium | Parámetros de hashing y configuración JWT necesitaban controles adicionales. | PBKDF2-HMAC-SHA256 a 600 000 iteraciones, salt aleatorio y comparación constante; verificación de hashes antiguos y rehash al login. JWT exige HS256, firma, issuer/audience/lifetime, skew de 30 s y clave de al menos 32 bytes. | Hash legacy/malformado, verificación positiva/negativa, opciones JWT inválidas y login real. |
| A09 | Medium | URLs de Graph y redirecciones automáticas ampliaban el destino de peticiones y podían propagar credenciales. | `GraphSharePointService` fija Graph a `graph.microsoft.com`, desactiva redirección automática y valida destinos HTTPS/443; transferencias usan allowlist y no reciben bearer de Graph. Máximo tres redirecciones de descarga. | Revisión de flujo; falta validación contra un tenant real. |
| A10 | Medium | Excepciones remotas/SQL/Excel y rutas físicas se exponían en respuestas. | Respuestas internas genéricas, `traceId` global y en OT; fallos de proveedor/importación conservan validación de dominio y ocultan detalles internos. SharePoint y alertas dejan de devolver rutas físicas. | Tests HTTP de importación rechazada, download/upload y metadatos sin LocalPath. |
| A11 | Medium | Logs de texto permitían ambigüedad de líneas y propagación innecesaria de errores. | Serilog JSON en Console/File, campos estructurados; mensajes de error externos sustituidos por tipo/correlación. No se incorpora logging de contraseñas ni bearer. | Revisión de sinks; requiere control operacional de retención/acceso. |
| A12 | Medium | Faltaban cabeceras defensivas y límites coherentes de proxy/API. | CSP compatible con SPA en Nginx, frame-ancestors, nosniff, Referrer-Policy, API no-store; HSTS fuera de Development. Nginx 25 MB, Kestrel 25 MB y política de archivo 20 MB. CORS con origins explícitos, métodos y headers limitados. | Build, HTTP tests y smoke navegador de CSP/navegación. |
| A13 | Critical/High/Medium según aviso | Dependencias con avisos conocidos en npm y NuGet. | Vite 6.4.3, Vitest 4.1.11, Router 6.30.6, transitivas npm; System.IO.Packaging 8.0.1 y SSH.NET 2026.0.0. DOMPurify 3.4.15 y HtmlSanitizer 9.2.1039 agregados como controles robustos. | Auditorías finales: NuGet sin avisos; npm conserva dos moderados. |

### Efectos de compatibilidad que debe conocer operación

- Los tokens previos a este cambio requieren iniciar sesión de nuevo. Logout revoca **todas las sesiones de esa cuenta**. Cambios de roles/faenas/permisos invalidan tokens; no se requiere migración. Cada solicitud autenticada consulta el estado vigente en SQL, con coste adicional de lectura y dependencia de disponibilidad de la base.
- Los hashes antiguos admitidos se verifican y actualizan solamente después de un login correcto. Nuevas contraseñas de usuarios pasan la política existente de complejidad. La longitud de una clave JWT no prueba que tenga entropía suficiente: debe generarse aleatoriamente y mantenerse fuera del repositorio.
- Nuevos uploads aceptan PNG, JPEG, WebP, PDF, DOCX y XLSX. Evidencias y firmas aceptan solamente imágenes; límites: 10 MB y 2 MB respectivamente. Archivos antiguos no se borran. Los formatos binarios DOC/XLS y otros formatos que antes podían cargarse quedan rechazados al no disponer de validación estructural segura; si son un requisito corporativo, se necesita incorporar un parser/conversor validado antes de habilitarlos.
- El HTML autorizado conserva párrafos, títulos, énfasis, listas y tablas. Scripts, handlers, formularios y contenido activo se eliminan. La allowlist de frontend es más restrictiva que la de estilos del correo. Revisar plantillas corporativas personalizadas.
- Links nuevos deben ser HTTPS. `SharePoint:AllowedHosts` vacío permite cualquier host DNS HTTPS no local; configurar hosts exactos en producción. `GraphTransferHosts` vacío admite subdominios de sharepoint.com; para limitar al tenant, definir hosts exactos observados y autorizados. Nubes soberanas no están contempladas por el endpoint Graph fijado.
- Imports por ruta HTTP deben estar bajo `Imports:StoragePath`. Rutas absolutas persistidas siguen admitidas únicamente dentro del root correspondiente. Herramientas offline administrativas conservan sus rutas explícitas de operador.
- Descargas internas desde React usan bearer mediante fetch y generan un blob; nunca se adjunta el bearer a enlaces HTTPS externos. Se bloquean redirects en ese fetch. Las previews se limitan a imágenes/PDF; el resto se descarga como octet-stream.
- El límite de login se aplica a la IP de conexión, no a un X-Forwarded-For arbitrario. Detrás de Nginx puede compartirse la cuota entre clientes. Antes de desplegar, configurar proxies confiables/límites en el borde y probar concurrencia real; no confiar en cualquier header reenviado.

## Revisión de flujos sin vulnerabilidad demostrada

| Categoría | Source → transformación → sink → control observado |
|---|---|
| SQL runtime | Filtros de DTO → expresiones LINQ/EF → parámetros SQL. Los comandos crudos revisados para contexto de sesión son constantes. La selección de secuencias pasa por un mapa cerrado de nombres a comandos literales. No se reescribió SQL estático seguro. |
| SQL migrador | Nombres de tablas/columnas → intersección de catálogos SQL/PostgreSQL → `SqlName` duplica `]`, `PostgreSqlName` duplica comillas → comandos de identificadores delimitados. Filtros de catálogo usan parámetros. Secuencias provienen de array estático; RESTART WITH recibe un `long` calculado, no texto del usuario. Estas interpolaciones concretas son candidatas a falso positivo por SQLi, con ese flujo y esas funciones como evidencia. |
| Command injection | Búsqueda de Process.Start/ProcessStartInfo y revisión de ejecución de aplicación/migrador: no se identificó un sink de ejecución de comandos del SO conectado a input HTTP. Esto no afirma seguridad de cualquier script operacional futuro. |
| Deserialización | DTOs JSON tipados/System.Text.Json, sin BinaryFormatter ni TypeNameHandling habilitado en las fuentes activas revisadas. OOXML no permite DTD/entidades externas. Validar aparte los avisos pendientes de React Router. |
| Mass assignment | Endpoints reciben DTOs y servicios asignan campos explícitos. JWT crea UserAccessContext a partir de claims validados; IDs de recursos se verifican en servicios/filtro documental. La ausencia de binding directo de entidades no reemplaza la revisión por rol de cada propiedad de negocio. |
| CSRF | El cliente usa bearer explícito, sin autenticación basada en cookies enviadas automáticamente. Un sitio externo no obtiene el bearer por CSRF clásico. CORS limita origins/headers. Si se cambia a cookies, se necesitarán antiforgery y una evaluación nueva. |
| XSS residual señalado por SAST | Datos → HtmlEncoder → plantilla → HtmlSanitizer → respuesta → DOMPurify allowlist → único dangerouslySetInnerHTML. No excluir el sink: comprobar que los futuros usos pasen siempre por SafeHtml. |
| Paths y migración | Rutas de uploads/import HTTP pasan por confinamiento; rutas de reporte del migrador vienen de argumentos CLI del operador. El migrador requiere credenciales administrativas y confirmación explícita para reemplazar datos; no se ejecutó sobre bases reales. |
| Autorización restante | Se revisaron servicios de equipos/faenas/unidades, avisos/OT, inventario, preventivos/programación y usuarios/roles, junto con sus pruebas existentes. No se presenta una cobertura matemática de todos los pares rol/recurso. Recursos globales o sin faena, roles personalizados, acceso asignado de técnicos y permisos de documentos heredados requieren validación con la matriz corporativa real. |

## Pruebas agregadas

- `AppSecControlTests.cs`: traversal, nombres reservados, MIME/firma/tamaño, host allowlist, hashes legacy/malformados, JWT inválido, HTML y rate limiter real con 429.
- `AppSecHttpTests.cs`: API real con WebApplicationFactory y SQL Server de prueba; autenticación, revocación, recursos de dos faenas, metadatos falsificados, rechazo de URLs/uploads, descarga positiva y ocultación de rutas.
- `AppSecRenderingHttpTests.cs`: payloads reflejados en preview, plantilla almacenada activa, permisos de consulta e importación por ruta.
- `SafeHtml.test.tsx` y `DocumentLink.test.tsx`: sanitización, protocolos, descarga bearer interna y aislamiento de enlaces externos.
- `appsec-smoke.spec.ts`: login y navegación de módulos en Docker, cabeceras y errores de CSP/JavaScript.
- Se sustituyeron fixtures de archivos ficticios por PNG/PDF reales en pruebas positivas; se actualizó la expectativa de login bloqueado a respuesta genérica. Los selectores E2E de encabezado y edición de unidad se ajustaron a la interfaz actual.

## Validación ejecutada

Los logs locales están bajo `.codex-tmp/`; TRX bajo `TestResults/security/`. Son evidencia de esta ejecución, no configuraciones de producción. No compartir el archivo temporal de secretos de Docker.

| Comando / comprobación | Resultado |
|---|---|
| `dotnet build backend/MaintenanceCMMS.sln -c Release` | Correcto, 0 errores; compilación completa con 10 advertencias nullable existentes. Recompilación incremental posterior: 0 errores/0 advertencias emitidas. |
| `dotnet test backend/MaintenanceCMMS.sln -c Release --no-build` | 229 correctas, 0 fallos, 0 omitidas en la ejecución completa inicial; resultado posterior registrado al cierre. |
| `dotnet build tools/PostgresToSqlServerMigration/PostgresToSqlServerMigration.csproj -c Release --no-restore` | Correcto, 0 errores y 0 advertencias. No se ejecutó una migración de datos. |
| `npm run build` (frontend) | Correcto, TypeScript/Vite 6.4.3; aviso de bundle mayor de 500 kB. |
| `npm run test:unit` (frontend) | 60 correctas, 11 archivos, 0 fallos. |
| `dotnet list backend/MaintenanceCMMS.sln package --vulnerable --include-transitive` | Sin paquetes vulnerables reportados por las fuentes NuGet consultadas, incluidos tests. |
| `dotnet list tools/PostgresToSqlServerMigration/PostgresToSqlServerMigration.csproj package --vulnerable --include-transitive` | Sin paquetes vulnerables reportados. |
| `npm audit --json` | Exit 1: 2 moderate; 0 critical, 0 high, 0 low. Afecta react-router y react-router-dom. Baseline: 11 avisos (1 critical, 4 high, 6 moderate). |
| Docker Compose aislado | Build y arranque comprobados con SQL Server healthy; login y comunicación frontend/API correctos. Resultado final de smoke se registra al cierre. |
| HTTP smoke | GET 200 de equipos, faenas, unidades, documentos, avisos, OT, preventivos, programación, usuarios y SharePoint; altas sintéticas de faena, equipo, unidad y cuatro roles. Upload positivo/negativo y descarga se cubren con integración HTTP real. |
| Playwright | Primera ejecución: 4 correctas/14 fallidas por selectores y expectativas de vistas antiguas. No se ocultaron esos fallos; resultado posterior se registra al cierre. |
| Checkmarx / imágenes | Checkmarx no disponible ni ejecutado. Docker Scout está instalado; su presencia no constituye un análisis de imágenes. No declarar las imágenes libres de CVE. |

## Riesgos pendientes y configuración de producción

1. **Router / SCA (Medium):** react-router-dom 6.30.6 mantiene GHSA-wrjc-x8rr-h8h6 y GHSA-337j-9hxr-rhxg en su árbol. El segundo describe SSR y esta SPA usa createBrowserRouter, pero eso no elimina el aviso SCA ni justifica una exclusión general. La solución propuesta requiere Router 7.18.4 y regenerar lockfile, build, unit tests y E2E.
2. **Contenedores (Medium/High según exposición):** siguen imágenes actuales y ejecución de aplicación/contenedores web sin el cambio propuesto a usuario no root. Se preparó un parche independiente para Node 22, Nginx unprivileged e internal port 8080 manteniendo puertos externos. Deben probarse permisos de volúmenes, health checks y escaneo de imágenes por digest.
3. **TLS/hosts/SQL (High si se usan defaults fuera de piloto):** AllowedHosts base sigue siendo `*`; no hay garantía de terminación TLS ni transporte SQL verificado en cada despliegue. Los defaults de piloto/Development no deben usarse como configuración corporativa. Faltan nombres DNS, certificados y cuenta SQL del ambiente real. No se impusieron gates de arranque que pudieran dejar una instalación existente inaccesible.
4. **Archivos:** magic bytes y estructura básica no son antivirus ni CDR. PDF puede contener acciones o adjuntos; imágenes deben decodificarse/re-encodearse si el modelo de amenaza lo exige. Integrar antivirus/cuarentena y límites de concurrencia antes de procesar archivos de orígenes no confiables. La comprobación de enlaces simbólicos no elimina una carrera si otro proceso privilegiado puede alterar el directorio.
5. **Identidad y navegador:** sessionStorage conserva bearer y sigue expuesto ante una futura XSS. No se migró a cookies porque cambia el modelo CSRF/sesión. Probar carga para PBKDF2 y validación online de sesiones; considerar rate limiting distribuido para múltiples réplicas. Validar seed admin mediante secret manager y política operacional.
6. **Integraciones:** falta prueba Graph real, hosts efectivos de descarga/upload, SMTP corporativo, proxy TLS y certificados SQL. No se enviaron correos a terceros.
7. **Checkmarx:** ejecutar SAST/SCA/IaC sobre este árbol y revisar cada source/flow/sink. La lista de candidatos a falso positivo anterior se limita a flujos analizados, no autoriza suppressions.

### Valores que debe proporcionar el despliegue corporativo

Ejemplo documental, **no aplicado al ambiente**; reemplazar todos los nombres de ejemplo y suministrar secretos fuera de Git:

```text
ASPNETCORE_ENVIRONMENT=Production
AllowedHosts=cmms.empresa.example
Cors__AllowedOrigins__0=https://cmms.empresa.example
Jwt__Secret=<secreto aleatorio desde vault, mínimo 32 bytes>
SharePoint__AllowedHosts__0=tenant.sharepoint.com
SharePoint__GraphTransferHosts__0=tenant.sharepoint.com
DataProvider__SqlServerConnectionString=Server=sql.empresa.example;Database=CMMS;User Id=cmms_app;Password=<vault>;Encrypt=True;TrustServerCertificate=False
```

Usar certificado válido y resolver DNS acorde a su SAN. Mantener credenciales de migración separadas de runtime: el arranque actual ejecuta bootstrap/migraciones y su separación requiere diseñar el despliegue, no quitar permisos sin adaptar ese flujo. Terminar HTTPS en un proxy corporativo, restringir acceso directo al backend y confiar exclusivamente en proxies identificados. Swagger se habilita únicamente en Development. El HSTS del backend requiere que la solicitud sea reconocida como HTTPS; si TLS termina en el borde, configurar HSTS allí y revisar forwarded headers confiables.

### Aprobación pendiente

La revisión automática de aprobación rechazó los cambios conjuntos de versiones mayores/contenedores y los gates de configuración productiva por riesgo de compatibilidad y disponibilidad. Se aplicaron las correcciones independientes aceptadas. Se solicitó al usuario autorización expresa para la propuesta concreta Router/Node/contenedores; hasta recibir respuesta no se aplica. El parche de revisión se mantiene separado en `APPSEC-pending-deployment-and-router.patch`; no contiene el lockfile actualizado ni representa una migración ya validada.

## Fuentes consultadas

- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html): parámetros PBKDF2.
- [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-8.0): middleware oficial.
- [Microsoft Graph driveItem content](https://learn.microsoft.com/en-us/graph/api/driveitem-get-content?view=graph-rest-1.0): redirección a descarga preautorizada.
- [SSH.NET advisory GHSA-q939-rpr3-3284](https://github.com/sshnet/SSH.NET/security/advisories/GHSA-q939-rpr3-3284): versión corregida 2026.0.0.
- [React Router redirect advisory](https://github.com/advisories/GHSA-wrjc-x8rr-h8h6) y [SSR advisory](https://github.com/advisories/GHSA-337j-9hxr-rhxg): avisos pendientes.

## Inventario de archivos

El inventario exacto del diff se genera al cierre a continuación. Archivos temporales de pruebas y secretos quedan fuera de este inventario.


```text
M backend/src/MaintenanceCMMS.Api/Program.cs
M backend/src/MaintenanceCMMS.Api/WorkOrderOperationalEndpoints.cs
M backend/src/MaintenanceCMMS.Api/appsettings.json
M backend/src/MaintenanceCMMS.Application/Auth/AuthServices.cs
M backend/src/MaintenanceCMMS.Infrastructure/Alerts/AlertService.cs
M backend/src/MaintenanceCMMS.Infrastructure/Alerts/EmailService.cs
M backend/src/MaintenanceCMMS.Infrastructure/Alerts/PdfTemplateService.cs
M backend/src/MaintenanceCMMS.Infrastructure/Data/Excel/ExcelDataProvider.cs
M backend/src/MaintenanceCMMS.Infrastructure/Data/Sql/SqlDataProvider.cs
M backend/src/MaintenanceCMMS.Infrastructure/Documents/DocumentService.cs
M backend/src/MaintenanceCMMS.Infrastructure/Imports/ExcelImportWorkflowService.cs
M backend/src/MaintenanceCMMS.Infrastructure/MaintenanceCMMS.Infrastructure.csproj
M backend/src/MaintenanceCMMS.Infrastructure/Options/SharePointOptions.cs
M backend/src/MaintenanceCMMS.Infrastructure/Procurement/ProcurementService.cs
M backend/src/MaintenanceCMMS.Infrastructure/Security/AuthService.cs
M backend/src/MaintenanceCMMS.Infrastructure/Security/JwtTokenService.cs
M backend/src/MaintenanceCMMS.Infrastructure/Security/PasswordHasher.cs
M backend/src/MaintenanceCMMS.Infrastructure/Security/SqlServerIdentityStore.cs
M backend/src/MaintenanceCMMS.Infrastructure/Security/UserManagementService.cs
M backend/src/MaintenanceCMMS.Infrastructure/SharePoint/FileMetadataExcelImportService.cs
M backend/src/MaintenanceCMMS.Infrastructure/SharePoint/GraphSharePointService.cs
M backend/src/MaintenanceCMMS.Infrastructure/SharePoint/LocalSharePointSimulationService.cs
M backend/src/MaintenanceCMMS.Infrastructure/SharePoint/SharePointManualLinkService.cs
M backend/src/MaintenanceCMMS.Infrastructure/SharePoint/SharePointStorageBase.cs
M backend/src/MaintenanceCMMS.Infrastructure/TechnicalHierarchy/TechnicalHierarchyExcelImportService.cs
M backend/src/MaintenanceCMMS.Infrastructure/WorkOrders/WorkOrderService.cs
M backend/tests/MaintenanceCMMS.Tests/AuthAndAuthorizationTests.cs
M backend/tests/MaintenanceCMMS.Tests/DocumentStorageServiceTests.cs
M backend/tests/MaintenanceCMMS.Tests/FileMetadataSqlServerTests.cs
M backend/tests/MaintenanceCMMS.Tests/MaintenanceCMMS.Tests.csproj
M backend/tests/MaintenanceCMMS.Tests/OperationalUnitDocumentServiceTests.cs
M frontend/e2e/equipment-smoke.spec.ts
M frontend/package-lock.json
M frontend/package.json
M frontend/src/features/alerts/AlertsPage.tsx
M frontend/src/features/documents/components/DocumentActionsMenu.tsx
M frontend/src/features/documents/components/DocumentVersionsDialog.tsx
M infra/nginx/frontend.conf
A backend/src/MaintenanceCMMS.Api/Security/FileAccessPolicy.cs
A backend/src/MaintenanceCMMS.Api/Security/HttpSecurity.cs
A backend/src/MaintenanceCMMS.Api/Security/ImportPathFilter.cs
A backend/src/MaintenanceCMMS.Application/Storage/StorageSecurity.cs
A backend/src/MaintenanceCMMS.Infrastructure/Alerts/SafeTemplateHtml.cs
A backend/src/MaintenanceCMMS.Infrastructure/Security/SessionSecurity.cs
A backend/tests/MaintenanceCMMS.Tests/AppSecControlTests.cs
A backend/tests/MaintenanceCMMS.Tests/AppSecHttpTests.cs
A backend/tests/MaintenanceCMMS.Tests/AppSecRenderingHttpTests.cs
A docs/APPSEC-HARDENING-2026-09-15.md
A docs/APPSEC-pending-deployment-and-router.patch
A frontend/e2e/appsec-smoke.spec.ts
A frontend/src/shared/security/DocumentLink.test.tsx
A frontend/src/shared/security/DocumentLink.tsx
A frontend/src/shared/security/SafeHtml.test.tsx
A frontend/src/shared/security/SafeHtml.tsx
A frontend/src/shared/security/documentLinks.ts
```
