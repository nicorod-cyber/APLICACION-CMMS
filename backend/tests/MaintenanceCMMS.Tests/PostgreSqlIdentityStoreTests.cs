using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class PostgreSqlIdentityStoreTests
{
    [Fact]
    public async Task UpsertUserAsync_SynchronizesRolesAndFaenasById_AndReactivatesHistoricalAssignments()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();

        var initialUser = await GetPlannerAsync(fixture);
        await UpsertAsync(fixture, initialUser with
        {
            Roles = [" PLANIFICADOR ", AuthRoles.Planner.ToUpperInvariant(), AuthRoles.MaintenanceSupervisor],
            Faenas = [" fae-1 "]
        });

        await using (var context = fixture.NewContext())
        {
            var plannerRole = await context.Roles.SingleAsync(role => role.Code == AuthRoles.Planner);
            var supervisorRole = await context.Roles.SingleAsync(role => role.Code == AuthRoles.MaintenanceSupervisor);
            var assignments = await context.UserRoles
                .Where(assignment => assignment.UserId == PostgreSqlWorkTestFixture.PlannerUserId)
                .ToListAsync();

            Assert.Equal(1, assignments.Count(assignment => assignment.RoleId == plannerRole.Id));
            Assert.Equal(1, assignments.Count(assignment => assignment.RoleId == supervisorRole.Id));
            Assert.All(assignments, assignment => Assert.True(assignment.IsActive));
        }

        var userWithTwoRoles = await GetPlannerAsync(fixture);
        await UpsertAsync(fixture, userWithTwoRoles with { Roles = [AuthRoles.MaintenanceSupervisor] });

        await using (var context = fixture.NewContext())
        {
            var plannerRole = await context.Roles.SingleAsync(role => role.Code == AuthRoles.Planner);
            var plannerAssignment = await context.UserRoles.SingleAsync(assignment =>
                assignment.UserId == PostgreSqlWorkTestFixture.PlannerUserId && assignment.RoleId == plannerRole.Id);

            Assert.False(plannerAssignment.IsActive);
            Assert.NotNull(plannerAssignment.UnassignedAtUtc);
        }

        var userWithSupervisorRole = await GetPlannerAsync(fixture);
        await UpsertAsync(fixture, userWithSupervisorRole with { Roles = [AuthRoles.Planner] });

        await using (var context = fixture.NewContext())
        {
            var plannerRole = await context.Roles.SingleAsync(role => role.Code == AuthRoles.Planner);
            var supervisorRole = await context.Roles.SingleAsync(role => role.Code == AuthRoles.MaintenanceSupervisor);
            var plannerAssignment = await context.UserRoles.SingleAsync(assignment =>
                assignment.UserId == PostgreSqlWorkTestFixture.PlannerUserId && assignment.RoleId == plannerRole.Id);
            var supervisorAssignment = await context.UserRoles.SingleAsync(assignment =>
                assignment.UserId == PostgreSqlWorkTestFixture.PlannerUserId && assignment.RoleId == supervisorRole.Id);

            Assert.True(plannerAssignment.IsActive);
            Assert.Null(plannerAssignment.UnassignedAtUtc);
            Assert.False(supervisorAssignment.IsActive);
            Assert.NotNull(supervisorAssignment.UnassignedAtUtc);
        }

        var userWithFaena = await GetPlannerAsync(fixture);
        await UpsertAsync(fixture, userWithFaena with { Faenas = [] });

        await using (var context = fixture.NewContext())
        {
            var assignment = await context.UserFaenas.SingleAsync(item => item.UserId == PostgreSqlWorkTestFixture.PlannerUserId);
            Assert.False(assignment.IsActive);
            Assert.NotNull(assignment.UnassignedAtUtc);
        }

        var userWithoutFaena = await GetPlannerAsync(fixture);
        await UpsertAsync(fixture, userWithoutFaena with { Faenas = [" FAE-1 "] });

        await using (var context = fixture.NewContext())
        {
            var assignments = await context.UserFaenas
                .Where(item => item.UserId == PostgreSqlWorkTestFixture.PlannerUserId)
                .ToListAsync();

            var assignment = Assert.Single(assignments);
            Assert.True(assignment.IsActive);
            Assert.Null(assignment.UnassignedAtUtc);
        }
    }

    [Fact]
    public async Task UpsertUserAsync_RejectsUnknownRoleAndFaenaWithDomainExceptions()
    {
        await using var fixture = await PostgreSqlWorkTestFixture.CreateAsync();
        var user = await GetPlannerAsync(fixture);

        await using (var context = fixture.NewContext())
        {
            var store = new PostgreSqlIdentityStore(context);
            var exception = await Assert.ThrowsAsync<DomainException>(() =>
                store.UpsertUserAsync(user with { Roles = ["rol_inexistente"] }, CancellationToken.None));

            Assert.Contains("rol_inexistente", exception.Message);
        }

        await using (var context = fixture.NewContext())
        {
            var store = new PostgreSqlIdentityStore(context);
            var exception = await Assert.ThrowsAsync<DomainException>(() =>
                store.UpsertUserAsync(user with { Faenas = ["faena_inexistente"] }, CancellationToken.None));

            Assert.Contains("FAENA_INEXISTENTE", exception.Message);
        }
    }

    private static async Task<UserAccount> GetPlannerAsync(PostgreSqlWorkTestFixture fixture)
    {
        await using var context = fixture.NewContext();
        var user = await new PostgreSqlIdentityStore(context)
            .FindUserByIdAsync(PostgreSqlWorkTestFixture.PlannerUserId.ToString("D"), CancellationToken.None);

        return Assert.IsType<UserAccount>(user);
    }

    private static async Task UpsertAsync(PostgreSqlWorkTestFixture fixture, UserAccount user)
    {
        await using var context = fixture.NewContext();
        await new PostgreSqlIdentityStore(context).UpsertUserAsync(user, CancellationToken.None);
    }
}
