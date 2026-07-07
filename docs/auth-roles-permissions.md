# Autenticacion, roles, permisos y faenas

## Estado actual

La primera version usa usuarios propios almacenados en Excel, con backend ASP.NET Core Web API y JWT.

- `usuarios.xlsx` guarda username, email, nombre, estado, bloqueo, hash de clave, roles y faenas.
- `roles.xlsx` guarda roles iniciales y permisos asociados.
- `audit_log.xlsx` registra eventos de autenticacion y administracion de usuarios.
- La API expone `/api/auth/*` y `/api/users/*`.
- El frontend guarda la sesion JWT en `sessionStorage` y filtra rutas/menu segun roles y permisos.

## Configuracion obligatoria

Las claves y el usuario inicial no viven en codigo. Deben configurarse con variables de entorno o archivos de configuracion.

```text
Jwt__Issuer=MaintenanceCMMS
Jwt__Audience=MaintenanceCMMS.Clients
Jwt__Secret=replace-with-at-least-32-characters
Jwt__ExpirationMinutes=60

Auth__SeedAdmin__Username=admin
Auth__SeedAdmin__Email=admin@example.local
Auth__SeedAdmin__DisplayName=Administrador CMMS
Auth__SeedAdmin__Password=ChangeMe.Admin123!
```

Si `usuarios.xlsx` no tiene usuarios, el backend crea un admin inicial con esos valores. En ambientes reales se debe cambiar la clave inicial antes de cargar datos productivos.

## Roles iniciales

- `admin`
- `planificador`
- `supervisor_mantenimiento`
- `tecnico`
- `bodeguero`
- `supervisor_bodega`
- `gerencia`
- `consulta_faena`

## Reglas implementadas

- Tecnicos acceden solo a OT donde estan asignados.
- Supervisores acceden solo a datos de sus faenas.
- Planificadores pueden operar sobre varias faenas asignadas.
- Bodega y supervisor de bodega ven bodegas globales.
- Costos requieren permiso `costos.ver`.
- Administracion requiere rol `admin` o permiso `administracion`.
- Importaciones requieren `importaciones.aprobar`.
- Ajustes de stock requieren `stock.ajustar`.
- Cierre de OT requiere supervisor o permiso `ot.cerrar`.
- Validacion final de OT requiere planificacion o permiso `ot.validar_final`.

## Microsoft Entra ID

El contrato futuro esta preparado en Application:

```csharp
IExternalIdentityProvider
```

La implementacion placeholder esta en Infrastructure:

```csharp
MicrosoftEntraIdentityProvider
```

Actualmente devuelve `null` y no participa en login. Cuando se active Microsoft 365/Entra ID, el cambio esperado es implementar ese adaptador, mapear grupos externos a roles internos y mantener los permisos/faenas internos como fuente de autorizacion del CMMS.

## Migracion SQL

La logica de autenticacion depende de `IIdentityStore`, no de archivos Excel directamente. Para migrar a SQL se debe implementar un store SQL con la misma interfaz y cambiar el registro de infraestructura, manteniendo contratos API, reglas de autorizacion y frontend.
