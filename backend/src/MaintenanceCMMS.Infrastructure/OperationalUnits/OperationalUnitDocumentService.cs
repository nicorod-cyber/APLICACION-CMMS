using System.Text.Json;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Application.OperationalUnits;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Documents;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.OperationalUnits;

/// <summary>
/// Aggregates the documentary obligations of the currently mounted components. It never changes
/// the technical ownership of a document: commands are delegated to <see cref="IDocumentService"/>
/// with the owning physical asset resolved on the server.
/// </summary>
public sealed class OperationalUnitDocumentService(
    CmmsDbContext db,
    IDocumentService documentService,
    IAuditService audit,
    IAuthorizationPolicyService authorization) : IOperationalUnitDocumentService
{
    private static readonly StringComparer CodeComparer = StringComparer.OrdinalIgnoreCase;

    public async Task<OperationalUnitDocumentResponse?> GetAsync(string unitCode, UserAccessContext user, CancellationToken ct)
    {
        var unit = await FindUnitAsync(unitCode, ct);
        if (unit is null) return null;
        EnsureView(unit, user);

        var components = await CurrentComponentsAsync(unit.Id, ct);
        var requiredRoles = await db.OperationalUnitCompositionRules.AsNoTracking()
            .Where(item => item.OperationalUnitTypeId == unit.OperationalUnitTypeId && item.IsActive && item.IsMandatory)
            .Select(item => item.ComponentRole.Code)
            .ToArrayAsync(ct);
        var missingRoles = requiredRoles
            .Where(role => !components.Any(component => Same(component.ComponentRole.Code, role)))
            .Distinct(CodeComparer)
            .OrderBy(role => role, CodeComparer)
            .ToArray();
        var warnings = new List<string>();
        if (missingRoles.Length > 0)
            warnings.Add("La composición está incompleta: falta " + string.Join(", ", missingRoles.Select(RoleLabel)) + ".");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var matrices = unit.FaenaId is null
            ? []
            : await db.DocumentRequirementMatrices.AsNoTracking()
                .Include(item => item.Items).ThenInclude(item => item.DocumentType)
                .Where(item => item.FaenaId == unit.FaenaId && item.Status == "VIGENTE" && item.ValidFrom <= today && (item.ValidTo == null || item.ValidTo >= today))
                .ToArrayAsync(ct);
        var matrixByAsset = components.ToDictionary(component => component.AssetId, component => ResolveMatrix(component.Asset, matrices));
        foreach (var component in components.Where(component => matrixByAsset[component.AssetId] is null))
            warnings.Add($"Falta configurar la matriz documental de {RoleLabel(component.ComponentRole.Code)}.");

        var assetIds = components.Select(component => component.AssetId).ToArray();
        var links = assetIds.Length == 0 ? [] : await db.DocumentAssets.AsNoTracking()
            .Where(link => link.IsActive && assetIds.Contains(link.AssetId))
            .Include(link => link.Document).ThenInclude(document => document.DocumentType)
            .Include(link => link.Document).ThenInclude(document => document.Versions).ThenInclude(version => version.File)
            .ToArrayAsync(ct);
        var documentsByAsset = links.GroupBy(link => link.AssetId).ToDictionary(group => group.Key, group => group.Select(link => link.Document).DistinctBy(document => document.Id).ToArray());
        var canManage = authorization.CanManageDocuments(user);
        var canValidate = authorization.CanValidateDocuments(user);
        var rows = new List<OperationalUnitDocumentRow>();

        foreach (var component in components)
        {
            var matrix = matrixByAsset[component.AssetId];
            if (matrix is null) continue;
            var ownerDocuments = documentsByAsset.GetValueOrDefault(component.AssetId, []);
            foreach (var requirement in matrix.Items.OrderBy(item => item.SortOrder).ThenBy(item => item.DocumentType.Code, CodeComparer))
            {
                var document = SelectDocument(ownerDocuments, component.AssetId, matrix.Id, requirement);
                var version = document?.Versions.SingleOrDefault(item => item.IsCurrent);
                var result = DocumentComplianceCalculator.Evaluate(
                    document?.Status,
                    version?.ExpiresOn ?? document?.ExpiresOn,
                    requirement.AlertDays,
                    version is not null,
                    requirement.BlocksAvailability,
                    today);
                rows.Add(new OperationalUnitDocumentRow(
                    RequirementKey: RequirementKey(component.AssetId, requirement.Id),
                    DocumentTypeCode: requirement.DocumentType.Code,
                    DocumentTypeName: requirement.DocumentType.Name,
                    Mandatory: requirement.IsMandatory,
                    Critical: requirement.IsCritical,
                    BlocksAvailability: requirement.BlocksAvailability,
                    RequiresExpirationDate: requirement.RequiresExpirationDate,
                    AlertDays: requirement.AlertDays,
                    Status: result.Status.ToString(),
                    VersionNumber: version?.VersionNumber,
                    IssueDate: version?.IssueDate ?? document?.IssueDate,
                    ExpirationDate: version?.ExpiresOn ?? document?.ExpiresOn,
                    ValidationStatus: version?.ValidationStatus,
                    RejectionReason: version?.RejectReason ?? document?.RejectReason,
                    DocumentId: document?.Id.ToString("D"),
                    CurrentVersionId: version?.Id.ToString("D"),
                    CanUpload: canManage && document is null,
                    CanReplace: canManage && document is not null && !document.IsHistorical,
                    CanValidate: canValidate && document is not null && result.Status == DocumentLifecycleStatus.PendienteValidacion,
                    CanReject: canValidate && document is not null && result.Status == DocumentLifecycleStatus.PendienteValidacion,
                    CanAnnul: canManage && document is not null && !document.IsHistorical,
                    TechnicalOwnerRole: component.ComponentRole.Code,
                    TechnicalOwnerAssetCode: component.Asset.Code,
                    TechnicalOwnerAssetName: component.Asset.Name,
                    MatrixId: matrix.Id.ToString("D"),
                    MatrixItemId: requirement.Id.ToString("D"),
                    IsHistorical: document?.IsHistorical ?? false,
                    ValidatedBy: version?.ValidatedByUserId ?? document?.ValidatedByUserId,
                    ValidatedAtUtc: version?.ValidatedAtUtc ?? document?.ValidatedAtUtc,
                    SharePointUrl: version?.File.LogicalUri,
                    DaysToExpire: result.DaysToExpire,
                    PendingReason: result.Observation));
            }
        }

        var compositionComplete = missingRoles.Length == 0;
        var matrixComplete = compositionComplete && components.All(component => matrixByAsset[component.AssetId] is not null);
        if (rows.Count == 0 && matrixComplete) warnings.Add("La unidad no requiere documentos.");
        else if (matrixComplete) warnings.Add("La configuración documental está completa.");
        var summary = new OperationalUnitDocumentSummary(
            PendingUpload: rows.Count(row => row.Status == nameof(DocumentLifecycleStatus.PendienteCarga)),
            PendingValidation: rows.Count(row => row.Status == nameof(DocumentLifecycleStatus.PendienteValidacion)),
            Expiring: rows.Count(row => row.Status == nameof(DocumentLifecycleStatus.PorVencer)),
            Expired: rows.Count(row => row.Status == nameof(DocumentLifecycleStatus.Vencido)),
            Valid: rows.Count(row => row.Status == nameof(DocumentLifecycleStatus.Vigente)),
            Compliant: compositionComplete && matrixComplete && rows.Where(row => row.Mandatory).All(row => row.Status is nameof(DocumentLifecycleStatus.Vigente) or nameof(DocumentLifecycleStatus.PorVencer)),
            BlocksAvailability: rows.Any(row => row.BlocksAvailability && row.Status is not nameof(DocumentLifecycleStatus.Vigente) and not nameof(DocumentLifecycleStatus.PorVencer)));
        return new OperationalUnitDocumentResponse(
            unit.Code, unit.Name, unit.Faena?.Code, unit.Faena?.Name, compositionComplete, matrixComplete, summary,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            rows.OrderBy(row => RoleOrder(row.TechnicalOwnerRole)).ThenBy(row => row.DocumentTypeName, CodeComparer).ToArray());
    }

    public async Task<OperationalUnitDocumentResponse?> UploadAsync(string unitCode, string requirementKey, DocumentUploadContent upload, UserAccessContext user, CancellationToken ct)
    {
        var (unit, component, matrix, requirement) = await ResolveCurrentRequirementAsync(unitCode, requirementKey, user, ct);
        var safeUpload = upload with { TipoDocumento = requirement.DocumentType.Code };
        var created = await documentService.UploadAssetAsync(component.Asset.Code, safeUpload, user, ct);
        await AuditAsync(user, "operational_unit_document.uploaded", unit, created.DocumentoId, new { UnitCode = unit.Code, ComponentRole = component.ComponentRole.Code, AssetCode = component.Asset.Code, MatrixId = matrix.Id, RequirementId = requirement.Id }, upload.Observaciones, ct);
        return await GetAsync(unit.Code, user, ct);
    }

    public async Task<OperationalUnitDocumentResponse?> ReplaceAsync(string unitCode, string documentId, DocumentUploadContent upload, UserAccessContext user, CancellationToken ct)
    {
        var (unit, component) = await ResolveCurrentDocumentAsync(unitCode, documentId, user, ct);
        var result = await documentService.ReplaceWithUploadAsync(documentId, upload, user, ct);
        if (result is null) return null;
        await AuditAsync(user, "operational_unit_document.replaced", unit, documentId, new { UnitCode = unit.Code, ComponentRole = component.ComponentRole.Code, AssetCode = component.Asset.Code }, upload.Observaciones, ct);
        return await GetAsync(unit.Code, user, ct);
    }

    public async Task<OperationalUnitDocumentResponse?> UpdateAsync(string unitCode, string documentId, UpdateDocumentRequest request, UserAccessContext user, CancellationToken ct)
    {
        var (unit, component) = await ResolveCurrentDocumentAsync(unitCode, documentId, user, ct);
        var result = await documentService.UpdateAsync(documentId, request, user, ct);
        if (result is null) return null;
        await AuditAsync(user, "operational_unit_document.updated", unit, documentId, new { UnitCode = unit.Code, ComponentRole = component.ComponentRole.Code, AssetCode = component.Asset.Code }, request.Reason, ct);
        return await GetAsync(unit.Code, user, ct);
    }

    public async Task<OperationalUnitDocumentResponse?> ValidateAsync(string unitCode, string documentId, ValidateDocumentRequest request, UserAccessContext user, CancellationToken ct)
    {
        var (unit, component) = await ResolveCurrentDocumentAsync(unitCode, documentId, user, ct);
        var result = await documentService.ValidateAsync(documentId, request, user, ct);
        if (result is null) return null;
        await AuditAsync(user, "operational_unit_document.validated", unit, documentId, new { UnitCode = unit.Code, ComponentRole = component.ComponentRole.Code, AssetCode = component.Asset.Code }, request.Comments, ct);
        return await GetAsync(unit.Code, user, ct);
    }

    public async Task<OperationalUnitDocumentResponse?> RejectAsync(string unitCode, string documentId, RejectDocumentRequest request, UserAccessContext user, CancellationToken ct)
    {
        var (unit, component) = await ResolveCurrentDocumentAsync(unitCode, documentId, user, ct);
        var result = await documentService.RejectAsync(documentId, request, user, ct);
        if (result is null) return null;
        await AuditAsync(user, "operational_unit_document.rejected", unit, documentId, new { UnitCode = unit.Code, ComponentRole = component.ComponentRole.Code, AssetCode = component.Asset.Code }, request.Reason, ct);
        return await GetAsync(unit.Code, user, ct);
    }

    public async Task<OperationalUnitDocumentResponse?> AnnulAsync(string unitCode, string documentId, AnnulDocumentRequest request, UserAccessContext user, CancellationToken ct)
    {
        var (unit, component) = await ResolveCurrentDocumentAsync(unitCode, documentId, user, ct);
        var result = await documentService.AnnulAsync(documentId, request, user, ct);
        if (result is null) return null;
        await AuditAsync(user, "operational_unit_document.annulled", unit, documentId, new { UnitCode = unit.Code, ComponentRole = component.ComponentRole.Code, AssetCode = component.Asset.Code }, request.Reason, ct);
        return await GetAsync(unit.Code, user, ct);
    }

    public async Task<OperationalUnitDocumentOwnerContext?> FindCurrentUnitByComponentAsync(string assetCode, UserAccessContext user, CancellationToken ct)
    {
        var component = await db.OperationalUnitComponents.AsNoTracking()
            .Include(item => item.OperationalUnit).ThenInclude(unit => unit.Faena)
            .Include(item => item.ComponentRole)
            .Include(item => item.Asset)
            .FirstOrDefaultAsync(item => item.RemovedAtUtc == null && item.Asset.Code == assetCode.Trim(), ct);
        if (component is null) return null;
        EnsureView(component.OperationalUnit, user);
        return new OperationalUnitDocumentOwnerContext(component.OperationalUnit.Code, component.OperationalUnit.Name, component.ComponentRole.Code, component.Asset.Code);
    }

    private async Task<(OperationalUnitEntity Unit, OperationalUnitComponentEntity Component, DocumentRequirementMatrixEntity Matrix, DocumentRequirementMatrixItemEntity Requirement)> ResolveCurrentRequirementAsync(string unitCode, string requirementKey, UserAccessContext user, CancellationToken ct)
    {
        var unit = await FindUnitAsync(unitCode, ct) ?? throw new DomainException("La unidad operativa no existe.");
        EnsureView(unit, user);
        if (!TryParseRequirementKey(requirementKey, out var assetId, out var itemId)) throw new DomainException("El requisito documental no es válido.");
        var component = await db.OperationalUnitComponents
            .Include(item => item.Asset).ThenInclude(asset => asset.Faena)
            .Include(item => item.Asset).ThenInclude(asset => asset.AssetTypeDefinition)
            .Include(item => item.Asset).ThenInclude(asset => asset.Family)
            .Include(item => item.ComponentRole)
            .SingleOrDefaultAsync(item => item.OperationalUnitId == unit.Id && item.AssetId == assetId && item.RemovedAtUtc == null, ct)
            ?? throw new DomainException("El componente propietario ya no está montado en la unidad.");
        var matrix = await ResolveMatrixAsync(component.Asset, ct) ?? throw new DomainException($"No existe una matriz documental vigente para {RoleLabel(component.ComponentRole.Code)}.");
        var requirement = matrix.Items.SingleOrDefault(item => item.Id == itemId)
            ?? throw new DomainException("El requisito no pertenece a la matriz documental vigente del componente.");
        return (unit, component, matrix, requirement);
    }

    private async Task<(OperationalUnitEntity Unit, OperationalUnitComponentEntity Component)> ResolveCurrentDocumentAsync(string unitCode, string documentId, UserAccessContext user, CancellationToken ct)
    {
        var unit = await FindUnitAsync(unitCode, ct) ?? throw new DomainException("La unidad operativa no existe.");
        EnsureView(unit, user);
        if (!Guid.TryParse(documentId, out var id)) throw new DomainException("El documento no es válido.");
        var component = await db.OperationalUnitComponents
            .Include(item => item.Asset)
            .Include(item => item.ComponentRole)
            .Where(item => item.OperationalUnitId == unit.Id && item.RemovedAtUtc == null)
            .FirstOrDefaultAsync(item => db.DocumentAssets.Any(link => link.DocumentId == id && link.AssetId == item.AssetId && link.IsActive), ct)
            ?? throw new DomainException("El documento no pertenece a un componente vigente de la unidad.");
        return (unit, component);
    }

    private async Task<OperationalUnitEntity?> FindUnitAsync(string code, CancellationToken ct) => await db.OperationalUnits
        .Include(item => item.Faena)
        .FirstOrDefaultAsync(item => item.Code == code.Trim(), ct);

    private async Task<OperationalUnitComponentEntity[]> CurrentComponentsAsync(Guid unitId, CancellationToken ct) => await db.OperationalUnitComponents.AsNoTracking()
        .Include(item => item.ComponentRole)
        .Include(item => item.Asset).ThenInclude(asset => asset.Faena)
        .Include(item => item.Asset).ThenInclude(asset => asset.AssetTypeDefinition)
        .Include(item => item.Asset).ThenInclude(asset => asset.Family)
        .Where(item => item.OperationalUnitId == unitId && item.RemovedAtUtc == null)
        .OrderBy(item => item.ComponentRole.Code)
        .ThenBy(item => item.Asset.Code)
        .ToArrayAsync(ct);

    private async Task<DocumentRequirementMatrixEntity?> ResolveMatrixAsync(AssetEntity asset, CancellationToken ct)
    {
        if (!asset.FaenaId.HasValue) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await db.DocumentRequirementMatrices.AsNoTracking().Include(item => item.Items).ThenInclude(item => item.DocumentType)
            .Where(item => item.FaenaId == asset.FaenaId && item.AssetTypeId == asset.AssetTypeId && (item.EquipmentFamilyId == null || item.EquipmentFamilyId == asset.FamilyId) && item.Status == "VIGENTE" && item.ValidFrom <= today && (item.ValidTo == null || item.ValidTo >= today))
            .OrderByDescending(item => item.EquipmentFamilyId.HasValue).ThenByDescending(item => item.ValidFrom).ThenByDescending(item => item.VersionNumber)
            .FirstOrDefaultAsync(ct);
    }

    private static DocumentRequirementMatrixEntity? ResolveMatrix(AssetEntity asset, IReadOnlyCollection<DocumentRequirementMatrixEntity> matrices) => matrices
        .Where(item => item.FaenaId == asset.FaenaId && item.AssetTypeId == asset.AssetTypeId && (item.EquipmentFamilyId == null || item.EquipmentFamilyId == asset.FamilyId))
        .OrderByDescending(item => item.EquipmentFamilyId.HasValue).ThenByDescending(item => item.ValidFrom).ThenByDescending(item => item.VersionNumber)
        .FirstOrDefault();

    private static DocumentEntity? SelectDocument(IEnumerable<DocumentEntity> documents, Guid ownerAssetId, Guid matrixId, DocumentRequirementMatrixItemEntity requirement) => documents
        .Where(document => !document.IsHistorical && !document.IsAnnulled && document.DocumentTypeId == requirement.DocumentTypeId && (document.RequirementAssetId == null || document.RequirementAssetId == ownerAssetId) && (!document.RequirementMatrixId.HasValue || document.RequirementMatrixId == matrixId || requirement.ReusableBetweenFaenas))
        .OrderByDescending(document => DocumentComplianceCalculator.Evaluate(document.Status, document.ExpiresOn, requirement.AlertDays, document.Versions.Any(version => version.IsCurrent), requirement.BlocksAvailability).IsCompliant)
        .ThenByDescending(document => document.CreatedAtUtc)
        .FirstOrDefault();

    private async Task AuditAsync(UserAccessContext user, string action, OperationalUnitEntity unit, string documentId, object detail, string? reason, CancellationToken ct) =>
        await audit.RecordAsync(new AuditEventRequest(user.UserId, action, AuditModules.Documents, "OperationalUnitDocument", documentId, null, JsonSerializer.Serialize(detail), unit.Faena?.Code, AuditSeverity.Medium, reason, Detail: $"Operación iniciada desde la unidad {unit.Code}."), ct);

    private void EnsureView(OperationalUnitEntity unit, UserAccessContext user)
    {
        if (unit.Faena is null || authorization.CanViewFaena(user, unit.Faena.Code)) return;
        throw new UnauthorizedAccessException("El usuario no tiene acceso a la faena de la unidad operativa.");
    }

    private static string RequirementKey(Guid assetId, Guid matrixItemId) => $"{assetId:D}:{matrixItemId:D}";
    private static bool TryParseRequirementKey(string value, out Guid assetId, out Guid matrixItemId)
    {
        assetId = Guid.Empty; matrixItemId = Guid.Empty;
        var parts = value.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 && Guid.TryParse(parts[0], out assetId) && Guid.TryParse(parts[1], out matrixItemId);
    }
    private static bool Same(string? left, string? right) => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
    private static int RoleOrder(string role) => Same(role, "CHASIS") ? 0 : Same(role, "FABRICA") ? 1 : 2;
    private static string RoleLabel(string role) => Same(role, "CHASIS") ? "el chasis" : Same(role, "FABRICA") ? "la fábrica" : role;
}