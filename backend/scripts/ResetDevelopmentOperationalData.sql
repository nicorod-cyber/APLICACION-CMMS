\set ON_ERROR_STOP on

-- ResetDevelopmentOperationalData
-- Destructive only for an explicitly marked development database.
-- First run PreviewDevelopmentOperationalReset.sql and verify custom pg_dump backups with pg_restore -l.
-- Then run:
--   psql -X -v ON_ERROR_STOP=1 \
--     -v cmms_environment=development \
--     -v backup_verified=true \
--     -v backup_file=/absolute/path/to/cmms-YYYYMMDD-HHMMSS.dump \
--     -v backup_sha256=<64-hex-sha256> \
--     -v confirm_reset_operational=RESET_DEVELOPMENT_OPERATIONAL_DATA \
--     "$CMMS_POSTGRES_CONNECTION" \
--     -f backend/scripts/ResetDevelopmentOperationalData.sql
--
-- The script uses PostgreSQL FK metadata to build a child-to-parent delete plan.
-- It never deletes identities, roles, permissions, master data, catalogs, generic
-- documents/files or audit history. Any unexpected FK from a preserved table aborts
-- the transaction instead of deleting through it.

\if :{?cmms_environment}
\else
    \echo 'Reset refused: pass -v cmms_environment=development.'
    \quit
\endif
\if :{?backup_verified}
\else
    \echo 'Reset refused: pass -v backup_verified=true after validating pg_restore -l.'
    \quit
\endif
\if :{?backup_file}
\else
    \echo 'Reset refused: pass -v backup_file=<verified custom dump path>.'
    \quit
\endif
\if :{?backup_sha256}
\else
    \echo 'Reset refused: pass -v backup_sha256=<verified SHA-256>.'
    \quit
\endif
\if :{?confirm_reset_operational}
\else
    \echo 'Reset refused: pass the explicit confirmation value.'
    \quit
\endif

BEGIN;

SELECT set_config('app.cmms_environment', :'cmms_environment', true);
SELECT set_config('app.cmms_backup_verified', :'backup_verified', true);
SELECT set_config('app.cmms_backup_file', :'backup_file', true);
SELECT set_config('app.cmms_backup_sha256', :'backup_sha256', true);
SELECT set_config('app.cmms_reset_confirmation', :'confirm_reset_operational', true);

CREATE TEMP TABLE IF NOT EXISTS reset_targets
(
    table_name text PRIMARY KEY
) ON COMMIT PRESERVE ROWS;

CREATE TEMP TABLE IF NOT EXISTS reset_preservation_snapshot
(
    table_name text PRIMARY KEY,
    rows_before bigint NOT NULL,
    rows_after bigint NULL
) ON COMMIT PRESERVE ROWS;

CREATE TEMP TABLE IF NOT EXISTS reset_fk_edges
(
    constraint_name text PRIMARY KEY,
    child_table text NOT NULL,
    parent_table text NOT NULL,
    child_oid oid NOT NULL,
    parent_oid oid NOT NULL,
    child_columns smallint[] NOT NULL
) ON COMMIT PRESERVE ROWS;

CREATE TEMP TABLE IF NOT EXISTS reset_detached_constraints
(
    constraint_name text PRIMARY KEY,
    child_table text NOT NULL,
    rows_detached bigint NOT NULL
) ON COMMIT PRESERVE ROWS;

CREATE TEMP TABLE IF NOT EXISTS reset_delete_plan
(
    table_name text PRIMARY KEY,
    delete_order integer NOT NULL
) ON COMMIT PRESERVE ROWS;

CREATE TEMP TABLE IF NOT EXISTS reset_summary
(
    table_name text PRIMARY KEY,
    delete_order integer NOT NULL,
    rows_before bigint NOT NULL,
    rows_deleted bigint NOT NULL,
    rows_after bigint NOT NULL
) ON COMMIT PRESERVE ROWS;

CREATE TEMP TABLE IF NOT EXISTS reset_demo_master_summary
(
    table_name text NOT NULL,
    code text NOT NULL,
    rows_deleted bigint NOT NULL,
    PRIMARY KEY (table_name, code)
) ON COMMIT PRESERVE ROWS;

TRUNCATE reset_targets;
TRUNCATE reset_preservation_snapshot;
TRUNCATE reset_fk_edges;
TRUNCATE reset_detached_constraints;
TRUNCATE reset_delete_plan;
TRUNCATE reset_summary;
TRUNCATE reset_demo_master_summary;

INSERT INTO reset_targets (table_name)
VALUES
    ('notificacion_intentos'),
    ('notificacion_destinatarios'),
    ('notificaciones'),
    ('alertas'),
    ('detalle_recepcion_abastecimiento'),
    ('detalle_orden_compra'),
    ('recepciones_abastecimiento'),
    ('ordenes_compra'),
    ('detalle_solicitud_abastecimiento'),
    ('solicitudes_abastecimiento'),
    ('dependencias_programacion'),
    ('alertas_programacion'),
    ('programaciones_ot'),
    ('historial_preventivo'),
    ('evaluaciones_preventivas'),
    ('alcances_plan_preventivo'),
    ('planes_preventivos_sql'),
    ('eventos_disponibilidad'),
    ('contrato_disponibilidad_objetivos'),
    ('contratos_disponibilidad'),
    ('errores_importacion'),
    ('filas_importacion'),
    ('eventos_importacion'),
    ('importaciones'),
    ('solicitud_repuesto_historial'),
    ('solicitud_repuesto_items'),
    ('solicitudes_repuestos'),
    ('ot_checklists_sql'),
    ('ot_firmas_sql'),
    ('ot_evidencias_sql'),
    ('ot_estado_historial_sql'),
    ('ot_tecnicos_tarea_sql'),
    ('ot_hh_sql'),
    ('ot_repuestos_sql'),
    ('tareas_ot_sql'),
    ('documento_ordenes_trabajo'),
    ('orden_trabajo_activos'),
    ('avisos_trabajo_sql'),
    ('costos'),
    ('estados_pago'),
    ('movimientos_stock'),
    ('reservas_stock'),
    ('transferencias_stock'),
    ('documento_activos'),
    ('nodo_tecnico_activos'),
    ('valores_atributo_activo'),
    ('eventos_estado_activo'),
    ('lecturas_activo'),
    ('componentes_unidad_operativa'),
    ('unidades_operativas'),
    ('ordenes_trabajo_sql'),
    ('activos')
ON CONFLICT DO NOTHING;

DELETE FROM reset_targets
WHERE to_regclass('public.' || table_name) IS NULL;

DO $$
DECLARE
    candidate text;
    count_rows bigint;
    protected_overlap text;
    external_fk text;
    legacy_has_rows boolean := false;
BEGIN
    IF lower(current_setting('app.cmms_environment', true)) IS DISTINCT FROM 'development' THEN
        RAISE EXCEPTION 'Reset refused: app.cmms_environment must be development.';
    END IF;

    IF current_database() ~* '(prod|production|prd)' THEN
        RAISE EXCEPTION 'Reset refused: database "%" appears to be production.', current_database();
    END IF;

    IF lower(current_setting('app.cmms_backup_verified', true)) IS DISTINCT FROM 'true' THEN
        RAISE EXCEPTION 'Reset refused: the verified backup flag is missing.';
    END IF;

    IF nullif(btrim(current_setting('app.cmms_backup_file', true)), '') IS NULL THEN
        RAISE EXCEPTION 'Reset refused: the backup file reference is empty.';
    END IF;

    IF current_setting('app.cmms_backup_sha256', true) !~ '^[0-9A-Fa-f]{64}$' THEN
        RAISE EXCEPTION 'Reset refused: backup_sha256 must be a 64-character SHA-256 value.';
    END IF;

    IF current_setting('app.cmms_reset_confirmation', true)
        IS DISTINCT FROM 'RESET_DEVELOPMENT_OPERATIONAL_DATA' THEN
        RAISE EXCEPTION 'Reset refused: explicit confirmation does not match.';
    END IF;

    IF to_regclass('public.conjuntos_datos_operacionales') IS NOT NULL THEN
        EXECUTE 'SELECT EXISTS (SELECT 1 FROM public.conjuntos_datos_operacionales)'
        INTO legacy_has_rows;

        IF legacy_has_rows THEN
            RAISE EXCEPTION
                'Reset refused: clear public.conjuntos_datos_operacionales with ClearLegacyOperationalDataSets.sql and apply EF migration first.';
        END IF;
    END IF;

    SELECT string_agg(table_name, ', ' ORDER BY table_name)
    INTO protected_overlap
    FROM reset_targets
    WHERE table_name IN
    (
        'usuarios',
        'roles',
        'permisos',
        'rol_permisos',
        'usuario_roles',
        'usuario_faenas',
        'estados_operacionales_activo',
        'tipos_activo',
        'familias_equipo',
        'tipos_unidad_operativa',
        'roles_componente_unidad',
        'tipos_documentales',
        'requisitos_documentales_tipo_activo',
        'catalogos_trabajo',
        'catalogos_inventario',
        'plantillas_checklist',
        'items_plantilla_checklist',
        'plantillas_pdf',
        'faenas',
        'bodegas',
        'repuestos',
        'proveedores',
        'nodos_tecnicos',
        'ubicaciones_tecnicas',
        'stock_bodega',
        'documentos',
        'archivos',
        'audit_log'
    );

    IF protected_overlap IS NOT NULL THEN
        RAISE EXCEPTION 'Reset refused: protected tables were mistakenly selected: %', protected_overlap;
    END IF;

    FOREACH candidate IN ARRAY ARRAY[
        'usuarios',
        'roles',
        'permisos',
        'rol_permisos',
        'usuario_roles',
        'usuario_faenas',
        'estados_operacionales_activo',
        'tipos_activo',
        'familias_equipo',
        'tipos_unidad_operativa',
        'roles_componente_unidad',
        'tipos_documentales',
        'requisitos_documentales_tipo_activo',
        'catalogos_trabajo',
        'catalogos_inventario',
        'plantillas_checklist',
        'items_plantilla_checklist',
        'plantillas_pdf',
        'faenas',
        'bodegas',
        'repuestos',
        'proveedores',
        'nodos_tecnicos',
        'ubicaciones_tecnicas',
        'stock_bodega',
        'documentos',
        'archivos',
        'audit_log'
    ]
    LOOP
        IF to_regclass('public.' || candidate) IS NOT NULL THEN
            EXECUTE format('SELECT count(*) FROM public.%I', candidate) INTO count_rows;
            INSERT INTO reset_preservation_snapshot (table_name, rows_before)
            VALUES (candidate, count_rows)
            ON CONFLICT (table_name) DO UPDATE SET rows_before = EXCLUDED.rows_before;
        END IF;
    END LOOP;

    SELECT string_agg(
        format(
            '%I.%I -> %I.%I via %I',
            child_namespace.nspname,
            child_table.relname,
            parent_namespace.nspname,
            parent_table.relname,
            constraint_row.conname
        ),
        E'\n'
        ORDER BY child_table.relname, constraint_row.conname
    )
    INTO external_fk
    FROM pg_constraint AS constraint_row
    JOIN pg_class AS child_table
        ON child_table.oid = constraint_row.conrelid
    JOIN pg_namespace AS child_namespace
        ON child_namespace.oid = child_table.relnamespace
    JOIN pg_class AS parent_table
        ON parent_table.oid = constraint_row.confrelid
    JOIN pg_namespace AS parent_namespace
        ON parent_namespace.oid = parent_table.relnamespace
    JOIN reset_targets AS parent_target
        ON parent_target.table_name = parent_table.relname
    LEFT JOIN reset_targets AS child_target
        ON child_target.table_name = child_table.relname
    WHERE constraint_row.contype = 'f'
      AND child_namespace.nspname = 'public'
      AND parent_namespace.nspname = 'public'
      AND child_target.table_name IS NULL;

    IF external_fk IS NOT NULL THEN
        RAISE EXCEPTION
            'Reset refused: preserved or unknown tables still reference reset targets. Add an explicit safe rule first:%',
            E'\n' || external_fk;
    END IF;
END $$;

INSERT INTO reset_fk_edges
(
    constraint_name,
    child_table,
    parent_table,
    child_oid,
    parent_oid,
    child_columns
)
SELECT
    constraint_row.conname,
    child_table.relname,
    parent_table.relname,
    child_table.oid,
    parent_table.oid,
    constraint_row.conkey
FROM pg_constraint AS constraint_row
JOIN pg_class AS child_table
    ON child_table.oid = constraint_row.conrelid
JOIN pg_namespace AS child_namespace
    ON child_namespace.oid = child_table.relnamespace
JOIN pg_class AS parent_table
    ON parent_table.oid = constraint_row.confrelid
JOIN pg_namespace AS parent_namespace
    ON parent_namespace.oid = parent_table.relnamespace
JOIN reset_targets AS child_target
    ON child_target.table_name = child_table.relname
JOIN reset_targets AS parent_target
    ON parent_target.table_name = parent_table.relname
WHERE constraint_row.contype = 'f'
  AND child_namespace.nspname = 'public'
  AND parent_namespace.nspname = 'public';

-- Two verified operational cycles are broken through their nullable link columns only:
-- ordenes_trabajo_sql.aviso_id (OT <-> aviso) and
-- lecturas_activo.lectura_corregida_id (self-reference). Other nullable FKs remain
-- in the topological plan because a nullable column can still be constrained by
-- business checks and must not be nulled generically.
DO $$
DECLARE
    edge record;
    assignments text;
    predicate text;
    updated_rows bigint;
BEGIN
    FOR edge IN
        SELECT fk_edge.*
        FROM reset_fk_edges AS fk_edge
        WHERE
              (
                  (
                      fk_edge.child_table = 'ordenes_trabajo_sql'
                  AND fk_edge.parent_table = 'avisos_trabajo_sql'
                  AND EXISTS
                      (
                          SELECT 1
                          FROM reset_fk_edges AS reciprocal
                          WHERE reciprocal.child_table = fk_edge.parent_table
                            AND reciprocal.parent_table = fk_edge.child_table
                      )
                  AND EXISTS
                      (
                          SELECT 1
                          FROM unnest(fk_edge.child_columns) AS key_column(attnum)
                          JOIN pg_attribute AS attribute_row
                              ON attribute_row.attrelid = fk_edge.child_oid
                             AND attribute_row.attnum = key_column.attnum
                          WHERE attribute_row.attname = 'aviso_id'
                      )
              )
              OR
              (
                  fk_edge.child_table = 'lecturas_activo'
                  AND fk_edge.parent_table = 'lecturas_activo'
                  AND EXISTS
                      (
                          SELECT 1
                          FROM unnest(fk_edge.child_columns) AS key_column(attnum)
                          JOIN pg_attribute AS attribute_row
                              ON attribute_row.attrelid = fk_edge.child_oid
                             AND attribute_row.attnum = key_column.attnum
                          WHERE attribute_row.attname = 'lectura_corregida_id'
                      )
                  )
              )
          AND NOT EXISTS
              (
                  SELECT 1
                  FROM unnest(fk_edge.child_columns) AS key_column(attnum)
                  JOIN pg_attribute AS attribute_row
                      ON attribute_row.attrelid = fk_edge.child_oid
                     AND attribute_row.attnum = key_column.attnum
                  WHERE attribute_row.attnotnull
              )
        ORDER BY fk_edge.constraint_name
    LOOP
        SELECT
            string_agg(format('%I = NULL', attribute_row.attname), ', ' ORDER BY key_column.ordinality),
            string_agg(format('%I IS NOT NULL', attribute_row.attname), ' OR ' ORDER BY key_column.ordinality)
        INTO assignments, predicate
        FROM unnest(edge.child_columns) WITH ORDINALITY AS key_column(attnum, ordinality)
        JOIN pg_attribute AS attribute_row
            ON attribute_row.attrelid = edge.child_oid
           AND attribute_row.attnum = key_column.attnum;

        EXECUTE format(
            'UPDATE public.%I SET %s WHERE %s',
            edge.child_table,
            assignments,
            predicate
        );
        GET DIAGNOSTICS updated_rows = ROW_COUNT;

        INSERT INTO reset_detached_constraints (constraint_name, child_table, rows_detached)
        VALUES (edge.constraint_name, edge.child_table, updated_rows);
    END LOOP;
END $$;

-- Kahn-style child-to-parent plan. A remaining table without a plan indicates a
-- non-nullable FK cycle; the exception rolls the complete transaction back.
DO $$
DECLARE
    plan_order integer := 0;
    added_rows bigint;
    unresolved_tables text;
BEGIN
    LOOP
        INSERT INTO reset_delete_plan (table_name, delete_order)
        SELECT target.table_name, plan_order
        FROM reset_targets AS target
        WHERE NOT EXISTS
              (
                  SELECT 1
                  FROM reset_delete_plan AS planned
                  WHERE planned.table_name = target.table_name
              )
          AND NOT EXISTS
              (
                  SELECT 1
                  FROM reset_fk_edges AS edge
                  LEFT JOIN reset_detached_constraints AS detached
                      ON detached.constraint_name = edge.constraint_name
                  WHERE edge.parent_table = target.table_name
                    AND detached.constraint_name IS NULL
                    AND NOT EXISTS
                        (
                            SELECT 1
                            FROM reset_delete_plan AS planned_child
                            WHERE planned_child.table_name = edge.child_table
                        )
              );

        GET DIAGNOSTICS added_rows = ROW_COUNT;
        EXIT WHEN added_rows = 0;
        plan_order := plan_order + 1;
    END LOOP;

    IF (SELECT count(*) FROM reset_delete_plan) <> (SELECT count(*) FROM reset_targets) THEN
        SELECT string_agg(target.table_name, ', ' ORDER BY target.table_name)
        INTO unresolved_tables
        FROM reset_targets AS target
        LEFT JOIN reset_delete_plan AS planned
            ON planned.table_name = target.table_name
        WHERE planned.table_name IS NULL;

        RAISE EXCEPTION
            'Reset refused: unresolved non-nullable FK cycle or dependency among: %',
            unresolved_tables;
    END IF;
END $$;

DO $$
DECLARE
    target record;
    before_rows bigint;
    deleted_rows bigint;
    after_rows bigint;
BEGIN
    FOR target IN
        SELECT table_name, delete_order
        FROM reset_delete_plan
        ORDER BY delete_order, table_name
    LOOP
        EXECUTE format('SELECT count(*) FROM public.%I', target.table_name) INTO before_rows;
        EXECUTE format('DELETE FROM public.%I', target.table_name);
        GET DIAGNOSTICS deleted_rows = ROW_COUNT;
        EXECUTE format('SELECT count(*) FROM public.%I', target.table_name) INTO after_rows;

        INSERT INTO reset_summary
        (
            table_name,
            delete_order,
            rows_before,
            rows_deleted,
            rows_after
        )
        VALUES
        (
            target.table_name,
            target.delete_order,
            before_rows,
            deleted_rows,
            after_rows
        );
    END LOOP;
END $$;

-- FAENA_DEMO is created exclusively by SeedDemoDataAsync. It is the only master
-- removed by this reset, and only after every live PostgreSQL FK has been checked.
DO $$
DECLARE
    demo_faena_id uuid;
    reference_row record;
    dependency_count bigint;
    dependencies text;
    deleted_rows bigint := 0;
BEGIN
    IF to_regclass('public.faenas') IS NULL THEN
        RETURN;
    END IF;

    SELECT id
    INTO demo_faena_id
    FROM public.faenas
    WHERE codigo = 'FAENA_DEMO';

    IF demo_faena_id IS NULL THEN
        RETURN;
    END IF;

    IF EXISTS
    (
        SELECT 1
        FROM pg_constraint AS constraint_row
        JOIN pg_class AS parent_table
            ON parent_table.oid = constraint_row.confrelid
        JOIN pg_namespace AS namespace_row
            ON namespace_row.oid = constraint_row.connamespace
        WHERE constraint_row.contype = 'f'
          AND namespace_row.nspname = 'public'
          AND parent_table.relname = 'faenas'
          AND cardinality(constraint_row.conkey) <> 1
    ) THEN
        RAISE EXCEPTION
            'Reset refused: a composite FK to faenas requires an explicit FAENA_DEMO cleanup rule.';
    END IF;

    FOR reference_row IN
        SELECT child_table.relname AS child_table, attribute_row.attname AS child_column
        FROM pg_constraint AS constraint_row
        JOIN pg_class AS child_table
            ON child_table.oid = constraint_row.conrelid
        JOIN pg_class AS parent_table
            ON parent_table.oid = constraint_row.confrelid
        JOIN pg_namespace AS namespace_row
            ON namespace_row.oid = constraint_row.connamespace
        JOIN unnest(constraint_row.conkey) AS key_column(attnum)
            ON true
        JOIN pg_attribute AS attribute_row
            ON attribute_row.attrelid = child_table.oid
           AND attribute_row.attnum = key_column.attnum
        WHERE constraint_row.contype = 'f'
          AND namespace_row.nspname = 'public'
          AND parent_table.relname = 'faenas'
    LOOP
        EXECUTE format(
            'SELECT count(*) FROM public.%I WHERE %I = $1',
            reference_row.child_table,
            reference_row.child_column
        )
        INTO dependency_count
        USING demo_faena_id;

        IF dependency_count <> 0 THEN
            dependencies := concat_ws(
                ', ',
                dependencies,
                format('%I.%I=%s', reference_row.child_table, reference_row.child_column, dependency_count)
            );
        END IF;
    END LOOP;

    IF dependencies IS NOT NULL THEN
        RAISE EXCEPTION
            'Reset refused: FAENA_DEMO still has dependent rows: %',
            dependencies;
    END IF;

    DELETE FROM public.faenas
    WHERE id = demo_faena_id;
    GET DIAGNOSTICS deleted_rows = ROW_COUNT;

    INSERT INTO reset_demo_master_summary (table_name, code, rows_deleted)
    VALUES ('faenas', 'FAENA_DEMO', deleted_rows);
END $$;

DO $$
DECLARE
    preserved record;
    remaining_operational text;
    removed_structural text;
    count_rows bigint;
BEGIN
    SELECT string_agg(format('%s=%s', table_name, rows_after), ', ' ORDER BY table_name)
    INTO remaining_operational
    FROM reset_summary
    WHERE rows_after <> 0;

    IF remaining_operational IS NOT NULL THEN
        RAISE EXCEPTION
            'Reset failed: operational rows remain after the delete plan: %',
            remaining_operational;
    END IF;

    FOR preserved IN SELECT table_name FROM reset_preservation_snapshot ORDER BY table_name LOOP
        EXECUTE format('SELECT count(*) FROM public.%I', preserved.table_name) INTO count_rows;
        UPDATE reset_preservation_snapshot
        SET rows_after = count_rows
        WHERE table_name = preserved.table_name;
    END LOOP;

    SELECT string_agg(table_name, ', ' ORDER BY table_name)
    INTO removed_structural
    FROM reset_preservation_snapshot
    WHERE rows_before > 0
      AND rows_after = 0;

    IF removed_structural IS NOT NULL THEN
        RAISE EXCEPTION
            'Reset failed: protected structural data was removed completely: %',
            removed_structural;
    END IF;
END $$;

SELECT current_database() AS database_name,
       current_setting('app.cmms_backup_file', true) AS backup_file,
       current_setting('app.cmms_backup_sha256', true) AS backup_sha256,
       clock_timestamp() AS completed_at_utc;

SELECT constraint_name, child_table, rows_detached
FROM reset_detached_constraints
ORDER BY constraint_name;

SELECT table_name, delete_order, rows_before, rows_deleted, rows_after
FROM reset_summary
ORDER BY delete_order, table_name;

SELECT table_name, code, rows_deleted
FROM reset_demo_master_summary
ORDER BY table_name, code;

SELECT table_name, rows_before, rows_after
FROM reset_preservation_snapshot
ORDER BY table_name;

COMMIT;
