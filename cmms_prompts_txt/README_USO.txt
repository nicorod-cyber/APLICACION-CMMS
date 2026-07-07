# Carpeta de prompts CMMS

Proyecto: Mantenimiento [Nombre Empresa]

## Cómo usar

1. Crea un repositorio vacío.
2. Ejecuta los prompts en orden numérico.
3. No avances al siguiente prompt si el anterior no compila o deja errores críticos.
4. Haz commit después de cada prompt.
5. Usa Excel como fuente inicial de datos.
6. Mantén la arquitectura preparada para SQL Server o PostgreSQL.

## Orden recomendado

Primero base técnica:
00 → 01 → 02 → 03 → 04 → 05 → 06

Luego módulos maestros:
07 → 08 → 09 → 10 → 11 → 12 → 13 → 14 → 15

Luego mantenimiento:
16 → 17 → 18 → 19 → 20 → 21 → 22

Luego reportes, offline y experiencia:
23 → 24 → 25 → 26 → 27 → 28 → 29 → 30 → 31

Luego infraestructura, seguridad y cierre:
32 → 33 → 34 → 35 → 36

## Nota clave

Excel será una fuente temporal y operativa inicial. La lógica del sistema debe quedar desacoplada mediante interfaces para migrar luego a SQL.
