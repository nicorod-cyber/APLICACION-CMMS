using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Migrations;

[DbContext(typeof(CmmsDbContext))]
[Migration("20260825010000_AssetStateEventCorrelationList")]
public partial class AssetStateEventCorrelationList : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION cmms_require_asset_state_event()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM eventos_estado_activo e
                    WHERE e.activo_id = NEW.id
                      AND e.estado_anterior_id = OLD.estado_operacional_id
                      AND e.estado_nuevo_id = NEW.estado_operacional_id
                      AND e.id::text = ANY(
                          COALESCE(
                              string_to_array(NULLIF(current_setting('cmms.asset_state_event_id', true), ''), ','),
                              ARRAY[]::text[]
                          )
                      )
                ) THEN
                    RAISE EXCEPTION 'El estado operacional solo puede cambiar mediante un evento de estado'
                        USING ERRCODE = 'check_violation';
                END IF;

                RETURN NULL;
            END
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE OR REPLACE FUNCTION cmms_require_asset_state_event()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM eventos_estado_activo e
                    WHERE e.activo_id = NEW.id
                      AND e.estado_anterior_id = OLD.estado_operacional_id
                      AND e.estado_nuevo_id = NEW.estado_operacional_id
                      AND e.id::text = current_setting('cmms.asset_state_event_id', true)
                ) THEN
                    RAISE EXCEPTION 'El estado operacional solo puede cambiar mediante un evento de estado'
                        USING ERRCODE = 'check_violation';
                END IF;

                RETURN NULL;
            END
            $$;
            """);
    }
}
