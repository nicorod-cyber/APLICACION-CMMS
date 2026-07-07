using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Inventory;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class InventoryServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin",
        [AuthRoles.Admin],
        [AuthPermissions.Administration, AuthPermissions.AdjustStock, AuthPermissions.ViewGlobalWarehouses, AuthPermissions.ViewCosts],
        []);

    [Fact]
    public async Task CreateSparePartAsync_GeneratesCmmsCode()
    {
        var fixture = await CreateFixtureAsync();

        var created = await fixture.Service.CreateSparePartAsync(SparePartRequest("Filtro hidraulico", "SAP-100"), Admin, CancellationToken.None);

        Assert.StartsWith("REP-", created.Summary.Codigo);
        Assert.Equal("SAP-100", created.Summary.CodigoSap);
        Assert.False(created.Summary.EsNoCodificado);
    }

    [Fact]
    public async Task CreateSparePartAsync_RejectsDuplicatedSapCode()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateSparePartAsync(SparePartRequest("Filtro hidraulico", "SAP-200"), Admin, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.CreateSparePartAsync(SparePartRequest("Filtro duplicado", "sap-200"), Admin, CancellationToken.None));

        Assert.Contains("codigo SAP", exception.Message);
    }

    [Fact]
    public async Task RegisterMovementAsync_UpdatesStockByWarehouse()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateWarehouseAsync(WarehouseRequest("BOD-01"), Admin, CancellationToken.None);
        var spare = await fixture.Service.CreateSparePartAsync(SparePartRequest("Rodamiento", "SAP-300"), Admin, CancellationToken.None);

        await fixture.Service.RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.Reception,
            spare.Summary.Codigo,
            10,
            "Recepcion inicial",
            BodegaCodigo: "BOD-01"), Admin, CancellationToken.None);

        await fixture.Service.RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.Reservation,
            spare.Summary.Codigo,
            3,
            "Reserva OT",
            BodegaCodigo: "BOD-01",
            ReferenceType: "OT",
            ReferenceId: "OT-1"), Admin, CancellationToken.None);

        var stock = await fixture.Service.ListStockAsync(new StockQuery(BodegaCodigo: "BOD-01"), Admin, CancellationToken.None);

        var item = Assert.Single(stock);
        Assert.Equal(10, item.StockFisico);
        Assert.Equal(3, item.StockReservado);
        Assert.Equal(7, item.StockDisponible);
    }

    [Fact]
    public async Task ListStockAsync_FlagsLowMinimum()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateWarehouseAsync(WarehouseRequest("BOD-02"), Admin, CancellationToken.None);
        var spare = await fixture.Service.CreateSparePartAsync(SparePartRequest("Sensor critico", "SAP-400", critical: true, minimum: 5), Admin, CancellationToken.None);

        await fixture.Service.RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.Reception,
            spare.Summary.Codigo,
            2,
            "Recepcion parcial",
            BodegaCodigo: "BOD-02"), Admin, CancellationToken.None);

        var stock = await fixture.Service.ListStockAsync(new StockQuery(LowStockOnly: true), Admin, CancellationToken.None);
        var dashboard = await fixture.Service.GetDashboardAsync(Admin, CancellationToken.None);

        Assert.Contains(stock, item => item.RepuestoCodigo == spare.Summary.Codigo && item.BajoMinimo);
        Assert.Contains(dashboard.Alerts, alert => alert.RepuestoCodigo == spare.Summary.Codigo);
    }

    [Fact]
    public async Task CreateReservationAsync_ReducesAvailableStockUntilReleased()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateWarehouseAsync(WarehouseRequest("BOD-03"), Admin, CancellationToken.None);
        var spare = await fixture.Service.CreateSparePartAsync(SparePartRequest("Correa", "SAP-500"), Admin, CancellationToken.None);
        await fixture.Service.RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.Reception,
            spare.Summary.Codigo,
            10,
            "Recepcion inicial",
            BodegaCodigo: "BOD-03"), Admin, CancellationToken.None);

        var reservation = await fixture.Service.CreateReservationAsync(new CreateStockReservationRequest(
            spare.Summary.Codigo,
            "BOD-03",
            4,
            "OT-100",
            "planificador",
            "Reserva preventiva"), Admin, CancellationToken.None);
        await fixture.Service.ReleaseReservationAsync(reservation.ReservaId, new ReleaseStockReservationRequest(2, "Liberacion parcial"), Admin, CancellationToken.None);

        var stock = await fixture.Service.ListStockAsync(new StockQuery(BodegaCodigo: "BOD-03"), Admin, CancellationToken.None);
        var item = Assert.Single(stock);
        Assert.Equal(10, item.StockFisico);
        Assert.Equal(2, item.StockReservado);
        Assert.Equal(8, item.StockDisponible);
    }

    [Fact]
    public async Task DeliverMaterialAsync_RequiresReferenceAndDiscountsPhysicalStock()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateWarehouseAsync(WarehouseRequest("BOD-04"), Admin, CancellationToken.None);
        var spare = await fixture.Service.CreateSparePartAsync(SparePartRequest("Perno", "SAP-600"), Admin, CancellationToken.None);
        await fixture.Service.RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.Reception,
            spare.Summary.Codigo,
            5,
            "Recepcion inicial",
            BodegaCodigo: "BOD-04"), Admin, CancellationToken.None);

        await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.DeliverMaterialAsync(new DeliverMaterialRequest(
                spare.Summary.Codigo,
                "BOD-04",
                1,
                "Entrega sin referencia"), Admin, CancellationToken.None));

        await fixture.Service.DeliverMaterialAsync(new DeliverMaterialRequest(
            spare.Summary.Codigo,
            "BOD-04",
            2,
            "Entrega a OT",
            WorkOrderId: "OT-200"), Admin, CancellationToken.None);

        var stock = await fixture.Service.ListStockAsync(new StockQuery(BodegaCodigo: "BOD-04"), Admin, CancellationToken.None);
        Assert.Equal(3, Assert.Single(stock).StockFisico);
    }

    [Fact]
    public async Task TransferStockAsync_UsesTransitWarehouseAndReception()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateWarehouseAsync(WarehouseRequest("BOD-05"), Admin, CancellationToken.None);
        await fixture.Service.CreateWarehouseAsync(WarehouseRequest("BOD-06"), Admin, CancellationToken.None);
        await fixture.Service.CreateWarehouseAsync(WarehouseRequest("TRANS-01", WarehouseType.Transito), Admin, CancellationToken.None);
        var spare = await fixture.Service.CreateSparePartAsync(SparePartRequest("Motor", "SAP-700"), Admin, CancellationToken.None);
        await fixture.Service.RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.Reception,
            spare.Summary.Codigo,
            10,
            "Recepcion inicial",
            BodegaCodigo: "BOD-05"), Admin, CancellationToken.None);

        var transfer = await fixture.Service.TransferStockAsync(new TransferStockRequest(
            spare.Summary.Codigo,
            "BOD-05",
            "TRANS-01",
            "BOD-06",
            4,
            "Traslado a faena"), Admin, CancellationToken.None);

        Assert.Equal(StockTransferStatus.EnTransito, transfer.Estado);
        Assert.Contains(transfer.Movements, item => item.Type == StockMovementType.InTransit);

        var received = await fixture.Service.ReceiveTransferAsync(transfer.TransferenciaId, new ReceiveTransferRequest("Recepcion conforme"), Admin, CancellationToken.None);
        Assert.NotNull(received);
        Assert.Equal(StockTransferStatus.Recibida, received!.Estado);

        var stock = await fixture.Service.ListStockAsync(new StockQuery(RepuestoCodigo: spare.Summary.Codigo), Admin, CancellationToken.None);
        Assert.Equal(6, stock.Single(item => item.BodegaCodigo == "BOD-05").StockFisico);
        Assert.Equal(0, stock.Single(item => item.BodegaCodigo == "TRANS-01").StockFisico);
        Assert.Equal(4, stock.Single(item => item.BodegaCodigo == "BOD-06").StockFisico);
    }

    [Fact]
    public async Task ReturnStockAsync_IncreasesStockWhenReusable()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateWarehouseAsync(WarehouseRequest("BOD-07"), Admin, CancellationToken.None);
        var spare = await fixture.Service.CreateSparePartAsync(SparePartRequest("Valvula", "SAP-800"), Admin, CancellationToken.None);
        await fixture.Service.RegisterMovementAsync(new StockMovementRequest(
            StockMovementType.Reception,
            spare.Summary.Codigo,
            5,
            "Recepcion inicial",
            BodegaCodigo: "BOD-07"), Admin, CancellationToken.None);
        await fixture.Service.DeliverMaterialAsync(new DeliverMaterialRequest(
            spare.Summary.Codigo,
            "BOD-07",
            2,
            "Entrega a OT",
            WorkOrderId: "OT-300"), Admin, CancellationToken.None);

        await fixture.Service.ReturnStockAsync(new ReturnStockRequest(
            spare.Summary.Codigo,
            "BOD-07",
            1,
            true,
            "Devolucion reutilizable",
            WorkOrderId: "OT-300"), Admin, CancellationToken.None);

        var stock = await fixture.Service.ListStockAsync(new StockQuery(BodegaCodigo: "BOD-07"), Admin, CancellationToken.None);
        Assert.Equal(4, Assert.Single(stock).StockFisico);
    }

    [Fact]
    public async Task AdjustStockAsync_AppliesPositiveAndNegativeAdjustments()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Service.CreateWarehouseAsync(WarehouseRequest("BOD-08"), Admin, CancellationToken.None);
        var spare = await fixture.Service.CreateSparePartAsync(SparePartRequest("Sensor", "SAP-900"), Admin, CancellationToken.None);

        await fixture.Service.AdjustStockAsync(new AdjustStockRequest(
            spare.Summary.Codigo,
            "BOD-08",
            3,
            "Ajuste inventario fisico"), Admin, CancellationToken.None);
        await fixture.Service.AdjustStockAsync(new AdjustStockRequest(
            spare.Summary.Codigo,
            "BOD-08",
            -1,
            "Ajuste por diferencia"), Admin, CancellationToken.None);

        var stock = await fixture.Service.ListStockAsync(new StockQuery(BodegaCodigo: "BOD-08"), Admin, CancellationToken.None);
        Assert.Equal(2, Assert.Single(stock).StockFisico);

        await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.AdjustStockAsync(new AdjustStockRequest(
                spare.Summary.Codigo,
                "BOD-08",
                -5,
                "Ajuste negativo sin stock"), Admin, CancellationToken.None));
    }

    private static CreateSparePartRequest SparePartRequest(string description, string sapCode, bool critical = false, decimal minimum = 0)
    {
        return new CreateSparePartRequest(
            description,
            "UN",
            sapCode,
            CodigoProveedor: "PROV-1",
            DescripcionTecnica: $"{description} tecnico",
            FamiliaEquipo: "Chancadores",
            MarcaFabricante: "OEM",
            ModeloReferencia: "REF-1",
            Critico: critical,
            StockMinimo: minimum,
            StockMaximo: 20,
            PuntoReposicion: minimum,
            LeadTimeEsperadoDias: 15,
            CostoUnitarioPromedio: 100);
    }

    private static CreateWarehouseRequest WarehouseRequest(string code, WarehouseType type = WarehouseType.Faena)
    {
        return new CreateWarehouseRequest(
            code,
            $"Bodega {code}",
            "F001",
            type,
            "Patio norte",
            ["Rack A"]);
    }

    private static async Task<InventoryFixture> CreateFixtureAsync()
    {
        var excelPath = Path.Combine(Path.GetTempPath(), "maintenance-cmms-inventory-tests", Guid.NewGuid().ToString("N"), "excel");
        var provider = new ExcelDataProvider(
            new ExcelSchemaRegistry(),
            Options.Create(new DataProviderSettings
            {
                Provider = "Excel",
                ExcelPath = excelPath
            }));

        await provider.InitializeAsync(CancellationToken.None);
        await provider.SaveRowsAsync("faenas", [
            new DataRow(new Dictionary<string, string?>
            {
                ["Codigo"] = "F001",
                ["Nombre"] = "Faena Norte",
                ["Empresa"] = "Empresa"
            })
        ], CancellationToken.None);

        var auditService = new ExcelAuditService(provider, new AuditContextAccessor());
        var service = new InventoryService(provider, auditService, new AuthorizationPolicyService());
        return new InventoryFixture(provider, service);
    }

    private sealed record InventoryFixture(
        ExcelDataProvider Provider,
        IInventoryService Service);
}
