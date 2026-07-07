using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.MaterialRequests;
using MaintenanceCMMS.Infrastructure.MaterialRequests;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class MaterialRequestServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.AdjustStock, AuthPermissions.ViewGlobalWarehouses],
        []);

    [Fact]
    public async Task ReviewWarehouseAsync_WithAvailableStock_CreatesReservation()
    {
        var inventory = new FakeInventoryService
        {
            Stock =
            [
                new StockItemResponse("BOD-01", "Bodega central", "FAE-1", "REP-001", "Filtro", "UN", false, 10, 0, 10, 1, 20, 5, false, false, null)
            ]
        };
        var service = CreateService(inventory);

        var created = await service.CreateAsync(CodedRequest(), Admin, CancellationToken.None);
        await service.ApproveMaintenanceAsync(created.NumeroSolicitud, new MaterialRequestReasonRequest("Aprobado"), Admin, CancellationToken.None);

        var reviewed = await service.ReviewWarehouseAsync(
            created.NumeroSolicitud,
            new WarehouseReviewMaterialRequestRequest("BOD-01", "Reservar stock"),
            Admin,
            CancellationToken.None);

        Assert.NotNull(reviewed);
        Assert.Equal(MaterialRequestStatus.Reservada, reviewed.Estado);
        Assert.Equal("RES-001", reviewed.ReservaId);
        Assert.Single(inventory.Reservations);
    }

    [Fact]
    public async Task ReviewWarehouseAsync_WithoutAvailableStock_DerivesToSupply()
    {
        var inventory = new FakeInventoryService
        {
            Stock =
            [
                new StockItemResponse("BOD-01", "Bodega central", "FAE-1", "REP-001", "Filtro", "UN", false, 0, 0, 0, 1, 20, 5, true, false, null)
            ]
        };
        var service = CreateService(inventory);

        var created = await service.CreateAsync(CodedRequest(), Admin, CancellationToken.None);
        await service.ApproveMaintenanceAsync(created.NumeroSolicitud, new MaterialRequestReasonRequest("Aprobado"), Admin, CancellationToken.None);
        var reviewed = await service.ReviewWarehouseAsync(created.NumeroSolicitud, new WarehouseReviewMaterialRequestRequest("BOD-01", "Sin stock"), Admin, CancellationToken.None);

        Assert.NotNull(reviewed);
        Assert.Equal(MaterialRequestStatus.PendienteAbastecimiento, reviewed.Estado);
        Assert.Null(reviewed.ReservaId);
    }

    [Fact]
    public async Task CreateAsync_AllowsNonCodedMaterialRequest()
    {
        var service = CreateService(new FakeInventoryService());

        var created = await service.CreateAsync(new CreateMaterialRequestRequest(
            MaterialRequestSource.Tarea,
            MaterialRequestType.MaterialNoCodificado,
            "Manguera especial 2 pulgadas con acople",
            2,
            "UN",
            "Reparacion correctiva",
            OtNumero: "OT-200",
            TareaCodigo: "T-01",
            FaenaCodigo: "FAE-1"), Admin, CancellationToken.None);

        Assert.Equal(MaterialRequestType.MaterialNoCodificado, created.Tipo);
        Assert.Equal(MaterialRequestStatus.PendienteAprobacionMantenimiento, created.Estado);
        Assert.Null(created.RepuestoCodigo);
    }

    [Fact]
    public async Task ConvertToSparePartAsync_StoresMasterSparePartCode()
    {
        var inventory = new FakeInventoryService();
        var service = CreateService(inventory);
        var created = await service.CreateAsync(new CreateMaterialRequestRequest(
            MaterialRequestSource.Bodega,
            MaterialRequestType.MaterialNoCodificado,
            "Sensor de presion no catalogado",
            1,
            "UN",
            "Normalizar maestro",
            FaenaCodigo: "FAE-1",
            BodegaCodigo: "BOD-01"), Admin, CancellationToken.None);
        await service.ApproveMaintenanceAsync(created.NumeroSolicitud, new MaterialRequestReasonRequest("Aprobado"), Admin, CancellationToken.None);
        await service.ReviewWarehouseAsync(created.NumeroSolicitud, new WarehouseReviewMaterialRequestRequest("BOD-01", "Material no codificado"), Admin, CancellationToken.None);

        var converted = await service.ConvertToSparePartAsync(
            created.NumeroSolicitud,
            new ConvertMaterialRequestToSparePartRequest("Sensor presion", "UN", CodigoSap: "SAP-900"),
            Admin,
            CancellationToken.None);

        Assert.NotNull(converted);
        Assert.Equal("REP-NEW", converted.RepuestoMaestroCodigo);
        Assert.Equal("REP-NEW", converted.RepuestoCodigo);
        Assert.Single(inventory.CreatedSpareParts);
    }

    private static MaterialRequestService CreateService(FakeInventoryService inventory)
    {
        return new MaterialRequestService(new InMemoryDataProvider(), inventory, new NullAuditService());
    }

    private static CreateMaterialRequestRequest CodedRequest()
    {
        return new CreateMaterialRequestRequest(
            MaterialRequestSource.OT,
            MaterialRequestType.RepuestoCodificado,
            "Filtro hidraulico 10 micras",
            2,
            "UN",
            "Mantencion preventiva",
            RepuestoCodigo: "REP-001",
            OtNumero: "OT-100",
            ActivoCodigo: "ACT-1",
            FaenaCodigo: "FAE-1");
    }

    private sealed class InMemoryDataProvider : IDataProvider
    {
        private readonly Dictionary<string, IReadOnlyList<DataRow>> _rows = new(StringComparer.OrdinalIgnoreCase)
        {
            ["solicitudes_repuestos"] = []
        };

        public string Name => "memory";

        public MaintenanceCMMS.Domain.Enums.DataProviderType ProviderType => MaintenanceCMMS.Domain.Enums.DataProviderType.Excel;

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
        public IReadOnlyCollection<StockItemResponse> Stock { get; init; } = [];
        public List<CreateStockReservationRequest> Reservations { get; } = [];
        public List<CreateSparePartRequest> CreatedSpareParts { get; } = [];

        public Task<IReadOnlyCollection<StockItemResponse>> ListStockAsync(StockQuery query, UserAccessContext user, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyCollection<StockItemResponse>>(Stock
                .Where(item => string.IsNullOrWhiteSpace(query.BodegaCodigo) || item.BodegaCodigo == query.BodegaCodigo)
                .Where(item => string.IsNullOrWhiteSpace(query.RepuestoCodigo) || item.RepuestoCodigo == query.RepuestoCodigo)
                .ToArray());
        }

        public Task<StockReservationResponse> CreateReservationAsync(CreateStockReservationRequest request, UserAccessContext user, CancellationToken cancellationToken)
        {
            Reservations.Add(request);
            return Task.FromResult(new StockReservationResponse("RES-001", StockReservationStatus.Activa, DateTimeOffset.UtcNow, request.RepuestoCodigo, request.BodegaCodigo, request.Quantity, 0, 0, request.Quantity, request.WorkOrderId, request.RequestedBy, request.Reason, user.UserId));
        }

        public Task<StockMovementResponse> DeliverMaterialAsync(DeliverMaterialRequest request, UserAccessContext user, CancellationToken cancellationToken)
        {
            return Task.FromResult(new StockMovementResponse("MOV-001", DateTimeOffset.UtcNow, MaintenanceCMMS.Domain.Enums.StockMovementType.MaintenanceConsumption, request.RepuestoCodigo, request.BodegaCodigo, null, null, request.Quantity, 10, 8, 2, 0, request.Reason, user.UserId, "SolicitudRepuesto", request.WorkOrderId, false));
        }

        public Task<SparePartDetail> CreateSparePartAsync(CreateSparePartRequest request, UserAccessContext user, CancellationToken cancellationToken)
        {
            CreatedSpareParts.Add(request);
            var summary = new SparePartSummary("REP-NEW", request.CodigoSap, request.CodigoProveedor, request.Descripcion, request.DescripcionTecnica ?? request.Descripcion, request.UnidadMedida, request.FamiliaEquipo, request.MarcaFabricante, request.ModeloReferencia, request.Critico, request.StockMinimo ?? 0, request.StockMaximo ?? 0, request.PuntoReposicion ?? 0, request.LeadTimeEsperadoDias ?? 0, null, SparePartStatus.Activo, false, request.ProveedorPreferente, null, 0, 0, 0, false, false);
            return Task.FromResult(new SparePartDetail(summary, [], []));
        }

        public Task<InventoryDashboardResponse> GetDashboardAsync(UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<WarehouseResponse>> ListWarehousesAsync(WarehouseQuery query, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WarehouseResponse> CreateWarehouseAsync(CreateWarehouseRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<SparePartSummary>> ListSparePartsAsync(SparePartQuery query, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SparePartDetail?> GetSparePartAsync(string code, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SparePartDetail?> UpdateSparePartAsync(string code, UpdateSparePartRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<StockMovementResponse>> ListMovementsAsync(StockMovementQuery query, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockMovementResponse> RegisterMovementAsync(StockMovementRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<StockReservationResponse>> ListReservationsAsync(UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockReservationResponse?> ReleaseReservationAsync(string reservationId, ReleaseStockReservationRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<StockTransferResponse>> ListTransfersAsync(UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockTransferResponse> TransferStockAsync(TransferStockRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockTransferResponse?> ReceiveTransferAsync(string transferId, ReceiveTransferRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockMovementResponse> ReturnStockAsync(ReturnStockRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockMovementResponse> AdjustStockAsync(AdjustStockRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockMovementResponse> WriteOffStockAsync(WriteOffStockRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
