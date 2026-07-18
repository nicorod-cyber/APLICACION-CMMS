using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.WorkOrders;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class WorkOrderServiceTests
{
    private const string FaenaCode = "FAE-1";
    private static readonly UserAccessContext Planner = new(PostgreSqlWorkTestFixture.PlannerUserId.ToString("D"), [AuthRoles.Planner], [AuthPermissions.CreateWorkOrders, AuthPermissions.AssignWorkOrderSupervisor, AuthPermissions.SendWorkOrderToSupervisor, AuthPermissions.FinalValidateWorkOrders], [FaenaCode]);
    private static readonly UserAccessContext Supervisor = new(PostgreSqlWorkTestFixture.SupervisorUserId.ToString("D"), [AuthRoles.MaintenanceSupervisor], [AuthPermissions.ManageWorkOrderTasks, AuthPermissions.ManageWorkOrderTechnicians, AuthPermissions.ReviewWorkOrderLabor, AuthPermissions.ReviewWorkOrderTasks, AuthPermissions.CloseWorkOrders], [FaenaCode]);
    private static readonly UserAccessContext TechnicianOne = new(PostgreSqlWorkTestFixture.TechnicianOneUserId.ToString("D"), [AuthRoles.Technician], [AuthPermissions.ExecuteAssignedWorkOrders, AuthPermissions.RegisterWorkOrderLabor, AuthPermissions.RegisterWorkOrderEvidence, AuthPermissions.SignWorkOrders], [FaenaCode]);
    private static readonly UserAccessContext TechnicianTwo = new(PostgreSqlWorkTestFixture.TechnicianTwoUserId.ToString("D"), [AuthRoles.Technician], [AuthPermissions.ExecuteAssignedWorkOrders, AuthPermissions.RegisterWorkOrderLabor, AuthPermissions.RegisterWorkOrderEvidence, AuthPermissions.SignWorkOrders], [FaenaCode]);

    [Fact]
    public async Task CorrectiveFlow_ClosesAndPlanningValidatesWithOrderScopedTechnicians()
    {
        await using var fixture = await Fixture.CreateAsync();
        var order = await CreateAndSendAsync(fixture);
        var taskOne = await fixture.Service.AddTaskAsync(order.Summary.NumeroOT, new CreateWorkOrderTaskRequest("Reparar fuga hidráulica", "SMOKE-1", RequiereEvidencia: true, RequiereHH: true), Supervisor, default);
        var taskTwo = await fixture.Service.AddTaskAsync(order.Summary.NumeroOT, new CreateWorkOrderTaskRequest("Probar equipo reparado", "SMOKE-2", RequiereEvidencia: true, RequiereHH: true), Supervisor, default);
        Assert.NotNull(taskOne);
        Assert.NotNull(taskTwo);

        var technicians = await fixture.Service.AssignTechniciansAsync(order.Summary.NumeroOT, new AssignWorkOrderTechniciansRequest([PostgreSqlWorkTestFixture.TechnicianOneUserId, PostgreSqlWorkTestFixture.TechnicianTwoUserId]), Supervisor, default);
        Assert.Equal(2, technicians!.Count);
        Assert.Equal(2, (await fixture.Service.ListTasksAsync(order.Summary.NumeroOT, TechnicianOne, default))!.Count);
        Assert.Single(await fixture.Service.ListMyAssignedAsync(TechnicianOne, default));

        var laborOne = await ExecuteTaskAsync(fixture, order.Summary.NumeroOT, taskOne!, TechnicianOne);
        var laborTwo = await ExecuteTaskAsync(fixture, order.Summary.NumeroOT, taskTwo!, TechnicianTwo);
        await fixture.Service.ApproveLaborAsync(order.Summary.NumeroOT, Guid.Parse(laborOne.HHId), new WorkOrderTaskActionRequest("HH conforme"), Supervisor, default);
        await fixture.Service.ApproveLaborAsync(order.Summary.NumeroOT, Guid.Parse(laborTwo.HHId), new WorkOrderTaskActionRequest("HH conforme"), Supervisor, default);

        var signatureOne = await AddImageAsync(fixture.DbContext, order.Summary.NumeroOT, "firma-tech-1.png", TechnicianOne.UserId);
        var signatureTwo = await AddImageAsync(fixture.DbContext, order.Summary.NumeroOT, "firma-tech-2.png", TechnicianTwo.UserId);
        await fixture.Service.RegisterOwnSignatureAsync(order.Summary.NumeroOT, new RegisterOwnWorkOrderSignatureRequest("Firma técnico uno"), signatureOne.Id, TechnicianOne, default);
        await fixture.Service.RegisterOwnSignatureAsync(order.Summary.NumeroOT, new RegisterOwnWorkOrderSignatureRequest("Firma técnico dos"), signatureTwo.Id, TechnicianTwo, default);
        await fixture.Service.ApproveTaskAsync(order.Summary.NumeroOT, taskOne!.CodigoTarea, new WorkOrderTaskActionRequest("Tarea aprobada"), Supervisor, default);
        await fixture.Service.ApproveTaskAsync(order.Summary.NumeroOT, taskTwo!.CodigoTarea, new WorkOrderTaskActionRequest("Tarea aprobada"), Supervisor, default);

        var closed = await fixture.Service.CloseTechnicallyAsync(order.Summary.NumeroOT, new WorkOrderActionRequest("Cierre técnico conforme"), Supervisor, default);
        var validated = await fixture.Service.ValidatePlanningAsync(order.Summary.NumeroOT, new WorkOrderActionRequest("Validación de planificación"), Planner, default);

        Assert.Equal(WorkOrderLifecycleStatus.CerradaTecnicamente, closed!.Summary.Estado);
        Assert.Equal(WorkOrderLifecycleStatus.ValidadaPlanificacion, validated!.Summary.Estado);
        Assert.Empty(validated.ClosureBlockers);
    }

    [Fact]
    public async Task TechnicianCannotReadForeignOrder_AndDuplicateAssignmentIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var order = await CreateAndSendAsync(fixture);
        await fixture.Service.AddTaskAsync(order.Summary.NumeroOT, new CreateWorkOrderTaskRequest("Tarea compartida", "SMOKE-READ"), Supervisor, default);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Service.GetByIdAsync(order.Summary.NumeroOT, TechnicianOne, default));

        await fixture.Service.AssignTechniciansAsync(order.Summary.NumeroOT, new AssignWorkOrderTechniciansRequest([PostgreSqlWorkTestFixture.TechnicianOneUserId]), Supervisor, default);
        var duplicate = await Assert.ThrowsAsync<DomainException>(() => fixture.Service.AssignTechniciansAsync(order.Summary.NumeroOT, new AssignWorkOrderTechniciansRequest([PostgreSqlWorkTestFixture.TechnicianOneUserId]), Supervisor, default));
        Assert.Contains("ya está asignado", duplicate.Message);
    }

    [Fact]
    public async Task CompleteTask_BlocksWithoutPhoto()
    {
        await using var fixture = await Fixture.CreateAsync();
        var order = await CreateAndSendAsync(fixture);
        var task = await fixture.Service.AddTaskAsync(order.Summary.NumeroOT, new CreateWorkOrderTaskRequest("Inspección visual", "SMOKE-BLOCK", RequiereEvidencia: true, RequiereHH: true), Supervisor, default);
        Assert.NotNull(task);
        await fixture.Service.AssignTechniciansAsync(order.Summary.NumeroOT, new AssignWorkOrderTechniciansRequest([PostgreSqlWorkTestFixture.TechnicianOneUserId]), Supervisor, default);
        await fixture.Service.StartTaskAsync(order.Summary.NumeroOT, task!.CodigoTarea, new WorkOrderTaskActionRequest("Inicio"), TechnicianOne, default);
        await fixture.Service.RegisterOwnLaborAsync(order.Summary.NumeroOT, task.CodigoTarea, new RegisterOwnLaborRequest(DateOnly.FromDateTime(DateTime.UtcNow), new TimeOnly(8, 0), new TimeOnly(9, 0), null, "NORMAL", "Inspección"), TechnicianOne, default);

        var blocked = await Assert.ThrowsAsync<DomainException>(() => fixture.Service.CompleteTaskAsync(order.Summary.NumeroOT, task.CodigoTarea, new WorkOrderTaskActionRequest("Completar"), TechnicianOne, default));
        Assert.Contains("fotografía", blocked.Message);
    }

    private static async Task<WorkOrderDetailResponse> CreateAndSendAsync(Fixture fixture)
    {
        var order = await fixture.Service.CreateAsync(new CreateWorkOrderRequest("ACT-1", "OT de regresión operacional", "Corrective", FaenaCodigo: FaenaCode, FechaProgramada: DateTimeOffset.UtcNow), Planner, default);
        await fixture.Service.AssignSupervisorAsync(order.Summary.NumeroOT, new AssignWorkOrderSupervisorRequest(PostgreSqlWorkTestFixture.SupervisorUserId, "Asignación de prueba"), Planner, default);
        return (await fixture.Service.SendToSupervisorAsync(order.Summary.NumeroOT, new WorkOrderActionRequest("Envío a supervisor"), Planner, default))!;
    }

    private static async Task<WorkOrderLaborResponse> ExecuteTaskAsync(Fixture fixture, string orderNumber, WorkOrderTaskResponse task, UserAccessContext technician)
    {
        await fixture.Service.StartTaskAsync(orderNumber, task.CodigoTarea, new WorkOrderTaskActionRequest("Inicio"), technician, default);
        var labor = await fixture.Service.RegisterOwnLaborAsync(orderNumber, task.CodigoTarea, new RegisterOwnLaborRequest(DateOnly.FromDateTime(DateTime.UtcNow), new TimeOnly(8, 0), new TimeOnly(10, 0), null, "NORMAL", "Trabajo ejecutado"), technician, default);
        var image = await AddImageAsync(fixture.DbContext, orderNumber, $"{task.CodigoTarea}-{technician.UserId}.png", technician.UserId);
        await fixture.Service.RegisterUploadedEvidenceAsync(orderNumber, task.CodigoTarea, new UploadWorkOrderEvidenceRequest("FotoDespues", "Evidencia de prueba", DateTimeOffset.UtcNow), image.Id, technician, default);
        var completed = await fixture.Service.CompleteTaskAsync(orderNumber, task.CodigoTarea, new WorkOrderTaskActionRequest("Completar"), technician, default);
        Assert.Equal(WorkOrderTaskStatus.EnRevisionSupervisor, completed!.Estado);
        return labor!;
    }

    private static async Task<FileMetadataEntity> AddImageAsync(CmmsDbContext db, string orderNumber, string fileName, string authorUserId)
    {
        var file = new FileMetadataEntity
        {
            FileKey = $"tests/{Guid.NewGuid():N}/{fileName}",
            FileName = fileName,
            StoredFileName = fileName,
            Extension = ".png",
            Provider = "LocalSimulation",
            StorageMode = "LocalSimulation",
            Purpose = "Evidence",
            Module = "WorkOrders",
            EntityType = "WorkOrder",
            EntityId = orderNumber,
            WorkOrderNumber = orderNumber,
            LogicalUri = $"/tests/{fileName}",
            MimeType = "image/png",
            SizeBytes = 1,
            Status = "Stored",
            AuthorUserId = authorUserId
        };
        db.Files.Add(file);
        await db.SaveChangesAsync();
        return file;
    }

    private sealed record Fixture(PostgreSqlWorkTestFixture Database, CmmsDbContext DbContext, WorkOrderService Service) : IAsyncDisposable
    {
        public static async Task<Fixture> CreateAsync()
        {
            var database = await PostgreSqlWorkTestFixture.CreateAsync();
            return new Fixture(database, database.DbContext, new WorkOrderService(database.DbContext, new NullAuditService()));
        }

        public ValueTask DisposeAsync() => Database.DisposeAsync();
    }

    private sealed class NullAuditService : IAuditService
    {
        public Task<string> RecordAsync(AuditEventRequest auditEvent, CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid().ToString("N"));
        public Task<AuditQueryResult> QueryAsync(AuditQuery query, CancellationToken cancellationToken) => Task.FromResult(new AuditQueryResult(0, []));
    }
}