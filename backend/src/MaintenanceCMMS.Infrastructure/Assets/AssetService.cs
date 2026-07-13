using System.Text.Json;
using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Assets;

public sealed class AssetService : IAssetService
{
    private static readonly string[] RequiredTechnicalFields =
    [
        "Nombre",
        "FaenaCodigo",
        "TipoActivo",
        "Familia",
        "Marca",
        "Modelo",
        "NumeroSerie",
        "Propiedad",
        "Criticidad",
        "EstadoDocumental",
        "EstadoOperacional"
    ];

    private readonly CmmsDbContext _dbContext;
    private readonly IAuditService _auditService;
    private readonly IAuthorizationPolicyService _authorizationPolicyService;

    public AssetService(
        CmmsDbContext dbContext,
        IAuditService auditService,
        IAuthorizationPolicyService authorizationPolicyService)
    {
        _dbContext = dbContext;
        _auditService = auditService;
        _authorizationPolicyService = authorizationPolicyService;
    }

    public async Task<IReadOnlyCollection<AssetSummary>> ListAsync(
        AssetListQuery query,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo) &&
            !_authorizationPolicyService.CanViewFaena(user, query.FaenaCodigo))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena solicitada.");
        }

        var assets = await QueryAssets()
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

        return assets
            .Where(asset => _authorizationPolicyService.CanViewFaena(user, asset.Faena.Code))
            .Select(ToSummary)
            .Where(asset => Matches(query, asset))
            .OrderBy(asset => asset.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<AssetDetail?> GetByIdAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var asset = await FindAssetAsync(codigo, tracking: false, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        EnsureCanViewAsset(user, asset);
        return ToDetail(asset);
    }

    public async Task<AssetDetail> CreateAsync(
        CreateAssetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanMaintainAssets(user);
        ValidateRequired(request.Codigo, nameof(request.Codigo));
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        ValidateRequired(request.FaenaCodigo, nameof(request.FaenaCodigo));
        ValidateRequired(request.TipoActivo, nameof(request.TipoActivo));
        ValidateRequired(request.Familia, nameof(request.Familia));
        EnsureCanUseFaena(user, request.FaenaCodigo);

        var code = NormalizeCode(request.Codigo);
        if (await _dbContext.Assets.AnyAsync(asset => asset.Code == code, cancellationToken))
        {
            throw new DomainException($"Ya existe un activo con codigo '{request.Codigo}'.");
        }

        var faena = await FindFaenaAsync(request.FaenaCodigo, cancellationToken);
        var family = await FindActiveFamilyAsync(request.Familia!, cancellationToken);
        var operationalState = await ResolveOperationalStateAsync(request.EstadoOperacional, request.Estado, cancellationToken);

        var entity = new AssetEntity
        {
            Code = code,
            Name = request.Nombre.Trim(),
            FaenaId = faena.Id,
            FamilyId = family.Id,
            OperationalStateId = operationalState.Id,
            RecordStatus = ToRecordStatus(request.Estado),
            AssetType = request.TipoActivo.Trim(),
            TechnicalLocationCode = EmptyToNull(request.UbicacionTecnicaCodigo),
            Brand = EmptyToNull(request.Marca),
            Model = EmptyToNull(request.Modelo),
            Plate = EmptyToNull(request.Patente),
            SerialNumber = EmptyToNull(request.NumeroSerie),
            Ownership = EmptyToNull(request.Propiedad),
            Criticality = EmptyToNull(request.Criticidad),
            DocumentStatus = EmptyToNull(request.EstadoDocumental) ?? "Pendiente",
            TechnicalSheetValidated = request.FichaValidada
        };

        _dbContext.Assets.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecordAssetAuditAsync(
            user,
            "Created",
            entity.Code,
            faena.Code,
            null,
            Serialize(entity),
            "Activo creado",
            cancellationToken);

        return (await GetByIdAsync(entity.Code, user, cancellationToken))!;
    }

    public async Task<AssetDetail?> UpdateAsync(
        string codigo,
        UpdateAssetRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanMaintainAssets(user);
        ValidateRequired(request.Nombre, nameof(request.Nombre));
        ValidateRequired(request.FaenaCodigo, nameof(request.FaenaCodigo));
        ValidateRequired(request.TipoActivo, nameof(request.TipoActivo));
        ValidateRequired(request.Familia, nameof(request.Familia));

        var asset = await FindAssetAsync(codigo, tracking: true, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        EnsureCanViewAsset(user, asset);

        if (!Same(asset.Faena.Code, request.FaenaCodigo) && !_authorizationPolicyService.CanChangeAssetFaena(user))
        {
            throw new UnauthorizedAccessException("Cambiar la faena del activo requiere permiso especial.");
        }

        EnsureCanUseFaena(user, request.FaenaCodigo);
        var previousValue = Serialize(asset);

        var faena = await FindFaenaAsync(request.FaenaCodigo, cancellationToken);
        var family = await FindActiveFamilyAsync(request.Familia!, cancellationToken);
        var operationalState = await ResolveOperationalStateAsync(request.EstadoOperacional, request.Estado, cancellationToken);

        asset.Name = request.Nombre.Trim();
        asset.FaenaId = faena.Id;
        asset.FamilyId = family.Id;
        asset.OperationalStateId = operationalState.Id;
        asset.RecordStatus = ToRecordStatus(request.Estado);
        asset.AssetType = request.TipoActivo.Trim();
        asset.TechnicalLocationCode = EmptyToNull(request.UbicacionTecnicaCodigo);
        asset.Brand = EmptyToNull(request.Marca);
        asset.Model = EmptyToNull(request.Modelo);
        asset.Plate = EmptyToNull(request.Patente);
        asset.SerialNumber = EmptyToNull(request.NumeroSerie);
        asset.Ownership = EmptyToNull(request.Propiedad);
        asset.Criticality = EmptyToNull(request.Criticidad);
        asset.DocumentStatus = EmptyToNull(request.EstadoDocumental) ?? "Pendiente";
        asset.TechnicalSheetValidated = request.FichaValidada ?? asset.TechnicalSheetValidated;
        asset.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await RecordAssetAuditAsync(
            user,
            "Updated",
            asset.Code,
            faena.Code,
            previousValue,
            Serialize(asset),
            request.Reason ?? "Activo actualizado",
            cancellationToken);

        return await GetByIdAsync(asset.Code, user, cancellationToken);
    }

    public async Task<AssetStateEventResponse?> AddStateEventAsync(
        string codigo,
        CreateAssetStateEventRequest request,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        EnsureCanChangeState(user);
        ValidateRequired(request.Reason, nameof(request.Reason));

        var asset = await FindAssetAsync(codigo, tracking: true, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        EnsureCanViewAsset(user, asset);

        var previousStatus = ParseStatus(asset.RecordStatus);
        var previousStateId = asset.OperationalStateId;
        var nextState = await ResolveOperationalStateAsync(null, request.Status, cancellationToken);

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        asset.RecordStatus = ToRecordStatus(request.Status);
        asset.OperationalStateId = nextState.Id;
        asset.UpdatedAtUtc = DateTimeOffset.UtcNow;

        var occurredAt = request.OccurredAtUtc ?? DateTimeOffset.UtcNow;
        var stateEvent = new AssetStateEventEntity
        {
            AssetId = asset.Id,
            PreviousStateId = previousStateId,
            NewStateId = nextState.Id,
            OccurredAtUtc = occurredAt,
            UserId = user.UserId,
            Reason = request.Reason.Trim()
        };

        _dbContext.AssetStateEvents.Add(stateEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await RecordAssetAuditAsync(
            user,
            "StateChanged",
            asset.Code,
            asset.Faena.Code,
            previousStatus.ToString(),
            request.Status.ToString(),
            request.Reason,
            cancellationToken);

        return new AssetStateEventResponse(
            stateEvent.Id.ToString("D"),
            asset.Code,
            previousStatus,
            request.Status,
            occurredAt,
            stateEvent.Reason,
            user.UserId);
    }

    public async Task<IReadOnlyCollection<AssetHistoryEntry>> GetHistoryAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var asset = await FindAssetAsync(codigo, tracking: false, cancellationToken);
        if (asset is null)
        {
            return [];
        }

        EnsureCanViewAsset(user, asset);

        var events = await _dbContext.AssetStateEvents
            .AsNoTracking()
            .Include(item => item.PreviousState)
            .Include(item => item.NewState)
            .Where(item => item.AssetId == asset.Id)
            .OrderByDescending(item => item.OccurredAtUtc)
            .ToArrayAsync(cancellationToken);

        var audit = await _auditService.QueryAsync(new AuditQuery(
            Module: AuditModules.Assets,
            EntityName: "Asset",
            Take: 500), cancellationToken);

        var eventEntries = events.Select(item => new AssetHistoryEntry(
            item.Id.ToString("D"),
            item.OccurredAtUtc,
            "StateChanged",
            "EventosEstado",
            item.UserId,
            item.PreviousState?.Code,
            item.NewState.Code,
            item.Reason));

        var auditEntries = audit.Items
            .Where(entry => Same(entry.EntityId, asset.Code))
            .Select(entry => new AssetHistoryEntry(
                entry.AuditId,
                entry.OccurredAtUtc,
                entry.Action,
                "Auditoria",
                entry.UserId,
                entry.PreviousValue,
                entry.NewValue,
                entry.Detail ?? entry.Reason));

        return auditEntries
            .Concat(eventEntries)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<AssetDocumentResponse>> GetDocumentsAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var asset = await FindAssetAsync(codigo, tracking: false, cancellationToken);
        if (asset is null)
        {
            return [];
        }

        EnsureCanViewAsset(user, asset);

        return await _dbContext.DocumentAssets
            .AsNoTracking()
            .Include(item => item.Document)
            .ThenInclude(document => document.Versions)
            .ThenInclude(version => version.File)
            .Where(item => item.AssetId == asset.Id && item.IsActive)
            .Select(item => new AssetDocumentResponse(
                "Activo",
                asset.Code,
                item.Document.DocumentType.Code,
                item.Document.Status,
                null,
                item.Document.Versions
                    .OrderByDescending(version => version.VersionNumber)
                    .Select(version => version.File.FileKey)
                    .FirstOrDefault(),
                false,
                false,
                false))
            .OrderBy(item => item.TipoDocumento)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AssetCostSummary?> GetCostsAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var asset = await GetByIdAsync(codigo, user, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        if (!_authorizationPolicyService.CanViewCosts(user))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para ver costos.");
        }

        var costs = await _dbContext.CostEntries.AsNoTracking().Where(item => item.Asset!.Code == asset.Codigo).OrderByDescending(item => item.OccurredAtUtc).Select(item => new AssetCostLine("Costos", item.Category, item.Amount, item.Currency, item.CostNumber)).ToArrayAsync(cancellationToken);
        return new AssetCostSummary(asset.Codigo, costs.Sum(item => item.Amount), "CLP", costs);
    }

    public async Task<AssetAvailabilityResponse?> GetAvailabilityAsync(
        string codigo,
        UserAccessContext user,
        CancellationToken cancellationToken)
    {
        var asset = await GetByIdAsync(codigo, user, cancellationToken);
        if (asset is null)
        {
            return null;
        }

        var operationallyAvailable = asset.Estado == AssetStatus.Active &&
                                      asset.EstadoOperacional.Equals("OPERATIVO_FAENA", StringComparison.OrdinalIgnoreCase);
        var blockers = operationallyAvailable
            ? Array.Empty<string>()
            : [$"Estado operacional: {asset.EstadoOperacional}"];

        return new AssetAvailabilityResponse(
            asset.Codigo,
            operationallyAvailable && asset.DisponibleDocumentalmente,
            operationallyAvailable,
            asset.DisponibleDocumentalmente,
            asset.EstadoOperacional,
            asset.EstadoDocumental,
            blockers,
            operationallyAvailable ? 100 : 0);
    }

    private IQueryable<AssetEntity> QueryAssets()
    {
        return _dbContext.Assets
            .Include(asset => asset.Faena)
            .Include(asset => asset.Family)
            .Include(asset => asset.OperationalState);
    }

    private Task<AssetEntity?> FindAssetAsync(string codigo, bool tracking, CancellationToken cancellationToken)
    {
        var query = QueryAssets();
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        var code = NormalizeCode(codigo);
        return query.FirstOrDefaultAsync(asset => asset.Code == code, cancellationToken);
    }

    private async Task<FaenaEntity> FindFaenaAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(code);
        var faena = await _dbContext.Faenas.FirstOrDefaultAsync(item => item.Code == normalized && item.IsActive, cancellationToken);
        return faena ?? throw new DomainException("La faena indicada no existe o esta inactiva.");
    }

    private async Task<EquipmentFamilyEntity> FindActiveFamilyAsync(string value, CancellationToken cancellationToken)
    {
        var normalized = NormalizeCode(value);
        var family = await _dbContext.EquipmentFamilies.FirstOrDefaultAsync(
            item => item.IsActive && (item.Code == normalized || item.Name.ToUpper() == normalized),
            cancellationToken);

        return family ?? throw new DomainException("La familia de equipo indicada no existe o esta inactiva.");
    }

    private async Task<AssetOperationalStateEntity> ResolveOperationalStateAsync(
        string? requested,
        AssetStatus status,
        CancellationToken cancellationToken)
    {
        var code = NormalizeOperationalStateCode(requested) ?? DefaultOperationalStateCode(status);
        var state = await _dbContext.AssetOperationalStates.FirstOrDefaultAsync(item => item.Code == code && item.IsActive, cancellationToken);
        return state ?? throw new DomainException($"El estado operacional '{code}' no existe o esta inactivo.");
    }

    private static AssetSummary ToSummary(AssetEntity entity)
    {
        var completeness = CalculateCompleteness(entity);

        return new AssetSummary(
            entity.Code,
            entity.Name,
            entity.Faena.Code,
            entity.AssetType,
            ParseStatus(entity.RecordStatus, entity.OperationalState.Code),
            entity.TechnicalLocationCode,
            entity.Family.Name,
            entity.Brand,
            entity.Model,
            entity.Plate,
            entity.SerialNumber,
            entity.Ownership,
            entity.Criticality,
            entity.DocumentStatus ?? "Pendiente",
            entity.OperationalState.Code,
            completeness,
            !string.Equals(entity.DocumentStatus, "Vencido", StringComparison.OrdinalIgnoreCase),
            entity.TechnicalSheetValidated);
    }

    private static AssetDetail ToDetail(AssetEntity entity)
    {
        var summary = ToSummary(entity);
        return new AssetDetail(
            summary.Codigo,
            summary.Nombre,
            summary.FaenaCodigo,
            summary.TipoActivo,
            summary.Estado,
            summary.UbicacionTecnicaCodigo,
            summary.Familia,
            summary.Marca,
            summary.Modelo,
            summary.Patente,
            summary.NumeroSerie,
            summary.Propiedad,
            summary.Criticidad,
            summary.EstadoDocumental,
            summary.EstadoOperacional,
            summary.CompletitudFicha,
            summary.DisponibleDocumentalmente,
            summary.FichaValidada,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            BuildTechnicalFields(entity),
            [],
            []);
    }

    private static bool Matches(AssetListQuery query, AssetSummary asset)
    {
        return (string.IsNullOrWhiteSpace(query.FaenaCodigo) ||
                Same(asset.FaenaCodigo, query.FaenaCodigo)) &&
               (!query.Estado.HasValue || asset.Estado == query.Estado.Value) &&
               (string.IsNullOrWhiteSpace(query.Familia) ||
                Same(asset.Familia, query.Familia)) &&
               (string.IsNullOrWhiteSpace(query.Criticidad) ||
                Same(asset.Criticidad, query.Criticidad));
    }

    private static AssetCompleteness CalculateCompleteness(AssetEntity entity)
    {
        var values = BuildTechnicalFields(entity);
        var missing = RequiredTechnicalFields
            .Where(field => !values.TryGetValue(field, out var value) || string.IsNullOrWhiteSpace(value))
            .ToArray();

        var completed = RequiredTechnicalFields.Length - missing.Length;
        var percentage = (int)Math.Round((decimal)completed / RequiredTechnicalFields.Length * 100, MidpointRounding.AwayFromZero);
        var state = percentage >= 100 ? "Completa" : completed == 0 ? "Pendiente" : "Parcial";

        return new AssetCompleteness(RequiredTechnicalFields.Length, completed, percentage, state, missing);
    }

    private static IReadOnlyDictionary<string, string?> BuildTechnicalFields(AssetEntity entity)
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Nombre"] = entity.Name,
            ["FaenaCodigo"] = entity.Faena.Code,
            ["TipoActivo"] = entity.AssetType,
            ["Familia"] = entity.Family.Name,
            ["Marca"] = entity.Brand,
            ["Modelo"] = entity.Model,
            ["NumeroSerie"] = entity.SerialNumber,
            ["Propiedad"] = entity.Ownership,
            ["Criticidad"] = entity.Criticality,
            ["EstadoDocumental"] = entity.DocumentStatus,
            ["EstadoOperacional"] = entity.OperationalState.Code,
            ["UbicacionTecnicaCodigo"] = entity.TechnicalLocationCode,
            ["Patente"] = entity.Plate
        };
    }

    private static string ToRecordStatus(AssetStatus status)
    {
        return status switch
        {
            AssetStatus.Active or AssetStatus.InMaintenance => "vigente",
            AssetStatus.Unavailable => "inactivo",
            AssetStatus.Retired => "obsoleto",
            AssetStatus.Draft => "no_vigente",
            _ => "vigente"
        };
    }

    private static AssetStatus ParseStatus(string? value, string? operationalStateCode = null)
    {
        if (Same(operationalStateCode, "FUERA_SERVICIO_TALLER"))
        {
            return AssetStatus.InMaintenance;
        }

        if (Same(operationalStateCode, "FUERA_SERVICIO_FAENA"))
        {
            return AssetStatus.Unavailable;
        }

        return value?.Trim().ToLowerInvariant() switch
        {
            "inactivo" => AssetStatus.Unavailable,
            "obsoleto" or "anulado" => AssetStatus.Retired,
            "no_vigente" => AssetStatus.Draft,
            _ => AssetStatus.Active
        };
    }

    private static string DefaultOperationalStateCode(AssetStatus status)
    {
        return status switch
        {
            AssetStatus.Active => "OPERATIVO_FAENA",
            AssetStatus.InMaintenance => "FUERA_SERVICIO_TALLER",
            AssetStatus.Unavailable or AssetStatus.Retired => "FUERA_SERVICIO_FAENA",
            AssetStatus.Draft => "ALERTA_FAENA",
            _ => "OPERATIVO_FAENA"
        };
    }

    private static string? NormalizeOperationalStateCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "OPERATIVO" or "OPERATIVO EN FAENA" or "OPERATIVO_FAENA" => "OPERATIVO_FAENA",
            "ALERTA" or "CON ALERTA EN FAENA" or "ALERTA_FAENA" => "ALERTA_FAENA",
            "FUERA DE SERVICIO EN FAENA" or "FUERA_SERVICIO_FAENA" => "FUERA_SERVICIO_FAENA",
            "EN MANTENIMIENTO" or "FUERA DE SERVICIO EN TALLER" or "FUERA_SERVICIO_TALLER" => "FUERA_SERVICIO_TALLER",
            var raw => raw.Replace(' ', '_')
        };
    }

    private async Task RecordAssetAuditAsync(
        UserAccessContext user,
        string action,
        string assetCode,
        string? faenaCodigo,
        string? previousValue,
        string? newValue,
        string? detail,
        CancellationToken cancellationToken)
    {
        await _auditService.RecordAsync(new AuditEventRequest(
            user.UserId,
            action,
            AuditModules.Assets,
            "Asset",
            assetCode,
            previousValue,
            newValue,
            faenaCodigo,
            action.Equals("StateChanged", StringComparison.OrdinalIgnoreCase) ? AuditSeverity.High : AuditSeverity.Medium,
            Detail: detail), cancellationToken);
    }

    private void EnsureCanMaintainAssets(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanAdminister(user) &&
            !user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para mantener activos.");
        }
    }

    private void EnsureCanChangeState(UserAccessContext user)
    {
        if (!_authorizationPolicyService.CanAdminister(user) &&
            !user.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase) &&
            !user.Roles.Contains(AuthRoles.MaintenanceSupervisor, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("El usuario no tiene permiso para cambiar estados de activos.");
        }
    }

    private void EnsureCanUseFaena(UserAccessContext user, string faenaCodigo)
    {
        if (!_authorizationPolicyService.CanViewFaena(user, faenaCodigo))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena indicada.");
        }
    }

    private void EnsureCanViewAsset(UserAccessContext user, AssetEntity asset)
    {
        if (!_authorizationPolicyService.CanViewFaena(user, asset.Faena.Code))
        {
            throw new UnauthorizedAccessException("El usuario no tiene acceso al activo solicitado.");
        }
    }

    private static void ValidateRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"El campo {fieldName} es obligatorio.");
        }
    }

    private static string NormalizeCode(string? value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static string? EmptyToNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool Same(string? left, string? right)
    {
        return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private static string Serialize(AssetEntity entity)
    {
        return JsonSerializer.Serialize(new
        {
            entity.Code,
            entity.Name,
            FaenaCodigo = entity.Faena?.Code,
            Familia = entity.Family?.Code,
            EstadoOperacional = entity.OperationalState?.Code,
            entity.RecordStatus,
            entity.AssetType,
            entity.TechnicalLocationCode,
            entity.Brand,
            entity.Model,
            entity.Plate,
            entity.SerialNumber,
            entity.Ownership,
            entity.Criticality,
            entity.DocumentStatus,
            entity.TechnicalSheetValidated
        });
    }
}

