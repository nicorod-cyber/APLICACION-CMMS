using System.Data;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Documents;

/// <summary>Creates one documentary OT per operational unit while retaining a technical owner for every requirement.</summary>
public sealed class DocumentaryWorkOrderService(CmmsDbContext db) : IDocumentaryWorkOrderService
{
    private sealed record DueItem(AssetEntity Asset, string? RoleCode, DocumentRequirementMatrixEntity Matrix, DocumentRequirementMatrixItemEntity Item, DocumentEntity? Document, DocumentVersionEntity? Version, DocumentComplianceResult Result, string Cycle);

    public async Task<DocumentaryEngineRunResponse> RunAsync(DateOnly referenceDate, string executedBy, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("LOCK TABLE detalles_ot_documental IN SHARE ROW EXCLUSIVE MODE", ct);
        var matrices = await db.DocumentRequirementMatrices.Include(x => x.Items).ThenInclude(x => x.DocumentType).Where(x => x.FaenaId != null && x.Status == "VIGENTE" && x.ValidFrom <= referenceDate && (x.ValidTo == null || x.ValidTo >= referenceDate)).ToListAsync(ct);
        var components = await db.OperationalUnitComponents
            .Include(x => x.OperationalUnit).ThenInclude(x => x.Faena)
            .Include(x => x.ComponentRole)
            .Include(x => x.Asset).ThenInclude(x => x.Faena)
            .Include(x => x.Asset).ThenInclude(x => x.OperationalState)
            .Include(x => x.Asset).ThenInclude(x => x.AssetTypeDefinition)
            .Include(x => x.Asset).ThenInclude(x => x.Family)
            .Where(x => x.RemovedAtUtc == null)
            .ToListAsync(ct);
        var mountedAssetIds = components.Select(x => x.AssetId).Distinct().ToArray();
        var assets = await db.Assets.Include(x => x.Faena).Include(x => x.OperationalState).Where(x => x.FaenaId != null && !mountedAssetIds.Contains(x.Id)).ToListAsync(ct);
        var status = await db.WorkCatalogs.SingleAsync(x => x.Category == "WorkOrderLifecycleStatus" && x.Code == WorkOrderLifecycleStatus.OTCreada.ToString(), ct);
        var maintenanceType = await db.WorkCatalogs.SingleOrDefaultAsync(x => x.Category == "MaintenanceType" && x.Code == MaintenanceType.Documentary.ToString(), ct);
        if (maintenanceType is null)
        {
            maintenanceType = new WorkCatalogEntity { Category = "MaintenanceType", Code = MaintenanceType.Documentary.ToString(), Name = "Documental", IsActive = true, SortOrder = 50 };
            db.WorkCatalogs.Add(maintenanceType); await db.SaveChangesAsync(ct);
        }

        var createdOrders = 0; var reusedOrders = 0; var createdRequirements = 0; var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in components.GroupBy(item => item.OperationalUnitId))
        {
            var unit = group.First().OperationalUnit;
            if (unit.FaenaId is null) continue;
            var due = new List<DueItem>();
            foreach (var component in group)
            {
                if (AssetOperationalPolicy.IsDecommissioned(component.Asset)) continue;
                var matrix = ResolveMatrix(component.Asset, matrices);
                if (matrix is null) continue;
                var documents = await DocumentsForAssetAsync(component.AssetId, ct);
                due.AddRange(DueForAsset(component.Asset, component.ComponentRole.Code, matrix, documents, referenceDate));
            }
            if (due.Count == 0) continue;
            var existingDetails = await db.DocumentaryWorkOrderRequirements.Include(x => x.WorkOrder)
                .Where(x => x.WorkOrder.OperationalUnitId == unit.Id)
                .ToListAsync(ct);
            var activeDetails = existingDetails.Where(detail => due.Any(current => current.Asset.Id == detail.AssetId && current.Matrix.Id == detail.MatrixVersionId && current.Cycle == detail.CycleKey)).ToArray();
            foreach (var detail in activeDetails)
            {
                var current = due.Single(item => item.Asset.Id == detail.AssetId && item.Matrix.Id == detail.MatrixVersionId && item.Cycle == detail.CycleKey);
                detail.Status = DocumentComplianceCalculator.ToCode(current.Result.Status); detail.Observation = current.Result.Observation; detail.CompletedAtUtc = current.Result.IsCompliant ? detail.CompletedAtUtc ?? DateTimeOffset.UtcNow : null;
                numbers.Add(detail.WorkOrder.WorkOrderNumber);
            }
            var newDue = due.Where(current => !existingDetails.Any(detail => detail.AssetId == current.Asset.Id && detail.MatrixVersionId == current.Matrix.Id && detail.CycleKey == current.Cycle)).ToArray();
            if (newDue.Length == 0) continue;
            var order = await db.WorkOrders.Include(x => x.Status).FirstOrDefaultAsync(x => x.OperationalUnitId == unit.Id && x.MaintenanceTypeId == maintenanceType.Id && x.Status.Code != WorkOrderLifecycleStatus.ValidadaPlanificacion.ToString() && x.Status.Code != WorkOrderLifecycleStatus.Anulada.ToString(), ct);
            if (order is null)
            {
                var sequence = await NextSequenceAsync(ct);
                order = new WorkOrderEntity { WorkOrderNumber = $"OT-{sequence:D6}", OperationalUnitId = unit.Id, FaenaId = unit.FaenaId.Value, StatusId = status.Id, MaintenanceTypeId = maintenanceType.Id, DocumentaryMatrixVersionId = newDue[0].Matrix.Id, Description = $"Regularizacion documental consolidada - {unit.Code}", CreatedByUserId = executedBy, CreatedByUserAtUtc = DateTimeOffset.UtcNow };
                db.WorkOrders.Add(order); createdOrders++;
                foreach (var component in group)
                    db.WorkOrderAssets.Add(new WorkOrderAssetEntity { WorkOrder = order, AssetId = component.AssetId, Role = component.ComponentRole.Code, AssetCodeSnapshot = component.Asset.Code, AssetNameSnapshot = component.Asset.Name, AddedAtUtc = DateTimeOffset.UtcNow, AddedByUserId = executedBy });
            }
            else reusedOrders++;
            numbers.Add(order.WorkOrderNumber);
            foreach (var current in newDue) { db.DocumentaryWorkOrderRequirements.Add(CreateRequirement(order, current)); createdRequirements++; }
        }

        foreach (var asset in assets)
        {
            if (AssetOperationalPolicy.IsDecommissioned(asset)) continue;
            var matrix = ResolveMatrix(asset, matrices);
            if (matrix is null) continue;
            var due = DueForAsset(asset, null, matrix, await DocumentsForAssetAsync(asset.Id, ct), referenceDate);
            if (due.Count == 0) continue;
            var existingDetails = await db.DocumentaryWorkOrderRequirements.Include(x => x.WorkOrder).Where(x => x.AssetId == asset.Id && x.MatrixVersionId == matrix.Id).ToListAsync(ct);
            foreach (var detail in existingDetails.Where(detail => due.Any(current => current.Cycle == detail.CycleKey)))
            {
                var current = due.Single(item => item.Cycle == detail.CycleKey);
                detail.Status = DocumentComplianceCalculator.ToCode(current.Result.Status); detail.Observation = current.Result.Observation; detail.CompletedAtUtc = current.Result.IsCompliant ? detail.CompletedAtUtc ?? DateTimeOffset.UtcNow : null;
                numbers.Add(detail.WorkOrder.WorkOrderNumber);
            }
            var newDue = due.Where(current => !existingDetails.Any(detail => detail.CycleKey == current.Cycle)).ToArray();
            if (newDue.Length == 0) continue;
            var order = await db.WorkOrders.Include(x => x.Status).FirstOrDefaultAsync(x => x.AssetId == asset.Id && x.DocumentaryMatrixVersionId == matrix.Id && x.Status.Code != WorkOrderLifecycleStatus.ValidadaPlanificacion.ToString() && x.Status.Code != WorkOrderLifecycleStatus.Anulada.ToString(), ct);
            if (order is null)
            {
                var sequence = await NextSequenceAsync(ct);
                order = new WorkOrderEntity { WorkOrderNumber = $"OT-{sequence:D6}", AssetId = asset.Id, FaenaId = asset.FaenaId!.Value, StatusId = status.Id, MaintenanceTypeId = maintenanceType.Id, DocumentaryMatrixVersionId = matrix.Id, Description = $"Regularizacion documental {matrix.Code} v{matrix.VersionNumber} - {asset.Code}", CreatedByUserId = executedBy, CreatedByUserAtUtc = DateTimeOffset.UtcNow };
                db.WorkOrders.Add(order); createdOrders++;
                db.WorkOrderAssets.Add(new WorkOrderAssetEntity { WorkOrder = order, AssetId = asset.Id, Role = "PRINCIPAL", AssetCodeSnapshot = asset.Code, AssetNameSnapshot = asset.Name, AddedAtUtc = DateTimeOffset.UtcNow, AddedByUserId = executedBy });
            }
            else reusedOrders++;
            numbers.Add(order.WorkOrderNumber);
            foreach (var current in newDue) { db.DocumentaryWorkOrderRequirements.Add(CreateRequirement(order, current)); createdRequirements++; }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(referenceDate, assets.Count + mountedAssetIds.Length, createdOrders, reusedOrders, createdRequirements, numbers.OrderBy(x => x).ToArray());
    }

    private async Task<List<DocumentEntity>> DocumentsForAssetAsync(Guid assetId, CancellationToken ct) => await db.DocumentAssets.Include(x => x.Document).ThenInclude(x => x.Versions).Where(x => x.AssetId == assetId && x.IsActive).Select(x => x.Document).ToListAsync(ct);

    private static DocumentRequirementMatrixEntity? ResolveMatrix(AssetEntity asset, IReadOnlyCollection<DocumentRequirementMatrixEntity> matrices) => matrices.Where(x => x.FaenaId == asset.FaenaId && x.AssetTypeId == asset.AssetTypeId && (x.EquipmentFamilyId == null || x.EquipmentFamilyId == asset.FamilyId)).OrderByDescending(x => x.EquipmentFamilyId.HasValue).ThenByDescending(x => x.ValidFrom).ThenByDescending(x => x.VersionNumber).FirstOrDefault();

    private static List<DueItem> DueForAsset(AssetEntity asset, string? roleCode, DocumentRequirementMatrixEntity matrix, IReadOnlyCollection<DocumentEntity> documents, DateOnly referenceDate)
    {
        var due = new List<DueItem>();
        foreach (var item in matrix.Items)
        {
            var document = documents.Where(x => x.DocumentTypeId == item.DocumentTypeId && (!x.RequirementMatrixId.HasValue || x.RequirementMatrixId == matrix.Id || item.ReusableBetweenFaenas)).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
            var version = document?.Versions.OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.VersionNumber).FirstOrDefault();
            var result = DocumentComplianceCalculator.Evaluate(document?.Status, version?.ExpiresOn ?? document?.ExpiresOn, item.AlertDays, version is not null, item.BlocksAvailability, referenceDate);
            var entersWindow = result.DaysToExpire is null ? !result.IsCompliant : result.DaysToExpire <= item.AlertDays;
            if (entersWindow) due.Add(new DueItem(asset, roleCode, matrix, item, document, version, result, version is null ? $"MISSING:{matrix.Id:N}:{item.Id:N}" : $"VERSION:{version.Id:N}"));
        }
        return due;
    }

    private static DocumentaryWorkOrderRequirementEntity CreateRequirement(WorkOrderEntity order, DueItem current) => new() { WorkOrder = order, AssetId = current.Asset.Id, MatrixVersionId = current.Matrix.Id, MatrixItemId = current.Item.Id, OriginDocumentId = current.Document?.Id, OriginDocumentVersionId = current.Version?.Id, DocumentTypeCodeSnapshot = current.Item.DocumentType.Code, DocumentTypeNameSnapshot = current.Item.DocumentType.Name, FaenaCodeSnapshot = current.Asset.Faena!.Code, IsMandatorySnapshot = current.Item.IsMandatory, IsCriticalSnapshot = current.Item.IsCritical, BlocksAvailabilitySnapshot = current.Item.BlocksAvailability, RequiresExpirationDateSnapshot = current.Item.RequiresExpirationDate, AlertDaysSnapshot = current.Item.AlertDays, ReusableBetweenFaenasSnapshot = current.Item.ReusableBetweenFaenas, CycleKey = current.Cycle, Status = DocumentComplianceCalculator.ToCode(current.Result.Status), IsApplicable = true, Observation = current.Result.Observation, CompletedAtUtc = current.Result.IsCompliant ? DateTimeOffset.UtcNow : null };
    private async Task<long> NextSequenceAsync(CancellationToken ct) => await db.Database.SqlQueryRaw<long>("SELECT nextval('work_order_number_seq') AS \"Value\"").SingleAsync(ct);
}