using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
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
        await using var fixture = await CreateFixtureAsync();

        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);

        Assert.StartsWith("AV-", created.AvisoId);
        Assert.Equal(WorkNotificationStatus.Creado, created.Estado);
        Assert.Equal("FAE-1", created.FaenaCodigo);
        Assert.Equal("ACT-1", created.ActivoCodigo);
    }

    [Fact]
    public async Task ApproveAsync_ApprovesNotification()
    {
        await using var fixture = await CreateFixtureAsync();
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
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);

        var rejected = await fixture.Service.RejectAsync(created.AvisoId, new WorkNotificationActionRequest("Duplicado"), Admin, CancellationToken.None);

        Assert.NotNull(rejected);
        Assert.Equal(WorkNotificationStatus.Rechazado, rejected.Estado);
        Assert.Equal("Duplicado", rejected.MotivoRechazo);
    }

    [Fact]
    public async Task ConvertToWorkOrderAsync_CreatesWorkOrderAndClosesNotification()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);
        await fixture.Service.ApproveAsync(created.AvisoId, new WorkNotificationActionRequest("Aprobado"), Admin, CancellationToken.None);

        var conversion = await fixture.Service.ConvertToWorkOrderAsync(
            created.AvisoId,
            new ConvertWorkNotificationToWorkOrderRequest("Crear OT", FechaProgramada: Day(2)),
            Admin,
            CancellationToken.None);

        Assert.NotNull(conversion);
        Assert.StartsWith("OT-", conversion!.NumeroOT);
        Assert.Equal(WorkNotificationStatus.ConvertidoOT, conversion!.Aviso.Estado);
        Assert.Equal(conversion!.NumeroOT, conversion.Aviso.NumeroOT);

        var workOrder = Assert.Single(fixture.DbContext.WorkOrders);
        Assert.Equal("ACT-1", workOrder.Asset!.Code);
        Assert.Equal(created.AvisoId, workOrder.Notification!.NotificationNumber);
    }

    [Fact]
    public async Task ConvertToWorkOrderAsync_PreservesOperationalUnitTarget()
    {
        await using var fixture = await CreateFixtureAsync();
        await SeedOperationalUnitAsync(fixture.DbContext);
        var created = await fixture.Service.CreateAsync(Request() with { ActivoCodigo = null, UnidadOperativaCodigo = "CFA-1000" }, Admin, CancellationToken.None);
        await fixture.Service.ApproveAsync(created.AvisoId, new WorkNotificationActionRequest("Aprobado"), Admin, CancellationToken.None);
        var conversion = await fixture.Service.ConvertToWorkOrderAsync(created.AvisoId, new ConvertWorkNotificationToWorkOrderRequest("Crear OT para unidad"), Admin, CancellationToken.None);

        Assert.NotNull(conversion);
        Assert.Equal("CFA-1000", conversion.Aviso.UnidadOperativaCodigo);
        var workOrder = Assert.Single(fixture.DbContext.WorkOrders.Include(item => item.OperationalUnit));
        Assert.Null(workOrder.AssetId);
        Assert.Equal("CFA-1000", workOrder.OperationalUnit!.Code);
    }
    private static async Task<Fixture> CreateFixtureAsync()
    {
        var database = await PostgreSqlWorkTestFixture.CreateAsync();
        var service = new WorkNotificationService(database.DbContext, new NullAuditService());
        return new Fixture(database, database.DbContext, service);
    }
    private static async Task SeedOperationalUnitAsync(CmmsDbContext db)
    {
        var faena = await db.Faenas.SingleAsync(item => item.Code == "FAE-1");
        var state = await db.AssetOperationalStates.SingleAsync(item => item.Code == "OPERATIVO_FAENA");
        var type = new OperationalUnitTypeEntity { Code = "CFA", Name = "Conjunto funcional", IsActive = true };
        db.OperationalUnits.Add(new OperationalUnitEntity { Code = "CFA-1000", Name = "Conjunto CFA 1000", OperationalUnitType = type, Faena = faena, OperationalState = state });
        await db.SaveChangesAsync();
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

    private sealed record Fixture(
        PostgreSqlWorkTestFixture Database,
        CmmsDbContext DbContext,
        WorkNotificationService Service) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }
    private sealed class NullAuditService : IAuditService
    {
        public Task<string> RecordAsync(AuditEventRequest auditEvent, CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid().ToString("N"));

        public Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken) => Task.FromResult(new AuditQueryResult(0, []));
    }
}

