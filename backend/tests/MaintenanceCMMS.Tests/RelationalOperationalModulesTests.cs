using Xunit;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Availability;
using MaintenanceCMMS.Application.PreventiveMaintenance;
using MaintenanceCMMS.Application.Procurement;
using MaintenanceCMMS.Application.Scheduling;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Availability;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Inventory;
using MaintenanceCMMS.Infrastructure.PreventiveMaintenance;
using MaintenanceCMMS.Infrastructure.Procurement;
using MaintenanceCMMS.Infrastructure.Scheduling;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Tests;

/// <summary>Exercises the relational operational modules against a real PostgreSQL Testcontainer.</summary>
public sealed class RelationalOperationalModulesTests
{
    private static readonly UserAccessContext Admin = new("integration-admin", [AuthRoles.Admin], [], ["FAE-1"]);

    [Fact]
    public async Task Availability_ContractAssignmentAndEvent_PersistAcrossContexts()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        var service = new AvailabilityService(fixture.DbContext);
        var from = DateTimeOffset.UtcNow.AddHours(-2);

        await service.UpsertContractAsync(new("CTR-REL-1", "Contrato relacional", "Cliente", "FAE-1", 24, .9m, from.AddDays(-1)), Admin, CancellationToken.None);
        await service.AssignAssetAsync(new("CTR-REL-1", "ACT-1", ContractAssetRole.Comprometido), Admin, CancellationToken.None);
        await service.RegisterEventAsync(new("CTR-REL-1", "ACT-1", AvailabilityCause.MantenimientoCorrectivo, from, from.AddHours(1), false), Admin, CancellationToken.None);

        await using var second = fixture.NewContext();
        Assert.Equal(1, await second.AvailabilityContracts.CountAsync());
        Assert.Equal(1, await second.AvailabilityContractAssignments.CountAsync());
        Assert.Equal(1, await second.AvailabilityEvents.CountAsync());
    }

    [Fact]
    public async Task Preventive_PlanAndReprogramHistory_PersistAcrossContexts()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        var service = new PreventiveMaintenanceService(fixture.DbContext);
        var due = DateTimeOffset.UtcNow.AddDays(30);

        await service.UpsertPlanAsync(new("PM-REL-1", "Plan relacional", ActivoCodigo: "ACT-1", FrecuenciaDias: 30, FechaInicio: DateTimeOffset.UtcNow), Admin, CancellationToken.None);
        var plan = await service.ReprogramAsync("PM-REL-1", new(ProximaFecha: due, Reason: "Prueba de persistencia"), Admin, CancellationToken.None);

        Assert.NotNull(plan);
        await using var second = fixture.NewContext();
        Assert.Equal(1, await second.PreventivePlans.CountAsync());
        Assert.Equal(1, await second.PreventivePlanScopes.CountAsync());
        Assert.True(await second.PreventiveHistory.AnyAsync(), "La reprogramacion debe dejar historial relacional.");
    }

    [Fact]
    public async Task Preventive_Generation_CopiesChecklistTasksAsAnImmutableSnapshot()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        var db = fixture.DbContext;
        var service = new PreventiveMaintenanceService(db);
        var template = await db.ChecklistTemplates.SingleAsync(x => x.Code == "TPL-BASE");
        var firstItem = await db.ChecklistTemplateItems.SingleAsync(x => x.TemplateId == template.Id && x.SortOrder == 1);
        db.ChecklistTemplateItems.Add(new ChecklistTemplateItemEntity
        {
            TemplateId = template.Id,
            SortOrder = 2,
            ItemText = "Medicion preventiva",
            Mandatory = true,
            ResponseTypeId = firstItem.ResponseTypeId,
            RequiresPhoto = true,
            IsActive = true
        });
        await db.SaveChangesAsync();

        await service.UpsertPlanAsync(new("PM-SNAPSHOT", "Plan con snapshot", ActivoCodigo: "ACT-1", FrecuenciaDias: 30, ChecklistCodigo: template.Code, FechaInicio: DateTimeOffset.UtcNow), Admin, CancellationToken.None);
        var generated = await service.GenerateWorkOrderAsync("PM-SNAPSHOT", new("ACT-1", "Generacion de prueba"), Admin, CancellationToken.None);

        var order = await db.WorkOrders.Include(x => x.MaintenanceType).Include(x => x.Tasks).Include(x => x.Checklist).SingleAsync(x => x.WorkOrderNumber == generated.NumeroOT);
        Assert.Equal(PreventiveStatus.OTGenerada, generated.Estado);
        Assert.True(order.IsAutomaticPreventive);
        Assert.Equal("PM-SNAPSHOT", order.PreventivePlanCode);
        Assert.Equal("Preventive", order.MaintenanceType.Code);
        Assert.Equal(2, order.Tasks.Count);
        Assert.All(order.Tasks, task => Assert.Equal("PautaPreventiva", task.Origin));
        Assert.Equal(2, order.Checklist.Count);

        firstItem.ItemText = "Pauta modificada despues de generar la OT";
        await db.SaveChangesAsync();
        await using var verification = fixture.NewContext();
        var snapshotTitles = await verification.WorkOrderTasks.Where(x => x.WorkOrderId == order.Id).OrderBy(x => x.TaskCode).Select(x => x.Title).ToArrayAsync();
        Assert.Contains("Verificacion base", snapshotTitles);
        Assert.Contains("Medicion preventiva", snapshotTitles);
        Assert.DoesNotContain("Pauta modificada despues de generar la OT", snapshotTitles);
    }
    [Fact]
    public async Task Scheduling_WorkshopResponsible_AcceptsEligibleAdminOrSupervisor_AndRejectsInvalidAssignments()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        var db = fixture.DbContext;
        var service = new SchedulingService(db);
        var administrator = await db.Users.SingleAsync(x => x.Username == "admin");
        var supervisor = await db.Users.SingleAsync(x => x.Id == PostgreSqlWorkTestFixture.SupervisorUserId);
        var technician = await db.Users.SingleAsync(x => x.Id == PostgreSqlWorkTestFixture.TechnicianOneUserId);
        var administratorRole = new RoleEntity { Code = AuthRoles.Admin, Name = "Administrador", Type = "System", IsActive = true };
        db.Roles.Add(administratorRole);
        db.UserRoles.Add(new UserRoleEntity { User = administrator, Role = administratorRole });
        await db.SaveChangesAsync();

        var eligible = await service.ListWorkshopSupervisorsAsync(Admin, CancellationToken.None);
        Assert.Contains(eligible, x => x.UsuarioId == administrator.Id.ToString("D"));
        Assert.Contains(eligible, x => x.UsuarioId == supervisor.Id.ToString("D"));
        var workshop = await service.UpsertWorkshopAsync(new("TAL-ADMIN", "Taller administrado", 2, "Antofagasta", administrator.Id.ToString("D")), Admin, CancellationToken.None);
        Assert.Equal(administrator.Id.ToString("D"), workshop.SupervisorUsuarioId);
        await Assert.ThrowsAsync<MaintenanceCMMS.Domain.Common.DomainException>(() => service.UpsertWorkshopAsync(new("TAL-TECH", "Taller técnico", 2, "Antofagasta", technician.Id.ToString("D")), Admin, CancellationToken.None));

        administrator.IsLocked = true; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<MaintenanceCMMS.Domain.Common.DomainException>(() => service.UpsertWorkshopAsync(new("TAL-LOCK", "Taller bloqueado", 2, "Antofagasta", administrator.Id.ToString("D")), Admin, CancellationToken.None));
        administrator.IsLocked = false; administrator.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<MaintenanceCMMS.Domain.Common.DomainException>(() => service.UpsertWorkshopAsync(new("TAL-INACTIVE", "Taller inactivo", 2, "Antofagasta", administrator.Id.ToString("D")), Admin, CancellationToken.None));
        administrator.IsActive = true;
        var administratorAssignment = await db.UserRoles.SingleAsync(x => x.UserId == administrator.Id && x.RoleId == administratorRole.Id);
        administratorAssignment.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<MaintenanceCMMS.Domain.Common.DomainException>(() => service.UpsertWorkshopAsync(new("TAL-NOROLE", "Taller sin rol", 2, "Antofagasta", administrator.Id.ToString("D")), Admin, CancellationToken.None));

        var supervisorAssignment = await db.UserRoles.SingleAsync(x => x.UserId == supervisor.Id && x.Role.Code == AuthRoles.MaintenanceSupervisor);
        supervisorAssignment.IsActive = false; await db.SaveChangesAsync();
        await Assert.ThrowsAsync<MaintenanceCMMS.Domain.Common.DomainException>(() => service.UpsertWorkshopAsync(new("TAL-SUP-NOROLE", "Taller supervisor sin rol", 2, "Antofagasta", supervisor.Id.ToString("D")), Admin, CancellationToken.None));
        supervisorAssignment.IsActive = true;
        db.UserRoles.Add(new UserRoleEntity { User = supervisor, Role = administratorRole });
        await db.SaveChangesAsync();
        eligible = await service.ListWorkshopSupervisorsAsync(Admin, CancellationToken.None);
        Assert.Equal(1, eligible.Count(x => x.UsuarioId == supervisor.Id.ToString("D")));
    }

    [Fact]
    public async Task Scheduling_DependencyRejectsDuplicateAndCycle_AndPersists()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        var db = fixture.DbContext;
        var faena = await db.Faenas.SingleAsync(x => x.Code == "FAE-1");
        var asset = await db.Assets.SingleAsync(x => x.Code == "ACT-1");
        var status = await db.WorkCatalogs.SingleAsync(x => x.Category == "WorkOrderLifecycleStatus" && x.Code == "OTCreada");
        var maintenanceType = await db.WorkCatalogs.SingleAsync(x => x.Category == "MaintenanceType" && x.Code == "Corrective");
        db.WorkOrders.AddRange(
            new WorkOrderEntity { WorkOrderNumber = "OT-REL-1", AssetId = asset.Id, FaenaId = faena.Id, StatusId = status.Id, MaintenanceTypeId = maintenanceType.Id, Description = "Primera OT", CreatedByUserId = Admin.UserId, CreatedByUserAtUtc = DateTimeOffset.UtcNow },
            new WorkOrderEntity { WorkOrderNumber = "OT-REL-2", AssetId = asset.Id, FaenaId = faena.Id, StatusId = status.Id, MaintenanceTypeId = maintenanceType.Id, Description = "Segunda OT", CreatedByUserId = Admin.UserId, CreatedByUserAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var service = new SchedulingService(db);
        await service.UpsertWorkshopAsync(new("TAL-REL", "Taller relacional", 4, "Antofagasta", PostgreSqlWorkTestFixture.SupervisorUserId.ToString("D")), Admin, CancellationToken.None);
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(1).AddHours(8), TimeSpan.Zero);
        await service.ScheduleWorkOrderAsync("OT-REL-1", new("TAL-REL", start, start.AddHours(2), 2, "PlanificaciÃ³n"), Admin, CancellationToken.None);
        await service.ScheduleWorkOrderAsync("OT-REL-2", new("TAL-REL", start.AddHours(3), start.AddHours(5), 2, "PlanificaciÃ³n"), Admin, CancellationToken.None);
        await service.AddDependencyAsync(new("OT-REL-1", "OT-REL-2"), Admin, CancellationToken.None);

        await Assert.ThrowsAsync<MaintenanceCMMS.Domain.Common.DomainException>(() => service.AddDependencyAsync(new("OT-REL-1", "OT-REL-2"), Admin, CancellationToken.None));
        await Assert.ThrowsAsync<MaintenanceCMMS.Domain.Common.DomainException>(() => service.AddDependencyAsync(new("OT-REL-2", "OT-REL-1"), Admin, CancellationToken.None));
        await using var second = fixture.NewContext();
        Assert.Equal(2, await second.WorkOrderSchedules.CountAsync());
        Assert.Single(await second.ScheduleDependencies.ToListAsync());
    }

    [Fact]
    public async Task Procurement_SupplierRequestAndPurchaseOrder_PersistAcrossContexts()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        var audit = new PostgreSqlAuditService(fixture.DbContext, new AuditContextAccessor());
        var inventory = new InventoryService(fixture.DbContext, audit, new AuthorizationPolicyService());
        var service = new ProcurementService(fixture.DbContext, inventory, audit);

        await service.CreateSupplierAsync(new("76.123.456-7", "Proveedor relacional"), Admin, CancellationToken.None);
        var request = await service.CreateRequestAsync(new("Filtro hidrÃ¡ulico", 2, "UN", "ReposiciÃ³n", FaenaCodigo: "FAE-1"), Admin, CancellationToken.None);
        var linked = await service.LinkPurchaseOrderAsync(request.SolicitudId, new("OC-REL-1", "76.123.456-7", DateTimeOffset.UtcNow.AddDays(7), "Compra aprobada"), Admin, CancellationToken.None);

        Assert.NotNull(linked);
        Assert.Equal(ProcurementRequestStatus.OCAsociada, linked!.Estado);
        await using var second = fixture.NewContext();
        Assert.Equal(1, await second.Suppliers.CountAsync());
        Assert.Equal(1, await second.ProcurementRequests.CountAsync());
        Assert.Equal(1, await second.PurchaseOrders.CountAsync());
    }
}
