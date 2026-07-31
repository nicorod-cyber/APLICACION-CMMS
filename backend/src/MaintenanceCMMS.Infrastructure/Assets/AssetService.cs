using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using MaintenanceCMMS.Application.Abstractions.Pagination;
using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Application.Faenas;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Documents;
using MaintenanceCMMS.Infrastructure.OperationalUnits;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Assets;

public sealed class AssetService : IAssetService
{
    private static readonly HashSet<string> Measurements = new(StringComparer.Ordinal) { "HOROMETRO", "KILOMETRAJE" };
    private static readonly HashSet<string> Sources = new(StringComparer.Ordinal) { "MANUAL", "ORDEN_TRABAJO", "IMPORTACION", "SAP", "TELEMETRIA" };
    private readonly CmmsDbContext _db;
    private readonly IAuditService _audit;
    private readonly IAuthorizationPolicyService _authorization;
    private readonly IDocumentaryWorkOrderService? _documentaryWorkOrders;
    public AssetService(CmmsDbContext db, IAuditService audit, IAuthorizationPolicyService authorization) : this(db, audit, authorization, null) { }
    public AssetService(CmmsDbContext db, IAuditService audit, IAuthorizationPolicyService authorization, IDocumentaryWorkOrderService? documentaryWorkOrders) => (_db, _audit, _authorization, _documentaryWorkOrders) = (db, audit, authorization, documentaryWorkOrders);

    public async Task<AssetCatalogResponse> GetCatalogAsync(UserAccessContext user, CancellationToken ct)
    {
        var types = await _db.AssetTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new AssetCatalogItem(x.Code, x.Name, null, null)).ToArrayAsync(ct);
        var families = await _db.EquipmentFamilies.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new AssetCatalogItem(x.Code, x.Name, x.AssetType.Code, null)).ToArrayAsync(ct);
        var states = await _db.AssetOperationalStates.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Severity).ThenBy(x => x.Code)
            .Select(x => new AssetCatalogItem(x.Code, x.Name, null, null)).ToArrayAsync(ct);
        var locations = (await _db.TechnicalLocations.AsNoTracking().Include(x => x.Faena).Where(x => !x.IsObsolete).OrderBy(x => x.Code).ToListAsync(ct))
            .Where(x => x.Faena is null || _authorization.CanViewFaena(user, x.Faena.Code))
            .Select(x => new AssetCatalogItem(x.Code, x.Name, null, x.Faena?.Code)).ToArray();
var criticalities = await _db.WorkCatalogs.AsNoTracking()
            .Where(x => x.Category == "WorkNotificationCriticality" && x.IsActive)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Code)
            .Select(x => new AssetCatalogItem(x.Code, x.Name, null, null)).ToArrayAsync(ct);
        return new AssetCatalogResponse(types, families, states, locations, criticalities);
    }

    public async Task<IReadOnlyCollection<AssetOperationalStateOption>> GetOperationalStatesAsync(string? tipoUbicacion, bool incluirTerminales, UserAccessContext user, CancellationToken ct)
    {
        var locationType = Code(tipoUbicacion);
        if (!string.IsNullOrWhiteSpace(locationType) && locationType is not "FAENA" and not "TALLER")
            throw new DomainException("El tipo de ubicaci?n debe ser FAENA o TALLER.");
        var allowed = string.IsNullOrWhiteSpace(locationType)
            ? AssetOperationalPolicy.Definitions.Select(item => item.Code).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : AssetOperationalPolicy.SelectableStateCodes(locationType, incluirTerminales).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(locationType) && !incluirTerminales) allowed.Remove(AssetOperationalPolicy.DecommissionedStateCode);
        return await _db.AssetOperationalStates.AsNoTracking()
            .Where(item => item.IsActive && allowed.Contains(item.Code))
            .OrderBy(item => item.Severity).ThenBy(item => item.Code)
            .Select(item => new AssetOperationalStateOption(item.Code, item.Name, item.Severity, AssetOperationalPolicy.IsAvailable(item.Code), AssetOperationalPolicy.IsExcludedFromOperationalUniverse(item.Code)))
            .ToArrayAsync(ct);
    }
    public async Task<IReadOnlyCollection<AssetAttributeDefinitionResponse>> GetApplicableDefinitionsAsync(string typeCode, string? familyCode, UserAccessContext user, CancellationToken ct)
    {
        var type = await _db.AssetTypes.AsNoTracking().SingleOrDefaultAsync(x => x.Code == Code(typeCode) && x.IsActive, ct) ?? throw new DomainException("Tipo de activo inexistente.");
        EquipmentFamilyEntity? family = null;
        if (!string.IsNullOrWhiteSpace(familyCode))
        {
            family = await _db.EquipmentFamilies.AsNoTracking().SingleOrDefaultAsync(x => x.Code == Code(familyCode) && x.IsActive, ct) ?? throw new DomainException("Familia inexistente.");
            if (family.AssetTypeId != type.Id) throw new DomainException("La familia no pertenece al tipo indicado.");
        }
        return (await DefinitionsAsync(type.Id, family?.Id, ct)).Select(ToDefinition).ToArray();
    }
    public async Task<IReadOnlyCollection<AssetSummary>> ListAsync(AssetListQuery query, UserAccessContext user, CancellationToken ct) => (await ListPageAsync(query with { Page = 1, PageSize = 100 }, user, ct)).Items;

    public async Task<PagedResponse<AssetSummary>> ListPageAsync(AssetListQuery query, UserAccessContext user, CancellationToken ct)
    {
        if (query.FaenaCodigo is not null && !_authorization.CanViewFaena(user, query.FaenaCodigo)) throw new UnauthorizedAccessException("No tiene acceso a la faena solicitada.");
        var page = Math.Max(1, query.Page); var pageSize = query.PageSize == int.MaxValue ? int.MaxValue : query.PageSize is 25 or 50 or 100 ? query.PageSize : 25;
        var canViewAll = user.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || user.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase);
        var authorizedFaenas = user.Faenas.Select(Code).Distinct().ToArray();
        IQueryable<AssetEntity> source = _db.Assets.AsNoTracking();
        if (!canViewAll) source = source.Where(a => a.FaenaId == null || (a.Faena != null && authorizedFaenas.Contains(a.Faena.Code)));
        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo)) source = source.Where(a => a.Faena != null && a.Faena.Code == Code(query.FaenaCodigo));
        if (!string.IsNullOrWhiteSpace(query.TipoActivoCodigo)) source = source.Where(a => a.AssetTypeDefinition.Code == Code(query.TipoActivoCodigo));
        if (!string.IsNullOrWhiteSpace(query.FamiliaEquipoCodigo)) source = source.Where(a => a.Family != null && a.Family.Code == Code(query.FamiliaEquipoCodigo));
        if (!string.IsNullOrWhiteSpace(query.Criticidad)) source = source.Where(a => a.Criticality != null && a.Criticality == query.Criticidad.Trim());
        if (!string.IsNullOrWhiteSpace(query.EstadoOperacionalCodigo)) source = source.Where(a => a.OperationalState.Code == Code(query.EstadoOperacionalCodigo));
        if (!string.IsNullOrWhiteSpace(query.Texto)) { var term=query.Texto.Trim(); source=source.Where(a=>EF.Functions.ILike(a.Code, "%" + term + "%") || EF.Functions.ILike(a.Name, "%" + term + "%")); }
        var total = await source.CountAsync(ct);
        var entities = await source.OrderBy(a => a.Code).ThenBy(a => a.Id).Skip((page - 1) * pageSize).Take(pageSize).Include(a => a.AssetTypeDefinition).Include(a => a.Family).Include(a => a.Faena).ThenInclude(f => f!.TechnicalLocation).Include(a => a.OperationalState).ToListAsync(ct);
        var items = await SummariesAsync(entities, ct);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new(items, page, pageSize, total, totalPages, page < totalPages, page > 1);
    }
    public async Task<PagedResponse<EquipmentOverviewRow>> ListEquipmentOverviewAsync(EquipmentOverviewQuery query, UserAccessContext user, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = query.PageSize == int.MaxValue ? int.MaxValue : query.PageSize is 25 or 50 or 100 ? query.PageSize : 25;
        var canViewAll = user.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase)
            || user.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase);
        var authorizedFaenas = user.Faenas.Select(Code).Distinct().ToArray();

        IQueryable<AssetEntity> assets = _db.Assets.AsNoTracking();
        IQueryable<OperationalUnitEntity> units = _db.OperationalUnits.AsNoTracking();

        if (!canViewAll)
        {
            assets = assets.Where(x => x.FaenaId == null || (x.Faena != null && authorizedFaenas.Contains(x.Faena.Code)));
            units = units.Where(x => x.FaenaId == null || (x.Faena != null && authorizedFaenas.Contains(x.Faena.Code)));
        }

        if (!string.IsNullOrWhiteSpace(query.FaenaCodigo))
        {
            var siteCode = Code(query.FaenaCodigo);
            assets = assets.Where(x => x.Faena != null && x.Faena.Code == siteCode);
            units = units.Where(x => x.Faena != null && x.Faena.Code == siteCode);
        }

        if (!string.IsNullOrWhiteSpace(query.Zona))
        {
            var zone = FaenaZones.NormalizeFilter(query.Zona);
            if (zone is null)
            {
                assets = assets.Where(_ => false);
                units = units.Where(_ => false);
            }
            else
            {
                assets = assets.Where(x => x.Faena != null && x.Faena.Zone == zone);
                units = units.Where(x => x.Faena != null && x.Faena.Zone == zone);
            }
        }

        if (!string.IsNullOrWhiteSpace(query.TipoActivoCodigo))
        {
            var assetTypeCode = Code(query.TipoActivoCodigo);
            assets = assets.Where(x => x.AssetTypeDefinition.Code == assetTypeCode);
            units = units.Where(_ => false);
        }

        if (!string.IsNullOrWhiteSpace(query.EstadoOperacionalCodigo))
        {
            var operationalStateCode = Code(query.EstadoOperacionalCodigo);
            assets = assets.Where(x => x.OperationalState.Code == operationalStateCode);
            units = units.Where(x => x.OperationalState.Code == operationalStateCode);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            assets = assets.Where(x => EF.Functions.ILike(x.Code, "%" + term + "%")
                || EF.Functions.ILike(x.Name, "%" + term + "%")
                || (x.SerialNumber != null && EF.Functions.ILike(x.SerialNumber, "%" + term + "%")));
            units = units.Where(x => EF.Functions.ILike(x.Code, "%" + term + "%") || EF.Functions.ILike(x.Name, "%" + term + "%"));
        }

        if (!string.IsNullOrWhiteSpace(query.TipoUbicacionFisica))
        {
            var physicalLocationType = Code(query.TipoUbicacionFisica);
            assets = assets.Where(x => _db.AssetPhysicalLocationPeriods.Any(p => p.AssetId == x.Id && p.ValidToUtc == null && p.LocationType == physicalLocationType));
            units = units.Where(unit => _db.OperationalUnitComponents.Any(component => component.OperationalUnitId == unit.Id && component.RemovedAtUtc == null && _db.AssetPhysicalLocationPeriods.Any(period => period.AssetId == component.AssetId && period.ValidToUtc == null && period.LocationType == physicalLocationType)));
        }

        if (!string.IsNullOrWhiteSpace(query.TallerCodigo))
        {
            var workshopCode = Code(query.TallerCodigo);
            assets = assets.Where(x => _db.AssetPhysicalLocationPeriods.Any(p => p.AssetId == x.Id && p.ValidToUtc == null && p.Workshop != null && p.Workshop.Code == workshopCode));
            units = units.Where(unit => _db.OperationalUnitComponents.Any(component => component.OperationalUnitId == unit.Id && component.RemovedAtUtc == null && _db.AssetPhysicalLocationPeriods.Any(period => period.AssetId == component.AssetId && period.ValidToUtc == null && period.Workshop != null && period.Workshop.Code == workshopCode)));
        }

        // No preventive or regulatory-document status is currently reliable as a unified projection.
        // A requested status therefore has no matching rows rather than presenting inferred information.
        if (!string.IsNullOrWhiteSpace(query.EstadoPreventivo) && string.IsNullOrWhiteSpace(query.EstadoDocumental))
        {
            assets = assets.Where(_ => false);
            units = units.Where(_ => false);
        }

        // A mounted component is represented by its operational unit. A component that was removed
        // stays visible as LOOSE_COMPONENT, while all other independent assets are ASSET.
        assets = assets.Where(x => !_db.OperationalUnitComponents.Any(c => c.AssetId == x.Id && c.RemovedAtUtc == null));

        var assetKeys = assets.Select(x => new
        {
            AssetId = (Guid?)x.Id,
            OperationalUnitId = (Guid?)null,
            RowType = _db.OperationalUnitComponents.Any(c => c.AssetId == x.Id) ? "LOOSE_COMPONENT" : "ASSET",
            x.Code,
            x.Name
        });
        var unitKeys = units.Select(x => new
        {
            AssetId = (Guid?)null,
            OperationalUnitId = (Guid?)x.Id,
            RowType = "COMPOSITE_UNIT",
            x.Code,
            x.Name
        });
        var keys = assetKeys.Concat(unitKeys);

        var allKeys = await keys.OrderBy(x => x.Name).ThenBy(x => x.Code).ThenBy(x => x.RowType).ThenBy(x => x.AssetId).ThenBy(x => x.OperationalUnitId).ToArrayAsync(ct);
        EquipmentOverviewDocumentProjection? documentProjection = null;
        var matchingKeys = allKeys;
        if (!string.IsNullOrWhiteSpace(query.EstadoDocumental))
        {
            var requestedStatus = NormalizeDocumentStatus(query.EstadoDocumental);
            var allAssetIds = allKeys.Where(x => x.AssetId.HasValue).Select(x => x.AssetId!.Value).ToArray();
            var allUnitIds = allKeys.Where(x => x.OperationalUnitId.HasValue).Select(x => x.OperationalUnitId!.Value).ToArray();
            documentProjection = await new EquipmentOverviewDocumentProjectionService(_db).ProjectAsync(allAssetIds, allUnitIds, ct);
            matchingKeys = allKeys.Where(key => documentProjection.Matches(key.AssetId, key.OperationalUnitId, requestedStatus)).ToArray();
        }
        var total = matchingKeys.Length;
        var pageKeys = matchingKeys.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        var assetIds = pageKeys.Where(x => x.AssetId.HasValue).Select(x => x.AssetId!.Value).ToArray();
        var unitIds = pageKeys.Where(x => x.OperationalUnitId.HasValue).Select(x => x.OperationalUnitId!.Value).ToArray();
        documentProjection ??= await new EquipmentOverviewDocumentProjectionService(_db).ProjectAsync(assetIds, unitIds, ct);

        var assetRows = await _db.Assets.AsNoTracking()
            .Where(x => assetIds.Contains(x.Id))
            .Select(x => new EquipmentOverviewRow(
                x.Id.ToString("D"),
                _db.OperationalUnitComponents.Any(c => c.AssetId == x.Id) ? "LOOSE_COMPONENT" : "ASSET",
                x.Id.ToString("D"),
                null,
                x.Code,
                x.Name,
                x.Faena == null ? null : x.Faena.Zone,
                x.FaenaId == null ? null : x.FaenaId.ToString(),
                x.Faena == null ? null : x.Faena.Code,
                x.Faena == null ? null : x.Faena.Name,
                x.OperationalState.Code,
                x.OperationalState.Name,
                _db.AssetPhysicalLocationPeriods.Where(p => p.AssetId == x.Id && p.ValidToUtc == null).Select(p => p.LocationType).FirstOrDefault(),
                _db.AssetPhysicalLocationPeriods.Where(p => p.AssetId == x.Id && p.ValidToUtc == null).Select(p => p.WorkshopId != null ? p.WorkshopId.ToString() : p.FaenaId != null ? p.FaenaId.ToString() : null).FirstOrDefault(),
                _db.AssetPhysicalLocationPeriods.Where(p => p.AssetId == x.Id && p.ValidToUtc == null).Select(p => p.Workshop != null ? p.Workshop.Name : p.Faena != null ? p.Faena.Name : null).FirstOrDefault(),
                _db.AssetPhysicalLocationPeriods.Where(p => p.AssetId == x.Id && p.ValidToUtc == null).Select(p => p.Workshop == null ? null : p.Workshop.Commune).FirstOrDefault(),
                _db.AssetPhysicalLocationPeriods.Where(p => p.AssetId == x.Id && p.ValidToUtc == null).Select(p => (DateTimeOffset?)p.ValidFromUtc).FirstOrDefault(),
                x.AssetTypeDefinition.Code,
                x.AssetTypeDefinition.Name,
                x.Brand,
                x.ManufacturingYear,
                x.UsageMeasurementType,
                _db.AssetReadings.Where(r => r.AssetId == x.Id && !r.IsAnomalous).OrderByDescending(r => r.ReadAtUtc).Select(r => (decimal?)r.Value).FirstOrDefault(),
                x.UsageMeasurementType == "HOROMETRO" ? "h" : x.UsageMeasurementType == "KILOMETRAJE" ? "km" : null,
                null,
                null,
                null,
                "No existe una regla preventiva agregada confiable para esta vista.",
                null,
                null,
                null,
                null,
                null))
            .ToArrayAsync(ct);

        var unitRows = await _db.OperationalUnits.AsNoTracking()
            .Where(x => unitIds.Contains(x.Id))
            .Select(x => new EquipmentOverviewRow(
                x.Id.ToString("D"),
                "COMPOSITE_UNIT",
                null,
                x.Id.ToString("D"),
                x.Code,
                x.Name,
                x.Faena == null ? null : x.Faena.Zone,
                x.FaenaId == null ? null : x.FaenaId.ToString(),
                x.Faena == null ? null : x.Faena.Code,
                x.Faena == null ? null : x.Faena.Name,
                x.OperationalState.Code,
                x.OperationalState.Name,
                null,
                null,
                null,
                null,
                null,
                x.OperationalUnitType.Code,
                x.OperationalUnitType.Name,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "No existe una consolidacion preventiva o documental aprobada para la unidad compuesta.",
                null,
                null,
                null,
                null,
                null))
            .ToArrayAsync(ct);

        var chassisByUnit = (await _db.OperationalUnitComponents.AsNoTracking()
            .Where(component => unitIds.Contains(component.OperationalUnitId) && component.RemovedAtUtc == null && component.ComponentRole.Code == "CHASIS")
            .Select(component => new { component.OperationalUnitId, component.Asset.Brand, component.Asset.ManufacturingYear })
            .ToArrayAsync(ct))
            .GroupBy(component => component.OperationalUnitId)
            .ToDictionary(group => group.Key, group => group.First());
        unitRows = unitRows.Select(row => Guid.TryParse(row.OperationalUnitId, out var unitId) && chassisByUnit.TryGetValue(unitId, out var chassis)
            ? row with { Brand = chassis.Brand, ManufacturingYear = chassis.ManufacturingYear }
            : row).ToArray();
        var componentLocations = await (from component in _db.OperationalUnitComponents.AsNoTracking()
                   join period in _db.AssetPhysicalLocationPeriods.AsNoTracking() on component.AssetId equals period.AssetId
                   where unitIds.Contains(component.OperationalUnitId) && component.RemovedAtUtc == null && period.ValidToUtc == null
                   select new
                   {
                       component.OperationalUnitId,
                       period.LocationType,
                       period.FaenaId,
                       FaenaName = period.Faena == null ? null : period.Faena.Name,
                       period.WorkshopId,
                       WorkshopName = period.Workshop == null ? null : period.Workshop.Name,
                       WorkshopCommune = period.Workshop == null ? null : period.Workshop.Commune,
                       period.ValidFromUtc
                   }).ToArrayAsync(ct);

        var locationsByUnit = componentLocations.GroupBy(location => location.OperationalUnitId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        unitRows = unitRows.Select(row =>
        {
            if (!Guid.TryParse(row.OperationalUnitId, out var unitId) || !locationsByUnit.TryGetValue(unitId, out var locations) || locations.Length == 0)
            {
                return row.SiteId is null || row.SiteName is null
                    ? row
                    : row with { PhysicalLocationType = "FAENA", PhysicalLocationId = row.SiteId, PhysicalLocationName = row.SiteName };
            }

            var distinctLocations = locations.Select(location => new { location.LocationType, LocationId = location.WorkshopId ?? location.FaenaId }).Distinct().ToArray();
            if (distinctLocations.Length != 1)
            {
                return row with { PhysicalLocationType = "INCONSISTENTE", PhysicalLocationName = "Ubicaci?n inconsistente" };
            }

            var location = locations.OrderByDescending(item => item.ValidFromUtc).First();
            return row with
            {
                PhysicalLocationType = location.LocationType,
                PhysicalLocationId = (location.WorkshopId ?? location.FaenaId)?.ToString(),
                PhysicalLocationName = location.WorkshopName ?? location.FaenaName,
                PhysicalLocationCommune = location.WorkshopCommune,
                PhysicalLocationSinceUtc = location.ValidFromUtc
            };
        }).ToArray();

        var componentsByUnit = (await _db.OperationalUnitComponents.AsNoTracking()
                .Where(x => unitIds.Contains(x.OperationalUnitId) && x.RemovedAtUtc == null)
                .OrderBy(x => x.ComponentRole.Code)
                .ThenBy(x => x.Asset.Code)
                .Select(x => new { x.OperationalUnitId, x.Asset.Code })
                .ToArrayAsync(ct))
                .GroupBy(x => x.OperationalUnitId)
                .ToDictionary(x => x.Key, x => (IReadOnlyCollection<string>)x.Select(component => component.Code).ToArray());

        var rowsById = assetRows.Concat(unitRows).ToDictionary(x => x.RowId);
        var items = pageKeys.Select(key =>
        {
            var rowId = key.AssetId?.ToString("D") ?? key.OperationalUnitId!.Value.ToString("D");
            var row = rowsById[rowId];
            row = key.AssetId.HasValue ? row with { TechnicalReview = documentProjection.ForAsset(key.AssetId.Value, RegulatoryDocumentCategory.TechnicalReview), Sernageomin = documentProjection.ForAsset(key.AssetId.Value, RegulatoryDocumentCategory.Sernageomin), Dgmn = documentProjection.ForAsset(key.AssetId.Value, RegulatoryDocumentCategory.Dgmn), FireSuppression = documentProjection.ForAsset(key.AssetId.Value, RegulatoryDocumentCategory.FireSuppression) } : row with { TechnicalReview = documentProjection.ForUnit(key.OperationalUnitId!.Value, RegulatoryDocumentCategory.TechnicalReview), Sernageomin = documentProjection.ForUnit(key.OperationalUnitId!.Value, RegulatoryDocumentCategory.Sernageomin), Dgmn = documentProjection.ForUnit(key.OperationalUnitId!.Value, RegulatoryDocumentCategory.Dgmn), FireSuppression = documentProjection.ForUnit(key.OperationalUnitId!.Value, RegulatoryDocumentCategory.FireSuppression) };
            return key.OperationalUnitId.HasValue && componentsByUnit.TryGetValue(key.OperationalUnitId.Value, out var components) ? row with { Components = components } : row;
        }).ToArray();

        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PagedResponse<EquipmentOverviewRow>(items, page, pageSize, total, totalPages, page < totalPages, page > 1);
    }
    public async Task<EquipmentOverviewSummary> GetEquipmentOverviewSummaryAsync(EquipmentOverviewQuery query, UserAccessContext user, CancellationToken ct)
    {
        var overview = await ListEquipmentOverviewAsync(query with { Page = 1, PageSize = int.MaxValue }, user, ct);
        var nonOperational = overview.Items.Count(row => !AssetOperationalPolicy.IsAvailable(row.OperationalStateCode) && !AssetOperationalPolicy.IsExcludedFromOperationalUniverse(row.OperationalStateCode));
        var directAssetIds = overview.Items.Where(row => Guid.TryParse(row.AssetId, out _)).Select(row => Guid.Parse(row.AssetId!)).ToArray();
        var unitIds = overview.Items.Where(row => Guid.TryParse(row.OperationalUnitId, out _)).Select(row => Guid.Parse(row.OperationalUnitId!)).ToArray();
        var componentAssetIds = unitIds.Length == 0 ? [] : await _db.OperationalUnitComponents.AsNoTracking().Where(component => unitIds.Contains(component.OperationalUnitId) && component.RemovedAtUtc == null).Select(component => component.AssetId).Distinct().ToArrayAsync(ct);
        var visibleAssetIds = directAssetIds.Concat(componentAssetIds).Distinct().ToArray();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var documents = visibleAssetIds.Length == 0 ? [] : await _db.DocumentAssets.AsNoTracking().Where(link => link.IsActive && visibleAssetIds.Contains(link.AssetId)).Include(link => link.Document).ThenInclude(document => document.DocumentType).Include(link => link.Document).ThenInclude(document => document.RequirementMatrixItem).Include(link => link.Document).ThenInclude(document => document.Versions).ThenInclude(version => version.File).Select(link => link.Document).Distinct().ToArrayAsync(ct);
        var expiring = documents.Count(document => document.IsCurrent && !document.IsHistorical && !document.IsAnnulled && !Same(document.Status, nameof(DocumentLifecycleStatus.Anulado)) && !Same(document.Status, nameof(DocumentLifecycleStatus.Reemplazado)) && DocumentComplianceCalculator.Evaluate(document.Status, document.Versions.SingleOrDefault(version => version.IsCurrent)?.ExpiresOn ?? document.ExpiresOn, document.RequirementMatrixItem?.AlertDays ?? document.DocumentType.AlertDays, document.Versions.Any(version => version.IsCurrent), document.BlocksAvailability, today).Status == DocumentLifecycleStatus.PorVencer);
        return new EquipmentOverviewSummary(overview.TotalCount, nonOperational, expiring);
    }
    public async Task<AssetDetail?> GetByIdAsync(string codigo, UserAccessContext user, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return null; View(user, asset); return await DetailAsync(asset, ct);
    }

    public async Task<AssetDetail> CreateAsync(CreateAssetRequest r, UserAccessContext u, CancellationToken ct)
    {
        Maintain(u); Require(r.Nombre, nameof(r.Nombre)); var code = await NextCodeAsync(ct);
        var refs = await ReferencesAsync(r.TipoActivoCodigo, r.FamiliaEquipoCodigo, r.FaenaCodigo, r.EstadoOperacionalCodigo, u, ct); ValidateDates(r.AnioFabricacion, r.FechaPuestaServicio, r.FechaBaja);
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var entity = new AssetEntity { Code = code, Name = r.Nombre.Trim(), AssetTypeId = refs.Type.Id, FamilyId = refs.Family?.Id, FaenaId = refs.Faena?.Id, OperationalStateId = refs.State.Id, Brand = Empty(r.Marca), Model = Empty(r.Modelo), SerialNumber = Empty(r.NumeroSerie), Ownership = Empty(r.Propiedad), Criticality = await CriticalityAsync(r.Criticidad, ct), ManufacturingYear = r.AnioFabricacion, AcquisitionDate = r.FechaAdquisicion, CommissioningDate = r.FechaPuestaServicio, DecommissioningDate = r.FechaBaja, UsageMeasurementType = Measurement(r.TipoMedicionUso), Observations = Empty(r.Observaciones) };
        _db.Assets.Add(entity);
        await AttributesAsync(entity, r.Atributos ?? [], refs.Type.Id, refs.Family?.Id, true, ct);
        _db.AssetLocationPeriods.Add(new AssetLocationPeriodEntity { AssetId = entity.Id, FaenaId = refs.Faena?.Id, ValidFromUtc = entity.CreatedAtUtc }); if (refs.Faena is not null) _db.AssetPhysicalLocationPeriods.Add(new AssetPhysicalLocationPeriodEntity { AssetId = entity.Id, LocationType = "FAENA", FaenaId = refs.Faena.Id, ValidFromUtc = entity.CreatedAtUtc, RegisteredByUserId = u.UserId, Reason = "Ubicacion inicial" });
        await _db.SaveChangesAsync(ct);
        await SyncIdentifierAliasesAsync(entity, ct);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await AuditAsync(u, "asset.created", entity, null, entity, ct); return (await GetByIdAsync(code, u, ct))!;
    }
    public async Task<AssetDetail?> UpdateAsync(string codigo, UpdateAssetRequest r, UserAccessContext u, CancellationToken ct)
    {
        Maintain(u); Require(r.Nombre, nameof(r.Nombre)); var asset = await FindAsync(codigo, true, ct); if (asset is null) return null; View(u, asset);
        var refs = await ReferencesAsync(r.TipoActivoCodigo, r.FamiliaEquipoCodigo, r.FaenaCodigo, r.EstadoOperacionalCodigo, u, ct); var measurement = Measurement(r.TipoMedicionUso);
        if (asset.FaenaId != refs.Faena?.Id) throw new DomainException("La faena no se edita directamente. Use el flujo de traslado con fecha efectiva, motivo y responsable.");
        if (asset.OperationalStateId != refs.State.Id) throw new DomainException("El estado operacional no se edita directamente. Registre un evento de transicion.");
        if (!Same(asset.UsageMeasurementType, measurement) && await _db.AssetReadings.AnyAsync(x => x.AssetId == asset.Id, ct)) throw new DomainException("No se puede cambiar el tipo de medicion cuando existen lecturas.");
        ValidateDates(r.AnioFabricacion, r.FechaPuestaServicio, r.FechaBaja); var old = AuditValue(asset);
        asset.Name = r.Nombre.Trim(); asset.AssetTypeId = refs.Type.Id; asset.FamilyId = refs.Family?.Id; asset.Brand = Empty(r.Marca); asset.Model = Empty(r.Modelo); asset.SerialNumber = Empty(r.NumeroSerie); asset.Ownership = Empty(r.Propiedad); asset.Criticality = await CriticalityAsync(r.Criticidad, ct); asset.ManufacturingYear = r.AnioFabricacion; asset.AcquisitionDate = r.FechaAdquisicion; asset.CommissioningDate = r.FechaPuestaServicio; asset.UsageMeasurementType = measurement; asset.Observations = Empty(r.Observaciones); asset.UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (r.Atributos is not null) await AttributesAsync(asset, r.Atributos, refs.Type.Id, refs.Family?.Id, true, ct);
        await _db.SaveChangesAsync(ct); await SyncIdentifierAliasesAsync(asset, ct); await _db.SaveChangesAsync(ct);
        await AuditAsync(u, "asset.updated", asset, old, asset, ct); return await GetByIdAsync(asset.Code, u, ct);
    }

    public async Task<AssetStateEventResponse?> AddStateEventAsync(string codigo, CreateAssetStateEventRequest r, UserAccessContext u, CancellationToken ct)
    {
        Maintain(u); Require(r.EstadoOperacionalCodigo, nameof(r.EstadoOperacionalCodigo)); Require(r.Motivo, nameof(r.Motivo));
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var asset = await FindAsync(codigo, true, ct); if (asset is null) return null; View(u, asset);
        var state = await _db.AssetOperationalStates.SingleOrDefaultAsync(x => x.Code == Code(r.EstadoOperacionalCodigo) && x.IsActive, ct) ?? throw new DomainException("Estado operacional inexistente.");
        AssetOperationalPolicy.EnsureTransitionAllowed(asset.OperationalState.Code, state.Code);
        var physicalLocation = await _db.AssetPhysicalLocationPeriods.AsNoTracking().SingleOrDefaultAsync(x => x.AssetId == asset.Id && x.ValidToUtc == null, ct);
        AssetOperationalPolicy.EnsureCompatibleWithPhysicalLocation(asset.Code, physicalLocation?.LocationType, state.Code, state.Name);
        var previous = asset.OperationalState; var occurred = r.FechaEventoUtc ?? DateTimeOffset.UtcNow;
        var antecedent = await ValidateStateEventAntecedentAsync(asset, r, u, ct);
        asset.OperationalStateId = state.Id; asset.OperationalState = state; asset.UpdatedAtUtc = DateTimeOffset.UtcNow;
        if (Same(state.Code, AssetOperationalPolicy.DecommissionedStateCode)) asset.DecommissioningDate ??= DateOnly.FromDateTime(occurred.UtcDateTime);
        var evt = new AssetStateEventEntity { AssetId = asset.Id, PreviousStateId = previous.Id, NewStateId = state.Id, OccurredAtUtc = occurred, UserId = u.UserId, Reason = r.Motivo.Trim(), ReferenceType = antecedent.Type, ReferenceId = antecedent.Id, ReferenceText = antecedent.Reference };
        _db.AssetStateEvents.Add(evt);
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('cmms.asset_state_event_id', {evt.Id.ToString("D")}, true)", ct);
        await OperationalUnitStateCalculator.RecalculateForAssetAsync(_db, asset.Id, $"{antecedent.Type}:{antecedent.Id ?? antecedent.Reference} {r.Motivo}".Trim(), ct);
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await AuditAsync(u, "asset.operational_state.changed", asset, new { Estado = previous.Code }, new { Estado = state.Code, r.Motivo, antecedent.Type, antecedent.Id, antecedent.Reference }, ct);
        return new(evt.Id.ToString("D"), asset.Code, previous.Code, state.Code, evt.OccurredAtUtc, evt.Reason, u.UserId, evt.ReferenceType, evt.ReferenceId, evt.ReferenceText);
    }

    public async Task<IReadOnlyCollection<AssetTransferResponse>> TransferAsync(string codigo, TransferAssetRequest r, UserAccessContext u, CancellationToken ct)
    {
        if (!_authorization.CanChangeAssetFaena(u)) throw new UnauthorizedAccessException("No tiene permiso para trasladar activos entre faenas.");
        Require(r.FaenaDestinoCodigo, nameof(r.FaenaDestinoCodigo)); Require(r.Motivo, nameof(r.Motivo));
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (_db.Database.IsNpgsql()) await _db.Database.ExecuteSqlRawAsync("LOCK TABLE vigencias_ubicacion_activo IN SHARE ROW EXCLUSIVE MODE", ct);
        var asset = await FindAsync(codigo, true, ct) ?? throw new DomainException("Activo inexistente."); View(u, asset);
        var destination = await _db.Faenas.Include(x => x.TechnicalLocation).SingleOrDefaultAsync(x => x.Code == Code(r.FaenaDestinoCodigo) && x.IsActive, ct) ?? throw new DomainException("Faena destino inexistente.");
        if (!_authorization.CanViewFaena(u, destination.Code)) throw new UnauthorizedAccessException("No tiene acceso a la faena destino.");
        if (destination.TechnicalLocation is null) throw new DomainException("La faena destino no tiene ubicaci?n t?cnica configurada.");
        if (asset.FaenaId == destination.Id) throw new DomainException("El activo ya pertenece a la faena destino.");
        AssetOperationalStateEntity? destinationState = null;
        if (!string.IsNullOrWhiteSpace(r.EstadoOperacionalDestinoCodigo))
        {
            destinationState = await _db.AssetOperationalStates.SingleOrDefaultAsync(x => x.Code == Code(r.EstadoOperacionalDestinoCodigo) && x.IsActive, ct) ?? throw new DomainException("El estado operacional destino no existe o estÃƒÆ’Ã‚Â¡ inactivo.");
            AssetOperationalPolicy.EnsureCompatibleWithPhysicalLocation(asset.Code, "FAENA", destinationState.Code, destinationState.Name);
        }
        var activeComponent = await _db.OperationalUnitComponents.Include(x => x.OperationalUnit).SingleOrDefaultAsync(x => x.AssetId == asset.Id && x.RemovedAtUtc == null, ct);
        var assets = new List<AssetEntity> { asset }; OperationalUnitEntity? unit = null;
        if (activeComponent is not null)
        {
            if (!r.TrasladarUnidadCompleta) throw new DomainException("El activo estÃƒÆ’Ã‚Â¡ montado. Traslade la unidad completa o desmonte previamente el componente.");
            unit = activeComponent.OperationalUnit;
            assets = await _db.OperationalUnitComponents.Include(x => x.Asset).ThenInclude(x => x.Faena).Where(x => x.OperationalUnitId == unit.Id && x.RemovedAtUtc == null).Select(x => x.Asset).ToListAsync(ct);
            if (assets.Any(x => x.FaenaId != asset.FaenaId)) throw new DomainException("La unidad contiene componentes con inconsistencia territorial; corrija la composiciÃƒÆ’Ã‚Â³n antes del traslado.");
            unit.FaenaId = destination.Id;
        }
        var results = new List<AssetTransferResponse>();
        foreach (var item in assets.DistinctBy(x => x.Id)) results.Add(await TransferCoreAsync(item, destination, unit, destinationState, r, u, ct));
        await _db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('cmms.asset_transfer_ids', {string.Join(",", results.Select(x => x.TrasladoId))}, true)", ct);
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        foreach (var item in assets) await AuditAsync(u, "asset.transferred", item, new { Faena = results.Single(x => x.ActivoCodigo == item.Code).FaenaOrigenCodigo }, new { Faena = destination.Code, Estado = destinationState?.Code ?? item.OperationalState.Code, r.FechaEfectivaUtc, r.Motivo, Unidad = unit?.Code }, ct);
        if (_documentaryWorkOrders is not null)
        {
            var documentaryRun = await _documentaryWorkOrders.RunAsync(DateOnly.FromDateTime(DateTime.UtcNow), u.UserId, ct);
            foreach (var item in assets) await AuditAsync(u, "asset.documentary_compliance_reevaluated", item, new { Faena = results.Single(x => x.ActivoCodigo == item.Code).FaenaOrigenCodigo }, new { Faena = destination.Code, MatricesProcesadas = documentaryRun.ActivosEvaluados, OrdenesCreadas = documentaryRun.OrdenesCreadas, RequisitosCreados = documentaryRun.RequisitosCreados }, ct);
        }
        return results;
    }
    public async Task<AssetStateEventAntecedentSearchResponse> SearchStateEventAntecedentsAsync(string codigo, string origen, string? texto, int pagina, int tamanoPagina, UserAccessContext u, CancellationToken ct)
    {
        Maintain(u);
        var asset = await FindAsync(codigo, false, ct) ?? throw new DomainException("Activo inexistente.");
        View(u, asset);
        var type = AntecedentType(origen, false)!;
        if (type is "NONE" or "OTHER") throw new DomainException("El origen seleccionado no requiere b?squeda de antecedentes.");
        var term = Empty(texto); var page = Math.Max(1, pagina); var size = Math.Clamp(tamanoPagina, 1, 50);
        var items = type switch
        {
            "WORK_ORDER" => await SearchWorkOrdersAsync(asset, term, ct),
            "NOTICE" => await SearchNoticesAsync(asset, term, ct),
            "DOCUMENT" => await SearchDocumentsAsync(asset, term, ct),
            "TRANSFER" => await SearchTransfersAsync(asset, term, ct),
            _ => throw new DomainException("El origen de cambio seleccionado no estÃƒÆ’Ã‚Â¡ disponible.")
        };
        return new AssetStateEventAntecedentSearchResponse(items.Skip((page - 1) * size).Take(size).ToArray(), items.Count, page, size);
    }

    private async Task<(string? Type, string? Id, string? Reference)> ValidateStateEventAntecedentAsync(AssetEntity asset, CreateAssetStateEventRequest request, UserAccessContext user, CancellationToken ct)
    {
        var type = AntecedentType(request.TipoAntecedente, true); var id = Empty(request.AntecedenteId); var reference = Empty(request.ReferenciaAntecedente);
        if (type is null)
        {
            if (id is not null || reference is not null) throw new DomainException("Debe seleccionar un origen de cambio para indicar un antecedente.");
            return (null, null, null);
        }
        if (type == "NONE")
        {
            if (id is not null || reference is not null) throw new DomainException("Sin antecedente no admite un registro relacionado.");
            return (null, null, null);
        }
        if (type == "OTHER")
        {
            if (id is not null) throw new DomainException("El origen Otro no admite un identificador t?cnico.");
            if (reference is null) throw new DomainException("Debe indicar la referencia o antecedente.");
            return (type, null, reference);
        }
        if (reference is not null) throw new DomainException("La referencia descriptiva solo se permite para el origen Otro.");
        if (id is null) throw new DomainException("Debe seleccionar un antecedente relacionado.");
        if (!Guid.TryParse(id, out var antecedentId)) throw new DomainException("El antecedente seleccionado es inv?lido.");
        switch (type)
        {
            case "WORK_ORDER":
            {
                var item = await _db.WorkOrders.Include(x => x.Faena).Include(x => x.Status).SingleOrDefaultAsync(x => x.Id == antecedentId, ct) ?? throw new DomainException("El antecedente seleccionado no existe.");
                if (item.AnnulledAtUtc is not null || Same(item.Status.Code, "Anulada")) throw new DomainException("La orden de trabajo seleccionada estÃƒÆ’Ã‚Â¡ anulada.");
                if (item.AssetId != asset.Id && !await _db.WorkOrderAssets.AnyAsync(x => x.WorkOrderId == item.Id && x.AssetId == asset.Id, ct)) throw new DomainException("La orden de trabajo seleccionada no corresponde al activo.");
                EnsureAntecedentFaena(asset, item.Faena, user); break;
            }
            case "NOTICE":
            {
                var item = await _db.WorkNotifications.Include(x => x.Faena).Include(x => x.Status).SingleOrDefaultAsync(x => x.Id == antecedentId, ct) ?? throw new DomainException("El antecedente seleccionado no existe.");
                if (item.AnnulledAtUtc is not null || Same(item.Status.Code, "Anulado")) throw new DomainException("El aviso seleccionado estÃƒÆ’Ã‚Â¡ anulado.");
                if (item.AssetId != asset.Id) throw new DomainException("El aviso seleccionado no corresponde al activo.");
                EnsureAntecedentFaena(asset, item.Faena, user); break;
            }
            case "DOCUMENT":
            {
                var item = await _db.Documents.Include(x => x.Assets).SingleOrDefaultAsync(x => x.Id == antecedentId, ct) ?? throw new DomainException("El antecedente seleccionado no existe.");
                if (item.IsAnnulled || item.IsHistorical || !item.IsCurrent || Same(item.Status, "Anulado") || Same(item.Status, "Reemplazado")) throw new DomainException("El documento seleccionado no estÃƒÆ’Ã‚Â¡ vigente para usarse como antecedente.");
                if (!item.Assets.Any(x => x.AssetId == asset.Id && x.IsActive)) throw new DomainException("El documento seleccionado no corresponde al activo.");
                break;
            }
            case "TRANSFER":
            {
                var item = await _db.AssetTransfers.SingleOrDefaultAsync(x => x.Id == antecedentId, ct) ?? throw new DomainException("El antecedente seleccionado no existe.");
                if (item.AssetId != asset.Id) throw new DomainException("El traslado seleccionado no corresponde al activo.");
                break;
            }
            default: throw new DomainException("El origen de cambio seleccionado no es v?lido.");
        }
        return (type, antecedentId.ToString("D"), null);
    }

    private void EnsureAntecedentFaena(AssetEntity asset, FaenaEntity faena, UserAccessContext user)
    {
        if (!_authorization.CanViewFaena(user, faena.Code)) throw new UnauthorizedAccessException("No tiene acceso a la faena del antecedente seleccionado.");
        if (asset.FaenaId != faena.Id) throw new DomainException("El antecedente seleccionado no corresponde a la faena vigente del activo.");
    }

    private async Task<List<AssetStateEventAntecedentSearchItem>> SearchWorkOrdersAsync(AssetEntity asset, string? term, CancellationToken ct)
    {
        var query = _db.WorkOrders.AsNoTracking().Include(x => x.Status).Include(x => x.Faena).Where(x => (x.AssetId == asset.Id || x.RelatedAssets.Any(a => a.AssetId == asset.Id)) && x.FaenaId == asset.FaenaId && x.AnnulledAtUtc == null && x.Status.Code != "Anulada");
        if (term is not null) query = query.Where(x => x.WorkOrderNumber.Contains(term) || x.Description.Contains(term));
        return (await query.OrderByDescending(x => x.ScheduledAtUtc ?? x.CreatedAtUtc).ThenBy(x => x.WorkOrderNumber).ToListAsync(ct)).Select(x => new AssetStateEventAntecedentSearchItem(x.Id.ToString("D"), x.WorkOrderNumber, x.Description, x.ScheduledAtUtc ?? x.CreatedAtUtc, x.Status.Code, asset.Code, x.Faena.Code)).ToList();
    }

    private async Task<List<AssetStateEventAntecedentSearchItem>> SearchNoticesAsync(AssetEntity asset, string? term, CancellationToken ct)
    {
        var query = _db.WorkNotifications.AsNoTracking().Include(x => x.Status).Include(x => x.Faena).Where(x => x.AssetId == asset.Id && x.FaenaId == asset.FaenaId && x.AnnulledAtUtc == null && x.Status.Code != "Anulado");
        if (term is not null) query = query.Where(x => x.NotificationNumber.Contains(term) || x.Description.Contains(term));
        return (await query.OrderByDescending(x => x.DetectedAtUtc).ThenBy(x => x.NotificationNumber).ToListAsync(ct)).Select(x => new AssetStateEventAntecedentSearchItem(x.Id.ToString("D"), x.NotificationNumber, x.Description, x.DetectedAtUtc, x.Status.Code, asset.Code, x.Faena.Code)).ToList();
    }

    private async Task<List<AssetStateEventAntecedentSearchItem>> SearchDocumentsAsync(AssetEntity asset, string? term, CancellationToken ct)
    {
        var query = _db.Documents.AsNoTracking().Include(x => x.DocumentType).Include(x => x.Versions).Where(x => x.Assets.Any(a => a.AssetId == asset.Id && a.IsActive) && !x.IsAnnulled && !x.IsHistorical && x.IsCurrent && x.Status != "Anulado" && x.Status != "Reemplazado");
        if (term is not null) query = query.Where(x => x.Code.Contains(term) || x.Title.Contains(term) || x.DocumentType.Code.Contains(term) || x.DocumentType.Name.Contains(term) || x.Status.Contains(term));
        return (await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Code).ToListAsync(ct)).Select(x => new AssetStateEventAntecedentSearchItem(x.Id.ToString("D"), x.Code, x.Title, x.CreatedAtUtc, x.Status, asset.Code, asset.Faena?.Code, $"{x.DocumentType.Code} ? v{x.Versions.Where(v => v.IsCurrent).OrderByDescending(v => v.VersionNumber).Select(v => v.VersionNumber).FirstOrDefault()}{(x.ExpiresOn is null ? string.Empty : $" ? vence {x.ExpiresOn:dd/MM/yyyy}")}" )).ToList();
    }

    private async Task<List<AssetStateEventAntecedentSearchItem>> SearchTransfersAsync(AssetEntity asset, string? term, CancellationToken ct)
    {
        var query = _db.AssetTransfers.AsNoTracking().Include(x => x.OriginFaena).Include(x => x.DestinationFaena).Where(x => x.AssetId == asset.Id);
        if (term is not null) query = query.Where(x => (x.OriginFaena != null && x.OriginFaena.Code.Contains(term)) || (x.DestinationFaena != null && x.DestinationFaena.Code.Contains(term)) || x.Reason.Contains(term));
        return (await query.OrderByDescending(x => x.EffectiveAtUtc).ToListAsync(ct)).Select(x => new AssetStateEventAntecedentSearchItem(x.Id.ToString("D"), "Traslado", x.Reason, x.EffectiveAtUtc, null, asset.Code, asset.Faena?.Code, $"{x.OriginFaena?.Code ?? "Sin faena"} ? {x.DestinationFaena?.Code ?? "Sin faena"}")).ToList();
    }

    private static string? AntecedentType(string? value, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(value)) return allowEmpty ? null : throw new DomainException("Seleccione un origen de cambio v?lido.");
        return Code(value) switch
        {
            "NONE" or "SIN_ANTECEDENTE" => "NONE", "WORK_ORDER" or "OT" or "ORDEN_TRABAJO" => "WORK_ORDER", "NOTICE" or "AVISO" => "NOTICE", "DOCUMENT" or "DOCUMENTO" => "DOCUMENT", "TRANSFER" or "TRASLADO" => "TRANSFER", "OTHER" or "OTRO" => "OTHER",
            "INSPECTION" or "INSPECCION" => throw new DomainException("No existe un m?dulo de inspecciones disponible para usar como antecedente."), _ => throw new DomainException("El origen de cambio seleccionado no es v?lido.")
        };
    }
    private async Task<AssetTransferResponse> TransferCoreAsync(AssetEntity asset, FaenaEntity destination, OperationalUnitEntity? unit, AssetOperationalStateEntity? destinationState, TransferAssetRequest r, UserAccessContext u, CancellationToken ct)
    {
        var current = await _db.AssetLocationPeriods.SingleOrDefaultAsync(x => x.AssetId == asset.Id && x.ValidToUtc == null, ct);
        if (current is not null && r.FechaEfectivaUtc <= current.ValidFromUtc) throw new DomainException($"La fecha efectiva del traslado de {asset.Code} debe ser posterior al inicio de su ubicaci?n vigente.");
        if (await _db.AssetTransfers.AnyAsync(x => x.AssetId == asset.Id && x.EffectiveAtUtc >= r.FechaEfectivaUtc, ct)) throw new DomainException($"El traslado de {asset.Code} se superpone con historia posterior.");
        var physical = await _db.AssetPhysicalLocationPeriods.SingleOrDefaultAsync(x => x.AssetId == asset.Id && x.ValidToUtc == null, ct) ?? throw new DomainException($"El activo '{asset.Code}' no tiene ubicaci?n f?sica vigente.");
        if (!Same(physical.LocationType, "FAENA")) throw new DomainException($"El activo '{asset.Code}' estÃƒÆ’Ã‚Â¡ en taller; utilice el flujo de retorno a faena.");
        var state = destinationState ?? asset.OperationalState;
        AssetOperationalPolicy.EnsureCompatibleWithPhysicalLocation(asset.Code, "FAENA", state.Code, state.Name);
        if (AssetOperationalPolicy.IsExcludedFromOperationalUniverse(asset.OperationalState.Code) && !Same(asset.OperationalState.Code, state.Code))
            throw new DomainException($"El activo '{asset.Code}' estÃƒÆ’Ã‚Â¡ dado de baja y debe conservar ese estado durante el traslado fÃƒÆ’Ã‚Â­sico.");
        var transfer = new AssetTransferEntity { AssetId = asset.Id, OriginFaenaId = asset.FaenaId, DestinationFaenaId = destination.Id, OperationalUnitId = unit?.Id, EffectiveAtUtc = r.FechaEfectivaUtc, Reason = r.Motivo.Trim(), UserId = u.UserId, RegisteredAtUtc = DateTimeOffset.UtcNow, Observations = Empty(r.Observaciones) };
        _db.AssetTransfers.Add(transfer);
        if (current is not null) current.ValidToUtc = r.FechaEfectivaUtc;
        _db.AssetLocationPeriods.Add(new AssetLocationPeriodEntity { AssetId = asset.Id, FaenaId = destination.Id, ValidFromUtc = r.FechaEfectivaUtc, TransferId = transfer.Id });
        physical.ValidToUtc = r.FechaEfectivaUtc;
        _db.AssetPhysicalLocationPeriods.Add(new AssetPhysicalLocationPeriodEntity { AssetId = asset.Id, LocationType = "FAENA", FaenaId = destination.Id, ValidFromUtc = r.FechaEfectivaUtc, RegisteredByUserId = u.UserId, Reason = r.Motivo, OperationalUnitId = unit?.Id, Observations = Empty(r.Observaciones) });
        var origin = asset.Faena?.Code; asset.FaenaId = destination.Id; asset.Faena = destination; asset.UpdatedAtUtc = DateTimeOffset.UtcNow;
        var previous = asset.OperationalState;
        if (!Same(previous.Code, state.Code))
        {
            AssetOperationalPolicy.EnsureTransitionAllowed(previous.Code, state.Code);
            asset.OperationalStateId = state.Id; asset.OperationalState = state;
            _db.AssetStateEvents.Add(new AssetStateEventEntity { AssetId = asset.Id, PreviousStateId = previous.Id, NewStateId = state.Id, OccurredAtUtc = r.FechaEfectivaUtc, UserId = u.UserId, Reason = r.Motivo.Trim(), ReferenceType = "TRANSFER", ReferenceId = transfer.Id.ToString("D"), ReferenceText = null });
        }
        await OperationalUnitStateCalculator.RecalculateForAssetAsync(_db, asset.Id, $"TRANSFER:{transfer.Id:D} {r.Motivo}", ct);
        return new(transfer.Id.ToString("D"), asset.Code, origin, destination.Code, transfer.EffectiveAtUtc, transfer.Reason, transfer.UserId, transfer.RegisteredAtUtc, transfer.Observations, unit?.Code);
    }
    public async Task<IReadOnlyCollection<AssetReadingResponse>> GetReadingsAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return []; View(u, asset); return MapReadings(await ValidReadingsAsync(asset.Id, ct), asset.UsageMeasurementType).OrderByDescending(x => x.FechaLecturaUtc).ToArray();
    }

    public async Task<AssetReadingResponse> AddReadingAsync(string codigo, CreateAssetReadingRequest r, UserAccessContext u, CancellationToken ct)
    {
        RegisterReadings(u); var asset = await FindAsync(codigo, true, ct) ?? throw new DomainException("Activo inexistente."); View(u, asset); AssetReadingPolicy.EnsureCanRegister(asset, r.Valor, r.FechaLecturaUtc, "nuevas lecturas");
        var valid = await ValidReadingsAsync(asset.Id, ct); var last = valid.OrderByDescending(x => x.ReadAtUtc).ThenByDescending(x => x.CreatedAtUtc).FirstOrDefault(); if (last is not null && r.Valor < last.Value) throw new DomainException("Una lectura normal no puede disminuir.");
        var reading = new AssetReadingEntity { AssetId = asset.Id, ReadAtUtc = r.FechaLecturaUtc ?? DateTimeOffset.UtcNow, Value = r.Valor, Source = Source(r.Origen), RegisteredByUserId = u.UserId, EvidenceReference = Empty(r.EvidenciaReferencia), Observations = Empty(r.Observaciones) }; _db.AssetReadings.Add(reading); await _db.SaveChangesAsync(ct); return MapReadings([.. valid, reading], asset.UsageMeasurementType).Single(x => x.Id == reading.Id.ToString("D"));
    }

    public async Task<AssetReadingResponse> CorrectReadingAsync(string codigo, string readingId, CorrectAssetReadingRequest r, UserAccessContext u, CancellationToken ct)
    {
        CorrectReadings(u); Require(r.MotivoCorreccion, nameof(r.MotivoCorreccion)); if (!Guid.TryParse(readingId, out var id)) throw new DomainException("Lectura invalida.");
        var asset = await FindAsync(codigo, true, ct) ?? throw new DomainException("Activo inexistente."); View(u, asset); AssetReadingPolicy.EnsureCanRegister(asset, r.Valor, r.FechaLecturaUtc, "correcciones de lecturas"); var original = await _db.AssetReadings.SingleOrDefaultAsync(x => x.Id == id && x.AssetId == asset.Id, ct) ?? throw new DomainException("Lectura inexistente."); if (await _db.AssetReadings.AnyAsync(x => x.CorrectedReadingId == original.Id, ct)) throw new DomainException("La lectura ya fue corregida.");
        var correction = new AssetReadingEntity { AssetId = asset.Id, ReadAtUtc = r.FechaLecturaUtc ?? DateTimeOffset.UtcNow, Value = r.Valor, Source = Source(r.Origen), RegisteredByUserId = u.UserId, EvidenceReference = Empty(r.EvidenciaReferencia), Observations = Empty(r.Observaciones), IsCorrection = true, CorrectedReadingId = original.Id, CorrectionReason = r.MotivoCorreccion.Trim(), AuthorizedByUserId = u.UserId }; _db.AssetReadings.Add(correction); await _db.SaveChangesAsync(ct); return MapReadings(await ValidReadingsAsync(asset.Id, ct), asset.UsageMeasurementType).Single(x => x.Id == correction.Id.ToString("D"));
    }

    public async Task<AssetPhysicalLocationResponse?> GetPhysicalLocationAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return null; View(u, asset);
        var location = await PhysicalLocationQuery().SingleOrDefaultAsync(x => x.AssetId == asset.Id && x.ValidToUtc == null, ct);
        return location is null ? null : ToPhysicalLocation(location, [asset.Code]);
    }

    public async Task<IReadOnlyCollection<AssetPhysicalLocationResponse>> GetPhysicalLocationHistoryAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct) ?? throw new DomainException("Activo inexistente."); View(u, asset);
        return (await PhysicalLocationQuery().Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.ValidFromUtc).ToListAsync(ct)).Select(x => ToPhysicalLocation(x, [asset.Code])).ToArray();
    }

    public Task<IReadOnlyCollection<AssetPhysicalLocationResponse>> RegisterWorkshopEntryAsync(string codigo, RegisterWorkshopEntryRequest r, UserAccessContext u, CancellationToken ct) => MovePhysicalLocationAsync(codigo, "TALLER", r.TallerCodigo, r.FechaEfectivaUtc, r.EstadoOperacionalDestinoCodigo, r.OrdenTrabajoId, r.Motivo, r.Observaciones, u, ct);
    public Task<IReadOnlyCollection<AssetPhysicalLocationResponse>> RegisterReturnToSiteAsync(string codigo, RegisterReturnToSiteRequest r, UserAccessContext u, CancellationToken ct) => MovePhysicalLocationAsync(codigo, "FAENA", null, r.FechaEfectivaUtc, r.EstadoOperacionalDestinoCodigo, r.OrdenTrabajoId, r.Motivo, r.Observaciones, u, ct);

    private async Task<IReadOnlyCollection<AssetPhysicalLocationResponse>> MovePhysicalLocationAsync(string codigo, string targetType, string? workshopCode, DateTimeOffset effectiveAt, string? destinationStateCode, string? workOrderNumber, string? reason, string? observations, UserAccessContext u, CancellationToken ct)
    {
        Maintain(u); if (effectiveAt == default) throw new DomainException("La fecha efectiva es obligatoria.");
        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (_db.Database.IsNpgsql()) await _db.Database.ExecuteSqlRawAsync("LOCK TABLE vigencias_ubicacion_fisica_activo IN SHARE ROW EXCLUSIVE MODE", ct);
        var asset = await FindAsync(codigo, true, ct) ?? throw new DomainException("Activo inexistente."); View(u, asset);
        WorkshopEntity? workshop = null; FaenaEntity? faena = null;
        if (targetType == "TALLER")
        {
            Require(workshopCode, nameof(workshopCode));
            workshop = await _db.Workshops.Include(x => x.SupervisorUser).SingleOrDefaultAsync(x => x.Code == Code(workshopCode) && x.IsActive, ct) ?? throw new DomainException("El taller no existe o estÃƒÆ’Ã‚Â¡ inactivo.");
            if (string.IsNullOrWhiteSpace(workshop.Commune) || workshop.SupervisorUser is null) throw new DomainException("El taller no estÃƒÆ’Ã‚Â¡ habilitado operacionalmente: requiere comuna y supervisor.");
        }
        else faena = asset.Faena ?? throw new DomainException("El activo no tiene faena asignada.");
        WorkOrderEntity? order = null;
        if (!string.IsNullOrWhiteSpace(workOrderNumber))
        {
            order = await _db.WorkOrders.Include(x => x.Faena).SingleOrDefaultAsync(x => x.WorkOrderNumber == Code(workOrderNumber), ct) ?? throw new DomainException("La OT asociada no existe.");
            if (order.AssetId != asset.Id && !order.RelatedAssets.Any(x => x.AssetId == asset.Id)) throw new DomainException("La OT asociada no corresponde al activo.");
            EnsureFaenaAccess(u, order.Faena.Code);
        }
        var component = await _db.OperationalUnitComponents.Include(x => x.OperationalUnit).ThenInclude(x => x.OperationalUnitType).SingleOrDefaultAsync(x => x.AssetId == asset.Id && x.RemovedAtUtc == null, ct);
        var unit = component?.OperationalUnit;
        var moveAsTruckFactory = unit is not null && (string.Equals(unit.OperationalUnitType.Code, "CFA", StringComparison.OrdinalIgnoreCase) || unit.OperationalUnitType.Name.Contains("fabrica", StringComparison.OrdinalIgnoreCase));
        var movementUnit = moveAsTruckFactory ? unit : null;
        var assets = movementUnit is null ? [asset] : await _db.OperationalUnitComponents.Include(x => x.Asset).ThenInclude(x => x.Faena).Where(x => x.OperationalUnitId == movementUnit.Id && x.RemovedAtUtc == null).Select(x => x.Asset).ToListAsync(ct);
        AssetOperationalStateEntity targetState;
        if (string.IsNullOrWhiteSpace(destinationStateCode))
        {
            if (!AssetOperationalPolicy.IsExcludedFromOperationalUniverse(asset.OperationalState.Code))
                throw new DomainException(targetType == "TALLER" ? "Debe seleccionar el estado operacional de destino para ingresar al taller." : "Debe seleccionar el estado operacional de destino para retornar a faena.");
            targetState = asset.OperationalState;
        }
        else
        {
            targetState = await _db.AssetOperationalStates.SingleOrDefaultAsync(x => x.Code == Code(destinationStateCode) && x.IsActive, ct) ?? throw new DomainException("El estado operacional destino no existe o estÃƒÆ’Ã‚Â¡ inactivo.");
        }
        AssetOperationalPolicy.EnsureCompatibleWithPhysicalLocation(asset.Code, targetType, targetState.Code, targetState.Name);
        var auditPrevious = new Dictionary<Guid, object>();
        foreach (var item in assets.DistinctBy(x => x.Id))
        {
            var current = await _db.AssetPhysicalLocationPeriods.SingleOrDefaultAsync(x => x.AssetId == item.Id && x.ValidToUtc == null, ct) ?? throw new DomainException($"El activo '{item.Code}' no tiene ubicaci?n f?sica vigente.");
            if (effectiveAt <= current.ValidFromUtc) throw new DomainException("La fecha efectiva debe ser posterior al inicio de la ubicaci?n vigente.");
            if (AssetOperationalPolicy.IsExcludedFromOperationalUniverse(item.OperationalState.Code) && !Same(item.OperationalState.Code, targetState.Code))
                throw new DomainException($"El activo '{item.Code}' estÃƒÆ’Ã‚Â¡ dado de baja y debe conservar ese estado durante el traslado fÃƒÆ’Ã‚Â­sico.");
            AssetOperationalPolicy.EnsureCompatibleWithPhysicalLocation(item.Code, targetType, targetState.Code, targetState.Name);
            auditPrevious[item.Id] = new { Ubicacion = current.LocationType, Faena = current.FaenaId, Taller = current.WorkshopId, Estado = item.OperationalState.Code, VigenciaDesde = current.ValidFromUtc };
            current.ValidToUtc = effectiveAt;
            _db.AssetPhysicalLocationPeriods.Add(new AssetPhysicalLocationPeriodEntity { AssetId = item.Id, LocationType = targetType, FaenaId = faena?.Id, WorkshopId = workshop?.Id, ValidFromUtc = effectiveAt, Reason = Empty(reason), RegisteredByUserId = u.UserId, WorkOrderId = order?.Id, OperationalUnitId = movementUnit?.Id, Observations = Empty(observations) });
            var previous = item.OperationalState;
            if (previous.Id != targetState.Id)
            {
                AssetOperationalPolicy.EnsureTransitionAllowed(previous.Code, targetState.Code);
                item.OperationalStateId = targetState.Id; item.OperationalState = targetState;
                _db.AssetStateEvents.Add(new AssetStateEventEntity { AssetId = item.Id, PreviousStateId = previous.Id, NewStateId = targetState.Id, OccurredAtUtc = effectiveAt, UserId = u.UserId, Reason = Empty(reason) ?? (targetType == "TALLER" ? "Ingreso efectivo a taller" : "Retorno efectivo a faena"), ReferenceType = "UBICACION_FISICA", ReferenceId = item.Id.ToString("D"), ReferenceText = order?.WorkOrderNumber });
            }
            item.UpdatedAtUtc = DateTimeOffset.UtcNow;
            await OperationalUnitStateCalculator.RecalculateForAssetAsync(_db, item.Id, $"UBICACION_FISICA:{targetType} {reason}".Trim(), ct);
        }
        await _db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        var affected = assets.Select(x => x.Code).Distinct().ToArray();
        var movements = await PhysicalLocationQuery().Where(x => affected.Contains(x.Asset.Code) && x.ValidToUtc == null).ToListAsync(ct);
        foreach (var item in assets) await AuditAsync(u, "asset.physical_location.changed", item, auditPrevious[item.Id], new { Ubicacion = targetType, Taller = workshop?.Code, Faena = faena?.Code, Estado = targetState.Code, FechaEfectiva = effectiveAt, FechaRegistro = DateTimeOffset.UtcNow, Usuario = u.UserId, UnidadOperativa = movementUnit?.Code, OT = order?.WorkOrderNumber, Motivo = Empty(reason), Observaciones = Empty(observations) }, ct);
        return movements.Select(x => ToPhysicalLocation(x, affected)).ToArray();
    }
    public async Task<IReadOnlyCollection<AssetHistoryEntry>> GetHistoryAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return []; View(u, asset); return (await _db.AssetStateEvents.AsNoTracking().Include(x => x.PreviousState).Include(x => x.NewState).Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.OccurredAtUtc).ToListAsync(ct)).Select(x => new AssetHistoryEntry(x.Id.ToString("D"), x.OccurredAtUtc, "STATE_CHANGED", "EVENTOS_ESTADO", x.UserId, x.PreviousState?.Code, x.NewState.Code, x.Reason)).ToArray();
    }

    public async Task<IReadOnlyCollection<AssetDocumentResponse>> GetDocumentsAsync(string codigo, UserAccessContext u, CancellationToken ct) => (await GetDocumentMatrixAsync(codigo, u, ct)).Select(x => new AssetDocumentResponse("Activo", codigo, x.TipoDocumento, x.Estado, x.FechaVencimiento, x.DocumentoVigente, x.Critico, x.Estado == "VENCIDO", x.BloqueaDisponibilidad)).ToArray();

    public async Task<IReadOnlyCollection<AssetDocumentMatrixRow>> GetDocumentMatrixAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return []; View(u, asset); return await MatrixAsync(asset, ct);
    }

    public async Task<AssetCostSummary?> GetCostsAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return null; View(u, asset); var items = await _db.CostEntries.AsNoTracking().Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.OccurredAtUtc).Select(x => new AssetCostLine("Costos", x.Category, x.Amount, x.Currency, x.CostNumber)).ToArrayAsync(ct); return new AssetCostSummary(asset.Code, items.Sum(x => x.Amount), items.FirstOrDefault()?.Currency ?? "CLP", items);
    }

    public async Task<AssetAvailabilityResponse?> GetAvailabilityAsync(string codigo, UserAccessContext u, CancellationToken ct)
    {
        var asset = await FindAsync(codigo, false, ct); if (asset is null) return null; View(u, asset); var matrix = await MatrixAsync(asset, ct); var documents = matrix.Where(x => x.BloqueaDisponibilidad && x.Estado is not "VALIDADO" and not "POR_VENCER").Select(x => $"Documento {x.TipoDocumento}: {x.Estado}").ToArray(); var excluded = AssetOperationalPolicy.IsExcludedFromOperationalUniverse(asset.OperationalState.Code); var operational = AssetOperationalPolicy.IsAvailable(asset.OperationalState.Code); var blocks = (operational || excluded ? [] : new[] { $"Estado operacional: {asset.OperationalState.Name}" }).Concat(documents).ToArray(); return new(asset.Code, !excluded && blocks.Length == 0, operational, documents.Length == 0, asset.OperationalState.Code, DocumentState(matrix), blocks, !excluded && blocks.Length == 0 ? 100 : 0);
    }

    private IQueryable<AssetEntity> Query() => _db.Assets.Include(x => x.AssetTypeDefinition).Include(x => x.Family).Include(x => x.Faena).ThenInclude(x => x.TechnicalLocation).Include(x => x.OperationalState);
    private Task<AssetEntity?> FindAsync(string code, bool tracking, CancellationToken ct) { var query = Query(); if (!tracking) query = query.AsNoTracking(); return query.SingleOrDefaultAsync(x => x.Code == Code(code), ct); }

    private async Task<(AssetTypeEntity Type, EquipmentFamilyEntity? Family, FaenaEntity? Faena, AssetOperationalStateEntity State)> ReferencesAsync(string type, string? family, string? faena, string state, UserAccessContext u, CancellationToken ct)
    {
        Require(type, "TipoActivoCodigo"); var typeEntity = await _db.AssetTypes.SingleOrDefaultAsync(x => x.Code == Code(type) && x.IsActive, ct) ?? throw new DomainException("Tipo de activo inexistente.");
        var familyEntity = family is null or "" ? null : await _db.EquipmentFamilies.SingleOrDefaultAsync(x => x.Code == Code(family) && x.IsActive, ct) ?? throw new DomainException("Familia inexistente."); if (familyEntity is not null && familyEntity.AssetTypeId != typeEntity.Id) throw new DomainException("La familia no pertenece al tipo indicado.");
        var faenaEntity = faena is null or "" ? null : await _db.Faenas.Include(x => x.TechnicalLocation).SingleOrDefaultAsync(x => x.Code == Code(faena) && x.IsActive, ct) ?? throw new DomainException("Faena inexistente."); if (faenaEntity is not null && !_authorization.CanViewFaena(u, faenaEntity.Code)) throw new UnauthorizedAccessException("No tiene acceso a la faena indicada."); if (faenaEntity is not null && faenaEntity.TechnicalLocation is null) throw new DomainException("La faena indicada no tiene una ubicacion tecnica configurada.");
        var effectiveState = Same(faenaEntity?.Code, "FAE_EDB") ? AssetOperationalPolicy.DecommissionedStateCode : state;
        Require(effectiveState, "EstadoOperacionalCodigo");
        var stateEntity = await _db.AssetOperationalStates.SingleOrDefaultAsync(x => x.Code == Code(effectiveState) && x.IsActive, ct) ?? throw new DomainException("Estado operacional inexistente."); if (faenaEntity is not null) AssetOperationalPolicy.EnsureCompatibleWithPhysicalLocation("nuevo activo", "FAENA", stateEntity.Code, stateEntity.Name); return (typeEntity, familyEntity, faenaEntity, stateEntity);
    }
    private static AssetAttributeDefinitionResponse ToDefinition(AssetAttributeDefinitionEntity x) => new(x.Code, x.Name, x.DataType, x.Unit, x.IsRequired, x.IsIdentifier, x.IsUnique, x.IsSearchable, x.IsFilterable, x.ShowInList, x.MinimumValue, x.MaximumValue, x.ValidationPattern, x.OptionsJson, x.DisplayGroup, x.SortOrder);
    private async Task<IReadOnlyCollection<AssetAttributeDefinitionEntity>> DefinitionsAsync(Guid typeId, Guid? familyId, CancellationToken ct) => (await _db.AssetAttributeDefinitions.Where(x => x.IsActive && x.AssetTypeId == typeId && (x.EquipmentFamilyId == null || x.EquipmentFamilyId == familyId)).ToListAsync(ct)).GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(x => x.EquipmentFamilyId.HasValue).First()).OrderBy(x => x.SortOrder).ToArray();

    private async Task AttributesAsync(AssetEntity asset, IReadOnlyCollection<AssetAttributeValueInput> inputs, Guid typeId, Guid? familyId, bool replace, CancellationToken ct)
    {
        var definitions = await DefinitionsAsync(typeId, familyId, ct); var definitionByCode = definitions.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase); var inputByCode = inputs.GroupBy(x => x.DefinicionCodigo, StringComparer.OrdinalIgnoreCase).ToDictionary(x => x.Key, x => x.Single(), StringComparer.OrdinalIgnoreCase);
        if (inputByCode.Keys.Any(x => !definitionByCode.ContainsKey(x))) throw new DomainException("Existe un atributo que no aplica al tipo o familia."); foreach (var definition in definitions.Where(x => x.IsRequired && !inputByCode.ContainsKey(x.Code))) throw new DomainException($"Falta el atributo obligatorio {definition.Name}.");
        var existing = await _db.AssetAttributeValues.Where(x => x.AssetId == asset.Id).ToListAsync(ct);
        foreach (var definition in definitions) if (inputByCode.TryGetValue(definition.Code, out var input)) { Validate(definition, input); await ValidateUniqueAsync(asset.Id, definition, input, ct); var value = existing.SingleOrDefault(x => x.AttributeDefinitionId == definition.Id); if (value is null) { value = new AssetAttributeValueEntity { AssetId = asset.Id, AttributeDefinitionId = definition.Id }; _db.AssetAttributeValues.Add(value); } value.TextValue = Empty(input.ValorTexto); value.NumericValue = input.ValorNumerico; value.BooleanValue = input.ValorBooleano; value.DateValue = input.ValorFecha; value.Observations = Empty(input.Observaciones); }
        if (replace) foreach (var old in existing.Where(x => definitions.Any(d => d.Id == x.AttributeDefinitionId) && !inputByCode.ContainsKey(definitions.Single(d => d.Id == x.AttributeDefinitionId).Code))) _db.AssetAttributeValues.Remove(old);
    }

    private static void Validate(AssetAttributeDefinitionEntity d, AssetAttributeValueInput i)
    {
        if (new object?[] { Empty(i.ValorTexto), i.ValorNumerico, i.ValorBooleano, i.ValorFecha }.Count(x => x is not null) != 1) throw new DomainException($"El atributo {d.Name} debe tener un solo valor.");
        var expected = d.DataType switch { "TEXTO" or "OPCION" => i.ValorTexto is not null, "NUMERO" => i.ValorNumerico.HasValue, "ENTERO" => i.ValorNumerico.HasValue && decimal.Truncate(i.ValorNumerico.Value) == i.ValorNumerico.Value, "BOOLEANO" => i.ValorBooleano.HasValue, "FECHA" => i.ValorFecha.HasValue, _ => false }; if (!expected) throw new DomainException($"El valor no coincide con el tipo de {d.Name}.");
        if (i.ValorNumerico is { } number && ((d.MinimumValue.HasValue && number < d.MinimumValue) || (d.MaximumValue.HasValue && number > d.MaximumValue))) throw new DomainException($"El valor de {d.Name} esta fuera de rango."); if (i.ValorTexto is { } text && d.ValidationPattern is { Length: > 0 } && !Regex.IsMatch(text, d.ValidationPattern)) throw new DomainException($"El valor de {d.Name} no cumple el patron."); if (d.DataType == "OPCION" && !Option(d.OptionsJson, i.ValorTexto!)) throw new DomainException($"El valor de {d.Name} no es una opcion permitida.");
    }

    private async Task ValidateUniqueAsync(Guid assetId, AssetAttributeDefinitionEntity d, AssetAttributeValueInput i, CancellationToken ct)
    {
        if (!d.IsUnique) return; var values = _db.AssetAttributeValues.Where(x => x.AttributeDefinitionId == d.Id && x.AssetId != assetId); var exists = d.DataType is "TEXTO" or "OPCION" ? await values.AnyAsync(x => x.TextValue == i.ValorTexto, ct) : d.DataType is "NUMERO" or "ENTERO" ? await values.AnyAsync(x => x.NumericValue == i.ValorNumerico, ct) : d.DataType == "BOOLEANO" ? await values.AnyAsync(x => x.BooleanValue == i.ValorBooleano, ct) : await values.AnyAsync(x => x.DateValue == i.ValorFecha, ct); if (exists) throw new DomainException($"El atributo unico {d.Name} ya esta asignado.");
    }

    private async Task<AssetSummary> SummaryAsync(AssetEntity asset, CancellationToken ct)
    {
        var definitions = await DefinitionsAsync(asset.AssetTypeId, asset.FamilyId, ct); var values = await _db.AssetAttributeValues.AsNoTracking().Where(x => x.AssetId == asset.Id).ToListAsync(ct); var readings = await ValidReadingsAsync(asset.Id, ct); var latest = readings.OrderByDescending(x => x.ReadAtUtc).ThenByDescending(x => x.CreatedAtUtc).FirstOrDefault(); var matrix = await MatrixAsync(asset, ct);
        var required = definitions.Where(x => x.IsRequired).ToArray(); var ids = values.Where(x => x.TextValue is not null || x.NumericValue.HasValue || x.BooleanValue.HasValue || x.DateValue.HasValue).Select(x => x.AttributeDefinitionId).ToHashSet(); var missing = required.Where(x => !ids.Contains(x.Id)).Select(x => x.Code).ToArray(); var completed = required.Length - missing.Length; var percentage = required.Length == 0 ? 100 : (int)Math.Round(completed * 100m / required.Length); var completeness = new AssetCompleteness(required.Length, completed, percentage, percentage == 100 ? "COMPLETA" : completed == 0 ? "PENDIENTE" : "PARCIAL", missing);
        return new AssetSummary(asset.Code, asset.Name, asset.AssetTypeDefinition.Code, asset.AssetTypeDefinition.Name, asset.Family?.Code, asset.Family?.Name, asset.Faena?.Code, asset.Faena?.TechnicalLocation?.Code, asset.OperationalState.Code, asset.Criticality, asset.UsageMeasurementType, latest?.Value, Unit(asset.UsageMeasurementType), completeness, DocumentState(matrix), !matrix.Any(x => x.BloqueaDisponibilidad && x.Estado is not "VALIDADO" and not "POR_VENCER"), asset.Faena?.Name, asset.Faena?.Zone, asset.OperationalState.Name);
    }

    private async Task<IReadOnlyCollection<AssetSummary>> SummariesAsync(IReadOnlyCollection<AssetEntity> assets, CancellationToken ct)
    {
        if (assets.Count == 0) return [];

        var assetIds = assets.Select(x => x.Id).ToArray();
        var typeIds = assets.Select(x => x.AssetTypeId).Distinct().ToArray();
        var familyIds = assets.Where(x => x.FamilyId.HasValue).Select(x => x.FamilyId!.Value).Distinct().ToArray();
        var faenaIds = assets.Where(x => x.FaenaId.HasValue).Select(x => x.FaenaId!.Value).Distinct().ToArray();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var definitions = await _db.AssetAttributeDefinitions.AsNoTracking()
            .Where(x => x.IsActive && typeIds.Contains(x.AssetTypeId) && (x.EquipmentFamilyId == null || familyIds.Contains(x.EquipmentFamilyId.Value)))
            .ToListAsync(ct);
        var values = await _db.AssetAttributeValues.AsNoTracking().Where(x => assetIds.Contains(x.AssetId)).ToListAsync(ct);
        var readings = await _db.AssetReadings.AsNoTracking().Where(x => assetIds.Contains(x.AssetId)).ToListAsync(ct);
        var matrices = await _db.DocumentRequirementMatrices.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.DocumentType)
            .Where(x => x.Status == "VIGENTE" && x.FaenaId.HasValue && faenaIds.Contains(x.FaenaId.Value) && typeIds.Contains(x.AssetTypeId) && (x.EquipmentFamilyId == null || familyIds.Contains(x.EquipmentFamilyId.Value)) && x.ValidFrom <= today && (x.ValidTo == null || x.ValidTo >= today))
            .ToListAsync(ct);
        var legacyRequirements = await _db.AssetDocumentRequirements.AsNoTracking().Include(x => x.DocumentType)
            .Where(x => x.IsActive && typeIds.Contains(x.AssetTypeId) && (x.EquipmentFamilyId == null || familyIds.Contains(x.EquipmentFamilyId.Value)))
            .ToListAsync(ct);
        var documentLinks = await _db.DocumentAssets.AsNoTracking().Include(x => x.Document).ThenInclude(x => x.DocumentType).Include(x => x.Document).ThenInclude(x => x.Versions)
            .Where(x => assetIds.Contains(x.AssetId) && x.IsActive).ToListAsync(ct);
        var valuesByAsset = values.GroupBy(x => x.AssetId).ToDictionary(x => x.Key, x => x.ToArray());
        var readingsByAsset = readings.GroupBy(x => x.AssetId).ToDictionary(x => x.Key, x => x.ToArray());
        var documentsByAsset = documentLinks.GroupBy(x => x.AssetId).ToDictionary(x => x.Key, x => x.Select(link => link.Document).ToArray());

        return assets.Select(asset =>
        {
            var applicableDefinitions = definitions.Where(x => x.AssetTypeId == asset.AssetTypeId && (x.EquipmentFamilyId == null || x.EquipmentFamilyId == asset.FamilyId))
                .GroupBy(x => x.Code, StringComparer.OrdinalIgnoreCase).Select(group => group.OrderByDescending(x => x.EquipmentFamilyId.HasValue).First()).ToArray();
            var assetValues = valuesByAsset.GetValueOrDefault(asset.Id, []);
            var populatedDefinitionIds = assetValues.Where(x => x.TextValue is not null || x.NumericValue.HasValue || x.BooleanValue.HasValue || x.DateValue.HasValue).Select(x => x.AttributeDefinitionId).ToHashSet();
            var requiredDefinitions = applicableDefinitions.Where(x => x.IsRequired).ToArray();
            var missing = requiredDefinitions.Where(x => !populatedDefinitionIds.Contains(x.Id)).Select(x => x.Code).ToArray();
            var completed = requiredDefinitions.Length - missing.Length;
            var percentage = requiredDefinitions.Length == 0 ? 100 : (int)Math.Round(completed * 100m / requiredDefinitions.Length);
            var completeness = new AssetCompleteness(requiredDefinitions.Length, completed, percentage, percentage == 100 ? "COMPLETA" : completed == 0 ? "PENDIENTE" : "PARCIAL", missing);

            var assetReadings = readingsByAsset.GetValueOrDefault(asset.Id, []);
            var replacedReadingIds = assetReadings.Where(x => x.CorrectedReadingId.HasValue).Select(x => x.CorrectedReadingId!.Value).ToHashSet();
            var latestReading = assetReadings.Where(x => !replacedReadingIds.Contains(x.Id)).OrderByDescending(x => x.ReadAtUtc).ThenByDescending(x => x.CreatedAtUtc).FirstOrDefault();
            var documents = documentsByAsset.GetValueOrDefault(asset.Id, []);
            var matrix = matrices.Where(x => x.FaenaId == asset.FaenaId && x.AssetTypeId == asset.AssetTypeId && (x.EquipmentFamilyId == null || x.EquipmentFamilyId == asset.FamilyId))
                .OrderByDescending(x => x.EquipmentFamilyId.HasValue).ThenByDescending(x => x.ValidFrom).ThenByDescending(x => x.VersionNumber).FirstOrDefault();
            IReadOnlyCollection<AssetDocumentMatrixRow> rows;
            if (matrix is null)
            {
                rows = [];
            }
            else
            {
                rows = matrix.Items.OrderBy(x => x.SortOrder).ThenBy(x => x.DocumentType.Code).Select(rule => MatrixRow(
                    rule.DocumentType, rule.IsMandatory, rule.IsCritical, rule.BlocksAvailability, rule.AlertDays,
                    BestDocument(documents.Where(document => !document.RequirementMatrixId.HasValue || document.RequirementMatrixId == matrix.Id || rule.ReusableBetweenFaenas), rule.DocumentTypeId),
                    today, matrix, rule, asset.Faena?.Code, asset.Name, asset.Faena?.Name)).ToArray();
            }            return new AssetSummary(asset.Code, asset.Name, asset.AssetTypeDefinition.Code, asset.AssetTypeDefinition.Name, asset.Family?.Code, asset.Family?.Name, asset.Faena?.Code, asset.Faena?.TechnicalLocation?.Code, asset.OperationalState.Code, asset.Criticality, asset.UsageMeasurementType, latestReading?.Value, Unit(asset.UsageMeasurementType), completeness, DocumentState(rows), !rows.Any(x => x.BloqueaDisponibilidad && x.Estado is not "VALIDADO" and not "POR_VENCER"), asset.Faena?.Name, asset.Faena?.Zone, asset.OperationalState.Name);
        }).ToArray();
    }
    private async Task<AssetDetail> DetailAsync(AssetEntity asset, CancellationToken ct)
    {
        var summary = await SummaryAsync(asset, ct); var definitions = await DefinitionsAsync(asset.AssetTypeId, asset.FamilyId, ct); var values = await _db.AssetAttributeValues.AsNoTracking().Include(x => x.AttributeDefinition).Where(x => x.AssetId == asset.Id).ToListAsync(ct); var readings = MapReadings(await ValidReadingsAsync(asset.Id, ct), asset.UsageMeasurementType); var components = await _db.OperationalUnitComponents.AsNoTracking().Include(x => x.OperationalUnit).Include(x => x.ComponentRole).Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.InstalledAtUtc).ToListAsync(ct); var orders = await _db.WorkOrders.AsNoTracking().Include(x => x.Status).Include(x => x.MaintenanceType).Where(x => x.AssetId == asset.Id || x.RelatedAssets.Any(related => related.AssetId == asset.Id)).OrderByDescending(x => x.ScheduledAtUtc ?? x.CreatedAtUtc).Select(x => new AssetWorkOrderSummary(x.WorkOrderNumber, x.Status.Code, x.MaintenanceType.Code, x.Description, x.ScheduledAtUtc.HasValue ? DateOnly.FromDateTime(x.ScheduledAtUtc.Value.UtcDateTime) : null)).ToArrayAsync(ct); var documents = (await MatrixAsync(asset, ct)).Select(x => new AssetDocumentResponse("Activo", asset.Code, x.TipoDocumento, x.Estado, x.FechaVencimiento, x.DocumentoVigente, x.Critico, x.Estado == "VENCIDO", x.BloqueaDisponibilidad)).ToArray();
        var transfers = await _db.AssetTransfers.AsNoTracking().Include(x => x.OriginFaena).Include(x => x.DestinationFaena).Include(x => x.OperationalUnit).Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.EffectiveAtUtc).Select(x => new AssetTransferResponse(x.Id.ToString("D"), asset.Code, x.OriginFaena == null ? null : x.OriginFaena.Code, x.DestinationFaena == null ? null : x.DestinationFaena.Code, x.EffectiveAtUtc, x.Reason, x.UserId, x.RegisteredAtUtc, x.Observations, x.OperationalUnit == null ? null : x.OperationalUnit.Code)).ToArrayAsync(ct);
        var aliases = await _db.AssetIdentifierAliases.AsNoTracking().Where(x => x.AssetId == asset.Id).OrderByDescending(x => x.ValidFromUtc).Select(x => new AssetIdentifierAliasResponse(x.IdentifierType, x.ScopeKey, x.Value, x.ValidFromUtc, x.ValidToUtc, x.ValidToUtc == null)).ToArrayAsync(ct);
        return new AssetDetail(summary, asset.Brand, asset.Model, asset.SerialNumber, asset.Ownership, asset.ManufacturingYear, asset.AcquisitionDate, asset.CommissioningDate, asset.DecommissioningDate, asset.Observations, values.Select(x => new AssetAttributeValueResponse(x.AttributeDefinition.Code, x.AttributeDefinition.Name, x.AttributeDefinition.DataType, x.AttributeDefinition.Unit, x.TextValue, x.NumericValue, x.BooleanValue, x.DateValue, x.Observations)).ToArray(), definitions.Select(ToDefinition).ToArray(), readings, orders, components.FirstOrDefault(x => x.RemovedAtUtc is null)?.OperationalUnit.Code, components.Select(x => new AssetCompositionHistoryEntry(x.OperationalUnit.Code, x.ComponentRole.Code, x.InstalledAtUtc, x.RemovedAtUtc, x.Observations)).ToArray(), documents, transfers, aliases);
    }

    private async Task<IReadOnlyCollection<AssetDocumentMatrixRow>> MatrixAsync(AssetEntity asset, CancellationToken ct)
    {
        if (!asset.FaenaId.HasValue) return [];
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var matrix = await _db.DocumentRequirementMatrices.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.DocumentType)
            .Where(x => x.Status == "VIGENTE" && x.FaenaId == asset.FaenaId && x.AssetTypeId == asset.AssetTypeId && (x.EquipmentFamilyId == null || x.EquipmentFamilyId == asset.FamilyId) && x.ValidFrom <= today && (x.ValidTo == null || x.ValidTo >= today))
            .OrderByDescending(x => x.EquipmentFamilyId.HasValue).ThenByDescending(x => x.ValidFrom).ThenByDescending(x => x.VersionNumber).FirstOrDefaultAsync(ct);
        if (matrix is null) return [];
        var documents = await _db.DocumentAssets.AsNoTracking().Include(x => x.Document).ThenInclude(x => x.DocumentType).Include(x => x.Document).ThenInclude(x => x.Versions).Where(x => x.AssetId == asset.Id && x.IsActive).Select(x => x.Document).ToListAsync(ct);
        return matrix.Items.OrderBy(x => x.SortOrder).ThenBy(x => x.DocumentType.Code).Select(rule => MatrixRow(
            rule.DocumentType, rule.IsMandatory, rule.IsCritical, rule.BlocksAvailability, rule.AlertDays,
            BestDocument(documents.Where(document => !document.RequirementMatrixId.HasValue || document.RequirementMatrixId == matrix.Id || rule.ReusableBetweenFaenas), rule.DocumentTypeId),
            today, matrix, rule, asset.Faena?.Code, asset.Name, asset.Faena?.Name)).ToArray();
    }
    private static DocumentEntity? BestDocument(IEnumerable<DocumentEntity> documents, Guid typeId) => documents.Where(x => x.DocumentTypeId == typeId).OrderByDescending(x => DocumentComplianceCalculator.Evaluate(x.Status, x.ExpiresOn, x.DocumentType.AlertDays, x.Versions.Any(v => v.IsCurrent), x.BlocksAvailability).IsCompliant).ThenByDescending(x => x.CreatedAtUtc).FirstOrDefault();

    private static AssetDocumentMatrixRow MatrixRow(DocumentTypeEntity type, bool mandatory, bool critical, bool blocks, int alertDays, DocumentEntity? document, DateOnly today, DocumentRequirementMatrixEntity matrix, DocumentRequirementMatrixItemEntity rule, string? faenaCode, string assetName, string? faenaName)
    {
        if (document is null) return new(type.Code, mandatory, critical, blocks, "PENDIENTE_CARGA", null, null, null, null, "No existe documento vigente.", matrix.Id.ToString("D"), rule.Id.ToString("D"), rule.RequiresExpirationDate, faenaCode, assetName, faenaName, type.Name);
        var result = DocumentComplianceCalculator.Evaluate(document.Status, document.ExpiresOn, alertDays, document.Versions.Any(x => x.IsCurrent), blocks, today);
        var current = document.Versions.OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.VersionNumber).FirstOrDefault();
        return new(type.Code, mandatory, critical, blocks, DocumentComplianceCalculator.ToCode(result.Status), document.Code, current?.VersionNumber, document.ExpiresOn, result.DaysToExpire, result.Observation, matrix.Id.ToString("D"), rule.Id.ToString("D"), rule.RequiresExpirationDate, faenaCode, assetName, faenaName, type.Name);
    }
    private async Task SyncIdentifierAliasesAsync(AssetEntity asset, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var desired = new Dictionary<string, (string Scope, string Value)>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(asset.SerialNumber)) desired["NUMERO_SERIE"] = ($"SERIAL:{asset.FamilyId?.ToString("N") ?? asset.AssetTypeId.ToString("N")}", asset.SerialNumber.Trim());
        var dynamicIds = await _db.AssetAttributeValues.Include(x => x.AttributeDefinition).Where(x => x.AssetId == asset.Id && x.AttributeDefinition.IsIdentifier && x.TextValue != null).ToListAsync(ct);
        foreach (var value in dynamicIds) desired[value.AttributeDefinition.Code] = ($"ATTR:{value.AttributeDefinitionId:N}", value.TextValue!.Trim());
        var current = await _db.AssetIdentifierAliases.Where(x => x.AssetId == asset.Id && x.ValidToUtc == null).ToListAsync(ct);
        foreach (var alias in current)
        {
            if (desired.TryGetValue(alias.IdentifierType, out var target) && Same(alias.ScopeKey, target.Scope) && Same(alias.Value, target.Value)) { desired.Remove(alias.IdentifierType); continue; }
            alias.ValidToUtc = now;
        }
        foreach (var (type, target) in desired)
        {
            var normalized = target.Value.ToUpperInvariant();
            if (await _db.AssetIdentifierAliases.AnyAsync(x => x.AssetId != asset.Id && x.ScopeKey == target.Scope && x.NormalizedValue == normalized && x.ValidToUtc == null, ct)) throw new DomainException($"El identificador {type} '{target.Value}' ya esta vigente en su ambito.");
            _db.AssetIdentifierAliases.Add(new AssetIdentifierAliasEntity { AssetId = asset.Id, IdentifierType = type, ScopeKey = target.Scope, Value = target.Value, NormalizedValue = normalized, ValidFromUtc = now });
        }
    }

    private IQueryable<AssetPhysicalLocationPeriodEntity> PhysicalLocationQuery() => _db.AssetPhysicalLocationPeriods.Include(x => x.Asset).Include(x => x.Faena).Include(x => x.Workshop).Include(x => x.WorkOrder).Include(x => x.OperationalUnit);
    private static AssetPhysicalLocationResponse ToPhysicalLocation(AssetPhysicalLocationPeriodEntity x, IReadOnlyCollection<string> affected) => new(x.Asset.Code, x.LocationType, x.LocationType == "TALLER" ? x.Workshop?.Name ?? string.Empty : x.Faena?.Name ?? string.Empty, x.Workshop?.Commune, x.ValidFromUtc, x.ValidToUtc, x.RegisteredByUserId, x.WorkOrder?.WorkOrderNumber, x.Reason, x.Observations, x.OperationalUnit?.Code, affected);
    private void EnsureFaenaAccess(UserAccessContext u, string faenaCode) { if (!_authorization.CanViewFaena(u, faenaCode)) throw new UnauthorizedAccessException("No tiene acceso a la faena."); }
    private async Task<List<AssetReadingEntity>> ValidReadingsAsync(Guid assetId, CancellationToken ct) { var all = await _db.AssetReadings.Where(x => x.AssetId == assetId).ToListAsync(ct); var replaced = all.Where(x => x.CorrectedReadingId.HasValue).Select(x => x.CorrectedReadingId!.Value).ToHashSet(); return all.Where(x => !replaced.Contains(x.Id)).ToList(); }
    private static IReadOnlyCollection<AssetReadingResponse> MapReadings(IReadOnlyCollection<AssetReadingEntity> readings, string? measurement) { AssetReadingEntity? previous = null; return readings.OrderBy(x => x.ReadAtUtc).ThenBy(x => x.CreatedAtUtc).Select(x => { var result = new AssetReadingResponse(x.Id.ToString("D"), x.ReadAtUtc, x.Value, Unit(measurement) ?? string.Empty, previous is null ? null : x.Value - previous.Value, x.Source, x.IsCorrection, x.CorrectedReadingId?.ToString("D"), x.IsAnomalous, x.ValidationMessage, x.Observations); previous = x; return result; }).ToArray(); }
    private static string DocumentState(IReadOnlyCollection<AssetDocumentMatrixRow> rows) => rows.Any(x => x.Obligatorio && x.Estado == "PENDIENTE_CARGA") ? "PENDIENTE_CARGA" : rows.Any(x => x.Obligatorio && x.Estado == "PENDIENTE_VALIDACION") ? "PENDIENTE_VALIDACION" : rows.Any(x => x.Obligatorio && x.Estado == "VENCIDO") ? "VENCIDO" : rows.Any(x => x.Obligatorio && x.Estado == "POR_VENCER") ? "POR_VENCER" : "VALIDADO";
    private static string? Unit(string? type) => type == "HOROMETRO" ? "horas" : type == "KILOMETRAJE" ? "kilometros" : null;
    private async Task<string> NextCodeAsync(CancellationToken ct)
    {
        var number = await _db.Database.SqlQueryRaw<long>("SELECT nextval('asset_number_seq') AS \"Value\"").SingleAsync(ct);
        return $"ACT-{number:D6}";
    }
    private async Task<string?> CriticalityAsync(string? value, CancellationToken ct)
    {
        var requested = Empty(value);
        if (requested is null) return null;
        var items = await _db.WorkCatalogs.AsNoTracking().Where(x => x.Category == "WorkNotificationCriticality" && x.IsActive).ToListAsync(ct);
        var match = items.SingleOrDefault(x => Same(x.Name, requested) || Same(x.Code, requested));
        return match?.Name ?? throw new DomainException($"La criticidad '{requested}' no existe en el catalogo WorkNotificationCriticality.");
    }
    private static bool Option(string? json, string value) { try { using var d = JsonDocument.Parse(json ?? "[]"); return d.RootElement.ValueKind == JsonValueKind.Array && d.RootElement.EnumerateArray().Any(x => string.Equals(x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString(), value, StringComparison.OrdinalIgnoreCase)); } catch { return false; } }
    private static string? Measurement(string? value) { if (string.IsNullOrWhiteSpace(value)) return null; var type = Code(value); if (!Measurements.Contains(type)) throw new DomainException("TipoMedicionUso solo permite HOROMETRO, KILOMETRAJE o null."); return type; }
    private static string Source(string value) { var source = Code(value); if (!Sources.Contains(source)) throw new DomainException("Origen de lectura invalido."); return source; }
    private static void ValidateDates(short? year, DateOnly? start, DateOnly? end) { if (year is { } y && (y < 1900 || y > DateTime.UtcNow.Year + 2)) throw new DomainException("A?o de fabricacion invalido."); if (start is { } a && end is { } b && b < a) throw new DomainException("La fecha de baja no puede ser anterior a la puesta en servicio."); }
    private void RegisterReadings(UserAccessContext u)
    {
        if (u.Permissions.Contains(AuthPermissions.RegisterAssetReadings, StringComparer.OrdinalIgnoreCase) || _authorization.CanAdminister(u)) return;
        throw new UnauthorizedAccessException("No tiene permisos para registrar lecturas de activos.");
    }

    private void CorrectReadings(UserAccessContext u)
    {
        if (u.Permissions.Contains(AuthPermissions.CorrectAssetReadings, StringComparer.OrdinalIgnoreCase) || _authorization.CanAdminister(u)) return;
        throw new UnauthorizedAccessException("No tiene permisos para corregir lecturas de activos.");
    }
    private void Maintain(UserAccessContext u) { if (!_authorization.CanAdminister(u) && !u.Permissions.Contains(AuthPermissions.ManageAssets, StringComparer.OrdinalIgnoreCase) && !u.Roles.Contains(AuthRoles.Planner, StringComparer.OrdinalIgnoreCase) && !u.Roles.Contains(AuthRoles.MaintenanceSupervisor, StringComparer.OrdinalIgnoreCase)) throw new UnauthorizedAccessException("No tiene permisos para administrar activos."); }
    private static bool CanView(UserAccessContext u, AssetEntity a) => a.Faena is null || u.Roles.Contains(AuthRoles.Admin, StringComparer.OrdinalIgnoreCase) || u.Roles.Contains(AuthRoles.Management, StringComparer.OrdinalIgnoreCase) || u.Faenas.Contains(a.Faena.Code, StringComparer.OrdinalIgnoreCase);
    private static void View(UserAccessContext u, AssetEntity a) { if (!CanView(u, a)) throw new UnauthorizedAccessException("No tiene acceso al activo."); }
    private async Task AuditAsync(UserAccessContext u, string action, AssetEntity a, object? previous, object? next, CancellationToken ct) =>
        await _audit.RecordAsync(new AuditEventRequest(
            u.UserId,
            action,
            AuditModules.Assets,
            "Asset",
            a.Code,
            previous is null ? null : JsonSerializer.Serialize(AuditValue(previous)),
            next is null ? null : JsonSerializer.Serialize(AuditValue(next)),
            a.Faena?.Code,
            AuditSeverity.Medium), ct);

    private static object AuditValue(object value) => value is AssetEntity asset
        ? new
        {
            asset.Id,
            asset.Code,
            asset.Name,
            asset.AssetTypeId,
            asset.FaenaId,
            asset.FamilyId,
            asset.OperationalStateId,
            asset.Brand,
            asset.Model,
            asset.SerialNumber,
            asset.Ownership,
            asset.Criticality,
            asset.ManufacturingYear,
            asset.AcquisitionDate,
            asset.CommissioningDate,
            asset.DecommissioningDate,
            asset.UsageMeasurementType,
            asset.Observations,
            asset.CreatedAtUtc,
            asset.UpdatedAtUtc
        }
        : value;
    private static bool Same(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static string Code(string? v) => v?.Trim().ToUpperInvariant() ?? string.Empty;
    private static string NormalizeDocumentStatus(string value) => Code(value).Replace(" ", "_").Replace("-", "_");
    private static string? Empty(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    private static void Require(string? v, string name) { if (string.IsNullOrWhiteSpace(v)) throw new DomainException($"{name} es obligatorio."); }
}
