-- Modelo relacional CMMS
-- Motor objetivo: PostgreSQL 16+
-- Para SQL Server: uuid -> uniqueidentifier, jsonb -> nvarchar(max) con validacion JSON,
-- timestamptz -> datetimeoffset y gen_random_uuid() -> NEWID().

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE SCHEMA IF NOT EXISTS cmms;
SET search_path TO cmms, public;

-- ============================================================
-- 1. ORGANIZACION, SEGURIDAD Y ACCESO
-- ============================================================

CREATE TABLE organizaciones (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rut                 varchar(20),
    nombre              varchar(200) NOT NULL,
    tipo                varchar(30) NOT NULL DEFAULT 'CLIENTE',
    activo              boolean NOT NULL DEFAULT true,
    creado_en           timestamptz NOT NULL DEFAULT now(),
    actualizado_en      timestamptz,
    CONSTRAINT uq_organizaciones_rut UNIQUE (rut),
    CONSTRAINT ck_organizaciones_tipo CHECK (tipo IN ('INTERNA','CLIENTE','CONTRATISTA','OTRA'))
);

CREATE TABLE usuarios (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    username            varchar(80) NOT NULL UNIQUE,
    email               varchar(254) NOT NULL UNIQUE,
    nombre              varchar(200) NOT NULL,
    activo              boolean NOT NULL DEFAULT true,
    bloqueado           boolean NOT NULL DEFAULT false,
    password_hash       text NOT NULL,
    creado_en           timestamptz NOT NULL DEFAULT now(),
    actualizado_en      timestamptz
);

CREATE TABLE roles (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo              varchar(60) NOT NULL UNIQUE,
    nombre              varchar(120) NOT NULL,
    tipo_rol            varchar(60),
    activo              boolean NOT NULL DEFAULT true
);

CREATE TABLE permisos (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo              varchar(120) NOT NULL UNIQUE,
    descripcion         varchar(300),
    modulo              varchar(80)
);

CREATE TABLE usuario_roles (
    usuario_id          uuid NOT NULL REFERENCES usuarios(id) ON DELETE RESTRICT,
    rol_id              uuid NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    asignado_en         timestamptz NOT NULL DEFAULT now(),
    asignado_por        uuid REFERENCES usuarios(id),
    PRIMARY KEY (usuario_id, rol_id)
);

CREATE TABLE rol_permisos (
    rol_id              uuid NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    permiso_id          uuid NOT NULL REFERENCES permisos(id) ON DELETE RESTRICT,
    PRIMARY KEY (rol_id, permiso_id)
);

CREATE TABLE faenas (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                  varchar(40) NOT NULL UNIQUE,
    nombre                  varchar(200) NOT NULL,
    organizacion_id         uuid REFERENCES organizaciones(id),
    descripcion             text,
    centro_costos           varchar(80),
    tipo_faena              varchar(100),
    region                  varchar(100),
    comuna                  varchar(100),
    latitud                 numeric(9,6),
    longitud                numeric(9,6),
    responsable_usuario_id  uuid REFERENCES usuarios(id),
    responsable_nombre      varchar(200),
    estado                  varchar(20) NOT NULL DEFAULT 'ACTIVA',
    creado_en               timestamptz NOT NULL DEFAULT now(),
    actualizado_en          timestamptz,
    CONSTRAINT ck_faenas_estado CHECK (estado IN ('ACTIVA','INACTIVA'))
);

CREATE TABLE usuario_faenas (
    usuario_id          uuid NOT NULL REFERENCES usuarios(id) ON DELETE RESTRICT,
    faena_id            uuid NOT NULL REFERENCES faenas(id) ON DELETE RESTRICT,
    asignado_en         timestamptz NOT NULL DEFAULT now(),
    asignado_por        uuid REFERENCES usuarios(id),
    PRIMARY KEY (usuario_id, faena_id)
);

CREATE TABLE ubicaciones_tecnicas (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo              varchar(60) NOT NULL UNIQUE,
    nombre              varchar(200) NOT NULL,
    faena_id            uuid NOT NULL REFERENCES faenas(id),
    ubicacion_padre_id  uuid REFERENCES ubicaciones_tecnicas(id),
    activa              boolean NOT NULL DEFAULT true
);

-- ============================================================
-- 2. ACTIVOS Y JERARQUIA TECNICA
-- ============================================================

CREATE TABLE familias_equipo (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo              varchar(60) NOT NULL UNIQUE,
    nombre              varchar(160) NOT NULL UNIQUE,
    descripcion         text,
    activa              boolean NOT NULL DEFAULT true
);

CREATE TABLE tipos_activo (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo              varchar(60) NOT NULL UNIQUE,
    nombre              varchar(160) NOT NULL,
    activo              boolean NOT NULL DEFAULT true
);

CREATE TABLE activos (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                  varchar(80) NOT NULL UNIQUE,
    nombre                  varchar(200) NOT NULL,
    faena_id                uuid NOT NULL REFERENCES faenas(id),
    tipo_activo_id          uuid REFERENCES tipos_activo(id),
    ubicacion_tecnica_id    uuid REFERENCES ubicaciones_tecnicas(id),
    familia_equipo_id       uuid REFERENCES familias_equipo(id),
    estado_registro         varchar(30) NOT NULL DEFAULT 'ACTIVO',
    marca                   varchar(120),
    modelo                  varchar(120),
    patente                 varchar(30),
    numero_serie            varchar(120),
    propiedad               varchar(30),
    criticidad              varchar(20),
    estado_documental       varchar(30),
    estado_operacional      varchar(50),
    completitud_ficha       smallint NOT NULL DEFAULT 0,
    ficha_validada          boolean NOT NULL DEFAULT false,
    fecha_alta              timestamptz NOT NULL DEFAULT now(),
    actualizado_en          timestamptz,
    CONSTRAINT ck_activos_completitud CHECK (completitud_ficha BETWEEN 0 AND 100)
);

CREATE UNIQUE INDEX uq_activos_patente_no_nula
    ON activos (patente) WHERE patente IS NOT NULL AND patente <> '';

CREATE UNIQUE INDEX uq_activos_serie_no_nula
    ON activos (numero_serie) WHERE numero_serie IS NOT NULL AND numero_serie <> '';

CREATE TABLE definiciones_atributo_activo (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    familia_equipo_id   uuid REFERENCES familias_equipo(id) ON DELETE RESTRICT,
    codigo              varchar(80) NOT NULL,
    nombre              varchar(160) NOT NULL,
    tipo_dato           varchar(20) NOT NULL,
    unidad              varchar(40),
    obligatorio         boolean NOT NULL DEFAULT false,
    activo              boolean NOT NULL DEFAULT true,
    CONSTRAINT uq_atributo_familia UNIQUE (familia_equipo_id, codigo),
    CONSTRAINT ck_atributo_tipo CHECK (tipo_dato IN ('TEXTO','NUMERO','BOOLEANO','FECHA','JSON'))
);

CREATE TABLE valores_atributo_activo (
    activo_id           uuid NOT NULL REFERENCES activos(id) ON DELETE RESTRICT,
    atributo_id         uuid NOT NULL REFERENCES definiciones_atributo_activo(id),
    valor               jsonb NOT NULL,
    actualizado_en      timestamptz NOT NULL DEFAULT now(),
    actualizado_por     uuid REFERENCES usuarios(id),
    PRIMARY KEY (activo_id, atributo_id)
);

CREATE TABLE eventos_estado_activo (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    activo_id           uuid NOT NULL REFERENCES activos(id),
    estado_anterior     varchar(50),
    estado_nuevo        varchar(50) NOT NULL,
    fecha_evento        timestamptz NOT NULL DEFAULT now(),
    motivo              text NOT NULL,
    usuario_id          uuid REFERENCES usuarios(id)
);

CREATE TABLE nodos_tecnicos (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                  varchar(80) NOT NULL UNIQUE,
    nombre                  varchar(200) NOT NULL,
    nivel                   varchar(30) NOT NULL,
    nodo_padre_id           uuid REFERENCES nodos_tecnicos(id),
    nombre_normalizado      varchar(200),
    faena_id                uuid REFERENCES faenas(id),
    ubicacion_tecnica_id    uuid REFERENCES ubicaciones_tecnicas(id),
    obsoleto                boolean NOT NULL DEFAULT false,
    fusionado_en_id         uuid REFERENCES nodos_tecnicos(id),
    creado_en               timestamptz NOT NULL DEFAULT now(),
    actualizado_en          timestamptz,
    CONSTRAINT ck_nodos_nivel CHECK (nivel IN ('SISTEMA','SUBSISTEMA','COMPONENTE','SUBCOMPONENTE'))
);

CREATE TABLE aliases_nodo_tecnico (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    nodo_tecnico_id     uuid NOT NULL REFERENCES nodos_tecnicos(id) ON DELETE RESTRICT,
    alias               varchar(200) NOT NULL,
    UNIQUE (nodo_tecnico_id, alias)
);

CREATE TABLE nodo_familias_equipo (
    nodo_tecnico_id     uuid NOT NULL REFERENCES nodos_tecnicos(id) ON DELETE RESTRICT,
    familia_equipo_id   uuid NOT NULL REFERENCES familias_equipo(id) ON DELETE RESTRICT,
    PRIMARY KEY (nodo_tecnico_id, familia_equipo_id)
);

CREATE TABLE activo_nodos_tecnicos (
    activo_id           uuid NOT NULL REFERENCES activos(id) ON DELETE RESTRICT,
    nodo_tecnico_id     uuid NOT NULL REFERENCES nodos_tecnicos(id) ON DELETE RESTRICT,
    fecha_asignacion    timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (activo_id, nodo_tecnico_id)
);

-- ============================================================
-- 3. ARCHIVOS, DOCUMENTOS Y PLANTILLAS
-- El binario NO se guarda en SQL; solo metadata y URI.
-- ============================================================

CREATE TABLE archivos (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    file_key            text NOT NULL UNIQUE,
    nombre_archivo      varchar(500) NOT NULL,
    content_type        varchar(160),
    modo                varchar(40),
    proposito           varchar(60),
    estado              varchar(30) NOT NULL DEFAULT 'STORED',
    modulo              varchar(80),
    ruta_relativa       text,
    uri                 text,
    tamano_bytes        bigint,
    version             integer NOT NULL DEFAULT 1,
    checksum_sha256     char(64),
    creado_en           timestamptz NOT NULL DEFAULT now(),
    creado_por          uuid REFERENCES usuarios(id),
    metadata            jsonb,
    CONSTRAINT ck_archivos_tamano CHECK (tamano_bytes IS NULL OR tamano_bytes >= 0)
);

CREATE TABLE tipos_documento (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                  varchar(80) NOT NULL UNIQUE,
    nombre                  varchar(200) NOT NULL,
    aplica_a                varchar(30) NOT NULL,
    obligatorio             boolean NOT NULL DEFAULT false,
    critico                 boolean NOT NULL DEFAULT false,
    bloquea_disponibilidad  boolean NOT NULL DEFAULT false,
    plazo_alerta_dias       integer NOT NULL DEFAULT 30,
    requiere_pdf_alerta     boolean NOT NULL DEFAULT false,
    activo                  boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_tipo_documento_aplica CHECK (aplica_a IN ('ACTIVO','FAENA','ORDEN_TRABAJO')),
    CONSTRAINT ck_tipo_documento_plazo CHECK (plazo_alerta_dias >= 0)
);

CREATE TABLE roles_tipo_documento (
    tipo_documento_id   uuid NOT NULL REFERENCES tipos_documento(id) ON DELETE RESTRICT,
    rol_id              uuid NOT NULL REFERENCES roles(id) ON DELETE RESTRICT,
    PRIMARY KEY (tipo_documento_id, rol_id)
);

CREATE TABLE documentos (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tipo_documento_id   uuid NOT NULL REFERENCES tipos_documento(id),
    faena_id            uuid REFERENCES faenas(id),
    activo_id           uuid REFERENCES activos(id),
    orden_trabajo_id    uuid,
    estado              varchar(30) NOT NULL DEFAULT 'VIGENTE',
    creado_en           timestamptz NOT NULL DEFAULT now(),
    creado_por          uuid REFERENCES usuarios(id),
    CONSTRAINT ck_documento_un_sujeto CHECK (
        ((faena_id IS NOT NULL)::integer +
         (activo_id IS NOT NULL)::integer +
         (orden_trabajo_id IS NOT NULL)::integer) = 1
    )
);

CREATE TABLE versiones_documento (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    documento_id                uuid NOT NULL REFERENCES documentos(id) ON DELETE RESTRICT,
    archivo_id                  uuid NOT NULL REFERENCES archivos(id),
    numero_version              integer NOT NULL,
    estado                      varchar(30) NOT NULL DEFAULT 'PENDIENTE',
    fecha_emision               date,
    fecha_vencimiento           date,
    critico_snapshot            boolean NOT NULL DEFAULT false,
    obligatorio_snapshot        boolean NOT NULL DEFAULT false,
    bloquea_disponibilidad_snapshot boolean NOT NULL DEFAULT false,
    cargado_en                  timestamptz NOT NULL DEFAULT now(),
    cargado_por                 uuid REFERENCES usuarios(id),
    validado_por                uuid REFERENCES usuarios(id),
    validado_en                 timestamptz,
    rechazado_por               uuid REFERENCES usuarios(id),
    rechazado_en                timestamptz,
    motivo_rechazo              text,
    reemplaza_version_id        uuid REFERENCES versiones_documento(id),
    anulado_por                 uuid REFERENCES usuarios(id),
    anulado_en                  timestamptz,
    motivo_anulacion            text,
    motivo_cambio               text,
    vencimiento_validado        boolean NOT NULL DEFAULT false,
    UNIQUE (documento_id, numero_version)
);

-- ============================================================
-- 4. INVENTARIO MAESTRO
-- ============================================================

CREATE TABLE unidades_medida (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo              varchar(30) NOT NULL UNIQUE,
    nombre              varchar(100) NOT NULL,
    activo              boolean NOT NULL DEFAULT true
);

CREATE TABLE familias_repuesto (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo              varchar(60) NOT NULL UNIQUE,
    nombre              varchar(160) NOT NULL UNIQUE,
    activa              boolean NOT NULL DEFAULT true
);

CREATE TABLE proveedores (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rut                     varchar(20) NOT NULL UNIQUE,
    nombre                  varchar(200) NOT NULL,
    contacto                varchar(200),
    email                   varchar(254),
    telefono                varchar(60),
    direccion               text,
    lead_time_esperado_dias integer,
    activo                  boolean NOT NULL DEFAULT true,
    observaciones           text
);

CREATE TABLE repuestos (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                      varchar(80) NOT NULL UNIQUE,
    descripcion                 varchar(300) NOT NULL,
    codigo_sap                  varchar(80),
    familia_repuesto_id         uuid REFERENCES familias_repuesto(id),
    unidad_medida_id            uuid REFERENCES unidades_medida(id),
    descripcion_tecnica         text,
    marca_fabricante            varchar(160),
    modelo_referencia           varchar(160),
    critico                     boolean NOT NULL DEFAULT false,
    stock_minimo_default        numeric(18,4) NOT NULL DEFAULT 0,
    stock_maximo_default        numeric(18,4),
    punto_reposicion_default    numeric(18,4),
    lead_time_esperado_dias     integer,
    costo_unitario_promedio     numeric(18,4),
    moneda                      char(3),
    estado                      varchar(30) NOT NULL DEFAULT 'ACTIVO',
    es_no_codificado            boolean NOT NULL DEFAULT false,
    fecha_alta                  timestamptz NOT NULL DEFAULT now(),
    actualizado_en              timestamptz,
    CONSTRAINT ck_repuestos_stock_min CHECK (stock_minimo_default >= 0)
);

CREATE UNIQUE INDEX uq_repuestos_codigo_sap_no_nulo
    ON repuestos (codigo_sap) WHERE codigo_sap IS NOT NULL AND codigo_sap <> '';

CREATE TABLE repuesto_proveedores (
    repuesto_id         uuid NOT NULL REFERENCES repuestos(id) ON DELETE RESTRICT,
    proveedor_id        uuid NOT NULL REFERENCES proveedores(id) ON DELETE RESTRICT,
    codigo_proveedor    varchar(100),
    preferente          boolean NOT NULL DEFAULT false,
    lead_time_dias      integer,
    costo_referencia    numeric(18,4),
    moneda              char(3),
    PRIMARY KEY (repuesto_id, proveedor_id)
);

CREATE TABLE repuesto_familias_equipo (
    repuesto_id         uuid NOT NULL REFERENCES repuestos(id) ON DELETE RESTRICT,
    familia_equipo_id   uuid NOT NULL REFERENCES familias_equipo(id) ON DELETE RESTRICT,
    PRIMARY KEY (repuesto_id, familia_equipo_id)
);

CREATE TABLE repuesto_sustitutos (
    repuesto_id             uuid NOT NULL REFERENCES repuestos(id) ON DELETE RESTRICT,
    repuesto_sustituto_id   uuid NOT NULL REFERENCES repuestos(id),
    prioridad               smallint NOT NULL DEFAULT 1,
    observaciones           text,
    PRIMARY KEY (repuesto_id, repuesto_sustituto_id),
    CONSTRAINT ck_repuesto_no_auto_sustituto CHECK (repuesto_id <> repuesto_sustituto_id)
);

CREATE TABLE bodegas (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                  varchar(60) NOT NULL UNIQUE,
    nombre                  varchar(200) NOT NULL,
    faena_id                uuid REFERENCES faenas(id),
    ubicacion               text,
    tipo_bodega             varchar(60),
    es_central              boolean NOT NULL DEFAULT false,
    activa                  boolean NOT NULL DEFAULT true,
    responsable_usuario_id  uuid REFERENCES usuarios(id),
    responsable_nombre      varchar(200),
    permite_stock_negativo  boolean NOT NULL DEFAULT false
);

CREATE TABLE ubicaciones_bodega (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    bodega_id           uuid NOT NULL REFERENCES bodegas(id) ON DELETE RESTRICT,
    codigo              varchar(80) NOT NULL,
    nombre              varchar(160),
    ubicacion_padre_id  uuid REFERENCES ubicaciones_bodega(id),
    activa              boolean NOT NULL DEFAULT true,
    UNIQUE (bodega_id, codigo)
);

-- ============================================================
-- 5. CHECKLISTS Y MANTENIMIENTO PREVENTIVO
-- ============================================================

CREATE TABLE plantillas_checklist (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo              varchar(80) NOT NULL UNIQUE,
    nombre              varchar(200) NOT NULL,
    tipo                varchar(60),
    activa              boolean NOT NULL DEFAULT true
);

CREATE TABLE items_plantilla_checklist (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    checklist_id        uuid NOT NULL REFERENCES plantillas_checklist(id) ON DELETE RESTRICT,
    orden               integer NOT NULL,
    texto               text NOT NULL,
    tipo_respuesta      varchar(30) NOT NULL DEFAULT 'BOOLEANO',
    obligatorio         boolean NOT NULL DEFAULT false,
    requiere_foto       boolean NOT NULL DEFAULT false,
    requiere_archivo    boolean NOT NULL DEFAULT false,
    requiere_firma      boolean NOT NULL DEFAULT false,
    UNIQUE (checklist_id, orden)
);

CREATE TABLE reglas_asignacion_checklist (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    checklist_id        uuid NOT NULL REFERENCES plantillas_checklist(id) ON DELETE RESTRICT,
    tipo_ot             varchar(60),
    familia_equipo_id   uuid REFERENCES familias_equipo(id),
    activo_id           uuid REFERENCES activos(id),
    codigo_tarea        varchar(80),
    activa              boolean NOT NULL DEFAULT true,
    CONSTRAINT ck_regla_checklist_alcance CHECK (
        tipo_ot IS NOT NULL OR familia_equipo_id IS NOT NULL OR activo_id IS NOT NULL OR codigo_tarea IS NOT NULL
    )
);

CREATE TABLE planes_preventivos (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                  varchar(80) NOT NULL UNIQUE,
    nombre                  varchar(200) NOT NULL,
    frecuencia_descripcion  varchar(200),
    frecuencia_horas        numeric(18,2),
    frecuencia_km           numeric(18,2),
    frecuencia_dias         integer,
    tolerancia_horas        numeric(18,2),
    tolerancia_km           numeric(18,2),
    tolerancia_dias         integer,
    hh_estimadas            numeric(10,2),
    fecha_inicio            date,
    ultima_ejecucion_fecha  date,
    ultima_ejecucion_horas  numeric(18,2),
    ultima_ejecucion_km     numeric(18,2),
    proxima_fecha           date,
    proxima_hora            numeric(18,2),
    proximo_km              numeric(18,2),
    estado                  varchar(30) NOT NULL DEFAULT 'ACTIVO',
    actualizado_en          timestamptz,
    actualizado_por         uuid REFERENCES usuarios(id)
);

CREATE TABLE plan_activos (
    plan_id             uuid NOT NULL REFERENCES planes_preventivos(id) ON DELETE RESTRICT,
    activo_id           uuid NOT NULL REFERENCES activos(id) ON DELETE RESTRICT,
    PRIMARY KEY (plan_id, activo_id)
);

CREATE TABLE plan_familias_equipo (
    plan_id             uuid NOT NULL REFERENCES planes_preventivos(id) ON DELETE RESTRICT,
    familia_equipo_id   uuid NOT NULL REFERENCES familias_equipo(id) ON DELETE RESTRICT,
    PRIMARY KEY (plan_id, familia_equipo_id)
);

CREATE TABLE plan_checklists (
    plan_id             uuid NOT NULL REFERENCES planes_preventivos(id) ON DELETE RESTRICT,
    checklist_id        uuid NOT NULL REFERENCES plantillas_checklist(id),
    obligatorio         boolean NOT NULL DEFAULT true,
    PRIMARY KEY (plan_id, checklist_id)
);

CREATE TABLE plan_repuestos (
    plan_id             uuid NOT NULL REFERENCES planes_preventivos(id) ON DELETE RESTRICT,
    repuesto_id         uuid NOT NULL REFERENCES repuestos(id),
    cantidad_sugerida   numeric(18,4) NOT NULL DEFAULT 1,
    unidad_medida_id    uuid REFERENCES unidades_medida(id),
    PRIMARY KEY (plan_id, repuesto_id)
);

-- ============================================================
-- 6. AVISOS Y ORDENES DE TRABAJO
-- ============================================================

CREATE TABLE avisos_trabajo (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero_aviso                varchar(40) NOT NULL UNIQUE,
    estado                      varchar(30) NOT NULL,
    tipo                        varchar(60) NOT NULL,
    faena_id                    uuid NOT NULL REFERENCES faenas(id),
    activo_id                   uuid REFERENCES activos(id),
    nodo_tecnico_id             uuid REFERENCES nodos_tecnicos(id),
    descripcion                 text NOT NULL,
    prioridad                   varchar(20),
    criticidad                  varchar(20),
    solicitante_id              uuid REFERENCES usuarios(id),
    evidencia_inicial_archivo_id uuid REFERENCES archivos(id),
    fecha_deteccion             timestamptz,
    fecha_creacion              timestamptz NOT NULL DEFAULT now(),
    clasificacion_falla         varchar(80),
    evaluado_por                uuid REFERENCES usuarios(id),
    evaluado_en                 timestamptz,
    aprobado_por                uuid REFERENCES usuarios(id),
    aprobado_en                 timestamptz,
    rechazado_por               uuid REFERENCES usuarios(id),
    rechazado_en                timestamptz,
    motivo_rechazo              text,
    anulado_por                 uuid REFERENCES usuarios(id),
    anulado_en                  timestamptz,
    motivo_anulacion            text,
    observaciones               text
);

CREATE TABLE ordenes_trabajo (
    id                              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero_ot                       varchar(40) NOT NULL UNIQUE,
    aviso_id                        uuid UNIQUE REFERENCES avisos_trabajo(id),
    activo_id                       uuid NOT NULL REFERENCES activos(id),
    faena_id                        uuid NOT NULL REFERENCES faenas(id),
    plan_preventivo_id              uuid REFERENCES planes_preventivos(id),
    nodo_tecnico_id                 uuid REFERENCES nodos_tecnicos(id),
    estado                          varchar(40) NOT NULL,
    tipo_mantenimiento              varchar(60) NOT NULL,
    descripcion                     text NOT NULL,
    fecha_programada                timestamptz,
    prioridad                       varchar(20),
    criticidad                      varchar(20),
    clasificacion_falla             varchar(80),
    es_preventiva_automatica        boolean NOT NULL DEFAULT false,
    requiere_firma                  boolean NOT NULL DEFAULT false,
    fecha_inicio_programada         timestamptz,
    fecha_fin_programada            timestamptz,
    fecha_inicio_real               timestamptz,
    fecha_finalizacion_tecnico      timestamptz,
    finalizado_por                  uuid REFERENCES usuarios(id),
    fecha_cierre_supervisor         timestamptz,
    cerrado_por                     uuid REFERENCES usuarios(id),
    fecha_validacion_planificacion  timestamptz,
    validado_por                    uuid REFERENCES usuarios(id),
    anulado_por                     uuid REFERENCES usuarios(id),
    fecha_anulacion                 timestamptz,
    motivo_anulacion                text,
    creado_por                      uuid REFERENCES usuarios(id),
    creado_en                       timestamptz NOT NULL DEFAULT now(),
    actualizado_por                 uuid REFERENCES usuarios(id),
    actualizado_en                  timestamptz
);

ALTER TABLE documentos
    ADD CONSTRAINT fk_documentos_ot
    FOREIGN KEY (orden_trabajo_id) REFERENCES ordenes_trabajo(id);

CREATE TABLE tareas_ot (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_trabajo_id        uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    codigo_tarea            varchar(80) NOT NULL,
    descripcion             text NOT NULL,
    requiere_evidencia      boolean NOT NULL DEFAULT false,
    requiere_hh             boolean NOT NULL DEFAULT false,
    checklist_obligatorio   boolean NOT NULL DEFAULT false,
    fecha_inicio_programada timestamptz,
    fecha_fin_programada    timestamptz,
    observaciones           text,
    UNIQUE (orden_trabajo_id, codigo_tarea)
);

CREATE TABLE tecnicos_tarea_ot (
    tarea_ot_id         uuid NOT NULL REFERENCES tareas_ot(id) ON DELETE RESTRICT,
    tecnico_usuario_id  uuid NOT NULL REFERENCES usuarios(id),
    asignado_en         timestamptz NOT NULL DEFAULT now(),
    asignado_por        uuid REFERENCES usuarios(id),
    PRIMARY KEY (tarea_ot_id, tecnico_usuario_id)
);

CREATE TABLE horas_hombre (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_trabajo_id        uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    tarea_ot_id             uuid REFERENCES tareas_ot(id) ON DELETE RESTRICT,
    tecnico_usuario_id      uuid NOT NULL REFERENCES usuarios(id),
    fecha_trabajo           date NOT NULL,
    hora_inicio             timestamptz,
    hora_termino            timestamptz,
    horas                   numeric(8,2) NOT NULL,
    descripcion             text,
    comentario              text,
    registrado_por          uuid REFERENCES usuarios(id),
    validado_supervisor     boolean NOT NULL DEFAULT false,
    validado_por            uuid REFERENCES usuarios(id),
    validado_en             timestamptz,
    CONSTRAINT ck_hh_positivas CHECK (horas > 0)
);

CREATE TABLE evidencias_ot (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_trabajo_id        uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    tarea_ot_id             uuid REFERENCES tareas_ot(id) ON DELETE RESTRICT,
    archivo_id              uuid NOT NULL REFERENCES archivos(id),
    nombre                  varchar(300),
    tipo_evidencia          varchar(60),
    cubre_obligatoria       boolean NOT NULL DEFAULT false,
    es_foto                 boolean NOT NULL DEFAULT false,
    es_obligatoria          boolean NOT NULL DEFAULT false,
    observaciones           text,
    offline_id              varchar(120),
    sync_status             varchar(30),
    creado_en               timestamptz NOT NULL DEFAULT now(),
    creado_por              uuid REFERENCES usuarios(id)
);

CREATE TABLE firmas_ot (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_trabajo_id        uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    tarea_ot_id             uuid REFERENCES tareas_ot(id) ON DELETE RESTRICT,
    usuario_id              uuid NOT NULL REFERENCES usuarios(id),
    archivo_id              uuid NOT NULL REFERENCES archivos(id),
    alcance                 varchar(40) NOT NULL,
    hash_firma              char(64),
    firmado_en              timestamptz NOT NULL DEFAULT now(),
    comentario              text
);

CREATE TABLE items_checklist_ot (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_trabajo_id        uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    tarea_ot_id             uuid REFERENCES tareas_ot(id) ON DELETE RESTRICT,
    item_plantilla_id       uuid REFERENCES items_plantilla_checklist(id),
    item_texto_snapshot     text NOT NULL,
    obligatorio             boolean NOT NULL DEFAULT false,
    completado              boolean NOT NULL DEFAULT false,
    completado_en           timestamptz,
    completado_por          uuid REFERENCES usuarios(id),
    tipo_respuesta          varchar(30),
    respuesta_boolean       boolean,
    valor_numerico          numeric(18,4),
    texto_respuesta         text,
    evidencia_ot_id         uuid REFERENCES evidencias_ot(id),
    firma_ot_id             uuid REFERENCES firmas_ot(id),
    requiere_foto           boolean NOT NULL DEFAULT false,
    requiere_archivo        boolean NOT NULL DEFAULT false,
    requiere_firma          boolean NOT NULL DEFAULT false
);

CREATE TABLE repuestos_ot (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_trabajo_id        uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    tarea_ot_id             uuid REFERENCES tareas_ot(id) ON DELETE RESTRICT,
    repuesto_id             uuid NOT NULL REFERENCES repuestos(id),
    bodega_id               uuid REFERENCES bodegas(id),
    cantidad_solicitada     numeric(18,4) NOT NULL,
    cantidad_utilizada      numeric(18,4) NOT NULL DEFAULT 0,
    cantidad_devuelta       numeric(18,4) NOT NULL DEFAULT 0,
    unidad_medida_id        uuid REFERENCES unidades_medida(id),
    estado                  varchar(30),
    observaciones           text,
    CONSTRAINT ck_repuestos_ot_cantidades CHECK (
        cantidad_solicitada >= 0 AND cantidad_utilizada >= 0 AND cantidad_devuelta >= 0
    )
);

CREATE TABLE historial_estado_ot (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_trabajo_id    uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    estado_anterior     varchar(40),
    estado_nuevo        varchar(40) NOT NULL,
    fecha               timestamptz NOT NULL DEFAULT now(),
    usuario_id          uuid REFERENCES usuarios(id),
    motivo              text
);

-- ============================================================
-- 7. EJECUCION PREVENTIVA
-- ============================================================

CREATE TABLE lecturas_medidor (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    activo_id           uuid NOT NULL REFERENCES activos(id),
    horometro           numeric(18,2),
    kilometraje         numeric(18,2),
    fecha_lectura       timestamptz NOT NULL,
    usuario_id          uuid REFERENCES usuarios(id),
    evidencia_archivo_id uuid REFERENCES archivos(id),
    es_correccion       boolean NOT NULL DEFAULT false,
    es_anomala          boolean NOT NULL DEFAULT false,
    mensaje_validacion  text,
    motivo_correccion   text,
    autorizado_por      uuid REFERENCES usuarios(id),
    creado_en           timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE evaluaciones_preventivas (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    plan_id                 uuid NOT NULL REFERENCES planes_preventivos(id),
    activo_id               uuid NOT NULL REFERENCES activos(id),
    faena_id                uuid NOT NULL REFERENCES faenas(id),
    estado                  varchar(30) NOT NULL,
    horas_restantes         numeric(18,2),
    km_restantes            numeric(18,2),
    dias_restantes          integer,
    fecha_vencimiento_estimada date,
    orden_trabajo_id        uuid REFERENCES ordenes_trabajo(id),
    mensaje                 text,
    fecha_evaluacion        timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE historial_preventivo (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    plan_id             uuid NOT NULL REFERENCES planes_preventivos(id),
    activo_id           uuid NOT NULL REFERENCES activos(id),
    estado_anterior     varchar(30),
    estado_nuevo        varchar(30) NOT NULL,
    fecha               timestamptz NOT NULL DEFAULT now(),
    usuario_id          uuid REFERENCES usuarios(id),
    motivo              text,
    orden_trabajo_id    uuid REFERENCES ordenes_trabajo(id)
);

-- ============================================================
-- 8. STOCK, RESERVAS, SOLICITUDES Y TRANSFERENCIAS
-- ============================================================

CREATE TABLE stock_bodega (
    bodega_id           uuid NOT NULL REFERENCES bodegas(id) ON DELETE RESTRICT,
    repuesto_id         uuid NOT NULL REFERENCES repuestos(id) ON DELETE RESTRICT,
    stock_fisico        numeric(18,4) NOT NULL DEFAULT 0,
    stock_reservado     numeric(18,4) NOT NULL DEFAULT 0,
    stock_disponible    numeric(18,4) GENERATED ALWAYS AS (stock_fisico - stock_reservado) STORED,
    stock_minimo        numeric(18,4) NOT NULL DEFAULT 0,
    stock_maximo        numeric(18,4),
    punto_reposicion    numeric(18,4),
    actualizado_en      timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (bodega_id, repuesto_id),
    CONSTRAINT ck_stock_no_negativo CHECK (stock_reservado >= 0)
);

CREATE TABLE solicitudes_repuesto (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero_solicitud    varchar(50) NOT NULL UNIQUE,
    estado              varchar(30) NOT NULL,
    solicitante_id      uuid REFERENCES usuarios(id),
    orden_trabajo_id    uuid REFERENCES ordenes_trabajo(id),
    tarea_ot_id         uuid REFERENCES tareas_ot(id),
    activo_id           uuid REFERENCES activos(id),
    faena_id            uuid REFERENCES faenas(id),
    bodega_id           uuid REFERENCES bodegas(id),
    tipo                varchar(40),
    origen              varchar(60),
    motivo              text,
    solicitado_en       timestamptz NOT NULL DEFAULT now(),
    cerrado_en          timestamptz,
    observaciones       text
);

CREATE TABLE items_solicitud_repuesto (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    solicitud_id                uuid NOT NULL REFERENCES solicitudes_repuesto(id) ON DELETE RESTRICT,
    repuesto_id                 uuid REFERENCES repuestos(id),
    descripcion_no_codificada   text,
    cantidad                    numeric(18,4) NOT NULL,
    unidad_medida_id            uuid REFERENCES unidades_medida(id),
    foto_referencia_archivo_id  uuid REFERENCES archivos(id),
    estado                      varchar(30),
    stock_decision              varchar(40),
    convertido_repuesto_id      uuid REFERENCES repuestos(id),
    CONSTRAINT ck_item_solicitud_repuesto CHECK (
        repuesto_id IS NOT NULL OR descripcion_no_codificada IS NOT NULL
    ),
    CONSTRAINT ck_item_solicitud_cantidad CHECK (cantidad > 0)
);

CREATE TABLE historial_solicitud_repuesto (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    solicitud_id        uuid NOT NULL REFERENCES solicitudes_repuesto(id) ON DELETE RESTRICT,
    estado_anterior     varchar(30),
    estado_nuevo        varchar(30) NOT NULL,
    fecha               timestamptz NOT NULL DEFAULT now(),
    usuario_id          uuid REFERENCES usuarios(id),
    motivo              text
);

CREATE TABLE reservas_stock (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    estado                  varchar(30) NOT NULL,
    fecha                   timestamptz NOT NULL DEFAULT now(),
    repuesto_id             uuid NOT NULL REFERENCES repuestos(id),
    bodega_id               uuid NOT NULL REFERENCES bodegas(id),
    orden_trabajo_id        uuid REFERENCES ordenes_trabajo(id),
    tarea_ot_id             uuid REFERENCES tareas_ot(id),
    item_solicitud_id       uuid REFERENCES items_solicitud_repuesto(id),
    cantidad_reservada      numeric(18,4) NOT NULL,
    cantidad_entregada      numeric(18,4) NOT NULL DEFAULT 0,
    cantidad_liberada       numeric(18,4) NOT NULL DEFAULT 0,
    solicitante_id          uuid REFERENCES usuarios(id),
    motivo                  text,
    usuario_id              uuid REFERENCES usuarios(id),
    CONSTRAINT ck_reserva_cantidades CHECK (
        cantidad_reservada >= 0 AND cantidad_entregada >= 0 AND cantidad_liberada >= 0
    )
);

CREATE TABLE transferencias_stock (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero_transferencia    varchar(50) UNIQUE,
    estado                  varchar(30) NOT NULL,
    bodega_origen_id        uuid NOT NULL REFERENCES bodegas(id),
    bodega_transito_id      uuid REFERENCES bodegas(id),
    bodega_destino_id       uuid NOT NULL REFERENCES bodegas(id),
    fecha_solicitud         timestamptz NOT NULL DEFAULT now(),
    fecha_recepcion         timestamptz,
    motivo                  text,
    solicitado_por          uuid REFERENCES usuarios(id),
    recibido_por            uuid REFERENCES usuarios(id),
    motivo_recepcion        text,
    CONSTRAINT ck_transferencia_bodegas CHECK (bodega_origen_id <> bodega_destino_id)
);

CREATE TABLE items_transferencia_stock (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    transferencia_id   uuid NOT NULL REFERENCES transferencias_stock(id) ON DELETE RESTRICT,
    repuesto_id         uuid NOT NULL REFERENCES repuestos(id),
    cantidad            numeric(18,4) NOT NULL,
    CONSTRAINT ck_transferencia_cantidad CHECK (cantidad > 0)
);

CREATE TABLE movimientos_stock (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    fecha                       timestamptz NOT NULL DEFAULT now(),
    tipo                        varchar(50) NOT NULL,
    bodega_id                   uuid REFERENCES bodegas(id),
    bodega_origen_id            uuid REFERENCES bodegas(id),
    bodega_destino_id           uuid REFERENCES bodegas(id),
    repuesto_id                 uuid NOT NULL REFERENCES repuestos(id),
    cantidad                    numeric(18,4) NOT NULL,
    stock_fisico_anterior       numeric(18,4),
    stock_fisico_nuevo          numeric(18,4),
    stock_reservado_anterior    numeric(18,4),
    stock_reservado_nuevo       numeric(18,4),
    motivo                      text NOT NULL,
    usuario_id                  uuid REFERENCES usuarios(id),
    orden_trabajo_id            uuid REFERENCES ordenes_trabajo(id),
    reserva_id                  uuid REFERENCES reservas_stock(id),
    transferencia_id           uuid REFERENCES transferencias_stock(id),
    item_solicitud_repuesto_id  uuid REFERENCES items_solicitud_repuesto(id),
    permite_negativo_excepcional boolean NOT NULL DEFAULT false,
    CONSTRAINT ck_movimiento_cantidad CHECK (cantidad > 0)
);

-- ============================================================
-- 9. ABASTECIMIENTO Y COMPRAS
-- ============================================================

CREATE TABLE solicitudes_abastecimiento (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero_interno_cmms         varchar(60) NOT NULL UNIQUE,
    numero_externo              varchar(80),
    estado                      varchar(40) NOT NULL,
    proveedor_sugerido_id       uuid REFERENCES proveedores(id),
    faena_id                    uuid REFERENCES faenas(id),
    bodega_id                   uuid REFERENCES bodegas(id),
    orden_trabajo_id            uuid REFERENCES ordenes_trabajo(id),
    activo_id                   uuid REFERENCES activos(id),
    motivo                      text,
    fecha_solicitud_tecnica     timestamptz NOT NULL DEFAULT now(),
    creado_por                  uuid REFERENCES usuarios(id),
    creado_en                   timestamptz NOT NULL DEFAULT now(),
    actualizado_por             uuid REFERENCES usuarios(id),
    actualizado_en              timestamptz,
    observaciones               text
);

CREATE TABLE items_solicitud_abastecimiento (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    solicitud_id            uuid NOT NULL REFERENCES solicitudes_abastecimiento(id) ON DELETE RESTRICT,
    repuesto_id             uuid REFERENCES repuestos(id),
    descripcion             text NOT NULL,
    cantidad                numeric(18,4) NOT NULL,
    unidad_medida_id        uuid REFERENCES unidades_medida(id),
    cantidad_recibida       numeric(18,4) NOT NULL DEFAULT 0,
    cantidad_entregada      numeric(18,4) NOT NULL DEFAULT 0,
    costo_estimado          numeric(18,4),
    costo_real              numeric(18,4),
    moneda                  char(3),
    CONSTRAINT ck_item_abastecimiento_cantidad CHECK (cantidad > 0)
);

CREATE TABLE historial_solicitud_abastecimiento (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    solicitud_id        uuid NOT NULL REFERENCES solicitudes_abastecimiento(id) ON DELETE RESTRICT,
    estado_anterior     varchar(40),
    estado_nuevo        varchar(40) NOT NULL,
    fecha               timestamptz NOT NULL DEFAULT now(),
    usuario_id          uuid REFERENCES usuarios(id),
    motivo              text
);

CREATE TABLE ordenes_compra (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero_oc           varchar(80) NOT NULL UNIQUE,
    proveedor_id        uuid NOT NULL REFERENCES proveedores(id),
    solicitud_id        uuid REFERENCES solicitudes_abastecimiento(id),
    fecha_oc            date NOT NULL,
    fecha_comprometida  date,
    costo_total         numeric(18,4),
    moneda              char(3),
    archivo_oc_id       uuid REFERENCES archivos(id),
    creado_por          uuid REFERENCES usuarios(id),
    motivo              text,
    creado_en           timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE items_orden_compra (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_compra_id         uuid NOT NULL REFERENCES ordenes_compra(id) ON DELETE RESTRICT,
    item_solicitud_id       uuid REFERENCES items_solicitud_abastecimiento(id),
    repuesto_id             uuid REFERENCES repuestos(id),
    descripcion             text NOT NULL,
    cantidad                numeric(18,4) NOT NULL,
    costo_unitario          numeric(18,4),
    moneda                  char(3),
    CONSTRAINT ck_item_oc_cantidad CHECK (cantidad > 0)
);

CREATE TABLE recepciones_abastecimiento (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_compra_id         uuid REFERENCES ordenes_compra(id),
    solicitud_id            uuid REFERENCES solicitudes_abastecimiento(id),
    fecha_recepcion         timestamptz NOT NULL,
    bodega_id               uuid REFERENCES bodegas(id),
    despacho_directo_ot     boolean NOT NULL DEFAULT false,
    orden_trabajo_id        uuid REFERENCES ordenes_trabajo(id),
    activo_id               uuid REFERENCES activos(id),
    faena_id                uuid REFERENCES faenas(id),
    archivo_recepcion_id    uuid REFERENCES archivos(id),
    archivo_entrega_id      uuid REFERENCES archivos(id),
    usuario_id              uuid REFERENCES usuarios(id),
    motivo                  text
);

CREATE TABLE items_recepcion_abastecimiento (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    recepcion_id            uuid NOT NULL REFERENCES recepciones_abastecimiento(id) ON DELETE RESTRICT,
    item_orden_compra_id    uuid REFERENCES items_orden_compra(id),
    repuesto_id             uuid REFERENCES repuestos(id),
    cantidad_recibida       numeric(18,4) NOT NULL DEFAULT 0,
    cantidad_despachada     numeric(18,4) NOT NULL DEFAULT 0,
    costo_real              numeric(18,4),
    movimiento_recepcion_id uuid REFERENCES movimientos_stock(id),
    movimiento_entrega_id   uuid REFERENCES movimientos_stock(id)
);

-- ============================================================
-- 10. DISPONIBILIDAD CONTRACTUAL
-- ============================================================

CREATE TABLE contratos_disponibilidad (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                      varchar(80) NOT NULL UNIQUE,
    nombre                      varchar(200) NOT NULL,
    cliente_id                  uuid REFERENCES organizaciones(id),
    faena_id                    uuid NOT NULL REFERENCES faenas(id),
    horas_comprometidas_dia     numeric(8,2) NOT NULL,
    disponibilidad_objetivo     numeric(6,3) NOT NULL,
    fecha_inicio                date NOT NULL,
    fecha_fin                   date,
    activo                      boolean NOT NULL DEFAULT true,
    actualizado_en              timestamptz,
    actualizado_por             uuid REFERENCES usuarios(id),
    CONSTRAINT ck_contrato_disponibilidad CHECK (
        disponibilidad_objetivo BETWEEN 0 AND 100 AND horas_comprometidas_dia > 0
    )
);

CREATE TABLE reglas_contrato_disponibilidad (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    contrato_id         uuid NOT NULL REFERENCES contratos_disponibilidad(id) ON DELETE RESTRICT,
    clave               varchar(100) NOT NULL,
    valor               jsonb NOT NULL,
    UNIQUE (contrato_id, clave)
);

CREATE TABLE activos_contrato (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    contrato_id         uuid NOT NULL REFERENCES contratos_disponibilidad(id) ON DELETE RESTRICT,
    activo_id           uuid NOT NULL REFERENCES activos(id),
    rol                 varchar(30) NOT NULL,
    fecha_inicio        date NOT NULL,
    fecha_fin           date,
    actualizado_en      timestamptz,
    actualizado_por     uuid REFERENCES usuarios(id),
    UNIQUE (contrato_id, activo_id, fecha_inicio),
    CONSTRAINT ck_activo_contrato_rol CHECK (rol IN ('COMPROMETIDO','BACKUP','ARRIENDO','ASIGNADO'))
);

CREATE TABLE eventos_disponibilidad (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    contrato_id                 uuid NOT NULL REFERENCES contratos_disponibilidad(id),
    activo_id                   uuid NOT NULL REFERENCES activos(id),
    faena_id                    uuid NOT NULL REFERENCES faenas(id),
    causa                       varchar(80) NOT NULL,
    inicio                      timestamptz NOT NULL,
    fin                         timestamptz,
    puede_utilizarse            boolean NOT NULL DEFAULT false,
    atribuible_mantenimiento    boolean NOT NULL DEFAULT true,
    penaliza_disponibilidad     boolean NOT NULL DEFAULT true,
    orden_trabajo_id            uuid REFERENCES ordenes_trabajo(id),
    comentario                  text,
    usuario_id                  uuid REFERENCES usuarios(id),
    creado_en                   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_evento_disponibilidad_fechas CHECK (fin IS NULL OR fin >= inicio)
);

CREATE TABLE snapshots_disponibilidad (
    id                          uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    contrato_id                 uuid NOT NULL REFERENCES contratos_disponibilidad(id),
    faena_id                    uuid NOT NULL REFERENCES faenas(id),
    periodo                     varchar(30) NOT NULL,
    desde                       timestamptz NOT NULL,
    hasta                       timestamptz NOT NULL,
    equipos_comprometidos       integer NOT NULL,
    equipos_cubiertos           integer NOT NULL,
    horas_comprometidas         numeric(18,2) NOT NULL,
    horas_disponibles           numeric(18,2) NOT NULL,
    disponibilidad_cantidad     numeric(8,4),
    disponibilidad_horas        numeric(8,4),
    calculado_en                timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_snapshot_fechas CHECK (hasta > desde)
);

-- ============================================================
-- 11. PROGRAMACION DE TALLER
-- ============================================================

CREATE TABLE talleres (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                  varchar(60) NOT NULL UNIQUE,
    nombre                  varchar(200) NOT NULL,
    faena_id                uuid NOT NULL REFERENCES faenas(id),
    capacidad_diaria_hh     numeric(10,2) NOT NULL,
    capacidad_equipos       integer,
    horario                 varchar(200),
    especialidad            varchar(160),
    activo                  boolean NOT NULL DEFAULT true
);

CREATE TABLE programaciones_ot (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    orden_trabajo_id        uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    taller_id               uuid NOT NULL REFERENCES talleres(id),
    faena_id                uuid NOT NULL REFERENCES faenas(id),
    activo_id               uuid NOT NULL REFERENCES activos(id),
    tecnico_usuario_id      uuid REFERENCES usuarios(id),
    fecha_inicio            timestamptz NOT NULL,
    fecha_fin               timestamptz NOT NULL,
    hh_estimadas            numeric(10,2),
    estado                  varchar(30) NOT NULL,
    prioridad               varchar(20),
    criticidad              varchar(20),
    motivo                  text,
    actualizado_por         uuid REFERENCES usuarios(id),
    actualizado_en          timestamptz,
    CONSTRAINT ck_programacion_fechas CHECK (fecha_fin > fecha_inicio)
);

CREATE TABLE dependencias_ot (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ot_predecesora_id       uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    ot_sucesora_id          uuid NOT NULL REFERENCES ordenes_trabajo(id) ON DELETE RESTRICT,
    tipo                    varchar(20) NOT NULL,
    motivo                  text,
    UNIQUE (ot_predecesora_id, ot_sucesora_id, tipo),
    CONSTRAINT ck_dependencia_ot_distinta CHECK (ot_predecesora_id <> ot_sucesora_id)
);

-- ============================================================
-- 12. ALERTAS Y NOTIFICACIONES
-- programacion_alertas se integra aqui.
-- ============================================================

CREATE TABLE plantillas_pdf (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo              varchar(80) NOT NULL UNIQUE,
    nombre              varchar(200) NOT NULL,
    tipo_evento         varchar(100),
    asunto_plantilla    text,
    html_plantilla      text NOT NULL,
    activa              boolean NOT NULL DEFAULT true,
    actualizado_en      timestamptz
);

CREATE TABLE reglas_alerta (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo                  varchar(100) NOT NULL UNIQUE,
    nombre                  varchar(200) NOT NULL,
    tipo_evento             varchar(100) NOT NULL,
    habilitada              boolean NOT NULL DEFAULT true,
    severidad               varchar(20) NOT NULL,
    repetir_hasta_resolver  boolean NOT NULL DEFAULT false,
    generar_email           boolean NOT NULL DEFAULT false,
    generar_pdf             boolean NOT NULL DEFAULT false,
    plantilla_pdf_id        uuid REFERENCES plantillas_pdf(id),
    faena_id                uuid REFERENCES faenas(id)
);

CREATE TABLE destinatarios_regla_alerta (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    regla_alerta_id     uuid NOT NULL REFERENCES reglas_alerta(id) ON DELETE RESTRICT,
    usuario_id          uuid REFERENCES usuarios(id),
    rol_id              uuid REFERENCES roles(id),
    email               varchar(254),
    CONSTRAINT ck_destinatario_regla_un_tipo CHECK (
        ((usuario_id IS NOT NULL)::integer +
         (rol_id IS NOT NULL)::integer +
         (email IS NOT NULL)::integer) = 1
    )
);

CREATE TABLE alertas (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    regla_alerta_id     uuid REFERENCES reglas_alerta(id),
    titulo              varchar(300) NOT NULL,
    mensaje             text NOT NULL,
    severidad           varchar(20) NOT NULL,
    estado              varchar(30) NOT NULL DEFAULT 'ABIERTA',
    origen              varchar(80),
    causa_clave         varchar(120),
    repeticion_critica  boolean NOT NULL DEFAULT false,
    contador_repeticion integer NOT NULL DEFAULT 0,
    creada_en           timestamptz NOT NULL DEFAULT now(),
    actualizada_en      timestamptz,
    reconocida_en       timestamptz,
    reconocida_por      uuid REFERENCES usuarios(id),
    resuelta_en         timestamptz,
    resuelta_por        uuid REFERENCES usuarios(id),
    motivo_resolucion   text
);

CREATE TABLE vinculos_alerta (
    id                      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    alerta_id               uuid NOT NULL REFERENCES alertas(id) ON DELETE RESTRICT,
    faena_id                uuid REFERENCES faenas(id),
    activo_id               uuid REFERENCES activos(id),
    orden_trabajo_id        uuid REFERENCES ordenes_trabajo(id),
    documento_id            uuid REFERENCES documentos(id),
    bodega_id               uuid REFERENCES bodegas(id),
    repuesto_id             uuid REFERENCES repuestos(id),
    contrato_id             uuid REFERENCES contratos_disponibilidad(id),
    taller_id               uuid REFERENCES talleres(id),
    CONSTRAINT ck_vinculo_alerta_un_objeto CHECK (
        ((faena_id IS NOT NULL)::integer +
         (activo_id IS NOT NULL)::integer +
         (orden_trabajo_id IS NOT NULL)::integer +
         (documento_id IS NOT NULL)::integer +
         (bodega_id IS NOT NULL)::integer +
         (repuesto_id IS NOT NULL)::integer +
         (contrato_id IS NOT NULL)::integer +
         (taller_id IS NOT NULL)::integer) = 1
    )
);

CREATE TABLE notificaciones (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    alerta_id           uuid REFERENCES alertas(id),
    asunto              varchar(500) NOT NULL,
    cuerpo              text NOT NULL,
    estado              varchar(30) NOT NULL,
    proveedor_envio     varchar(80),
    creada_en           timestamptz NOT NULL DEFAULT now(),
    enviada_en          timestamptz,
    error               text
);

CREATE TABLE destinatarios_notificacion (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    notificacion_id     uuid NOT NULL REFERENCES notificaciones(id) ON DELETE RESTRICT,
    email               varchar(254) NOT NULL,
    tipo                varchar(10) NOT NULL DEFAULT 'TO',
    estado_entrega      varchar(30),
    CONSTRAINT ck_destinatario_notificacion_tipo CHECK (tipo IN ('TO','CC','BCC'))
);

CREATE TABLE adjuntos_notificacion (
    notificacion_id     uuid NOT NULL REFERENCES notificaciones(id) ON DELETE RESTRICT,
    archivo_id          uuid NOT NULL REFERENCES archivos(id),
    PRIMARY KEY (notificacion_id, archivo_id)
);

-- ============================================================
-- 13. AUDITORIA
-- EntityName/EntityId se mantienen sin FK deliberadamente para conservar
-- trazabilidad aunque la entidad original haya sido eliminada o archivada.
-- ============================================================

CREATE TABLE auditoria (
    id                  uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ocurrido_en         timestamptz NOT NULL DEFAULT now(),
    usuario_id          uuid REFERENCES usuarios(id),
    accion              varchar(160) NOT NULL,
    modulo              varchar(100),
    entidad_nombre      varchar(120),
    entidad_id          varchar(160),
    faena_id            uuid REFERENCES faenas(id),
    severidad           varchar(20),
    valor_anterior      text,
    valor_nuevo         text,
    ip_address          inet,
    dispositivo         text,
    motivo              text,
    exitoso             boolean NOT NULL DEFAULT true,
    detalle             text,
    correlation_id      varchar(120),
    before_json         jsonb,
    after_json          jsonb
);

-- ============================================================
-- 14. INDICES OPERACIONALES PRINCIPALES
-- ============================================================

CREATE INDEX ix_activos_faena ON activos(faena_id);
CREATE INDEX ix_activos_ubicacion ON activos(ubicacion_tecnica_id);
CREATE INDEX ix_ot_activo_estado ON ordenes_trabajo(activo_id, estado);
CREATE INDEX ix_ot_faena_estado ON ordenes_trabajo(faena_id, estado);
CREATE INDEX ix_tareas_ot ON tareas_ot(orden_trabajo_id);
CREATE INDEX ix_hh_ot_fecha ON horas_hombre(orden_trabajo_id, fecha_trabajo);
CREATE INDEX ix_documentos_activo ON documentos(activo_id);
CREATE INDEX ix_versiones_documento_vencimiento ON versiones_documento(fecha_vencimiento, estado);
CREATE INDEX ix_stock_repuesto ON stock_bodega(repuesto_id);
CREATE INDEX ix_movimientos_stock_fecha ON movimientos_stock(repuesto_id, fecha);
CREATE INDEX ix_alertas_estado_severidad ON alertas(estado, severidad);
CREATE INDEX ix_auditoria_fecha ON auditoria(ocurrido_en);
CREATE INDEX ix_auditoria_entidad ON auditoria(entidad_nombre, entidad_id);
CREATE INDEX ix_eventos_disponibilidad_periodo ON eventos_disponibilidad(activo_id, inicio, fin);
