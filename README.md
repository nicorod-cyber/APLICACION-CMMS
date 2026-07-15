# Mantenimiento [Nombre Empresa]

CMMS web industrial con backend ASP.NET Core, frontend React + TypeScript y migracion en curso para operar localmente sobre PostgreSQL 16 como fuente oficial de datos.

## Prerrequisitos

- .NET SDK 8.
- Node.js 20 o superior.
- Docker Desktop o Docker Engine con Docker Compose.

## Estructura

```text
/backend
  /src
    /MaintenanceCMMS.Api
    /MaintenanceCMMS.Application
    /MaintenanceCMMS.Domain
    /MaintenanceCMMS.Infrastructure
  /tests
    /MaintenanceCMMS.Tests
/frontend
  /src
/data
  /excel
  /templates
  /sharepoint-simulated
/infra
  /docker
  /nginx
  /scripts
/docs
/tests
```

`data/sharepoint-simulator` se conserva como alias legacy del contexto inicial. La ruta activa del prompt 01 es `data/sharepoint-simulated`.

## Backend

```powershell
cd backend
dotnet restore .\MaintenanceCMMS.sln
dotnet run --project .\src\MaintenanceCMMS.Api\MaintenanceCMMS.Api.csproj
```

Endpoints iniciales:

- `GET http://localhost:5041/api/health`
- `GET http://localhost:5041/api/system/info`
- Swagger en `https://localhost:7041/swagger` o `http://localhost:5041/swagger`

## Frontend

```powershell
cd frontend
npm install
npm run dev
```

En PowerShell, si la politica de ejecucion bloquea `npm.ps1`, usa `npm.cmd install` y `npm.cmd run dev`.

Aplicacion local:

- `http://localhost:5173`

Compilacion frontend:

```powershell
cd frontend
npm run build
```

## Docker

```powershell
docker compose up --build
```

Servicios base:

- Frontend directo: `http://localhost:5173`
- Nginx reverse proxy: `http://localhost:8080`
- Backend API: `http://localhost:5041`
- Mailhog UI: `http://localhost:8025`

## Tests

```powershell
cd backend
dotnet test .\MaintenanceCMMS.sln
```

## Configuracion

La configuracion inicial vive en `backend/src/MaintenanceCMMS.Api/appsettings.json` y puede sobrescribirse con variables de entorno. Secciones iniciales:

- `DataProvider`
- `DataProviders`
- `Jwt`
- `SharePoint`
- `Mail`
- `Pdf`
- `PowerBI`
- `Offline`

PostgreSQL 16 es la única fuente operacional. Excel se usa únicamente como archivo de entrada durante una importación explícita; el estado, las filas, los errores, los eventos y los metadatos del archivo se guardan en PostgreSQL.

Antes de iniciar Docker, copia `.env.example` a `.env` y sustituye todos los valores `<...>` por secretos locales. El archivo `.env` está ignorado por Git. Docker exige las variables de base de datos, JWT y administrador inicial, espera el healthcheck de PostgreSQL y no carga datos demo (`Database__SeedDemoData=false`).

```powershell
Copy-Item .env.example .env
# Edita .env con secretos locales.
docker compose up --build -d
docker compose ps
curl http://localhost:5041/api/health
curl http://localhost:5041/api/system/database-health
```

Para migraciones EF Core configura `CMMS_POSTGRES_CONNECTION` y usa `dotnet ef database update`. Las pruebas de integración usan Testcontainers, no una instancia fija en `localhost`.

Documentación de migración, respaldo y limpieza controlada: [postgresql-runtime.md](/C:/Users/Thinkpad/Documents/Enaex/sistema/APLICACION-CMMS/docs/database/postgresql-runtime.md).

Documentacion de migracion:

- `docs/database/matriz-impacto-excel-sql.md`
- `docs/database/migraciones.md`
- `docs/database/seed-desarrollo.md`
- `docs/database/verificacion-post-migracion.md`
- `docs/database/excel-runtime-removal-checklist.md`
