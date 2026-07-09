# Rollback local

Este rollback es solo para desarrollo local.

## Detener entorno

```bash
docker compose down
```

## Reiniciar base local

```bash
docker compose down -v
docker compose up --build
```

Esto elimina el volumen `postgres-data`, recrea la base, aplica migraciones y ejecuta seeds de desarrollo.

## Volver temporalmente a Excel

Cambiar:

```bash
DataProvider__Provider=Excel
```

Ese modo queda como compatibilidad local. No representa la arquitectura objetivo.
