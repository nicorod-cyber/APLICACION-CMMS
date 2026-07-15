\set ON_ERROR_STOP on

-- ClearLegacyOperationalDataSets
-- Destructive only for an explicitly marked development session.
-- Before execution create and verify both custom dumps with pg_restore -l, then run:
--   psql -X -v ON_ERROR_STOP=1 \
--     -v cmms_environment=development \
--     -v backup_verified=true \
--     -v backup_file=/absolute/path/to/cmms-YYYYMMDD-HHMMSS.dump \
--     -v backup_sha256=<64-hex-sha256> \
--     -v confirm_clear_legacy=DELETE_LEGACY_OPERATIONAL_DATA \
--     "$CMMS_POSTGRES_CONNECTION" \
--     -f backend/scripts/ClearLegacyOperationalDataSets.sql
--
-- PostgreSQL cannot inspect a client-side backup path. The mandatory backup_file,
-- backup_sha256 and backup_verified values record the already verified external backup
-- in the transaction output; do not set them before pg_restore -l succeeds.

\if :{?cmms_environment}
\else
    \echo 'Clear refused: pass -v cmms_environment=development.'
    \quit
\endif
\if :{?backup_verified}
\else
    \echo 'Clear refused: pass -v backup_verified=true after validating pg_restore -l.'
    \quit
\endif
\if :{?backup_file}
\else
    \echo 'Clear refused: pass -v backup_file=<verified custom dump path>.'
    \quit
\endif
\if :{?backup_sha256}
\else
    \echo 'Clear refused: pass -v backup_sha256=<verified SHA-256>.'
    \quit
\endif
\if :{?confirm_clear_legacy}
\else
    \echo 'Clear refused: pass the explicit confirmation value.'
    \quit
\endif

BEGIN;

SELECT set_config('app.cmms_environment', :'cmms_environment', true);
SELECT set_config('app.cmms_backup_verified', :'backup_verified', true);
SELECT set_config('app.cmms_backup_file', :'backup_file', true);
SELECT set_config('app.cmms_backup_sha256', :'backup_sha256', true);
SELECT set_config('app.cmms_clear_legacy_confirmation', :'confirm_clear_legacy', true);

CREATE TEMP TABLE IF NOT EXISTS legacy_clear_classification
(
    codigo text NOT NULL,
    contenido jsonb NOT NULL,
    clasificacion text NOT NULL,
    tabla_relacional_destino text NULL
) ON COMMIT PRESERVE ROWS;

CREATE TEMP TABLE IF NOT EXISTS legacy_clear_summary
(
    database_name text NOT NULL,
    backup_file text NOT NULL,
    backup_sha256 text NOT NULL,
    rows_before bigint NOT NULL,
    rows_deleted bigint NOT NULL,
    rows_after bigint NOT NULL,
    executed_at_utc timestamptz NOT NULL
) ON COMMIT PRESERVE ROWS;

TRUNCATE legacy_clear_classification;
TRUNCATE legacy_clear_summary;

DO $$
BEGIN
    IF lower(current_setting('app.cmms_environment', true)) IS DISTINCT FROM 'development' THEN
        RAISE EXCEPTION 'Clear refused: app.cmms_environment must be development.';
    END IF;

    IF current_database() ~* '(prod|production|prd)' THEN
        RAISE EXCEPTION 'Clear refused: database "%" appears to be production.', current_database();
    END IF;

    IF lower(current_setting('app.cmms_backup_verified', true)) IS DISTINCT FROM 'true' THEN
        RAISE EXCEPTION 'Clear refused: the verified backup flag is missing.';
    END IF;

    IF nullif(btrim(current_setting('app.cmms_backup_file', true)), '') IS NULL THEN
        RAISE EXCEPTION 'Clear refused: the backup file reference is empty.';
    END IF;

    IF current_setting('app.cmms_backup_sha256', true) !~ '^[0-9A-Fa-f]{64}$' THEN
        RAISE EXCEPTION 'Clear refused: backup_sha256 must be a 64-character SHA-256 value.';
    END IF;

    IF current_setting('app.cmms_clear_legacy_confirmation', true)
        IS DISTINCT FROM 'DELETE_LEGACY_OPERATIONAL_DATA' THEN
        RAISE EXCEPTION 'Clear refused: explicit confirmation does not match.';
    END IF;

    IF to_regclass('public.conjuntos_datos_operacionales') IS NULL THEN
        RAISE NOTICE 'Legacy table does not exist; there is nothing to clear.';
        RETURN;
    END IF;

    INSERT INTO legacy_clear_classification (codigo, contenido, clasificacion, tabla_relacional_destino)
    SELECT
        legacy.codigo,
        legacy.contenido::jsonb,
        CASE
            WHEN lower(legacy.codigo) ~ '(^|[_-])(demo|test|sample|prueba)([_-]|$)'
                 OR lower(legacy.contenido::text) ~ '(^|[^[:alnum:]])(demo|test|sample|prueba)([^[:alnum:]]|$)'
                THEN 'dato_operacional_de_prueba_descartable'
            WHEN lower(legacy.codigo) IN
                (
                    'disponibilidad_contratos',
                    'eventos_disponibilidad',
                    'planes_preventivos',
                    'preventivo_lecturas',
                    'programacion_ot',
                    'abastecimiento_solicitudes',
                    'importaciones'
                )
                THEN 'coleccion_ya_reemplazada_por_tablas_relacionales'
            WHEN lower(legacy.codigo) ~ '(usuario|rol|permiso|estado|tipo|catalog|familia|unidad_medida|plantilla|configur)'
                THEN 'dato_estructural_migrable'
            ELSE 'dato_desconocido_requiere_revision'
        END,
        CASE lower(legacy.codigo)
            WHEN 'disponibilidad_contratos' THEN 'contratos_disponibilidad'
            WHEN 'eventos_disponibilidad' THEN 'eventos_disponibilidad'
            WHEN 'planes_preventivos' THEN 'planes_preventivos_sql'
            WHEN 'preventivo_lecturas' THEN 'lecturas_activo'
            WHEN 'programacion_ot' THEN 'programaciones_ot'
            WHEN 'abastecimiento_solicitudes' THEN 'solicitudes_abastecimiento'
            WHEN 'importaciones' THEN 'importaciones'
            ELSE NULL
        END
    FROM public.conjuntos_datos_operacionales AS legacy;
END $$;

SELECT current_database() AS database_name,
       count(*) AS legacy_rows,
       coalesce(string_agg(codigo, ', ' ORDER BY codigo), '(sin registros)') AS codigos_existentes,
       current_setting('app.cmms_backup_file', true) AS backup_file,
       current_setting('app.cmms_backup_sha256', true) AS backup_sha256
FROM legacy_clear_classification;

SELECT codigo, clasificacion, tabla_relacional_destino, contenido
FROM legacy_clear_classification
ORDER BY codigo;

DO $$
DECLARE
    before_rows bigint;
    deleted_rows bigint := 0;
    after_rows bigint := 0;
    blocked_codes text;
BEGIN
    SELECT string_agg(codigo, ', ' ORDER BY codigo)
    INTO blocked_codes
    FROM legacy_clear_classification
    WHERE clasificacion IN ('dato_estructural_migrable', 'dato_desconocido_requiere_revision');

    IF blocked_codes IS NOT NULL THEN
        RAISE EXCEPTION
            'Clear refused: structural or unknown legacy codes require review before deletion: %',
            blocked_codes;
    END IF;

    SELECT count(*) INTO before_rows FROM legacy_clear_classification;

    IF to_regclass('public.conjuntos_datos_operacionales') IS NOT NULL THEN
        DELETE FROM public.conjuntos_datos_operacionales;
        GET DIAGNOSTICS deleted_rows = ROW_COUNT;
        SELECT count(*) INTO after_rows FROM public.conjuntos_datos_operacionales;
    END IF;

    IF after_rows <> 0 THEN
        RAISE EXCEPTION 'Clear failed: legacy table still contains % rows.', after_rows;
    END IF;

    INSERT INTO legacy_clear_summary
    (
        database_name,
        backup_file,
        backup_sha256,
        rows_before,
        rows_deleted,
        rows_after,
        executed_at_utc
    )
    VALUES
    (
        current_database(),
        current_setting('app.cmms_backup_file', true),
        current_setting('app.cmms_backup_sha256', true),
        before_rows,
        deleted_rows,
        after_rows,
        clock_timestamp()
    );
END $$;

SELECT * FROM legacy_clear_summary;

COMMIT;
