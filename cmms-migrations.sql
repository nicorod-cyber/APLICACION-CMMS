CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE EXTENSION IF NOT EXISTS pgcrypto;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE TABLE faenas (
        id uuid NOT NULL,
        codigo character varying(80) NOT NULL,
        nombre character varying(240) NOT NULL,
        activo boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT pk_faenas PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE TABLE familias_equipo (
        id uuid NOT NULL,
        codigo character varying(80) NOT NULL,
        nombre character varying(160) NOT NULL,
        activo boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT pk_familias_equipo PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE TABLE permisos (
        id uuid NOT NULL,
        codigo character varying(160) NOT NULL,
        nombre character varying(240) NOT NULL,
        activo boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT pk_permisos PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_audit_log_faena_codigo ON audit_log (faena_codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_audit_log_modulo_entidad ON audit_log (modulo, entidad);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_audit_log_occurred_at_utc ON audit_log (occurred_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_estados_operacionales_activo_codigo ON estados_operacionales_activo (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_faenas_codigo ON faenas (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_familias_equipo_codigo ON familias_equipo (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_permisos_codigo ON permisos (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_roles_codigo ON roles (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_usuarios_email ON usuarios (email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_usuarios_username ON usuarios (username);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_archivos_file_key ON archivos (file_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_activos_codigo ON activos (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_activos_estado_operacional_id ON activos (estado_operacional_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_activos_faena_id ON activos (faena_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_activos_familia_equipo_id ON activos (familia_equipo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_documentos_codigo ON documentos (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_rol_permisos_permiso_id ON rol_permisos (permiso_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_rol_permisos_rol_id_permiso_id_vigente ON rol_permisos (rol_id, permiso_id, vigente);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_usuario_roles_rol_id ON usuario_roles (rol_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_usuario_roles_usuario_id_rol_id_vigente ON usuario_roles (usuario_id, rol_id, vigente);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_usuario_faenas_faena_id ON usuario_faenas (faena_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_usuario_faenas_usuario_id_faena_id_vigente ON usuario_faenas (usuario_id, faena_id, vigente);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_eventos_estado_activo_activo_id ON eventos_estado_activo (activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_eventos_estado_activo_estado_anterior_id ON eventos_estado_activo (estado_anterior_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_eventos_estado_activo_estado_nuevo_id ON eventos_estado_activo (estado_nuevo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_documento_activos_activo_id ON documento_activos (activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_documento_activos_documento_id_activo_id_vigente ON documento_activos (documento_id, activo_id, vigente);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE INDEX ix_versiones_documento_archivo_id ON versiones_documento (archivo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    CREATE UNIQUE INDEX ix_versiones_documento_documento_id_numero_version ON versiones_documento (documento_id, numero_version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090001_InitialPostgreSqlSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('202607090001_InitialPostgreSqlSchema', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    INSERT INTO tipos_documentales (
        id, codigo, nombre, aplica_a, obligatorio, critico, bloquea_disponibilidad,
        dias_alerta, requiere_pdf_alerta, activo, created_by_user_id, created_at_utc)
    SELECT gen_random_uuid(), UPPER(TRIM(tipo_documento_codigo)), TRIM(tipo_documento_codigo), 'Activo', false, false, false,
           30, false, true, 'migration', now()
    FROM documentos
    WHERE tipo_documento_codigo IS NOT NULL AND TRIM(tipo_documento_codigo) <> ''
    GROUP BY UPPER(TRIM(tipo_documento_codigo)), TRIM(tipo_documento_codigo)
    ON CONFLICT DO NOTHING;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    INSERT INTO tipos_documentales (
        id, codigo, nombre, aplica_a, obligatorio, critico, bloquea_disponibilidad,
        dias_alerta, requiere_pdf_alerta, activo, created_by_user_id, created_at_utc)
    VALUES
        (gen_random_uuid(), 'REV-TEC', 'Revision tecnica', 'Activo', true, true, true, 30, false, true, 'migration', now()),
        (gen_random_uuid(), 'PERMISO', 'Permiso operacional', 'Activo', true, false, false, 30, false, true, 'migration', now()),
        (gen_random_uuid(), 'CERT', 'Certificado', 'Activo', false, false, false, 45, false, true, 'migration', now()),
        (gen_random_uuid(), 'FAENA-GRAL', 'Documento general de faena', 'Faena', false, false, false, 30, false, true, 'migration', now())
    ON CONFLICT DO NOTHING;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE archivos ADD ruta_logica character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD descripcion character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD tipo_documental_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD fecha_emision date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD fecha_vencimiento date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD vigente boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD anulado boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD anulado_por_usuario_id character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD anulado_at_utc timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD motivo_anulacion character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD created_by_user_id character varying(120) NOT NULL DEFAULT 'migration';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD updated_by_user_id character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD validado_por_usuario_id character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD validado_at_utc timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD rechazado_por_usuario_id character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD rechazado_at_utc timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD motivo_rechazo character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD fecha_vencimiento_validada boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD reemplaza_documento_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD reemplazado_por_documento_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD historico boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD critico boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD obligatorio boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD bloquea_disponibilidad boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD motivo_cambio character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    UPDATE documentos d
    SET tipo_documental_id = t.id
    FROM tipos_documentales t
    WHERE t.codigo = UPPER(TRIM(d.tipo_documento_codigo));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ALTER COLUMN tipo_documental_id TYPE uuid;
    ALTER TABLE documentos ALTER COLUMN tipo_documental_id SET NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos DROP COLUMN tipo_documento_codigo;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE versiones_documento ADD codigo_version character varying(80) NOT NULL DEFAULT '1';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE versiones_documento ADD fecha_carga_utc timestamptz NOT NULL DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE versiones_documento ADD cargado_por_usuario_id character varying(120) NOT NULL DEFAULT 'migration';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE versiones_documento ADD observaciones character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE versiones_documento ADD vigente boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    UPDATE versiones_documento v
    SET codigo_version = v.numero_version::text,
        vigente = v.numero_version = latest.max_version
    FROM (
        SELECT documento_id, MAX(numero_version) AS max_version
        FROM versiones_documento
        GROUP BY documento_id
    ) latest
    WHERE latest.documento_id = v.documento_id;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    DROP INDEX ix_documento_activos_documento_id_activo_id_vigente;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    CREATE UNIQUE INDEX ix_tipos_documentales_codigo ON tipos_documentales (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    CREATE INDEX ix_documentos_tipo_documental_id ON documentos (tipo_documental_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    CREATE INDEX ix_documentos_estado ON documentos (estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    CREATE INDEX ix_documentos_reemplaza_documento_id ON documentos (reemplaza_documento_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    CREATE INDEX ix_documentos_reemplazado_por_documento_id ON documentos (reemplazado_por_documento_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    CREATE UNIQUE INDEX ix_versiones_documento_documento_id_vigente ON versiones_documento (documento_id, vigente) WHERE vigente;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    CREATE UNIQUE INDEX ix_documento_activos_documento_id_activo_id_vigente ON documento_activos (documento_id, activo_id, vigente) WHERE vigente;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    CREATE INDEX ix_documento_faenas_faena_id ON documento_faenas (faena_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    CREATE UNIQUE INDEX ix_documento_faenas_documento_id_faena_id_vigente ON documento_faenas (documento_id, faena_id, vigente) WHERE vigente;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD CONSTRAINT fk_documentos_tipos_documentales_tipo_documental_id FOREIGN KEY (tipo_documental_id) REFERENCES tipos_documentales (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD CONSTRAINT fk_documentos_documentos_reemplaza_documento_id FOREIGN KEY (reemplaza_documento_id) REFERENCES documentos (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    ALTER TABLE documentos ADD CONSTRAINT fk_documentos_documentos_reemplazado_por_documento_id FOREIGN KEY (reemplazado_por_documento_id) REFERENCES documentos (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090002_DocumentDomainPostgreSql') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('202607090002_DocumentDomainPostgreSql', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090003_WorkNotificationsAndOrdersPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '202607090003_WorkNotificationsAndOrdersPostgreSql') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('202607090003_WorkNotificationsAndOrdersPostgreSql', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE SEQUENCE spare_part_number_seq START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE SEQUENCE stock_movement_number_seq START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE SEQUENCE stock_reservation_number_seq START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE SEQUENCE stock_transfer_number_seq START WITH 1 INCREMENT BY 1 NO MINVALUE NO MAXVALUE NO CYCLE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_bodegas_codigo" ON bodegas (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_bodegas_faena_id" ON bodegas (faena_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_bodegas_tipo_id" ON bodegas (tipo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_catalogos_inventario_categoria_codigo" ON catalogos_inventario (categoria, codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_movimientos_stock_bodega_destino_id" ON movimientos_stock (bodega_destino_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_movimientos_stock_bodega_origen_id" ON movimientos_stock (bodega_origen_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_movimientos_stock_numero_movimiento" ON movimientos_stock (numero_movimiento);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_movimientos_stock_orden_trabajo_id" ON movimientos_stock (orden_trabajo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_movimientos_stock_repuesto_id_fecha_utc" ON movimientos_stock (repuesto_id, fecha_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_movimientos_stock_reserva_id" ON movimientos_stock (reserva_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_movimientos_stock_tipo_movimiento_id" ON movimientos_stock (tipo_movimiento_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_movimientos_stock_transferencia_id" ON movimientos_stock (transferencia_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_repuestos_categoria_id" ON repuestos (categoria_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_repuestos_codigo" ON repuestos (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_repuestos_codigo_sap" ON repuestos (codigo_sap) WHERE codigo_sap IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_repuestos_unidad_id" ON repuestos (unidad_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_reservas_stock_bodega_id" ON reservas_stock (bodega_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_reservas_stock_codigo" ON reservas_stock (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_reservas_stock_orden_trabajo_id" ON reservas_stock (orden_trabajo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_reservas_stock_repuesto_id" ON reservas_stock (repuesto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_stock_bodega_bodega_id" ON stock_bodega (bodega_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_stock_bodega_repuesto_id_bodega_id_ubicacion_bodega_id" ON stock_bodega (repuesto_id, bodega_id, ubicacion_bodega_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_stock_bodega_ubicacion_bodega_id" ON stock_bodega (ubicacion_bodega_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_transferencias_stock_bodega_destino_id" ON transferencias_stock (bodega_destino_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_transferencias_stock_bodega_origen_id" ON transferencias_stock (bodega_origen_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_transferencias_stock_bodega_transito_id" ON transferencias_stock (bodega_transito_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_transferencias_stock_codigo" ON transferencias_stock (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE INDEX "IX_transferencias_stock_repuesto_id" ON transferencias_stock (repuesto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_ubicaciones_bodega_bodega_id_codigo" ON ubicaciones_bodega (bodega_id, codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710134248_InventoryDomainPostgreSql') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260710134248_InventoryDomainPostgreSql', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodo_tecnico_activos_activo_id" ON nodo_tecnico_activos (activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_nodo_tecnico_activos_nodo_tecnico_id_activo_id" ON nodo_tecnico_activos (nodo_tecnico_id, activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodo_tecnico_aliases_alias_normalizado" ON nodo_tecnico_aliases (alias_normalizado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_nodo_tecnico_aliases_nodo_tecnico_id_alias_normalizado" ON nodo_tecnico_aliases (nodo_tecnico_id, alias_normalizado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodo_tecnico_familias_familia_equipo_id" ON nodo_tecnico_familias (familia_equipo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_nodo_tecnico_familias_nodo_tecnico_id_familia_equipo_id" ON nodo_tecnico_familias (nodo_tecnico_id, familia_equipo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_nodos_tecnicos_codigo" ON nodos_tecnicos (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodos_tecnicos_faena_id" ON nodos_tecnicos (faena_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodos_tecnicos_fusionado_en_nodo_id" ON nodos_tecnicos (fusionado_en_nodo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodos_tecnicos_nivel" ON nodos_tecnicos (nivel);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodos_tecnicos_nodo_padre_id" ON nodos_tecnicos (nodo_padre_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodos_tecnicos_nodo_padre_id_nivel_nombre_normalizado" ON nodos_tecnicos (nodo_padre_id, nivel, nombre_normalizado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodos_tecnicos_nombre_normalizado" ON nodos_tecnicos (nombre_normalizado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodos_tecnicos_obsoleto" ON nodos_tecnicos (obsoleto);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_nodos_tecnicos_ubicacion_tecnica_id" ON nodos_tecnicos (ubicacion_tecnica_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_ubicaciones_tecnicas_codigo" ON ubicaciones_tecnicas (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_ubicaciones_tecnicas_faena_id" ON ubicaciones_tecnicas (faena_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_ubicaciones_tecnicas_nombre" ON ubicaciones_tecnicas (nombre);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_ubicaciones_tecnicas_nombre_normalizado" ON ubicaciones_tecnicas (nombre_normalizado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_ubicaciones_tecnicas_obsoleto" ON ubicaciones_tecnicas (obsoleto);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    CREATE INDEX "IX_ubicaciones_tecnicas_ubicacion_padre_id" ON ubicaciones_tecnicas (ubicacion_padre_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710164638_TechnicalHierarchyDomainPostgreSql') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260710164638_TechnicalHierarchyDomainPostgreSql', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD activo_codigo character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD eliminado boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD eliminado_at_utc timestamptz;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD eliminado_por_usuario_id character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD entidad_id character varying(240) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD extension character varying(32) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD faena_codigo character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD modo_almacenamiento character varying(80) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD modulo character varying(120) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD nombre_almacenado character varying(300) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD numero_ot character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD proposito character varying(80) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD tipo_entidad character varying(120) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD ubicacion_fisica character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD version_archivo integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    CREATE INDEX "IX_archivos_checksum" ON archivos (checksum);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    CREATE INDEX "IX_archivos_created_at_utc" ON archivos (created_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    CREATE INDEX "IX_archivos_proveedor" ON archivos (proveedor);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    CREATE INDEX "IX_archivos_tipo_entidad_entidad_id_eliminado" ON archivos (tipo_entidad, entidad_id, eliminado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    CREATE INDEX "IX_archivos_uri_logica" ON archivos (uri_logica);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD CONSTRAINT ck_archivos_estado CHECK (estado IN ('Stored','ManualLink','PendingManualLink','GraphApiReady','InvalidPath','Deleted'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD CONSTRAINT ck_archivos_proveedor CHECK (proveedor IN ('ManualLink','LocalSimulation','GraphApiReady'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    ALTER TABLE archivos ADD CONSTRAINT ck_archivos_tamano_no_negativo CHECK (tamano_bytes IS NULL OR tamano_bytes >= 0);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711041342_FileMetadataPostgreSql') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260711041342_FileMetadataPostgreSql', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
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
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_alertas_archivo_pdf_id" ON alertas (archivo_pdf_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_alertas_estado_severidad" ON alertas (estado, severidad);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_alertas_faena_id" ON alertas (faena_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_alertas_regla_alerta_id_clave_deduplicacion_activa" ON alertas (regla_alerta_id, clave_deduplicacion, activa) WHERE activa;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_alertas_tipo_entidad_entidad_id" ON alertas (tipo_entidad, entidad_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_notificacion_destinatarios_notificacion_id_destino" ON notificacion_destinatarios (notificacion_id, destino);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_notificacion_destinatarios_rol_id" ON notificacion_destinatarios (rol_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_notificacion_destinatarios_usuario_id" ON notificacion_destinatarios (usuario_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_notificacion_intentos_notificacion_id_numero_intento" ON notificacion_intentos (notificacion_id, numero_intento);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_notificaciones_alerta_id" ON notificaciones (alerta_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_notificaciones_archivo_pdf_id" ON notificaciones (archivo_pdf_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_notificaciones_estado_created_at_utc" ON notificaciones (estado, created_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_plantillas_pdf_archivo_id" ON plantillas_pdf (archivo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_plantillas_pdf_codigo" ON plantillas_pdf (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_plantillas_pdf_tipo_evento_activo" ON plantillas_pdf (tipo_evento, activo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_regla_alerta_destinatarios_regla_alerta_id_destino_canal" ON regla_alerta_destinatarios (regla_alerta_id, destino, canal);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_regla_alerta_destinatarios_rol_id" ON regla_alerta_destinatarios (rol_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_regla_alerta_destinatarios_usuario_id" ON regla_alerta_destinatarios (usuario_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_reglas_alerta_activa_severidad" ON reglas_alerta (activa, severidad);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_reglas_alerta_codigo" ON reglas_alerta (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_reglas_alerta_faena_id" ON reglas_alerta (faena_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_reglas_alerta_plantilla_id" ON reglas_alerta (plantilla_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    CREATE INDEX "IX_reglas_alerta_tipo_evento" ON reglas_alerta (tipo_evento);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711134157_AlertsNotificationsPdfTemplatesPostgreSql') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260711134157_AlertsNotificationsPdfTemplatesPostgreSql', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE TABLE solicitudes_repuestos (
        id uuid NOT NULL,
        numero_solicitud character varying(80) NOT NULL,
        estado character varying(80) NOT NULL,
        tipo character varying(80) NOT NULL,
        origen character varying(80) NOT NULL,
        faena_id uuid,
        orden_trabajo_id uuid,
        activo_id uuid,
        bodega_id uuid,
        solicitante_usuario_id character varying(120) NOT NULL,
        solicitado_at_utc timestamptz NOT NULL,
        descripcion_tecnica character varying(1000) NOT NULL,
        unidad character varying(40) NOT NULL,
        motivo character varying(500) NOT NULL,
        foto_referencia character varying(1000),
        codigo_tarea character varying(80),
        decision_stock character varying(500),
        aprobador_mantenimiento_usuario_id character varying(120),
        aprobado_mantenimiento_at_utc timestamptz,
        aprobador_bodega_usuario_id character varying(120),
        aprobado_bodega_at_utc timestamptz,
        rechazado_por_usuario_id character varying(120),
        rechazado_at_utc timestamptz,
        motivo_rechazo character varying(500),
        recibido_por_usuario_id character varying(120),
        recibido_at_utc timestamptz,
        convertido_por_usuario_id character varying(120),
        convertido_at_utc timestamptz,
        cerrado_at_utc timestamptz,
        observaciones character varying(2000),
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_solicitudes_repuestos" PRIMARY KEY (id),
        CONSTRAINT "FK_solicitudes_repuestos_activos_activo_id" FOREIGN KEY (activo_id) REFERENCES activos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_solicitudes_repuestos_bodegas_bodega_id" FOREIGN KEY (bodega_id) REFERENCES bodegas (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_solicitudes_repuestos_faenas_faena_id" FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_solicitudes_repuestos_ordenes_trabajo_sql_orden_trabajo_id" FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE TABLE solicitud_repuesto_historial (
        id uuid NOT NULL,
        solicitud_repuesto_id uuid NOT NULL,
        estado_anterior character varying(80) NOT NULL,
        estado_nuevo character varying(80) NOT NULL,
        usuario_id character varying(120) NOT NULL,
        fecha_utc timestamptz NOT NULL,
        motivo character varying(500) NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_solicitud_repuesto_historial" PRIMARY KEY (id),
        CONSTRAINT "FK_solicitud_repuesto_historial_solicitudes_repuestos_solicitu~" FOREIGN KEY (solicitud_repuesto_id) REFERENCES solicitudes_repuestos (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE TABLE solicitud_repuesto_items (
        id uuid NOT NULL,
        solicitud_repuesto_id uuid NOT NULL,
        repuesto_id uuid,
        reserva_id uuid,
        repuesto_maestro_codigo character varying(80),
        cantidad_solicitada numeric(14,2) NOT NULL,
        cantidad_aprobada numeric(14,2) NOT NULL,
        cantidad_reservada numeric(14,2) NOT NULL,
        cantidad_entregada numeric(14,2) NOT NULL,
        cantidad_devuelta numeric(14,2) NOT NULL,
        unidad character varying(40) NOT NULL,
        movimiento_entrega_numero character varying(80),
        observaciones character varying(1000),
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_solicitud_repuesto_items" PRIMARY KEY (id),
        CONSTRAINT ck_solicitud_repuesto_items_cantidades CHECK (cantidad_solicitada > 0 AND cantidad_aprobada >= 0 AND cantidad_reservada >= 0 AND cantidad_entregada >= 0 AND cantidad_devuelta >= 0),
        CONSTRAINT "FK_solicitud_repuesto_items_repuestos_repuesto_id" FOREIGN KEY (repuesto_id) REFERENCES repuestos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_solicitud_repuesto_items_reservas_stock_reserva_id" FOREIGN KEY (reserva_id) REFERENCES reservas_stock (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_solicitud_repuesto_items_solicitudes_repuestos_solicitud_re~" FOREIGN KEY (solicitud_repuesto_id) REFERENCES solicitudes_repuestos (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE INDEX "IX_solicitud_repuesto_historial_solicitud_repuesto_id_fecha_utc" ON solicitud_repuesto_historial (solicitud_repuesto_id, fecha_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE INDEX "IX_solicitud_repuesto_items_repuesto_id" ON solicitud_repuesto_items (repuesto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE INDEX "IX_solicitud_repuesto_items_reserva_id" ON solicitud_repuesto_items (reserva_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE INDEX "IX_solicitud_repuesto_items_solicitud_repuesto_id" ON solicitud_repuesto_items (solicitud_repuesto_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE INDEX "IX_solicitudes_repuestos_activo_id" ON solicitudes_repuestos (activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE INDEX "IX_solicitudes_repuestos_bodega_id" ON solicitudes_repuestos (bodega_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE INDEX "IX_solicitudes_repuestos_faena_id_estado" ON solicitudes_repuestos (faena_id, estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_solicitudes_repuestos_numero_solicitud" ON solicitudes_repuestos (numero_solicitud);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    CREATE INDEX "IX_solicitudes_repuestos_orden_trabajo_id" ON solicitudes_repuestos (orden_trabajo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713142806_MaterialRequestsPostgreSql') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713142806_MaterialRequestsPostgreSql', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713154641_OperationalDataSetsPostgreSql') THEN
    CREATE TABLE conjuntos_datos_operacionales (
        id uuid NOT NULL,
        codigo character varying(120) NOT NULL,
        contenido jsonb NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_conjuntos_datos_operacionales" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713154641_OperationalDataSetsPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_conjuntos_datos_operacionales_codigo" ON conjuntos_datos_operacionales (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713154641_OperationalDataSetsPostgreSql') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713154641_OperationalDataSetsPostgreSql', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713160000_MaterialRequestNumberSequence') THEN
    CREATE SEQUENCE IF NOT EXISTS material_request_number_seq START WITH 1 INCREMENT BY 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713160000_MaterialRequestNumberSequence') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713160000_MaterialRequestNumberSequence', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE TABLE costos (
        id uuid NOT NULL,
        numero character varying(80) NOT NULL,
        categoria character varying(50) NOT NULL,
        monto numeric(14,2) NOT NULL,
        moneda character varying(10) NOT NULL,
        descripcion character varying(1000) NOT NULL,
        fecha_utc timestamptz NOT NULL,
        "WorkOrderId" uuid,
        "AssetId" uuid,
        "FaenaId" uuid,
        "SparePartId" uuid,
        "StockMovementId" uuid,
        contrato_codigo character varying(80),
        proveedor_rut character varying(40),
        cantidad numeric(14,2),
        costo_unitario numeric(14,2),
        documento_url character varying(1000),
        "CreatedByUserId" text NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_costos" PRIMARY KEY (id),
        CONSTRAINT "FK_costos_activos_AssetId" FOREIGN KEY ("AssetId") REFERENCES activos (id),
        CONSTRAINT "FK_costos_faenas_FaenaId" FOREIGN KEY ("FaenaId") REFERENCES faenas (id),
        CONSTRAINT "FK_costos_movimientos_stock_StockMovementId" FOREIGN KEY ("StockMovementId") REFERENCES movimientos_stock (id),
        CONSTRAINT "FK_costos_ordenes_trabajo_sql_WorkOrderId" FOREIGN KEY ("WorkOrderId") REFERENCES ordenes_trabajo_sql (id),
        CONSTRAINT "FK_costos_repuestos_SparePartId" FOREIGN KEY ("SparePartId") REFERENCES repuestos (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE TABLE estados_pago (
        id uuid NOT NULL,
        numero character varying(80) NOT NULL,
        proveedor_rut character varying(40) NOT NULL,
        monto numeric(14,2) NOT NULL,
        "Currency" text NOT NULL,
        estado character varying(30) NOT NULL,
        "ContractCode" text,
        "FaenaId" uuid,
        "DocumentUrl" text,
        "CreatedByUserId" text NOT NULL,
        "UpdatedByUserId" text,
        "StatusChangedAtUtc" timestamp with time zone,
        "RejectReason" text,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_estados_pago" PRIMARY KEY (id),
        CONSTRAINT "FK_estados_pago_faenas_FaenaId" FOREIGN KEY ("FaenaId") REFERENCES faenas (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE TABLE tarifas_hh (
        id uuid NOT NULL,
        codigo character varying(80) NOT NULL,
        especialidad character varying(120),
        tarifa_hora numeric(14,2) NOT NULL,
        "IsActive" boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_tarifas_hh" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE INDEX "IX_costos_AssetId" ON costos ("AssetId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE INDEX "IX_costos_FaenaId_fecha_utc" ON costos ("FaenaId", fecha_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_costos_numero" ON costos (numero);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE INDEX "IX_costos_SparePartId" ON costos ("SparePartId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE INDEX "IX_costos_StockMovementId" ON costos ("StockMovementId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE INDEX "IX_costos_WorkOrderId" ON costos ("WorkOrderId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE INDEX "IX_estados_pago_FaenaId" ON estados_pago ("FaenaId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_estados_pago_numero" ON estados_pago (numero);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE INDEX "IX_estados_pago_proveedor_rut_estado" ON estados_pago (proveedor_rut, estado);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    CREATE UNIQUE INDEX "IX_tarifas_hh_codigo" ON tarifas_hh (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260713163856_CostsAndPaymentStatementsPostgreSql') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260713163856_CostsAndPaymentStatementsPostgreSql', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos DROP CONSTRAINT ck_activos_estado_registro;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE estados_operacionales_activo DROP CONSTRAINT ck_estados_operacionales_activo_codigo;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE familias_equipo ADD descripcion character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE familias_equipo ADD marca_referencia character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE familias_equipo ADD modelo_referencia character varying(120);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE familias_equipo ADD tipo_activo_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ALTER COLUMN propiedad TYPE character varying(80);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ALTER COLUMN numero_serie TYPE character varying(160);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ALTER COLUMN familia_equipo_id DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ALTER COLUMN faena_id DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ALTER COLUMN criticidad TYPE character varying(40);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD anio_fabricacion smallint;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD fecha_adquisicion date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD fecha_baja date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD fecha_puesta_servicio date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD observaciones character varying(2000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD tipo_activo_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD tipo_medicion_uso character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD ubicacion_tecnica_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE lecturas_activo (
        id uuid NOT NULL,
        activo_id uuid NOT NULL,
        fecha_lectura_utc timestamptz NOT NULL,
        valor numeric(18,2) NOT NULL,
        origen character varying(40) NOT NULL,
        orden_trabajo_id uuid,
        registrado_por_usuario_id character varying(120),
        evidencia_referencia character varying(1000),
        observaciones character varying(1000),
        es_correccion boolean NOT NULL,
        lectura_corregida_id uuid,
        motivo_correccion character varying(1000),
        autorizado_por_usuario_id character varying(120),
        es_anomala boolean NOT NULL,
        mensaje_validacion character varying(1000),
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_lecturas_activo" PRIMARY KEY (id),
        CONSTRAINT ck_lecturas_activo_origen CHECK (origen IN ('MANUAL','ORDEN_TRABAJO','IMPORTACION','SAP','TELEMETRIA')),
        CONSTRAINT ck_lecturas_activo_valor CHECK (valor >= 0),
        CONSTRAINT "FK_lecturas_activo_activos_activo_id" FOREIGN KEY (activo_id) REFERENCES activos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_lecturas_activo_lecturas_activo_lectura_corregida_id" FOREIGN KEY (lectura_corregida_id) REFERENCES lecturas_activo (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_lecturas_activo_ordenes_trabajo_sql_orden_trabajo_id" FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE roles_componente_unidad (
        id uuid NOT NULL,
        codigo character varying(60) NOT NULL,
        nombre character varying(160) NOT NULL,
        descripcion character varying(500),
        activo boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_roles_componente_unidad" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE tipos_activo (
        id uuid NOT NULL,
        codigo character varying(60) NOT NULL,
        nombre character varying(160) NOT NULL,
        descripcion character varying(1000),
        categoria character varying(60) NOT NULL,
        es_movil boolean NOT NULL,
        es_montable boolean NOT NULL,
        puede_ser_portador boolean NOT NULL,
        controla_mantenimiento boolean NOT NULL,
        participa_en_disponibilidad boolean NOT NULL,
        orden_visualizacion integer NOT NULL,
        activo boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_tipos_activo" PRIMARY KEY (id),
        CONSTRAINT ck_tipos_activo_orden CHECK (orden_visualizacion >= 0)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE tipos_unidad_operativa (
        id uuid NOT NULL,
        codigo character varying(60) NOT NULL,
        nombre character varying(160) NOT NULL,
        descripcion character varying(1000),
        participa_en_disponibilidad boolean NOT NULL,
        activo boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_tipos_unidad_operativa" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE definiciones_atributo_activo (
        id uuid NOT NULL,
        tipo_activo_id uuid NOT NULL,
        familia_equipo_id uuid,
        codigo character varying(80) NOT NULL,
        nombre character varying(160) NOT NULL,
        descripcion character varying(1000),
        tipo_dato character varying(30) NOT NULL,
        unidad character varying(40),
        obligatorio boolean NOT NULL,
        es_identificador boolean NOT NULL,
        es_unico boolean NOT NULL,
        permite_busqueda boolean NOT NULL,
        permite_filtro boolean NOT NULL,
        mostrar_en_listado boolean NOT NULL,
        valor_minimo numeric(18,4),
        valor_maximo numeric(18,4),
        patron_validacion character varying(500),
        opciones_json jsonb,
        grupo_visualizacion character varying(120),
        orden_visualizacion integer NOT NULL,
        activo boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_definiciones_atributo_activo" PRIMARY KEY (id),
        CONSTRAINT ck_definiciones_atributo_rango CHECK (valor_minimo IS NULL OR valor_maximo IS NULL OR valor_minimo <= valor_maximo),
        CONSTRAINT ck_definiciones_atributo_tipo CHECK (tipo_dato IN ('TEXTO','NUMERO','ENTERO','BOOLEANO','FECHA','OPCION')),
        CONSTRAINT "FK_definiciones_atributo_activo_familias_equipo_familia_equipo~" FOREIGN KEY (familia_equipo_id) REFERENCES familias_equipo (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_definiciones_atributo_activo_tipos_activo_tipo_activo_id" FOREIGN KEY (tipo_activo_id) REFERENCES tipos_activo (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE requisitos_documentales_tipo_activo (
        id uuid NOT NULL,
        tipo_activo_id uuid NOT NULL,
        familia_equipo_id uuid,
        tipo_documental_id uuid NOT NULL,
        obligatorio boolean NOT NULL,
        critico boolean NOT NULL,
        bloquea_disponibilidad boolean NOT NULL,
        requiere_fecha_vencimiento boolean NOT NULL,
        dias_alerta integer,
        activo boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_requisitos_documentales_tipo_activo" PRIMARY KEY (id),
        CONSTRAINT ck_requisitos_documentales_dias_alerta CHECK (dias_alerta IS NULL OR dias_alerta >= 0),
        CONSTRAINT "FK_requisitos_documentales_tipo_activo_familias_equipo_familia~" FOREIGN KEY (familia_equipo_id) REFERENCES familias_equipo (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_requisitos_documentales_tipo_activo_tipos_activo_tipo_activ~" FOREIGN KEY (tipo_activo_id) REFERENCES tipos_activo (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_requisitos_documentales_tipo_activo_tipos_documentales_tipo~" FOREIGN KEY (tipo_documental_id) REFERENCES tipos_documentales (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE reglas_composicion_unidad (
        id uuid NOT NULL,
        tipo_unidad_operativa_id uuid NOT NULL,
        rol_componente_id uuid NOT NULL,
        cantidad_minima integer NOT NULL,
        cantidad_maxima integer NOT NULL,
        obligatorio boolean NOT NULL,
        activo boolean NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_reglas_composicion_unidad" PRIMARY KEY (id),
        CONSTRAINT ck_reglas_composicion_cantidades CHECK (cantidad_minima >= 0 AND cantidad_maxima >= cantidad_minima),
        CONSTRAINT ck_reglas_composicion_obligatorio CHECK ((obligatorio AND cantidad_minima > 0) OR (NOT obligatorio AND cantidad_minima = 0)),
        CONSTRAINT "FK_reglas_composicion_unidad_roles_componente_unidad_rol_compo~" FOREIGN KEY (rol_componente_id) REFERENCES roles_componente_unidad (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_reglas_composicion_unidad_tipos_unidad_operativa_tipo_unida~" FOREIGN KEY (tipo_unidad_operativa_id) REFERENCES tipos_unidad_operativa (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE unidades_operativas (
        id uuid NOT NULL,
        codigo character varying(80) NOT NULL,
        nombre character varying(240) NOT NULL,
        tipo_unidad_operativa_id uuid NOT NULL,
        faena_id uuid,
        ubicacion_tecnica_id uuid,
        estado_operacional_id uuid NOT NULL,
        criticidad character varying(40),
        fecha_puesta_servicio date,
        fecha_baja date,
        observaciones character varying(2000),
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_unidades_operativas" PRIMARY KEY (id),
        CONSTRAINT ck_unidades_operativas_fecha_baja CHECK (fecha_baja IS NULL OR fecha_puesta_servicio IS NULL OR fecha_baja >= fecha_puesta_servicio),
        CONSTRAINT "FK_unidades_operativas_estados_operacionales_activo_estado_ope~" FOREIGN KEY (estado_operacional_id) REFERENCES estados_operacionales_activo (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_unidades_operativas_faenas_faena_id" FOREIGN KEY (faena_id) REFERENCES faenas (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_unidades_operativas_tipos_unidad_operativa_tipo_unidad_oper~" FOREIGN KEY (tipo_unidad_operativa_id) REFERENCES tipos_unidad_operativa (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_unidades_operativas_ubicaciones_tecnicas_ubicacion_tecnica_~" FOREIGN KEY (ubicacion_tecnica_id) REFERENCES ubicaciones_tecnicas (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE valores_atributo_activo (
        id uuid NOT NULL,
        activo_id uuid NOT NULL,
        definicion_atributo_id uuid NOT NULL,
        valor_texto character varying(2000),
        valor_numerico numeric(18,4),
        valor_booleano boolean,
        valor_fecha date,
        observaciones character varying(1000),
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_valores_atributo_activo" PRIMARY KEY (id),
        CONSTRAINT ck_valores_atributo_un_valor CHECK ((CASE WHEN valor_texto IS NULL THEN 0 ELSE 1 END + CASE WHEN valor_numerico IS NULL THEN 0 ELSE 1 END + CASE WHEN valor_booleano IS NULL THEN 0 ELSE 1 END + CASE WHEN valor_fecha IS NULL THEN 0 ELSE 1 END) <= 1),
        CONSTRAINT "FK_valores_atributo_activo_activos_activo_id" FOREIGN KEY (activo_id) REFERENCES activos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_valores_atributo_activo_definiciones_atributo_activo_defini~" FOREIGN KEY (definicion_atributo_id) REFERENCES definiciones_atributo_activo (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE TABLE componentes_unidad_operativa (
        id uuid NOT NULL,
        unidad_operativa_id uuid NOT NULL,
        activo_id uuid NOT NULL,
        rol_componente_id uuid NOT NULL,
        fecha_montaje_utc timestamptz NOT NULL,
        fecha_desmontaje_utc timestamptz,
        orden_trabajo_montaje_id uuid,
        orden_trabajo_desmontaje_id uuid,
        observaciones character varying(1000),
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_componentes_unidad_operativa" PRIMARY KEY (id),
        CONSTRAINT ck_componentes_unidad_fechas CHECK (fecha_desmontaje_utc IS NULL OR fecha_desmontaje_utc >= fecha_montaje_utc),
        CONSTRAINT "FK_componentes_unidad_operativa_activos_activo_id" FOREIGN KEY (activo_id) REFERENCES activos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_componentes_unidad_operativa_ordenes_trabajo_sql_orden_trab~" FOREIGN KEY (orden_trabajo_montaje_id) REFERENCES ordenes_trabajo_sql (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_componentes_unidad_operativa_ordenes_trabajo_sql_orden_tra~1" FOREIGN KEY (orden_trabajo_desmontaje_id) REFERENCES ordenes_trabajo_sql (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_componentes_unidad_operativa_roles_componente_unidad_rol_co~" FOREIGN KEY (rol_componente_id) REFERENCES roles_componente_unidad (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_componentes_unidad_operativa_unidades_operativas_unidad_ope~" FOREIGN KEY (unidad_operativa_id) REFERENCES unidades_operativas (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_familias_equipo_tipo_activo_id" ON familias_equipo (tipo_activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_activos_tipo_activo_id" ON activos (tipo_activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_activos_ubicacion_tecnica_id" ON activos (ubicacion_tecnica_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD CONSTRAINT ck_activos_anio_fabricacion CHECK (anio_fabricacion IS NULL OR anio_fabricacion BETWEEN 1900 AND 2200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD CONSTRAINT ck_activos_fecha_baja CHECK (fecha_baja IS NULL OR fecha_puesta_servicio IS NULL OR fecha_baja >= fecha_puesta_servicio);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD CONSTRAINT ck_activos_tipo_medicion_uso CHECK (tipo_medicion_uso IS NULL OR tipo_medicion_uso IN ('HOROMETRO','KILOMETRAJE'));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_componentes_unidad_operativa_activo_id_fecha_desmontaje_utc" ON componentes_unidad_operativa (activo_id, fecha_desmontaje_utc) WHERE fecha_desmontaje_utc IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_componentes_unidad_operativa_orden_trabajo_desmontaje_id" ON componentes_unidad_operativa (orden_trabajo_desmontaje_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_componentes_unidad_operativa_orden_trabajo_montaje_id" ON componentes_unidad_operativa (orden_trabajo_montaje_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_componentes_unidad_operativa_rol_componente_id" ON componentes_unidad_operativa (rol_componente_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_componentes_unidad_operativa_unidad_operativa_id" ON componentes_unidad_operativa (unidad_operativa_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_definiciones_atributo_activo_familia_equipo_id" ON definiciones_atributo_activo (familia_equipo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_definiciones_atributo_activo_tipo_activo_id_codigo" ON definiciones_atributo_activo (tipo_activo_id, codigo) WHERE familia_equipo_id IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_definiciones_atributo_activo_tipo_activo_id_familia_equipo_~" ON definiciones_atributo_activo (tipo_activo_id, familia_equipo_id, codigo) WHERE familia_equipo_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_lecturas_activo_activo_id_fecha_lectura_utc" ON lecturas_activo (activo_id, fecha_lectura_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_lecturas_activo_lectura_corregida_id" ON lecturas_activo (lectura_corregida_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_lecturas_activo_orden_trabajo_id" ON lecturas_activo (orden_trabajo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_reglas_composicion_unidad_rol_componente_id" ON reglas_composicion_unidad (rol_componente_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_reglas_composicion_unidad_tipo_unidad_operativa_id_rol_comp~" ON reglas_composicion_unidad (tipo_unidad_operativa_id, rol_componente_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_requisitos_documentales_tipo_activo_familia_equipo_id" ON requisitos_documentales_tipo_activo (familia_equipo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_requisitos_documentales_tipo_activo_tipo_activo_id_familia_~" ON requisitos_documentales_tipo_activo (tipo_activo_id, familia_equipo_id, tipo_documental_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_requisitos_documentales_tipo_activo_tipo_documental_id" ON requisitos_documentales_tipo_activo (tipo_documental_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_roles_componente_unidad_codigo" ON roles_componente_unidad (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_tipos_activo_codigo" ON tipos_activo (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_tipos_unidad_operativa_codigo" ON tipos_unidad_operativa (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_unidades_operativas_codigo" ON unidades_operativas (codigo);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_unidades_operativas_estado_operacional_id" ON unidades_operativas (estado_operacional_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_unidades_operativas_faena_id" ON unidades_operativas (faena_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_unidades_operativas_tipo_unidad_operativa_id" ON unidades_operativas (tipo_unidad_operativa_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_unidades_operativas_ubicacion_tecnica_id" ON unidades_operativas (ubicacion_tecnica_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE UNIQUE INDEX "IX_valores_atributo_activo_activo_id_definicion_atributo_id" ON valores_atributo_activo (activo_id, definicion_atributo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    CREATE INDEX "IX_valores_atributo_activo_definicion_atributo_id" ON valores_atributo_activo (definicion_atributo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN

                    INSERT INTO tipos_activo (id, codigo, nombre, categoria, es_movil, es_montable, puede_ser_portador, controla_mantenimiento, participa_en_disponibilidad, orden_visualizacion, activo, created_at_utc)
                    SELECT gen_random_uuid(), codigo, nombre, 'LEGADO', false, false, false, true, true, 0, true, now()
                    FROM (
                        SELECT DISTINCT upper(regexp_replace(trim(tipo_activo), '[^A-Za-z0-9]+', '_', 'g')) AS codigo, trim(tipo_activo) AS nombre
                        FROM activos WHERE nullif(trim(tipo_activo), '') IS NOT NULL
                    ) legacy
                    ON CONFLICT (codigo) DO NOTHING;

                    INSERT INTO tipos_activo (id, codigo, nombre, categoria, es_movil, es_montable, puede_ser_portador, controla_mantenimiento, participa_en_disponibilidad, orden_visualizacion, activo, created_at_utc)
                    SELECT gen_random_uuid(), 'SIN_CLASIFICAR', 'Sin clasificar', 'LEGADO', false, false, false, true, true, 9999, true, now()
                    WHERE NOT EXISTS (SELECT 1 FROM tipos_activo WHERE codigo = 'SIN_CLASIFICAR');

                    UPDATE activos a
                    SET tipo_activo_id = t.id
                    FROM tipos_activo t
                    WHERE t.codigo = coalesce(nullif(upper(regexp_replace(trim(a.tipo_activo), '[^A-Za-z0-9]+', '_', 'g')), ''), 'SIN_CLASIFICAR');

                    UPDATE familias_equipo f
                    SET tipo_activo_id = coalesce((SELECT a.tipo_activo_id FROM activos a WHERE a.familia_equipo_id = f.id GROUP BY a.tipo_activo_id ORDER BY count(*) DESC LIMIT 1), (SELECT id FROM tipos_activo WHERE codigo = 'SIN_CLASIFICAR'));

                    UPDATE activos a
                    SET ubicacion_tecnica_id = u.id
                    FROM ubicaciones_tecnicas u
                    WHERE upper(trim(u.codigo)) = upper(trim(a.ubicacion_tecnica_codigo));

                    UPDATE activos a
                    SET observaciones = concat_ws(E'\n', nullif(observaciones, ''), CASE WHEN nullif(trim(ubicacion_tecnica_codigo), '') IS NOT NULL AND ubicacion_tecnica_id IS NULL THEN '[MIGRACION] ubicacion tecnica legado no encontrada: ' || ubicacion_tecnica_codigo END)
                    WHERE nullif(trim(ubicacion_tecnica_codigo), '') IS NOT NULL;

                    INSERT INTO definiciones_atributo_activo (id, tipo_activo_id, codigo, nombre, tipo_dato, obligatorio, es_identificador, es_unico, permite_busqueda, permite_filtro, mostrar_en_listado, orden_visualizacion, activo, created_at_utc)
                    SELECT gen_random_uuid(), a.tipo_activo_id, 'PATENTE', 'Patente', 'TEXTO', false, true, false, true, true, true, 0, true, now()
                    FROM activos a WHERE nullif(trim(a.patente), '') IS NOT NULL
                    GROUP BY a.tipo_activo_id
                    ON CONFLICT DO NOTHING;

                    INSERT INTO valores_atributo_activo (id, activo_id, definicion_atributo_id, valor_texto, created_at_utc)
                    SELECT gen_random_uuid(), a.id, d.id, a.patente, now()
                    FROM activos a JOIN definiciones_atributo_activo d ON d.tipo_activo_id = a.tipo_activo_id AND d.codigo = 'PATENTE' AND d.familia_equipo_id IS NULL
                    WHERE nullif(trim(a.patente), '') IS NOT NULL;

                    INSERT INTO estados_operacionales_activo (id, codigo, nombre, activo, created_at_utc)
                    SELECT gen_random_uuid(), 'DADO_DE_BAJA', 'Dado de baja', true, now()
                    WHERE NOT EXISTS (SELECT 1 FROM estados_operacionales_activo WHERE codigo = 'DADO_DE_BAJA');

                    UPDATE activos a SET estado_operacional_id = s.id
                    FROM estados_operacionales_activo s
                    WHERE s.codigo = 'DADO_DE_BAJA' AND lower(coalesce(a.estado_registro, '')) IN ('baja', 'obsoleto', 'no_vigente', 'anulado');

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD CONSTRAINT "FK_activos_tipos_activo_tipo_activo_id" FOREIGN KEY (tipo_activo_id) REFERENCES tipos_activo (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos ADD CONSTRAINT "FK_activos_ubicaciones_tecnicas_ubicacion_tecnica_id" FOREIGN KEY (ubicacion_tecnica_id) REFERENCES ubicaciones_tecnicas (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE familias_equipo ADD CONSTRAINT "FK_familias_equipo_tipos_activo_tipo_activo_id" FOREIGN KEY (tipo_activo_id) REFERENCES tipos_activo (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos DROP COLUMN estado_documental;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos DROP COLUMN estado_registro;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos DROP COLUMN ficha_validada;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos DROP COLUMN patente;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos DROP COLUMN tipo_activo;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    ALTER TABLE activos DROP COLUMN ubicacion_tecnica_codigo;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714160353_AssetOperationalModelRefactoring') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714160353_AssetOperationalModelRefactoring', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    ALTER TABLE ordenes_trabajo_sql ALTER COLUMN activo_id DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    ALTER TABLE ordenes_trabajo_sql ADD unidad_operativa_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    ALTER TABLE avisos_trabajo_sql ADD unidad_operativa_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    CREATE TABLE orden_trabajo_activos (
        id uuid NOT NULL,
        orden_trabajo_id uuid NOT NULL,
        activo_id uuid NOT NULL,
        rol character varying(20) NOT NULL,
        activo_codigo_snapshot character varying(80) NOT NULL,
        activo_nombre_snapshot character varying(240) NOT NULL,
        agregado_en_utc timestamptz NOT NULL,
        agregado_por_usuario_id character varying(120) NOT NULL,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_orden_trabajo_activos" PRIMARY KEY (id),
        CONSTRAINT ck_orden_trabajo_activos_rol CHECK (rol IN ('PRINCIPAL','AFECTADO','MONTAJE','DESMONTAJE')),
        CONSTRAINT "FK_orden_trabajo_activos_activos_activo_id" FOREIGN KEY (activo_id) REFERENCES activos (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_orden_trabajo_activos_ordenes_trabajo_sql_orden_trabajo_id" FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo_sql (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN

                    INSERT INTO orden_trabajo_activos (id, orden_trabajo_id, activo_id, rol, activo_codigo_snapshot, activo_nombre_snapshot, agregado_en_utc, agregado_por_usuario_id, created_at_utc)
                    SELECT gen_random_uuid(), ot.id, a.id, 'PRINCIPAL', a.codigo, a.nombre, ot.created_at_utc, ot.creado_por_usuario_id, now()
                    FROM ordenes_trabajo_sql ot
                    INNER JOIN activos a ON a.id = ot.activo_id
                    WHERE NOT EXISTS (SELECT 1 FROM orden_trabajo_activos ota WHERE ota.orden_trabajo_id = ot.id AND ota.activo_id = a.id);

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    CREATE INDEX "IX_ordenes_trabajo_sql_unidad_operativa_id" ON ordenes_trabajo_sql (unidad_operativa_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    ALTER TABLE ordenes_trabajo_sql ADD CONSTRAINT ck_ordenes_trabajo_sql_objetivo CHECK (activo_id IS NOT NULL OR unidad_operativa_id IS NOT NULL);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    CREATE INDEX "IX_avisos_trabajo_sql_unidad_operativa_id" ON avisos_trabajo_sql (unidad_operativa_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    CREATE INDEX "IX_orden_trabajo_activos_activo_id" ON orden_trabajo_activos (activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    CREATE UNIQUE INDEX "IX_orden_trabajo_activos_orden_trabajo_id_activo_id" ON orden_trabajo_activos (orden_trabajo_id, activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    CREATE INDEX "IX_orden_trabajo_activos_orden_trabajo_id_rol" ON orden_trabajo_activos (orden_trabajo_id, rol);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    ALTER TABLE avisos_trabajo_sql ADD CONSTRAINT "FK_avisos_trabajo_sql_unidades_operativas_unidad_operativa_id" FOREIGN KEY (unidad_operativa_id) REFERENCES unidades_operativas (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    ALTER TABLE ordenes_trabajo_sql ADD CONSTRAINT "FK_ordenes_trabajo_sql_unidades_operativas_unidad_operativa_id" FOREIGN KEY (unidad_operativa_id) REFERENCES unidades_operativas (id) ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714165414_WorkOrderOperationalUnitTargets') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714165414_WorkOrderOperationalUnitTargets', '8.0.11');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714170216_OperationalUnitAllowedComponents') THEN
    CREATE TABLE reglas_composicion_unidad_activos_permitidos (
        id uuid NOT NULL,
        regla_composicion_id uuid NOT NULL,
        tipo_activo_id uuid,
        familia_equipo_id uuid,
        created_at_utc timestamptz NOT NULL,
        updated_at_utc timestamptz,
        CONSTRAINT "PK_reglas_composicion_unidad_activos_permitidos" PRIMARY KEY (id),
        CONSTRAINT ck_reglas_composicion_permitidos_objetivo CHECK (tipo_activo_id IS NOT NULL OR familia_equipo_id IS NOT NULL),
        CONSTRAINT "FK_reglas_composicion_unidad_activos_permitidos_familias_equip~" FOREIGN KEY (familia_equipo_id) REFERENCES familias_equipo (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_reglas_composicion_unidad_activos_permitidos_reglas_composi~" FOREIGN KEY (regla_composicion_id) REFERENCES reglas_composicion_unidad (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_reglas_composicion_unidad_activos_permitidos_tipos_activo_t~" FOREIGN KEY (tipo_activo_id) REFERENCES tipos_activo (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714170216_OperationalUnitAllowedComponents') THEN
    CREATE INDEX "IX_reglas_composicion_unidad_activos_permitidos_familia_equipo~" ON reglas_composicion_unidad_activos_permitidos (familia_equipo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714170216_OperationalUnitAllowedComponents') THEN
    CREATE UNIQUE INDEX "IX_reglas_composicion_unidad_activos_permitidos_regla_composic~" ON reglas_composicion_unidad_activos_permitidos (regla_composicion_id, tipo_activo_id, familia_equipo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714170216_OperationalUnitAllowedComponents') THEN
    CREATE INDEX "IX_reglas_composicion_unidad_activos_permitidos_tipo_activo_id" ON reglas_composicion_unidad_activos_permitidos (tipo_activo_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260714170216_OperationalUnitAllowedComponents') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260714170216_OperationalUnitAllowedComponents', '8.0.11');
    END IF;
END $EF$;
COMMIT;

