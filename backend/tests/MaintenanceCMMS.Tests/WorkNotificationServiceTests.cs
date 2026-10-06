using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.SqlServer;
using MaintenanceCMMS.Infrastructure.Data.SqlServer.Entities;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.WorkNotifications;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Text.Json;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class WorkNotificationServiceTests
{
    private static readonly UserAccessContext Admin = new("admin", [AuthRoles.Admin], [AuthPermissions.Administration], []);
    private static readonly UserAccessContext OtherSite = new("site-user", [AuthRoles.Technician], [], ["OTHER"]);

    [Fact] public async Task CreateAsync_RequiresExistingEquipment()
    {
        await using var fixture = await CreateFixtureAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(Request() with { ActivoCodigo = null }, Admin, CancellationToken.None));
    }

    [Fact] public async Task CreateAsync_PreventsCrossSiteAssetSelection()
    {
        await using var fixture = await CreateFixtureAsync();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.CreateAsync(Request(), OtherSite, CancellationToken.None));
    }

    [Fact] public async Task CreateAsync_ValidatesOperationalStateRequirements()
    {
        await using var fixture = await CreateFixtureAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(Request() with { EstadoOperacional = NotificationOperationalState.FueraDeServicio }, Admin, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CreateAsync(Request() with { EstadoOperacional = NotificationOperationalState.OperativoConAlerta }, Admin, CancellationToken.None));
    }

    [Fact] public async Task CreateAndSubmitAsync_SupportsMultipleWorkItemsAndAuditsTransition()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateAsync(Request() with { Trabajos = [new("Fuga hidráulica"), new("Inspeccionar sello", "Riesgo de fuga")] }, Admin, CancellationToken.None);
        Assert.Equal(WorkNotificationStatus.Borrador, created.Estado); Assert.Equal(2, created.Trabajos.Count);
        var submitted = await fixture.Service.SubmitAsync(created.AvisoId, Admin, CancellationToken.None);
        Assert.Equal(WorkNotificationStatus.PendientePlanificacion, submitted!.Estado); Assert.Contains(submitted.Historial, x => x.EstadoDestino == WorkNotificationStatus.PendientePlanificacion && x.UsuarioId == "admin");
    }

    [Fact] public async Task PlanningCanReturnThenSiteCanResubmit()
    {
        await using var fixture = await CreateFixtureAsync(); var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None); await fixture.Service.SubmitAsync(created.AvisoId, Admin, CancellationToken.None);
        var returned = await fixture.Service.ReturnToSiteAsync(created.AvisoId, new("Falta fotografía"), Admin, CancellationToken.None); Assert.Equal(WorkNotificationStatus.DevueltoFaena, returned!.Estado);
        var resent = await fixture.Service.SubmitAsync(created.AvisoId, Admin, CancellationToken.None); Assert.Equal(WorkNotificationStatus.PendientePlanificacion, resent!.Estado);
    }

    [Fact]
    public async Task CreateAsync_WritesCycleFreeAuditSnapshot()
    {
        await using var fixture = await CreateFixtureAsync();
        var service = new WorkNotificationService(fixture.Db, new SqlServerAuditService(fixture.Db, new AuditContextAccessor()));

        var created = await service.CreateAsync(Request() with { Trabajos = [new("Uno"), new("Dos")] }, Admin, CancellationToken.None);
        var audit = await fixture.Db.AuditLogs.SingleAsync(entry => entry.EntityId == created.AvisoId && entry.Action == "work_notification.created");

        using var snapshot = JsonDocument.Parse(audit.NewValue!);
        Assert.Equal(created.AvisoId, snapshot.RootElement.GetProperty("NotificationNumber").GetString());
        Assert.Equal(2, snapshot.RootElement.GetProperty("Items").GetArrayLength());
        Assert.DoesNotContain("\"Notification\":", audit.NewValue!);
    }

    [Fact]
    public async Task CreateAsync_RollsBackHeaderAndItemsWhenAuditFails()
    {
        await using var fixture = await CreateFixtureAsync();
        var service = new WorkNotificationService(fixture.Db, new ThrowingAuditService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(Request(), Admin, CancellationToken.None));
        await using var verify = fixture.Database.NewContext();
        Assert.Empty(await verify.WorkNotifications.ToListAsync());
        Assert.Empty(await verify.WorkNotificationItems.ToListAsync());
        Assert.Empty(await verify.AssetReadings.Where(reading => reading.Source == "AVISO").ToListAsync());
    }

    [Fact]
    public async Task CreateAsync_AdvancesPastHistoricalNotificationNumber()
    {
        await using var fixture = await CreateFixtureAsync();
        var historical = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);
        var entity = await fixture.Db.WorkNotifications.SingleAsync(item => item.NotificationNumber == historical.AvisoId);
        entity.NotificationNumber = "AV-000052";
        await fixture.Db.SaveChangesAsync();

        var next = await fixture.Service.CreateAsync(Request() with { Trabajos = [new("Nueva falla")] }, Admin, CancellationToken.None);
        Assert.Equal("AV-000053", next.AvisoId);
    }

    [Fact]
    public async Task AnnulAsync_RollsBackStateAndHistoryWhenAuditFails()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);
        var service = new WorkNotificationService(fixture.Db, new ThrowingAuditService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnnulAsync(created.AvisoId, new("Motivo de anulación"), Admin, CancellationToken.None));
        await using var verify = fixture.Database.NewContext();
        var persisted = await new WorkNotificationService(verify, new NullAuditService()).GetByIdAsync(created.AvisoId, Admin, CancellationToken.None);
        Assert.Equal(WorkNotificationStatus.Borrador, persisted!.Estado);
        Assert.DoesNotContain(persisted.Historial, history => history.EstadoDestino == WorkNotificationStatus.Anulado);
    }

    [Fact]
    public async Task AnnulAsync_InsertsExactlyOneAddedHistoryWithoutHistoryUpdate()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);
        var stateProbe = new HistoryStateProbe();
        var commandProbe = new HistoryCommandProbe();
        var options = new DbContextOptionsBuilder<CmmsDbContext>()
            .UseSqlServer(SqlServerWorkTestFixture.ConnectionString(fixture.Database.AdminConnectionString, fixture.Database.DatabaseName))
            .AddInterceptors(stateProbe, commandProbe)
            .Options;
        await using var transitionDb = new CmmsDbContext(options);
        var service = new WorkNotificationService(transitionDb, new SqlServerAuditService(transitionDb, new AuditContextAccessor()));

        var annulled = await service.AnnulAsync(created.AvisoId, new("Motivo de anulación"), Admin, CancellationToken.None);

        Assert.Equal(WorkNotificationStatus.Anulado, annulled!.Estado);
        Assert.True(stateProbe.SawAddedHistory);
        Assert.False(stateProbe.SawModifiedHistory);
        Assert.DoesNotContain(commandProbe.Commands, sql => sql.Contains("UPDATE [avisos_trabajo_historial_sql]", StringComparison.OrdinalIgnoreCase));
        var notificationId = await transitionDb.WorkNotifications.Where(notice => notice.NotificationNumber == created.AvisoId).Select(notice => notice.Id).SingleAsync();
        Assert.Equal(2, await transitionDb.WorkNotificationStatusHistory.CountAsync(history => history.NotificationId == notificationId));
        Assert.Single(await transitionDb.AuditLogs.Where(entry => entry.EntityId == created.AvisoId && entry.Action == "work_notification.annulled").ToListAsync());
    }

    [Fact]
    public async Task SubmitAsync_WorksForLegacyStyleAvisoAndInsertsOneHistory()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);
        var legacy = await fixture.Db.WorkNotifications.SingleAsync(notice => notice.NotificationNumber == created.AvisoId);
        legacy.NotificationNumber = "AV-000051";
        legacy.OperationalStatus = null;
        await fixture.Db.SaveChangesAsync();
        var before = await fixture.Db.WorkNotificationStatusHistory.CountAsync(history => history.NotificationId == legacy.Id);

        var submitted = await fixture.Service.SubmitAsync("AV-000051", Admin, CancellationToken.None);

        Assert.Equal(WorkNotificationStatus.PendientePlanificacion, submitted!.Estado);
        Assert.Equal(before + 1, await fixture.Db.WorkNotificationStatusHistory.CountAsync(history => history.NotificationId == legacy.Id));
    }

    [Fact]
    public async Task SubmitAsync_MapsRealNotificationRowVersionConflictAndDoesNotPartiallyTransition()
    {
        await using var fixture = await CreateFixtureAsync();
        var created = await fixture.Service.CreateAsync(Request(), Admin, CancellationToken.None);
        var concurrentUpdate = new ConcurrentNotificationUpdateInterceptor();
        var options = new DbContextOptionsBuilder<CmmsDbContext>()
            .UseSqlServer(SqlServerWorkTestFixture.ConnectionString(fixture.Database.AdminConnectionString, fixture.Database.DatabaseName))
            .AddInterceptors(concurrentUpdate)
            .Options;
        await using var transitionDb = new CmmsDbContext(options);
        var service = new WorkNotificationService(transitionDb, new NullAuditService());

        await Assert.ThrowsAsync<WorkNotificationConcurrencyException>(() => service.SubmitAsync(created.AvisoId, Admin, CancellationToken.None));

        await using var verify = fixture.Database.NewContext();
        var persisted = await new WorkNotificationService(verify, new NullAuditService()).GetByIdAsync(created.AvisoId, Admin, CancellationToken.None);
        Assert.Equal(WorkNotificationStatus.Borrador, persisted!.Estado);
        Assert.DoesNotContain(persisted.Historial, history => history.EstadoDestino == WorkNotificationStatus.PendientePlanificacion);
    }

    private static async Task<Fixture> CreateFixtureAsync() { var database = await SqlServerWorkTestFixture.CreateAsync(); return new Fixture(database, database.DbContext, new WorkNotificationService(database.DbContext, new NullAuditService())); }
    private static CreateWorkNotificationRequest Request() => new("FAE-1", ActivoCodigo: "ACT-1", FechaDeteccion: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Trabajos: [new("Descripción de falla / trabajo requerido")]);
    private sealed record Fixture(SqlServerWorkTestFixture Database, CmmsDbContext Db, WorkNotificationService Service) : IAsyncDisposable { public ValueTask DisposeAsync() => Database.DisposeAsync(); }
    private sealed class NullAuditService : IAuditService { public Task<string> RecordAsync(AuditEventRequest request, CancellationToken ct) => Task.FromResult("audit"); public Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken ct) => Task.FromResult(new AuditQueryResult(0, [])); }
    private sealed class ThrowingAuditService : IAuditService { public Task<string> RecordAsync(AuditEventRequest request, CancellationToken ct) => throw new InvalidOperationException("Fallo de auditoría para prueba de rollback."); public Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken ct) => Task.FromResult(new AuditQueryResult(0, [])); }

    private sealed class HistoryStateProbe : SaveChangesInterceptor
    {
        public bool SawAddedHistory { get; private set; }
        public bool SawModifiedHistory { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            foreach (var entry in eventData.Context!.ChangeTracker.Entries<WorkNotificationStatusHistoryEntity>())
            {
                SawAddedHistory |= entry.State == EntityState.Added;
                SawModifiedHistory |= entry.State == EntityState.Modified;
            }
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class HistoryCommandProbe : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class ConcurrentNotificationUpdateInterceptor : SaveChangesInterceptor
    {
        private int triggered;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var notification = eventData.Context!.ChangeTracker.Entries<WorkNotificationEntity>().SingleOrDefault(entry => entry.State == EntityState.Modified);
            if (notification is not null && Interlocked.Exchange(ref triggered, 1) == 0)
            {
                await using var connection = new SqlConnection(eventData.Context.Database.GetConnectionString());
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE dbo.avisos_trabajo_sql SET updated_at_utc = SYSUTCDATETIME() WHERE id = @id;";
                command.Parameters.Add(new SqlParameter("@id", notification.Entity.Id));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
