using System.Data;
using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MaintenanceCMMS.Infrastructure.WorkOrders;

public sealed class WorkOrderService : IWorkOrderService
{
    private readonly CmmsDbContext _dbContext;
    private readonly IAuditService _auditService;

    public WorkOrderService(CmmsDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<IReadOnlyCollection<WorkOrderSummaryResponse>> ListAsync(WorkOrderQuery query, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var orders = await BaseQuery().AsNoTracking().ToArrayAsync(cancellationToken);
        return orders.Select(ToDetail)
            .Where(d => query.IncludeClosed || d.Summary.Estado is not (WorkOrderLifecycleStatus.ValidadaPlanificacion or WorkOrderLifecycleStatus.Anulada))
            .Where(d => !query.Status.HasValue || d.Summary.Estado == query.Status)
            .Where(d => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(d.Summary.FaenaCodigo, query.FaenaCodigo))
            .Where(d => string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(d.Summary.ActivoCodigo, query.ActivoCodigo) || d.Summary.ActivosRelacionados.Any(a => Same(a.ActivoCodigo, query.ActivoCodigo)))
            .Where(d => string.IsNullOrWhiteSpace(query.UnidadOperativaCodigo) || Same(d.Summary.UnidadOperativaCodigo, query.UnidadOperativaCodigo))
            .Where(d => string.IsNullOrWhiteSpace(query.TechnicianId) || d.Technicians.Any(t => Same(t.TecnicoUserId, query.TechnicianId)))
            .Where(d => CanViewOrder(user, d))
            .Select(d => d.Summary)
            .OrderByDescending(x => x.FechaProgramada ?? x.FechaInicioProgramada ?? DateTimeOffset.MinValue)
            .ThenBy(x => x.NumeroOT, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<WorkOrderDetailResponse?> GetByIdAsync(string numeroOt, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var order = await FindAsync(numeroOt, false, cancellationToken);
        if (order is null) return null;
        var detail = ToDetail(order);
        if (!CanViewOrder(user, detail)) throw new UnauthorizedAccessException("No tiene acceso a la OT solicitada.");
        return detail;
    }

    public async Task<WorkOrderDetailResponse> CreateAsync(CreateWorkOrderRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanPlan(user); ValidateRequired(request.Descripcion, nameof(request.Descripcion)); ValidateRequired(request.TipoMantenimiento, nameof(request.TipoMantenimiento));
        if (string.IsNullOrWhiteSpace(request.ActivoCodigo) && string.IsNullOrWhiteSpace(request.UnidadOperativaCodigo)) throw new DomainException("La OT debe tener un activo principal o una unidad operativa.");
        var asset = string.IsNullOrWhiteSpace(request.ActivoCodigo) ? null : await ResolveAssetAsync(request.ActivoCodigo, request.FaenaCodigo, user, ct);
        var unit = string.IsNullOrWhiteSpace(request.UnidadOperativaCodigo) ? null : await ResolveUnitAsync(request.UnidadOperativaCodigo, request.FaenaCodigo, user, ct);
        var faenaCode = request.FaenaCodigo ?? asset?.Faena?.Code ?? unit?.Faena?.Code;
        ValidateRequired(faenaCode, "FaenaCodigo");
        var faena = await _dbContext.Faenas.FirstOrDefaultAsync(x => x.Code == C(faenaCode) && x.IsActive, ct) ?? throw new DomainException("La faena indicada no existe.");
        EnsureFaena(user, faena.Code);
        if ((asset?.FaenaId is Guid assetFaena && assetFaena != faena.Id) || (unit?.FaenaId is Guid unitFaena && unitFaena != faena.Id)) throw new DomainException("Los objetivos de la OT deben pertenecer a la faena indicada.");
        var related = new List<(AssetEntity Asset, string Role)>();
        if (asset is not null) related.Add((asset, "PRINCIPAL"));
        foreach (var target in request.ActivosRelacionados ?? [])
        {
            ValidateRequired(target.ActivoCodigo, "ActivosRelacionados.ActivoCodigo");
            var relatedAsset = await ResolveAssetAsync(target.ActivoCodigo, faena.Code, user, ct);
            if (related.Any(x => x.Asset.Id == relatedAsset.Id)) throw new DomainException("Un activo solo puede asociarse una vez a la OT.");
            related.Add((relatedAsset, TargetRole(target.Rol)));
        }
        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow; var status = await CatalogAsync("WorkOrderLifecycleStatus", WorkOrderLifecycleStatus.OTCreada.ToString(), ct);
        var order = new WorkOrderEntity
        {
            Id = Guid.NewGuid(), WorkOrderNumber = await NextNumberAsync("work_order_number_seq", "OT", ct), AssetId = asset?.Id, Asset = asset, OperationalUnitId = unit?.Id, OperationalUnit = unit, FaenaId = faena.Id, Faena = faena,
            StatusId = status.Id, Status = status, MaintenanceTypeId = (await CatalogAsync("MaintenanceType", request.TipoMantenimiento, ct)).Id,
            Description = request.Descripcion.Trim(), NotificationId = await ResolveNotificationIdAsync(request.AvisoId, ct), System = N(request.Sistema), Subsystem = N(request.Subsistema), Component = N(request.Componente),
            PriorityId = (await CatalogAsync("WorkNotificationPriority", N(request.Prioridad) ?? "Media", ct)).Id, CriticalityId = (await CatalogAsync("WorkNotificationCriticality", N(request.Criticidad) ?? "Media", ct)).Id,
            ScheduledAtUtc = request.FechaProgramada, ScheduledStartUtc = request.FechaInicioProgramada, ScheduledEndUtc = request.FechaFinProgramada,
            RequiresSignature = request.RequiereFirma, CreatedByUserId = user.UserId, CreatedByUserAtUtc = now, UpdatedByUserId = user.UserId, UpdatedByUserAtUtc = now
        };
        _dbContext.WorkOrders.Add(order);
        foreach (var target in related) _dbContext.WorkOrderAssets.Add(new WorkOrderAssetEntity { Id = Guid.NewGuid(), WorkOrder = order, WorkOrderId = order.Id, Asset = target.Asset, AssetId = target.Asset.Id, Role = target.Role, AssetCodeSnapshot = target.Asset.Code, AssetNameSnapshot = target.Asset.Name, AddedAtUtc = now, AddedByUserId = user.UserId });
        AddHistory(order, status, status, user, "OT creada", now);
        await _dbContext.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await Audit(user, "work_order.created", order.WorkOrderNumber, null, order, faena.Code, request.Descripcion, ct);
        return (await GetByIdAsync(order.WorkOrderNumber, user, ct))!;
    }

    private static string TargetRole(string? value)
    {
        var role = C(value) ?? "AFECTADO";
        return role is "AFECTADO" or "MONTAJE" or "DESMONTAJE" ? role : throw new DomainException("El rol del activo relacionado es invalido.");
    }
public async Task<WorkOrderDetailResponse> CreatePreventiveAsync(CreatePreventiveWorkOrderRequest r, UserAccessContext u, CancellationToken ct)
    {
        var d = await CreateAsync(new CreateWorkOrderRequest(r.ActivoCodigo, r.Descripcion, "Preventive", r.FaenaCodigo, null, r.Sistema, r.Subsistema, r.Componente, "Media", "Media", r.FechaProgramada, r.FechaInicioProgramada, r.FechaFinProgramada, r.RequiereFirma), u, ct);
        var e = await FindAsync(d.Summary.NumeroOT, true, ct); e!.PreventivePlanCode = N(r.PlanPreventivoCodigo); e.IsAutomaticPreventive = true; await _dbContext.SaveChangesAsync(ct);
        return (await GetByIdAsync(d.Summary.NumeroOT, u, ct))!;
    }

    public async Task<WorkOrderTaskResponse?> AddTaskAsync(string numeroOt, CreateWorkOrderTaskRequest r, UserAccessContext u, CancellationToken ct)
    {
        EnsureCanPlan(u); ValidateRequired(r.Descripcion, nameof(r.Descripcion)); var o = await MutOrder(numeroOt, u, ct); EnsureOpen(o);
        var code = C(r.CodigoTarea) ?? await NextTaskCodeAsync(o.Id, ct); if (o.Tasks.Any(t => t.IsActive && Same(t.TaskCode, code))) throw new DomainException($"La tarea '{code}' ya existe en la OT.");
        var t = new WorkOrderTaskEntity { Id = Guid.NewGuid(), WorkOrderId = o.Id, TaskCode = code, Description = r.Descripcion.Trim(), ScheduledStartUtc = r.FechaInicioProgramada, ScheduledEndUtc = r.FechaFinProgramada, RequiresEvidence = r.RequiereEvidencia, RequiresLabor = r.RequiereHH, ChecklistMandatory = r.ChecklistObligatorio, Observations = N(r.Observaciones) };
        _dbContext.WorkOrderTasks.Add(t); await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.task_created", numeroOt, null, t, o.Faena.Code, r.Descripcion, ct); return ToTask(o.WorkOrderNumber, t);
    }

    public async Task<WorkOrderTaskTechnicianResponse?> AssignTechnicianAsync(string numeroOt, string codigoTarea, AssignTaskTechnicianRequest r, UserAccessContext u, CancellationToken ct)
    {
        EnsureCanPlan(u); ValidateRequired(r.TecnicoUserId, nameof(r.TecnicoUserId)); var o = await MutOrder(numeroOt, u, ct); var task = Task(o, codigoTarea);
        if (o.Technicians.Any(t => t.IsActive && t.TaskId == task.Id && Same(t.TechnicianUserId, r.TecnicoUserId))) throw new DomainException("El tecnico ya esta asignado a esta tarea.");
        var e = new WorkOrderTaskTechnicianEntity { Id = Guid.NewGuid(), WorkOrderId = o.Id, TaskId = task.Id, TechnicianUserId = r.TecnicoUserId.Trim(), TechnicianDisplayName = N(r.TecnicoNombre), AssignedAtUtc = DateTimeOffset.UtcNow, AssignedByUserId = u.UserId };
        _dbContext.WorkOrderTaskTechnicians.Add(e); await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.technician_assigned", numeroOt, null, e, o.Faena.Code, r.TecnicoUserId, ct); return ToTech(o.WorkOrderNumber, task.TaskCode, e);
    }

    public async Task<WorkOrderLaborResponse?> RegisterLaborAsync(string numeroOt, string codigoTarea, RegisterLaborRequest r, UserAccessContext u, CancellationToken ct)
    {
        ValidateRequired(r.TecnicoUserId, nameof(r.TecnicoUserId)); ValidateRequired(r.Descripcion, nameof(r.Descripcion)); var h = Hours(r.Horas, r.HoraInicio, r.HoraTermino); if (h <= 0) throw new DomainException("Las HH deben ser mayores a cero.");
        var o = await MutOrder(numeroOt, u, ct); var task = Task(o, codigoTarea); EnsureTechCanWork(u, ToDetail(o), codigoTarea, r.TecnicoUserId);
        var e = new WorkOrderLaborEntity { Id = Guid.NewGuid(), WorkOrderId = o.Id, TaskId = task.Id, TechnicianUserId = r.TecnicoUserId.Trim(), Hours = h, Description = r.Descripcion.Trim(), WorkDateUtc = r.FechaTrabajo ?? r.HoraInicio ?? DateTimeOffset.UtcNow, StartTimeUtc = r.HoraInicio, EndTimeUtc = r.HoraTermino, RegisteredByUserId = u.UserId, Comment = N(r.Comentario) };
        _dbContext.WorkOrderLabor.Add(e); await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.labor_registered", numeroOt, null, e, o.Faena.Code, r.Descripcion, ct); return ToLabor(o.WorkOrderNumber, task.TaskCode, e);
    }

    public async Task<WorkOrderLaborResponse?> ValidateLaborAsync(string numeroOt, string hhId, ValidateLaborRequest r, UserAccessContext u, CancellationToken ct)
    {
        ValidateRequired(r.Reason, nameof(r.Reason)); EnsureCanPlan(u); var o = await MutOrder(numeroOt, u, ct); if (!Guid.TryParse(hhId, out var id)) return null;
        var e = o.Labor.FirstOrDefault(x => x.Id == id && x.IsActive); if (e is null) return null; e.SupervisorValidated = r.Validado; e.ValidatedByUserId = r.Validado ? u.UserId : null; e.ValidatedAtUtc = r.Validado ? DateTimeOffset.UtcNow : null; e.Comment = Append(e.Comment, r.Reason); e.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.labor_validated", numeroOt, null, e, o.Faena.Code, r.Reason, ct); return ToLabor(o.WorkOrderNumber, e.Task.TaskCode, e);
    }

    public async Task<WorkOrderEvidenceResponse?> RegisterEvidenceAsync(string numeroOt, RegisterEvidenceRequest r, UserAccessContext u, CancellationToken ct)
    {
        ValidateRequired(r.Nombre, nameof(r.Nombre)); if (r.TipoEvidencia != WorkOrderEvidenceType.Comentario && string.IsNullOrWhiteSpace(r.ArchivoKey) && string.IsNullOrWhiteSpace(r.SharePointUrl) && string.IsNullOrWhiteSpace(r.LocalPath)) throw new DomainException("Debe indicar ArchivoKey, SharePointUrl o LocalPath para la evidencia.");
        var o = await MutOrder(numeroOt, u, ct); WorkOrderTaskEntity? task = null; if (!string.IsNullOrWhiteSpace(r.CodigoTarea)) { task = Task(o, r.CodigoTarea); EnsureTechCanWork(u, ToDetail(o), r.CodigoTarea, u.UserId); } else EnsurePlanSupervisorOrAssigned(u, ToDetail(o));
        var e = new WorkOrderEvidenceEntity { Id = Guid.NewGuid(), WorkOrderId = o.Id, TaskId = task?.Id, Name = r.Nombre.Trim(), EvidenceTypeId = (await CatalogAsync("WorkOrderEvidenceType", r.TipoEvidencia.ToString(), ct)).Id, IsPhoto = r.EsFoto || r.TipoEvidencia is WorkOrderEvidenceType.FotoAntes or WorkOrderEvidenceType.FotoDespues, IsMandatory = r.EsObligatoria, CoversMandatoryEvidence = r.CubreEvidenciaObligatoria, StorageProvider = N(r.StorageProvider) ?? Infer(r), ExternalKey = N(r.ArchivoKey), ExternalUri = N(r.SharePointUrl), LocalPath = N(r.LocalPath), OfflineId = N(r.OfflineId), SyncStatus = N(r.SyncStatus) ?? "Synced", Observations = N(r.Observaciones), CreatedByUserId = u.UserId, CreatedByUserAtUtc = DateTimeOffset.UtcNow };
        _dbContext.WorkOrderEvidences.Add(e); await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.evidence_registered", numeroOt, null, e, o.Faena.Code, r.Nombre, ct); return ToEvidence(o.WorkOrderNumber, task?.TaskCode, e);
    }

    public async Task<WorkOrderSparePartResponse?> AddSparePartAsync(string numeroOt, AddWorkOrderSparePartRequest r, UserAccessContext u, CancellationToken ct)
    {
        EnsureCanPlan(u); ValidateRequired(r.CodigoTarea, nameof(r.CodigoTarea)); ValidateRequired(r.RepuestoCodigo, nameof(r.RepuestoCodigo)); ValidateRequired(r.Unidad, nameof(r.Unidad)); if (r.Cantidad <= 0) throw new DomainException("La cantidad de repuesto debe ser mayor a cero.");
        var o = await MutOrder(numeroOt, u, ct); var task = Task(o, r.CodigoTarea); var e = new WorkOrderSparePartEntity { Id = Guid.NewGuid(), WorkOrderId = o.Id, TaskId = task.Id, SparePartCode = C(r.RepuestoCodigo)!, Quantity = r.Cantidad, Unit = r.Unidad.Trim(), WarehouseCode = C(r.BodegaCodigo), StatusId = (await CatalogAsync("WorkOrderSparePartStatus", r.Estado.ToString(), ct)).Id, Observations = N(r.Observaciones) };
        _dbContext.WorkOrderSpareParts.Add(e); await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.spare_part_added", numeroOt, null, e, o.Faena.Code, r.RepuestoCodigo, ct); return ToSpare(o.WorkOrderNumber, task.TaskCode, e);
    }

    public async Task<WorkOrderSparePartResponse?> UpdateSparePartUsageAsync(string numeroOt, string itemId, UpdateWorkOrderSparePartUsageRequest r, UserAccessContext u, CancellationToken ct)
    {
        ValidateRequired(r.Reason, nameof(r.Reason)); var o = await MutOrder(numeroOt, u, ct); if (!Guid.TryParse(itemId, out var id)) return null; var e = o.SpareParts.FirstOrDefault(x => x.Id == id && x.IsActive); if (e is null) return null;
        e.StatusId = (await CatalogAsync("WorkOrderSparePartStatus", r.Estado.ToString(), ct)).Id; e.UsedQuantity = r.CantidadUtilizada ?? e.UsedQuantity; e.ReturnedQuantity = r.CantidadDevuelta ?? e.ReturnedQuantity; e.Observations = Append(e.Observations, r.Reason); e.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.spare_part_updated", numeroOt, null, e, o.Faena.Code, r.Reason, ct); return ToSpare(o.WorkOrderNumber, e.Task.TaskCode, e);
    }

    public async Task<WorkOrderChecklistItemResponse?> AddChecklistItemAsync(string numeroOt, AddWorkOrderChecklistItemRequest r, UserAccessContext u, CancellationToken ct)
    {
        EnsureCanPlan(u); ValidateRequired(r.CodigoTarea, nameof(r.CodigoTarea)); ValidateRequired(r.Item, nameof(r.Item)); var o = await MutOrder(numeroOt, u, ct); var task = Task(o, r.CodigoTarea);
        var e = new WorkOrderChecklistEntity { Id = Guid.NewGuid(), WorkOrderId = o.Id, TaskId = task.Id, ItemText = r.Item.Trim(), Mandatory = r.Obligatorio, Completed = r.Completado, CompletedAtUtc = r.Completado ? DateTimeOffset.UtcNow : null, CompletedByUserId = r.Completado ? u.UserId : null, ResponseTypeId = (await CatalogAsync("WorkOrderChecklistResponseType", r.TipoRespuesta.ToString(), ct)).Id, RequiresPhoto = r.RequiereFoto, RequiresFile = r.RequiereArchivo, RequiresSignature = r.RequiereFirma };
        _dbContext.WorkOrderChecklist.Add(e); await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.checklist_added", numeroOt, null, e, o.Faena.Code, r.Item, ct); return ToChecklist(o.WorkOrderNumber, task.TaskCode, e);
    }

    public async Task<WorkOrderChecklistItemResponse?> UpdateChecklistItemAsync(string numeroOt, string itemId, UpdateChecklistItemRequest r, UserAccessContext u, CancellationToken ct)
    {
        ValidateRequired(r.Reason, nameof(r.Reason)); var o = await MutOrder(numeroOt, u, ct); if (!Guid.TryParse(itemId, out var id)) return null; var e = o.Checklist.FirstOrDefault(x => x.Id == id && x.IsActive); if (e is null) return null; ValidateChecklist(e, r);
        e.Completed = r.Completado; e.CompletedAtUtc = r.Completado ? DateTimeOffset.UtcNow : null; e.CompletedByUserId = r.Completado ? u.UserId : null; e.Response = N(r.Respuesta) ?? e.Response ?? DefaultResp(ParseEnum(e.ResponseType.Code, WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica)); e.NumericValue = r.ValorNumerico ?? e.NumericValue; e.TextValue = N(r.Texto) ?? e.TextValue; e.EvidenceId = G(r.EvidenciaId) ?? e.EvidenceId; e.SignatureId = G(r.FirmaId) ?? e.SignatureId; e.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.checklist_updated", numeroOt, null, e, o.Faena.Code, r.Reason, ct); return ToChecklist(o.WorkOrderNumber, e.Task.TaskCode, e);
    }

    public async Task<IReadOnlyCollection<WorkOrderChecklistItemResponse>> ApplyChecklistTemplateAsync(string numeroOt, ApplyChecklistTemplateRequest r, UserAccessContext u, CancellationToken ct)
    {
        EnsureCanPlan(u); ValidateRequired(r.CodigoTarea, nameof(r.CodigoTarea)); ValidateRequired(r.TemplateCode, nameof(r.TemplateCode)); var o = await MutOrder(numeroOt, u, ct); var task = Task(o, r.CodigoTarea);
        var template = await _dbContext.ChecklistTemplates.Include(t => t.Items).FirstOrDefaultAsync(t => t.Code == C(r.TemplateCode) && t.IsActive, ct); if (template is null) throw new DomainException($"No existe la plantilla de checklist '{r.TemplateCode}'.");
        var list = new List<WorkOrderChecklistEntity>(); foreach (var i in template.Items.Where(i => i.IsActive).OrderBy(i => i.SortOrder)) { var e = new WorkOrderChecklistEntity { Id = Guid.NewGuid(), WorkOrderId = o.Id, TaskId = task.Id, TemplateId = template.Id, TemplateItemId = i.Id, ItemText = i.ItemText, Mandatory = i.Mandatory, ResponseTypeId = i.ResponseTypeId, RequiresPhoto = i.RequiresPhoto, RequiresFile = i.RequiresFile, RequiresSignature = i.RequiresSignature }; _dbContext.WorkOrderChecklist.Add(e); list.Add(e); }
        await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.checklist_template_applied", numeroOt, null, list, o.Faena.Code, r.TemplateCode, ct); return list.Select(i => ToChecklist(o.WorkOrderNumber, task.TaskCode, i)).ToArray();
    }

    public async Task<WorkOrderSignatureResponse?> RegisterSignatureAsync(string numeroOt, RegisterWorkOrderSignatureRequest r, UserAccessContext u, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(r.SignatureImageDataUrl) && string.IsNullOrWhiteSpace(r.SignatureFileKey)) throw new DomainException("La firma en Data URL debe almacenarse como archivo externo antes de registrarse en SQL.");
        var o = await MutOrder(numeroOt, u, ct); WorkOrderTaskEntity? task = string.IsNullOrWhiteSpace(r.CodigoTarea) ? null : Task(o, r.CodigoTarea);
        var e = new WorkOrderSignatureEntity { Id = Guid.NewGuid(), WorkOrderId = o.Id, TaskId = task?.Id, Scope = N(r.Scope) ?? "OT", SignerUserId = N(r.UsuarioId) ?? u.UserId, SignatureFileKey = N(r.SignatureFileKey), SignedAtUtc = DateTimeOffset.UtcNow, Comment = N(r.Comentario) };
        _dbContext.WorkOrderSignatures.Add(e); await _dbContext.SaveChangesAsync(ct); await Audit(u, "work_order.signature_registered", numeroOt, null, e, o.Faena.Code, e.Scope, ct); return ToSignature(o.WorkOrderNumber, task?.TaskCode, e);
    }

    public Task<WorkOrderDetailResponse?> ScheduleAsync(string n, ScheduleWorkOrderRequest r, UserAccessContext u, CancellationToken ct) => Status(n, u, ct, WorkOrderLifecycleStatus.Programada, "work_order.scheduled", r.Reason, e => { e.ScheduledStartUtc = r.FechaInicioProgramada; e.ScheduledEndUtc = r.FechaFinProgramada; e.ScheduledAtUtc = r.FechaInicioProgramada; });
    public Task<WorkOrderDetailResponse?> StartAsync(string n, WorkOrderActionRequest r, UserAccessContext u, CancellationToken ct) => Status(n, u, ct, WorkOrderLifecycleStatus.EnEjecucion, "work_order.started", r.Reason, e => e.ActualStartUtc ??= DateTimeOffset.UtcNow);
    public Task<WorkOrderDetailResponse?> PauseAsync(string n, WorkOrderActionRequest r, UserAccessContext u, CancellationToken ct) => Status(n, u, ct, WorkOrderLifecycleStatus.Pausada, "work_order.paused", r.Reason, _ => { });
    public Task<WorkOrderDetailResponse?> FinishByTechnicianAsync(string n, WorkOrderActionRequest r, UserAccessContext u, CancellationToken ct) => Status(n, u, ct, WorkOrderLifecycleStatus.FinalizadaTecnico, "work_order.finished_by_technician", r.Reason, e => { e.TechnicianFinishedAtUtc = DateTimeOffset.UtcNow; e.FinishedByUserId = u.UserId; });
    public Task<WorkOrderDetailResponse?> CloseTechnicallyAsync(string n, WorkOrderActionRequest r, UserAccessContext u, CancellationToken ct) => Status(n, u, ct, WorkOrderLifecycleStatus.CerradaTecnicamente, "work_order.closed_technically", r.Reason, e => { var b = Blockers(ToDetail(e)); if (b.Count > 0) throw new DomainException($"La OT tiene bloqueos de cierre: {string.Join("; ", b.Select(x => x.Message))}"); e.SupervisorClosedAtUtc = DateTimeOffset.UtcNow; e.ClosedByUserId = u.UserId; });
    public Task<WorkOrderDetailResponse?> ValidatePlanningAsync(string n, WorkOrderActionRequest r, UserAccessContext u, CancellationToken ct) => Status(n, u, ct, WorkOrderLifecycleStatus.ValidadaPlanificacion, "work_order.planning_validated", r.Reason, e => { e.PlanningValidatedAtUtc = DateTimeOffset.UtcNow; e.ValidatedByUserId = u.UserId; });
    public Task<WorkOrderDetailResponse?> AnnulAsync(string n, WorkOrderActionRequest r, UserAccessContext u, CancellationToken ct) => Status(n, u, ct, WorkOrderLifecycleStatus.Anulada, "work_order.annulled", r.Reason, e => { e.AnnulledAtUtc = DateTimeOffset.UtcNow; e.AnnulledByUserId = u.UserId; e.AnnulReason = r.Reason; });

    private async Task<WorkOrderDetailResponse?> Status(string n, UserAccessContext u, CancellationToken ct, WorkOrderLifecycleStatus next, string action, string reason, Action<WorkOrderEntity> mutate)
    {
        ValidateRequired(reason, "Reason"); var o = await MutOrder(n, u, ct); if (next is WorkOrderLifecycleStatus.EnEjecucion or WorkOrderLifecycleStatus.Pausada or WorkOrderLifecycleStatus.FinalizadaTecnico) EnsurePlanSupervisorOrAssigned(u, ToDetail(o)); else EnsureCanPlan(u); var prev = o.Status; mutate(o); var ns = await CatalogAsync("WorkOrderLifecycleStatus", next.ToString(), ct); o.StatusId = ns.Id; o.UpdatedByUserId = u.UserId; o.UpdatedByUserAtUtc = DateTimeOffset.UtcNow; AddHistory(o, prev, ns, u, reason, DateTimeOffset.UtcNow); await _dbContext.SaveChangesAsync(ct); await Audit(u, action, n, prev.Code, ns.Code, o.Faena.Code, reason, ct); return await GetByIdAsync(n, u, ct);
    }

    private IQueryable<WorkOrderEntity> BaseQuery() => _dbContext.WorkOrders.AsSplitQuery()
        .Include(o => o.Asset).ThenInclude(a => a!.Faena).Include(o => o.OperationalUnit).ThenInclude(u => u!.Faena).Include(o => o.RelatedAssets).ThenInclude(x => x.Asset).Include(o => o.Faena).Include(o => o.Status).Include(o => o.MaintenanceType).Include(o => o.Priority).Include(o => o.Criticality).Include(o => o.FailureClassification).Include(o => o.Notification)
        .Include(o => o.Tasks).Include(o => o.Technicians).ThenInclude(t => t.Task).Include(o => o.Labor).ThenInclude(l => l.Task).Include(o => o.Evidences).ThenInclude(e => e.EvidenceType).Include(o => o.Evidences).ThenInclude(e => e.Task)
        .Include(o => o.SpareParts).ThenInclude(s => s.Status).Include(o => o.SpareParts).ThenInclude(s => s.Task).Include(o => o.Checklist).ThenInclude(c => c.ResponseType).Include(o => o.Checklist).ThenInclude(c => c.Task).Include(o => o.Checklist).ThenInclude(c => c.Template)
        .Include(o => o.Signatures).ThenInclude(s => s.Task).Include(o => o.History).ThenInclude(h => h.PreviousStatus).Include(o => o.History).ThenInclude(h => h.NewStatus);
    private Task<WorkOrderEntity?> FindAsync(string n, bool tracking, CancellationToken ct) { var q = BaseQuery(); if (!tracking) q = q.AsNoTracking(); return q.FirstOrDefaultAsync(o => o.WorkOrderNumber == C(n), ct); }
    private async Task<WorkOrderEntity> MutOrder(string n, UserAccessContext u, CancellationToken ct) { var o = await FindAsync(n, true, ct) ?? throw new DomainException("La OT no existe."); EnsureFaena(u, o.Faena.Code); return o; }
    private static WorkOrderTaskEntity Task(WorkOrderEntity o, string c) => o.Tasks.FirstOrDefault(t => t.IsActive && Same(t.TaskCode, c)) ?? throw new DomainException($"La tarea '{c}' no existe en la OT.");
    private async Task<AssetEntity> ResolveAssetAsync(string c, string? f, UserAccessContext u, CancellationToken ct)
    {
        var a = await _dbContext.Assets.Include(x => x.Faena).Include(x => x.OperationalState).FirstOrDefaultAsync(x => x.Code == C(c) && x.OperationalState.Code != "DADO_DE_BAJA", ct) ?? throw new DomainException($"El activo '{c}' no existe.");
        if (a.Faena is null) throw new DomainException("El activo seleccionado no tiene faena asignada.");
        if (!string.IsNullOrWhiteSpace(f) && !Same(a.Faena.Code, f)) throw new DomainException("El activo seleccionado no pertenece a la faena indicada.");
        EnsureFaena(u, a.Faena.Code); return a;
    }
    private async Task<OperationalUnitEntity> ResolveUnitAsync(string c, string? f, UserAccessContext u, CancellationToken ct)
    {
        var unit = await _dbContext.OperationalUnits.Include(x => x.Faena).Include(x => x.OperationalState).FirstOrDefaultAsync(x => x.Code == C(c) && x.OperationalState.Code != "DADO_DE_BAJA", ct) ?? throw new DomainException($"La unidad operativa '{c}' no existe.");
        if (unit.Faena is null) throw new DomainException("La unidad operativa seleccionada no tiene faena asignada.");
        if (!string.IsNullOrWhiteSpace(f) && !Same(unit.Faena.Code, f)) throw new DomainException("La unidad operativa no pertenece a la faena indicada.");
        EnsureFaena(u, unit.Faena.Code); return unit;
    }
    private async Task<Guid?> ResolveNotificationIdAsync(string? a, CancellationToken ct) => string.IsNullOrWhiteSpace(a) ? null : (await _dbContext.WorkNotifications.FirstOrDefaultAsync(n => n.NotificationNumber == C(a), ct))?.Id;
    private async Task<WorkCatalogEntity> CatalogAsync(string cat, string code, CancellationToken ct) => await _dbContext.WorkCatalogs.FirstOrDefaultAsync(x => x.Category == cat && x.Code == code.Trim(), ct) ?? throw new DomainException($"No existe catalogo {cat}:{code}.");
    private async Task<string> NextNumberAsync(string seq, string prefix, CancellationToken ct) { var cn = _dbContext.Database.GetDbConnection(); if (cn.State != ConnectionState.Open) await cn.OpenAsync(ct); await using var cmd = cn.CreateCommand(); if (_dbContext.Database.CurrentTransaction is not null) cmd.Transaction = _dbContext.Database.CurrentTransaction.GetDbTransaction(); cmd.CommandText = $"SELECT nextval('{seq}')"; var v = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture); return $"{prefix}-{v:000000}"; }
    private async Task<string> NextTaskCodeAsync(Guid id, CancellationToken ct) => $"T-{(await _dbContext.WorkOrderTasks.CountAsync(t => t.WorkOrderId == id, ct) + 1):000}";
    private void AddHistory(WorkOrderEntity o, WorkCatalogEntity p, WorkCatalogEntity n, UserAccessContext u, string r, DateTimeOffset now) => _dbContext.WorkOrderStatusHistory.Add(new WorkOrderStatusHistoryEntity { Id = Guid.NewGuid(), WorkOrder = o, WorkOrderId = o.Id, PreviousStatusId = p.Id, NewStatusId = n.Id, OccurredAtUtc = now, UserId = u.UserId, Reason = r });

    private static WorkOrderDetailResponse ToDetail(WorkOrderEntity o)
    {
        var tasks = o.Tasks.Where(t => t.IsActive).OrderBy(t => t.TaskCode).Select(t => ToTask(o.WorkOrderNumber, t)).ToArray();
        var tech = o.Technicians.Where(t => t.IsActive).Select(t => ToTech(o.WorkOrderNumber, t.Task.TaskCode, t)).ToArray();
        var labor = o.Labor.Where(l => l.IsActive).Select(l => ToLabor(o.WorkOrderNumber, l.Task.TaskCode, l)).ToArray();
        var ev = o.Evidences.Where(e => e.IsActive).Select(e => ToEvidence(o.WorkOrderNumber, e.Task?.TaskCode, e)).ToArray();
        var sp = o.SpareParts.Where(s => s.IsActive).Select(s => ToSpare(o.WorkOrderNumber, s.Task.TaskCode, s)).ToArray();
        var ch = o.Checklist.Where(c => c.IsActive).Select(c => ToChecklist(o.WorkOrderNumber, c.Task.TaskCode, c)).ToArray();
        var sig = o.Signatures.Where(s => s.IsActive).Select(s => ToSignature(o.WorkOrderNumber, s.Task?.TaskCode, s)).ToArray();
        var hist = o.History.OrderBy(h => h.OccurredAtUtc).Select(h => new WorkOrderStatusHistoryResponse(h.Id.ToString("D"), o.WorkOrderNumber, ParseEnum(h.PreviousStatus.Code, WorkOrderLifecycleStatus.OTCreada), ParseEnum(h.NewStatus.Code, WorkOrderLifecycleStatus.OTCreada), h.OccurredAtUtc, h.UserId, h.Reason)).ToArray();
        var related = o.RelatedAssets.OrderBy(x => x.AddedAtUtc).Select(x => new WorkOrderAssetResponse(x.AssetCodeSnapshot, x.AssetNameSnapshot, x.Role, x.Role == "PRINCIPAL")).ToArray(); if (related.Length == 0 && o.Asset is not null) related = [new WorkOrderAssetResponse(o.Asset.Code, o.Asset.Name, "PRINCIPAL", true)];
        var sum = new WorkOrderSummaryResponse(o.WorkOrderNumber, ParseEnum(o.Status.Code, WorkOrderLifecycleStatus.OTCreada), o.Asset?.Code ?? string.Empty, o.Asset?.Name, o.Faena.Code, o.MaintenanceType.Code, o.Description, o.Notification?.NotificationNumber, o.System, o.Subsystem, o.Component, o.Priority?.Code ?? "Media", o.Criticality?.Code ?? "Media", o.ScheduledAtUtc, o.ScheduledStartUtc, o.ScheduledEndUtc, o.IsAutomaticPreventive, o.RequiresSignature, tasks.Length, tech.Length, labor.Sum(l => l.Horas), 0, o.OperationalUnit?.Code, o.OperationalUnit?.Name, related);
        var detail = new WorkOrderDetailResponse(sum, tasks, tech, labor, ev, sp, ch, sig, hist, []); var b = Blockers(detail); return detail with { Summary = sum with { BloqueosCierre = b.Count }, ClosureBlockers = b };
    }
    private static WorkOrderTaskResponse ToTask(string ot, WorkOrderTaskEntity t) => new(ot, t.TaskCode, t.Description, t.ScheduledStartUtc, t.ScheduledEndUtc, t.RequiresEvidence, t.RequiresLabor, t.ChecklistMandatory, t.Observations);
    private static WorkOrderTaskTechnicianResponse ToTech(string ot, string task, WorkOrderTaskTechnicianEntity t) => new(t.Id.ToString("D"), ot, task, t.TechnicianUserId, t.TechnicianDisplayName, t.AssignedAtUtc, t.AssignedByUserId);
    private static WorkOrderLaborResponse ToLabor(string ot, string task, WorkOrderLaborEntity l) => new(l.Id.ToString("D"), ot, task, l.TechnicianUserId, l.Hours, l.Description, l.WorkDateUtc, l.RegisteredByUserId, l.StartTimeUtc, l.EndTimeUtc, l.Comment, l.SupervisorValidated, l.ValidatedByUserId, l.ValidatedAtUtc);
    private static WorkOrderEvidenceResponse ToEvidence(string ot, string? task, WorkOrderEvidenceEntity e) => new(e.Id.ToString("D"), ot, task, e.Name, e.ExternalKey, e.ExternalUri, e.CoversMandatoryEvidence, e.CreatedByUserAtUtc, e.CreatedByUserId, e.Observations, ParseEnum(e.EvidenceType.Code, WorkOrderEvidenceType.Archivo), e.IsPhoto, e.IsMandatory, e.StorageProvider, e.LocalPath, e.OfflineId, e.SyncStatus);
    private static WorkOrderSparePartResponse ToSpare(string ot, string task, WorkOrderSparePartEntity s) => new(s.Id.ToString("D"), ot, task, s.SparePartCode, s.Quantity, s.Unit, s.WarehouseCode, ParseEnum(s.Status.Code, WorkOrderSparePartStatus.Solicitado), s.UsedQuantity, s.ReturnedQuantity, s.Observations);
    private static WorkOrderChecklistItemResponse ToChecklist(string ot, string task, WorkOrderChecklistEntity c) => new(c.Id.ToString("D"), ot, task, c.ItemText, c.Mandatory, c.Completed, c.CompletedAtUtc, c.CompletedByUserId, c.Template?.Code, ParseEnum(c.ResponseType.Code, WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica), c.Response, c.NumericValue, c.TextValue, c.EvidenceId?.ToString("D"), c.RequiresPhoto, c.RequiresFile, c.RequiresSignature, c.SignatureId?.ToString("D"));
    private static WorkOrderSignatureResponse ToSignature(string ot, string? task, WorkOrderSignatureEntity s) => new(s.Id.ToString("D"), ot, s.SignerUserId, s.SignatureFileKey, s.SignedAtUtc, s.Comment, task, s.Scope, null);

    private static IReadOnlyCollection<WorkOrderClosureBlocker> Blockers(WorkOrderDetailResponse d)
    {
        var b = new List<WorkOrderClosureBlocker>();
        foreach (var t in d.Tasks)
        {
            if (t.RequiereEvidencia && !d.Evidences.Any(e => Same(e.CodigoTarea, t.CodigoTarea) && e.CubreEvidenciaObligatoria)) b.Add(new("EVIDENCE_REQUIRED", $"La tarea {t.CodigoTarea} requiere evidencia."));
            if (t.RequiereHH && !d.Labor.Any(l => Same(l.CodigoTarea, t.CodigoTarea))) b.Add(new("LABOR_REQUIRED", $"La tarea {t.CodigoTarea} requiere HH."));
            if (d.Labor.Any(l => Same(l.CodigoTarea, t.CodigoTarea) && !l.ValidadoSupervisor)) b.Add(new("LABOR_VALIDATION_REQUIRED", $"La tarea {t.CodigoTarea} tiene HH sin validar."));
            if (t.ChecklistObligatorio && !d.Checklist.Any(c => Same(c.CodigoTarea, t.CodigoTarea) && c.Completado)) b.Add(new("CHECKLIST_REQUIRED", $"La tarea {t.CodigoTarea} requiere checklist completo."));
        }
        foreach (var c in d.Checklist.Where(c => c.Obligatorio && !c.Completado)) b.Add(new("CHECKLIST_ITEM_REQUIRED", $"Checklist pendiente: {c.Item}"));
        foreach (var c in d.Checklist.Where(c => c.Completado && (c.RequiereFoto || c.RequiereArchivo) && string.IsNullOrWhiteSpace(c.EvidenciaId))) b.Add(new("CHECKLIST_EVIDENCE_REQUIRED", $"Checklist requiere evidencia: {c.Item}"));
        foreach (var c in d.Checklist.Where(c => c.Completado && c.RequiereFirma && string.IsNullOrWhiteSpace(c.FirmaId))) b.Add(new("CHECKLIST_SIGNATURE_REQUIRED", $"Checklist requiere firma: {c.Item}"));
        foreach (var s in d.SpareParts.Where(s => s.Estado == WorkOrderSparePartStatus.Entregado && s.CantidadUtilizada + s.CantidadDevuelta < s.Cantidad)) b.Add(new("SPARE_PART_PENDING", $"Repuesto pendiente de uso/devolucion: {s.RepuestoCodigo}"));
        if (d.Summary.RequiereFirma && !d.Signatures.Any(s => Same(s.Scope, "OT"))) b.Add(new("SIGNATURE_REQUIRED", "La OT requiere firma general."));
        return b;
    }

    private static void EnsureCanView(UserAccessContext u) { if (AnyRole(u, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician, AuthRoles.Management, AuthRoles.FaenaViewer)) return; throw new UnauthorizedAccessException("No tiene permisos para ver ordenes de trabajo."); }
    private static void EnsureCanPlan(UserAccessContext u) { if (AnyRole(u, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor)) return; throw new UnauthorizedAccessException("La accion requiere planificador o supervisor."); }
    private static void EnsurePlanSupervisorOrAssigned(UserAccessContext u, WorkOrderDetailResponse o) { if (AnyRole(u, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor) || o.Technicians.Any(t => Same(t.TecnicoUserId, u.UserId))) return; throw new UnauthorizedAccessException("No tiene permisos sobre esta OT."); }
    private static bool CanViewOrder(UserAccessContext u, WorkOrderDetailResponse o) => AnyRole(u, AuthRoles.Admin, AuthRoles.Management) || u.Faenas.Contains(o.Summary.FaenaCodigo, StringComparer.OrdinalIgnoreCase) || o.Technicians.Any(t => Same(t.TecnicoUserId, u.UserId));
    private static void EnsureFaena(UserAccessContext u, string? f) { if (string.IsNullOrWhiteSpace(f) || AnyRole(u, AuthRoles.Admin, AuthRoles.Management) || u.Faenas.Contains(f, StringComparer.OrdinalIgnoreCase)) return; throw new UnauthorizedAccessException("No tiene acceso a la faena de la OT."); }
    private static void EnsureTechCanWork(UserAccessContext u, WorkOrderDetailResponse o, string? task, string tech) { if (AnyRole(u, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor) || Same(u.UserId, tech) || o.Technicians.Any(t => Same(t.CodigoTarea, task) && Same(t.TecnicoUserId, u.UserId))) return; throw new UnauthorizedAccessException("El tecnico no esta asignado a esta tarea."); }
    private static bool AnyRole(UserAccessContext u, params string[] roles) => roles.Any(r => u.Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
    private static void EnsureOpen(WorkOrderEntity o) { var s = ParseEnum(o.Status.Code, WorkOrderLifecycleStatus.OTCreada); if (s is WorkOrderLifecycleStatus.ValidadaPlanificacion or WorkOrderLifecycleStatus.Anulada) throw new DomainException("La OT esta cerrada y no admite cambios."); }
    private async Task Audit(UserAccessContext u, string action, string id, object? prev, object? next, string? faena, string? reason, CancellationToken ct) => await _auditService.RecordAsync(new AuditEventRequest(u.UserId, action, AuditModules.WorkOrders, "WorkOrder", id, prev is null ? null : Ser(prev), next is null ? null : Ser(next), faena, AuditSeverity.Medium, reason), ct);
    private static void ValidateChecklist(WorkOrderChecklistEntity i, UpdateChecklistItemRequest r) { if (!r.Completado) return; var t = ParseEnum(i.ResponseType.Code, WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica); if (t == WorkOrderChecklistResponseType.Numerico && !r.ValorNumerico.HasValue && !i.NumericValue.HasValue) throw new DomainException("Debe registrar un valor numerico en el checklist."); if (t == WorkOrderChecklistResponseType.Texto && string.IsNullOrWhiteSpace(r.Texto) && string.IsNullOrWhiteSpace(i.TextValue)) throw new DomainException("Debe registrar texto en el checklist."); if ((t is WorkOrderChecklistResponseType.FotoObligatoria or WorkOrderChecklistResponseType.Archivo || i.RequiresPhoto || i.RequiresFile) && string.IsNullOrWhiteSpace(r.EvidenciaId) && !i.EvidenceId.HasValue) throw new DomainException("Debe asociar evidencia al checklist."); if ((t == WorkOrderChecklistResponseType.Firma || i.RequiresSignature) && string.IsNullOrWhiteSpace(r.FirmaId) && !i.SignatureId.HasValue) throw new DomainException("Debe asociar firma al checklist."); }
    private static string? DefaultResp(WorkOrderChecklistResponseType t) => t switch { WorkOrderChecklistResponseType.CumpleNoCumpleNoAplica => "Cumple", WorkOrderChecklistResponseType.BuenoRegularMalo => "Bueno", WorkOrderChecklistResponseType.SiNo => "Si", _ => null };
    private static decimal Hours(decimal? h, DateTimeOffset? s, DateTimeOffset? e) { if (s.HasValue || e.HasValue) { if (!s.HasValue || !e.HasValue) throw new DomainException("Debe indicar hora inicio y hora termino para calcular HH."); if (e.Value <= s.Value) throw new DomainException("La hora termino debe ser posterior a la hora inicio."); return Math.Round((decimal)(e.Value - s.Value).TotalHours, 2); } return h ?? 0; }
    private static string Infer(RegisterEvidenceRequest r) => !string.IsNullOrWhiteSpace(r.SharePointUrl) ? "SharePoint" : !string.IsNullOrWhiteSpace(r.LocalPath) ? "LocalSimulation" : "ManualLink";
    private static void ValidateRequired(string? v, string f) { if (string.IsNullOrWhiteSpace(v)) throw new DomainException($"El campo {f} es obligatorio."); }
    private static string? N(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static string? C(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim().ToUpperInvariant();
    private static bool Same(string? l, string? r) => string.Equals(l?.Trim(), r?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string? Append(string? e, string n) => string.IsNullOrWhiteSpace(e) ? n : $"{e} | {n}";
    private static TEnum ParseEnum<TEnum>(string? v, TEnum fb) where TEnum : struct => Enum.TryParse<TEnum>(v, true, out var p) ? p : fb;
    private static Guid? G(string? v) => Guid.TryParse(v, out var id) ? id : null;
    private static string Ser(object v) => JsonSerializer.Serialize(v, new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles });
}
