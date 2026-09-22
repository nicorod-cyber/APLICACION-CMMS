# Hardening de despliegue AppSec — 22-09-2026

## Resultado

La propuesta pendiente quedó aplicada. React Router, Node y Nginx se actualizaron; backend, frontend y reverse proxy ejecutan sin privilegios permanentes; los puertos externos permanecen en `5173`, `8080` y `5041`.

## Versiones e imágenes

| Componente | Resultado final |
|---|---|
| React Router DOM | `7.18.4`, versión exacta en `package.json` y `package-lock.json` |
| Node de build | `node:22.23.2-alpine3.23@sha256:72c5815a06aed9a2273aea5628d74d348af57843a7b547af2fe53dd3e4b95261` |
| Nginx unprivileged | `nginxinc/nginx-unprivileged:1.30.4-alpine3.24@sha256:adf5042a17f4ecdd200c595fa9ffd1be37efb18f89a830bd1a00e4ab4d59d42c` |
| .NET SDK | `mcr.microsoft.com/dotnet/sdk:8.0@sha256:78235e09001f52b6592c458ac010775ebac6725422e80cd0c1650590f67b2743` |
| ASP.NET runtime | `mcr.microsoft.com/dotnet/aspnet:8.0@sha256:2f202e1169ec507bdc07007cf68c14d0ff3a098110b17c460a60185e1f36a9d1` |
| SQL Server | `mcr.microsoft.com/mssql/server:2022-latest@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090` |
| MailHog | `mailhog/mailhog:v1.0.1`; digest local `sha256:8d76a3d4ffa32a3661311944007a415332c4bb855657f4f6c57996405c009bea` |
| Backend construido | `maintenance-cmms-backend:local`, imagen `sha256:38ebdebe56f92cea81b1f4a7d9fca7bec72ba79a0753b28311fd7803fd4878c3` |
| Frontend construido | `aplicacion-cmms-frontend:latest`, imagen local `sha256:48fb926a33ad1e047219cad95dcf487f6ef7de676f1de19c308aadc886b0ea96` |

Los tags externos relevantes quedan acompañados por digest inmutable. El tag `latest` del frontend es solamente el nombre local generado por Compose; su imagen efectiva se identifica arriba por SHA.

## Usuarios efectivos y puertos

| Contenedor | Usuario efectivo | Puerto externo → interno |
|---|---:|---:|
| `maintenance-cmms-backend` | `uid=1654(app) gid=1654(app)` | `5041 → 8080` |
| `maintenance-cmms-frontend` | `uid=101(nginx) gid=101(nginx)` | `5173 → 8080` |
| `maintenance-cmms-nginx` | `uid=101(nginx) gid=101(nginx)` | `8080 → 8080` |
| `maintenance-cmms-sqlserver` | `uid=10001(mssql) gid=10001(mssql)` | `1433 → 1433` |
| `maintenance-cmms-mailhog` | `uid=1000(mailhog) gid=1000(mailhog)` | `1025`, `8025` |
| `maintenance-cmms-backend-permissions` | `0:0`, efímero | Sin puerto; terminó con código `0` |

El inicializador con root solamente ejecuta `chown`/`chmod` sobre los tres mounts autorizados y termina antes de arrancar el backend. El backend declara además `USER app` en la imagen y `user: 1654:1654` en Compose.

## Prueba de bind mounts

Se escribió desde `maintenance-cmms-backend` usando UID 1654, se leyó desde Windows, luego se escribió desde Windows y se leyó desde el contenedor:

- `/app/data/imports`: correcto.
- `/app/data/templates`: correcto.
- `/app/data/sharepoint-simulated`: correcto.

Los tres directorios devolvieron `test -w` correcto. Los marcadores fueron eliminados después de la prueba.

## Validaciones

| Validación | Resultado exacto |
|---|---|
| Backend Release | Correcto; 0 errores y 10 advertencias nullable existentes |
| Tests backend | `229` aprobados, `0` fallidos, `0` omitidos; 4 min 42 s |
| `npm ci` | 262 paquetes instalados/auditados; 0 vulnerabilidades |
| `npm audit --json` | `0` info, `0` low, `0` moderate, `0` high, `0` critical; total `0` |
| Frontend build | Correcto con Vite 6.4.3; aviso no bloqueante por chunk de 856.10 kB |
| Tests frontend | 11 archivos, `60` aprobados, `0` fallidos |
| Docker build | Correcto con `--no-cache --pull`; backend y frontend construidos desde cero |
| Docker Compose | Servicios activos; SQL healthy; inicializador de permisos terminó con código 0 |
| Smoke HTTP | Login, equipos, faenas, documentos, avisos, OT, preventivos y programación: HTTP 200 |
| Upload/download | Upload 200, download 200, bytes idénticos; artefacto de prueba y fila SQL eliminados |
| E2E en Pilot | `npm run test:e2e`: 1 smoke de navegación de solo lectura aprobado, 18 omitidos por guard explícito de Pilot |
| E2E aislado | 19 ejecutados: `12` aprobados y `7` fallidos; mejora sobre baseline previa `11/8`, sin regresión por Router 7 |
| NuGet SCA | Ningún paquete vulnerable en los cinco proyectos, incluidas dependencias transitivas |

Los siete fallos del entorno aislado son los mismos flujos de UI preexistentes documentados en la revisión anterior: expectativas antiguas en documentos, editor/traslado de unidad y evento de estado. No se modificó lógica funcional para ocultarlos. React Router 7 no agregó fallos; el smoke de navegación/CSP pasó.

La revisión posterior del log del backend encontró 74 entradas de nivel error, correspondientes a 19 solicitudes únicas y duplicadas por la configuración de logging existente: 56 líneas por solicitudes canceladas durante las navegaciones rápidas de Playwright, 14 mensajes de conexión asociados a esas cancelaciones y 4 líneas de una carga deliberadamente rechazada por la política MIME. No hubo entradas nuevas desde las 13:40 UTC, respuestas 500 persistentes ni reinicios del contenedor (RestartCount=0).

## SCA pendiente

No permanecen vulnerabilidades conocidas en `npm audit` ni en `dotnet list package --vulnerable --include-transitive` según los orígenes consultados.

El análisis CVE de imágenes con Docker Scout no se ejecutó: la herramienta requiere enviar metadatos o capas a un servicio externo y la revisión automática rechazó esa operación por falta de autorización específica para ese destino. Por eso las imágenes no deben declararse libres de vulnerabilidades de sistema operativo; ejecutar el escáner corporativo/Checkmarx SCA o Docker Scout autorizado sobre los digests indicados.

## Archivos de esta migración

- `frontend/package.json`, `frontend/package-lock.json`.
- Dos tests `MemoryRouter`: se eliminaron flags `future` exclusivas de React Router 6.
- `frontend/e2e/appsec-smoke.spec.ts`: permite explícitamente el smoke de solo lectura en Pilot mediante `E2E_ALLOW_PILOT_READONLY=true`.
- `infra/docker/backend.Dockerfile`, `infra/docker/frontend.Dockerfile`.
- `infra/nginx/default.conf`, `infra/nginx/frontend.conf`.
- `docker-compose.yml`.
- Este informe y la actualización del informe AppSec original.

