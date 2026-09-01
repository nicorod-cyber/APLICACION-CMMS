using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

[DbContext(typeof(CmmsDbContext))]
[Migration("202607090003_WorkNotificationsAndOrdersPostgreSql")]
public partial class WorkNotificationsAndOrdersPostgreSql : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
CREATE SEQUENCE IF NOT EXISTS work_notification_number_seq START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE IF NOT EXISTS work_order_number_seq START WITH 1 INCREMENT BY 1;

CREATE TABLE IF NOT EXISTS catalogos_trabajo (
    id uuid PRIMARY KEY, categoria varchar(80) NOT NULL, codigo varchar(120) NOT NULL, nombre varchar(240) NOT NULL,
    descripcion varchar(1000), activo boolean NOT NULL, orden integer NOT NULL,
    created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz,
    CONSTRAINT uq_catalogos_trabajo_categoria_codigo UNIQUE (categoria, codigo)
);

CREATE TABLE IF NOT EXISTS ordenes_trabajo_sql (
    id uuid PRIMARY KEY, numero_ot varchar(40) NOT NULL UNIQUE, activo_id uuid NOT NULL, faena_id uuid NOT NULL,
    estado_id uuid NOT NULL, tipo_mantenimiento_id uuid NOT NULL, descripcion varchar(2000) NOT NULL, aviso_id uuid NULL,
    sistema varchar(120), subsistema varchar(120), componente varchar(120), prioridad_id uuid NULL, criticidad_id uuid NULL,
    clasificacion_falla_id uuid NULL, plan_preventivo_codigo varchar(120), preventiva_automatica boolean NOT NULL,
    requiere_firma boolean NOT NULL, fecha_programada_utc timestamptz, inicio_programado_utc timestamptz, fin_programado_utc timestamptz,
    creado_por_usuario_id varchar(120) NOT NULL, creado_por_usuario_at_utc timestamptz NOT NULL, inicio_real_utc timestamptz,
    finalizacion_tecnico_utc timestamptz, finalizado_por_usuario_id varchar(120), cierre_supervisor_utc timestamptz,
    cerrado_por_usuario_id varchar(120), validacion_planificacion_utc timestamptz, validado_por_usuario_id varchar(120),
    anulado_por_usuario_id varchar(120), anulado_at_utc timestamptz, motivo_anulacion varchar(500),
    actualizado_por_usuario_id varchar(120), actualizado_por_usuario_at_utc timestamptz,
    created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz,
    CONSTRAINT fk_ot_sql_activos FOREIGN KEY (activo_id) REFERENCES activos(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ot_sql_faenas FOREIGN KEY (faena_id) REFERENCES faenas(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ot_sql_estado FOREIGN KEY (estado_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ot_sql_tipo FOREIGN KEY (tipo_mantenimiento_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ot_sql_prioridad FOREIGN KEY (prioridad_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ot_sql_criticidad FOREIGN KEY (criticidad_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT,
    CONSTRAINT fk_ot_sql_clasificacion FOREIGN KEY (clasificacion_falla_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_ordenes_trabajo_sql_aviso_id ON ordenes_trabajo_sql(aviso_id) WHERE aviso_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_ordenes_trabajo_sql_activo_id ON ordenes_trabajo_sql(activo_id);
CREATE INDEX IF NOT EXISTS ix_ordenes_trabajo_sql_faena_id ON ordenes_trabajo_sql(faena_id);

CREATE TABLE IF NOT EXISTS avisos_trabajo_sql (
    id uuid PRIMARY KEY, aviso_id varchar(40) NOT NULL UNIQUE, estado_id uuid NOT NULL, tipo_id uuid NOT NULL,
    faena_id uuid NOT NULL, activo_id uuid NULL, sistema varchar(120), subsistema varchar(120), componente varchar(120),
    descripcion varchar(2000) NOT NULL, prioridad_id uuid NOT NULL, criticidad_id uuid NOT NULL, solicitante_usuario_id varchar(120) NOT NULL,
    evidencia_inicial varchar(1000), fecha_deteccion_utc timestamptz NOT NULL, fecha_creacion_usuario_utc timestamptz NOT NULL,
    clasificacion_falla_id uuid NOT NULL, evaluado_por_usuario_id varchar(120), evaluado_at_utc timestamptz,
    aprobado_por_usuario_id varchar(120), aprobado_at_utc timestamptz, rechazado_por_usuario_id varchar(120), rechazado_at_utc timestamptz,
    motivo_rechazo varchar(500), anulado_por_usuario_id varchar(120), anulado_at_utc timestamptz, motivo_anulacion varchar(500),
    orden_trabajo_id uuid NULL, convertido_por_usuario_id varchar(120), convertido_at_utc timestamptz, observaciones varchar(2000),
    created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz,
    CONSTRAINT fk_aviso_sql_estado FOREIGN KEY (estado_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT,
    CONSTRAINT fk_aviso_sql_tipo FOREIGN KEY (tipo_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT,
    CONSTRAINT fk_aviso_sql_faena FOREIGN KEY (faena_id) REFERENCES faenas(id) ON DELETE RESTRICT,
    CONSTRAINT fk_aviso_sql_activo FOREIGN KEY (activo_id) REFERENCES activos(id) ON DELETE RESTRICT,
    CONSTRAINT fk_aviso_sql_prioridad FOREIGN KEY (prioridad_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT,
    CONSTRAINT fk_aviso_sql_criticidad FOREIGN KEY (criticidad_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT,
    CONSTRAINT fk_aviso_sql_clasificacion FOREIGN KEY (clasificacion_falla_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT,
    CONSTRAINT fk_aviso_sql_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_avisos_trabajo_sql_orden_trabajo_id ON avisos_trabajo_sql(orden_trabajo_id) WHERE orden_trabajo_id IS NOT NULL;
ALTER TABLE ordenes_trabajo_sql ADD CONSTRAINT fk_ot_sql_aviso FOREIGN KEY (aviso_id) REFERENCES avisos_trabajo_sql(id) ON DELETE RESTRICT;

CREATE TABLE IF NOT EXISTS tareas_ot_sql (id uuid PRIMARY KEY, orden_trabajo_id uuid NOT NULL, codigo_tarea varchar(40) NOT NULL, descripcion varchar(1000) NOT NULL, inicio_programado_utc timestamptz, fin_programado_utc timestamptz, requiere_evidencia boolean NOT NULL, requiere_hh boolean NOT NULL, checklist_obligatorio boolean NOT NULL, observaciones varchar(1000), vigente boolean NOT NULL, created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT fk_tareas_ot_sql_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT, CONSTRAINT uq_tareas_ot_sql_ot_codigo UNIQUE (orden_trabajo_id, codigo_tarea));
CREATE TABLE IF NOT EXISTS ot_tecnicos_tarea_sql (id uuid PRIMARY KEY, orden_trabajo_id uuid NOT NULL, tarea_id uuid NOT NULL, tecnico_usuario_id varchar(120) NOT NULL, tecnico_nombre varchar(240), asignado_at_utc timestamptz NOT NULL, asignado_por_usuario_id varchar(120) NOT NULL, vigente boolean NOT NULL, desasignado_at_utc timestamptz, desasignado_por_usuario_id varchar(120), motivo_desasignacion varchar(500), created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT fk_ot_tecnicos_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_tecnicos_tarea FOREIGN KEY (tarea_id) REFERENCES tareas_ot_sql(id) ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS ix_ot_tecnicos_tarea_sql_tarea_tecnico_vigente ON ot_tecnicos_tarea_sql(tarea_id, tecnico_usuario_id, vigente) WHERE vigente;
CREATE TABLE IF NOT EXISTS ot_hh_sql (id uuid PRIMARY KEY, orden_trabajo_id uuid NOT NULL, tarea_id uuid NOT NULL, tecnico_usuario_id varchar(120) NOT NULL, horas numeric(12,2) NOT NULL CHECK (horas > 0), descripcion varchar(1000) NOT NULL, fecha_trabajo_utc timestamptz NOT NULL, hora_inicio_utc timestamptz, hora_termino_utc timestamptz, registrado_por_usuario_id varchar(120) NOT NULL, comentario varchar(1000), validado_supervisor boolean NOT NULL, validado_por_usuario_id varchar(120), validado_at_utc timestamptz, vigente boolean NOT NULL, created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT fk_ot_hh_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_hh_tarea FOREIGN KEY (tarea_id) REFERENCES tareas_ot_sql(id) ON DELETE RESTRICT);
CREATE TABLE IF NOT EXISTS ot_evidencias_sql (id uuid PRIMARY KEY, orden_trabajo_id uuid NOT NULL, tarea_id uuid NULL, nombre varchar(300) NOT NULL, archivo_id uuid NULL, tipo_evidencia_id uuid NOT NULL, es_foto boolean NOT NULL, es_obligatoria boolean NOT NULL, cubre_evidencia_obligatoria boolean NOT NULL, proveedor varchar(80), uri_externa varchar(1000), clave_externa varchar(300), ruta_local varchar(1000), offline_id varchar(120), estado_sync varchar(80), observaciones varchar(1000), creado_por_usuario_id varchar(120) NOT NULL, creado_por_usuario_at_utc timestamptz NOT NULL, vigente boolean NOT NULL, created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT fk_ot_evidencias_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_evidencias_tarea FOREIGN KEY (tarea_id) REFERENCES tareas_ot_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_evidencias_archivo FOREIGN KEY (archivo_id) REFERENCES archivos(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_evidencias_tipo FOREIGN KEY (tipo_evidencia_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT);
CREATE TABLE IF NOT EXISTS ot_repuestos_sql (id uuid PRIMARY KEY, orden_trabajo_id uuid NOT NULL, tarea_id uuid NOT NULL, repuesto_codigo varchar(120) NOT NULL, cantidad numeric(12,2) NOT NULL, unidad varchar(40) NOT NULL, bodega_codigo varchar(120), estado_id uuid NOT NULL, cantidad_utilizada numeric(12,2) NOT NULL, cantidad_devuelta numeric(12,2) NOT NULL, observaciones varchar(1000), vigente boolean NOT NULL, created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT ck_ot_repuestos_sql_cantidades CHECK (cantidad > 0 AND cantidad_utilizada >= 0 AND cantidad_devuelta >= 0), CONSTRAINT fk_ot_repuestos_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_repuestos_tarea FOREIGN KEY (tarea_id) REFERENCES tareas_ot_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_repuestos_estado FOREIGN KEY (estado_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT);
CREATE TABLE IF NOT EXISTS plantillas_checklist (id uuid PRIMARY KEY, codigo varchar(120) NOT NULL UNIQUE, nombre varchar(240) NOT NULL, tipo_ot_codigo varchar(120), familia_codigo varchar(120), plan_preventivo_codigo varchar(120), tarea_codigo varchar(40), activo_codigo varchar(120), activo boolean NOT NULL, created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz);
CREATE TABLE IF NOT EXISTS items_plantilla_checklist (id uuid PRIMARY KEY, plantilla_id uuid NOT NULL, orden integer NOT NULL, texto varchar(1000) NOT NULL, obligatorio boolean NOT NULL, tipo_respuesta_id uuid NOT NULL, requiere_foto boolean NOT NULL, requiere_archivo boolean NOT NULL, requiere_firma boolean NOT NULL, activo boolean NOT NULL, created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT fk_items_plantilla FOREIGN KEY (plantilla_id) REFERENCES plantillas_checklist(id) ON DELETE RESTRICT, CONSTRAINT fk_items_tipo_respuesta FOREIGN KEY (tipo_respuesta_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT, CONSTRAINT uq_items_plantilla_orden UNIQUE (plantilla_id, orden));
CREATE TABLE IF NOT EXISTS ot_firmas_sql (id uuid PRIMARY KEY, orden_trabajo_id uuid NOT NULL, tarea_id uuid NULL, alcance varchar(80) NOT NULL, firmante_usuario_id varchar(120) NOT NULL, archivo_id uuid NULL, signature_file_key varchar(300), firmado_at_utc timestamptz NOT NULL, comentario varchar(1000), vigente boolean NOT NULL, created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT fk_ot_firmas_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_firmas_tarea FOREIGN KEY (tarea_id) REFERENCES tareas_ot_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_firmas_archivo FOREIGN KEY (archivo_id) REFERENCES archivos(id) ON DELETE RESTRICT);
CREATE TABLE IF NOT EXISTS ot_checklists_sql (id uuid PRIMARY KEY, orden_trabajo_id uuid NOT NULL, tarea_id uuid NOT NULL, plantilla_id uuid NULL, item_plantilla_id uuid NULL, texto_item varchar(1000) NOT NULL, obligatorio boolean NOT NULL, completado boolean NOT NULL, completado_at_utc timestamptz, completado_por_usuario_id varchar(120), tipo_respuesta_id uuid NOT NULL, respuesta varchar(500), valor_numerico numeric(12,2), texto_libre varchar(1000), evidencia_id uuid NULL, firma_id uuid NULL, requiere_foto boolean NOT NULL, requiere_archivo boolean NOT NULL, requiere_firma boolean NOT NULL, vigente boolean NOT NULL, created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT fk_ot_checklists_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_checklists_tarea FOREIGN KEY (tarea_id) REFERENCES tareas_ot_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_checklists_plantilla FOREIGN KEY (plantilla_id) REFERENCES plantillas_checklist(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_checklists_item FOREIGN KEY (item_plantilla_id) REFERENCES items_plantilla_checklist(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_checklists_tipo FOREIGN KEY (tipo_respuesta_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_checklists_evidencia FOREIGN KEY (evidencia_id) REFERENCES ot_evidencias_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_checklists_firma FOREIGN KEY (firma_id) REFERENCES ot_firmas_sql(id) ON DELETE RESTRICT);
CREATE TABLE IF NOT EXISTS ot_estado_historial_sql (id uuid PRIMARY KEY, orden_trabajo_id uuid NOT NULL, estado_anterior_id uuid NOT NULL, estado_nuevo_id uuid NOT NULL, fecha_utc timestamptz NOT NULL, usuario_id varchar(120) NOT NULL, motivo varchar(500) NOT NULL, created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT fk_ot_historial_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_historial_anterior FOREIGN KEY (estado_anterior_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT, CONSTRAINT fk_ot_historial_nuevo FOREIGN KEY (estado_nuevo_id) REFERENCES catalogos_trabajo(id) ON DELETE RESTRICT);
CREATE TABLE IF NOT EXISTS documento_ordenes_trabajo (id uuid PRIMARY KEY, documento_id uuid NOT NULL, orden_trabajo_id uuid NOT NULL, vigente boolean NOT NULL, asignado_at_utc timestamptz NOT NULL, asignado_por_usuario_id varchar(120), desasignado_at_utc timestamptz, desasignado_por_usuario_id varchar(120), motivo_desasignacion varchar(500), created_at_utc timestamptz NOT NULL, updated_at_utc timestamptz, CONSTRAINT fk_doc_ot_documento FOREIGN KEY (documento_id) REFERENCES documentos(id) ON DELETE RESTRICT, CONSTRAINT fk_doc_ot_ot FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql(id) ON DELETE RESTRICT);
CREATE UNIQUE INDEX IF NOT EXISTS ix_documento_ordenes_trabajo_vigente ON documento_ordenes_trabajo(documento_id, orden_trabajo_id, vigente) WHERE vigente;

CREATE OR REPLACE FUNCTION prevent_work_order_asset_faena_update() RETURNS trigger AS $$
BEGIN
    IF NEW.activo_id <> OLD.activo_id OR NEW.faena_id <> OLD.faena_id THEN
        RAISE EXCEPTION 'No se puede modificar activo_id ni faena_id de una OT existente';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS trg_prevent_work_order_asset_faena_update ON ordenes_trabajo_sql;
CREATE TRIGGER trg_prevent_work_order_asset_faena_update BEFORE UPDATE ON ordenes_trabajo_sql FOR EACH ROW EXECUTE FUNCTION prevent_work_order_asset_faena_update();
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
DROP TRIGGER IF EXISTS trg_prevent_work_order_asset_faena_update ON ordenes_trabajo_sql;
DROP FUNCTION IF EXISTS prevent_work_order_asset_faena_update();
DROP TABLE IF EXISTS documento_ordenes_trabajo;
DROP TABLE IF EXISTS ot_estado_historial_sql;
DROP TABLE IF EXISTS ot_checklists_sql;
DROP TABLE IF EXISTS ot_firmas_sql;
DROP TABLE IF EXISTS items_plantilla_checklist;
DROP TABLE IF EXISTS plantillas_checklist;
DROP TABLE IF EXISTS ot_repuestos_sql;
DROP TABLE IF EXISTS ot_evidencias_sql;
DROP TABLE IF EXISTS ot_hh_sql;
DROP TABLE IF EXISTS ot_tecnicos_tarea_sql;
DROP TABLE IF EXISTS tareas_ot_sql;
ALTER TABLE ordenes_trabajo_sql DROP CONSTRAINT IF EXISTS fk_ot_sql_aviso;
DROP TABLE IF EXISTS avisos_trabajo_sql;
DROP TABLE IF EXISTS ordenes_trabajo_sql;
DROP TABLE IF EXISTS catalogos_trabajo;
DROP SEQUENCE IF EXISTS work_notification_number_seq;
DROP SEQUENCE IF EXISTS work_order_number_seq;
""");
    }
}

