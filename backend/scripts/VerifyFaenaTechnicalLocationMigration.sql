\pset pager off

\echo '=== Faena / technical-location post-migration verification ==='

\echo ''
\echo '--- Functional faena and technical-location data ---'
SELECT
    f.codigo,
    f.nombre,
    f.zona,
    f.cliente,
    f.centro_costes,
    f.tipo_faena,
    f.region,
    f.comuna,
    f.latitud,
    f.longitud,
    f.responsable_usuario_id,
    f.activo,
    ut.codigo AS ubicacion_tecnica_codigo,
    ut.nombre AS ubicacion_tecnica_nombre,
    ut.obsoleto AS ubicacion_tecnica_obsoleta
FROM faenas AS f
LEFT JOIN ubicaciones_tecnicas AS ut ON ut.faena_id = f.id
ORDER BY f.codigo;

\echo ''
\echo '--- One-to-one and responsible constraints ---'
SELECT conrelid::regclass AS tabla, conname, pg_get_constraintdef(oid) AS definicion
FROM pg_constraint
WHERE conrelid IN ('faenas'::regclass, 'ubicaciones_tecnicas'::regclass)
  AND conname IN (
      'FK_faenas_usuarios_responsable_usuario_id',
      'FK_ubicaciones_tecnicas_faenas_faena_id',
      'ck_faenas_latitud',
      'ck_faenas_longitud')
ORDER BY conrelid::regclass::text, conname;

\echo ''
\echo '--- Required indexes ---'
SELECT tablename, indexname, indexdef
FROM pg_indexes
WHERE schemaname = current_schema()
  AND indexname IN (
      'IX_faenas_responsable_usuario_id',
      'IX_ubicaciones_tecnicas_faena_id',
      'IX_ubicaciones_tecnicas_codigo')
ORDER BY indexname;

\echo ''
\echo '--- Removed redundant direct-location columns (must return zero rows) ---'
SELECT table_name, column_name
FROM information_schema.columns
WHERE table_schema = current_schema()
  AND table_name IN ('activos', 'unidades_operativas', 'nodos_tecnicos')
  AND column_name = 'ubicacion_tecnica_id';

\echo ''
\echo '--- Removed technical-location hierarchy columns (must return zero rows) ---'
SELECT column_name
FROM information_schema.columns
WHERE table_schema = current_schema()
  AND table_name = 'ubicaciones_tecnicas'
  AND column_name IN ('nombre_normalizado', 'ubicacion_padre_id', 'tipo', 'creado_por_usuario_id', 'actualizado_por_usuario_id')
ORDER BY column_name;

\echo ''
\echo '--- Deterministically repaired node ---'
SELECT n.codigo AS nodo_codigo, n.nombre AS nodo_nombre, f.codigo AS faena_codigo
FROM nodos_tecnicos AS n
LEFT JOIN faenas AS f ON f.id = n.faena_id
WHERE n.codigo = 'SIS-MOTOR';

\echo ''
\echo 'Verification complete.'
