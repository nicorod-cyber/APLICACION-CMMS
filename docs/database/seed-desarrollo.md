# Seed de desarrollo

El seed PostgreSQL se ejecuta con `Database__SeedDevelopment=true`.

## Datos incluidos

- Faena `FAENA_DEMO`.
- Estados operacionales oficiales:
  - `OPERATIVO_FAENA`
  - `ALERTA_FAENA`
  - `FUERA_SERVICIO_FAENA`
  - `FUERA_SERVICIO_TALLER`
- Familias:
  - `CAMION_PLUMA`
  - `COMPRESOR`
  - `GRUA_HORQUILLA`
- Permiso `familias_equipo.gestionar`.
- Activo demo `ACT-DEMO-001`.
- Roles y administrador inicial mediante `IdentitySeedService`.

## Idempotencia

Los registros se buscan por codigo o username antes de insertarse. Ejecutar el seed varias veces no debe duplicar filas.

## Variables de administrador

```bash
Auth__SeedAdmin__Username=admin
Auth__SeedAdmin__Email=admin@example.local
Auth__SeedAdmin__Password=<set from a secret or environment variable>
```
