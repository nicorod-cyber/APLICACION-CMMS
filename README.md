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

PostgreSQL 16 es el proveedor por defecto en `Development`, `.env.example` y `docker-compose.yml`. Excel queda solo como compatibilidad legacy, importacion, exportacion y plantillas; en Docker `data/excel` se monta solo lectura.

El archivo base [appsettings.json](/C:/Users/Thinkpad/Documents/Enaex/sistema/APLICACION-CMMS/backend/src/MaintenanceCMMS.Api/appsettings.json) aun conserva `Provider=Excel` como fallback general, pero el runtime local validado usa [appsettings.Development.json](/C:/Users/Thinkpad/Documents/Enaex/sistema/APLICACION-CMMS/backend/src/MaintenanceCMMS.Api/appsettings.Development.json) y variables de entorno con `Provider=PostgreSql`.

Variables principales:

```text
DataProvider__Provider=PostgreSql
DataProvider__PostgreSqlConnectionString=Host=postgres;Port=5432;Database=cmms;Username=cmms_app;Password=cmms_app_password
Database__SeedDevelopment=true
```

Verificacion:

```powershell
curl http://localhost:5041/api/system/data-provider
curl http://localhost:5041/api/system/database-health
```

Verificacion ejecutada el 2026-07-09:

- `dotnet restore backend/MaintenanceCMMS.sln`: correcto.
- `dotnet build backend/MaintenanceCMMS.sln --no-restore`: correcto.
- `dotnet test backend/MaintenanceCMMS.sln --no-build`: correcto, `84/84` pruebas.
- `npm.cmd ci`: correcto.
- `npm.cmd run build`: correcto.
- `docker compose up --build -d`: correcto.
- `GET /api/health`: `Healthy`.
- `GET /api/system/data-provider`: `activeProvider=PostgreSql`.
- `GET /api/system/database-health`: `healthy=true`, `pendingMigrations=[]`.
- Inicio verificado incluso retirando temporalmente `data/excel`: el stack levanto con PostgreSQL y Docker recreo un directorio vacio de montaje sin usar Excel como base operacional.

Documentacion de migracion:

- `docs/database/matriz-impacto-excel-sql.md`
- `docs/database/migraciones.md`
- `docs/database/seed-desarrollo.md`
- `docs/database/verificacion-post-migracion.md`
- `docs/database/excel-runtime-removal-checklist.md`
