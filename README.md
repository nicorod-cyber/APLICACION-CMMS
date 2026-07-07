# Mantenimiento [Nombre Empresa]

CMMS web industrial con backend ASP.NET Core, frontend React + TypeScript y arquitectura preparada para operar inicialmente con Excel y migrar luego a SQL Server o PostgreSQL.

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

La fuente inicial es Excel bajo `data/excel`. El cambio futuro a SQL debe hacerse mediante configuracion e inyeccion de dependencias, manteniendo los contratos de Application.
