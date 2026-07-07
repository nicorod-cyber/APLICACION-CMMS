using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.Procurement;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Procurement;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class ProcurementServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.AdjustStock, AuthPermissions.ViewGlobalWarehouses],
        []);

    [Fact]
    public async Task CreateRequestAsync_CreatesInternalProcurementRequest()
    {
        var fixture = CreateFixture();

        var created = await fixture.Service.CreateRequestAsync(Request(), Admin, CancellationToken.None);

        Assert.StartsWith("AB-", created.SolicitudId);
        Assert.Equal(ProcurementRequestStatus.EnviadaAbastecimiento, created.Estado);
        Assert.Equal("REP-001", created.RepuestoCodigo);
        Assert.Equal(4, created.Cantidad);
    }

    [Fact]
    public async Task LinkPurchaseOrderAsync_AssociatesSupplierAndOc()
    {
        var fixture = CreateFixture();
        await fixture.Service.CreateSupplierAsync(Supplier(), Admin, CancellationToken.None);
        var created = await fixture.Service.CreateRequestAsync(Request(), Admin, CancellationToken.None);

        var updated = await fixture.Service.LinkPurchaseOrderAsync(created.SolicitudId, PurchaseOrder(), Admin, CancellationToken.None);

        Assert.NotNull(updated);
        Assert.Equal(ProcurementRequestStatus.OCAsociada, updated.Estado);
        Assert.Equal("OC-100", updated.OcNumero);
        Assert.Equal("Proveedor Norte", updated.ProveedorNombre);
    }

    [Fact]
    public async Task RegisterReceptionAsync_IngressesStock()
    {
        var fixture = CreateFixture();
        await fixture.Service.CreateSupplierAsync(Supplier(), Admin, CancellationToken.None);
        var created = await fixture.Service.CreateRequestAsync(Request(), Admin, CancellationToken.None);
        await fixture.Service.LinkPurchaseOrderAsync(created.SolicitudId, PurchaseOrder(), Admin, CancellationToken.None);

        var received = await fixture.Service.RegisterReceptionAsync(
            created.SolicitudId,
            new RegisterProcurementReceptionRequest(4, "BOD-01", "Recepcion completa", FechaRecepcion: Day(6), CostoReal: 120000),
            Admin,
            CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(ProcurementRequestStatus.Recepcionada, received.Estado);
        Assert.Equal(4, received.CantidadRecibida);
        Assert.Single(fixture.Inventory.Receptions);
    }

    [Fact]
    public async Task RegisterReceptionAsync_CalculatesLeadTimeByStage()
    {
        var fixture = CreateFixture();
        await fixture.Service.CreateSupplierAsync(Supplier(), Admin, CancellationToken.None);
        var created = await fixture.Service.CreateRequestAsync(Request(), Admin, CancellationToken.None);
        await fixture.Service.LinkPurchaseOrderAsync(created.SolicitudId, PurchaseOrder(), Admin, CancellationToken.None);

        var received = await fixture.Service.RegisterReceptionAsync(
            created.SolicitudId,
            new RegisterProcurementReceptionRequest(4, "BOD-01", "Recepcion y despacho", FechaRecepcion: Day(6), DespachoDirectoOt: true, OtNumero: "OT-1", FechaEntrega: Day(7)),
            Admin,
            CancellationToken.None);

        Assert.NotNull(received);
        Assert.Equal(1, received.LeadTime.SolicitudAprobacionDias);
        Assert.Equal(1, received.LeadTime.AprobacionEnvioDias);
        Assert.Equal(2, received.LeadTime.EnvioOCDias);
        Assert.Equal(2, received.LeadTime.OCRecepcionDias);
        Assert.Equal(1, received.LeadTime.RecepcionEntregaDias);
        Assert.Equal(7, received.LeadTime.TotalDias);
        Assert.Single(fixture.Inventory.Deliveries);
    }

    private static Fixture CreateFixture()
    {
        var inventory = new FakeInventoryService();
        var service = new ProcurementService(new InMemoryDataProvider(), inventory, new NullAuditService());
        return new Fixture(service, inventory);
    }

    private static UpsertSupplierRequest Supplier()
    {
        return new UpsertSupplierRequest("76.111.222-3", "Proveedor Norte", "Compras", "compras@example.local", LeadTimeEsperadoDias: 5);
    }

    private static CreateProcurementRequestRequest Request()
    {
        return new CreateProcurementRequestRequest(
            "Filtro hidraulico",
            4,
            "UN",
            "Stock bajo",
            SolicitudInternaCmms: "SOL-1",
            RepuestoCodigo: "REP-001",
            FaenaCodigo: "FAE-1",
            BodegaCodigo: "BOD-01",
            OtNumero: "OT-1",
            FechaSolicitudTecnica: Day(0),
            FechaAprobacionMantenimiento: Day(1),
            FechaEnvioAbastecimiento: Day(2),
            CostoEstimado: 100000,
            DocumentoRespaldoUrl: "https://sharepoint.local/respaldo");
    }

    private static LinkPurchaseOrderRequest PurchaseOrder()
    {
        return new LinkPurchaseOrderRequest(
            "OC-100",
            "76.111.222-3",
            Day(8),
            "OC emitida",
            SolicitudExternaNumero: "SAP-REQ-100",
            FechaOC: Day(4),
            CostoOC: 110000,
            DocumentoOcUrl: "https://sharepoint.local/oc");
    }

    private static DateTimeOffset Day(int offset) => new(2026, 1, 1 + offset, 0, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(ProcurementService Service, FakeInventoryService Inventory);

    private sealed class InMemoryDataProvider : IDataProvider
    {
        private readonly Dictionary<string, IReadOnlyList<DataRow>> _rows = new(StringComparer.OrdinalIgnoreCase)
        {
            ["proveedores"] = [],
            ["abastecimiento_solicitudes"] = [],
            ["ordenes_compra"] = [],
            ["recepciones_abastecimiento"] = [],
            ["solicitudes_repuestos"] =
            [
                new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["NumeroSolicitud"] = "SOL-1",
                    ["SolicitadoEnUtc"] = Day(0).ToString("O"),
                    ["AprobadoMantenimientoEnUtc"] = Day(1).ToString("O"),
                    ["RepuestoCodigo"] = "REP-001",
                    ["DescripcionTecnica"] = "Filtro hidraulico",
                    ["Cantidad"] = "4",
                    ["Unidad"] = "UN",
                    ["FaenaCodigo"] = "FAE-1",
                    ["BodegaCodigo"] = "BOD-01",
                    ["OT"] = "OT-1"
                })
            ]
        };

        public string Name => "memory";

        public DataProviderType ProviderType => DataProviderType.Excel;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<DataProviderHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DataProviderHealth("memory", true, "memory", [], []));

        public Task<IReadOnlyList<DataRow>> ReadRowsAsync(string schemaName, CancellationToken cancellationToken)
        {
            return Task.FromResult(_rows.TryGetValue(schemaName, out var rows) ? rows : []);
        }

        public Task SaveRowsAsync(string schemaName, IReadOnlyCollection<DataRow> rows, CancellationToken cancellationToken)
        {
            _rows[schemaName] = rows.ToArray();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<T>> QueryAsync<T>(DataQuery query, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<T>>([]);

        public Task SaveChangesAsync(UnitOfWorkChanges changes, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NullAuditService : IAuditService
    {
        public Task<string> RecordAsync(AuditEventRequest auditEvent, CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid().ToString("N"));

        public Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken) => Task.FromResult(new AuditQueryResult(0, []));
    }

    private sealed class FakeInventoryService : IInventoryService
    {
        public List<StockMovementRequest> Receptions { get; } = [];
        public List<DeliverMaterialRequest> Deliveries { get; } = [];

        public Task<StockMovementResponse> RegisterMovementAsync(StockMovementRequest request, UserAccessContext user, CancellationToken cancellationToken)
        {
            Receptions.Add(request);
            return Task.FromResult(new StockMovementResponse($"MOV-IN-{Receptions.Count}", DateTimeOffset.UtcNow, request.Type, request.RepuestoCodigo, request.BodegaCodigo, request.SourceWarehouseCode, request.TargetWarehouseCode, request.Quantity, 0, request.Quantity, 0, 0, request.Reason, user.UserId, request.ReferenceType, request.ReferenceId, request.AllowNegativeException));
        }

        public Task<StockMovementResponse> DeliverMaterialAsync(DeliverMaterialRequest request, UserAccessContext user, CancellationToken cancellationToken)
        {
            Deliveries.Add(request);
            return Task.FromResult(new StockMovementResponse($"MOV-OUT-{Deliveries.Count}", DateTimeOffset.UtcNow, StockMovementType.MaintenanceConsumption, request.RepuestoCodigo, request.BodegaCodigo, null, null, request.Quantity, request.Quantity, 0, 0, 0, request.Reason, user.UserId, "OT", request.WorkOrderId, false));
        }

        public Task<InventoryDashboardResponse> GetDashboardAsync(UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WarehouseResponse>> ListWarehousesAsync(WarehouseQuery query, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WarehouseResponse> CreateWarehouseAsync(CreateWarehouseRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<SparePartSummary>> ListSparePartsAsync(SparePartQuery query, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SparePartDetail?> GetSparePartAsync(string code, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SparePartDetail> CreateSparePartAsync(CreateSparePartRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SparePartDetail?> UpdateSparePartAsync(string code, UpdateSparePartRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<StockItemResponse>> ListStockAsync(StockQuery query, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<StockMovementResponse>> ListMovementsAsync(StockMovementQuery query, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<StockReservationResponse>> ListReservationsAsync(UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockReservationResponse> CreateReservationAsync(CreateStockReservationRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockReservationResponse?> ReleaseReservationAsync(string reservationId, ReleaseStockReservationRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<StockTransferResponse>> ListTransfersAsync(UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockTransferResponse> TransferStockAsync(TransferStockRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockTransferResponse?> ReceiveTransferAsync(string transferId, ReceiveTransferRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockMovementResponse> ReturnStockAsync(ReturnStockRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockMovementResponse> AdjustStockAsync(AdjustStockRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockMovementResponse> WriteOffStockAsync(WriteOffStockRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
