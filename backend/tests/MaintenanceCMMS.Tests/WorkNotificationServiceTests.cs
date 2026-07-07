using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.WorkNotifications;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class WorkNotificationServiceTests
{
    private static readonly UserAccessContext Admin = new(
        "admin",
        [AuthRoles.Admin],
        [AuthPermissions.Administration],
        []);

    [Fact]
    public async Task CreateAsync_CreatesWorkNotification()
    {
        var fixture = CreateFixture();

        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);

        Assert.StartsWith("AV-", created.AvisoId);
        Assert.Equal(WorkNotificationStatus.Creado, created.Estado);
        Assert.Equal("FAE-1", created.FaenaCodigo);
        Assert.Equal("ACT-1", created.ActivoCodigo);
    }

    [Fact]
    public async Task ApproveAsync_ApprovesNotification()
    {
        var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);
        await fixture.Service.EvaluateAsync(created.AvisoId, new WorkNotificationActionRequest("Evaluar condicion"), Admin, CancellationToken.None);

        var approved = await fixture.Service.ApproveAsync(created.AvisoId, new WorkNotificationActionRequest("Generar OT correctiva"), Admin, CancellationToken.None);

        Assert.NotNull(approved);
        Assert.Equal(WorkNotificationStatus.Aprobado, approved.Estado);
        Assert.Equal("admin", approved.AprobadoPor);
    }

    [Fact]
    public async Task RejectAsync_RejectsNotificationWithReason()
    {
        var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);

        var rejected = await fixture.Service.RejectAsync(created.AvisoId, new WorkNotificationActionRequest("Duplicado"), Admin, CancellationToken.None);

        Assert.NotNull(rejected);
        Assert.Equal(WorkNotificationStatus.Rechazado, rejected.Estado);
        Assert.Equal("Duplicado", rejected.MotivoRechazo);
    }

    [Fact]
    public async Task ConvertToWorkOrderAsync_CreatesWorkOrderAndClosesNotification()
    {
        var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);
        await fixture.Service.ApproveAsync(created.AvisoId, new WorkNotificationActionRequest("Aprobado"), Admin, CancellationToken.None);

        var conversion = await fixture.Service.ConvertToWorkOrderAsync(
            created.AvisoId,
            new ConvertWorkNotificationToWorkOrderRequest("Crear OT", FechaProgramada: Day(2)),
            Admin,
            CancellationToken.None);

        Assert.NotNull(conversion);
        Assert.StartsWith("OT-", conversion.NumeroOT);
        Assert.Equal(WorkNotificationStatus.ConvertidoOT, conversion.Aviso.Estado);
        Assert.Equal(conversion.NumeroOT, conversion.Aviso.NumeroOT);

        var workOrders = await fixture.Provider.ReadRowsAsync("ordenes_trabajo", CancellationToken.None);
        var workOrder = Assert.Single(workOrders);
        Assert.Equal("ACT-1", workOrder.GetValue("ActivoCodigo"));
        Assert.Equal(created.AvisoId, workOrder.GetValue("AvisoId"));
    }

    private static Fixture CreateFixture()
    {
        var provider = new InMemoryDataProvider();
        var service = new WorkNotificationService(provider, new NullAuditService());
        return new Fixture(provider, service);
    }

    private static CreateWorkNotificationRequest Request()
    {
        return new CreateWorkNotificationRequest(
            WorkNotificationType.Falla,
            "Fuga hidraulica en cilindro principal",
            WorkNotificationPriority.Alta,
            WorkNotificationCriticality.Critica,
            WorkFailureClassification.ConRestriccion,
            FaenaCodigo: "FAE-1",
            ActivoCodigo: "ACT-1",
            Sistema: "Hidraulico",
            Subsistema: "Levante",
            Componente: "Cilindro",
            EvidenciaInicial: "https://sharepoint.local/evidencia/aviso-1.jpg",
            FechaDeteccion: Day(0));
    }

    private static DateTimeOffset Day(int offset) => new(2026, 1, 1 + offset, 0, 0, 0, TimeSpan.Zero);

    private sealed record Fixture(InMemoryDataProvider Provider, WorkNotificationService Service);

    private sealed class InMemoryDataProvider : IDataProvider
    {
        private readonly Dictionary<string, IReadOnlyList<DataRow>> _rows = new(StringComparer.OrdinalIgnoreCase)
        {
            ["avisos_trabajo"] = [],
            ["ordenes_trabajo"] = [],
            ["activos"] =
            [
                new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Codigo"] = "ACT-1",
                    ["Nombre"] = "Excavadora 01",
                    ["FaenaCodigo"] = "FAE-1",
                    ["TipoActivo"] = "Equipo movil",
                    ["Estado"] = "Active"
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
}
