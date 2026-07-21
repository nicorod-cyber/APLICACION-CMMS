using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Configurations;

internal static class PostgreSqlConfigurationHelpers
{
    public static void ConfigureBase<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : PostgreSqlEntity
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).HasColumnName("id");
        builder.Property(entity => entity.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").IsRequired();
        builder.Property(entity => entity.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.Version).HasColumnName("xmin").IsRowVersion();
    }
}

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUserEntity>
{
    public void Configure(EntityTypeBuilder<AppUserEntity> builder)
    {
        builder.ToTable("usuarios");
        builder.ConfigureBase();
        builder.Property(entity => entity.Username).HasColumnName("username").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Email).HasColumnName("email").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.DisplayName).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.IsActive).HasColumnName("activo").IsRequired();
        builder.Property(entity => entity.IsLocked).HasColumnName("bloqueado").IsRequired();
        builder.Property(entity => entity.PasswordHash).HasColumnName("password_hash").HasMaxLength(500).IsRequired();
        builder.HasIndex(entity => entity.Username).IsUnique();
        builder.HasIndex(entity => entity.Email).IsUnique();
    }
}

public sealed class RoleConfiguration : IEntityTypeConfiguration<RoleEntity>
{
    public void Configure(EntityTypeBuilder<RoleEntity> builder)
    {
        builder.ToTable("roles");
        builder.ConfigureBase();
        builder.Property(entity => entity.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Type).HasColumnName("tipo_rol").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.IsActive).HasColumnName("activo").IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique();
    }
}

public sealed class PermissionConfiguration : IEntityTypeConfiguration<PermissionEntity>
{
    public void Configure(EntityTypeBuilder<PermissionEntity> builder)
    {
        builder.ToTable("permisos");
        builder.ConfigureBase();
        builder.Property(entity => entity.Code).HasColumnName("codigo").HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.IsActive).HasColumnName("activo").IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique();
    }
}

public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRoleEntity>
{
    public void Configure(EntityTypeBuilder<UserRoleEntity> builder)
    {
        builder.ToTable("usuario_roles");
        builder.ConfigureBase();
        builder.Property(entity => entity.UserId).HasColumnName("usuario_id");
        builder.Property(entity => entity.RoleId).HasColumnName("rol_id");
        builder.Property(entity => entity.IsActive).HasColumnName("vigente");
        builder.Property(entity => entity.AssignedByUserId).HasColumnName("asignado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.AssignedAtUtc).HasColumnName("asignado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.UnassignedByUserId).HasColumnName("desasignado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.UnassignedAtUtc).HasColumnName("desasignado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.UnassignedReason).HasColumnName("motivo_desasignacion").HasMaxLength(500);
        builder.HasOne(entity => entity.User).WithMany(user => user.Roles).HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Role).WithMany().HasForeignKey(entity => entity.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.UserId, entity.RoleId, entity.IsActive }).IsUnique();
    }
}

public sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermissionEntity>
{
    public void Configure(EntityTypeBuilder<RolePermissionEntity> builder)
    {
        builder.ToTable("rol_permisos");
        builder.ConfigureBase();
        builder.Property(entity => entity.RoleId).HasColumnName("rol_id");
        builder.Property(entity => entity.PermissionId).HasColumnName("permiso_id");
        builder.Property(entity => entity.IsActive).HasColumnName("vigente");
        builder.HasOne(entity => entity.Role).WithMany(role => role.Permissions).HasForeignKey(entity => entity.RoleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Permission).WithMany().HasForeignKey(entity => entity.PermissionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.RoleId, entity.PermissionId, entity.IsActive }).IsUnique();
    }
}

public sealed class UserFaenaConfiguration : IEntityTypeConfiguration<UserFaenaEntity>
{
    public void Configure(EntityTypeBuilder<UserFaenaEntity> builder)
    {
        builder.ToTable("usuario_faenas");
        builder.ConfigureBase();
        builder.Property(entity => entity.UserId).HasColumnName("usuario_id");
        builder.Property(entity => entity.FaenaId).HasColumnName("faena_id");
        builder.Property(entity => entity.IsActive).HasColumnName("vigente");
        builder.Property(entity => entity.AssignedAtUtc).HasColumnName("asignado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.AssignedByUserId).HasColumnName("asignado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.UnassignedAtUtc).HasColumnName("desasignado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.UnassignedByUserId).HasColumnName("desasignado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.UnassignedReason).HasColumnName("motivo_desasignacion").HasMaxLength(500);
        builder.HasOne(entity => entity.User).WithMany(user => user.Faenas).HasForeignKey(entity => entity.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Faena).WithMany().HasForeignKey(entity => entity.FaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.UserId, entity.FaenaId, entity.IsActive }).IsUnique();
    }
}

public sealed class FaenaConfiguration : IEntityTypeConfiguration<FaenaEntity>
{
    public void Configure(EntityTypeBuilder<FaenaEntity> builder)
    {
        builder.ToTable("faenas");
        builder.ConfigureBase();
        builder.Property(entity => entity.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Zone).HasColumnName("zona").HasMaxLength(80);
        builder.Property(entity => entity.Client).HasColumnName("cliente").HasMaxLength(160);
        builder.Property(entity => entity.CostCenter).HasColumnName("centro_costes").HasMaxLength(80);
        builder.Property(entity => entity.FaenaType).HasColumnName("tipo_faena").HasMaxLength(80);
        builder.Property(entity => entity.Region).HasColumnName("region").HasMaxLength(120);
        builder.Property(entity => entity.Commune).HasColumnName("comuna").HasMaxLength(120);
        builder.Property(entity => entity.Latitude).HasColumnName("latitud").HasPrecision(10, 7);
        builder.Property(entity => entity.Longitude).HasColumnName("longitud").HasPrecision(10, 7);
        builder.Property(entity => entity.ResponsibleUserId).HasColumnName("responsable_usuario_id");
        builder.Property(entity => entity.IsActive).HasColumnName("activo").IsRequired();
        builder.HasOne(entity => entity.ResponsibleUser)
            .WithMany(user => user.ResponsibleFaenas)
            .HasForeignKey(entity => entity.ResponsibleUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.ResponsibleUserId);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_faenas_latitud", "latitud IS NULL OR latitud BETWEEN -90 AND 90");
            table.HasCheckConstraint("ck_faenas_longitud", "longitud IS NULL OR longitud BETWEEN -180 AND 180");
        });
    }
}

public sealed class AssetOperationalStateConfiguration : IEntityTypeConfiguration<AssetOperationalStateEntity>
{
    public void Configure(EntityTypeBuilder<AssetOperationalStateEntity> builder)
    {
        builder.ToTable("estados_operacionales_activo");
        builder.ConfigureBase();
        builder.Property(entity => entity.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("nombre").HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Severity).HasColumnName("severidad").IsRequired();
        builder.Property(entity => entity.IsActive).HasColumnName("activo").IsRequired();
        builder.HasIndex(entity => entity.Code).IsUnique();
    }
}

public sealed class AssetTypeConfiguration : IEntityTypeConfiguration<AssetTypeEntity>
{
    public void Configure(EntityTypeBuilder<AssetTypeEntity> builder)
    {
        builder.ToTable("tipos_activo"); builder.ConfigureBase();
        builder.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(60).IsRequired(); builder.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(160).IsRequired(); builder.Property(x => x.Description).HasColumnName("descripcion").HasMaxLength(1000); builder.Property(x => x.Category).HasColumnName("categoria").HasMaxLength(60).IsRequired();
        builder.Property(x => x.IsMobile).HasColumnName("es_movil"); builder.Property(x => x.IsMountable).HasColumnName("es_montable"); builder.Property(x => x.CanBeCarrier).HasColumnName("puede_ser_portador"); builder.Property(x => x.ControlsMaintenance).HasColumnName("controla_mantenimiento"); builder.Property(x => x.ParticipatesInAvailability).HasColumnName("participa_en_disponibilidad"); builder.Property(x => x.SortOrder).HasColumnName("orden_visualizacion"); builder.Property(x => x.IsActive).HasColumnName("activo");
        builder.HasIndex(x => x.Code).IsUnique(); builder.ToTable(t => t.HasCheckConstraint("ck_tipos_activo_orden", "orden_visualizacion >= 0"));
    }
}

public sealed class EquipmentFamilyConfiguration : IEntityTypeConfiguration<EquipmentFamilyEntity>
{
    public void Configure(EntityTypeBuilder<EquipmentFamilyEntity> builder)
    {
        builder.ToTable("familias_equipo"); builder.ConfigureBase();
        builder.Property(x => x.AssetTypeId).HasColumnName("tipo_activo_id"); builder.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired(); builder.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(160).IsRequired(); builder.Property(x => x.Description).HasColumnName("descripcion").HasMaxLength(1000); builder.Property(x => x.ReferenceBrand).HasColumnName("marca_referencia").HasMaxLength(120); builder.Property(x => x.ReferenceModel).HasColumnName("modelo_referencia").HasMaxLength(120); builder.Property(x => x.IsActive).HasColumnName("activo");
        builder.HasIndex(x => x.Code).IsUnique(); builder.HasOne(x => x.AssetType).WithMany().HasForeignKey(x => x.AssetTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AssetConfiguration : IEntityTypeConfiguration<AssetEntity>
{
    public void Configure(EntityTypeBuilder<AssetEntity> builder)
    {
        builder.ToTable("activos"); builder.ConfigureBase();
        builder.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired(); builder.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired(); builder.Property(x => x.AssetTypeId).HasColumnName("tipo_activo_id"); builder.Property(x => x.FamilyId).HasColumnName("familia_equipo_id"); builder.Property(x => x.FaenaId).HasColumnName("faena_id"); builder.Property(x => x.OperationalStateId).HasColumnName("estado_operacional_id");
        builder.Property(x => x.Brand).HasColumnName("marca").HasMaxLength(120); builder.Property(x => x.Model).HasColumnName("modelo").HasMaxLength(120); builder.Property(x => x.SerialNumber).HasColumnName("numero_serie").HasMaxLength(160); builder.Property(x => x.Ownership).HasColumnName("propiedad").HasMaxLength(80); builder.Property(x => x.Criticality).HasColumnName("criticidad").HasMaxLength(40); builder.Property(x => x.ManufacturingYear).HasColumnName("anio_fabricacion"); builder.Property(x => x.AcquisitionDate).HasColumnName("fecha_adquisicion"); builder.Property(x => x.CommissioningDate).HasColumnName("fecha_puesta_servicio"); builder.Property(x => x.DecommissioningDate).HasColumnName("fecha_baja"); builder.Property(x => x.UsageMeasurementType).HasColumnName("tipo_medicion_uso").HasMaxLength(20); builder.Property(x => x.Observations).HasColumnName("observaciones").HasMaxLength(2000);
        builder.HasIndex(x => x.Code).IsUnique(); builder.HasIndex(x => x.FaenaId); builder.HasIndex(x => x.FamilyId); builder.HasIndex(x => x.OperationalStateId); builder.HasOne(x => x.AssetTypeDefinition).WithMany().HasForeignKey(x => x.AssetTypeId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(x => x.Faena).WithMany().HasForeignKey(x => x.FaenaId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(x => x.Family).WithMany().HasForeignKey(x => x.FamilyId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(x => x.OperationalState).WithMany().HasForeignKey(x => x.OperationalStateId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => { t.HasCheckConstraint("ck_activos_tipo_medicion_uso", "tipo_medicion_uso IS NULL OR tipo_medicion_uso IN ('HOROMETRO','KILOMETRAJE')"); t.HasCheckConstraint("ck_activos_anio_fabricacion", "anio_fabricacion IS NULL OR anio_fabricacion BETWEEN 1900 AND 2200"); t.HasCheckConstraint("ck_activos_fecha_baja", "fecha_baja IS NULL OR fecha_puesta_servicio IS NULL OR fecha_baja >= fecha_puesta_servicio"); });
    }
}
public sealed class AssetStateEventConfiguration : IEntityTypeConfiguration<AssetStateEventEntity>
{
    public void Configure(EntityTypeBuilder<AssetStateEventEntity> builder)
    {
        builder.ToTable("eventos_estado_activo");
        builder.ConfigureBase();
        builder.Property(entity => entity.AssetId).HasColumnName("activo_id");
        builder.Property(entity => entity.PreviousStateId).HasColumnName("estado_anterior_id");
        builder.Property(entity => entity.NewStateId).HasColumnName("estado_nuevo_id");
        builder.Property(entity => entity.OccurredAtUtc).HasColumnName("fecha_evento_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.UserId).HasColumnName("usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Reason).HasColumnName("motivo").HasMaxLength(500).IsRequired();
        builder.Property(entity => entity.ReferenceType).HasColumnName("tipo_antecedente").HasMaxLength(80);
        builder.Property(entity => entity.ReferenceId).HasColumnName("antecedente_id").HasMaxLength(240);
        builder.HasOne(entity => entity.Asset).WithMany().HasForeignKey(entity => entity.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.PreviousState).WithMany().HasForeignKey(entity => entity.PreviousStateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.NewState).WithMany().HasForeignKey(entity => entity.NewStateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentTypeConfiguration : IEntityTypeConfiguration<DocumentTypeEntity>
{
    public void Configure(EntityTypeBuilder<DocumentTypeEntity> builder)
    {
        builder.ToTable("tipos_documentales");
        builder.ConfigureBase();
        builder.Property(entity => entity.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.Description).HasColumnName("descripcion").HasMaxLength(1000);
        builder.Property(entity => entity.AppliesTo).HasColumnName("aplica_a").HasMaxLength(40);
        builder.Property(entity => entity.IsMandatory).HasColumnName("obligatorio");
        builder.Property(entity => entity.IsCritical).HasColumnName("critico");
        builder.Property(entity => entity.BlocksAvailability).HasColumnName("bloquea_disponibilidad");
        builder.Property(entity => entity.AlertDays).HasColumnName("dias_alerta");
        builder.Property(entity => entity.ResponsibleRoles).HasColumnName("roles_responsables").HasMaxLength(1000);
        builder.Property(entity => entity.RequiresAlertPdf).HasColumnName("requiere_pdf_alerta");
        builder.Property(entity => entity.HtmlTemplateCode).HasColumnName("plantilla_html_codigo").HasMaxLength(120);
        builder.Property(entity => entity.IsActive).HasColumnName("activo");
        builder.Property(entity => entity.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(120);
        builder.Property(entity => entity.UpdatedByUserId).HasColumnName("updated_by_user_id").HasMaxLength(120);
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("ck_tipos_documentales_dias_alerta", "dias_alerta >= 0"));
    }
}

public sealed class DocumentConfiguration : IEntityTypeConfiguration<DocumentEntity>
{
    public void Configure(EntityTypeBuilder<DocumentEntity> builder)
    {
        builder.ToTable("documentos");
        builder.ConfigureBase();
        builder.Property(entity => entity.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Title).HasColumnName("titulo").HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Description).HasColumnName("descripcion").HasMaxLength(1000);
        builder.Property(entity => entity.DocumentTypeId).HasColumnName("tipo_documental_id");
        builder.Property(entity => entity.Status).HasColumnName("estado").HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.IssueDate).HasColumnName("fecha_emision");
        builder.Property(entity => entity.ExpiresOn).HasColumnName("fecha_vencimiento");
        builder.Property(entity => entity.IsCurrent).HasColumnName("vigente");
        builder.Property(entity => entity.IsAnnulled).HasColumnName("anulado");
        builder.Property(entity => entity.AnnulledByUserId).HasColumnName("anulado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.AnnulledAtUtc).HasColumnName("anulado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.AnnulReason).HasColumnName("motivo_anulacion").HasMaxLength(500);
        builder.Property(entity => entity.CreatedByUserId).HasColumnName("created_by_user_id").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.UpdatedByUserId).HasColumnName("updated_by_user_id").HasMaxLength(120);
        builder.Property(entity => entity.ValidatedByUserId).HasColumnName("validado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.ValidatedAtUtc).HasColumnName("validado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.RejectedByUserId).HasColumnName("rechazado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.RejectedAtUtc).HasColumnName("rechazado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.RejectReason).HasColumnName("motivo_rechazo").HasMaxLength(500);
        builder.Property(entity => entity.ExpiryDateValidated).HasColumnName("fecha_vencimiento_validada");
        builder.Property(entity => entity.ReplacesDocumentId).HasColumnName("reemplaza_documento_id");
        builder.Property(entity => entity.ReplacedByDocumentId).HasColumnName("reemplazado_por_documento_id");
        builder.Property(entity => entity.IsHistorical).HasColumnName("historico");
        builder.Property(entity => entity.IsCritical).HasColumnName("critico");
        builder.Property(entity => entity.IsMandatory).HasColumnName("obligatorio");
        builder.Property(entity => entity.BlocksAvailability).HasColumnName("bloquea_disponibilidad");
        builder.Property(entity => entity.ChangeReason).HasColumnName("motivo_cambio").HasMaxLength(500);
        builder.HasIndex(entity => entity.Code).IsUnique();
        builder.HasIndex(entity => entity.DocumentTypeId);
        builder.HasIndex(entity => entity.Status);
        builder.HasOne(entity => entity.DocumentType).WithMany(type => type.Documents).HasForeignKey(entity => entity.DocumentTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ReplacesDocument).WithMany().HasForeignKey(entity => entity.ReplacesDocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ReplacedByDocument).WithMany().HasForeignKey(entity => entity.ReplacedByDocumentId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersionEntity>
{
    public void Configure(EntityTypeBuilder<DocumentVersionEntity> builder)
    {
        builder.ToTable("versiones_documento");
        builder.ConfigureBase();
        builder.Property(entity => entity.DocumentId).HasColumnName("documento_id");
        builder.Property(entity => entity.VersionNumber).HasColumnName("numero_version");
        builder.Property(entity => entity.VersionCode).HasColumnName("codigo_version").HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.FileId).HasColumnName("archivo_id");
        builder.Property(entity => entity.UploadedAtUtc).HasColumnName("fecha_carga_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.UploadedByUserId).HasColumnName("cargado_por_usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Observations).HasColumnName("observaciones").HasMaxLength(1000);
        builder.Property(entity => entity.IsCurrent).HasColumnName("vigente");
        builder.Property(entity => entity.Status).HasColumnName("estado").HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.IssueDate).HasColumnName("fecha_emision");
        builder.Property(entity => entity.ExpiresOn).HasColumnName("fecha_vencimiento");
        builder.Property(entity => entity.ValidationStatus).HasColumnName("estado_validacion").HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.ValidatedByUserId).HasColumnName("validado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.ValidatedAtUtc).HasColumnName("validado_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.RejectedByUserId).HasColumnName("rechazado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.RejectedAtUtc).HasColumnName("rechazado_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.RejectReason).HasColumnName("motivo_rechazo").HasMaxLength(500);
        builder.Property(entity => entity.ReplacesVersionId).HasColumnName("reemplaza_version_id");
        builder.Property(entity => entity.CorrectionResponsibleUserId).HasColumnName("responsable_correccion_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.CorrectionStatus).HasColumnName("estado_correccion").HasMaxLength(40);
        builder.Property(entity => entity.CorrectionObservation).HasColumnName("observacion_correccion").HasMaxLength(1000);
        builder.Property(entity => entity.CorrectionCycleId).HasColumnName("ciclo_correccion_id");
        builder.HasOne(entity => entity.Document).WithMany(document => document.Versions).HasForeignKey(entity => entity.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.ReplacesVersion).WithMany().HasForeignKey(entity => entity.ReplacesVersionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.File).WithMany().HasForeignKey(entity => entity.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.DocumentId, entity.VersionNumber }).IsUnique();
        builder.HasIndex(entity => new { entity.DocumentId, entity.IsCurrent })
            .IsUnique()
            .HasFilter("vigente");
    }
}

public sealed class FileMetadataConfiguration : IEntityTypeConfiguration<FileMetadataEntity>
{
    public void Configure(EntityTypeBuilder<FileMetadataEntity> builder)
    {
        builder.ToTable("archivos");
        builder.ConfigureBase();
        builder.Property(entity => entity.FileKey).HasColumnName("file_key").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.FileName).HasColumnName("nombre").HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.StoredFileName).HasColumnName("nombre_almacenado").HasMaxLength(300).IsRequired();
        builder.Property(entity => entity.Extension).HasColumnName("extension").HasMaxLength(32).IsRequired();
        builder.Property(entity => entity.Provider).HasColumnName("proveedor").HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.StorageMode).HasColumnName("modo_almacenamiento").HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Purpose).HasColumnName("proposito").HasMaxLength(80).IsRequired();
        builder.Property(entity => entity.Module).HasColumnName("modulo").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.EntityType).HasColumnName("tipo_entidad").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.EntityId).HasColumnName("entidad_id").HasMaxLength(240).IsRequired();
        builder.Property(entity => entity.FaenaCode).HasColumnName("faena_codigo").HasMaxLength(80);
        builder.Property(entity => entity.AssetCode).HasColumnName("activo_codigo").HasMaxLength(80);
        builder.Property(entity => entity.WorkOrderNumber).HasColumnName("numero_ot").HasMaxLength(80);
        builder.Property(entity => entity.LogicalUri).HasColumnName("uri_logica").HasMaxLength(1000).IsRequired();
        builder.Property(entity => entity.LogicalPath).HasColumnName("ruta_logica").HasMaxLength(1000);
        builder.Property(entity => entity.PhysicalLocation).HasColumnName("ubicacion_fisica").HasMaxLength(2000);
        builder.Property(entity => entity.MimeType).HasColumnName("tipo_mime").HasMaxLength(160);
        builder.Property(entity => entity.SizeBytes).HasColumnName("tamano_bytes");
        builder.Property(entity => entity.Checksum).HasColumnName("checksum").HasMaxLength(200);
        builder.Property(entity => entity.Status).HasColumnName("estado").HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.FileVersion).HasColumnName("version_archivo").IsRequired();
        builder.Property(entity => entity.IsDeleted).HasColumnName("eliminado").IsRequired();
        builder.Property(entity => entity.DeletedAtUtc).HasColumnName("eliminado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.DeletedByUserId).HasColumnName("eliminado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.MetadataJson).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(entity => entity.AuthorUserId).HasColumnName("autor_usuario_id").HasMaxLength(120);
        builder.HasIndex(entity => entity.FileKey).IsUnique();
        builder.HasIndex(entity => entity.LogicalUri);
        builder.HasIndex(entity => entity.Provider);
        builder.HasIndex(entity => entity.Checksum);
        builder.HasIndex(entity => entity.CreatedAtUtc);
        builder.HasIndex(entity => new { entity.EntityType, entity.EntityId, entity.IsDeleted });
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_archivos_tamano_no_negativo", "tamano_bytes IS NULL OR tamano_bytes >= 0");
            table.HasCheckConstraint("ck_archivos_proveedor", "proveedor IN ('ManualLink','LocalSimulation','GraphApiReady')");
            table.HasCheckConstraint("ck_archivos_estado", "estado IN ('Stored','ManualLink','PendingManualLink','GraphApiReady','InvalidPath','Deleted')");
        });
    }
}

public sealed class DocumentAssetConfiguration : IEntityTypeConfiguration<DocumentAssetEntity>
{
    public void Configure(EntityTypeBuilder<DocumentAssetEntity> builder)
    {
        builder.ToTable("documento_activos");
        builder.ConfigureBase();
        builder.Property(entity => entity.DocumentId).HasColumnName("documento_id");
        builder.Property(entity => entity.AssetId).HasColumnName("activo_id");
        builder.Property(entity => entity.IsActive).HasColumnName("vigente");
        builder.Property(entity => entity.AssignedAtUtc).HasColumnName("asignado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.AssignedByUserId).HasColumnName("asignado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.UnassignedAtUtc).HasColumnName("desasignado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.UnassignedByUserId).HasColumnName("desasignado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.UnassignedReason).HasColumnName("motivo_desasignacion").HasMaxLength(500);
        builder.HasOne(entity => entity.Document).WithMany(document => document.Assets).HasForeignKey(entity => entity.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Asset).WithMany().HasForeignKey(entity => entity.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.DocumentId, entity.AssetId, entity.IsActive })
            .IsUnique()
            .HasFilter("vigente");
    }
}

public sealed class DocumentFaenaConfiguration : IEntityTypeConfiguration<DocumentFaenaEntity>
{
    public void Configure(EntityTypeBuilder<DocumentFaenaEntity> builder)
    {
        builder.ToTable("documento_faenas");
        builder.ConfigureBase();
        builder.Property(entity => entity.DocumentId).HasColumnName("documento_id");
        builder.Property(entity => entity.FaenaId).HasColumnName("faena_id");
        builder.Property(entity => entity.IsActive).HasColumnName("vigente");
        builder.Property(entity => entity.AssignedAtUtc).HasColumnName("asignado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.AssignedByUserId).HasColumnName("asignado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.UnassignedAtUtc).HasColumnName("desasignado_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.UnassignedByUserId).HasColumnName("desasignado_por_usuario_id").HasMaxLength(120);
        builder.Property(entity => entity.UnassignedReason).HasColumnName("motivo_desasignacion").HasMaxLength(500);
        builder.HasOne(entity => entity.Document).WithMany(document => document.Faenas).HasForeignKey(entity => entity.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.Faena).WithMany().HasForeignKey(entity => entity.FaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.FaenaId);
        builder.HasIndex(entity => new { entity.DocumentId, entity.FaenaId, entity.IsActive })
            .IsUnique()
            .HasFilter("vigente");
    }
}

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("audit_log");
        builder.ConfigureBase();
        builder.Property(entity => entity.OccurredAtUtc).HasColumnName("occurred_at_utc").HasColumnType("timestamptz");
        builder.Property(entity => entity.UserId).HasColumnName("usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.Action).HasColumnName("accion").HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.Module).HasColumnName("modulo").HasMaxLength(120).IsRequired();
        builder.Property(entity => entity.EntityName).HasColumnName("entidad").HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.EntityId).HasColumnName("entidad_id").HasMaxLength(160).IsRequired();
        builder.Property(entity => entity.FaenaCode).HasColumnName("faena_codigo").HasMaxLength(80);
        builder.Property(entity => entity.Severity).HasColumnName("severidad").HasMaxLength(40).IsRequired();
        builder.Property(entity => entity.PreviousValue).HasColumnName("valor_anterior");
        builder.Property(entity => entity.NewValue).HasColumnName("valor_nuevo");
        builder.Property(entity => entity.IpAddress).HasColumnName("ip_address").HasMaxLength(80);
        builder.Property(entity => entity.Device).HasColumnName("dispositivo").HasMaxLength(240);
        builder.Property(entity => entity.Reason).HasColumnName("motivo").HasMaxLength(500);
        builder.Property(entity => entity.Success).HasColumnName("exitoso");
        builder.Property(entity => entity.Detail).HasColumnName("detalle");
        builder.Property(entity => entity.CorrelationId).HasColumnName("correlation_id").HasMaxLength(120);
        builder.HasIndex(entity => entity.OccurredAtUtc);
        builder.HasIndex(entity => new { entity.Module, entity.EntityName });
        builder.HasIndex(entity => entity.FaenaCode);
    }
}
