using MaintenanceCMMS.Application.Assets;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Assets;

/// <summary>Loads regulatory-document status for overview rows in a bounded number of queries.</summary>
internal sealed class EquipmentOverviewDocumentProjectionService(CmmsDbContext db)
{
    private static readonly IReadOnlyDictionary<DocumentLifecycleStatus, int> Restrictiveness = new Dictionary<DocumentLifecycleStatus, int>
    {
        [DocumentLifecycleStatus.Rechazado] = 0,
        [DocumentLifecycleStatus.Vencido] = 1,
        [DocumentLifecycleStatus.PendienteCarga] = 2,
        [DocumentLifecycleStatus.PendienteValidacion] = 3,
        [DocumentLifecycleStatus.PorVencer] = 4,
        [DocumentLifecycleStatus.Vigente] = 5
    };

    public async Task<EquipmentOverviewDocumentProjection> ProjectAsync(
        IReadOnlyCollection<Guid> directAssetIds,
        IReadOnlyCollection<Guid> unitIds,
        CancellationToken ct)
    {
        var currentComponents = await db.OperationalUnitComponents.AsNoTracking()
            .Where(component => unitIds.Contains(component.OperationalUnitId) && component.RemovedAtUtc == null)
            .Select(component => new Component(component.OperationalUnitId, component.AssetId))
            .ToArrayAsync(ct);
        var involvedAssetIds = directAssetIds.Concat(currentComponents.Select(component => component.AssetId)).Distinct().ToArray();
        if (involvedAssetIds.Length == 0) return new(new Dictionary<Guid, EquipmentDocumentRequirementStatus[]>(), new Dictionary<Guid, EquipmentDocumentRequirementStatus[]>());

        var assets = await db.Assets.AsNoTracking()
            .Where(asset => involvedAssetIds.Contains(asset.Id))
            .Select(asset => new AssetContext(asset.Id, asset.FaenaId, asset.AssetTypeId, asset.FamilyId))
            .ToArrayAsync(ct);
        var faenaIds = assets.Where(asset => asset.FaenaId.HasValue).Select(asset => asset.FaenaId!.Value).Distinct().ToArray();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var matrices = faenaIds.Length == 0 ? [] : await db.DocumentRequirementMatrices.AsNoTracking()
            .Include(matrix => matrix.Items).ThenInclude(item => item.DocumentType)
            .Where(matrix => faenaIds.Contains(matrix.FaenaId!.Value) && matrix.Status == "VIGENTE" && matrix.ValidFrom <= today && (matrix.ValidTo == null || matrix.ValidTo >= today))
            .ToArrayAsync(ct);
        var links = await db.DocumentAssets.AsNoTracking()
            .Where(link => link.IsActive && involvedAssetIds.Contains(link.AssetId))
            .Include(link => link.Document).ThenInclude(document => document.DocumentType)
            .Include(link => link.Document).ThenInclude(document => document.Versions).ThenInclude(version => version.File)
            .ToArrayAsync(ct);
        var documentsByAsset = links.GroupBy(link => link.AssetId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<DocumentEntity>)group.Select(link => link.Document).DistinctBy(document => document.Id).ToArray());

        var assetStatuses = assets.ToDictionary(
            asset => asset.Id,
            asset => ProjectAsset(asset, matrices, documentsByAsset.GetValueOrDefault(asset.Id, []), today));
        var unitStatuses = currentComponents.GroupBy(component => component.UnitId).ToDictionary(
            group => group.Key,
            group => Consolidate(group.Select(component => assetStatuses[component.AssetId]).ToArray()));
        return new(assetStatuses, unitStatuses);
    }

    private static EquipmentDocumentRequirementStatus[] ProjectAsset(AssetContext asset, IReadOnlyCollection<DocumentRequirementMatrixEntity> matrices, IReadOnlyCollection<DocumentEntity> documents, DateOnly today)
    {
        var matrix = ResolveMatrix(asset, matrices);
        return RegulatoryDocumentCategories.All.Select(category =>
        {
            var requirements = matrix?.Items.Where(item => RegulatoryDocumentCategories.Classify(item.DocumentType.Code, item.DocumentType.Name) == category).ToArray() ?? [];
            if (requirements.Length == 0) return new EquipmentDocumentRequirementStatus(RegulatoryDocumentCategories.Code(category), null, null, null, false);
            return Consolidate(requirements.Select(requirement => ProjectRequirement(asset.Id, matrix!, requirement, documents, today)).ToArray());
        }).ToArray();
    }

    private static DocumentRequirementMatrixEntity? ResolveMatrix(AssetContext asset, IReadOnlyCollection<DocumentRequirementMatrixEntity> matrices) => matrices
        .Where(matrix => matrix.FaenaId == asset.FaenaId && matrix.AssetTypeId == asset.AssetTypeId && (matrix.EquipmentFamilyId == null || matrix.EquipmentFamilyId == asset.FamilyId))
        .OrderByDescending(matrix => matrix.EquipmentFamilyId.HasValue).ThenByDescending(matrix => matrix.ValidFrom).ThenByDescending(matrix => matrix.VersionNumber)
        .FirstOrDefault();

    private static EquipmentDocumentRequirementStatus ProjectRequirement(Guid assetId, DocumentRequirementMatrixEntity matrix, DocumentRequirementMatrixItemEntity requirement, IReadOnlyCollection<DocumentEntity> documents, DateOnly today)
    {
        var category = RegulatoryDocumentCategories.Classify(requirement.DocumentType.Code, requirement.DocumentType.Name)!.Value;
        var candidates = documents.Where(document => IsApplicable(document, assetId, matrix, requirement, category))
            .Select(document => new { Document = document, Version = document.Versions.SingleOrDefault(version => version.IsCurrent) })
            .Select(item => new { item.Document, item.Version, Result = DocumentComplianceCalculator.Evaluate(item.Document.Status, item.Version?.ExpiresOn ?? item.Document.ExpiresOn, requirement.AlertDays, item.Version is not null, requirement.BlocksAvailability, today) })
            .OrderBy(item => Rank(item.Result.Status)).ThenBy(item => Distance(item.Result.DaysToExpire)).ThenByDescending(item => item.Document.CreatedAtUtc)
            .FirstOrDefault();
        var result = candidates?.Result ?? DocumentComplianceCalculator.Evaluate(null, null, requirement.AlertDays, false, requirement.BlocksAvailability, today);
        var expiration = candidates?.Version?.ExpiresOn ?? candidates?.Document.ExpiresOn;
        return new EquipmentDocumentRequirementStatus(RegulatoryDocumentCategories.Code(category), DocumentComplianceCalculator.ToCode(result.Status), expiration, result.DaysToExpire, true);
    }

    private static bool IsApplicable(DocumentEntity document, Guid assetId, DocumentRequirementMatrixEntity matrix, DocumentRequirementMatrixItemEntity requirement, RegulatoryDocumentCategory category) =>
        document.IsCurrent && !document.IsHistorical && !document.IsAnnulled && !IsRetired(document.Status)
        && RegulatoryDocumentCategories.Classify(document.DocumentType.Code, document.DocumentType.Name) == category
        && (document.RequirementAssetId == null || document.RequirementAssetId == assetId)
        && (!document.RequirementMatrixId.HasValue || document.RequirementMatrixId == matrix.Id || requirement.ReusableBetweenFaenas);

    private static bool IsRetired(string? status) => string.Equals(status, nameof(DocumentLifecycleStatus.Anulado), StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, nameof(DocumentLifecycleStatus.Reemplazado), StringComparison.OrdinalIgnoreCase);

    private static EquipmentDocumentRequirementStatus[] Consolidate(IReadOnlyCollection<EquipmentDocumentRequirementStatus[]> statuses) => RegulatoryDocumentCategories.All
        .Select(category => Consolidate(statuses.Select(set => set.Single(status => status.Code == RegulatoryDocumentCategories.Code(category))).ToArray()))
        .ToArray();

    private static EquipmentDocumentRequirementStatus Consolidate(IReadOnlyCollection<EquipmentDocumentRequirementStatus> statuses)
    {
        var categoryCode = statuses.FirstOrDefault()?.Code ?? string.Empty;
        var applicable = statuses.Where(status => status.Applies == true).ToArray();
        if (applicable.Length == 0) return new EquipmentDocumentRequirementStatus(categoryCode, null, null, null, false);
        return applicable.OrderBy(status => Rank(status.Status)).ThenBy(status => Distance(status.DaysUntilExpiration)).ThenBy(status => status.ExpirationDate).First() with { Applies = true };
    }

    private static int Rank(string? status) => Enum.TryParse<DocumentLifecycleStatus>(status?.Replace("_", string.Empty), true, out var parsed) && Restrictiveness.TryGetValue(parsed, out var rank) ? rank : 6;
    private static int Rank(DocumentLifecycleStatus status) => Restrictiveness.GetValueOrDefault(status, 6);
    private static int Distance(int? days) => days.HasValue ? Math.Abs(days.Value) : int.MaxValue;
    private sealed record AssetContext(Guid Id, Guid? FaenaId, Guid AssetTypeId, Guid? FamilyId);
    private sealed record Component(Guid UnitId, Guid AssetId);
}

internal sealed class EquipmentOverviewDocumentProjection(
    IReadOnlyDictionary<Guid, EquipmentDocumentRequirementStatus[]> assets,
    IReadOnlyDictionary<Guid, EquipmentDocumentRequirementStatus[]> units)
{
    public EquipmentDocumentRequirementStatus? ForAsset(Guid id, RegulatoryDocumentCategory category) => assets.TryGetValue(id, out var statuses) ? statuses.Single(status => status.Code == RegulatoryDocumentCategories.Code(category)) : null;
    public EquipmentDocumentRequirementStatus? ForUnit(Guid id, RegulatoryDocumentCategory category) => units.TryGetValue(id, out var statuses) ? statuses.Single(status => status.Code == RegulatoryDocumentCategories.Code(category)) : null;
    public bool Matches(Guid? assetId, Guid? unitId, string normalizedStatus) => (assetId.HasValue ? ForAll(assets.GetValueOrDefault(assetId.Value)) : ForAll(units.GetValueOrDefault(unitId!.Value)))
        .Any(status => status.Applies == true && string.Equals(status.Status, normalizedStatus, StringComparison.OrdinalIgnoreCase));
    private static IEnumerable<EquipmentDocumentRequirementStatus> ForAll(EquipmentDocumentRequirementStatus[]? statuses) => statuses ?? [];
}
