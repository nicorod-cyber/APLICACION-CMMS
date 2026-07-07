using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.WorkOrders;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class WorkOrderServiceTests
{
    private static readonly UserAccessContext Planner = new(
        "planner",
        [AuthRoles.Planner],
        [AuthPermissions.FinalValidateWorkOrders],
        []);

    private static readonly UserAccessContext Supervisor = new(
        "supervisor",
        [AuthRoles.MaintenanceSupervisor],
        [AuthPermissions.CloseWorkOrders],
        ["FAE-1"]);

    private static readonly UserAccessContext Technician = new(
        "tech-1",
        [AuthRoles.Technician],
        [AuthPermissions.ViewAssignedWorkOrders],
        ["FAE-1"]);

    [Fact]
    public async Task CompleteFlow_ClosesAndValidatesWorkOrder()
    {
        var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(OrderRequest(requiresSignature: true), Planner, CancellationToken.None);
        var task = await fixture.Service.AddTaskAsync(created.Summary.NumeroOT, new CreateWorkOrderTaskRequest(
            "Inspeccionar fuga",
            RequiereEvidencia: true,
            RequiereHH: true,
            ChecklistObligatorio: true), Planner, CancellationToken.None);
        Assert.NotNull(task);

        await fixture.Service.AssignTechnicianAsync(created.Summary.NumeroOT, task.CodigoTarea, new AssignTaskTechnicianRequest("tech-1", "Tecnico Uno"), Planner, CancellationToken.None);
        await fixture.Service.AddChecklistItemAsync(created.Summary.NumeroOT, new AddWorkOrderChecklistItemRequest(task.CodigoTarea, "Equipo bloqueado", true), Planner, CancellationToken.None);
        var spare = await fixture.Service.AddSparePartAsync(created.Summary.NumeroOT, new AddWorkOrderSparePartRequest(task.CodigoTarea, "REP-001", 1, "UN", Estado: WorkOrderSparePartStatus.Entregado), Planner, CancellationToken.None);
        Assert.NotNull(spare);

        await fixture.Service.ScheduleAsync(created.Summary.NumeroOT, new ScheduleWorkOrderRequest(Day(2), "Programada"), Planner, CancellationToken.None);
        await fixture.Service.StartAsync(created.Summary.NumeroOT, new WorkOrderActionRequest("Inicio"), Technician, CancellationToken.None);
        await fixture.Service.RegisterLaborAsync(created.Summary.NumeroOT, task.CodigoTarea, new RegisterLaborRequest("tech-1", 2, "Cambio de sello", Day(2)), Technician, CancellationToken.None);
        await fixture.Service.RegisterEvidenceAsync(created.Summary.NumeroOT, new RegisterEvidenceRequest("Foto final", task.CodigoTarea, ArchivoKey: "evidencia/final.jpg"), Technician, CancellationToken.None);
        var checklist = (await fixture.Service.GetByIdAsync(created.Summary.NumeroOT, Planner, CancellationToken.None))!.Checklist.Single();
        await fixture.Service.UpdateChecklistItemAsync(created.Summary.NumeroOT, checklist.ItemId, new UpdateChecklistItemRequest(true, "Completo"), Technician, CancellationToken.None);
        await fixture.Service.UpdateSparePartUsageAsync(created.Summary.NumeroOT, spare.ItemId, new UpdateWorkOrderSparePartUsageRequest(WorkOrderSparePartStatus.Utilizado, "Utilizado", CantidadUtilizada: 1), Planner, CancellationToken.None);
        await fixture.Service.RegisterSignatureAsync(created.Summary.NumeroOT, new RegisterWorkOrderSignatureRequest("firma/tech-1.svg", "tech-1"), Technician, CancellationToken.None);
        await fixture.Service.FinishByTechnicianAsync(created.Summary.NumeroOT, new WorkOrderActionRequest("Finalizada por tecnico"), Technician, CancellationToken.None);

        var closed = await fixture.Service.CloseTechnicallyAsync(created.Summary.NumeroOT, new WorkOrderActionRequest("Cierre conforme"), Supervisor, CancellationToken.None);
        var validated = await fixture.Service.ValidatePlanningAsync(created.Summary.NumeroOT, new WorkOrderActionRequest("Validada"), Planner, CancellationToken.None);

        Assert.NotNull(closed);
        Assert.NotNull(validated);
        Assert.Equal(WorkOrderLifecycleStatus.CerradaTecnicamente, closed.Summary.Estado);
        Assert.Equal(WorkOrderLifecycleStatus.ValidadaPlanificacion, validated.Summary.Estado);
        Assert.Empty(validated.ClosureBlockers);
    }

    [Fact]
    public async Task CloseTechnicallyAsync_BlocksWhenRequiredEvidenceIsMissing()
    {
        var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(OrderRequest(), Planner, CancellationToken.None);
        var task = await fixture.Service.AddTaskAsync(created.Summary.NumeroOT, new CreateWorkOrderTaskRequest(
            "Tomar muestra",
            RequiereEvidencia: true,
            RequiereHH: false), Planner, CancellationToken.None);
        Assert.NotNull(task);
        await fixture.Service.AssignTechnicianAsync(created.Summary.NumeroOT, task.CodigoTarea, new AssignTaskTechnicianRequest("tech-1"), Planner, CancellationToken.None);
        await fixture.Service.StartAsync(created.Summary.NumeroOT, new WorkOrderActionRequest("Inicio"), Technician, CancellationToken.None);
        await fixture.Service.FinishByTechnicianAsync(created.Summary.NumeroOT, new WorkOrderActionRequest("Trabajo terminado"), Technician, CancellationToken.None);

        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Service.CloseTechnicallyAsync(created.Summary.NumeroOT, new WorkOrderActionRequest("Cerrar"), Supervisor, CancellationToken.None));

        Assert.Contains("requiere evidencia", exception.Message);
    }

    [Fact]
    public async Task AssignTechnicianAsync_AllowsMultipleTechniciansPerTask()
    {
        var fixture = CreateFixture();
        var created = await fixture.Service.CreateAsync(OrderRequest(), Planner, CancellationToken.None);
        var task = await fixture.Service.AddTaskAsync(created.Summary.NumeroOT, new CreateWorkOrderTaskRequest("Trabajo compartido"), Planner, CancellationToken.None);
        Assert.NotNull(task);

        await fixture.Service.AssignTechnicianAsync(created.Summary.NumeroOT, task.CodigoTarea, new AssignTaskTechnicianRequest("tech-1"), Planner, CancellationToken.None);
        await fixture.Service.AssignTechnicianAsync(created.Summary.NumeroOT, task.CodigoTarea, new AssignTaskTechnicianRequest("tech-2"), Planner, CancellationToken.None);

        var detail = await fixture.Service.GetByIdAsync(created.Summary.NumeroOT, Planner, CancellationToken.None);
        var technicianView = await fixture.Service.ListAsync(new WorkOrderQuery(TechnicianId: "tech-1"), Technician, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(2, detail.Technicians.Count);
        Assert.Single(technicianView);
    }

    private static CreateWorkOrderRequest OrderRequest(bool requiresSignature = false)
    {
        return new CreateWorkOrderRequest(
            "ACT-1",
            "Atender fuga hidraulica",
            "Corrective",
            FaenaCodigo: "FAE-1",
            Sistema: "Hidraulico",
            Subsistema: "Levante",
            Componente: "Cilindro",
            Prioridad: "Alta",
            Criticidad: "Critica",
            FechaProgramada: Day(1),
            RequiereFirma: requiresSignature);
    }

    private static DateTimeOffset Day(int offset) => new(2026, 2, 1 + offset, 0, 0, 0, TimeSpan.Zero);

    private static Fixture CreateFixture()
    {
        var provider = new InMemoryDataProvider();
        return new Fixture(provider, new WorkOrderService(provider, new NullAuditService()));
    }

    private sealed record Fixture(InMemoryDataProvider Provider, WorkOrderService Service);

    private sealed class InMemoryDataProvider : IDataProvider
    {
        private readonly Dictionary<string, IReadOnlyList<DataRow>> _rows = new(StringComparer.OrdinalIgnoreCase)
        {
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
