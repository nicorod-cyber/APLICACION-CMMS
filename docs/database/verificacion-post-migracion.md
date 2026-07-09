# Verificacion post migracion

## Proveedor activo

```bash
curl http://localhost:5041/api/system/data-provider
```

Debe reportar `PostgreSql`.

## Health PostgreSQL

```bash
curl http://localhost:5041/api/system/database-health
```

Debe indicar `canConnect=true` y `pendingMigrations=[]`.

## Excel legacy

```bash
curl http://localhost:5041/api/system/excel-health
```

Debe responder con `legacy=true` y `officialDataSource=false`.

## Verificar ausencia de cascadas

```bash
rg "ON DELETE CASCADE|ReferentialAction.Cascade|DeleteBehavior.Cascade" backend
```

No debe devolver coincidencias en migraciones o configuraciones oficiales.

## Comandos de build

```bash
dotnet restore backend/MaintenanceCMMS.sln
dotnet build backend/MaintenanceCMMS.sln
dotnet test backend/MaintenanceCMMS.sln
npm ci --prefix frontend
npm run build --prefix frontend
docker compose up --build
```
