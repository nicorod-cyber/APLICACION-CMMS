using System.Text.Json;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Data.SqlServer;
using MaintenanceCMMS.Infrastructure.Data.SqlServer.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MaintenanceCMMS.Infrastructure.WorkNotifications;

/// <summary>Authoritative Avisos workflow. Client supplied codes are always resolved and scoped server-side.</summary>
public sealed class WorkNotificationService(CmmsDbContext db, IAuditService audit, ILogger<WorkNotificationService>? logger = null) : IWorkNotificationService
{
    public async Task<IReadOnlyCollection<WorkNotificationResponse>> ListAsync(WorkNotificationQuery query, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanView(user);
        var notices = await Query().AsNoTracking().ToArrayAsync(ct);
        return notices.Where(x => CanAccessFaena(user, x.Faena.Code))
            .Where(x => !query.Status.HasValue || StatusOf(x) == query.Status.Value)
            .Where(x => string.IsNullOrWhiteSpace(query.FaenaCodigo) || Same(x.Faena.Code, query.FaenaCodigo))
            .Where(x => string.IsNullOrWhiteSpace(query.EquipoCodigo) || Same(x.Asset?.Code, query.EquipoCodigo) || Same(x.OperationalUnit?.Code, query.EquipoCodigo))
            .Where(x => !query.DesdeUtc.HasValue || x.DetectedAtUtc >= query.DesdeUtc.Value)
            .Where(x => !query.HastaUtc.HasValue || x.DetectedAtUtc <= query.HastaUtc.Value)
            .Where(x => string.IsNullOrWhiteSpace(query.Texto) || x.NotificationNumber.Contains(query.Texto.Trim(), StringComparison.OrdinalIgnoreCase) || x.Items.Any(i => i.Description.Contains(query.Texto.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(x => x.CreatedAtUtc).Select(ToResponse).ToArray();
    }

    public async Task<WorkNotificationResponse?> GetByIdAsync(string id, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanView(user); var notice = await FindAsync(id, false, ct); if (notice is null) return null; EnsureFaenaAccess(user, notice.Faena.Code); return ToResponse(notice);
    }

    public async Task<WorkNotificationResponse> CreateAsync(CreateWorkNotificationRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanCreate(user); ValidateHeader(request.FechaDeteccion, request.EstadoOperacional, request.FueraServicioDesde, request.RestriccionOperacional);
        var target = await ResolveTargetAsync(request.ActivoCodigo, request.UnidadOperativaCodigo, request.FaenaCodigo, user, ct);
        var work = request.Trabajos?.ToArray() ?? [];
        if (work.Length == 0) throw new DomainException("El aviso debe contener al menos un trabajo.");
        return await ExecuteAtomicAsync(async () =>
        {
            var now = DateTimeOffset.UtcNow;
            var status = await CatalogAsync("WorkNotificationStatus", WorkNotificationStatus.Borrador.ToString(), ct);
            var notice = new WorkNotificationEntity
            {
                Id = Guid.NewGuid(), NotificationNumber = await NextNumberAsync(ct), StatusId = status.Id, Status = status, TypeId = (await CatalogAsync("WorkNotificationType", "Falla", ct)).Id,
                FaenaId = target.Faena.Id, Faena = target.Faena, AssetId = target.Asset?.Id, Asset = target.Asset, OperationalUnitId = target.Unit?.Id, OperationalUnit = target.Unit,
                Description = "Aviso de trabajo", PriorityId = (await CatalogAsync("WorkNotificationPriority", "Media", ct)).Id, CriticalityId = (await CatalogAsync("WorkNotificationCriticality", "Media", ct)).Id,
                RequesterUserId = user.UserId, DetectedAtUtc = request.FechaDeteccion ?? now, CreatedByUserAtUtc = now,
                FailureClassificationId = (await CatalogAsync("WorkFailureClassification", "SinDetencion", ct)).Id, Observations = Text(request.Observaciones),
                OperationalStatus = request.EstadoOperacional.ToString(), OutOfServiceSinceUtc = request.FueraServicioDesde, OperationalRestriction = Text(request.RestriccionOperacional), MeterReading = request.LecturaMedidor
            };
            db.WorkNotifications.Add(notice);
            await AddItemsAsync(notice, target, work, user, ct);
            await RegisterReadingAsync(notice, target, request.LecturaMedidor, user, ct);
            AddHistory(notice, null, WorkNotificationStatus.Borrador, user, null, now);
            await db.SaveChangesAsync(ct);
            await AuditAsync(user, "work_notification.created", notice, null, Snapshot(notice, WorkNotificationStatus.Borrador), null, ct);
            return ToResponse((await FindAsync(notice.NotificationNumber, false, ct))!);
        }, ct);
    }

    public async Task<WorkNotificationResponse?> UpdateDraftAsync(string id, UpdateWorkNotificationDraftRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanCreate(user); ValidateHeader(request.FechaDeteccion, request.EstadoOperacional, request.FueraServicioDesde, request.RestriccionOperacional);
        return await ExecuteAtomicAsync(async () =>
        {
            var notice = await FindAsync(id, true, ct); if (notice is null) return null; EnsureFaenaAccess(user, notice.Faena.Code); EnsureStatus(notice, WorkNotificationStatus.Borrador, WorkNotificationStatus.DevueltoFaena);
            if (request.Trabajos is null || request.Trabajos.Count == 0) throw new DomainException("El aviso debe contener al menos un trabajo.");
            var currentStatus = StatusOf(notice); var previous = Snapshot(notice, currentStatus);
            notice.DetectedAtUtc = request.FechaDeteccion ?? notice.DetectedAtUtc; notice.OperationalStatus = request.EstadoOperacional.ToString(); notice.OutOfServiceSinceUtc = request.FueraServicioDesde; notice.OperationalRestriction = Text(request.RestriccionOperacional); notice.Observations = Text(request.Observaciones); notice.UpdatedAtUtc = DateTimeOffset.UtcNow;
            // Existing operational work/evidence is immutable. Draft correction may add work; the UI creates the final list before sending.
            if (notice.Items.Count == 0) await AddItemsAsync(notice, new Target(notice.Faena, notice.Asset, notice.OperationalUnit), request.Trabajos, user, ct);
            await RegisterReadingAsync(notice, new Target(notice.Faena, notice.Asset, notice.OperationalUnit), request.LecturaMedidor, user, ct);
            await db.SaveChangesAsync(ct);
            await AuditAsync(user, "work_notification.draft_updated", notice, previous, Snapshot(notice, currentStatus), null, ct);
            return ToResponse((await FindAsync(id, false, ct))!);
        }, ct);
    }

    public async Task<WorkNotificationResponse?> SubmitAsync(string id, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanCreate(user); return await TransitionAsync(id, user, WorkNotificationStatus.PendientePlanificacion, null, [WorkNotificationStatus.Borrador, WorkNotificationStatus.DevueltoFaena], "work_notification.submitted", ct);
    }
    public async Task<WorkNotificationResponse?> ReturnToSiteAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanPlan(user); return await TransitionAsync(id, user, WorkNotificationStatus.DevueltoFaena, RequireReason(request), [WorkNotificationStatus.PendientePlanificacion], "work_notification.returned", ct);
    }
    public async Task<WorkNotificationResponse?> AcceptForManagementAsync(string id, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanPlan(user); return await TransitionAsync(id, user, WorkNotificationStatus.EnGestion, null, [WorkNotificationStatus.PendientePlanificacion], "work_notification.accepted", ct);
    }
    public async Task<WorkNotificationResponse?> RejectAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanPlan(user); return await TransitionAsync(id, user, WorkNotificationStatus.Rechazado, RequireReason(request), [WorkNotificationStatus.PendientePlanificacion], "work_notification.rejected", ct);
    }
    public async Task<WorkNotificationResponse?> AnnulAsync(string id, WorkNotificationActionRequest request, UserAccessContext user, CancellationToken ct)
    {
        EnsureCanPlan(user); return await TransitionAsync(id, user, WorkNotificationStatus.Anulado, RequireReason(request), [WorkNotificationStatus.Borrador, WorkNotificationStatus.DevueltoFaena, WorkNotificationStatus.PendientePlanificacion, WorkNotificationStatus.EnGestion], "work_notification.annulled", ct);
    }

    private async Task<WorkNotificationResponse?> TransitionAsync(string id, UserAccessContext user, WorkNotificationStatus next, string? reason, WorkNotificationStatus[] allowed, string action, CancellationToken ct)
    {
        return await ExecuteAtomicAsync(async () =>
        {
            var notice = await FindAsync(id, true, ct); if (notice is null) return null; EnsureFaenaAccess(user, notice.Faena.Code); EnsureStatus(notice, allowed);
            var currentStatus = StatusOf(notice); var previous = Snapshot(notice, currentStatus); var now = DateTimeOffset.UtcNow;
            var nextStatus = await CatalogAsync("WorkNotificationStatus", next.ToString(), ct);
            // The notification is already tracked. Change only the FK so the catalog entity and
            // the rest of the loaded graph remain Unchanged.
            notice.StatusId = nextStatus.Id; notice.UpdatedAtUtc = now;
            if (next == WorkNotificationStatus.EnGestion) foreach (var item in notice.Items.Where(x => x.Status == WorkNotificationItemStatus.PendientePlanificacion.ToString())) item.Status = WorkNotificationItemStatus.AprobadoParaGestion.ToString();
            AddHistory(notice, currentStatus, next, user, reason, now);
            await db.SaveChangesAsync(ct);
            await AuditAsync(user, action, notice, previous, Snapshot(notice, next), reason, ct);
            return ToResponse((await FindAsync(id, false, ct))!);
        }, ct);
    }

    private async Task AddItemsAsync(WorkNotificationEntity notice, Target target, IReadOnlyCollection<CreateWorkNotificationItemRequest> requests, UserAccessContext user, CancellationToken ct)
    {
        var sequence = notice.Items.Count + 1;
        foreach (var request in requests)
        {
            if (string.IsNullOrWhiteSpace(request.Descripcion)) throw new DomainException("Descripción de falla / trabajo requerido es obligatoria.");
            var asset = await ResolveItemAssetAsync(target, request.RolComponente, ct);
            var item = new WorkNotificationItemEntity { Id = Guid.NewGuid(), Notification = notice, NotificationId = notice.Id, Sequence = sequence++, AffectedAssetId = asset.Id, AffectedAsset = asset, AffectedAssetCodeSnapshot = asset.Code, AffectedAssetNameSnapshot = asset.Name, ComponentRoleSnapshot = request.RolComponente?.ToString(), TechnicalSystemId = ParseGuid(request.TechnicalSystemId, "sistema técnico"), TechnicalSubsystemId = ParseGuid(request.TechnicalSubsystemId, "subsistema técnico"), TechnicalComponentId = ParseGuid(request.TechnicalComponentId, "componente técnico"), Description = request.Descripcion.Trim(), Observations = Text(request.Observaciones), Status = WorkNotificationItemStatus.PendientePlanificacion.ToString() };
            await ValidateTechnicalNodesAsync(item, ct); notice.Items.Add(item);
            foreach (var fileId in request.EvidenciaFileIds?.Distinct(StringComparer.OrdinalIgnoreCase) ?? [])
            {
                if (!Guid.TryParse(fileId, out var fileGuid)) throw new DomainException("La evidencia indicada no es válida.");
                var file = await db.Files.SingleOrDefaultAsync(x => x.Id == fileGuid && !x.IsDeleted && x.FaenaCode == notice.Faena.Code, ct) ?? throw new DomainException("La evidencia no existe o no está autorizada para la faena del aviso.");
                item.Evidences.Add(new WorkNotificationEvidenceEntity { Id = Guid.NewGuid(), WorkNotificationItem = item, WorkNotificationItemId = item.Id, File = file, FileId = file.Id, UploadedByUserId = user.UserId });
            }
        }
    }

    private async Task RegisterReadingAsync(WorkNotificationEntity notice, Target target, decimal? value, UserAccessContext user, CancellationToken ct)
    {
        if (!value.HasValue) return;
        if (!user.Permissions.Contains(AuthPermissions.RegisterAssetReadings, StringComparer.OrdinalIgnoreCase) && !HasRole(user, AuthRoles.Admin)) throw new UnauthorizedAccessException("No tiene permiso para registrar la lectura del aviso.");
        var asset = target.Asset ?? await ResolveItemAssetAsync(target, null, ct); AssetReadingPolicy.EnsureCanRegister(asset, value.Value, notice.DetectedAtUtc, "lecturas originadas por avisos");
        var last = await db.AssetReadings.Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.ReadAtUtc).ThenByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);
        if (last is not null && value < last.Value) throw new DomainException("La lectura del aviso no puede disminuir la lectura vigente.");
        var reading = new AssetReadingEntity { Id = Guid.NewGuid(), AssetId = asset.Id, Asset = asset, OperationalUnitId = target.Unit?.Id, Value = value.Value, ReadAtUtc = notice.DetectedAtUtc, Source = "AVISO", RegisteredByUserId = user.UserId, Observations = $"Aviso {notice.NotificationNumber}" }; db.AssetReadings.Add(reading); notice.MeterReading = value; notice.MeterReadingId = reading.Id;
    }

    private IQueryable<WorkNotificationEntity> Query() => db.WorkNotifications.AsSplitQuery().Include(x => x.Faena).Include(x => x.Asset).ThenInclude(x => x!.AssetTypeDefinition).Include(x => x.OperationalUnit).Include(x => x.Status).Include(x => x.Items).ThenInclude(x => x.AffectedAsset).Include(x => x.Items).ThenInclude(x => x.Evidences).ThenInclude(x => x.File).Include(x => x.StatusHistory);
    private Task<WorkNotificationEntity?> FindAsync(string id, bool tracking, CancellationToken ct) { var q = Query(); if (!tracking) q = q.AsNoTracking(); return q.SingleOrDefaultAsync(x => x.NotificationNumber == id.Trim(), ct); }
    private async Task<Target> ResolveTargetAsync(string? assetCode, string? unitCode, string? requestedSite, UserAccessContext user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(assetCode) == string.IsNullOrWhiteSpace(unitCode)) throw new DomainException("Debe seleccionar exactamente un equipo existente (activo o unidad operativa).");
        AssetEntity? asset = null; OperationalUnitEntity? unit = null; FaenaEntity? site;
        if (!string.IsNullOrWhiteSpace(assetCode)) { asset = await db.Assets.Include(x => x.Faena).Include(x => x.OperationalState).SingleOrDefaultAsync(x => x.Code == Code(assetCode), ct) ?? throw new DomainException("El activo seleccionado no existe."); site = asset.Faena; }
        else { unit = await db.OperationalUnits.Include(x => x.Faena).Include(x => x.OperationalState).SingleOrDefaultAsync(x => x.Code == Code(unitCode), ct) ?? throw new DomainException("La unidad operativa seleccionada no existe."); site = unit.Faena; }
        if (site is null || !site.IsActive) throw new DomainException("El equipo seleccionado no tiene una faena vigente."); if (!string.IsNullOrWhiteSpace(requestedSite) && !Same(site.Code, requestedSite)) throw new DomainException("El equipo no pertenece a la faena indicada."); EnsureFaenaAccess(user, site.Code); if (asset is not null && !AssetOperationalPolicy.AllowsNotices(asset.OperationalState.Code)) throw new DomainException("El activo está dado de baja."); return new Target(site, asset, unit);
    }
    private async Task<AssetEntity> ResolveItemAssetAsync(Target target, NotificationComponentRole? role, CancellationToken ct)
    {
        if (target.Asset is not null) { if (role is not null) throw new DomainException("Sólo una unidad compuesta permite seleccionar FÁBRICA o CHASIS."); return target.Asset; }
        if (target.Unit is null) throw new DomainException("No se pudo resolver el equipo del aviso."); if (role is null) throw new DomainException("Debe seleccionar FÁBRICA o CHASIS para cada trabajo de la unidad compuesta.");
        var component = await db.OperationalUnitComponents.Include(x => x.Asset).Include(x => x.ComponentRole).SingleOrDefaultAsync(x => x.OperationalUnitId == target.Unit.Id && x.RemovedAtUtc == null && x.ComponentRole.Code == (role == NotificationComponentRole.Fabrica ? "FABRICA" : "CHASIS"), ct);
        return component?.Asset ?? throw new DomainException($"La unidad no tiene componente vigente {role}.");
    }
    private async Task ValidateTechnicalNodesAsync(WorkNotificationItemEntity item, CancellationToken ct)
    {
        var ids = new[] { item.TechnicalSystemId, item.TechnicalSubsystemId, item.TechnicalComponentId }.Where(x => x.HasValue).Select(x => x!.Value).ToArray(); if (ids.Length == 0) return;
        if (await db.TechnicalNodes.CountAsync(x => ids.Contains(x.Id) && !x.IsObsolete, ct) != ids.Length) throw new DomainException("La jerarquía técnica indicada no es válida.");
    }
    private async Task<WorkCatalogEntity> CatalogAsync(string category, string code, CancellationToken ct) => await db.WorkCatalogs.SingleOrDefaultAsync(x => x.Category == category && x.Code == code, ct) ?? throw new DomainException($"Falta catálogo requerido {category}:{code}.");
    private async Task<string> NextNumberAsync(CancellationToken ct) => $"AV-{await SqlServerSequence.NextWorkNotificationValueAsync(db, ct):000000}";
    private void AddHistory(WorkNotificationEntity n, WorkNotificationStatus? from, WorkNotificationStatus to, UserAccessContext user, string? reason, DateTimeOffset now)
    {
        var history = new WorkNotificationStatusHistoryEntity
        {
            Id = Guid.NewGuid(),
            Notification = n,
            NotificationId = n.Id,
            PreviousStatus = from?.ToString(),
            NewStatus = to.ToString(),
            UserId = user.UserId,
            OccurredAtUtc = now,
            Reason = Text(reason)
        };

        // The Guid is application-generated. Adding only through a tracked navigation lets EF's
        // graph discovery treat that non-default key as an existing entity (Modified). Registering
        // it explicitly guarantees an INSERT and relationship fix-up adds it to StatusHistory.
        db.WorkNotificationStatusHistory.Add(history);
    }
    private static WorkNotificationResponse ToResponse(WorkNotificationEntity n) => new(n.NotificationNumber, StatusOf(n), n.Faena.Code, n.Asset?.Code, n.OperationalUnit?.Code, n.DetectedAtUtc, Enum.TryParse<NotificationOperationalState>(n.OperationalStatus, true, out var operational) ? operational : NotificationOperationalState.Operativo, n.OutOfServiceSinceUtc, n.OperationalRestriction, n.MeterReading, n.Asset?.UsageMeasurementType, n.Observations, n.RequesterUserId, n.CreatedByUserAtUtc, n.Items.OrderBy(x => x.Sequence).Select(x => new WorkNotificationItemResponse(x.Id.ToString(), x.Sequence, x.AffectedAssetId.ToString(), x.AffectedAssetCodeSnapshot, x.AffectedAssetNameSnapshot, Enum.TryParse<NotificationComponentRole>(x.ComponentRoleSnapshot, true, out var role) ? role : null, x.TechnicalSystemId?.ToString(), x.TechnicalSubsystemId?.ToString(), x.TechnicalComponentId?.ToString(), x.Description, x.Observations, Enum.TryParse<WorkNotificationItemStatus>(x.Status, true, out var itemStatus) ? itemStatus : WorkNotificationItemStatus.PendientePlanificacion, x.CreatedAtUtc, x.Evidences.Select(e => new WorkNotificationEvidenceResponse(e.Id.ToString(), e.FileId.ToString(), e.File.FileName, e.CreatedAtUtc)).ToArray(), x.WorkOrderId?.ToString(), x.WorkOrderTaskId?.ToString())).ToArray(), n.StatusHistory.OrderBy(x => x.OccurredAtUtc).Select(x => new WorkNotificationHistoryResponse(x.Id.ToString(), Enum.TryParse<WorkNotificationStatus>(x.PreviousStatus, true, out var previous) ? previous : null, Enum.TryParse<WorkNotificationStatus>(x.NewStatus, true, out var next) ? next : WorkNotificationStatus.Borrador, x.UserId, x.OccurredAtUtc, x.Reason)).ToArray());
    private static WorkNotificationStatus StatusOf(WorkNotificationEntity n) => Enum.TryParse<WorkNotificationStatus>(n.Status?.Code, true, out var status) ? status : WorkNotificationStatus.Borrador;
    private static void ValidateHeader(DateTimeOffset? detected, NotificationOperationalState state, DateTimeOffset? outSince, string? restriction) { if (detected > DateTimeOffset.UtcNow.AddMinutes(5)) throw new DomainException("La fecha de detección no puede ser futura."); if (state == NotificationOperationalState.FueraDeServicio && outSince is null) throw new DomainException("Fuera de servicio exige fecha y hora."); if (state == NotificationOperationalState.OperativoConAlerta && string.IsNullOrWhiteSpace(restriction)) throw new DomainException("Operativo con alerta exige restricción operacional."); }
    private static void EnsureStatus(WorkNotificationEntity n, params WorkNotificationStatus[] allowed) { if (!allowed.Contains(StatusOf(n))) throw new DomainException("La transición no es válida para el estado actual del aviso."); }
    private static string RequireReason(WorkNotificationActionRequest request) => string.IsNullOrWhiteSpace(request.Motivo) ? throw new DomainException("El motivo es obligatorio.") : request.Motivo.Trim();
    private static void EnsureCanView(UserAccessContext u) { if (!HasRole(u, AuthRoles.Admin, AuthRoles.Planner, AuthRoles.MaintenanceSupervisor, AuthRoles.Technician, AuthRoles.Management, AuthRoles.FaenaViewer)) throw new UnauthorizedAccessException("No tiene permisos para ver avisos."); }
    private static void EnsureCanCreate(UserAccessContext u) { if (!HasRole(u, AuthRoles.Admin, AuthRoles.Technician, AuthRoles.MaintenanceSupervisor, AuthRoles.Planner)) throw new UnauthorizedAccessException("No tiene permisos para crear o corregir avisos."); }
    private static void EnsureCanPlan(UserAccessContext u) { if (!HasRole(u, AuthRoles.Admin, AuthRoles.Planner)) throw new UnauthorizedAccessException("La gestión de planificación requiere rol planificador."); }
    private static void EnsureFaenaAccess(UserAccessContext u, string code) { if (!CanAccessFaena(u, code)) throw new UnauthorizedAccessException("No tiene acceso a la faena del aviso."); }
    private static bool CanAccessFaena(UserAccessContext u, string code) => HasRole(u, AuthRoles.Admin, AuthRoles.Management) || u.Faenas.Contains(code, StringComparer.OrdinalIgnoreCase);
    private static bool HasRole(UserAccessContext u, params string[] roles) => roles.Any(r => u.Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
    private async Task AuditAsync(UserAccessContext u, string action, WorkNotificationEntity notice, WorkNotificationAuditSnapshot? previous, WorkNotificationAuditSnapshot? current, string? reason, CancellationToken ct) =>
        await audit.RecordAsync(new AuditEventRequest(u.UserId, action, AuditModules.WorkNotifications, "WorkNotification", notice.NotificationNumber,
            previous is null ? null : JsonSerializer.Serialize(previous), current is null ? null : JsonSerializer.Serialize(current), notice.Faena.Code, AuditSeverity.Medium, reason), ct);

    // Deliberately maps scalar fields only. EF navigations (in particular Item.Notification)
    // never enter an audit payload, so serialization cannot traverse an entity graph.
    private static WorkNotificationAuditSnapshot Snapshot(WorkNotificationEntity notice, WorkNotificationStatus status) => new(
        notice.NotificationNumber, status.ToString(), notice.Faena.Code, notice.Asset?.Code, notice.OperationalUnit?.Code,
        notice.OperationalStatus, notice.DetectedAtUtc, notice.OutOfServiceSinceUtc, notice.OperationalRestriction, notice.MeterReading,
        notice.Items.OrderBy(item => item.Sequence).Select(item => new WorkNotificationItemAuditSnapshot(
            item.Id, item.Sequence, item.AffectedAssetId, item.AffectedAssetCodeSnapshot, item.ComponentRoleSnapshot,
            item.TechnicalSystemId, item.TechnicalSubsystemId, item.TechnicalComponentId, item.Description, item.Status)).ToArray());

    private async Task<T> ExecuteAtomicAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        return await SqlServerExecutionStrategy.ExecuteAsync(db, async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await operation();
                await transaction.CommitAsync(ct);
                return result;
            }
            catch (DbUpdateConcurrencyException exception)
            {
                var entities = string.Join(", ", exception.Entries.Select(entry => $"{entry.Metadata.ClrType.Name}:{entry.Properties.FirstOrDefault(property => property.Metadata.IsPrimaryKey())?.CurrentValue}"));
                await transaction.RollbackAsync(ct);
                if (exception.Entries.Any(entry => entry.Entity is WorkNotificationEntity))
                {
                    logger?.LogWarning(exception, "Conflicto de concurrencia real al persistir aviso. Entidades: {Entities}", entities);
                    throw new WorkNotificationConcurrencyException();
                }

                logger?.LogError(exception, "Error de persistencia inesperado en entidades relacionadas del aviso. Entidades: {Entities}", entities);
                throw;
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
        });
    }
    private static Guid? ParseGuid(string? value, string label) => string.IsNullOrWhiteSpace(value) ? null : Guid.TryParse(value, out var parsed) ? parsed : throw new DomainException($"El {label} no es válido.");
    private static string Code(string value) => value.Trim().ToUpperInvariant(); private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim(); private static bool Same(string? x, string? y) => string.Equals(Text(x), Text(y), StringComparison.OrdinalIgnoreCase);
    private sealed record Target(FaenaEntity Faena, AssetEntity? Asset, OperationalUnitEntity? Unit);
    private sealed record WorkNotificationAuditSnapshot(string NotificationNumber, string Status, string FaenaCode, string? AssetCode, string? OperationalUnitCode, string? OperationalState, DateTimeOffset DetectedAtUtc, DateTimeOffset? OutOfServiceSinceUtc, string? OperationalRestriction, decimal? MeterReading, IReadOnlyCollection<WorkNotificationItemAuditSnapshot> Items);
    private sealed record WorkNotificationItemAuditSnapshot(Guid Id, int Sequence, Guid AffectedAssetId, string AffectedAssetCodeSnapshot, string? ComponentRoleSnapshot, Guid? TechnicalSystemId, Guid? TechnicalSubsystemId, Guid? TechnicalComponentId, string Description, string Status);
}
