\pset pager off

\echo '=== Faena / technical-location preflight ==='
\echo 'This script is read-only. Do not apply the migration while blocking sections return rows.'

\echo ''
\echo '--- Cardinality by faena (zero or more than one technical location) ---'
SELECT
    f.id AS faena_id,
    f.codigo AS faena_codigo,
    f.nombre AS faena_nombre,
    COUNT(ut.id) AS ubicaciones_tecnicas
FROM faenas AS f
LEFT JOIN ubicaciones_tecnicas AS ut ON ut.faena_id = f.id
GROUP BY f.id, f.codigo, f.nombre
HAVING COUNT(ut.id) <> 1
ORDER BY f.codigo;

\echo ''
\echo '--- More than one technical location per faena (BLOCKS unique faena_id) ---'
SELECT
    f.id AS faena_id,
    f.codigo AS faena_codigo,
    COUNT(ut.id) AS ubicaciones_tecnicas,
    string_agg(format('%s (%s)', ut.codigo, ut.id), ', ' ORDER BY ut.codigo) AS detalle
FROM faenas AS f
INNER JOIN ubicaciones_tecnicas AS ut ON ut.faena_id = f.id
GROUP BY f.id, f.codigo
HAVING COUNT(ut.id) > 1
ORDER BY f.codigo;

\echo ''
\echo '--- Duplicate technical-location codes (BLOCKS code uniqueness) ---'
SELECT codigo, COUNT(*) AS repeticiones, string_agg(id::text, ', ' ORDER BY id) AS ids
FROM ubicaciones_tecnicas
GROUP BY codigo
HAVING COUNT(*) > 1
ORDER BY codigo;

\echo ''
\echo '--- Orphan technical locations (BLOCKS derivation) ---'
SELECT ut.id, ut.codigo, ut.nombre, ut.faena_id
FROM ubicaciones_tecnicas AS ut
LEFT JOIN faenas AS f ON f.id = ut.faena_id
WHERE ut.faena_id IS NULL OR f.id IS NULL
ORDER BY ut.codigo;

\echo ''
\echo '--- Deterministic technical-node faena repairs (will be applied by the migration) ---'
SELECT
    n.id AS nodo_id,
    n.codigo AS nodo_codigo,
    n.nombre AS nodo_nombre,
    f.codigo AS faena_actual_codigo,
    ut.codigo AS ubicacion_codigo,
    utf.codigo AS faena_de_ubicacion_codigo,
    parent.codigo AS nodo_padre_codigo,
    parent_faena.codigo AS faena_de_nodo_padre_codigo
FROM nodos_tecnicos AS n
INNER JOIN ubicaciones_tecnicas AS ut ON ut.id = n.ubicacion_tecnica_id
INNER JOIN faenas AS f ON f.id = n.faena_id
INNER JOIN faenas AS utf ON utf.id = ut.faena_id
INNER JOIN nodos_tecnicos AS parent ON parent.id = n.nodo_padre_id
INNER JOIN faenas AS parent_faena ON parent_faena.id = parent.faena_id
WHERE n.faena_id <> ut.faena_id
  AND parent.faena_id = ut.faena_id
ORDER BY n.codigo;

\echo ''
\echo '--- Residual direct links conflicting with their faena (BLOCKS migration) ---'
SELECT *
FROM (
    SELECT 'activo' AS origen, a.id AS registro_id, a.faena_id AS faena_id,
           a.ubicacion_tecnica_id AS ubicacion_tecnica_id, ut.faena_id AS faena_de_ubicacion_id
    FROM activos AS a
    LEFT JOIN ubicaciones_tecnicas AS ut ON ut.id = a.ubicacion_tecnica_id
    WHERE a.ubicacion_tecnica_id IS NOT NULL
      AND (ut.faena_id IS NULL OR (a.faena_id IS NOT NULL AND a.faena_id <> ut.faena_id))
    UNION ALL
    SELECT 'unidad_operativa', u.id, u.faena_id, u.ubicacion_tecnica_id, ut.faena_id
    FROM unidades_operativas AS u
    LEFT JOIN ubicaciones_tecnicas AS ut ON ut.id = u.ubicacion_tecnica_id
    WHERE u.ubicacion_tecnica_id IS NOT NULL
      AND (ut.faena_id IS NULL OR (u.faena_id IS NOT NULL AND u.faena_id <> ut.faena_id))
    UNION ALL
    SELECT 'nodo_tecnico', n.id, n.faena_id, n.ubicacion_tecnica_id, ut.faena_id
    FROM nodos_tecnicos AS n
    LEFT JOIN ubicaciones_tecnicas AS ut ON ut.id = n.ubicacion_tecnica_id
    LEFT JOIN nodos_tecnicos AS parent ON parent.id = n.nodo_padre_id
    WHERE n.ubicacion_tecnica_id IS NOT NULL
      AND (
          ut.faena_id IS NULL
          OR (
              n.faena_id IS NOT NULL
              AND n.faena_id <> ut.faena_id
              AND (parent.faena_id IS NULL OR parent.faena_id <> ut.faena_id)
          )
      )
) AS conflictos
ORDER BY origen, registro_id;

\echo ''
\echo '--- Deterministic faena backfill candidates (informational) ---'
SELECT origen, COUNT(*) AS registros
FROM (
    SELECT 'activo' AS origen
    FROM activos AS a
    INNER JOIN ubicaciones_tecnicas AS ut ON ut.id = a.ubicacion_tecnica_id
    WHERE a.faena_id IS NULL
    UNION ALL
    SELECT 'unidad_operativa'
    FROM unidades_operativas AS u
    INNER JOIN ubicaciones_tecnicas AS ut ON ut.id = u.ubicacion_tecnica_id
    WHERE u.faena_id IS NULL
    UNION ALL
    SELECT 'nodo_tecnico'
    FROM nodos_tecnicos AS n
    INNER JOIN ubicaciones_tecnicas AS ut ON ut.id = n.ubicacion_tecnica_id
    WHERE n.faena_id IS NULL
) AS candidatos
GROUP BY origen
ORDER BY origen;

\echo ''
\echo '--- Legacy technical-location metadata that will be intentionally removed ---'
SELECT
    COUNT(*) FILTER (WHERE ubicacion_padre_id IS NOT NULL) AS relaciones_padre,
    COUNT(*) FILTER (WHERE NULLIF(BTRIM(tipo), '') IS NOT NULL) AS tipos_con_valor,
    COUNT(*) FILTER (WHERE NULLIF(BTRIM(creado_por_usuario_id), '') IS NOT NULL) AS creados_por_con_valor,
    COUNT(*) FILTER (WHERE NULLIF(BTRIM(actualizado_por_usuario_id), '') IS NOT NULL) AS actualizados_por_con_valor
FROM ubicaciones_tecnicas;

\echo ''
\echo 'Preflight complete.'
