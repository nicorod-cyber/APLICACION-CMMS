# Verificacion post migracion

## Proveedor activo

```bash
curl http://localhost:5041/api/system/data-provider
```

Debe reportar `PostgreSql`.

Resultado ejecutado el 2026-07-09:

```json
{"activeProvider":"PostgreSql","providerType":"PostgreSql","excelPath":"/app/data/excel","sqlServerConfigured":false,"postgreSqlConfigured":true}
```

## Health PostgreSQL

```bash
curl http://localhost:5041/api/system/database-health
```

Debe indicar `canConnect=true` y `pendingMigrations=[]`.

Resultado ejecutado el 2026-07-09:

```json
{"activeProvider":"PostgreSql","postgreSqlOfficial":true,"healthy":true,"canConnect":true,"appliedMigrations":["202607090001_InitialPostgreSqlSchema"],"pendingMigrations":[]}
```

## Excel legacy

```bash
curl http://localhost:5041/api/system/excel-health
```

Debe responder con `legacy=true` y `officialDataSource=false`.

Nota:

- En esta intervencion no se reconsulto `/api/system/excel-health` porque la evidencia decisiva fue el arranque correcto del stack con `data/excel` retirado temporalmente y PostgreSQL como proveedor activo.

## Verificar ausencia de cascadas

```bash
rg "ON DELETE CASCADE|ReferentialAction.Cascade|DeleteBehavior.Cascade" backend
```

No debe devolver coincidencias en migraciones o configuraciones oficiales.

Resultado ejecutado el 2026-07-09:

- Sin coincidencias.

## Comandos de build

```bash
dotnet restore backend/MaintenanceCMMS.sln
dotnet build backend/MaintenanceCMMS.sln
dotnet test backend/MaintenanceCMMS.sln
npm ci --prefix frontend
npm run build --prefix frontend
docker compose up --build
```

Resultado ejecutado el 2026-07-09:

- `dotnet restore backend/MaintenanceCMMS.sln`: correcto.
- `dotnet build backend/MaintenanceCMMS.sln --no-restore`: correcto.
- `dotnet test backend/MaintenanceCMMS.sln --no-build`: correcto, `84/84` pruebas superadas.
- `npm.cmd ci` en `frontend`: correcto.
- `npm.cmd run build` en `frontend`: correcto.
- `docker compose up --build -d`: correcto.

## Verificacion de runtime sin Excel operacional

Prueba ejecutada el 2026-07-09:

1. Se bajo el stack Docker.
2. Se movio temporalmente `data/excel` a `data/excel.off`.
3. Se levanto nuevamente `docker compose up --build -d`.
4. Docker recreo `data/excel` como directorio vacio de montaje.
5. `GET /api/health`, `GET /api/system/data-provider` y `GET /api/system/database-health` siguieron respondiendo correctamente.
6. Se restauro la carpeta original `data/excel`.

Conclusion:

- El backend ya no necesita los Excel operacionales para iniciar en modo PostgreSQL.
- Esto no significa que todos los flujos funcionales esten migrados; solo confirma que el arranque y la salud basica ya no dependen de `data/excel`.
