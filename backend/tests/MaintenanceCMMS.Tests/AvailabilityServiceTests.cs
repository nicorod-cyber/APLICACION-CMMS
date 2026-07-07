using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Availability;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Availability;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AvailabilityServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin",
        [AuthRoles.Admin],
        [AuthPermissions.Administration],
        []);

    [Fact]
    public async Task GetDashboardAsync_CalculatesQuantityAvailability()
    {
        var fixture = CreateFixture();

        var dashboard = await fixture.Service.GetDashboardAsync(Query(), Admin, CancellationToken.None);

        Assert.Equal(1, dashboard.Kpi.EquiposComprometidos);
        Assert.Equal(1, dashboard.Kpi.EquiposCubiertos);
        Assert.Equal(1, dashboard.Kpi.DisponibilidadCantidad);
        Assert.Equal(1, dashboard.Kpi.DisponibilidadHoras);
    }

    [Fact]
    public async Task GetDashboardAsync_CalculatesHourAvailabilityFromPenalizingEvent()
    {
        var fixture = CreateFixture(events:
        [
            Row(
                ("EventId", "EV-1"),
                ("ContractCode", "C-1"),
                ("ActivoCodigo", "ACT-1"),
                ("FaenaCodigo", "F-1"),
                ("Causa", AvailabilityCause.MantenimientoCorrectivo.ToString()),
                ("InicioUtc", Day(0).ToString("O")),
                ("FinUtc", Day(0).AddHours(12).ToString("O")),
                ("PuedeUtilizarse", "false"),
                ("AtribuibleMantenimiento", "true"),
                ("PenalizaDisponibilidad", "true"),
                ("CreatedAtUtc", Day(0).ToString("O")),
                ("UsuarioId", "admin"))
        ]);

        var dashboard = await fixture.Service.GetDashboardAsync(Query(), Admin, CancellationToken.None);

        Assert.Equal(24, dashboard.Kpi.HorasComprometidas);
        Assert.Equal(12, dashboard.Kpi.HorasDisponibles);
        Assert.Equal(0.5m, dashboard.Kpi.DisponibilidadHoras);
    }

    [Fact]
    public async Task GetDashboardAsync_BlocksAvailabilityWithExpiredDocument()
    {
        var fixture = CreateFixture(documents:
        [
            Row(
                ("EntidadTipo", "Activo"),
                ("EntidadCodigo", "ACT-1"),
                ("TipoDocumento", "PermisoOperacion"),
                ("Estado", "Vigente"),
                ("FechaVencimiento", Day(-1).ToString("O")),
                ("Critico", "true"),
                ("BloqueaDisponibilidad", "true"))
        ]);

        var dashboard = await fixture.Service.GetDashboardAsync(Query(), Admin, CancellationToken.None);

        Assert.Equal(0, dashboard.Kpi.DisponibilidadHoras);
        Assert.Contains(dashboard.UnavailableAssets, item => item.Causa == AvailabilityCause.DocumentacionVencida);
    }

    [Fact]
    public async Task GetDashboardAsync_UsesContractualBackupToCoverUnavailableCommittedAsset()
    {
        var fixture = CreateFixture(
            assets:
            [
                Asset("ACT-1", "Excavadora 1"),
                Asset("ACT-2", "Excavadora backup")
            ],
            assignments:
            [
                Assignment("ASG-1", "ACT-1", ContractAssetRole.Comprometido),
                Assignment("ASG-2", "ACT-2", ContractAssetRole.Backup)
            ],
            events:
            [
                Row(
                    ("EventId", "EV-1"),
                    ("ContractCode", "C-1"),
                    ("ActivoCodigo", "ACT-1"),
                    ("FaenaCodigo", "F-1"),
                    ("Causa", AvailabilityCause.MantenimientoCorrectivo.ToString()),
                    ("InicioUtc", Day(0).ToString("O")),
                    ("FinUtc", Day(1).ToString("O")),
                    ("PuedeUtilizarse", "false"),
                    ("AtribuibleMantenimiento", "true"),
                    ("PenalizaDisponibilidad", "true"),
                    ("CreatedAtUtc", Day(0).ToString("O")),
                    ("UsuarioId", "admin"))
            ]);

        var dashboard = await fixture.Service.GetDashboardAsync(Query(), Admin, CancellationToken.None);

        Assert.Equal(1, dashboard.Kpi.EquiposComprometidos);
        Assert.Equal(1, dashboard.Kpi.EquiposCubiertos);
        Assert.Equal(1, dashboard.Kpi.DisponibilidadCantidad);
        Assert.Equal(1, dashboard.Kpi.DisponibilidadHoras);
        Assert.Contains(dashboard.UnavailableAssets, item => item.ActivoCodigo == "ACT-1" && item.CubiertoPorBackup);
    }

    private static AvailabilityQuery Query() => new(Day(0), Day(1), Period: AvailabilityPeriod.Dia);

    private static DateTimeOffset Day(int offset) =>
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddDays(offset);

    private static Fixture CreateFixture(
        IReadOnlyCollection<DataRow>? contracts = null,
        IReadOnlyCollection<DataRow>? assignments = null,
        IReadOnlyCollection<DataRow>? events = null,
        IReadOnlyCollection<DataRow>? assets = null,
        IReadOnlyCollection<DataRow>? documents = null,
        IReadOnlyCollection<DataRow>? workOrders = null)
    {
        var provider = new InMemoryDataProvider(
            contracts ?? [Contract()],
            assignments ?? [Assignment("ASG-1", "ACT-1", ContractAssetRole.Comprometido)],
            events ?? [],
            assets ?? [Asset("ACT-1", "Excavadora 1")],
            documents ?? [],
            workOrders ?? []);
        return new Fixture(provider, new AvailabilityService(provider, new NullAuditService()));
    }

    private static DataRow Contract() => Row(
        ("ContractCode", "C-1"),
        ("Nombre", "Contrato prueba"),
        ("Cliente", "Cliente A"),
        ("FaenaCodigo", "F-1"),
        ("HorasComprometidasDia", "24"),
        ("DisponibilidadObjetivo", "0.9"),
        ("Activo", "true"));

    private static DataRow Assignment(string id, string assetCode, ContractAssetRole role) => Row(
        ("AssignmentId", id),
        ("ContractCode", "C-1"),
        ("ActivoCodigo", assetCode),
        ("Rol", role.ToString()),
        ("Activo", "true"));

    private static DataRow Asset(string code, string name) => Row(
        ("Codigo", code),
        ("Nombre", name),
        ("FaenaCodigo", "F-1"),
        ("Estado", AssetStatus.Active.ToString()),
        ("EstadoOperacional", "Operativo"));

    private static DataRow Row(params (string Key, string? Value)[] values)
    {
        return new DataRow(values.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase));
    }

    private sealed record Fixture(InMemoryDataProvider Provider, AvailabilityService Service);

    private sealed class InMemoryDataProvider : IDataProvider
    {
        private readonly Dictionary<string, IReadOnlyList<DataRow>> _rows;

        public InMemoryDataProvider(
            IReadOnlyCollection<DataRow> contracts,
            IReadOnlyCollection<DataRow> assignments,
            IReadOnlyCollection<DataRow> events,
            IReadOnlyCollection<DataRow> assets,
            IReadOnlyCollection<DataRow> documents,
            IReadOnlyCollection<DataRow> workOrders)
        {
            _rows = new Dictionary<string, IReadOnlyList<DataRow>>(StringComparer.OrdinalIgnoreCase)
            {
                ["disponibilidad_contratos"] = contracts.ToArray(),
                ["disponibilidad_activos_contrato"] = assignments.ToArray(),
                ["disponibilidad_eventos"] = events.ToArray(),
                ["activos"] = assets.ToArray(),
                ["faenas"] = [Row(("Codigo", "F-1"), ("Nombre", "Faena 1"), ("Activa", "true"))],
                ["documentos"] = documents.ToArray(),
                ["ordenes_trabajo"] = workOrders.ToArray()
            };
        }

        public string Name => "memory";

        public DataProviderType ProviderType => DataProviderType.Excel;

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<DataProviderHealth> CheckHealthAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DataProviderHealth("memory", true, "memory", [], []));

        public Task<IReadOnlyList<DataRow>> ReadRowsAsync(string schemaName, CancellationToken cancellationToken) =>
            Task.FromResult(_rows.TryGetValue(schemaName, out var rows) ? rows : []);

        public Task SaveRowsAsync(string schemaName, IReadOnlyCollection<DataRow> rows, CancellationToken cancellationToken)
        {
            _rows[schemaName] = rows.ToArray();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<T>> QueryAsync<T>(DataQuery query, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<T>>([]);

        public Task SaveChangesAsync(UnitOfWorkChanges changes, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NullAuditService : IAuditService
    {
        public Task<string> RecordAsync(AuditEventRequest auditEvent, CancellationToken cancellationToken) =>
            Task.FromResult(Guid.NewGuid().ToString("N"));

        public Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new AuditQueryResult(0, []));
    }
}
