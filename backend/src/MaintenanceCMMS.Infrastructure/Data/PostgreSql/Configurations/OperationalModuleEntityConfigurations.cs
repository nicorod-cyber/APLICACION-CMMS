using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql.Configurations;

public sealed class AvailabilityContractConfiguration : IEntityTypeConfiguration<AvailabilityContractEntity>
{
    public void Configure(EntityTypeBuilder<AvailabilityContractEntity> b)
    {
        b.ToTable("contratos_disponibilidad"); b.ConfigureBase();
        b.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired();
        b.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired();
        b.Property(x => x.Client).HasColumnName("cliente").HasMaxLength(240).IsRequired();
        b.Property(x => x.FaenaId).HasColumnName("faena_id");
        b.Property(x => x.CommittedHoursPerDay).HasColumnName("horas_comprometidas_dia").HasPrecision(12, 2);
        b.Property(x => x.TargetAvailability).HasColumnName("disponibilidad_objetivo").HasPrecision(5, 4);
        b.Property(x => x.StartsAtUtc).HasColumnName("fecha_inicio_utc").HasColumnType("timestamptz"); b.Property(x => x.EndsAtUtc).HasColumnName("fecha_fin_utc").HasColumnType("timestamptz");
        b.Property(x => x.ClientRules).HasColumnName("reglas_cliente").HasMaxLength(4000); b.Property(x => x.IsActive).HasColumnName("activo");
        b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        b.HasIndex(x => x.Code).IsUnique(); b.HasIndex(x => x.FaenaId);
        b.HasOne(x => x.Faena).WithMany().HasForeignKey(x => x.FaenaId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => { t.HasCheckConstraint("ck_contratos_disponibilidad_horas", "horas_comprometidas_dia > 0"); t.HasCheckConstraint("ck_contratos_disponibilidad_objetivo", "disponibilidad_objetivo >= 0 AND disponibilidad_objetivo <= 1"); t.HasCheckConstraint("ck_contratos_disponibilidad_fechas", "fecha_fin_utc IS NULL OR fecha_inicio_utc IS NULL OR fecha_fin_utc >= fecha_inicio_utc"); });
    }
}

public sealed class AvailabilityContractAssignmentConfiguration : IEntityTypeConfiguration<AvailabilityContractAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<AvailabilityContractAssignmentEntity> b)
    {
        b.ToTable("contrato_disponibilidad_objetivos"); b.ConfigureBase();
        b.Property(x => x.ContractId).HasColumnName("contrato_id"); b.Property(x => x.AssetId).HasColumnName("activo_id"); b.Property(x => x.OperationalUnitId).HasColumnName("unidad_operativa_id"); b.Property(x => x.Role).HasColumnName("rol");
        b.Property(x => x.StartsAtUtc).HasColumnName("fecha_inicio_utc").HasColumnType("timestamptz"); b.Property(x => x.EndsAtUtc).HasColumnName("fecha_fin_utc").HasColumnType("timestamptz"); b.Property(x => x.IsActive).HasColumnName("activo"); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.ContractId, x.AssetId, x.IsActive }).IsUnique().HasFilter("activo_id IS NOT NULL AND activo"); b.HasIndex(x => new { x.ContractId, x.OperationalUnitId, x.IsActive }).IsUnique().HasFilter("unidad_operativa_id IS NOT NULL AND activo");
        b.HasOne(x => x.Contract).WithMany(x => x.Assignments).HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.OperationalUnit).WithMany().HasForeignKey(x => x.OperationalUnitId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => { t.HasCheckConstraint("ck_contrato_disponibilidad_objetivos_un_objetivo", "(CASE WHEN activo_id IS NULL THEN 0 ELSE 1 END + CASE WHEN unidad_operativa_id IS NULL THEN 0 ELSE 1 END) = 1"); t.HasCheckConstraint("ck_contrato_disponibilidad_objetivos_fechas", "fecha_fin_utc IS NULL OR fecha_inicio_utc IS NULL OR fecha_fin_utc >= fecha_inicio_utc"); });
    }
}

public sealed class AvailabilityEventConfiguration : IEntityTypeConfiguration<AvailabilityEventEntity>
{
    public void Configure(EntityTypeBuilder<AvailabilityEventEntity> b)
    {
        b.ToTable("eventos_disponibilidad"); b.ConfigureBase();
        b.Property(x => x.ContractId).HasColumnName("contrato_id"); b.Property(x => x.ContractAssignmentId).HasColumnName("asignacion_contrato_id"); b.Property(x => x.AssetId).HasColumnName("activo_id"); b.Property(x => x.OperationalUnitId).HasColumnName("unidad_operativa_id"); b.Property(x => x.WorkOrderId).HasColumnName("orden_trabajo_id"); b.Property(x => x.Cause).HasColumnName("causa");
        b.Property(x => x.StartsAtUtc).HasColumnName("inicio_utc").HasColumnType("timestamptz"); b.Property(x => x.EndsAtUtc).HasColumnName("fin_utc").HasColumnType("timestamptz"); b.Property(x => x.CanBeUsed).HasColumnName("puede_utilizarse"); b.Property(x => x.IsMaintenanceAttributable).HasColumnName("atribuible_mantenimiento"); b.Property(x => x.Comment).HasColumnName("comentario").HasMaxLength(4000); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        b.HasIndex(x => new { x.ContractId, x.StartsAtUtc }); b.HasIndex(x => new { x.AssetId, x.StartsAtUtc }); b.HasIndex(x => new { x.OperationalUnitId, x.StartsAtUtc });
        b.HasOne(x => x.Contract).WithMany().HasForeignKey(x => x.ContractId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.ContractAssignment).WithMany().HasForeignKey(x => x.ContractAssignmentId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.OperationalUnit).WithMany().HasForeignKey(x => x.OperationalUnitId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => { t.HasCheckConstraint("ck_eventos_disponibilidad_fechas", "fin_utc IS NULL OR fin_utc >= inicio_utc"); t.HasCheckConstraint("ck_eventos_disponibilidad_objetivo", "activo_id IS NOT NULL OR unidad_operativa_id IS NOT NULL"); });
    }
}

public sealed class PreventivePlanConfiguration : IEntityTypeConfiguration<PreventivePlanEntity>
{
    public void Configure(EntityTypeBuilder<PreventivePlanEntity> b)
    {
        b.ToTable("planes_preventivos_sql"); b.ConfigureBase(); b.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(120).IsRequired(); b.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired(); b.Property(x => x.FrequencyType).HasColumnName("tipo_frecuencia");
        b.Property(x => x.FrequencyHours).HasColumnName("frecuencia_horas").HasPrecision(18, 2); b.Property(x => x.FrequencyKilometers).HasColumnName("frecuencia_km").HasPrecision(18, 2); b.Property(x => x.FrequencyDays).HasColumnName("frecuencia_dias"); b.Property(x => x.HourTolerance).HasColumnName("tolerancia_horas").HasPrecision(18, 2); b.Property(x => x.KilometerTolerance).HasColumnName("tolerancia_km").HasPrecision(18, 2); b.Property(x => x.DayTolerance).HasColumnName("tolerancia_dias"); b.Property(x => x.ChecklistTemplateId).HasColumnName("plantilla_checklist_id"); b.Property(x => x.SuggestedSpareParts).HasColumnName("repuestos_sugeridos").HasMaxLength(4000); b.Property(x => x.EstimatedLaborHours).HasColumnName("hh_estimadas").HasPrecision(12, 2);
        b.Property(x => x.StartsAtUtc).HasColumnName("fecha_inicio_utc").HasColumnType("timestamptz"); b.Property(x => x.LastExecutedAtUtc).HasColumnName("ultima_ejecucion_utc").HasColumnType("timestamptz"); b.Property(x => x.LastExecutedHours).HasColumnName("ultima_ejecucion_horas").HasPrecision(18, 2); b.Property(x => x.LastExecutedKilometers).HasColumnName("ultima_ejecucion_km").HasPrecision(18, 2); b.Property(x => x.NextDueAtUtc).HasColumnName("proxima_fecha_utc").HasColumnType("timestamptz"); b.Property(x => x.NextDueHours).HasColumnName("proxima_hora").HasPrecision(18, 2); b.Property(x => x.NextDueKilometers).HasColumnName("proximo_km").HasPrecision(18, 2); b.Property(x => x.IsActive).HasColumnName("activo"); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        b.HasIndex(x => x.Code).IsUnique(); b.HasIndex(x => new { x.IsActive, x.NextDueAtUtc }); b.HasOne(x => x.ChecklistTemplate).WithMany().HasForeignKey(x => x.ChecklistTemplateId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => { t.HasCheckConstraint("ck_planes_preventivos_hh", "hh_estimadas > 0"); t.HasCheckConstraint("ck_planes_preventivos_frecuencias", "frecuencia_horas IS NOT NULL OR frecuencia_km IS NOT NULL OR frecuencia_dias IS NOT NULL"); });
    }
}

public sealed class PreventivePlanScopeConfiguration : IEntityTypeConfiguration<PreventivePlanScopeEntity>
{
    public void Configure(EntityTypeBuilder<PreventivePlanScopeEntity> b)
    {
        b.ToTable("alcances_plan_preventivo"); b.ConfigureBase(); b.Property(x => x.PreventivePlanId).HasColumnName("plan_preventivo_id"); b.Property(x => x.AssetId).HasColumnName("activo_id"); b.Property(x => x.EquipmentFamilyId).HasColumnName("familia_equipo_id"); b.Property(x => x.AssetTypeId).HasColumnName("tipo_activo_id"); b.Property(x => x.OperationalUnitId).HasColumnName("unidad_operativa_id"); b.Property(x => x.Brand).HasColumnName("marca").HasMaxLength(120); b.Property(x => x.Model).HasColumnName("modelo").HasMaxLength(120);
        b.HasIndex(x => x.PreventivePlanId); b.HasOne(x => x.PreventivePlan).WithMany(x => x.Scopes).HasForeignKey(x => x.PreventivePlanId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.EquipmentFamily).WithMany().HasForeignKey(x => x.EquipmentFamilyId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.AssetType).WithMany().HasForeignKey(x => x.AssetTypeId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.OperationalUnit).WithMany().HasForeignKey(x => x.OperationalUnitId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_alcances_plan_preventivo_objetivo", "activo_id IS NOT NULL OR familia_equipo_id IS NOT NULL OR tipo_activo_id IS NOT NULL OR unidad_operativa_id IS NOT NULL"));
    }
}

public sealed class PreventiveEvaluationConfiguration : IEntityTypeConfiguration<PreventiveEvaluationEntity>
{
    public void Configure(EntityTypeBuilder<PreventiveEvaluationEntity> b)
    {
        b.ToTable("evaluaciones_preventivas"); b.ConfigureBase(); b.Property(x => x.PreventivePlanId).HasColumnName("plan_preventivo_id"); b.Property(x => x.AssetId).HasColumnName("activo_id"); b.Property(x => x.Status).HasColumnName("estado"); b.Property(x => x.EvaluatedAtUtc).HasColumnName("evaluado_at_utc").HasColumnType("timestamptz"); b.Property(x => x.CurrentHours).HasColumnName("horas_actuales").HasPrecision(18, 2); b.Property(x => x.CurrentKilometers).HasColumnName("km_actuales").HasPrecision(18, 2); b.Property(x => x.WorkOrderId).HasColumnName("orden_trabajo_id"); b.Property(x => x.EvaluatedByUserId).HasColumnName("evaluado_por_usuario_id").HasMaxLength(120).IsRequired();
        b.HasIndex(x => new { x.PreventivePlanId, x.AssetId, x.EvaluatedAtUtc }); b.HasOne(x => x.PreventivePlan).WithMany().HasForeignKey(x => x.PreventivePlanId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PreventiveHistoryConfiguration : IEntityTypeConfiguration<PreventiveHistoryEntity>
{
    public void Configure(EntityTypeBuilder<PreventiveHistoryEntity> b)
    {
        b.ToTable("historial_preventivo"); b.ConfigureBase(); b.Property(x => x.PreventivePlanId).HasColumnName("plan_preventivo_id"); b.Property(x => x.AssetId).HasColumnName("activo_id"); b.Property(x => x.PreviousStatus).HasColumnName("estado_anterior"); b.Property(x => x.NewStatus).HasColumnName("estado_nuevo"); b.Property(x => x.OccurredAtUtc).HasColumnName("fecha_utc").HasColumnType("timestamptz"); b.Property(x => x.UserId).HasColumnName("usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(1000).IsRequired(); b.Property(x => x.WorkOrderId).HasColumnName("orden_trabajo_id");
        b.HasIndex(x => new { x.PreventivePlanId, x.AssetId, x.OccurredAtUtc }); b.HasOne(x => x.PreventivePlan).WithMany().HasForeignKey(x => x.PreventivePlanId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkshopConfiguration : IEntityTypeConfiguration<WorkshopEntity>
{
    public void Configure(EntityTypeBuilder<WorkshopEntity> b)
    {
        b.ToTable("talleres"); b.ConfigureBase(); b.Property(x => x.Code).HasColumnName("codigo").HasMaxLength(80).IsRequired(); b.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired(); b.Property(x => x.FaenaId).HasColumnName("faena_id"); b.Property(x => x.DailyLaborCapacity).HasColumnName("capacidad_diaria_hh").HasPrecision(12, 2); b.Property(x => x.EquipmentCapacity).HasColumnName("capacidad_equipos"); b.Property(x => x.Schedule).HasColumnName("horario").HasMaxLength(500).IsRequired(); b.Property(x => x.Specialty).HasColumnName("especialidad").HasMaxLength(240).IsRequired(); b.Property(x => x.IsActive).HasColumnName("activo"); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        b.HasIndex(x => x.Code).IsUnique(); b.HasIndex(x => x.FaenaId); b.HasOne(x => x.Faena).WithMany().HasForeignKey(x => x.FaenaId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_talleres_capacidad_hh", "capacidad_diaria_hh >= 0"); t.HasCheckConstraint("ck_talleres_capacidad_equipos", "capacidad_equipos >= 0"); });
    }
}

public sealed class WorkOrderScheduleConfiguration : IEntityTypeConfiguration<WorkOrderScheduleEntity>
{
    public void Configure(EntityTypeBuilder<WorkOrderScheduleEntity> b)
    {
        b.ToTable("programaciones_ot"); b.ConfigureBase(); b.Property(x => x.WorkOrderId).HasColumnName("orden_trabajo_id"); b.Property(x => x.WorkshopId).HasColumnName("taller_id"); b.Property(x => x.StartsAtUtc).HasColumnName("inicio_utc").HasColumnType("timestamptz"); b.Property(x => x.EndsAtUtc).HasColumnName("fin_utc").HasColumnType("timestamptz"); b.Property(x => x.EstimatedLaborHours).HasColumnName("hh_estimadas").HasPrecision(12, 2); b.Property(x => x.TechnicianUserId).HasColumnName("tecnico_usuario_id").HasMaxLength(120); b.Property(x => x.Status).HasColumnName("estado"); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        b.HasIndex(x => x.WorkOrderId).IsUnique(); b.HasIndex(x => new { x.WorkshopId, x.StartsAtUtc }); b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => { t.HasCheckConstraint("ck_programaciones_ot_fechas", "fin_utc > inicio_utc"); t.HasCheckConstraint("ck_programaciones_ot_hh", "hh_estimadas > 0"); });
    }
}

public sealed class ScheduleDependencyConfiguration : IEntityTypeConfiguration<ScheduleDependencyEntity>
{
    public void Configure(EntityTypeBuilder<ScheduleDependencyEntity> b)
    {
        b.ToTable("dependencias_programacion"); b.ConfigureBase(); b.Property(x => x.PredecessorScheduleId).HasColumnName("programacion_predecesora_id"); b.Property(x => x.SuccessorScheduleId).HasColumnName("programacion_sucesora_id"); b.Property(x => x.Type).HasColumnName("tipo").HasMaxLength(40).IsRequired(); b.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(1000); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired(); b.HasIndex(x => new { x.PredecessorScheduleId, x.SuccessorScheduleId }).IsUnique(); b.HasOne(x => x.PredecessorSchedule).WithMany().HasForeignKey(x => x.PredecessorScheduleId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.SuccessorSchedule).WithMany().HasForeignKey(x => x.SuccessorScheduleId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => t.HasCheckConstraint("ck_dependencias_programacion_distintas", "programacion_predecesora_id <> programacion_sucesora_id"));
    }
}

public sealed class ScheduleAlertConfiguration : IEntityTypeConfiguration<ScheduleAlertEntity>
{
    public void Configure(EntityTypeBuilder<ScheduleAlertEntity> b)
    {
        b.ToTable("alertas_programacion"); b.ConfigureBase(); b.Property(x => x.Type).HasColumnName("tipo"); b.Property(x => x.Severity).HasColumnName("severidad").HasMaxLength(40).IsRequired(); b.Property(x => x.Message).HasColumnName("mensaje").HasMaxLength(2000).IsRequired(); b.Property(x => x.WorkshopId).HasColumnName("taller_id"); b.Property(x => x.WorkOrderId).HasColumnName("orden_trabajo_id"); b.Property(x => x.FaenaId).HasColumnName("faena_id"); b.Property(x => x.IsResolved).HasColumnName("resuelta"); b.Property(x => x.RaisedAtUtc).HasColumnName("creada_at_utc").HasColumnType("timestamptz"); b.HasIndex(x => new { x.IsResolved, x.RaisedAtUtc }); b.HasOne(x => x.Workshop).WithMany().HasForeignKey(x => x.WorkshopId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Faena).WithMany().HasForeignKey(x => x.FaenaId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<SupplierEntity>
{
    public void Configure(EntityTypeBuilder<SupplierEntity> b)
    {
        b.ToTable("proveedores"); b.ConfigureBase(); b.Property(x => x.TaxId).HasColumnName("rut").HasMaxLength(40).IsRequired(); b.Property(x => x.Name).HasColumnName("nombre").HasMaxLength(240).IsRequired(); b.Property(x => x.Contact).HasColumnName("contacto").HasMaxLength(240); b.Property(x => x.Email).HasColumnName("email").HasMaxLength(240); b.Property(x => x.Phone).HasColumnName("telefono").HasMaxLength(80); b.Property(x => x.Address).HasColumnName("direccion").HasMaxLength(500); b.Property(x => x.ExpectedLeadTimeDays).HasColumnName("lead_time_esperado_dias"); b.Property(x => x.IsActive).HasColumnName("activo"); b.Property(x => x.Notes).HasColumnName("observaciones").HasMaxLength(2000); b.HasIndex(x => x.TaxId).IsUnique(); b.ToTable(t => t.HasCheckConstraint("ck_proveedores_lead_time", "lead_time_esperado_dias >= 0"));
    }
}

public sealed class ProcurementRequestConfiguration : IEntityTypeConfiguration<ProcurementRequestEntity>
{
    public void Configure(EntityTypeBuilder<ProcurementRequestEntity> b)
    {
        b.ToTable("solicitudes_abastecimiento"); b.ConfigureBase(); b.Property(x => x.RequestNumber).HasColumnName("numero_solicitud").HasMaxLength(40).IsRequired(); b.Property(x => x.Status).HasColumnName("estado"); b.Property(x => x.MaterialRequestId).HasColumnName("solicitud_material_id"); b.Property(x => x.FaenaId).HasColumnName("faena_id"); b.Property(x => x.WarehouseId).HasColumnName("bodega_id"); b.Property(x => x.WorkOrderId).HasColumnName("orden_trabajo_id"); b.Property(x => x.AssetId).HasColumnName("activo_id"); b.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(2000).IsRequired(); b.Property(x => x.TechnicalRequestedAtUtc).HasColumnName("solicitada_tecnica_at_utc").HasColumnType("timestamptz"); b.Property(x => x.MaintenanceApprovedAtUtc).HasColumnName("aprobada_mantenimiento_at_utc").HasColumnType("timestamptz"); b.Property(x => x.SentToProcurementAtUtc).HasColumnName("enviada_abastecimiento_at_utc").HasColumnType("timestamptz"); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.UpdatedByUserId).HasColumnName("actualizado_por_usuario_id").HasMaxLength(120);
        b.HasIndex(x => x.RequestNumber).IsUnique(); b.HasIndex(x => new { x.Status, x.SentToProcurementAtUtc }); b.HasIndex(x => x.FaenaId); b.HasOne(x => x.MaterialRequest).WithMany().HasForeignKey(x => x.MaterialRequestId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Faena).WithMany().HasForeignKey(x => x.FaenaId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.WorkOrder).WithMany().HasForeignKey(x => x.WorkOrderId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.AssetId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementRequestLineConfiguration : IEntityTypeConfiguration<ProcurementRequestLineEntity>
{
    public void Configure(EntityTypeBuilder<ProcurementRequestLineEntity> b)
    {
        b.ToTable("detalle_solicitud_abastecimiento"); b.ConfigureBase(); b.Property(x => x.ProcurementRequestId).HasColumnName("solicitud_abastecimiento_id"); b.Property(x => x.SparePartId).HasColumnName("repuesto_id"); b.Property(x => x.ExternalRequestNumber).HasColumnName("numero_solicitud_externa").HasMaxLength(120); b.Property(x => x.Description).HasColumnName("descripcion").HasMaxLength(2000).IsRequired(); b.Property(x => x.RequestedQuantity).HasColumnName("cantidad_solicitada").HasPrecision(18, 2); b.Property(x => x.ReceivedQuantity).HasColumnName("cantidad_recibida").HasPrecision(18, 2); b.Property(x => x.DeliveredQuantity).HasColumnName("cantidad_entregada").HasPrecision(18, 2); b.Property(x => x.Unit).HasColumnName("unidad").HasMaxLength(40).IsRequired(); b.Property(x => x.EstimatedCost).HasColumnName("costo_estimado").HasPrecision(18, 2); b.Property(x => x.Currency).HasColumnName("moneda").HasMaxLength(10).IsRequired(); b.Property(x => x.SupportingDocumentUrl).HasColumnName("documento_respaldo_url").HasMaxLength(2000); b.Property(x => x.Notes).HasColumnName("observaciones").HasMaxLength(2000); b.HasIndex(x => x.ProcurementRequestId); b.HasOne(x => x.ProcurementRequest).WithMany(x => x.Lines).HasForeignKey(x => x.ProcurementRequestId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.SparePart).WithMany().HasForeignKey(x => x.SparePartId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => t.HasCheckConstraint("ck_detalle_solicitud_abastecimiento_cantidades", "cantidad_solicitada > 0 AND cantidad_recibida >= 0 AND cantidad_entregada >= 0 AND cantidad_entregada <= cantidad_recibida"));
    }
}

public sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrderEntity>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderEntity> b)
    {
        b.ToTable("ordenes_compra"); b.ConfigureBase(); b.Property(x => x.PurchaseOrderNumber).HasColumnName("numero_oc").HasMaxLength(80).IsRequired(); b.Property(x => x.ProcurementRequestId).HasColumnName("solicitud_abastecimiento_id"); b.Property(x => x.SupplierId).HasColumnName("proveedor_id"); b.Property(x => x.OrderedAtUtc).HasColumnName("fecha_oc_utc").HasColumnType("timestamptz"); b.Property(x => x.PromisedAtUtc).HasColumnName("fecha_comprometida_utc").HasColumnType("timestamptz"); b.Property(x => x.Cost).HasColumnName("costo_oc").HasPrecision(18, 2); b.Property(x => x.Currency).HasColumnName("moneda").HasMaxLength(10).IsRequired(); b.Property(x => x.DocumentUrl).HasColumnName("documento_oc_url").HasMaxLength(2000); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(1000).IsRequired(); b.HasIndex(x => x.PurchaseOrderNumber).IsUnique(); b.HasIndex(x => x.ProcurementRequestId); b.HasOne(x => x.ProcurementRequest).WithMany(x => x.PurchaseOrders).HasForeignKey(x => x.ProcurementRequestId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => t.HasCheckConstraint("ck_ordenes_compra_fechas", "fecha_comprometida_utc >= fecha_oc_utc"));
    }
}

public sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLineEntity>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLineEntity> b)
    {
        b.ToTable("detalle_orden_compra"); b.ConfigureBase(); b.Property(x => x.PurchaseOrderId).HasColumnName("orden_compra_id"); b.Property(x => x.ProcurementRequestLineId).HasColumnName("detalle_solicitud_id"); b.Property(x => x.Quantity).HasColumnName("cantidad").HasPrecision(18, 2); b.Property(x => x.UnitCost).HasColumnName("costo_unitario").HasPrecision(18, 2); b.HasIndex(x => new { x.PurchaseOrderId, x.ProcurementRequestLineId }).IsUnique(); b.HasOne(x => x.PurchaseOrder).WithMany(x => x.Lines).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.ProcurementRequestLine).WithMany().HasForeignKey(x => x.ProcurementRequestLineId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => t.HasCheckConstraint("ck_detalle_orden_compra_cantidad", "cantidad > 0"));
    }
}

public sealed class ProcurementReceiptConfiguration : IEntityTypeConfiguration<ProcurementReceiptEntity>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptEntity> b)
    {
        b.ToTable("recepciones_abastecimiento"); b.ConfigureBase(); b.Property(x => x.ProcurementRequestId).HasColumnName("solicitud_abastecimiento_id"); b.Property(x => x.PurchaseOrderId).HasColumnName("orden_compra_id"); b.Property(x => x.WarehouseId).HasColumnName("bodega_id"); b.Property(x => x.ReceivedAtUtc).HasColumnName("fecha_recepcion_utc").HasColumnType("timestamptz"); b.Property(x => x.DirectDispatchToWorkOrder).HasColumnName("despacho_directo_ot"); b.Property(x => x.ReceptionMovementId).HasColumnName("movimiento_recepcion_id"); b.Property(x => x.DeliveryMovementId).HasColumnName("movimiento_entrega_id"); b.Property(x => x.ActualCost).HasColumnName("costo_real").HasPrecision(18, 2); b.Property(x => x.ReceptionDocumentUrl).HasColumnName("documento_recepcion_url").HasMaxLength(2000); b.Property(x => x.DeliveryDocumentUrl).HasColumnName("documento_entrega_url").HasMaxLength(2000); b.Property(x => x.CreatedByUserId).HasColumnName("creado_por_usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.Reason).HasColumnName("motivo").HasMaxLength(1000).IsRequired(); b.HasIndex(x => new { x.ProcurementRequestId, x.ReceivedAtUtc }); b.HasOne(x => x.ProcurementRequest).WithMany(x => x.Receipts).HasForeignKey(x => x.ProcurementRequestId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.ReceptionMovement).WithMany().HasForeignKey(x => x.ReceptionMovementId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.DeliveryMovement).WithMany().HasForeignKey(x => x.DeliveryMovementId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ProcurementReceiptLineConfiguration : IEntityTypeConfiguration<ProcurementReceiptLineEntity>
{
    public void Configure(EntityTypeBuilder<ProcurementReceiptLineEntity> b)
    {
        b.ToTable("detalle_recepcion_abastecimiento"); b.ConfigureBase(); b.Property(x => x.ProcurementReceiptId).HasColumnName("recepcion_id"); b.Property(x => x.ProcurementRequestLineId).HasColumnName("detalle_solicitud_id"); b.Property(x => x.ReceivedQuantity).HasColumnName("cantidad_recibida").HasPrecision(18, 2); b.Property(x => x.DeliveredQuantity).HasColumnName("cantidad_entregada").HasPrecision(18, 2); b.HasIndex(x => new { x.ProcurementReceiptId, x.ProcurementRequestLineId }).IsUnique(); b.HasOne(x => x.ProcurementReceipt).WithMany(x => x.Lines).HasForeignKey(x => x.ProcurementReceiptId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x => x.ProcurementRequestLine).WithMany().HasForeignKey(x => x.ProcurementRequestLineId).OnDelete(DeleteBehavior.Restrict); b.ToTable(t => t.HasCheckConstraint("ck_detalle_recepcion_abastecimiento_cantidades", "cantidad_recibida >= 0 AND cantidad_entregada >= 0 AND cantidad_entregada <= cantidad_recibida"));
    }
}

public sealed class ImportConfiguration : IEntityTypeConfiguration<ImportEntity>
{
    public void Configure(EntityTypeBuilder<ImportEntity> b)
    {
        b.ToTable("importaciones"); b.ConfigureBase(); b.Property(x => x.EntityName).HasColumnName("entidad").HasMaxLength(120).IsRequired(); b.Property(x => x.SchemaName).HasColumnName("esquema").HasMaxLength(120).IsRequired(); b.Property(x => x.OriginalFileName).HasColumnName("archivo_original").HasMaxLength(300).IsRequired(); b.Property(x => x.FileId).HasColumnName("archivo_id"); b.Property(x => x.SimulateOnly).HasColumnName("solo_simulacion"); b.Property(x => x.Status).HasColumnName("estado"); b.Property(x => x.UploadedByUserId).HasColumnName("cargado_por_usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.UploadedAtUtc).HasColumnName("cargado_at_utc").HasColumnType("timestamptz"); b.Property(x => x.AppliedAtUtc).HasColumnName("aplicado_at_utc").HasColumnType("timestamptz"); b.Property(x => x.AppliedByUserId).HasColumnName("aplicado_por_usuario_id").HasMaxLength(120); b.Property(x => x.RejectedAtUtc).HasColumnName("rechazado_at_utc").HasColumnType("timestamptz"); b.Property(x => x.RejectedByUserId).HasColumnName("rechazado_por_usuario_id").HasMaxLength(120); b.Property(x => x.RejectReason).HasColumnName("motivo_rechazo").HasMaxLength(2000); b.HasIndex(x => new { x.Status, x.UploadedAtUtc }); b.HasOne(x => x.File).WithMany().HasForeignKey(x => x.FileId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ImportRowConfiguration : IEntityTypeConfiguration<ImportRowEntity>
{
    public void Configure(EntityTypeBuilder<ImportRowEntity> b)
    {
        b.ToTable("filas_importacion"); b.ConfigureBase(); b.Property(x => x.ImportId).HasColumnName("importacion_id"); b.Property(x => x.RowNumber).HasColumnName("numero_fila"); b.Property(x => x.Operation).HasColumnName("operacion").HasMaxLength(40).IsRequired(); b.Property(x => x.InputSnapshot).HasColumnName("snapshot_entrada").HasColumnType("jsonb").IsRequired(); b.HasIndex(x => new { x.ImportId, x.RowNumber }).IsUnique(); b.HasOne(x => x.Import).WithMany(x => x.Rows).HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ImportErrorConfiguration : IEntityTypeConfiguration<ImportErrorEntity>
{
    public void Configure(EntityTypeBuilder<ImportErrorEntity> b)
    {
        b.ToTable("errores_importacion"); b.ConfigureBase(); b.Property(x => x.ImportId).HasColumnName("importacion_id"); b.Property(x => x.RowNumber).HasColumnName("numero_fila"); b.Property(x => x.ColumnName).HasColumnName("columna").HasMaxLength(120); b.Property(x => x.Message).HasColumnName("mensaje").HasMaxLength(2000).IsRequired(); b.HasIndex(x => x.ImportId); b.HasOne(x => x.Import).WithMany(x => x.Errors).HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ImportEventConfiguration : IEntityTypeConfiguration<ImportEventEntity>
{
    public void Configure(EntityTypeBuilder<ImportEventEntity> b)
    {
        b.ToTable("eventos_importacion"); b.ConfigureBase(); b.Property(x => x.ImportId).HasColumnName("importacion_id"); b.Property(x => x.Status).HasColumnName("estado"); b.Property(x => x.UserId).HasColumnName("usuario_id").HasMaxLength(120).IsRequired(); b.Property(x => x.OccurredAtUtc).HasColumnName("fecha_utc").HasColumnType("timestamptz"); b.Property(x => x.Detail).HasColumnName("detalle").HasMaxLength(2000); b.HasIndex(x => new { x.ImportId, x.OccurredAtUtc }); b.HasOne(x => x.Import).WithMany(x => x.Events).HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Restrict);
    }
}
