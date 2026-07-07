using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Alerts;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.PreventiveMaintenance;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.PreventiveMaintenance;
using MaintenanceCMMS.Infrastructure.WorkOrders;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class PreventiveMaintenanceServiceTests
{
    private static readonly UserAccessContext Planner = new(
        "planner",
        [AuthRoles.Planner],
        [AuthPermissions.FinalValidateWorkOrders, AuthPermissions.ManageAlerts],
        []);

    [Fact]
    public async Task EvaluateAsync_DetectsHourFrequencyOverdue()
    {
        var fixture = CreateFixture();
        await fixture.Service.UpsertPlanAsync(Plan(FrecuenciaHoras: 100), Planner, CancellationToken.None);
        await fixture.Service.RegisterReadingAsync(new RegisterPreventiveReadingRequest("ACT-1", 120, null, Day(1)), Planner, CancellationToken.None);

        var dashboard = await fixture.Service.EvaluateAsync(new PreventiveEvaluationQuery(ActivoCodigo: "ACT-1", EvaluationDate: Day(1)), Planner, CancellationToken.None);

        Assert.Contains(dashboard.DueItems, item => item.PlanCodigo == "PM-1" && item.Estado == PreventiveStatus.Vencido);
    }

    [Fact]
    public async Task EvaluateAsync_DetectsKilometerFrequencyWindow()
    {
        var fixture = CreateFixture();
        await fixture.Service.UpsertPlanAsync(Plan(FrecuenciaKm: 1000, ToleranciaKm: 100), Planner, CancellationToken.None);
        await fixture.Service.RegisterReadingAsync(new RegisterPreventiveReadingRequest("ACT-1", null, 950, Day(1)), Planner, CancellationToken.None);

        var dashboard = await fixture.Service.EvaluateAsync(new PreventiveEvaluationQuery(ActivoCodigo: "ACT-1", EvaluationDate: Day(1)), Planner, CancellationToken.None);

        Assert.Contains(dashboard.DueItems, item => item.PlanCodigo == "PM-1" && item.Estado == PreventiveStatus.EnVentana);
    }

    [Fact]
    public async Task EvaluateAsync_DetectsCalendarFrequencyOverdue()
    {
        var fixture = CreateFixture();
        await fixture.Service.UpsertPlanAsync(Plan(FrecuenciaDias: 30, FechaInicio: Day(0)), Planner, CancellationToken.None);

        var dashboard = await fixture.Service.EvaluateAsync(new PreventiveEvaluationQuery(ActivoCodigo: "ACT-1", EvaluationDate: Day(31)), Planner, CancellationToken.None);

        Assert.Contains(dashboard.DueItems, item => item.PlanCodigo == "PM-1" && item.Estado == PreventiveStatus.Vencido);
    }

    [Fact]
    public async Task EvaluateAsync_UsesToleranceBeforeDueDate()
    {
        var fixture = CreateFixture();
        await fixture.Service.UpsertPlanAsync(Plan(FrecuenciaDias: 30, ToleranciaDias: 3, FechaInicio: Day(0)), Planner, CancellationToken.None);

        var dashboard = await fixture.Service.EvaluateAsync(new PreventiveEvaluationQuery(ActivoCodigo: "ACT-1", EvaluationDate: Day(28)), Planner, CancellationToken.None);

        Assert.Contains(dashboard.DueItems, item => item.PlanCodigo == "PM-1" && item.Estado == PreventiveStatus.EnVentana);
    }

    private static UpsertPreventivePlanRequest Plan(
        decimal? FrecuenciaHoras = null,
        decimal? FrecuenciaKm = null,
        int? FrecuenciaDias = null,
        decimal ToleranciaHoras = 0,
        decimal ToleranciaKm = 0,
        int ToleranciaDias = 0,
        DateTimeOffset? FechaInicio = null)
    {
        return new UpsertPreventivePlanRequest(
            "PM-1",
            "Mantencion preventiva",
            ActivoCodigo: "ACT-1",
            FrecuenciaHoras: FrecuenciaHoras,
            FrecuenciaKm: FrecuenciaKm,
            FrecuenciaDias: FrecuenciaDias,
            ToleranciaHoras: ToleranciaHoras,
            ToleranciaKm: ToleranciaKm,
            ToleranciaDias: ToleranciaDias,
            HHEstimadas: 2,
            FechaInicio: FechaInicio,
            Reason: "Test");
    }

    private static DateTimeOffset Day(int offset) => new DateTimeOffset(2026, 1, 1, 8, 0, 0, TimeSpan.Zero).AddDays(offset);

    private static Fixture CreateFixture()
    {
        var provider = new InMemoryDataProvider();
        var audit = new NullAuditService();
        var workOrders = new WorkOrderService(provider, audit);
        var service = new PreventiveMaintenanceService(provider, workOrders, new NullAlertService(), audit);
        return new Fixture(provider, service);
    }

    private sealed record Fixture(InMemoryDataProvider Provider, PreventiveMaintenanceService Service);

    private sealed class InMemoryDataProvider : IDataProvider
    {
        private readonly Dictionary<string, IReadOnlyList<DataRow>> _rows = new(StringComparer.OrdinalIgnoreCase)
        {
            ["planes_preventivos"] = [],
            ["preventivo_lecturas"] = [],
            ["preventivo_evaluaciones"] = [],
            ["preventivo_historial"] = [],
            ["ordenes_trabajo"] = [],
            ["tareas_ot"] = [],
            ["ot_tecnicos_tarea"] = [],
            ["ot_hh"] = [],
            ["ot_evidencias"] = [],
            ["ot_repuestos"] = [],
            ["ot_checklists"] = [],
            ["ot_firmas"] = [],
            ["ot_estado_historial"] = [],
            ["checklists"] = [],
            ["activos"] =
            [
                new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Codigo"] = "ACT-1",
                    ["Nombre"] = "Camion 01",
                    ["FaenaCodigo"] = "FAE-1",
                    ["TipoActivo"] = "Camion",
                    ["Estado"] = "Active",
                    ["Familia"] = "Camion",
                    ["Marca"] = "Marca",
                    ["Modelo"] = "Modelo"
                })
            ]
        };

        public string Name => "memory";
        public DataProviderType ProviderType => DataProviderType.Excel;
        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<DataProviderHealth> CheckHealthAsync(CancellationToken cancellationToken) => Task.FromResult(new DataProviderHealth("memory", true, "memory", [], []));
        public Task<IReadOnlyList<DataRow>> ReadRowsAsync(string schemaName, CancellationToken cancellationToken) => Task.FromResult(_rows.TryGetValue(schemaName, out var rows) ? rows : []);
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

    private sealed class NullAlertService : IAlertService
    {
        public Task<IReadOnlyCollection<AlertResponse>> ListAsync(AlertQuery query, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AlertResponse>>([]);
        public Task<AlertResponse> GenerateAsync(GenerateAlertRequest request, UserAccessContext user, CancellationToken cancellationToken)
        {
            return Task.FromResult(new AlertResponse(Guid.NewGuid().ToString("N"), request.RuleCode, request.Title, request.Message, AlertSeverityLevel.Info, AlertStatus.Open, request.Source, request.CauseKey, request.FaenaCodigo, request.EntityType, request.EntityId, false, 1, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null, null, null));
        }

        public Task<AlertResponse?> AcknowledgeAsync(string id, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<AlertResponse?>(null);
        public Task<AlertResponse?> ResolveAsync(string id, ResolveAlertRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<AlertResponse?>(null);
        public Task<NotificationResponse?> SendTestAsync(string id, SendTestNotificationRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<NotificationResponse?>(null);
        public Task<IReadOnlyCollection<NotificationResponse>> ListNotificationsAsync(UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<NotificationResponse>>([]);
        public Task<IReadOnlyCollection<AlertRuleResponse>> ListRulesAsync(UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<AlertRuleResponse>>([]);
        public Task<AlertRuleResponse?> UpdateRuleAsync(string code, UpdateAlertRuleRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<AlertRuleResponse?>(null);
    }
}
