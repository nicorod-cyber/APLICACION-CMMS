\set ON_ERROR_STOP on

-- PreviewDevelopmentOperationalReset
-- Read-only. It can be run directly with:
--   psql -X -v ON_ERROR_STOP=1 "$CMMS_POSTGRES_CONNECTION" -f backend/scripts/PreviewDevelopmentOperationalReset.sql
-- It deliberately reports missing post-migration tables as zero rather than failing.

DO $$
DECLARE
    database_name text := current_database();
BEGIN
    IF database_name ~* '(prod|production|prd)' THEN
        RAISE EXCEPTION 'Preview refused: database "%" appears to be production.', database_name;
    END IF;
END $$;

CREATE TEMP TABLE IF NOT EXISTS reset_preview
(
    categoria text NOT NULL,
    table_name text NOT NULL,
    existe boolean NOT NULL,
    row_count bigint NOT NULL,
    nota text NOT NULL,
    PRIMARY KEY (categoria, table_name)
) ON COMMIT PRESERVE ROWS;

TRUNCATE reset_preview;

DO $$
DECLARE
    candidate text;
    count_rows bigint;
    operational_targets text[] := ARRAY[
        'notificacion_intentos',
        'notificacion_destinatarios',
        'notificaciones',
        'alertas',
        'detalle_recepcion_abastecimiento',
        'detalle_orden_compra',
        'recepciones_abastecimiento',
        'ordenes_compra',
        'detalle_solicitud_abastecimiento',
        'solicitudes_abastecimiento',
        'dependencias_programacion',
        'alertas_programacion',
        'programaciones_ot',
        'historial_preventivo',
        'evaluaciones_preventivas',
        'alcances_plan_preventivo',
        'planes_preventivos_sql',
        'eventos_disponibilidad',
        'contrato_disponibilidad_objetivos',
        'contratos_disponibilidad',
        'errores_importacion',
        'filas_importacion',
        'eventos_importacion',
        'importaciones',
        'solicitud_repuesto_historial',
        'solicitud_repuesto_items',
        'solicitudes_repuestos',
        'ot_checklists_sql',
        'ot_firmas_sql',
        'ot_evidencias_sql',
        'ot_estado_historial_sql',
        'ot_tecnicos_tarea_sql',
        'ot_hh_sql',
        'ot_repuestos_sql',
        'tareas_ot_sql',
        'documento_ordenes_trabajo',
        'orden_trabajo_activos',
        'avisos_trabajo_sql',
        'costos',
        'estados_pago',
        'movimientos_stock',
        'reservas_stock',
        'transferencias_stock',
        'documento_activos',
        'nodo_tecnico_activos',
        'valores_atributo_activo',
        'eventos_estado_activo',
        'lecturas_activo',
        'componentes_unidad_operativa',
        'unidades_operativas',
        'ordenes_trabajo_sql',
        'activos'
    ];
    preserved_targets text[] := ARRAY[
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
        'definiciones_atributo_activo',
        'catalogos_trabajo',
        'catalogos_inventario',
        'unidades_medida',
        'reglas_composicion_unidad',
        'reglas_composicion_unidad_activos_permitidos',
        'reglas_alerta',
        'regla_alerta_destinatarios',
        'plantillas_checklist',
        'items_plantilla_checklist',
        'plantillas_pdf',
        'tarifas_hh',
        'audit_log',
        'documentos',
        'archivos',
        'versiones_documento'
    ];
    review_targets text[] := ARRAY[
        'faenas',
        'bodegas',
        'repuestos',
        'stock_bodega',
        'ubicaciones_bodega',
        'ubicaciones_tecnicas',
        'nodos_tecnicos',
        'nodo_tecnico_aliases',
        'nodo_tecnico_familias',
        'familias_equipo',
        'proveedores',
        'talleres'
    ];
BEGIN
    FOREACH candidate IN ARRAY operational_targets LOOP
        IF to_regclass('public.' || candidate) IS NULL THEN
            INSERT INTO reset_preview VALUES
                ('se_eliminara', candidate, false, 0, 'La tabla no existe a?n en la migraci?n actual.');
        ELSE
            EXECUTE format('SELECT count(*) FROM public.%I', candidate) INTO count_rows;
            INSERT INTO reset_preview VALUES
                ('se_eliminara', candidate, true, count_rows, 'Registro operacional incluido en el reset controlado.');
        END IF;
    END LOOP;

    FOREACH candidate IN ARRAY preserved_targets LOOP
        IF to_regclass('public.' || candidate) IS NULL THEN
            INSERT INTO reset_preview VALUES
                ('se_conservara', candidate, false, 0, 'La tabla no existe en la migraci?n actual.');
        ELSE
            EXECUTE format('SELECT count(*) FROM public.%I', candidate) INTO count_rows;
            INSERT INTO reset_preview VALUES
                ('se_conservara', candidate, true, count_rows, 'Dato estructural, cat?logo, configuraci?n o auditor?a preservado.');
        END IF;
    END LOOP;

    FOREACH candidate IN ARRAY review_targets LOOP
        IF to_regclass('public.' || candidate) IS NULL THEN
            INSERT INTO reset_preview VALUES
                ('revision_manual', candidate, false, 0, 'La tabla no existe en la migraci?n actual.');
        ELSE
            EXECUTE format('SELECT count(*) FROM public.%I', candidate) INTO count_rows;
            INSERT INTO reset_preview VALUES
                ('revision_manual', candidate, true, count_rows, 'Dato maestro preservado; no se elimina sin decisi?n expl?cita.');
        END IF;
    END LOOP;
END $$;

SELECT categoria, table_name, existe, row_count, nota
FROM reset_preview
ORDER BY
    CASE categoria
        WHEN 'se_conservara' THEN 1
        WHEN 'se_eliminara' THEN 2
        ELSE 3
    END,
    table_name;

SELECT
    categoria,
    count(*) FILTER (WHERE existe) AS tablas_existentes,
    count(*) FILTER (WHERE NOT existe) AS tablas_ausentes,
    sum(row_count) AS filas_actuales
FROM reset_preview
GROUP BY categoria
ORDER BY categoria;
