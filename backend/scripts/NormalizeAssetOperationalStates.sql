-- Idempotent data compatibility script for the definitive operational-state catalog.
-- It preserves legacy UUIDs whenever the canonical row does not yet exist.
BEGIN;

CREATE TEMP TABLE _estado_operacional_mapeo (legado text primary key, canonico text not null) ON COMMIT DROP;
INSERT INTO _estado_operacional_mapeo (legado, canonico) VALUES
  ('OPERATIVO_FAENA', 'OPERATIVO'),
  ('ALERTA_FAENA', 'CON_ALERTA'),
  ('FUERA_SERVICIO_FAENA', 'FUERA_SERVICIO'),
  ('FUERA_SERVICIO_TALLER', 'CORRECTIVO'),
  ('EN_PREPARACION', 'PREPARACION');

-- If both rows exist, retain the canonical UUID and re-point every known FK.
CREATE TEMP TABLE _estado_operacional_conflictos ON COMMIT DROP AS
SELECT legado.id AS legado_id, canonico.id AS canonico_id
FROM estados_operacionales_activo legado
JOIN _estado_operacional_mapeo m ON m.legado = legado.codigo
JOIN estados_operacionales_activo canonico ON canonico.codigo = m.canonico;

UPDATE activos a SET estado_operacional_id = c.canonico_id
FROM _estado_operacional_conflictos c WHERE a.estado_operacional_id = c.legado_id;
UPDATE unidades_operativas u SET estado_operacional_id = c.canonico_id
FROM _estado_operacional_conflictos c WHERE u.estado_operacional_id = c.legado_id;
UPDATE unidades_operativas u SET estado_operacional_base_id = c.canonico_id
FROM _estado_operacional_conflictos c WHERE u.estado_operacional_base_id = c.legado_id;
UPDATE eventos_estado_activo e SET estado_anterior_id = c.canonico_id
FROM _estado_operacional_conflictos c WHERE e.estado_anterior_id = c.legado_id;
UPDATE eventos_estado_activo e SET estado_nuevo_id = c.canonico_id
FROM _estado_operacional_conflictos c WHERE e.estado_nuevo_id = c.legado_id;
DELETE FROM estados_operacionales_activo s USING _estado_operacional_conflictos c WHERE s.id = c.legado_id;

-- Rename remaining legacy rows in place, preserving their UUIDs and FK references.
UPDATE estados_operacionales_activo s SET codigo = m.canonico, updated_at_utc = CURRENT_TIMESTAMP
FROM _estado_operacional_mapeo m WHERE s.codigo = m.legado;

INSERT INTO estados_operacionales_activo (id, codigo, nombre, severidad, activo, created_at_utc)
VALUES
  (gen_random_uuid(), 'OPERATIVO', 'Operativo', 0, true, CURRENT_TIMESTAMP),
  (gen_random_uuid(), 'CON_ALERTA', 'Con alerta', 25, true, CURRENT_TIMESTAMP),
  (gen_random_uuid(), 'PREPARACION', 'Preparación', 50, true, CURRENT_TIMESTAMP),
  (gen_random_uuid(), 'DOCUMENTAL', 'Documental', 60, true, CURRENT_TIMESTAMP),
  (gen_random_uuid(), 'PREVENTIVO', 'Preventivo', 80, true, CURRENT_TIMESTAMP),
  (gen_random_uuid(), 'CORRECTIVO', 'Correctivo', 100, true, CURRENT_TIMESTAMP),
  (gen_random_uuid(), 'FUERA_SERVICIO', 'F/S', 120, true, CURRENT_TIMESTAMP),
  (gen_random_uuid(), 'DADO_DE_BAJA', 'Dado de baja', 200, true, CURRENT_TIMESTAMP)
ON CONFLICT (codigo) DO NOTHING;

UPDATE estados_operacionales_activo s
SET nombre = d.nombre, severidad = d.severidad, activo = true, updated_at_utc = CURRENT_TIMESTAMP
FROM (VALUES
  ('OPERATIVO', 'Operativo', 0), ('CON_ALERTA', 'Con alerta', 25),
  ('PREPARACION', 'Preparación', 50), ('DOCUMENTAL', 'Documental', 60),
  ('PREVENTIVO', 'Preventivo', 80), ('CORRECTIVO', 'Correctivo', 100),
  ('FUERA_SERVICIO', 'F/S', 120), ('DADO_DE_BAJA', 'Dado de baja', 200)
) AS d(codigo, nombre, severidad)
WHERE s.codigo = d.codigo;

COMMIT;
