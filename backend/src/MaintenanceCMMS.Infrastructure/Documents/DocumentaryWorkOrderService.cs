using System.Data;
using MaintenanceCMMS.Application.Documents;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Assets;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Documents;

public sealed class DocumentaryWorkOrderService(CmmsDbContext db) : IDocumentaryWorkOrderService
{
    public async Task<DocumentaryEngineRunResponse> RunAsync(DateOnly referenceDate, string executedBy, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (db.Database.IsNpgsql()) await db.Database.ExecuteSqlRawAsync("LOCK TABLE detalles_ot_documental IN SHARE ROW EXCLUSIVE MODE", ct);
        var matrices = await db.DocumentRequirementMatrices.Include(x => x.Items).ThenInclude(x => x.DocumentType).Where(x => x.Status == "VIGENTE" && x.ValidFrom <= referenceDate && (x.ValidTo == null || x.ValidTo >= referenceDate)).ToListAsync(ct);
        var assets = await db.Assets.Include(x => x.Faena).Include(x => x.OperationalState).Where(x => x.FaenaId != null).ToListAsync(ct);
        var status = await db.WorkCatalogs.SingleAsync(x => x.Category == "WorkOrderLifecycleStatus" && x.Code == WorkOrderLifecycleStatus.OTCreada.ToString(), ct);
        var maintenanceType = await db.WorkCatalogs.SingleOrDefaultAsync(x => x.Category == "MaintenanceType" && x.Code == MaintenanceType.Documentary.ToString(), ct);
        if (maintenanceType is null)
        {
            maintenanceType = new WorkCatalogEntity { Category = "MaintenanceType", Code = MaintenanceType.Documentary.ToString(), Name = "Documental", IsActive = true, SortOrder = 50 };
            db.WorkCatalogs.Add(maintenanceType); await db.SaveChangesAsync(ct);
        }
        var createdOrders = 0; var reusedOrders = 0; var createdRequirements = 0; var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var asset in assets)
        {
            if (AssetOperationalPolicy.IsDecommissioned(asset)) continue;
            var matrix = matrices.Where(x => x.AssetTypeId == asset.AssetTypeId && (x.EquipmentFamilyId == null || x.EquipmentFamilyId == asset.FamilyId)).OrderByDescending(x => x.EquipmentFamilyId.HasValue).ThenByDescending(x => x.ValidFrom).ThenByDescending(x => x.VersionNumber).FirstOrDefault();
            if (matrix is null) continue;
            var documents = await db.DocumentAssets.Include(x => x.Document).ThenInclude(x => x.Versions).Where(x => x.AssetId == asset.Id && x.IsActive).Select(x => x.Document).ToListAsync(ct);
            var due = new List<(DocumentRequirementMatrixItemEntity Item, DocumentEntity? Document, DocumentVersionEntity? Version, DocumentComplianceResult Result, string Cycle)>();
            foreach (var item in matrix.Items)
            {
                var document = documents.Where(x => x.DocumentTypeId == item.DocumentTypeId).OrderByDescending(x => x.CreatedAtUtc).FirstOrDefault();
                var version = document?.Versions.OrderByDescending(x => x.IsCurrent).ThenByDescending(x => x.VersionNumber).FirstOrDefault();
                var result = DocumentComplianceCalculator.Evaluate(document?.Status, version?.ExpiresOn ?? document?.ExpiresOn, item.AlertDays, version is not null, item.BlocksAvailability, referenceDate);
                var entersWindow = result.DaysToExpire is null ? !result.IsCompliant : result.DaysToExpire <= 45;
                if (!entersWindow) continue;
                var cycle = version is null ? $"MISSING:{matrix.Id:N}:{item.Id:N}" : $"VERSION:{version.Id:N}";
                due.Add((item, document, version, result, cycle));
            }
            if (due.Count == 0) continue;
            var existingCycles = await db.DocumentaryWorkOrderRequirements.Where(x => x.AssetId == asset.Id && x.MatrixVersionId == matrix.Id).Select(x => x.CycleKey).ToListAsync(ct);
            var newDue = due.Where(x => !existingCycles.Contains(x.Cycle, StringComparer.OrdinalIgnoreCase)).ToList();
            var existingDetails = await db.DocumentaryWorkOrderRequirements.Include(x => x.WorkOrder).Where(x => x.AssetId == asset.Id && x.MatrixVersionId == matrix.Id && due.Select(d => d.Cycle).Contains(x.CycleKey)).ToListAsync(ct);
            foreach (var detail in existingDetails)
            {
                var current = due.Single(x => x.Cycle == detail.CycleKey);
                detail.Status = DocumentComplianceCalculator.ToCode(current.Result.Status); detail.Observation = current.Result.Observation; detail.CompletedAtUtc = current.Result.IsCompliant ? detail.CompletedAtUtc ?? DateTimeOffset.UtcNow : null;
                numbers.Add(detail.WorkOrder.WorkOrderNumber);
            }
            if (newDue.Count == 0) continue;
            var order = await db.WorkOrders.Include(x => x.Status).FirstOrDefaultAsync(x => x.AssetId == asset.Id && x.DocumentaryMatrixVersionId == matrix.Id && x.Status.Code != WorkOrderLifecycleStatus.ValidadaPlanificacion.ToString() && x.Status.Code != WorkOrderLifecycleStatus.Anulada.ToString(), ct);
            if (order is null)
            {
                var sequence = await db.Database.SqlQueryRaw<long>("SELECT nextval('work_order_number_seq') AS \"Value\"").SingleAsync(ct);
                order = new WorkOrderEntity { WorkOrderNumber = $"OT-{sequence:D6}", AssetId = asset.Id, FaenaId = asset.FaenaId!.Value, StatusId = status.Id, MaintenanceTypeId = maintenanceType.Id, DocumentaryMatrixVersionId = matrix.Id, Description = $"Regularizacion documental {matrix.Code} v{matrix.VersionNumber} - {asset.Code}", CreatedByUserId = executedBy, CreatedByUserAtUtc = DateTimeOffset.UtcNow };
                db.WorkOrders.Add(order); createdOrders++;
                db.WorkOrderAssets.Add(new WorkOrderAssetEntity { WorkOrder = order, AssetId = asset.Id, Role = "PRINCIPAL", AssetCodeSnapshot = asset.Code, AssetNameSnapshot = asset.Name, AddedAtUtc = DateTimeOffset.UtcNow, AddedByUserId = executedBy });
            }
            else reusedOrders++;
            numbers.Add(order.WorkOrderNumber);
            foreach (var current in newDue)
            {
                db.DocumentaryWorkOrderRequirements.Add(new DocumentaryWorkOrderRequirementEntity { WorkOrder = order, AssetId = asset.Id, MatrixVersionId = matrix.Id, MatrixItemId = current.Item.Id, OriginDocumentId = current.Document?.Id, OriginDocumentVersionId = current.Version?.Id, CycleKey = current.Cycle, Status = DocumentComplianceCalculator.ToCode(current.Result.Status), IsApplicable = true, Observation = current.Result.Observation, CompletedAtUtc = current.Result.IsCompliant ? DateTimeOffset.UtcNow : null });
                createdRequirements++;
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new(referenceDate, assets.Count, createdOrders, reusedOrders, createdRequirements, numbers.OrderBy(x => x).ToArray());
    }
}
