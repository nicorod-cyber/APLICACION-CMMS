using System.Data;
using System.Globalization;
using System.Text.Json;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MaintenanceCMMS.Infrastructure.WorkNotifications;

public sealed class WorkNotificationService : IWorkNotificationService
{
    private readonly CmmsDbContext _dbContext;
    private readonly IAuditService _auditService;

    public WorkNotificationService(CmmsDbContext dbContext, IAuditService auditService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
    }

    public async Task<IReadOnlyCollection<WorkNotificationResponse>> ListAsync(WorkNotificationQuery query, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var items = await BaseQuery().AsNoTracking().ToArrayAsync(cancellationToken);
        return items.Select(ToResponse)
            .Where(item => query.IncludeClosed || item.Estado is not (WorkNotificationStatus.Rechazado or WorkNotificationStatus.ConvertidoOT or WorkNotificationStatus.Anulado))
            .Where(item => !query.Status.HasValue || item.Estado == query.Status)
            .Where(item => !query.Type.HasValue || item.Tipo == query.Type)
            .Where(item => !query.Priority.HasValue || item.Prioridad == query.Priority)
            .Where(item => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(item.FaenaCodigo, query.FaenaCodigo))
            .Where(item => string.IsNullOrWhiteSpace(query.ActivoCodigo) || Same(item.ActivoCodigo, query.ActivoCodigo))
            .Where(item => !query.SupervisorInbox || item.Estado is WorkNotificationStatus.Creado or WorkNotificationStatus.EnEvaluacion or WorkNotificationStatus.Aprobado)
            .Where(item => CanAccessFaena(user, item.FaenaCodigo))
            .OrderByDescending(item => item.Prioridad)
            .ThenByDescending(item => item.FechaDeteccion)
            .ToArray();
    }

    public async Task<WorkNotificationResponse?> GetByIdAsync(string id, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanView(user);
        var entity = await FindAsync(id, tracking: false, cancellationToken);
        if (entity is null) return null;
        EnsureFaenaAccess(user, entity.Faena.Code);
        return ToResponse(entity);
    }

    public async Task<WorkNotificationResponse> CreateAsync(CreateWorkNotificationRequest request, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanCreate(user);
        ValidateCreate(request);
        var asset = await FindAssetAsync(request.ActivoCodigo, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.ActivoCodigo) && asset is null) throw new DomainException($"El activo '{request.ActivoCodigo}' no existe.");
        var faena = await ResolveFaenaAsync(request.FaenaCodigo, asset, cancellationToken);
        EnsureFaenaAccess(user, faena.Code);
        var now = DateTimeOffset.UtcNow;
        var entity = new WorkNotificationEntity
        {
            Id = Guid.NewGuid(),
            NotificationNumber = await NextNumberAsync("work_notification_number_seq", "AV", cancellationToken),
            StatusId = (await CatalogAsync("WorkNotificationStatus", WorkNotificationStatus.Creado.ToString(), cancellationToken)).Id,
            TypeId = (await CatalogAsync("WorkNotificationType", request.Tipo.ToString(), cancellationToken)).Id,
            FaenaId = faena.Id,
            Faena = faena,
            AssetId = asset?.Id,
            Asset = asset,
            System = NormalizeText(request.Sistema),
            Subsystem = NormalizeText(request.Subsistema),
            Component = NormalizeText(request.Componente),
            Description = request.Descripcion.Trim(),
            PriorityId = (await CatalogAsync("WorkNotificationPriority", request.Prioridad.ToString(), cancellationToken)).Id,
            CriticalityId = (await CatalogAsync("WorkNotificationCriticality", request.Criticidad.ToString(), cancellationToken)).Id,
            RequesterUserId = user.UserId,
            InitialEvidenceReference = NormalizeText(request.EvidenciaInicial),
            DetectedAtUtc = request.FechaDeteccion ?? now,
            CreatedByUserAtUtc = now,
            FailureClassificationId = (await CatalogAsync("WorkFailureClassification", request.ClasificacionFalla.ToString(), cancellationToken)).Id,
            Observations = "Aviso creado en CMMS"
        };
        _dbContext.WorkNotifications.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecordNotificationAuditAsync(user, "work_notification.created", entity.NotificationNumber, null, entity, faena.Code, request.Descripcion, cancellationToken);
        return ToResponse((await FindAsync(entity.NotificationNumber, false, cancellationToken))!);
    }

    public Task<WorkNotificationResponse?> EvaluateAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken cancellationToken)
        => MutateAsync(id, request, user, cancellationToken, WorkNotificationStatus.EnEvaluacion, "work_notification.evaluated", current => EnsureStatus(current, WorkNotificationStatus.Creado));

    public Task<WorkNotificationResponse?> ApproveAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken cancellationToken)
        => MutateAsync(id, request, user, cancellationToken, WorkNotificationStatus.Aprobado, "work_notification.approved", current => EnsureStatus(current, WorkNotificationStatus.Creado, WorkNotificationStatus.EnEvaluacion));

    public Task<WorkNotificationResponse?> RejectAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken cancellationToken)
        => MutateAsync(id, request, user, cancellationToken, WorkNotificationStatus.Rechazado, "work_notification.rejected", current =>
        {
            if (current.Estado is WorkNotificationStatus.ConvertidoOT or WorkNotificationStatus.Anulado) throw new DomainException("El aviso ya no admite rechazo.");
        });

    public async Task<WorkNotificationConversionResponse?> ConvertToWorkOrderAsync(string id, ConvertWorkNotificationToWorkOrderRequest request, UserAccessContext user, CancellationToken cancellationToken)
    {
        EnsureCanEvaluate(user);
        ValidateReason(request.Reason);
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var notification = await FindAsync(id, tracking: true, cancellationToken);
        if (notification is null) return null;
        EnsureFaenaAccess(user, notification.Faena.Code);
        var current = ToResponse(notification);
        EnsureStatus(current, WorkNotificationStatus.Aprobado);
        if (notification.WorkOrderId.HasValue) throw new DomainException("El aviso ya fue convertido a OT.");
        if (notification.Asset is null) throw new DomainException("El aviso requiere activo para convertirse a OT.");
        var now = DateTimeOffset.UtcNow;
        var maintenanceType = NormalizeText(request.TipoMantenimiento) ?? ResolveMaintenanceType(current.Tipo);
        var status = await CatalogAsync("WorkOrderLifecycleStatus", WorkOrderLifecycleStatus.OTCreada.ToString(), cancellationToken);
        var workOrder = new WorkOrderEntity
        {
            Id = Guid.NewGuid(),
            WorkOrderNumber = await NextNumberAsync("work_order_number_seq", "OT", cancellationToken),
            AssetId = notification.AssetId!.Value,
            Asset = notification.Asset,
            FaenaId = notification.FaenaId,
            Faena = notification.Faena,
            StatusId = status.Id,
            Status = status,
            MaintenanceTypeId = (await CatalogAsync("MaintenanceType", maintenanceType, cancellationToken)).Id,
            Description = notification.Description,
            NotificationId = notification.Id,
            System = notification.System,
            Subsystem = notification.Subsystem,
            Component = notification.Component,
            PriorityId = notification.PriorityId,
            CriticalityId = notification.CriticalityId,
            FailureClassificationId = notification.FailureClassificationId,
            ScheduledAtUtc = request.FechaProgramada,
            CreatedByUserId = user.UserId,
            CreatedByUserAtUtc = now,
            UpdatedByUserId = user.UserId,
            UpdatedByUserAtUtc = now
        };
        _dbContext.WorkOrders.Add(workOrder);
        _dbContext.WorkOrderStatusHistory.Add(new WorkOrderStatusHistoryEntity
        {
            Id = Guid.NewGuid(), WorkOrder = workOrder, PreviousStatusId = status.Id, NewStatusId = status.Id, OccurredAtUtc = now, UserId = user.UserId, Reason = "OT creada desde aviso"
        });
        notification.StatusId = (await CatalogAsync("WorkNotificationStatus", WorkNotificationStatus.ConvertidoOT.ToString(), cancellationToken)).Id;
        notification.WorkOrder = workOrder;
        notification.WorkOrderId = workOrder.Id;
        notification.ConvertedByUserId = user.UserId;
        notification.ConvertedAtUtc = now;
        notification.Observations = Append(notification.Observations, $"OT {workOrder.WorkOrderNumber}: {request.Reason}");
        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        await RecordNotificationAuditAsync(user, "work_notification.converted_to_work_order", notification.NotificationNumber, null, notification, notification.Faena.Code, request.Reason, cancellationToken, AuditSeverity.High);
        await RecordWorkOrderAuditAsync(user, "work_order.created_from_notification", workOrder.WorkOrderNumber, null, workOrder, notification.Faena.Code, notification.NotificationNumber, cancellationToken);
        return new WorkNotificationConversionResponse(ToResponse((await FindAsync(notification.NotificationNumber, false, cancellationToken))!), workOrder.WorkOrderNumber);
    }

    public Task<WorkNotificationResponse?> AnnulAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken cancellationToken)
        => MutateAsync(id, request, user, cancellationToken, WorkNotificationStatus.Anulado, "work_notification.annulled", current =>
        {
            if (current.Estado == WorkNotificationStatus.ConvertidoOT) throw new DomainException("No se puede anular un aviso convertido a OT.");
        });

    private async Task<WorkNotificationResponse?> MutateAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken cancellationToken, WorkNotificationStatus nextStatus, string action, Action<WorkNotificationResponse> validate)
    {
        EnsureCanEvaluate(user);
        ValidateReason(request.Reason);
        var entity = await FindAsync(id, tracking: true, cancellationToken);
        if (entity is null) return null;
        EnsureFaenaAccess(user, entity.Faena.Code);
        var current = ToResponse(entity);
        validate(current);
        var previous = JsonSerializer.Serialize(current);
        var now = DateTimeOffset.UtcNow;
        entity.StatusId = (await CatalogAsync("WorkNotificationStatus", nextStatus.ToString(), cancellationToken)).Id;
        switch (nextStatus)
        {
            case WorkNotificationStatus.EnEvaluacion: entity.EvaluatedByUserId = user.UserId; entity.EvaluatedAtUtc = now; entity.Observations = Append(entity.Observations, $"Evaluacion: {request.Reason}"); break;
            case WorkNotificationStatus.Aprobado: entity.ApprovedByUserId = user.UserId; entity.ApprovedAtUtc = now; entity.Observations = Append(entity.Observations, $"Aprobacion: {request.Reason}"); break;
            case WorkNotificationStatus.Rechazado: entity.RejectedByUserId = user.UserId; entity.RejectedAtUtc = now; entity.RejectReason = request.Reason; entity.Observations = Append(entity.Observations, $"Rechazo: {request.Reason}"); break;
            case WorkNotificationStatus.Anulado: entity.AnnulledByUserId = user.UserId; entity.AnnulledAtUtc = now; entity.AnnulReason = request.Reason; entity.Observations = Append(entity.Observations, $"Anulacion: {request.Reason}"); break;
        }
        entity.UpdatedAtUtc = now;
        await _dbContext.SaveChangesAsync(cancellationToken);
        await RecordNotificationAuditAsync(user, action, entity.NotificationNumber, previous, entity, entity.Faena.Code, request.Reason, cancellationToken);
        return ToResponse((await FindAsync(id, false, cancellationToken))!);
    }

    private IQueryable<WorkNotificationEntity> BaseQuery() => _dbContext.WorkNotifications.AsSplitQuery()
        .Include(e => e.Status).Include(e => e.Type).Include(e => e.Faena).Include(e => e.Asset)
        .Include(e => e.Priority).Include(e => e.Criticality).Include(e => e.FailureClassification).Include(e => e.WorkOrder);

    private Task<WorkNotificationEntity?> FindAsync(string id, bool tracking, CancellationToken ct)
    {
        var query = BaseQuery();
        if (!tracking) query = query.AsNoTracking();
        return query.FirstOrDefaultAsync(e => e.NotificationNumber == id, ct);
    }

    private async Task<WorkCatalogEntity> CatalogAsync(string category, string code, CancellationToken ct)
    {
        var normalized = code.Trim();
        var catalog = await _dbContext.WorkCatalogs.FirstOrDefaultAsync(e => e.Category == category && e.Code == normalized, ct);
        if (catalog is null) throw new DomainException($"No existe catalogo {category}:{code}.");
        return catalog;
    }

    private async Task<AssetEntity?> FindAssetAsync(string? assetCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(assetCode)) return null;
        return await _dbContext.Assets.Include(e => e.Faena).FirstOrDefaultAsync(e => e.Code == NormalizeCode(assetCode), ct);
    }

    private async Task<FaenaEntity> ResolveFaenaAsync(string? requestedFaena, AssetEntity? asset, CancellationToken ct)
    {
        if (asset is not null && string.IsNullOrWhiteSpace(requestedFaena)) return asset.Faena;
        if (asset is not null && !Same(requestedFaena, asset.Faena.Code)) throw new DomainException("El activo seleccionado no pertenece a la faena indicada.");
        var code = NormalizeCode(requestedFaena);
        var faena = await _dbContext.Faenas.FirstOrDefaultAsync(e => e.Code == code, ct);
        return faena ?? throw new DomainException($"La faena '{requestedFaena}' no existe.");
    }

    private async Task<string> NextNumberAsync(string sequenceName, string prefix, CancellationToken ct)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        if (_dbContext.Database.CurrentTransaction is not null) command.Transaction = _dbContext.Database.CurrentTransaction.GetDbTransaction();
        command.CommandText = $"SELECT nextval('{sequenceName}')";
        var value = Convert.ToInt64(await command.ExecuteScalarAsync(ct), CultureInfo.InvariantCulture);
        return $"{prefix}-{value:000000}";
    }

    private static WorkNotificationResponse ToResponse(WorkNotificationEntity e) => new(
        e.NotificationNumber,
        ParseEnum(e.Status.Code, WorkNotificationStatus.Creado),
        ParseEnum(e.Type.Code, WorkNotificationType.Falla),
        e.Faena.Code,
        e.Asset?.Code,
        e.System,
        e.Subsystem,
        e.Component,
        e.Description,
        ParseEnum(e.Priority.Code, WorkNotificationPriority.Media),
        ParseEnum(e.Criticality.Code, WorkNotificationCriticality.Media),
        e.RequesterUserId,
        e.InitialEvidenceReference,
        e.DetectedAtUtc,
        e.CreatedByUserAtUtc,
        ParseEnum(e.FailureClassification.Code, WorkFailureClassification.SinDetencion),
        e.EvaluatedByUserId,
        e.EvaluatedAtUtc,
        e.ApprovedByUserId,
        e.ApprovedAtUtc,
        e.RejectedByUserId,
        e.RejectedAtUtc,
        e.RejectReason,
        e.AnnulledByUserId,
        e.AnnulledAtUtc,
        e.AnnulReason,
        e.WorkOrder?.WorkOrderNumber,
        e.ConvertedByUserId,
        e.ConvertedAtUtc,
        e.Observations);

    private static string ResolveMaintenanceType(WorkNotificationType type) => type switch
    {
        WorkNotificationType.Preventivo => MaintenanceType.Preventive.ToString(),
        WorkNotificationType.Inspeccion => MaintenanceType.Inspection.ToString(),
        WorkNotificationType.Mejora => MaintenanceType.Predictive.ToString(),
        _ => MaintenanceType.Corrective.ToString()
    };

    private static void ValidateCreate(CreateWorkNotificationRequest request)
    {
        ValidateRequired(request.Descripcion, nameof(request.Descripcion));
        if (string.IsNullOrWhiteSpace(request.FaenaCodigo) && string.IsNullOrWhiteSpace(request.ActivoCodigo)) throw new DomainException("Debe indicar faena o activo para crear el aviso.");
        if (request.FechaDeteccion.HasValue && request.FechaDeteccion.Value > DateTimeOffset.UtcNow.AddMinutes(5)) throw new DomainException("La fecha de deteccion no puede ser futura.");
    }

    private static void EnsureStatus(WorkNotificationResponse request, params WorkNotificationStatus[] expected)
    {
        if (!expected.Contains(request.Estado)) throw new DomainException($"El aviso esta en estado {request.Estado} y no admite esta accion.");
    }

    private static void EnsureCanView(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician, AuthRoles.Management, AuthRoles.FaenaViewer)) return;
        throw new UnauthorizedAccessException("No tiene permisos para ver avisos de trabajo.");
    }

    private static void EnsureCanCreate(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician)) return;
        throw new UnauthorizedAccessException("No tiene permisos para crear avisos de trabajo.");
    }

    private static void EnsureCanEvaluate(UserAccessContext user)
    {
        if (HasAnyRole(user, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor)) return;
        throw new UnauthorizedAccessException("La evaluacion de avisos requiere planificador o supervisor de mantenimiento.");
    }

    private static void EnsureFaenaAccess(UserAccessContext user, string? faenaCodigo)
    {
        if (!CanAccessFaena(user, faenaCodigo)) throw new UnauthorizedAccessException("No tiene acceso a la faena del aviso.");
    }

    private static bool CanAccessFaena(UserAccessContext user, string? faenaCodigo) => string.IsNullOrWhiteSpace(faenaCodigo) || HasAnyRole(user, AuthRoles.Admin, AuthRoles.Management) || user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase);
    private static bool HasAnyRole(UserAccessContext user, params string[] roles) => roles.Any(role => user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase));

    private async Task RecordNotificationAuditAsync(UserAccessContext user, string action, string entityId, object? previous, object? updated, string? faenaCodigo, string? reason, CancellationToken ct, AuditSeverity severity = AuditSeverity.Medium)
    {
        await _auditService.RecordAsync(new AuditEventRequest(user.UserId, action, AuditModules.WorkNotifications, "WorkNotification", entityId, previous is null ? null : Serialize(previous), updated is null ? null : Serialize(updated), faenaCodigo, severity, reason), ct);
    }

    private async Task RecordWorkOrderAuditAsync(UserAccessContext user, string action, string entityId, object? previous, object? updated, string? faenaCodigo, string? reason, CancellationToken ct)
    {
        await _auditService.RecordAsync(new AuditEventRequest(user.UserId, action, AuditModules.WorkOrders, "WorkOrder", entityId, previous is null ? null : Serialize(previous), updated is null ? null : Serialize(updated), faenaCodigo, AuditSeverity.High, reason), ct);
    }

    private static void ValidateReason(string? value) => ValidateRequired(value, "Reason");
    private static void ValidateRequired(string? value, string fieldName) { if (string.IsNullOrWhiteSpace(value)) throw new DomainException($"El campo {fieldName} es obligatorio."); }
    private static string? NormalizeText(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? NormalizeCode(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string? Append(string? existing, string next) => string.IsNullOrWhiteSpace(existing) ? next : $"{existing} | {next}";
    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct => Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;
    private static string Serialize(object value) => JsonSerializer.Serialize(value, new JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles });
}


