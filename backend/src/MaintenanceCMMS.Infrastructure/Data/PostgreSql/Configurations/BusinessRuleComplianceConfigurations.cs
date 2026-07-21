using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Configurations;

public sealed class AssetTransferConfiguration : IEntityTypeConfiguration<AssetTransferEntity>
{
    public void Configure(EntityTypeBuilder<AssetTransferEntity> builder)
    {
        builder.ToTable("traslados_activo");
        builder.ConfigureBase();
        builder.Property(x => x.AssetId).HasColumnName("activo_id");
        builder.Property(x => x.OriginFaenaId).HasColumnName("faena_origen_id");
        builder.Property(x => x.DestinationFaenaId).HasColumnName("faena_destino_id");
        builder.Property(x => x.OperationalUnitId).HasColumnName("unidad_operativa_id");
        builder.Property(x => x.EffectiveAtUtc).HasColumnName("fecha_efectiva_utc").HasColumnType("timestamptz");
        builder.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(500).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.RegisteredAtUtc).HasColumnName("fecha_registro_utc").HasColumnType("timestamptz");
        builder.Property(x => x.Observations).HasColumnName("observaciones").HasMaxLength(1000);
        builder.HasIndex(x => new { x.AssetId, x.EffectiveAtUtc }).IsUnique();
        builder.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginFaena).WithMany().HasForeignKey(x => x.OriginFaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DestinationFaena).WithMany().HasForeignKey(x => x.DestinationFaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OperationalUnit).WithMany().HasForeignKey(x => x.OperationalUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("ck_traslados_activo_origen_destino", "faena_origen_id IS DISTINCT FROM faena_destino_id"));
    }
}

public sealed class AssetLocationPeriodConfiguration : IEntityTypeConfiguration<AssetLocationPeriodEntity>
{
    public void Configure(EntityTypeBuilder<AssetLocationPeriodEntity> builder)
    {
        builder.ToTable("vigencias_ubicacion_activo");
        builder.ConfigureBase();
        builder.Property(x => x.AssetId).HasColumnName("activo_id");
        builder.Property(x => x.FaenaId).HasColumnName("faena_id");
        builder.Property(x => x.ValidFromUtc).HasColumnName("vigencia_desde_utc").HasColumnType("timestamptz");
        builder.Property(x => x.ValidToUtc).HasColumnName("vigencia_hasta_utc").HasColumnType("timestamptz");
        builder.Property(x => x.TransferId).HasColumnName("traslado_id");
        builder.HasIndex(x => x.AssetId).IsUnique().HasFilter("vigencia_hasta_utc IS NULL");
        builder.HasIndex(x => new { x.AssetId, x.ValidFromUtc }).IsUnique();
        builder.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Faena).WithMany().HasForeignKey(x => x.FaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Transfer).WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("ck_vigencias_ubicacion_activo_fechas", "vigencia_hasta_utc IS NULL OR vigencia_hasta_utc > vigencia_desde_utc"));
    }
}

public sealed class AssetIdentifierAliasConfiguration : IEntityTypeConfiguration<AssetIdentifierAliasEntity>
{
    public void Configure(EntityTypeBuilder<AssetIdentifierAliasEntity> builder)
    {
        builder.ToTable("alias_identificador_activo");
        builder.ConfigureBase();
        builder.Property(x => x.AssetId).HasColumnName("activo_id");
        builder.Property(x => x.IdentifierType).HasColumnName("tipo_identificador").HasMaxLength(80).IsRequired();
        builder.Property(x => x.ScopeKey).HasColumnName("ambito").HasMaxLength(240).IsRequired();
        builder.Property(x => x.Value).HasColumnName("valor").HasMaxLength(500).IsRequired();
        builder.Property(x => x.NormalizedValue).HasColumnName("valor_normalizado").HasMaxLength(500).IsRequired();
        builder.Property(x => x.ValidFromUtc).HasColumnName("vigencia_desde_utc").HasColumnType("timestamptz");
        builder.Property(x => x.ValidToUtc).HasColumnName("vigencia_hasta_utc").HasColumnType("timestamptz");
        builder.Property(x => x.ReplacedByAliasId).HasColumnName("reemplazado_por_alias_id");
        builder.HasIndex(x => new { x.ScopeKey, x.NormalizedValue }).IsUnique().HasFilter("vigencia_hasta_utc IS NULL");
        builder.HasIndex(x => new { x.AssetId, x.IdentifierType, x.ValidFromUtc });
        builder.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReplacedByAlias).WithMany().HasForeignKey(x => x.ReplacedByAliasId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("ck_alias_identificador_vigencias", "vigencia_hasta_utc IS NULL OR vigencia_hasta_utc > vigencia_desde_utc"));
    }
}

public sealed class DocumentRequirementMatrixConfiguration : IEntityTypeConfiguration<DocumentRequirementMatrixEntity>
{
    public void Configure(EntityTypeBuilder<DocumentRequirementMatrixEntity> builder)
    {
        builder.ToTable("matrices_requisitos_documentales");
        builder.ConfigureBase();
        builder.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(x => x.VersionNumber).HasColumnName("numero_version");
        builder.Property(x => x.ValidFrom).HasColumnName("vigencia_desde");
        builder.Property(x => x.ValidTo).HasColumnName("vigencia_hasta");
        builder.Property(x => x.Status).HasColumnName("estado").HasMaxLength(40).IsRequired();
        builder.Property(x => x.AssetTypeId).HasColumnName("tipo_activo_id");
        builder.Property(x => x.EquipmentFamilyId).HasColumnName("familia_equipo_id");
        builder.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(x => x.ChangeReason).HasColumnName("motivo_cambio").HasMaxLength(500);
        builder.HasIndex(x => new { x.Code, x.VersionNumber }).IsUnique();
        builder.HasIndex(x => new { x.AssetTypeId, x.EquipmentFamilyId, x.ValidFrom });
        builder.HasOne(x => x.AssetType).WithMany().HasForeignKey(x => x.AssetTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.EquipmentFamily).WithMany().HasForeignKey(x => x.EquipmentFamilyId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_matrices_requisitos_version", "numero_version > 0");
            table.HasCheckConstraint("ck_matrices_requisitos_vigencias", "vigencia_hasta IS NULL OR vigencia_hasta >= vigencia_desde");
            table.HasCheckConstraint("ck_matrices_requisitos_estado", "estado IN ('BORRADOR','VIGENTE','REEMPLAZADA','ANULADA')");
        });
    }
}

public sealed class DocumentRequirementMatrixItemConfiguration : IEntityTypeConfiguration<DocumentRequirementMatrixItemEntity>
{
    public void Configure(EntityTypeBuilder<DocumentRequirementMatrixItemEntity> builder)
    {
        builder.ToTable("detalles_matriz_requisitos_documentales");
        builder.ConfigureBase();
        builder.Property(x => x.MatrixId).HasColumnName("matriz_id");
        builder.Property(x => x.DocumentTypeId).HasColumnName("tipo_documental_id");
        builder.Property(x => x.IsMandatory).HasColumnName("obligatorio");
        builder.Property(x => x.IsCritical).HasColumnName("critico");
        builder.Property(x => x.BlocksAvailability).HasColumnName("bloquea_disponibilidad");
        builder.Property(x => x.RequiresExpirationDate).HasColumnName("requiere_fecha_vencimiento");
        builder.Property(x => x.AlertDays).HasColumnName("dias_anticipacion");
        builder.HasIndex(x => new { x.MatrixId, x.DocumentTypeId }).IsUnique();
        builder.HasOne(x => x.Matrix).WithMany(x => x.Items).HasForeignKey(x => x.MatrixId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DocumentType).WithMany().HasForeignKey(x => x.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("ck_detalles_matriz_dias_anticipacion", "dias_anticipacion >= 0"));
    }
}

public sealed class DocumentaryWorkOrderRequirementConfiguration : IEntityTypeConfiguration<DocumentaryWorkOrderRequirementEntity>
{
    public void Configure(EntityTypeBuilder<DocumentaryWorkOrderRequirementEntity> builder)
    {
        builder.ToTable("detalles_ot_documental");
        builder.ConfigureBase();
        builder.Property(x => x.WorkOrderId).HasColumnName("orden_trabajo_id");
        builder.Property(x => x.AssetId).HasColumnName("activo_id");
        builder.Property(x => x.MatrixVersionId).HasColumnName("matriz_version_id");
        builder.Property(x => x.MatrixItemId).HasColumnName("matriz_detalle_id");
        builder.Property(x => x.OriginDocumentId).HasColumnName("documento_origen_id");
        builder.Property(x => x.OriginDocumentVersionId).HasColumnName("version_documento_origen_id");
        builder.Property(x => x.CycleKey).HasColumnName("clave_ciclo").HasMaxLength(240).IsRequired();
        builder.Property(x => x.Status).HasColumnName("estado").HasMaxLength(40).IsRequired();
        builder.Property(x => x.IsApplicable).HasColumnName("aplicable");
        builder.Property(x => x.Observation).HasColumnName("observacion").HasMaxLength(1000);
        builder.Property(x => x.CompletedAtUtc).HasColumnName("completado_utc").HasColumnType("timestamptz");
        builder.HasIndex(x => new { x.AssetId, x.MatrixItemId, x.CycleKey }).IsUnique();
        builder.HasIndex(x => x.WorkOrderId);
        builder.HasOne(x => x.WorkOrder).WithMany(x => x.DocumentaryRequirements).HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MatrixVersion).WithMany().HasForeignKey(x => x.MatrixVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MatrixItem).WithMany().HasForeignKey(x => x.MatrixItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginDocument).WithMany().HasForeignKey(x => x.OriginDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.OriginDocumentVersion).WithMany().HasForeignKey(x => x.OriginDocumentVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("ck_detalles_ot_documental_estado", "estado IN ('PENDIENTE','CARGADO','PENDIENTE_CARGA','PENDIENTE_VALIDACION','VALIDADO','VIGENTE','POR_VENCER','RECHAZADO','VENCIDO','REEMPLAZADO','ANULADO','NO_APLICA')"));
    }
}
