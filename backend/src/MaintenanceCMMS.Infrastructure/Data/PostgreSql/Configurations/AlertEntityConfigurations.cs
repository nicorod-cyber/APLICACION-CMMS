using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Configurations;

public sealed class PdfTemplateConfiguration : IEntityTypeConfiguration<PdfTemplateEntity>
{
    public void Configure(EntityTypeBuilder<PdfTemplateEntity> builder)
    {
        builder.ToTable("plantillas_pdf"); builder.ConfigureBase();
        builder.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(x => x.EventType).HasColumnName("tipo_evento").HasMaxLength(120).IsRequired();
        builder.Property(x => x.SubjectTemplate).HasColumnName("asunto_plantilla").HasMaxLength(500).IsRequired();
        builder.Property(x => x.HtmlTemplate).HasColumnName("html_plantilla").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("activo"); builder.Property(x => x.TemplateVersion).HasColumnName("version_plantilla");
        builder.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120); builder.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120); builder.Property(x => x.FileId).HasColumnName("archivo_id");
        builder.HasIndex(x => x.Code).IsUnique(); builder.HasIndex(x => new { x.EventType, x.IsActive });
        builder.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(x => x.HasCheckConstraint("ck_plantillas_pdf_version", "version_plantilla >= 1"));
    }
}

public sealed class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRuleEntity>
{
    public void Configure(EntityTypeBuilder<AlertRuleEntity> builder)
    {
        builder.ToTable("reglas_alerta"); builder.ConfigureBase();
        builder.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired(); builder.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired(); builder.Property(x => x.EventType).HasColumnName("tipo_evento").HasMaxLength(120).IsRequired();
        builder.Property(x => x.IsEnabled).HasColumnName("activa"); builder.Property(x => x.Severity).HasColumnName("severidad").HasMaxLength(40).IsRequired(); builder.Property(x => x.RepeatUntilResolved).HasColumnName("repetir_hasta_resolver"); builder.Property(x => x.GenerateEmail).HasColumnName("genera_email"); builder.Property(x => x.GeneratePdf).HasColumnName("genera_pdf");
        builder.Property(x => x.TemplateId).HasColumnName("plantilla_id"); builder.Property(x => x.FaenaId).HasColumnName("faena_id"); builder.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120); builder.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        builder.HasIndex(x => x.Code).IsUnique(); builder.HasIndex(x => x.EventType); builder.HasIndex(x => x.FaenaId); builder.HasIndex(x => new { x.IsEnabled, x.Severity });
        builder.HasOne(x => x.Template).WithMany().HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(x => x.Faena).WithMany().HasForeignKey(x => x.FaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(x => x.HasCheckConstraint("ck_reglas_alerta_severidad", "severidad IN ('Info','Warning','Critical')"));
    }
}

public sealed class AlertRuleRecipientConfiguration : IEntityTypeConfiguration<AlertRuleRecipientEntity>
{
    public void Configure(EntityTypeBuilder<AlertRuleRecipientEntity> builder)
    {
        builder.ToTable("regla_alerta_destinatarios"); builder.ConfigureBase();
        builder.Property(x => x.AlertRuleId).HasColumnName("regla_alerta_id"); builder.Property(x => x.UserId).HasColumnName("usuario_id"); builder.Property(x => x.RoleId).HasColumnName("rol_id"); builder.Property(x => x.Destination).HasColumnName("destino").HasMaxLength(320); builder.Property(x => x.Channel).HasColumnName("canal").HasMaxLength(40).IsRequired(); builder.Property(x => x.IsActive).HasColumnName("activo");
        builder.HasIndex(x => new { x.AlertRuleId, x.Destination, x.Channel }).IsUnique(); builder.HasOne(x => x.AlertRule).WithMany(x => x.Recipients).HasForeignKey(x => x.AlertRuleId).OnDelete(DeleteBehavior.Cascade); builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AlertConfiguration : IEntityTypeConfiguration<AlertEntity>
{
    public void Configure(EntityTypeBuilder<AlertEntity> builder)
    {
        builder.ToTable("alertas"); builder.ConfigureBase();
        builder.Property(x => x.AlertRuleId).HasColumnName("regla_alerta_id"); builder.Property(x => x.Title).HasColumnName("titulo").HasMaxLength(500).IsRequired(); builder.Property(x => x.Message).HasColumnName("mensaje").IsRequired(); builder.Property(x => x.Severity).HasColumnName("severidad").HasMaxLength(40).IsRequired(); builder.Property(x => x.Status).HasColumnName("estado").HasMaxLength(40).IsRequired(); builder.Property(x => x.Source).HasColumnName("origen").HasMaxLength(160).IsRequired(); builder.Property(x => x.CauseKey).HasColumnName("clave_causa").HasMaxLength(240).IsRequired(); builder.Property(x => x.DeduplicationKey).HasColumnName("clave_deduplicacion").HasMaxLength(400).IsRequired();
        builder.Property(x => x.FaenaId).HasColumnName("faena_id"); builder.Property(x => x.EntityType).HasColumnName("tipo_entidad").HasMaxLength(120); builder.Property(x => x.EntityId).HasColumnName("entidad_id").HasMaxLength(240); builder.Property(x => x.IsCriticalRepeat).HasColumnName("repeticion_critica"); builder.Property(x => x.RepeatCount).HasColumnName("cantidad_repeticiones"); builder.Property(x => x.AcknowledgedAtUtc).HasColumnName("reconocido_at_utc").HasColumnType("timestamptz"); builder.Property(x => x.AcknowledgedByUserId).HasColumnName("reconocido_por_usuario_id").HasMaxLength(120); builder.Property(x => x.ResolvedAtUtc).HasColumnName("resuelto_at_utc").HasColumnType("timestamptz"); builder.Property(x => x.ResolvedByUserId).HasColumnName("resuelto_por_usuario_id").HasMaxLength(120); builder.Property(x => x.ResolutionReason).HasColumnName("motivo_resolucion").HasMaxLength(1000); builder.Property(x => x.IsActive).HasColumnName("activa"); builder.Property(x => x.GeneratedPdfFileId).HasColumnName("archivo_pdf_id");
        builder.HasIndex(x => new { x.AlertRuleId, x.DeduplicationKey, x.IsActive }).IsUnique().HasFilter("activa"); builder.HasIndex(x => new { x.EntityType, x.EntityId }); builder.HasIndex(x => x.FaenaId); builder.HasIndex(x => new { x.Status, x.Severity });
        builder.HasOne(x => x.AlertRule).WithMany().HasForeignKey(x => x.AlertRuleId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(x => x.Faena).WithMany().HasForeignKey(x => x.FaenaId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(x => x.GeneratedPdfFile).WithMany().HasForeignKey(x => x.GeneratedPdfFileId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(x => { x.HasCheckConstraint("ck_alertas_severidad", "severidad IN ('Info','Warning','Critical')"); x.HasCheckConstraint("ck_alertas_estado", "estado IN ('Open','Acknowledged','Resolved')"); x.HasCheckConstraint("ck_alertas_repeticiones", "cantidad_repeticiones >= 1"); });
    }
}

public sealed class NotificationConfiguration : IEntityTypeConfiguration<NotificationEntity>
{
    public void Configure(EntityTypeBuilder<NotificationEntity> builder)
    {
        builder.ToTable("notificaciones"); builder.ConfigureBase();
        builder.Property(x => x.AlertId).HasColumnName("alerta_id"); builder.Property(x => x.Channel).HasColumnName("canal").HasMaxLength(40).IsRequired(); builder.Property(x => x.Subject).HasColumnName("asunto").HasMaxLength(500).IsRequired(); builder.Property(x => x.Body).HasColumnName("cuerpo").IsRequired(); builder.Property(x => x.Status).HasColumnName("estado").HasMaxLength(40).IsRequired(); builder.Property(x => x.ScheduledAtUtc).HasColumnName("programado_at_utc").HasColumnType("timestamptz"); builder.Property(x => x.SentAtUtc).HasColumnName("enviado_at_utc").HasColumnType("timestamptz"); builder.Property(x => x.AttemptCount).HasColumnName("cantidad_intentos"); builder.Property(x => x.Provider).HasColumnName("proveedor").HasMaxLength(120); builder.Property(x => x.LastError).HasColumnName("ultimo_error").HasMaxLength(2000); builder.Property(x => x.PdfFileId).HasColumnName("archivo_pdf_id");
        builder.HasIndex(x => x.AlertId); builder.HasIndex(x => new { x.Status, x.CreatedAtUtc }); builder.HasOne(x => x.Alert).WithMany(x => x.Notifications).HasForeignKey(x => x.AlertId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(x => x.PdfFile).WithMany().HasForeignKey(x => x.PdfFileId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(x => x.HasCheckConstraint("ck_notificaciones_estado", "estado IN ('Pending','Sent','Failed','Cancelled')"));
    }
}

public sealed class NotificationRecipientConfiguration : IEntityTypeConfiguration<NotificationRecipientEntity>
{
    public void Configure(EntityTypeBuilder<NotificationRecipientEntity> builder)
    {
        builder.ToTable("notificacion_destinatarios"); builder.ConfigureBase();
        builder.Property(x => x.NotificationId).HasColumnName("notificacion_id"); builder.Property(x => x.UserId).HasColumnName("usuario_id"); builder.Property(x => x.RoleId).HasColumnName("rol_id"); builder.Property(x => x.Destination).HasColumnName("destino").HasMaxLength(320); builder.Property(x => x.DeliveryStatus).HasColumnName("estado_entrega").HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.NotificationId, x.Destination }).IsUnique(); builder.HasOne(x => x.Notification).WithMany(x => x.Recipients).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade); builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(x => x.Role).WithMany().HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class NotificationAttemptConfiguration : IEntityTypeConfiguration<NotificationAttemptEntity>
{
    public void Configure(EntityTypeBuilder<NotificationAttemptEntity> builder)
    {
        builder.ToTable("notificacion_intentos"); builder.ConfigureBase();
        builder.Property(x => x.NotificationId).HasColumnName("notificacion_id"); builder.Property(x => x.AttemptNumber).HasColumnName("numero_intento"); builder.Property(x => x.AttemptedAtUtc).HasColumnName("intentado_at_utc").HasColumnType("timestamptz"); builder.Property(x => x.Success).HasColumnName("exitoso"); builder.Property(x => x.Provider).HasColumnName("proveedor").HasMaxLength(120); builder.Property(x => x.Error).HasColumnName("error").HasMaxLength(2000);
        builder.HasIndex(x => new { x.NotificationId, x.AttemptNumber }).IsUnique(); builder.HasOne(x => x.Notification).WithMany(x => x.Attempts).HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
    }
}
