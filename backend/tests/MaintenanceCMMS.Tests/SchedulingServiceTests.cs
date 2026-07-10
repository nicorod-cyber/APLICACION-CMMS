using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Scheduling;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Scheduling;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class SchedulingServiceTests
{
    private static readonly UserAccessContext Planner = new(
        "planner",
        [AuthRoles.Planner],
        [AuthPermissions.FinalValidateWorkOrders],
        []);

    [Fact]
    public async Task ScheduleWorkOrderAsync_WarnsWhenWorkshopCapacityIsExceeded()
    {
        var fixture = CreateFixture();
        await fixture.Scheduling.UpsertWorkshopAsync(Workshop(capacityHours: 4), Planner, CancellationToken.None);
        var first = await fixture.WorkOrders.CreateAsync(Order("ACT-1", "Primera OT"), Planner, CancellationToken.None);
        var second = await fixture.WorkOrders.CreateAsync(Order("ACT-2", "Segunda OT"), Planner, CancellationToken.None);

        await fixture.Scheduling.ScheduleWorkOrderAsync(first.Summary.NumeroOT, Schedule(Day(1), Day(1).AddHours(2), 3), Planner, CancellationToken.None);
        var overloaded = await fixture.Scheduling.ScheduleWorkOrderAsync(second.Summary.NumeroOT, Schedule(Day(1).AddHours(3), Day(1).AddHours(5), 3), Planner, CancellationToken.None);

        Assert.NotEmpty(overloaded.Warnings);
        Assert.Contains(overloaded.Alerts, alert => alert.Tipo == ScheduleAlertType.TallerSobrecargado);
    }

    [Fact]
    public async Task ScheduleWorkOrderAsync_ReprogramsExistingItemWithReason()
    {
        var fixture = CreateFixture();
        await fixture.Scheduling.UpsertWorkshopAsync(Workshop(), Planner, CancellationToken.None);
        var order = await fixture.WorkOrders.CreateAsync(Order("ACT-1", "Reprogramar OT"), Planner, CancellationToken.None);

        var first = await fixture.Scheduling.ScheduleWorkOrderAsync(order.Summary.NumeroOT, Schedule(Day(1), Day(1).AddHours(2), 2), Planner, CancellationToken.None);
        var second = await fixture.Scheduling.ScheduleWorkOrderAsync(order.Summary.NumeroOT, Schedule(Day(3), Day(3).AddHours(2), 2, "Cambio de prioridad"), Planner, CancellationToken.None);
        var board = await fixture.Scheduling.GetBoardAsync(new ScheduleBoardQuery(From: Day(1), To: Day(4)), Planner, CancellationToken.None);

        Assert.Equal(first.Item.ProgramacionId, second.Item.ProgramacionId);
        Assert.Single(board.Items.Where(item => item.NumeroOT == order.Summary.NumeroOT));
        Assert.Equal(Day(3), board.Items.Single(item => item.NumeroOT == order.Summary.NumeroOT).FechaInicio);
    }

    [Fact]
    public async Task AddDependencyAsync_StoresGanttDependency()
    {
        var fixture = CreateFixture();
        var first = await fixture.WorkOrders.CreateAsync(Order("ACT-1", "Predecesora"), Planner, CancellationToken.None);
        var second = await fixture.WorkOrders.CreateAsync(Order("ACT-2", "Sucesora"), Planner, CancellationToken.None);

        var dependency = await fixture.Scheduling.AddDependencyAsync(new AddScheduleDependencyRequest(first.Summary.NumeroOT, second.Summary.NumeroOT, Motivo: "Secuencia tecnica"), Planner, CancellationToken.None);
        var board = await fixture.Scheduling.GetBoardAsync(new ScheduleBoardQuery(From: Day(1), To: Day(4)), Planner, CancellationToken.None);

        Assert.Equal(first.Summary.NumeroOT, dependency.PredecessorNumeroOT);
        Assert.Contains(board.Dependencies, item => item.SuccessorNumeroOT == second.Summary.NumeroOT);
    }

    private static UpsertWorkshopRequest Workshop(decimal capacityHours = 8)
    {
        return new UpsertWorkshopRequest("TALLER-1", "Taller Mina", "FAE-1", capacityHours, 1, "08:00-18:00", "Mecanica");
    }

    private static ScheduleWorkOrderPlanningRequest Schedule(DateTimeOffset start, DateTimeOffset end, decimal hours, string reason = "Programacion")
    {
        return new ScheduleWorkOrderPlanningRequest("TALLER-1", start, end, hours, reason);
    }

    private static CreateWorkOrderRequest Order(string assetCode, string description)
    {
        return new CreateWorkOrderRequest(assetCode, description, "Corrective", FaenaCodigo: "FAE-1", Prioridad: "Alta", Criticidad: "Critica");
    }

    private static DateTimeOffset Day(int offset) => new(2026, 2, offset, 0, 0, 0, TimeSpan.Zero);

    private static Fixture CreateFixture()
    {
        var provider = new InMemoryDataProvider();
        var audit = new NullAuditService();
        var workOrders = new FakeWorkOrderService(provider);
        var scheduling = new SchedulingService(provider, workOrders, audit);
        return new Fixture(workOrders, scheduling);
    }

    private sealed record Fixture(IWorkOrderService WorkOrders, SchedulingService Scheduling);


    private sealed class FakeWorkOrderService : IWorkOrderService
    {
        private readonly InMemoryDataProvider _provider;
        private int _next = 1;
        public FakeWorkOrderService(InMemoryDataProvider provider) { _provider = provider; }
        public async Task<WorkOrderDetailResponse> CreateAsync(CreateWorkOrderRequest request, UserAccessContext user, CancellationToken cancellationToken)
        {
            var number = $"OT-{_next++:000000}";
            var rows = (await _provider.ReadRowsAsync("ordenes_trabajo", cancellationToken)).ToList();
            rows.Add(new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
            {
                ["NumeroOT"] = number,
                ["ActivoCodigo"] = request.ActivoCodigo,
                ["Estado"] = WorkOrderLifecycleStatus.OTCreada.ToString(),
                ["TipoMantenimiento"] = request.TipoMantenimiento,
                ["Descripcion"] = request.Descripcion,
                ["FaenaCodigo"] = request.FaenaCodigo,
                ["Prioridad"] = request.Prioridad,
                ["Criticidad"] = request.Criticidad,
                ["CreadoPor"] = user.UserId,
                ["CreadoEnUtc"] = DateTimeOffset.UtcNow.ToString("O")
            }));
            await _provider.SaveRowsAsync("ordenes_trabajo", rows, cancellationToken);
            var summary = new WorkOrderSummaryResponse(number, WorkOrderLifecycleStatus.OTCreada, request.ActivoCodigo, request.ActivoCodigo, request.FaenaCodigo ?? "FAE-1", request.TipoMantenimiento, request.Descripcion, null, null, null, null, request.Prioridad, request.Criticidad, null, null, null, false, false, 0, 0, 0, 0);
            return new WorkOrderDetailResponse(summary, [], [], [], [], [], [], [], [], []);
        }
        public Task<IReadOnlyCollection<WorkOrderSummaryResponse>> ListAsync(WorkOrderQuery query, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<WorkOrderSummaryResponse>>([]);
        public Task<WorkOrderDetailResponse?> GetByIdAsync(string numeroOt, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderDetailResponse?>(null);
        public Task<WorkOrderDetailResponse> CreatePreventiveAsync(CreatePreventiveWorkOrderRequest request, UserAccessContext user, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<WorkOrderTaskResponse?> AddTaskAsync(string numeroOt, CreateWorkOrderTaskRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderTaskResponse?>(null);
        public Task<WorkOrderTaskTechnicianResponse?> AssignTechnicianAsync(string numeroOt, string codigoTarea, AssignTaskTechnicianRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderTaskTechnicianResponse?>(null);
        public Task<WorkOrderLaborResponse?> RegisterLaborAsync(string numeroOt, string codigoTarea, RegisterLaborRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderLaborResponse?>(null);
        public Task<WorkOrderLaborResponse?> ValidateLaborAsync(string numeroOt, string hhId, ValidateLaborRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderLaborResponse?>(null);
        public Task<WorkOrderEvidenceResponse?> RegisterEvidenceAsync(string numeroOt, RegisterEvidenceRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderEvidenceResponse?>(null);
        public Task<WorkOrderSparePartResponse?> AddSparePartAsync(string numeroOt, AddWorkOrderSparePartRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderSparePartResponse?>(null);
        public Task<WorkOrderSparePartResponse?> UpdateSparePartUsageAsync(string numeroOt, string itemId, UpdateWorkOrderSparePartUsageRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderSparePartResponse?>(null);
        public Task<WorkOrderChecklistItemResponse?> AddChecklistItemAsync(string numeroOt, AddWorkOrderChecklistItemRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderChecklistItemResponse?>(null);
        public Task<WorkOrderChecklistItemResponse?> UpdateChecklistItemAsync(string numeroOt, string itemId, UpdateChecklistItemRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderChecklistItemResponse?>(null);
        public Task<IReadOnlyCollection<WorkOrderChecklistItemResponse>> ApplyChecklistTemplateAsync(string numeroOt, ApplyChecklistTemplateRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<WorkOrderChecklistItemResponse>>([]);
        public Task<WorkOrderSignatureResponse?> RegisterSignatureAsync(string numeroOt, RegisterWorkOrderSignatureRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderSignatureResponse?>(null);
        public Task<WorkOrderDetailResponse?> ScheduleAsync(string numeroOt, ScheduleWorkOrderRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderDetailResponse?>(null);
        public Task<WorkOrderDetailResponse?> StartAsync(string numeroOt, WorkOrderActionRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderDetailResponse?>(null);
        public Task<WorkOrderDetailResponse?> PauseAsync(string numeroOt, WorkOrderActionRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderDetailResponse?>(null);
        public Task<WorkOrderDetailResponse?> FinishByTechnicianAsync(string numeroOt, WorkOrderActionRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderDetailResponse?>(null);
        public Task<WorkOrderDetailResponse?> CloseTechnicallyAsync(string numeroOt, WorkOrderActionRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderDetailResponse?>(null);
        public Task<WorkOrderDetailResponse?> ValidatePlanningAsync(string numeroOt, WorkOrderActionRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderDetailResponse?>(null);
        public Task<WorkOrderDetailResponse?> AnnulAsync(string numeroOt, WorkOrderActionRequest request, UserAccessContext user, CancellationToken cancellationToken) => Task.FromResult<WorkOrderDetailResponse?>(null);
    }
    private sealed class InMemoryDataProvider : IDataProvider
    {
        private readonly Dictionary<string, IReadOnlyList<DataRow>> _rows = new(StringComparer.OrdinalIgnoreCase)
        {
            ["programacion_talleres"] = [],
            ["programacion_ot"] = [],
            ["programacion_dependencias"] = [],
            ["programacion_alertas"] = [],
            ["ordenes_trabajo"] = [],
            ["tareas_ot"] = [],
            ["ot_tecnicos_tarea"] = [],
            ["ot_hh"] = [],
            ["ot_evidencias"] = [],
            ["ot_repuestos"] = [],
            ["ot_checklists"] = [],
            ["ot_firmas"] = [],
            ["ot_estado_historial"] = [],
            ["activos"] =
            [
                new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Codigo"] = "ACT-1",
                    ["Nombre"] = "Camion 01",
                    ["FaenaCodigo"] = "FAE-1",
                    ["TipoActivo"] = "Camion",
                    ["Estado"] = "Active"
                }),
                new DataRow(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Codigo"] = "ACT-2",
                    ["Nombre"] = "Camion 02",
                    ["FaenaCodigo"] = "FAE-1",
                    ["TipoActivo"] = "Camion",
                    ["Estado"] = "Active"
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
}

