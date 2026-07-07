# Adaptador SharePoint

El almacenamiento documental queda detras de `IDocumentStorageService`. Excel guarda solo metadata en `sharepoint_files.xlsx`; los binarios viven fuera del maestro operacional.

## Modos

- `LocalSimulation`: guarda archivos en `SharePoint__LocalPath` y expone descarga por `/api/sharepoint/download?fileKey=...`.
- `ManualLink`: el usuario pega un enlace SharePoint y el sistema guarda metadata auditada.
- `GraphApiReady`: placeholder para Microsoft Graph. Registra metadata y rutas esperadas sin requerir credenciales reales todavia.

`LocalSimulator` se mantiene como alias legacy de `LocalSimulation`.

## Estructura local

La ruta generada respeta:

`Faena / Activo / OT / Proposito`

Cuando un flujo no tiene faena, activo u OT, usa segmentos controlados como `SinFaena` o `SinActivo`.

Propositos soportados:

- `Document`
- `AlertPdf`
- `Evidence`
- `ImportBackup`

## Variables

```env
SharePoint__Provider=LocalSimulation
SharePoint__LocalPath=/app/data/sharepoint-simulated
SharePoint__ManualRootUrl=
SharePoint__SiteUrl=
SharePoint__TenantId=
SharePoint__ClientId=
SharePoint__SiteId=
SharePoint__DriveId=
```

## Paso a Graph API

1. Cambiar `SharePoint__Provider` a `GraphApiReady`.
2. Solicitar a TI una app registration en Microsoft Entra ID.
3. Configurar `TenantId`, `ClientId`, `SiteId` y `DriveId`.
4. Implementar credenciales reales en `GraphSharePointService`.
5. Mantener el contrato `IDocumentStorageService`; los modulos de documentos, PDF e importaciones no deben cambiar.

Permisos a solicitar:

- `Sites.ReadWrite.All` o permisos acotados por sitio mediante `Sites.Selected`.
- Acceso al drive/biblioteca documental CMMS.
- Politica de expiracion/rotacion de secreto o certificado.

## Endpoints

- `GET /api/sharepoint/status`
- `GET /api/sharepoint/files`
- `POST /api/sharepoint/files/upload`
- `POST /api/sharepoint/files/manual-link`
- `POST /api/sharepoint/folders`
- `POST /api/sharepoint/validate-path`
- `GET /api/sharepoint/link?fileKey=...`
- `GET /api/sharepoint/download?fileKey=...`
