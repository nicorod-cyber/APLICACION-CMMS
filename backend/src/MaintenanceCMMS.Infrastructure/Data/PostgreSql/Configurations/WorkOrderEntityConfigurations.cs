using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Configurations;

public sealed class WorkCatalogConfiguration : IEntityTypeConfiguration<WorkCatalogEntity>
{
    public void Configure(EntityTypeBuilder<WorkCatalogEntity> builder)
    {
        builder.ToTable("catalogos_trabajo");
        builder.ConfigureBase();
        builder.Property(e => e.Category).HasColumnName("categoria").HasMaxLength(80).IsRequired();
        builder.Property(e => e.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(e => e.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(e => e.Description).HasColumnName("descripcion").HasMaxLength(1000);
        builder.Property(e => e.IsActive).HasColumnName("activo");
        builder.Property(e => e.SortOrder).HasColumnName("orden");
        builder.HasIndex(e => new { e.Category, e.Code }).IsUnique();
    }
}

public sealed class WorkNotificationConfiguration : IEntityTypeConfiguration<WorkNotificationEntity>
{
    public void Configure(EntityTypeBuilder<WorkNotificationEntity> builder)
    {
        builder.ToTable("avisos_trabajo_sql");
        builder.ConfigureBase();
        builder.Property(e => e.NotificationNumber).HasColumnName("aviso_id").HasMaxLength(40).IsRequired();
        builder.Property(e => e.StatusId).HasColumnName("estado_id");
        builder.Property(e => e.TypeId).HasColumnName("tipo_id");
        builder.Property(e => e.FaenaId).HasColumnName("faena_id");
        builder.Property(e => e.AssetId).HasColumnName("activo_id");
        builder.Property(e => e.OperationalUnitId).HasColumnName("unidad_operativa_id");
        builder.Property(e => e.System).HasColumnName("sistema").HasMaxLength(120);
        builder.Property(e => e.Subsystem).HasColumnName("subsistema").HasMaxLength(120);
        builder.Property(e => e.Component).HasColumnName("componente").HasMaxLength(120);
        builder.Property(e => e.Description).HasColumnName("descripcion").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.PriorityId).HasColumnName("prioridad_id");
        builder.Property(e => e.CriticalityId).HasColumnName("criticidad_id");
        builder.Property(e => e.RequesterUserId).HasColumnName("solicitante_usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(e => e.InitialEvidenceReference).HasColumnName("evidencia_inicial").HasMaxLength(1000);
        builder.Property(e => e.DetectedAtUtc).HasColumnName("fecha_deteccion_utc").HasColumnType("timestamptz");
        builder.Property(e => e.CreatedByUserAtUtc).HasColumnName("fecha_creacion_usuario_utc").HasColumnType("timestamptz");
        builder.Property(e => e.FailureClassificationId).HasColumnName("clasificacion_falla_id");
        builder.Property(e => e.EvaluatedByUserId).HasColumnName("evaluado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.EvaluatedAtUtc).HasColumnName("evaluado_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.ApprovedByUserId).HasColumnName("aprobado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.ApprovedAtUtc).HasColumnName("aprobado_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.RejectedByUserId).HasColumnName("rechazado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.RejectedAtUtc).HasColumnName("rechazado_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.RejectReason).HasColumnName("motivo_rechazo").HasMaxLength(500);
        builder.Property(e => e.AnnulledByUserId).HasColumnName("anulado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.AnnulledAtUtc).HasColumnName("anulado_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.AnnulReason).HasColumnName("motivo_anulacion").HasMaxLength(500);
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id");
        builder.Property(e => e.ConvertedByUserId).HasColumnName("convertido_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.ConvertedAtUtc).HasColumnName("convertido_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.Observations).HasColumnName("observaciones").HasMaxLength(2000);
        builder.HasIndex(e => e.NotificationNumber).IsUnique();
        builder.HasIndex(e => e.FaenaId);
        builder.HasIndex(e => e.AssetId);
        builder.HasIndex(e => e.OperationalUnitId);
        builder.HasIndex(e => e.WorkOrderId).IsUnique().HasFilter("orden_trabajo_id IS NOT NULL");
        builder.HasOne(e => e.Status).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Type).WithMany().HasForeignKey(e => e.TypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Priority).WithMany().HasForeignKey(e => e.PriorityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Criticality).WithMany().HasForeignKey(e => e.CriticalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.FailureClassification).WithMany().HasForeignKey(e => e.FailureClassificationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Faena).WithMany().HasForeignKey(e => e.FaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Asset).WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.OperationalUnit).WithMany().HasForeignKey(e => e.OperationalUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WorkOrder).WithOne(e => e.Notification).HasForeignKey<WorkNotificationEntity>(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkOrderConfiguration : IEntityTypeConfiguration<WorkOrderEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderEntity> builder)
    {
        builder.ToTable("ordenes_trabajo_sql");
        builder.ConfigureBase();
        builder.Property(e => e.WorkOrderNumber).HasColumnName("numero_ot").HasMaxLength(40).IsRequired();
        builder.Property(e => e.AssetId).HasColumnName("activo_id");
        builder.Property(e => e.OperationalUnitId).HasColumnName("unidad_operativa_id");
        builder.Property(e => e.FaenaId).HasColumnName("faena_id");
        builder.Property(e => e.StatusId).HasColumnName("estado_id");
        builder.Property(e => e.MaintenanceTypeId).HasColumnName("tipo_mantenimiento_id");
        builder.Property(e => e.Description).HasColumnName("descripcion").HasMaxLength(2000).IsRequired();
        builder.Property(e => e.NotificationId).HasColumnName("aviso_id");
        builder.Property(e => e.System).HasColumnName("sistema").HasMaxLength(120);
        builder.Property(e => e.Subsystem).HasColumnName("subsistema").HasMaxLength(120);
        builder.Property(e => e.Component).HasColumnName("componente").HasMaxLength(120);
        builder.Property(e => e.PriorityId).HasColumnName("prioridad_id");
        builder.Property(e => e.CriticalityId).HasColumnName("criticidad_id");
        builder.Property(e => e.FailureClassificationId).HasColumnName("clasificacion_falla_id");
        builder.Property(e => e.PreventivePlanCode).HasColumnName("plan_preventivo_codigo").HasMaxLength(120);
        builder.Property(e => e.IsAutomaticPreventive).HasColumnName("preventiva_automatica");
        builder.Property(e => e.RequiresSignature).HasColumnName("requiere_firma");
        builder.Property(e => e.ScheduledAtUtc).HasColumnName("fecha_programada_utc").HasColumnType("timestamptz");
        builder.Property(e => e.ScheduledStartUtc).HasColumnName("inicio_programado_utc").HasColumnType("timestamptz");
        builder.Property(e => e.ScheduledEndUtc).HasColumnName("fin_programado_utc").HasColumnType("timestamptz");
        builder.Property(e => e.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(e => e.CreatedByUserAtUtc).HasColumnName("creado_por_usuario_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.ActualStartUtc).HasColumnName("inicio_real_utc").HasColumnType("timestamptz");
        builder.Property(e => e.TechnicianFinishedAtUtc).HasColumnName("finalizacion_tecnico_utc").HasColumnType("timestamptz");
        builder.Property(e => e.FinishedByUserId).HasColumnName("finalizado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.SupervisorClosedAtUtc).HasColumnName("cierre_supervisor_utc").HasColumnType("timestamptz");
        builder.Property(e => e.ClosedByUserId).HasColumnName("cerrado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.PlanningValidatedAtUtc).HasColumnName("validacion_planificacion_utc").HasColumnType("timestamptz");
        builder.Property(e => e.ValidatedByUserId).HasColumnName("validado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.AnnulledByUserId).HasColumnName("anulado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.AnnulledAtUtc).HasColumnName("anulado_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.AnnulReason).HasColumnName("motivo_anulacion").HasMaxLength(500);
        builder.Property(e => e.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.UpdatedByUserAtUtc).HasColumnName("actualizado_por_usuario_at_utc").HasColumnType("timestamptz");
        builder.HasIndex(e => e.WorkOrderNumber).IsUnique();
        builder.HasIndex(e => e.AssetId);
        builder.HasIndex(e => e.OperationalUnitId);
        builder.HasIndex(e => e.FaenaId);
        builder.HasIndex(e => e.NotificationId).IsUnique().HasFilter("aviso_id IS NOT NULL");
        builder.HasOne(e => e.Asset).WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.OperationalUnit).WithMany().HasForeignKey(e => e.OperationalUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Faena).WithMany().HasForeignKey(e => e.FaenaId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Status).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.MaintenanceType).WithMany().HasForeignKey(e => e.MaintenanceTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Priority).WithMany().HasForeignKey(e => e.PriorityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Criticality).WithMany().HasForeignKey(e => e.CriticalityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.FailureClassification).WithMany().HasForeignKey(e => e.FailureClassificationId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_ordenes_trabajo_sql_objetivo", "activo_id IS NOT NULL OR unidad_operativa_id IS NOT NULL"));
    }
}

public sealed class WorkOrderTaskConfiguration : IEntityTypeConfiguration<WorkOrderTaskEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderTaskEntity> builder)
    {
        builder.ToTable("tareas_ot_sql"); builder.ConfigureBase();
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id");
        builder.Property(e => e.TaskCode).HasColumnName("codigo_tarea").HasMaxLength(40).IsRequired();
        builder.Property(e => e.Description).HasColumnName("descripcion").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.ScheduledStartUtc).HasColumnName("inicio_programado_utc").HasColumnType("timestamptz");
        builder.Property(e => e.ScheduledEndUtc).HasColumnName("fin_programado_utc").HasColumnType("timestamptz");
        builder.Property(e => e.RequiresEvidence).HasColumnName("requiere_evidencia");
        builder.Property(e => e.RequiresLabor).HasColumnName("requiere_hh");
        builder.Property(e => e.ChecklistMandatory).HasColumnName("checklist_obligatorio");
        builder.Property(e => e.Observations).HasColumnName("observaciones").HasMaxLength(1000);
        builder.Property(e => e.IsActive).HasColumnName("vigente");
        builder.HasOne(e => e.WorkOrder).WithMany(e => e.Tasks).HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.WorkOrderId, e.TaskCode }).IsUnique();
    }
}

public sealed class WorkOrderTaskTechnicianConfiguration : IEntityTypeConfiguration<WorkOrderTaskTechnicianEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderTaskTechnicianEntity> builder)
    {
        builder.ToTable("ot_tecnicos_tarea_sql"); builder.ConfigureBase();
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id"); builder.Property(e => e.TaskId).HasColumnName("tarea_id");
        builder.Property(e => e.TechnicianUserId).HasColumnName("tecnico_usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(e => e.TechnicianDisplayName).HasColumnName("tecnico_nombre").HasMaxLength(240);
        builder.Property(e => e.AssignedAtUtc).HasColumnName("asignado_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.AssignedByUserId).HasColumnName("asignado_por_usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(e => e.IsActive).HasColumnName("vigente");
        builder.Property(e => e.UnassignedAtUtc).HasColumnName("desasignado_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.UnassignedByUserId).HasColumnName("desasignado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.UnassignedReason).HasColumnName("motivo_desasignacion").HasMaxLength(500);
        builder.HasOne(e => e.WorkOrder).WithMany(e => e.Technicians).HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Task).WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TaskId, e.TechnicianUserId, e.IsActive }).IsUnique().HasFilter("vigente");
    }
}

public sealed class WorkOrderLaborConfiguration : IEntityTypeConfiguration<WorkOrderLaborEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderLaborEntity> builder)
    {
        builder.ToTable("ot_hh_sql"); builder.ConfigureBase();
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id"); builder.Property(e => e.TaskId).HasColumnName("tarea_id");
        builder.Property(e => e.TechnicianUserId).HasColumnName("tecnico_usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(e => e.Hours).HasColumnName("horas").HasColumnType("numeric(12,2)");
        builder.Property(e => e.Description).HasColumnName("descripcion").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.WorkDateUtc).HasColumnName("fecha_trabajo_utc").HasColumnType("timestamptz");
        builder.Property(e => e.StartTimeUtc).HasColumnName("hora_inicio_utc").HasColumnType("timestamptz");
        builder.Property(e => e.EndTimeUtc).HasColumnName("hora_termino_utc").HasColumnType("timestamptz");
        builder.Property(e => e.RegisteredByUserId).HasColumnName("registrado_por_usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(e => e.Comment).HasColumnName("comentario").HasMaxLength(1000);
        builder.Property(e => e.SupervisorValidated).HasColumnName("validado_supervisor");
        builder.Property(e => e.ValidatedByUserId).HasColumnName("validado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.ValidatedAtUtc).HasColumnName("validado_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.IsActive).HasColumnName("vigente");
        builder.HasOne(e => e.WorkOrder).WithMany(e => e.Labor).HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Task).WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_ot_hh_sql_horas", "horas > 0"));
    }
}

public sealed class WorkOrderEvidenceConfiguration : IEntityTypeConfiguration<WorkOrderEvidenceEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderEvidenceEntity> builder)
    {
        builder.ToTable("ot_evidencias_sql"); builder.ConfigureBase();
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id"); builder.Property(e => e.TaskId).HasColumnName("tarea_id");
        builder.Property(e => e.Name).HasColumnName("nombre").HasMaxLength(300).IsRequired();
        builder.Property(e => e.FileId).HasColumnName("archivo_id"); builder.Property(e => e.EvidenceTypeId).HasColumnName("tipo_evidencia_id");
        builder.Property(e => e.IsPhoto).HasColumnName("es_foto"); builder.Property(e => e.IsMandatory).HasColumnName("es_obligatoria");
        builder.Property(e => e.CoversMandatoryEvidence).HasColumnName("cubre_evidencia_obligatoria");
        builder.Property(e => e.StorageProvider).HasColumnName("proveedor").HasMaxLength(80);
        builder.Property(e => e.ExternalUri).HasColumnName("uri_externa").HasMaxLength(1000);
        builder.Property(e => e.ExternalKey).HasColumnName("clave_externa").HasMaxLength(300);
        builder.Property(e => e.LocalPath).HasColumnName("ruta_local").HasMaxLength(1000);
        builder.Property(e => e.OfflineId).HasColumnName("offline_id").HasMaxLength(120);
        builder.Property(e => e.SyncStatus).HasColumnName("estado_sync").HasMaxLength(80);
        builder.Property(e => e.Observations).HasColumnName("observaciones").HasMaxLength(1000);
        builder.Property(e => e.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired();
        builder.Property(e => e.CreatedByUserAtUtc).HasColumnName("creado_por_usuario_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.IsActive).HasColumnName("vigente");
        builder.HasOne(e => e.WorkOrder).WithMany(e => e.Evidences).HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Task).WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.File).WithMany().HasForeignKey(e => e.FileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.EvidenceType).WithMany().HasForeignKey(e => e.EvidenceTypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkOrderSparePartConfiguration : IEntityTypeConfiguration<WorkOrderSparePartEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderSparePartEntity> builder)
    {
        builder.ToTable("ot_repuestos_sql"); builder.ConfigureBase();
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id"); builder.Property(e => e.TaskId).HasColumnName("tarea_id");
        builder.Property(e => e.SparePartCode).HasColumnName("repuesto_codigo").HasMaxLength(120).IsRequired();
        builder.Property(e => e.Quantity).HasColumnName("cantidad").HasColumnType("numeric(12,2)");
        builder.Property(e => e.Unit).HasColumnName("unidad").HasMaxLength(40).IsRequired();
        builder.Property(e => e.WarehouseCode).HasColumnName("bodega_codigo").HasMaxLength(120);
        builder.Property(e => e.StatusId).HasColumnName("estado_id");
        builder.Property(e => e.UsedQuantity).HasColumnName("cantidad_utilizada").HasColumnType("numeric(12,2)");
        builder.Property(e => e.ReturnedQuantity).HasColumnName("cantidad_devuelta").HasColumnType("numeric(12,2)");
        builder.Property(e => e.Observations).HasColumnName("observaciones").HasMaxLength(1000);
        builder.Property(e => e.IsActive).HasColumnName("vigente");
        builder.HasOne(e => e.WorkOrder).WithMany(e => e.SpareParts).HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Task).WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Status).WithMany().HasForeignKey(e => e.StatusId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_ot_repuestos_sql_cantidades", "cantidad > 0 AND cantidad_utilizada >= 0 AND cantidad_devuelta >= 0"));
    }
}

public sealed class ChecklistTemplateConfiguration : IEntityTypeConfiguration<ChecklistTemplateEntity>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplateEntity> builder)
    {
        builder.ToTable("plantillas_checklist"); builder.ConfigureBase();
        builder.Property(e => e.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired();
        builder.Property(e => e.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        builder.Property(e => e.WorkOrderTypeCode).HasColumnName("tipo_ot_codigo").HasMaxLength(120);
        builder.Property(e => e.FamilyCode).HasColumnName("familia_codigo").HasMaxLength(120);
        builder.Property(e => e.PreventivePlanCode).HasColumnName("plan_preventivo_codigo").HasMaxLength(120);
        builder.Property(e => e.TaskCode).HasColumnName("tarea_codigo").HasMaxLength(40);
        builder.Property(e => e.AssetCode).HasColumnName("activo_codigo").HasMaxLength(120);
        builder.Property(e => e.IsActive).HasColumnName("activo");
        builder.HasIndex(e => e.Code).IsUnique();
    }
}

public sealed class ChecklistTemplateItemConfiguration : IEntityTypeConfiguration<ChecklistTemplateItemEntity>
{
    public void Configure(EntityTypeBuilder<ChecklistTemplateItemEntity> builder)
    {
        builder.ToTable("items_plantilla_checklist"); builder.ConfigureBase();
        builder.Property(e => e.TemplateId).HasColumnName("plantilla_id"); builder.Property(e => e.SortOrder).HasColumnName("orden");
        builder.Property(e => e.ItemText).HasColumnName("texto").HasMaxLength(1000).IsRequired();
        builder.Property(e => e.Mandatory).HasColumnName("obligatorio"); builder.Property(e => e.ResponseTypeId).HasColumnName("tipo_respuesta_id");
        builder.Property(e => e.RequiresPhoto).HasColumnName("requiere_foto"); builder.Property(e => e.RequiresFile).HasColumnName("requiere_archivo");
        builder.Property(e => e.RequiresSignature).HasColumnName("requiere_firma"); builder.Property(e => e.IsActive).HasColumnName("activo");
        builder.HasOne(e => e.Template).WithMany(e => e.Items).HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ResponseType).WithMany().HasForeignKey(e => e.ResponseTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TemplateId, e.SortOrder }).IsUnique();
    }
}

public sealed class WorkOrderChecklistConfiguration : IEntityTypeConfiguration<WorkOrderChecklistEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderChecklistEntity> builder)
    {
        builder.ToTable("ot_checklists_sql"); builder.ConfigureBase();
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id"); builder.Property(e => e.TaskId).HasColumnName("tarea_id");
        builder.Property(e => e.TemplateId).HasColumnName("plantilla_id"); builder.Property(e => e.TemplateItemId).HasColumnName("item_plantilla_id");
        builder.Property(e => e.ItemText).HasColumnName("texto_item").HasMaxLength(1000).IsRequired(); builder.Property(e => e.Mandatory).HasColumnName("obligatorio");
        builder.Property(e => e.Completed).HasColumnName("completado"); builder.Property(e => e.CompletedAtUtc).HasColumnName("completado_at_utc").HasColumnType("timestamptz");
        builder.Property(e => e.CompletedByUserId).HasColumnName("completado_por_usuario_id").HasMaxLength(120); builder.Property(e => e.ResponseTypeId).HasColumnName("tipo_respuesta_id");
        builder.Property(e => e.Response).HasColumnName("respuesta").HasMaxLength(500); builder.Property(e => e.NumericValue).HasColumnName("valor_numerico").HasColumnType("numeric(12,2)");
        builder.Property(e => e.TextValue).HasColumnName("texto_libre").HasMaxLength(1000); builder.Property(e => e.EvidenceId).HasColumnName("evidencia_id"); builder.Property(e => e.SignatureId).HasColumnName("firma_id");
        builder.Property(e => e.RequiresPhoto).HasColumnName("requiere_foto"); builder.Property(e => e.RequiresFile).HasColumnName("requiere_archivo"); builder.Property(e => e.RequiresSignature).HasColumnName("requiere_firma"); builder.Property(e => e.IsActive).HasColumnName("vigente");
        builder.HasOne(e => e.WorkOrder).WithMany(e => e.Checklist).HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Task).WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Template).WithMany().HasForeignKey(e => e.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.TemplateItem).WithMany().HasForeignKey(e => e.TemplateItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.ResponseType).WithMany().HasForeignKey(e => e.ResponseTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Evidence).WithMany().HasForeignKey(e => e.EvidenceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Signature).WithMany().HasForeignKey(e => e.SignatureId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkOrderSignatureConfiguration : IEntityTypeConfiguration<WorkOrderSignatureEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderSignatureEntity> builder)
    {
        builder.ToTable("ot_firmas_sql"); builder.ConfigureBase();
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id"); builder.Property(e => e.TaskId).HasColumnName("tarea_id"); builder.Property(e => e.Scope).HasColumnName("alcance").HasMaxLength(80).IsRequired();
        builder.Property(e => e.SignerUserId).HasColumnName("firmante_usuario_id").HasMaxLength(120).IsRequired(); builder.Property(e => e.FileId).HasColumnName("archivo_id"); builder.Property(e => e.SignatureFileKey).HasColumnName("signature_file_key").HasMaxLength(300);
        builder.Property(e => e.SignedAtUtc).HasColumnName("firmado_at_utc").HasColumnType("timestamptz"); builder.Property(e => e.Comment).HasColumnName("comentario").HasMaxLength(1000); builder.Property(e => e.IsActive).HasColumnName("vigente");
        builder.HasOne(e => e.WorkOrder).WithMany(e => e.Signatures).HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Task).WithMany().HasForeignKey(e => e.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.File).WithMany().HasForeignKey(e => e.FileId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkOrderStatusHistoryConfiguration : IEntityTypeConfiguration<WorkOrderStatusHistoryEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderStatusHistoryEntity> builder)
    {
        builder.ToTable("ot_estado_historial_sql"); builder.ConfigureBase();
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id"); builder.Property(e => e.PreviousStatusId).HasColumnName("estado_anterior_id"); builder.Property(e => e.NewStatusId).HasColumnName("estado_nuevo_id");
        builder.Property(e => e.OccurredAtUtc).HasColumnName("fecha_utc").HasColumnType("timestamptz"); builder.Property(e => e.UserId).HasColumnName("usuario_id").HasMaxLength(120).IsRequired(); builder.Property(e => e.Reason).HasColumnName("motivo").HasMaxLength(500).IsRequired();
        builder.HasOne(e => e.WorkOrder).WithMany(e => e.History).HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.PreviousStatus).WithMany().HasForeignKey(e => e.PreviousStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.NewStatus).WithMany().HasForeignKey(e => e.NewStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.WorkOrderId, e.OccurredAtUtc });
    }
}

public sealed class DocumentWorkOrderConfiguration : IEntityTypeConfiguration<DocumentWorkOrderEntity>
{
    public void Configure(EntityTypeBuilder<DocumentWorkOrderEntity> builder)
    {
        builder.ToTable("documento_ordenes_trabajo"); builder.ConfigureBase();
        builder.Property(e => e.DocumentId).HasColumnName("documento_id"); builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id"); builder.Property(e => e.IsActive).HasColumnName("vigente");
        builder.Property(e => e.AssignedAtUtc).HasColumnName("asignado_at_utc").HasColumnType("timestamptz"); builder.Property(e => e.AssignedByUserId).HasColumnName("asignado_por_usuario_id").HasMaxLength(120);
        builder.Property(e => e.UnassignedAtUtc).HasColumnName("desasignado_at_utc").HasColumnType("timestamptz"); builder.Property(e => e.UnassignedByUserId).HasColumnName("desasignado_por_usuario_id").HasMaxLength(120); builder.Property(e => e.UnassignedReason).HasColumnName("motivo_desasignacion").HasMaxLength(500);
        builder.HasOne(e => e.Document).WithMany(e => e.WorkOrders).HasForeignKey(e => e.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.WorkOrder).WithMany().HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.DocumentId, e.WorkOrderId, e.IsActive }).IsUnique().HasFilter("vigente");
    }
}


public sealed class WorkOrderAssetConfiguration : IEntityTypeConfiguration<WorkOrderAssetEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderAssetEntity> builder)
    {
        builder.ToTable("orden_trabajo_activos"); builder.ConfigureBase();
        builder.Property(e => e.WorkOrderId).HasColumnName("orden_trabajo_id"); builder.Property(e => e.AssetId).HasColumnName("activo_id");
        builder.Property(e => e.Role).HasColumnName("rol").HasMaxLength(20).IsRequired(); builder.Property(e => e.AssetCodeSnapshot).HasColumnName("activo_codigo_snapshot").HasMaxLength(80).IsRequired(); builder.Property(e => e.AssetNameSnapshot).HasColumnName("activo_nombre_snapshot").HasMaxLength(240).IsRequired(); builder.Property(e => e.AddedAtUtc).HasColumnName("agregado_en_utc").HasColumnType("timestamptz"); builder.Property(e => e.AddedByUserId).HasColumnName("agregado_por_usuario_id").HasMaxLength(120).IsRequired();
        builder.HasIndex(e => new { e.WorkOrderId, e.AssetId }).IsUnique(); builder.HasIndex(e => new { e.WorkOrderId, e.Role });
        builder.HasOne(e => e.WorkOrder).WithMany(e => e.RelatedAssets).HasForeignKey(e => e.WorkOrderId).OnDelete(DeleteBehavior.Restrict); builder.HasOne(e => e.Asset).WithMany().HasForeignKey(e => e.AssetId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_orden_trabajo_activos_rol", "rol IN ('PRINCIPAL','AFECTADO','MONTAJE','DESMONTAJE')"));
    }
}

