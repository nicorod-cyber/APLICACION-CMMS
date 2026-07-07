using MaintenanceCMMS.Domain.Assets;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Common.ValueObjects;
using MaintenanceCMMS.Domain.Documents;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Domain.Inventory;
using MaintenanceCMMS.Domain.Procurement;
using MaintenanceCMMS.Domain.WorkOrders;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class DomainRulesTests
{
    [Fact]
    public void CriticalExpiredDocument_BlocksAssetAvailability()
    {
        var asset = new Asset(new EntityCode("EQ-001"), "Primary crusher", EntityId.New(), EntityId.New());
        var document = new AssetDocument(asset.Id, EntityId.New(), "docs/certificado.pdf", new DateOnly(2026, 1, 1), isCritical: true);

        var blocksAvailability = asset.IsAvailabilityBlockedByDocuments([document], new DateOnly(2026, 6, 30));

        Assert.True(blocksAvailability);
        Assert.True(document.BlocksAvailability(new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void StockAvailable_DiscountsReservedQuantity()
    {
        var stockItem = new StockItem(EntityId.New(), EntityId.New(), physicalQuantity: 10);

        stockItem.Reserve(3, EntityId.New(), "warehouse-user");

        Assert.Equal(10, stockItem.PhysicalQuantity);
        Assert.Equal(3, stockItem.ReservedQuantity);
        Assert.Equal(7, stockItem.AvailableQuantity);
    }

    [Fact]
    public void WorkOrder_DoesNotClose_WhenRequiredEvidenceIsMissing()
    {
        var workOrder = new WorkOrder(EntityId.New(), MaintenanceType.Corrective, "Repair conveyor");
        var task = new WorkOrderTask(workOrder.Id, "Inspect belt", requiresEvidence: true, requiresLabor: false);
        workOrder.AddTask(task);

        var exception = Assert.Throws<DomainException>(() => workOrder.Close("supervisor"));

        Assert.Contains("required evidence", exception.Message);
        Assert.Equal(WorkOrderStatus.PendingEvidence, workOrder.Status);
    }

    [Fact]
    public void WorkOrder_DoesNotClose_WhenMandatoryLaborIsMissing()
    {
        var workOrder = new WorkOrder(EntityId.New(), MaintenanceType.Corrective, "Repair conveyor");
        var task = new WorkOrderTask(workOrder.Id, "Inspect belt", requiresEvidence: false, requiresLabor: true);
        workOrder.AddTask(task);

        var exception = Assert.Throws<DomainException>(() => workOrder.Close("supervisor"));

        Assert.Contains("labor hours", exception.Message);
        Assert.Equal(WorkOrderStatus.PendingLabor, workOrder.Status);
    }

    [Fact]
    public void MaintenanceConsumption_WithoutAssetOrWorkOrder_Fails()
    {
        var exception = Assert.Throws<DomainException>(() =>
            StockMovement.CreateMaintenanceConsumption(EntityId.New(), quantity: 1, assetId: null, workOrderId: null));

        Assert.Contains("asset or work order", exception.Message);
    }

    [Fact]
    public void NonCodedMaterial_CanConvertToSparePartMaster()
    {
        var request = new NonCodedMaterialRequest("Special bearing", quantity: 2);

        var sparePart = request.ConvertToSparePart(new EntityCode("RP-001"), sapCode: "SAP-7788", userId: "planner");

        Assert.True(request.ConvertedToSparePart);
        Assert.Equal(sparePart.Id, request.ConvertedSparePartId);
        Assert.Equal("Special bearing", sparePart.Description);
        Assert.Equal("SAP-7788", sparePart.SapCode);
    }

    [Fact]
    public void ValidatedDocument_BlocksCriticalFieldChanges()
    {
        var document = new AssetDocument(EntityId.New(), EntityId.New(), "docs/manual.pdf", new DateOnly(2026, 12, 31), isCritical: true);
        document.Validate("validator");

        var exception = Assert.Throws<DomainException>(() =>
            document.UpdateCriticalMetadata(new DateOnly(2027, 12, 31), isCritical: false, userId: "planner"));

        Assert.Contains("Validated documents", exception.Message);
    }
}

