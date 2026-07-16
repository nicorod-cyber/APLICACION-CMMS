using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Configurations;

public sealed class AssetAttributeDefinitionConfiguration : IEntityTypeConfiguration<AssetAttributeDefinitionEntity>
{
    public void Configure(EntityTypeBuilder<AssetAttributeDefinitionEntity> b)
    {
        b.ToTable("definiciones_atributo_activo"); b.ConfigureBase();
        b.Property(x=>x.AssetTypeId).HasColumnName("tipo_activo_id"); b.Property(x=>x.EquipmentFamilyId).HasColumnName("familia_equipo_id"); b.Property(x=>x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired(); b.Property(x=>x.Name).HasColumnName("nombre").HasMaxLength(160).IsRequired(); b.Property(x=>x.Description).HasColumnName("descripcion").HasMaxLength(1000); b.Property(x=>x.DataType).HasColumnName("tipo_dato").HasMaxLength(30).IsRequired(); b.Property(x=>x.Unit).HasColumnName("unidad").HasMaxLength(40); b.Property(x=>x.IsRequired).HasColumnName("obligatorio"); b.Property(x=>x.IsIdentifier).HasColumnName("es_identificador"); b.Property(x=>x.IsUnique).HasColumnName("es_unico"); b.Property(x=>x.IsSearchable).HasColumnName("permite_busqueda"); b.Property(x=>x.IsFilterable).HasColumnName("permite_filtro"); b.Property(x=>x.ShowInList).HasColumnName("mostrar_en_listado"); b.Property(x=>x.MinimumValue).HasColumnName("valor_minimo").HasPrecision(18,4); b.Property(x=>x.MaximumValue).HasColumnName("valor_maximo").HasPrecision(18,4); b.Property(x=>x.ValidationPattern).HasColumnName("patron_validacion").HasMaxLength(500); b.Property(x=>x.OptionsJson).HasColumnName("opciones_json").HasColumnType("jsonb"); b.Property(x=>x.DisplayGroup).HasColumnName("grupo_visualizacion").HasMaxLength(120); b.Property(x=>x.SortOrder).HasColumnName("orden_visualizacion"); b.Property(x=>x.IsActive).HasColumnName("activo");
        b.HasIndex(x=>new{x.AssetTypeId,x.Code}).IsUnique().HasFilter("familia_equipo_id IS NULL"); b.HasIndex(x=>new{x.AssetTypeId,x.EquipmentFamilyId,x.Code}).IsUnique().HasFilter("familia_equipo_id IS NOT NULL"); b.HasOne(x=>x.AssetType).WithMany().HasForeignKey(x=>x.AssetTypeId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.EquipmentFamily).WithMany().HasForeignKey(x=>x.EquipmentFamilyId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => { t.HasCheckConstraint("ck_definiciones_atributo_tipo", "tipo_dato IN ('TEXTO','NUMERO','ENTERO','BOOLEANO','FECHA','OPCION')"); t.HasCheckConstraint("ck_definiciones_atributo_rango", "valor_minimo IS NULL OR valor_maximo IS NULL OR valor_minimo <= valor_maximo"); });
    }
}

public sealed class AssetAttributeValueConfiguration : IEntityTypeConfiguration<AssetAttributeValueEntity>
{
    public void Configure(EntityTypeBuilder<AssetAttributeValueEntity> b)
    {
        b.ToTable("valores_atributo_activo"); b.ConfigureBase(); b.Property(x=>x.AssetId).HasColumnName("activo_id"); b.Property(x=>x.AttributeDefinitionId).HasColumnName("definicion_atributo_id"); b.Property(x=>x.TextValue).HasColumnName("valor_texto").HasMaxLength(2000); b.Property(x=>x.NumericValue).HasColumnName("valor_numerico").HasPrecision(18,4); b.Property(x=>x.BooleanValue).HasColumnName("valor_booleano"); b.Property(x=>x.DateValue).HasColumnName("valor_fecha"); b.Property(x=>x.Observations).HasColumnName("observaciones").HasMaxLength(1000); b.HasIndex(x=>new{x.AssetId,x.AttributeDefinitionId}).IsUnique(); b.HasOne(x=>x.Asset).WithMany().HasForeignKey(x=>x.AssetId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.AttributeDefinition).WithMany().HasForeignKey(x=>x.AttributeDefinitionId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t=>t.HasCheckConstraint("ck_valores_atributo_un_valor", "(CASE WHEN valor_texto IS NULL THEN 0 ELSE 1 END + CASE WHEN valor_numerico IS NULL THEN 0 ELSE 1 END + CASE WHEN valor_booleano IS NULL THEN 0 ELSE 1 END + CASE WHEN valor_fecha IS NULL THEN 0 ELSE 1 END) <= 1"));
    }
}

public sealed class AssetReadingConfiguration : IEntityTypeConfiguration<AssetReadingEntity>
{
    public void Configure(EntityTypeBuilder<AssetReadingEntity> b)
    {
        b.ToTable("lecturas_activo"); b.ConfigureBase(); b.Property(x=>x.AssetId).HasColumnName("activo_id"); b.Property(x=>x.ReadAtUtc).HasColumnName("fecha_lectura_utc").HasColumnType("timestamptz"); b.Property(x=>x.Value).HasColumnName("valor").HasPrecision(18,2); b.Property(x=>x.Source).HasColumnName("origen").HasMaxLength(40); b.Property(x=>x.WorkOrderId).HasColumnName("orden_trabajo_id"); b.Property(x=>x.RegisteredByUserId).HasColumnName("registrado_por_usuario_id").HasMaxLength(120); b.Property(x=>x.EvidenceReference).HasColumnName("evidencia_referencia").HasMaxLength(1000); b.Property(x=>x.Observations).HasColumnName("observaciones").HasMaxLength(1000); b.Property(x=>x.IsCorrection).HasColumnName("es_correccion"); b.Property(x=>x.CorrectedReadingId).HasColumnName("lectura_corregida_id"); b.Property(x=>x.CorrectionReason).HasColumnName("motivo_correccion").HasMaxLength(1000); b.Property(x=>x.AuthorizedByUserId).HasColumnName("autorizado_por_usuario_id").HasMaxLength(120); b.Property(x=>x.IsAnomalous).HasColumnName("es_anomala"); b.Property(x=>x.ValidationMessage).HasColumnName("mensaje_validacion").HasMaxLength(1000); b.HasIndex(x=>new{x.AssetId,x.ReadAtUtc}); b.HasOne(x=>x.Asset).WithMany().HasForeignKey(x=>x.AssetId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.WorkOrder).WithMany().HasForeignKey(x=>x.WorkOrderId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.CorrectedReading).WithMany().HasForeignKey(x=>x.CorrectedReadingId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_lecturas_activo_valor", "valor >= 0"); t.HasCheckConstraint("ck_lecturas_activo_origen", "origen IN ('MANUAL','ORDEN_TRABAJO','IMPORTACION','SAP','TELEMETRIA')"); });
    }
}

public sealed class AssetDocumentRequirementConfiguration : IEntityTypeConfiguration<AssetDocumentRequirementEntity>
{
    public void Configure(EntityTypeBuilder<AssetDocumentRequirementEntity> b)
    {
        b.ToTable("requisitos_documentales_tipo_activo"); b.ConfigureBase(); b.Property(x=>x.AssetTypeId).HasColumnName("tipo_activo_id"); b.Property(x=>x.EquipmentFamilyId).HasColumnName("familia_equipo_id"); b.Property(x=>x.DocumentTypeId).HasColumnName("tipo_documental_id"); b.Property(x=>x.IsMandatory).HasColumnName("obligatorio"); b.Property(x=>x.IsCritical).HasColumnName("critico"); b.Property(x=>x.BlocksAvailability).HasColumnName("bloquea_disponibilidad"); b.Property(x=>x.RequiresExpirationDate).HasColumnName("requiere_fecha_vencimiento"); b.Property(x=>x.AlertDays).HasColumnName("dias_alerta"); b.Property(x=>x.IsActive).HasColumnName("activo"); b.HasIndex(x=>new{x.AssetTypeId,x.EquipmentFamilyId,x.DocumentTypeId}).IsUnique(); b.HasOne(x=>x.AssetType).WithMany().HasForeignKey(x=>x.AssetTypeId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.EquipmentFamily).WithMany().HasForeignKey(x=>x.EquipmentFamilyId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.DocumentType).WithMany().HasForeignKey(x=>x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t=>t.HasCheckConstraint("ck_requisitos_documentales_dias_alerta", "dias_alerta IS NULL OR dias_alerta >= 0"));
    }
}

public sealed class OperationalUnitTypeConfiguration : IEntityTypeConfiguration<OperationalUnitTypeEntity>
{
    public void Configure(EntityTypeBuilder<OperationalUnitTypeEntity> b) { b.ToTable("tipos_unidad_operativa"); b.ConfigureBase(); b.Property(x=>x.Code).HasColumnName("codigo").HasMaxLength(60).IsRequired(); b.Property(x=>x.Name).HasColumnName("nombre").HasMaxLength(160).IsRequired(); b.Property(x=>x.Description).HasColumnName("descripcion").HasMaxLength(1000); b.Property(x=>x.ParticipatesInAvailability).HasColumnName("participa_en_disponibilidad"); b.Property(x=>x.IsActive).HasColumnName("activo"); b.HasIndex(x=>x.Code).IsUnique(); }
}
public sealed class OperationalUnitConfiguration : IEntityTypeConfiguration<OperationalUnitEntity>
{
    public void Configure(EntityTypeBuilder<OperationalUnitEntity> b) { b.ToTable("unidades_operativas"); b.ConfigureBase(); b.Property(x=>x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired(); b.Property(x=>x.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired(); b.Property(x=>x.OperationalUnitTypeId).HasColumnName("tipo_unidad_operativa_id"); b.Property(x=>x.FaenaId).HasColumnName("faena_id"); b.Property(x=>x.OperationalStateId).HasColumnName("estado_operacional_id"); b.Property(x=>x.Criticality).HasColumnName("criticidad").HasMaxLength(40); b.Property(x=>x.CommissioningDate).HasColumnName("fecha_puesta_servicio"); b.Property(x=>x.DecommissioningDate).HasColumnName("fecha_baja"); b.Property(x=>x.Observations).HasColumnName("observaciones").HasMaxLength(2000); b.HasIndex(x=>x.Code).IsUnique(); b.HasOne(x=>x.OperationalUnitType).WithMany().HasForeignKey(x=>x.OperationalUnitTypeId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.Faena).WithMany().HasForeignKey(x=>x.FaenaId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.OperationalState).WithMany().HasForeignKey(x=>x.OperationalStateId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t=>t.HasCheckConstraint("ck_unidades_operativas_fecha_baja", "fecha_baja IS NULL OR fecha_puesta_servicio IS NULL OR fecha_baja >= fecha_puesta_servicio")); }
}
public sealed class OperationalUnitComponentRoleConfiguration : IEntityTypeConfiguration<OperationalUnitComponentRoleEntity>
{
    public void Configure(EntityTypeBuilder<OperationalUnitComponentRoleEntity> b) { b.ToTable("roles_componente_unidad"); b.ConfigureBase(); b.Property(x=>x.Code).HasColumnName("codigo").HasMaxLength(60).IsRequired(); b.Property(x=>x.Name).HasColumnName("nombre").HasMaxLength(160).IsRequired(); b.Property(x=>x.Description).HasColumnName("descripcion").HasMaxLength(500); b.Property(x=>x.IsActive).HasColumnName("activo"); b.HasIndex(x=>x.Code).IsUnique(); }
}
public sealed class OperationalUnitCompositionRuleConfiguration : IEntityTypeConfiguration<OperationalUnitCompositionRuleEntity>
{
    public void Configure(EntityTypeBuilder<OperationalUnitCompositionRuleEntity> b) { b.ToTable("reglas_composicion_unidad"); b.ConfigureBase(); b.Property(x=>x.OperationalUnitTypeId).HasColumnName("tipo_unidad_operativa_id"); b.Property(x=>x.ComponentRoleId).HasColumnName("rol_componente_id"); b.Property(x=>x.MinimumQuantity).HasColumnName("cantidad_minima"); b.Property(x=>x.MaximumQuantity).HasColumnName("cantidad_maxima"); b.Property(x=>x.IsMandatory).HasColumnName("obligatorio"); b.Property(x=>x.IsActive).HasColumnName("activo"); b.HasIndex(x=>new{x.OperationalUnitTypeId,x.ComponentRoleId}).IsUnique(); b.HasOne(x=>x.OperationalUnitType).WithMany().HasForeignKey(x=>x.OperationalUnitTypeId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.ComponentRole).WithMany().HasForeignKey(x=>x.ComponentRoleId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t=> { t.HasCheckConstraint("ck_reglas_composicion_cantidades", "cantidad_minima >= 0 AND cantidad_maxima >= cantidad_minima"); t.HasCheckConstraint("ck_reglas_composicion_obligatorio", "(obligatorio AND cantidad_minima > 0) OR (NOT obligatorio AND cantidad_minima = 0)"); }); }
}
public sealed class OperationalUnitCompositionRuleAllowedAssetConfiguration : IEntityTypeConfiguration<OperationalUnitCompositionRuleAllowedAssetEntity>
{
    public void Configure(EntityTypeBuilder<OperationalUnitCompositionRuleAllowedAssetEntity> b)
    {
        b.ToTable("reglas_composicion_unidad_activos_permitidos"); b.ConfigureBase(); b.Property(x => x.OperationalUnitCompositionRuleId).HasColumnName("regla_composicion_id"); b.Property(x => x.AssetTypeId).HasColumnName("tipo_activo_id"); b.Property(x => x.EquipmentFamilyId).HasColumnName("familia_equipo_id");
        b.HasIndex(x => new { x.OperationalUnitCompositionRuleId, x.AssetTypeId, x.EquipmentFamilyId }).IsUnique(); b.HasOne(x => x.OperationalUnitCompositionRule).WithMany(x => x.AllowedAssets).HasForeignKey(x => x.OperationalUnitCompositionRuleId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.AssetType).WithMany().HasForeignKey(x => x.AssetTypeId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.EquipmentFamily).WithMany().HasForeignKey(x => x.EquipmentFamilyId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_reglas_composicion_permitidos_objetivo", "tipo_activo_id IS NOT NULL OR familia_equipo_id IS NOT NULL"));
    }
}
public sealed class OperationalUnitComponentConfiguration : IEntityTypeConfiguration<OperationalUnitComponentEntity>
{
    public void Configure(EntityTypeBuilder<OperationalUnitComponentEntity> b) { b.ToTable("componentes_unidad_operativa"); b.ConfigureBase(); b.Property(x=>x.OperationalUnitId).HasColumnName("unidad_operativa_id"); b.Property(x=>x.AssetId).HasColumnName("activo_id"); b.Property(x=>x.ComponentRoleId).HasColumnName("rol_componente_id"); b.Property(x=>x.InstalledAtUtc).HasColumnName("fecha_montaje_utc").HasColumnType("timestamptz"); b.Property(x=>x.RemovedAtUtc).HasColumnName("fecha_desmontaje_utc").HasColumnType("timestamptz"); b.Property(x=>x.InstallationWorkOrderId).HasColumnName("orden_trabajo_montaje_id"); b.Property(x=>x.RemovalWorkOrderId).HasColumnName("orden_trabajo_desmontaje_id"); b.Property(x=>x.Observations).HasColumnName("observaciones").HasMaxLength(1000); b.HasIndex(x=>new{x.AssetId,x.RemovedAtUtc}).IsUnique().HasFilter("fecha_desmontaje_utc IS NULL"); b.HasOne(x=>x.OperationalUnit).WithMany().HasForeignKey(x=>x.OperationalUnitId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.Asset).WithMany().HasForeignKey(x=>x.AssetId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.ComponentRole).WithMany().HasForeignKey(x=>x.ComponentRoleId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.InstallationWorkOrder).WithMany().HasForeignKey(x=>x.InstallationWorkOrderId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.RemovalWorkOrder).WithMany().HasForeignKey(x=>x.RemovalWorkOrderId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t=>t.HasCheckConstraint("ck_componentes_unidad_fechas", "fecha_desmontaje_utc IS NULL OR fecha_desmontaje_utc >= fecha_montaje_utc")); }
}



