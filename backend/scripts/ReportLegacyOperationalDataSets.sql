\set ON_ERROR_STOP on

-- ReportLegacyOperationalDataSets
-- Read-only report. It can be run directly with:
--   psql -X -v ON_ERROR_STOP=1 "$CMMS_POSTGRES_CONNECTION" -f backend/scripts/ReportLegacyOperationalDataSets.sql
-- Redirect its output to a file under backups/ after the logical dumps have been verified.

CREATE TEMP TABLE IF NOT EXISTS legacy_operational_data_sets_report
(
    codigo text NOT NULL,
    tipo_json text NOT NULL,
    cantidad_elementos bigint NOT NULL,
    tamano_estimado_bytes bigint NOT NULL,
    created_at_utc timestamptz NULL,
    updated_at_utc timestamptz NULL,
    clasificacion text NOT NULL,
    tabla_relacional_destino text NULL,
    accion_recomendada text NOT NULL
) ON COMMIT PRESERVE ROWS;

TRUNCATE legacy_operational_data_sets_report;

DO $$
BEGIN
    IF to_regclass('public.conjuntos_datos_operacionales') IS NULL THEN
        RAISE NOTICE 'La tabla public.conjuntos_datos_operacionales no existe; no hay datos heredados JSONB que reportar.';
        RETURN;
    END IF;

    INSERT INTO legacy_operational_data_sets_report
    (
        codigo,
        tipo_json,
        cantidad_elementos,
        tamano_estimado_bytes,
        created_at_utc,
        updated_at_utc,
        clasificacion,
        tabla_relacional_destino,
        accion_recomendada
    )
    SELECT
        legacy.codigo,
        jsonb_typeof(legacy.contenido::jsonb),
        CASE jsonb_typeof(legacy.contenido::jsonb)
            WHEN 'array' THEN jsonb_array_length(legacy.contenido::jsonb)
            WHEN 'null' THEN 0
            ELSE 1
        END,
        pg_column_size(legacy.contenido::jsonb),
        legacy.created_at_utc,
        legacy.updated_at_utc,
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
        END,
        CASE
            WHEN lower(legacy.codigo) ~ '(^|[_-])(demo|test|sample|prueba)([_-]|$)'
                 OR lower(legacy.contenido::text) ~ '(^|[^[:alnum:]])(demo|test|sample|prueba)([^[:alnum:]]|$)'
                THEN 'Respaldar, conservar este reporte y eliminar solamente con ClearLegacyOperationalDataSets.sql.'
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
                THEN 'Confirmar que la coleccion ya fue reemplazada y eliminar solamente con ClearLegacyOperationalDataSets.sql.'
            WHEN lower(legacy.codigo) ~ '(usuario|rol|permiso|estado|tipo|catalog|familia|unidad_medida|plantilla|configur)'
                THEN 'Detener la limpieza: revisar y migrar el dato estructural antes de continuar.'
            ELSE 'Detener la limpieza: clasificar manualmente el dato antes de continuar.'
        END
    FROM public.conjuntos_datos_operacionales AS legacy;
END $$;

SELECT
    codigo,
    tipo_json,
    cantidad_elementos,
    tamano_estimado_bytes,
    pg_size_pretty(tamano_estimado_bytes) AS tamano_estimado,
    created_at_utc,
    updated_at_utc,
    clasificacion,
    tabla_relacional_destino,
    accion_recomendada
FROM legacy_operational_data_sets_report
ORDER BY clasificacion, codigo;

SELECT
    clasificacion,
    count(*) AS conjuntos,
    coalesce(sum(cantidad_elementos), 0) AS elementos,
    coalesce(sum(tamano_estimado_bytes), 0) AS tamano_estimado_bytes,
    pg_size_pretty(coalesce(sum(tamano_estimado_bytes), 0)) AS tamano_estimado
FROM legacy_operational_data_sets_report
GROUP BY clasificacion
ORDER BY clasificacion;
