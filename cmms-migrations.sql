CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE audit_log (
    id uuid NOT NULL,
    occurred_at_utc timestamptz NOT NULL,
    usuario_id character varying(120) NOT NULL,
    accion character varying(160) NOT NULL,
    modulo character varying(120) NOT NULL,
    entidad character varying(160) NOT NULL,
    entidad_id character varying(160) NOT NULL,
    faena_codigo character varying(80),
    severidad character varying(40) NOT NULL,
    valor_anterior text,
    valor_nuevo text,
    ip_address character varying(80),
    dispositivo character varying(240),
    motivo character varying(500),
    exitoso boolean NOT NULL,
    detalle text,
    correlation_id character varying(120),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_audit_log PRIMARY KEY (id)
);

CREATE TABLE estados_operacionales_activo (
    id uuid NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(160) NOT NULL,
    activo boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_estados_operacionales_activo PRIMARY KEY (id),
    CONSTRAINT ck_estados_operacionales_activo_codigo CHECK (codigo IN ('OPERATIVO_FAENA','ALERTA_FAENA','FUERA_SERVICIO_FAENA','FUERA_SERVICIO_TALLER'))
);

CREATE TABLE faenas (
    id uuid NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(240) NOT NULL,
    activo boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_faenas PRIMARY KEY (id)
);

CREATE TABLE familias_equipo (
    id uuid NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(160) NOT NULL,
    activo boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_familias_equipo PRIMARY KEY (id)
);

CREATE TABLE permisos (
    id uuid NOT NULL,
    codigo character varying(160) NOT NULL,
    nombre character varying(240) NOT NULL,
    activo boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_permisos PRIMARY KEY (id)
);

CREATE TABLE roles (
    id uuid NOT NULL,
    codigo character varying(120) NOT NULL,
    nombre character varying(240) NOT NULL,
    tipo_rol character varying(120) NOT NULL,
    activo boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_roles PRIMARY KEY (id)
);

CREATE TABLE usuarios (
    id uuid NOT NULL,
    username character varying(120) NOT NULL,
    email character varying(240) NOT NULL,
    nombre character varying(240) NOT NULL,
    activo boolean NOT NULL,
    bloqueado boolean NOT NULL,
    password_hash character varying(500) NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_usuarios PRIMARY KEY (id)
);

CREATE TABLE archivos (
    id uuid NOT NULL,
    file_key character varying(240) NOT NULL,
    nombre character varying(300) NOT NULL,
    proveedor character varying(80) NOT NULL,
    uri_logica character varying(1000) NOT NULL,
    tipo_mime character varying(160),
    tamano_bytes bigint,
    checksum character varying(200),
    estado character varying(40) NOT NULL,
    metadata jsonb,
    autor_usuario_id character varying(120),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_archivos PRIMARY KEY (id)
);

CREATE TABLE activos (
    id uuid NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(240) NOT NULL,
    faena_id uuid NOT NULL,
    familia_equipo_id uuid NOT NULL,
    estado_operacional_id uuid NOT NULL,
    estado_registro character varying(40) NOT NULL,
    tipo_activo character varying(120) NOT NULL,
    ubicacion_tecnica_codigo character varying(120),
    marca character varying(120),
    modelo character varying(120),
    patente character varying(80),
    numero_serie character varying(120),
    propiedad character varying(120),
    criticidad character varying(80),
    estado_documental character varying(80),
    ficha_validada boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_activos PRIMARY KEY (id),
    CONSTRAINT ck_activos_estado_registro CHECK (estado_registro IN ('vigente','inactivo','anulado','obsoleto','reemplazado','no_vigente')),
    CONSTRAINT fk_activos_estados_operacionales_activo_estado_operacional_id FOREIGN KEY (estado_operacional_id) REFERENCES estados_operacionales_activo (id) ON DELETE RESTRICT,
    CONSTRAINT fk_activos_faenas_faena_id FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT,
    CONSTRAINT fk_activos_familias_equipo_familia_equipo_id FOREIGN KEY (familia_equipo_id) REFERENCES familias_equipo (id) ON DELETE RESTRICT
);

CREATE TABLE documentos (
    id uuid NOT NULL,
    codigo character varying(120) NOT NULL,
    titulo character varying(300) NOT NULL,
    tipo_documento_codigo character varying(120) NOT NULL,
    estado character varying(40) NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_documentos PRIMARY KEY (id)
);

CREATE TABLE rol_permisos (
    id uuid NOT NULL,
    rol_id uuid NOT NULL,
    permiso_id uuid NOT NULL,
    vigente boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_rol_permisos PRIMARY KEY (id),
    CONSTRAINT fk_rol_permisos_permisos_permiso_id FOREIGN KEY (permiso_id) REFERENCES permisos (id) ON DELETE RESTRICT,
    CONSTRAINT fk_rol_permisos_roles_rol_id FOREIGN KEY (rol_id) REFERENCES roles (id) ON DELETE RESTRICT
);

CREATE TABLE usuario_roles (
    id uuid NOT NULL,
    usuario_id uuid NOT NULL,
    rol_id uuid NOT NULL,
    vigente boolean NOT NULL,
    asignado_por_usuario_id character varying(120),
    asignado_at_utc timestamptz NOT NULL,
    desasignado_por_usuario_id character varying(120),
    desasignado_at_utc timestamptz,
    motivo_desasignacion character varying(500),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_usuario_roles PRIMARY KEY (id),
    CONSTRAINT fk_usuario_roles_roles_rol_id FOREIGN KEY (rol_id) REFERENCES roles (id) ON DELETE RESTRICT,
    CONSTRAINT fk_usuario_roles_usuarios_usuario_id FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT
);

CREATE TABLE usuario_faenas (
    id uuid NOT NULL,
    usuario_id uuid NOT NULL,
    faena_id uuid NOT NULL,
    vigente boolean NOT NULL,
    asignado_at_utc timestamptz NOT NULL,
    asignado_por_usuario_id character varying(120),
    desasignado_at_utc timestamptz,
    desasignado_por_usuario_id character varying(120),
    motivo_desasignacion character varying(500),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_usuario_faenas PRIMARY KEY (id),
    CONSTRAINT fk_usuario_faenas_faenas_faena_id FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT,
    CONSTRAINT fk_usuario_faenas_usuarios_usuario_id FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT
);

CREATE TABLE eventos_estado_activo (
    id uuid NOT NULL,
    activo_id uuid NOT NULL,
    estado_anterior_id uuid,
    estado_nuevo_id uuid NOT NULL,
    fecha_evento_utc timestamptz NOT NULL,
    usuario_id character varying(120) NOT NULL,
    motivo character varying(500) NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_eventos_estado_activo PRIMARY KEY (id),
    CONSTRAINT fk_eventos_estado_activo_activos_activo_id FOREIGN KEY (activo_id) REFERENCES activos (id) ON DELETE RESTRICT,
    CONSTRAINT fk_eventos_estado_activo_estados_anterior FOREIGN KEY (estado_anterior_id) REFERENCES estados_operacionales_activo (id) ON DELETE RESTRICT,
    CONSTRAINT fk_eventos_estado_activo_estados_nuevo FOREIGN KEY (estado_nuevo_id) REFERENCES estados_operacionales_activo (id) ON DELETE RESTRICT
);

CREATE TABLE documento_activos (
    id uuid NOT NULL,
    documento_id uuid NOT NULL,
    activo_id uuid NOT NULL,
    vigente boolean NOT NULL,
    asignado_at_utc timestamptz NOT NULL,
    asignado_por_usuario_id character varying(120),
    desasignado_at_utc timestamptz,
    desasignado_por_usuario_id character varying(120),
    motivo_desasignacion character varying(500),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_documento_activos PRIMARY KEY (id),
    CONSTRAINT fk_documento_activos_activos_activo_id FOREIGN KEY (activo_id) REFERENCES activos (id) ON DELETE RESTRICT,
    CONSTRAINT fk_documento_activos_documentos_documento_id FOREIGN KEY (documento_id) REFERENCES documentos (id) ON DELETE RESTRICT
);

CREATE TABLE versiones_documento (
    id uuid NOT NULL,
    documento_id uuid NOT NULL,
    numero_version integer NOT NULL,
    archivo_id uuid NOT NULL,
    estado character varying(40) NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_versiones_documento PRIMARY KEY (id),
    CONSTRAINT fk_versiones_documento_archivos_archivo_id FOREIGN KEY (archivo_id) REFERENCES archivos (id) ON DELETE RESTRICT,
    CONSTRAINT fk_versiones_documento_documentos_documento_id FOREIGN KEY (documento_id) REFERENCES documentos (id) ON DELETE RESTRICT
);

CREATE INDEX ix_audit_log_faena_codigo ON audit_log (faena_codigo);

CREATE INDEX ix_audit_log_modulo_entidad ON audit_log (modulo, entidad);

CREATE INDEX ix_audit_log_occurred_at_utc ON audit_log (occurred_at_utc);

CREATE UNIQUE INDEX ix_estados_operacionales_activo_codigo ON estados_operacionales_activo (codigo);

CREATE UNIQUE INDEX ix_faenas_codigo ON faenas (codigo);

CREATE UNIQUE INDEX ix_familias_equipo_codigo ON familias_equipo (codigo);

CREATE UNIQUE INDEX ix_permisos_codigo ON permisos (codigo);

CREATE UNIQUE INDEX ix_roles_codigo ON roles (codigo);

CREATE UNIQUE INDEX ix_usuarios_email ON usuarios (email);

CREATE UNIQUE INDEX ix_usuarios_username ON usuarios (username);

CREATE UNIQUE INDEX ix_archivos_file_key ON archivos (file_key);

CREATE UNIQUE INDEX ix_activos_codigo ON activos (codigo);

CREATE INDEX ix_activos_estado_operacional_id ON activos (estado_operacional_id);

CREATE INDEX ix_activos_faena_id ON activos (faena_id);

CREATE INDEX ix_activos_familia_equipo_id ON activos (familia_equipo_id);

CREATE UNIQUE INDEX ix_documentos_codigo ON documentos (codigo);

CREATE INDEX ix_rol_permisos_permiso_id ON rol_permisos (permiso_id);

CREATE UNIQUE INDEX ix_rol_permisos_rol_id_permiso_id_vigente ON rol_permisos (rol_id, permiso_id, vigente);

CREATE INDEX ix_usuario_roles_rol_id ON usuario_roles (rol_id);

CREATE UNIQUE INDEX ix_usuario_roles_usuario_id_rol_id_vigente ON usuario_roles (usuario_id, rol_id, vigente);

CREATE INDEX ix_usuario_faenas_faena_id ON usuario_faenas (faena_id);

CREATE UNIQUE INDEX ix_usuario_faenas_usuario_id_faena_id_vigente ON usuario_faenas (usuario_id, faena_id, vigente);

CREATE INDEX ix_eventos_estado_activo_activo_id ON eventos_estado_activo (activo_id);

CREATE INDEX ix_eventos_estado_activo_estado_anterior_id ON eventos_estado_activo (estado_anterior_id);

CREATE INDEX ix_eventos_estado_activo_estado_nuevo_id ON eventos_estado_activo (estado_nuevo_id);

CREATE INDEX ix_documento_activos_activo_id ON documento_activos (activo_id);

CREATE UNIQUE INDEX ix_documento_activos_documento_id_activo_id_vigente ON documento_activos (documento_id, activo_id, vigente);

CREATE INDEX ix_versiones_documento_archivo_id ON versiones_documento (archivo_id);

CREATE UNIQUE INDEX ix_versiones_documento_documento_id_numero_version ON versiones_documento (documento_id, numero_version);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('202607090001_InitialPostgreSqlSchema', '8.0.11');

COMMIT;

START TRANSACTION;

CREATE TABLE tipos_documentales (
    id uuid NOT NULL,
    codigo character varying(120) NOT NULL,
    nombre character varying(240) NOT NULL,
    descripcion character varying(1000),
    aplica_a character varying(40),
    obligatorio boolean NOT NULL,
    critico boolean NOT NULL,
    bloquea_disponibilidad boolean NOT NULL,
    dias_alerta integer NOT NULL,
    roles_responsables character varying(1000),
    requiere_pdf_alerta boolean NOT NULL,
    plantilla_html_codigo character varying(120),
    activo boolean NOT NULL,
    created_by_user_id character varying(120),
    updated_by_user_id character varying(120),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_tipos_documentales PRIMARY KEY (id),
    CONSTRAINT ck_tipos_documentales_dias_alerta CHECK (dias_alerta >= 0)
);

INSERT INTO tipos_documentales (
    id, codigo, nombre, aplica_a, obligatorio, critico, bloquea_disponibilidad,
    dias_alerta, requiere_pdf_alerta, activo, created_by_user_id, created_at_utc)
SELECT gen_random_uuid(), UPPER(TRIM(tipo_documento_codigo)), TRIM(tipo_documento_codigo), 'Activo', false, false, false,
       30, false, true, 'migration', now()
FROM documentos
WHERE tipo_documento_codigo IS NOT NULL AND TRIM(tipo_documento_codigo) <> ''
GROUP BY UPPER(TRIM(tipo_documento_codigo)), TRIM(tipo_documento_codigo)
ON CONFLICT DO NOTHING;

INSERT INTO tipos_documentales (
    id, codigo, nombre, aplica_a, obligatorio, critico, bloquea_disponibilidad,
    dias_alerta, requiere_pdf_alerta, activo, created_by_user_id, created_at_utc)
VALUES
    (gen_random_uuid(), 'REV-TEC', 'Revision tecnica', 'Activo', true, true, true, 30, false, true, 'migration', now()),
    (gen_random_uuid(), 'PERMISO', 'Permiso operacional', 'Activo', true, false, false, 30, false, true, 'migration', now()),
    (gen_random_uuid(), 'CERT', 'Certificado', 'Activo', false, false, false, 45, false, true, 'migration', now()),
    (gen_random_uuid(), 'FAENA-GRAL', 'Documento general de faena', 'Faena', false, false, false, 30, false, true, 'migration', now())
ON CONFLICT DO NOTHING;

ALTER TABLE archivos ADD ruta_logica character varying(1000);

ALTER TABLE documentos ADD descripcion character varying(1000);

ALTER TABLE documentos ADD tipo_documental_id uuid;

ALTER TABLE documentos ADD fecha_emision date;

ALTER TABLE documentos ADD fecha_vencimiento date;

ALTER TABLE documentos ADD vigente boolean NOT NULL DEFAULT TRUE;

ALTER TABLE documentos ADD anulado boolean NOT NULL DEFAULT FALSE;

ALTER TABLE documentos ADD anulado_por_usuario_id character varying(120);

ALTER TABLE documentos ADD anulado_at_utc timestamptz;

ALTER TABLE documentos ADD motivo_anulacion character varying(500);

ALTER TABLE documentos ADD created_by_user_id character varying(120) NOT NULL DEFAULT 'migration';

ALTER TABLE documentos ADD updated_by_user_id character varying(120);

ALTER TABLE documentos ADD validado_por_usuario_id character varying(120);

ALTER TABLE documentos ADD validado_at_utc timestamptz;

ALTER TABLE documentos ADD rechazado_por_usuario_id character varying(120);

ALTER TABLE documentos ADD rechazado_at_utc timestamptz;

ALTER TABLE documentos ADD motivo_rechazo character varying(500);

ALTER TABLE documentos ADD fecha_vencimiento_validada boolean NOT NULL DEFAULT FALSE;

ALTER TABLE documentos ADD reemplaza_documento_id uuid;

ALTER TABLE documentos ADD reemplazado_por_documento_id uuid;

ALTER TABLE documentos ADD historico boolean NOT NULL DEFAULT FALSE;

ALTER TABLE documentos ADD critico boolean NOT NULL DEFAULT FALSE;

ALTER TABLE documentos ADD obligatorio boolean NOT NULL DEFAULT FALSE;

ALTER TABLE documentos ADD bloquea_disponibilidad boolean NOT NULL DEFAULT FALSE;

ALTER TABLE documentos ADD motivo_cambio character varying(500);

UPDATE documentos d
SET tipo_documental_id = t.id
FROM tipos_documentales t
WHERE t.codigo = UPPER(TRIM(d.tipo_documento_codigo));

ALTER TABLE documentos ALTER COLUMN tipo_documental_id TYPE uuid;
ALTER TABLE documentos ALTER COLUMN tipo_documental_id SET NOT NULL;

ALTER TABLE documentos DROP COLUMN tipo_documento_codigo;

ALTER TABLE versiones_documento ADD codigo_version character varying(80) NOT NULL DEFAULT '1';

ALTER TABLE versiones_documento ADD fecha_carga_utc timestamptz NOT NULL DEFAULT (now());

ALTER TABLE versiones_documento ADD cargado_por_usuario_id character varying(120) NOT NULL DEFAULT 'migration';

ALTER TABLE versiones_documento ADD observaciones character varying(1000);

ALTER TABLE versiones_documento ADD vigente boolean NOT NULL DEFAULT TRUE;

UPDATE versiones_documento v
SET codigo_version = v.numero_version::text,
    vigente = v.numero_version = latest.max_version
FROM (
    SELECT documento_id, MAX(numero_version) AS max_version
    FROM versiones_documento
    GROUP BY documento_id
) latest
WHERE latest.documento_id = v.documento_id;

CREATE TABLE documento_faenas (
    id uuid NOT NULL,
    documento_id uuid NOT NULL,
    faena_id uuid NOT NULL,
    vigente boolean NOT NULL,
    asignado_at_utc timestamptz NOT NULL,
    asignado_por_usuario_id character varying(120),
    desasignado_at_utc timestamptz,
    desasignado_por_usuario_id character varying(120),
    motivo_desasignacion character varying(500),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT pk_documento_faenas PRIMARY KEY (id),
    CONSTRAINT fk_documento_faenas_documentos_documento_id FOREIGN KEY (documento_id) REFERENCES documentos (id) ON DELETE RESTRICT,
    CONSTRAINT fk_documento_faenas_faenas_faena_id FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT
);

DROP INDEX ix_documento_activos_documento_id_activo_id_vigente;

CREATE UNIQUE INDEX ix_tipos_documentales_codigo ON tipos_documentales (codigo);

CREATE INDEX ix_documentos_tipo_documental_id ON documentos (tipo_documental_id);

CREATE INDEX ix_documentos_estado ON documentos (estado);

CREATE INDEX ix_documentos_reemplaza_documento_id ON documentos (reemplaza_documento_id);

CREATE INDEX ix_documentos_reemplazado_por_documento_id ON documentos (reemplazado_por_documento_id);

CREATE UNIQUE INDEX ix_versiones_documento_documento_id_vigente ON versiones_documento (documento_id, vigente) WHERE vigente;

CREATE UNIQUE INDEX ix_documento_activos_documento_id_activo_id_vigente ON documento_activos (documento_id, activo_id, vigente) WHERE vigente;

CREATE INDEX ix_documento_faenas_faena_id ON documento_faenas (faena_id);

CREATE UNIQUE INDEX ix_documento_faenas_documento_id_faena_id_vigente ON documento_faenas (documento_id, faena_id, vigente) WHERE vigente;

ALTER TABLE documentos ADD CONSTRAINT fk_documentos_tipos_documentales_tipo_documental_id FOREIGN KEY (tipo_documental_id) REFERENCES tipos_documentales (id) ON DELETE RESTRICT;

ALTER TABLE documentos ADD CONSTRAINT fk_documentos_documentos_reemplaza_documento_id FOREIGN KEY (reemplaza_documento_id) REFERENCES documentos (id) ON DELETE RESTRICT;

ALTER TABLE documentos ADD CONSTRAINT fk_documentos_documentos_reemplazado_por_documento_id FOREIGN KEY (reemplazado_por_documento_id) REFERENCES documentos (id) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('202607090002_DocumentDomainPostgreSql', '8.0.11');

COMMIT;

START TRANSACTION;

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

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('202607090003_WorkNotificationsAndOrdersPostgreSql', '8.0.11');

COMMIT;

START TRANSACTION;

CREATE SEQUENCE spare_part_number_seq START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;

CREATE SEQUENCE stock_movement_number_seq START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;

CREATE SEQUENCE stock_reservation_number_seq START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;

CREATE SEQUENCE stock_transfer_number_seq START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;

CREATE TABLE catalogos_inventario (
    id uuid NOT NULL,
    categoria character varying(80) NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(160) NOT NULL,
    descripcion character varying(500),
    activo boolean NOT NULL,
    orden integer NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_catalogos_inventario" PRIMARY KEY (id)
);

CREATE TABLE repuestos (
    id uuid NOT NULL,
    codigo character varying(80) NOT NULL,
    codigo_sap character varying(120),
    codigo_proveedor character varying(120),
    descripcion character varying(300) NOT NULL,
    descripcion_tecnica character varying(1000) NOT NULL,
    unidad_id uuid NOT NULL,
    categoria_id uuid,
    fabricante character varying(160),
    modelo_referencia character varying(160),
    critico boolean NOT NULL,
    stock_minimo numeric(14,2) NOT NULL,
    stock_maximo numeric(14,2) NOT NULL,
    punto_reposicion numeric(14,2) NOT NULL,
    lead_time_dias integer NOT NULL,
    costo_unitario_promedio numeric(14,2),
    estado character varying(40) NOT NULL,
    proveedor_preferente character varying(160),
    reemplazo_codigo character varying(80),
    creado_por_usuario_id character varying(120) NOT NULL,
    actualizado_por_usuario_id character varying(120),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_repuestos" PRIMARY KEY (id),
    CONSTRAINT ck_repuestos_stocks CHECK (stock_minimo >= 0 AND stock_maximo >= 0 AND punto_reposicion >= 0),
    CONSTRAINT "FK_repuestos_catalogos_inventario_categoria_id" FOREIGN KEY (categoria_id) REFERENCES catalogos_inventario (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_repuestos_catalogos_inventario_unidad_id" FOREIGN KEY (unidad_id) REFERENCES catalogos_inventario (id) ON DELETE RESTRICT
);

CREATE TABLE bodegas (
    id uuid NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(240) NOT NULL,
    faena_id uuid NOT NULL,
    tipo_id uuid NOT NULL,
    ubicacion character varying(300),
    responsable_usuario_id character varying(120),
    activo boolean NOT NULL,
    permite_stock_negativo boolean NOT NULL,
    creado_por_usuario_id character varying(120) NOT NULL,
    actualizado_por_usuario_id character varying(120),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_bodegas" PRIMARY KEY (id),
    CONSTRAINT "FK_bodegas_catalogos_inventario_tipo_id" FOREIGN KEY (tipo_id) REFERENCES catalogos_inventario (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_bodegas_faenas_faena_id" FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT
);

CREATE TABLE transferencias_stock (
    id uuid NOT NULL,
    codigo character varying(80) NOT NULL,
    bodega_origen_id uuid NOT NULL,
    bodega_transito_id uuid NOT NULL,
    bodega_destino_id uuid NOT NULL,
    repuesto_id uuid NOT NULL,
    cantidad numeric(14,2) NOT NULL,
    estado character varying(40) NOT NULL,
    motivo character varying(500) NOT NULL,
    solicitado_por_usuario_id character varying(120) NOT NULL,
    solicitado_at_utc timestamptz NOT NULL,
    recibido_at_utc timestamptz,
    recibido_por_usuario_id character varying(120),
    motivo_recepcion character varying(500),
    motivo_anulacion character varying(500),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_transferencias_stock" PRIMARY KEY (id),
    CONSTRAINT ck_transferencias_stock_bodegas CHECK (bodega_origen_id <> bodega_destino_id AND cantidad > 0),
    CONSTRAINT "FK_transferencias_stock_bodegas_bodega_destino_id" FOREIGN KEY (bodega_destino_id) REFERENCES bodegas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_transferencias_stock_bodegas_bodega_origen_id" FOREIGN KEY (bodega_origen_id) REFERENCES bodegas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_transferencias_stock_bodegas_bodega_transito_id" FOREIGN KEY (bodega_transito_id) REFERENCES bodegas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_transferencias_stock_repuestos_repuesto_id" FOREIGN KEY (repuesto_id) REFERENCES repuestos (id) ON DELETE RESTRICT
);

CREATE TABLE ubicaciones_bodega (
    id uuid NOT NULL,
    bodega_id uuid NOT NULL,
    codigo character varying(80) NOT NULL,
    nombre character varying(160) NOT NULL,
    descripcion character varying(500),
    pasillo character varying(80),
    estante character varying(80),
    nivel character varying(80),
    posicion character varying(80),
    activo boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_ubicaciones_bodega" PRIMARY KEY (id),
    CONSTRAINT "FK_ubicaciones_bodega_bodegas_bodega_id" FOREIGN KEY (bodega_id) REFERENCES bodegas (id) ON DELETE RESTRICT
);

CREATE TABLE stock_bodega (
    id uuid NOT NULL,
    repuesto_id uuid NOT NULL,
    bodega_id uuid NOT NULL,
    ubicacion_bodega_id uuid,
    cantidad_fisica numeric(14,2) NOT NULL,
    cantidad_reservada numeric(14,2) NOT NULL,
    stock_minimo_especifico numeric(14,2),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_stock_bodega" PRIMARY KEY (id),
    CONSTRAINT ck_stock_bodega_saldos CHECK (cantidad_fisica >= 0 AND cantidad_reservada >= 0 AND cantidad_reservada <= cantidad_fisica),
    CONSTRAINT "FK_stock_bodega_bodegas_bodega_id" FOREIGN KEY (bodega_id) REFERENCES bodegas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_stock_bodega_repuestos_repuesto_id" FOREIGN KEY (repuesto_id) REFERENCES repuestos (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_stock_bodega_ubicaciones_bodega_ubicacion_bodega_id" FOREIGN KEY (ubicacion_bodega_id) REFERENCES ubicaciones_bodega (id) ON DELETE RESTRICT
);

CREATE TABLE reservas_stock (
    id uuid NOT NULL,
    codigo character varying(80) NOT NULL,
    repuesto_id uuid NOT NULL,
    bodega_id uuid NOT NULL,
    cantidad_solicitada numeric(14,2) NOT NULL,
    cantidad_reservada numeric(14,2) NOT NULL,
    cantidad_entregada numeric(14,2) NOT NULL,
    cantidad_liberada numeric(14,2) NOT NULL,
    orden_trabajo_id uuid,
    orden_trabajo_numero character varying(80) NOT NULL,
    solicitante character varying(120) NOT NULL,
    estado character varying(40) NOT NULL,
    motivo character varying(500) NOT NULL,
    motivo_anulacion character varying(500),
    creado_por_usuario_id character varying(120) NOT NULL,
    entregado_at_utc timestamptz,
    liberado_at_utc timestamptz,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_reservas_stock" PRIMARY KEY (id),
    CONSTRAINT ck_reservas_stock_cantidades CHECK (cantidad_solicitada > 0 AND cantidad_reservada >= 0 AND cantidad_entregada >= 0 AND cantidad_liberada >= 0 AND cantidad_entregada + cantidad_liberada <= cantidad_reservada),
    CONSTRAINT "FK_reservas_stock_bodegas_bodega_id" FOREIGN KEY (bodega_id) REFERENCES bodegas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reservas_stock_ordenes_trabajo_sql_orden_trabajo_id" FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reservas_stock_repuestos_repuesto_id" FOREIGN KEY (repuesto_id) REFERENCES repuestos (id) ON DELETE RESTRICT
);

CREATE TABLE movimientos_stock (
    id uuid NOT NULL,
    numero_movimiento character varying(80) NOT NULL,
    tipo_movimiento_id uuid NOT NULL,
    repuesto_id uuid NOT NULL,
    cantidad numeric(14,2) NOT NULL,
    bodega_origen_id uuid,
    bodega_destino_id uuid,
    reserva_id uuid,
    transferencia_id uuid,
    orden_trabajo_id uuid,
    tipo_referencia character varying(80),
    referencia_id character varying(120),
    motivo character varying(500) NOT NULL,
    usuario_id character varying(120) NOT NULL,
    fecha_utc timestamptz NOT NULL,
    fisico_anterior numeric(14,2) NOT NULL,
    fisico_nuevo numeric(14,2) NOT NULL,
    reservado_anterior numeric(14,2) NOT NULL,
    reservado_nuevo numeric(14,2) NOT NULL,
    anulado boolean NOT NULL,
    movimiento_reverso_de_id uuid,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_movimientos_stock" PRIMARY KEY (id),
    CONSTRAINT ck_movimientos_stock_cantidad CHECK (cantidad > 0),
    CONSTRAINT "FK_movimientos_stock_bodegas_bodega_destino_id" FOREIGN KEY (bodega_destino_id) REFERENCES bodegas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_movimientos_stock_bodegas_bodega_origen_id" FOREIGN KEY (bodega_origen_id) REFERENCES bodegas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_movimientos_stock_catalogos_inventario_tipo_movimiento_id" FOREIGN KEY (tipo_movimiento_id) REFERENCES catalogos_inventario (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_movimientos_stock_ordenes_trabajo_sql_orden_trabajo_id" FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_movimientos_stock_repuestos_repuesto_id" FOREIGN KEY (repuesto_id) REFERENCES repuestos (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_movimientos_stock_reservas_stock_reserva_id" FOREIGN KEY (reserva_id) REFERENCES reservas_stock (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_movimientos_stock_transferencias_stock_transferencia_id" FOREIGN KEY (transferencia_id) REFERENCES transferencias_stock (id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX "IX_bodegas_codigo" ON bodegas (codigo);

CREATE INDEX "IX_bodegas_faena_id" ON bodegas (faena_id);

CREATE INDEX "IX_bodegas_tipo_id" ON bodegas (tipo_id);

CREATE UNIQUE INDEX "IX_catalogos_inventario_categoria_codigo" ON catalogos_inventario (categoria, codigo);

CREATE INDEX "IX_movimientos_stock_bodega_destino_id" ON movimientos_stock (bodega_destino_id);

CREATE INDEX "IX_movimientos_stock_bodega_origen_id" ON movimientos_stock (bodega_origen_id);

CREATE UNIQUE INDEX "IX_movimientos_stock_numero_movimiento" ON movimientos_stock (numero_movimiento);

CREATE INDEX "IX_movimientos_stock_orden_trabajo_id" ON movimientos_stock (orden_trabajo_id);

CREATE INDEX "IX_movimientos_stock_repuesto_id_fecha_utc" ON movimientos_stock (repuesto_id, fecha_utc);

CREATE INDEX "IX_movimientos_stock_reserva_id" ON movimientos_stock (reserva_id);

CREATE INDEX "IX_movimientos_stock_tipo_movimiento_id" ON movimientos_stock (tipo_movimiento_id);

CREATE INDEX "IX_movimientos_stock_transferencia_id" ON movimientos_stock (transferencia_id);

CREATE INDEX "IX_repuestos_categoria_id" ON repuestos (categoria_id);

CREATE UNIQUE INDEX "IX_repuestos_codigo" ON repuestos (codigo);

CREATE UNIQUE INDEX "IX_repuestos_codigo_sap" ON repuestos (codigo_sap) WHERE codigo_sap IS NOT NULL;

CREATE INDEX "IX_repuestos_unidad_id" ON repuestos (unidad_id);

CREATE INDEX "IX_reservas_stock_bodega_id" ON reservas_stock (bodega_id);

CREATE UNIQUE INDEX "IX_reservas_stock_codigo" ON reservas_stock (codigo);

CREATE INDEX "IX_reservas_stock_orden_trabajo_id" ON reservas_stock (orden_trabajo_id);

CREATE INDEX "IX_reservas_stock_repuesto_id" ON reservas_stock (repuesto_id);

CREATE INDEX "IX_stock_bodega_bodega_id" ON stock_bodega (bodega_id);

CREATE UNIQUE INDEX "IX_stock_bodega_repuesto_id_bodega_id_ubicacion_bodega_id" ON stock_bodega (repuesto_id, bodega_id, ubicacion_bodega_id);

CREATE INDEX "IX_stock_bodega_ubicacion_bodega_id" ON stock_bodega (ubicacion_bodega_id);

CREATE INDEX "IX_transferencias_stock_bodega_destino_id" ON transferencias_stock (bodega_destino_id);

CREATE INDEX "IX_transferencias_stock_bodega_origen_id" ON transferencias_stock (bodega_origen_id);

CREATE INDEX "IX_transferencias_stock_bodega_transito_id" ON transferencias_stock (bodega_transito_id);

CREATE UNIQUE INDEX "IX_transferencias_stock_codigo" ON transferencias_stock (codigo);

CREATE INDEX "IX_transferencias_stock_repuesto_id" ON transferencias_stock (repuesto_id);

CREATE UNIQUE INDEX "IX_ubicaciones_bodega_bodega_id_codigo" ON ubicaciones_bodega (bodega_id, codigo);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260710134248_InventoryDomainPostgreSql', '8.0.11');

COMMIT;

START TRANSACTION;

CREATE TABLE ubicaciones_tecnicas (
    id uuid NOT NULL,
    codigo character varying(120) NOT NULL,
    nombre character varying(240) NOT NULL,
    nombre_normalizado character varying(240) NOT NULL,
    faena_id uuid NOT NULL,
    ubicacion_padre_id uuid,
    tipo character varying(80),
    obsoleto boolean NOT NULL,
    creado_por_usuario_id character varying(120),
    actualizado_por_usuario_id character varying(120),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_ubicaciones_tecnicas" PRIMARY KEY (id),
    CONSTRAINT ck_ubicaciones_tecnicas_no_self_parent CHECK (ubicacion_padre_id IS NULL OR ubicacion_padre_id <> id),
    CONSTRAINT "FK_ubicaciones_tecnicas_faenas_faena_id" FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ubicaciones_tecnicas_ubicaciones_tecnicas_ubicacion_padre_id" FOREIGN KEY (ubicacion_padre_id) REFERENCES ubicaciones_tecnicas (id) ON DELETE RESTRICT
);

CREATE TABLE nodos_tecnicos (
    id uuid NOT NULL,
    codigo character varying(120) NOT NULL,
    nombre character varying(240) NOT NULL,
    nombre_normalizado character varying(240) NOT NULL,
    nivel character varying(40) NOT NULL,
    nodo_padre_id uuid,
    faena_id uuid,
    ubicacion_tecnica_id uuid,
    obsoleto boolean NOT NULL,
    fusionado_en_nodo_id uuid,
    creado_por_usuario_id character varying(120),
    actualizado_por_usuario_id character varying(120),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_nodos_tecnicos" PRIMARY KEY (id),
    CONSTRAINT ck_nodos_tecnicos_nivel CHECK (nivel IN ('Sistema','Subsistema','Componente','Subcomponente')),
    CONSTRAINT ck_nodos_tecnicos_no_self_merge CHECK (fusionado_en_nodo_id IS NULL OR fusionado_en_nodo_id <> id),
    CONSTRAINT ck_nodos_tecnicos_no_self_parent CHECK (nodo_padre_id IS NULL OR nodo_padre_id <> id),
    CONSTRAINT "FK_nodos_tecnicos_faenas_faena_id" FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_nodos_tecnicos_nodos_tecnicos_fusionado_en_nodo_id" FOREIGN KEY (fusionado_en_nodo_id) REFERENCES nodos_tecnicos (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_nodos_tecnicos_nodos_tecnicos_nodo_padre_id" FOREIGN KEY (nodo_padre_id) REFERENCES nodos_tecnicos (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_nodos_tecnicos_ubicaciones_tecnicas_ubicacion_tecnica_id" FOREIGN KEY (ubicacion_tecnica_id) REFERENCES ubicaciones_tecnicas (id) ON DELETE RESTRICT
);

CREATE TABLE nodo_tecnico_activos (
    id uuid NOT NULL,
    nodo_tecnico_id uuid NOT NULL,
    activo_id uuid NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_nodo_tecnico_activos" PRIMARY KEY (id),
    CONSTRAINT "FK_nodo_tecnico_activos_activos_activo_id" FOREIGN KEY (activo_id) REFERENCES activos (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_nodo_tecnico_activos_nodos_tecnicos_nodo_tecnico_id" FOREIGN KEY (nodo_tecnico_id) REFERENCES nodos_tecnicos (id) ON DELETE RESTRICT
);

CREATE TABLE nodo_tecnico_aliases (
    id uuid NOT NULL,
    nodo_tecnico_id uuid NOT NULL,
    alias character varying(240) NOT NULL,
    alias_normalizado character varying(240) NOT NULL,
    origen character varying(80) NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_nodo_tecnico_aliases" PRIMARY KEY (id),
    CONSTRAINT "FK_nodo_tecnico_aliases_nodos_tecnicos_nodo_tecnico_id" FOREIGN KEY (nodo_tecnico_id) REFERENCES nodos_tecnicos (id) ON DELETE RESTRICT
);

CREATE TABLE nodo_tecnico_familias (
    id uuid NOT NULL,
    nodo_tecnico_id uuid NOT NULL,
    familia_equipo_id uuid NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_nodo_tecnico_familias" PRIMARY KEY (id),
    CONSTRAINT "FK_nodo_tecnico_familias_familias_equipo_familia_equipo_id" FOREIGN KEY (familia_equipo_id) REFERENCES familias_equipo (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_nodo_tecnico_familias_nodos_tecnicos_nodo_tecnico_id" FOREIGN KEY (nodo_tecnico_id) REFERENCES nodos_tecnicos (id) ON DELETE RESTRICT
);

CREATE INDEX "IX_nodo_tecnico_activos_activo_id" ON nodo_tecnico_activos (activo_id);

CREATE UNIQUE INDEX "IX_nodo_tecnico_activos_nodo_tecnico_id_activo_id" ON nodo_tecnico_activos (nodo_tecnico_id, activo_id);

CREATE INDEX "IX_nodo_tecnico_aliases_alias_normalizado" ON nodo_tecnico_aliases (alias_normalizado);

CREATE UNIQUE INDEX "IX_nodo_tecnico_aliases_nodo_tecnico_id_alias_normalizado" ON nodo_tecnico_aliases (nodo_tecnico_id, alias_normalizado);

CREATE INDEX "IX_nodo_tecnico_familias_familia_equipo_id" ON nodo_tecnico_familias (familia_equipo_id);

CREATE UNIQUE INDEX "IX_nodo_tecnico_familias_nodo_tecnico_id_familia_equipo_id" ON nodo_tecnico_familias (nodo_tecnico_id, familia_equipo_id);

CREATE UNIQUE INDEX "IX_nodos_tecnicos_codigo" ON nodos_tecnicos (codigo);

CREATE INDEX "IX_nodos_tecnicos_faena_id" ON nodos_tecnicos (faena_id);

CREATE INDEX "IX_nodos_tecnicos_fusionado_en_nodo_id" ON nodos_tecnicos (fusionado_en_nodo_id);

CREATE INDEX "IX_nodos_tecnicos_nivel" ON nodos_tecnicos (nivel);

CREATE INDEX "IX_nodos_tecnicos_nodo_padre_id" ON nodos_tecnicos (nodo_padre_id);

CREATE INDEX "IX_nodos_tecnicos_nodo_padre_id_nivel_nombre_normalizado" ON nodos_tecnicos (nodo_padre_id, nivel, nombre_normalizado);

CREATE INDEX "IX_nodos_tecnicos_nombre_normalizado" ON nodos_tecnicos (nombre_normalizado);

CREATE INDEX "IX_nodos_tecnicos_obsoleto" ON nodos_tecnicos (obsoleto);

CREATE INDEX "IX_nodos_tecnicos_ubicacion_tecnica_id" ON nodos_tecnicos (ubicacion_tecnica_id);

CREATE UNIQUE INDEX "IX_ubicaciones_tecnicas_codigo" ON ubicaciones_tecnicas (codigo);

CREATE INDEX "IX_ubicaciones_tecnicas_faena_id" ON ubicaciones_tecnicas (faena_id);

CREATE INDEX "IX_ubicaciones_tecnicas_nombre" ON ubicaciones_tecnicas (nombre);

CREATE INDEX "IX_ubicaciones_tecnicas_nombre_normalizado" ON ubicaciones_tecnicas (nombre_normalizado);

CREATE INDEX "IX_ubicaciones_tecnicas_obsoleto" ON ubicaciones_tecnicas (obsoleto);

CREATE INDEX "IX_ubicaciones_tecnicas_ubicacion_padre_id" ON ubicaciones_tecnicas (ubicacion_padre_id);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260710164638_TechnicalHierarchyDomainPostgreSql', '8.0.11');

COMMIT;

START TRANSACTION;

ALTER TABLE archivos ADD activo_codigo character varying(80);

ALTER TABLE archivos ADD eliminado boolean NOT NULL DEFAULT FALSE;

ALTER TABLE archivos ADD eliminado_at_utc timestamptz;

ALTER TABLE archivos ADD eliminado_por_usuario_id character varying(120);

ALTER TABLE archivos ADD entidad_id character varying(240) NOT NULL DEFAULT '';

ALTER TABLE archivos ADD extension character varying(32) NOT NULL DEFAULT '';

ALTER TABLE archivos ADD faena_codigo character varying(80);

ALTER TABLE archivos ADD modo_almacenamiento character varying(80) NOT NULL DEFAULT '';

ALTER TABLE archivos ADD modulo character varying(120) NOT NULL DEFAULT '';

ALTER TABLE archivos ADD nombre_almacenado character varying(300) NOT NULL DEFAULT '';

ALTER TABLE archivos ADD numero_ot character varying(80);

ALTER TABLE archivos ADD proposito character varying(80) NOT NULL DEFAULT '';

ALTER TABLE archivos ADD tipo_entidad character varying(120) NOT NULL DEFAULT '';

ALTER TABLE archivos ADD ubicacion_fisica character varying(2000);

ALTER TABLE archivos ADD version_archivo integer NOT NULL DEFAULT 0;

UPDATE archivos
SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
    extension = CASE
        WHEN extension <> '' THEN extension
        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
        ELSE ''
    END,
    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
    modo_almacenamiento = CASE
        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
        ELSE 'ManualLink'
    END,
    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
    estado = CASE
        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
        ELSE 'Stored'
    END;

CREATE INDEX "IX_archivos_checksum" ON archivos (checksum);

UPDATE archivos
SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
    extension = CASE
        WHEN extension <> '' THEN extension
        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
        ELSE ''
    END,
    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
    modo_almacenamiento = CASE
        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
        ELSE 'ManualLink'
    END,
    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
    estado = CASE
        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
        ELSE 'Stored'
    END;

CREATE INDEX "IX_archivos_created_at_utc" ON archivos (created_at_utc);

UPDATE archivos
SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
    extension = CASE
        WHEN extension <> '' THEN extension
        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
        ELSE ''
    END,
    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
    modo_almacenamiento = CASE
        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
        ELSE 'ManualLink'
    END,
    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
    estado = CASE
        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
        ELSE 'Stored'
    END;

CREATE INDEX "IX_archivos_proveedor" ON archivos (proveedor);

UPDATE archivos
SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
    extension = CASE
        WHEN extension <> '' THEN extension
        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
        ELSE ''
    END,
    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
    modo_almacenamiento = CASE
        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
        ELSE 'ManualLink'
    END,
    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
    estado = CASE
        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
        ELSE 'Stored'
    END;

CREATE INDEX "IX_archivos_tipo_entidad_entidad_id_eliminado" ON archivos (tipo_entidad, entidad_id, eliminado);

UPDATE archivos
SET nombre_almacenado = CASE WHEN nombre_almacenado = '' THEN COALESCE(NULLIF(nombre, ''), 'archivo') ELSE nombre_almacenado END,
    extension = CASE
        WHEN extension <> '' THEN extension
        WHEN position('.' IN nombre) > 0 THEN lower(reverse(split_part(reverse(nombre), '.', 1)))
        ELSE ''
    END,
    proveedor = CASE WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor ELSE 'ManualLink' END,
    modo_almacenamiento = CASE
        WHEN modo_almacenamiento IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN modo_almacenamiento
        WHEN proveedor IN ('ManualLink', 'LocalSimulation', 'GraphApiReady') THEN proveedor
        ELSE 'ManualLink'
    END,
    proposito = CASE WHEN proposito <> '' THEN proposito ELSE 'Document' END,
    modulo = CASE WHEN modulo <> '' THEN modulo ELSE 'Legacy' END,
    tipo_entidad = CASE WHEN tipo_entidad <> '' THEN tipo_entidad ELSE 'Legacy' END,
    entidad_id = CASE WHEN entidad_id <> '' THEN entidad_id ELSE file_key END,
    version_archivo = CASE WHEN version_archivo < 1 THEN 1 ELSE version_archivo END,
    estado = CASE
        WHEN estado IN ('Stored', 'ManualLink', 'PendingManualLink', 'GraphApiReady', 'InvalidPath', 'Deleted') THEN estado
        ELSE 'Stored'
    END;

CREATE INDEX "IX_archivos_uri_logica" ON archivos (uri_logica);

ALTER TABLE archivos ADD CONSTRAINT ck_archivos_estado CHECK (estado IN ('Stored','ManualLink','PendingManualLink','GraphApiReady','InvalidPath','Deleted'));

ALTER TABLE archivos ADD CONSTRAINT ck_archivos_proveedor CHECK (proveedor IN ('ManualLink','LocalSimulation','GraphApiReady'));

ALTER TABLE archivos ADD CONSTRAINT ck_archivos_tamano_no_negativo CHECK (tamano_bytes IS NULL OR tamano_bytes >= 0);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260711041342_FileMetadataPostgreSql', '8.0.11');

COMMIT;

START TRANSACTION;

CREATE TABLE plantillas_pdf (
    id uuid NOT NULL,
    codigo character varying(120) NOT NULL,
    nombre character varying(240) NOT NULL,
    tipo_evento character varying(120) NOT NULL,
    asunto_plantilla character varying(500) NOT NULL,
    html_plantilla text NOT NULL,
    activo boolean NOT NULL,
    version_plantilla integer NOT NULL,
    creado_por_usuario_id character varying(120),
    actualizado_por_usuario_id character varying(120),
    archivo_id uuid,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_plantillas_pdf" PRIMARY KEY (id),
    CONSTRAINT ck_plantillas_pdf_version CHECK (version_plantilla >= 1),
    CONSTRAINT "FK_plantillas_pdf_archivos_archivo_id" FOREIGN KEY (archivo_id) REFERENCES archivos (id) ON DELETE RESTRICT
);

CREATE TABLE reglas_alerta (
    id uuid NOT NULL,
    codigo character varying(120) NOT NULL,
    nombre character varying(240) NOT NULL,
    tipo_evento character varying(120) NOT NULL,
    activa boolean NOT NULL,
    severidad character varying(40) NOT NULL,
    repetir_hasta_resolver boolean NOT NULL,
    genera_email boolean NOT NULL,
    genera_pdf boolean NOT NULL,
    plantilla_id uuid NOT NULL,
    faena_id uuid,
    creado_por_usuario_id character varying(120),
    actualizado_por_usuario_id character varying(120),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_reglas_alerta" PRIMARY KEY (id),
    CONSTRAINT ck_reglas_alerta_severidad CHECK (severidad IN ('Info','Warning','Critical')),
    CONSTRAINT "FK_reglas_alerta_faenas_faena_id" FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_reglas_alerta_plantillas_pdf_plantilla_id" FOREIGN KEY (plantilla_id) REFERENCES plantillas_pdf (id) ON DELETE RESTRICT
);

CREATE TABLE alertas (
    id uuid NOT NULL,
    regla_alerta_id uuid NOT NULL,
    titulo character varying(500) NOT NULL,
    mensaje text NOT NULL,
    severidad character varying(40) NOT NULL,
    estado character varying(40) NOT NULL,
    origen character varying(160) NOT NULL,
    clave_causa character varying(240) NOT NULL,
    clave_deduplicacion character varying(400) NOT NULL,
    faena_id uuid,
    tipo_entidad character varying(120),
    entidad_id character varying(240),
    repeticion_critica boolean NOT NULL,
    cantidad_repeticiones integer NOT NULL,
    reconocido_at_utc timestamptz,
    reconocido_por_usuario_id character varying(120),
    resuelto_at_utc timestamptz,
    resuelto_por_usuario_id character varying(120),
    motivo_resolucion character varying(1000),
    activa boolean NOT NULL,
    archivo_pdf_id uuid,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_alertas" PRIMARY KEY (id),
    CONSTRAINT ck_alertas_estado CHECK (estado IN ('Open','Acknowledged','Resolved')),
    CONSTRAINT ck_alertas_repeticiones CHECK (cantidad_repeticiones >= 1),
    CONSTRAINT ck_alertas_severidad CHECK (severidad IN ('Info','Warning','Critical')),
    CONSTRAINT "FK_alertas_archivos_archivo_pdf_id" FOREIGN KEY (archivo_pdf_id) REFERENCES archivos (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_alertas_faenas_faena_id" FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_alertas_reglas_alerta_regla_alerta_id" FOREIGN KEY (regla_alerta_id) REFERENCES reglas_alerta (id) ON DELETE RESTRICT
);

CREATE TABLE regla_alerta_destinatarios (
    id uuid NOT NULL,
    regla_alerta_id uuid NOT NULL,
    usuario_id uuid,
    rol_id uuid,
    destino character varying(320),
    canal character varying(40) NOT NULL,
    activo boolean NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_regla_alerta_destinatarios" PRIMARY KEY (id),
    CONSTRAINT "FK_regla_alerta_destinatarios_reglas_alerta_regla_alerta_id" FOREIGN KEY (regla_alerta_id) REFERENCES reglas_alerta (id) ON DELETE CASCADE,
    CONSTRAINT "FK_regla_alerta_destinatarios_roles_rol_id" FOREIGN KEY (rol_id) REFERENCES roles (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_regla_alerta_destinatarios_usuarios_usuario_id" FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT
);

CREATE TABLE notificaciones (
    id uuid NOT NULL,
    alerta_id uuid NOT NULL,
    canal character varying(40) NOT NULL,
    asunto character varying(500) NOT NULL,
    cuerpo text NOT NULL,
    estado character varying(40) NOT NULL,
    programado_at_utc timestamptz,
    enviado_at_utc timestamptz,
    cantidad_intentos integer NOT NULL,
    proveedor character varying(120),
    ultimo_error character varying(2000),
    archivo_pdf_id uuid,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_notificaciones" PRIMARY KEY (id),
    CONSTRAINT ck_notificaciones_estado CHECK (estado IN ('Pending','Sent','Failed','Cancelled')),
    CONSTRAINT "FK_notificaciones_alertas_alerta_id" FOREIGN KEY (alerta_id) REFERENCES alertas (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_notificaciones_archivos_archivo_pdf_id" FOREIGN KEY (archivo_pdf_id) REFERENCES archivos (id) ON DELETE RESTRICT
);

CREATE TABLE notificacion_destinatarios (
    id uuid NOT NULL,
    notificacion_id uuid NOT NULL,
    usuario_id uuid,
    rol_id uuid,
    destino character varying(320),
    estado_entrega character varying(40) NOT NULL,
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_notificacion_destinatarios" PRIMARY KEY (id),
    CONSTRAINT "FK_notificacion_destinatarios_notificaciones_notificacion_id" FOREIGN KEY (notificacion_id) REFERENCES notificaciones (id) ON DELETE CASCADE,
    CONSTRAINT "FK_notificacion_destinatarios_roles_rol_id" FOREIGN KEY (rol_id) REFERENCES roles (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_notificacion_destinatarios_usuarios_usuario_id" FOREIGN KEY (usuario_id) REFERENCES usuarios (id) ON DELETE RESTRICT
);

CREATE TABLE notificacion_intentos (
    id uuid NOT NULL,
    notificacion_id uuid NOT NULL,
    numero_intento integer NOT NULL,
    intentado_at_utc timestamptz NOT NULL,
    exitoso boolean NOT NULL,
    proveedor character varying(120),
    error character varying(2000),
    created_at_utc timestamptz NOT NULL,
    updated_at_utc timestamptz,
    CONSTRAINT "PK_notificacion_intentos" PRIMARY KEY (id),
    CONSTRAINT "FK_notificacion_intentos_notificaciones_notificacion_id" FOREIGN KEY (notificacion_id) REFERENCES notificaciones (id) ON DELETE CASCADE
);

CREATE INDEX "IX_alertas_archivo_pdf_id" ON alertas (archivo_pdf_id);

CREATE INDEX "IX_alertas_estado_severidad" ON alertas (estado, severidad);

CREATE INDEX "IX_alertas_faena_id" ON alertas (faena_id);

CREATE UNIQUE INDEX "IX_alertas_regla_alerta_id_clave_deduplicacion_activa" ON alertas (regla_alerta_id, clave_deduplicacion, activa) WHERE activa;

CREATE INDEX "IX_alertas_tipo_entidad_entidad_id" ON alertas (tipo_entidad, entidad_id);

CREATE UNIQUE INDEX "IX_notificacion_destinatarios_notificacion_id_destino" ON notificacion_destinatarios (notificacion_id, destino);

CREATE INDEX "IX_notificacion_destinatarios_rol_id" ON notificacion_destinatarios (rol_id);

CREATE INDEX "IX_notificacion_destinatarios_usuario_id" ON notificacion_destinatarios (usuario_id);

CREATE UNIQUE INDEX "IX_notificacion_intentos_notificacion_id_numero_intento" ON notificacion_intentos (notificacion_id, numero_intento);

CREATE INDEX "IX_notificaciones_alerta_id" ON notificaciones (alerta_id);

CREATE INDEX "IX_notificaciones_archivo_pdf_id" ON notificaciones (archivo_pdf_id);

CREATE INDEX "IX_notificaciones_estado_created_at_utc" ON notificaciones (estado, created_at_utc);

CREATE INDEX "IX_plantillas_pdf_archivo_id" ON plantillas_pdf (archivo_id);

CREATE UNIQUE INDEX "IX_plantillas_pdf_codigo" ON plantillas_pdf (codigo);

CREATE INDEX "IX_plantillas_pdf_tipo_evento_activo" ON plantillas_pdf (tipo_evento, activo);

CREATE UNIQUE INDEX "IX_regla_alerta_destinatarios_regla_alerta_id_destino_canal" ON regla_alerta_destinatarios (regla_alerta_id, destino, canal);

CREATE INDEX "IX_regla_alerta_destinatarios_rol_id" ON regla_alerta_destinatarios (rol_id);

CREATE INDEX "IX_regla_alerta_destinatarios_usuario_id" ON regla_alerta_destinatarios (usuario_id);

CREATE INDEX "IX_reglas_alerta_activa_severidad" ON reglas_alerta (activa, severidad);

CREATE UNIQUE INDEX "IX_reglas_alerta_codigo" ON reglas_alerta (codigo);

CREATE INDEX "IX_reglas_alerta_faena_id" ON reglas_alerta (faena_id);

CREATE INDEX "IX_reglas_alerta_plantilla_id" ON reglas_alerta (plantilla_id);

CREATE INDEX "IX_reglas_alerta_tipo_evento" ON reglas_alerta (tipo_evento);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260711134157_AlertsNotificationsPdfTemplatesPostgreSql', '8.0.11');

COMMIT;

