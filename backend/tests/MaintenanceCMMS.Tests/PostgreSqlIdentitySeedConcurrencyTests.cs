using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class PostgreSqlIdentitySeedConcurrencyTests
{
    [Fact]
    public async Task SeedAsync_IsIdempotent_RepairsDifferentialChanges_AndSerializesConcurrentSeeders()
    {
        await using var database = await PostgreSqlWorkTestFixture.CreateAsync();
        await using var services = CreateServices(database.DatabaseName);

        await SeedAsync(services);

        DateTimeOffset? unchangedRoleVersion;
        await using (var context = database.NewContext())
        {
            unchangedRoleVersion = await context.Roles
                .Where(role => role.Code == AuthRoles.Management)
                .Select(role => role.UpdatedAtUtc)
                .SingleAsync();
        }

        await SeedAsync(services);

        await using (var context = database.NewContext())
        {
            var unchangedAfterSecondSeed = await context.Roles
                .Where(role => role.Code == AuthRoles.Management)
                .Select(role => role.UpdatedAtUtc)
                .SingleAsync();
            Assert.Equal(unchangedRoleVersion, unchangedAfterSecondSeed);
        }

        var mutatedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await using (var context = database.NewContext())
        {
            var planner = await context.Roles.SingleAsync(role => role.Code == AuthRoles.Planner);
            planner.Name = "Planificador temporal";
            planner.UpdatedAtUtc = mutatedAt;

            var stalePermission = new PermissionEntity
            {
                Code = "test.seed.permiso.sobrante",
                Name = "Permiso temporal",
                IsActive = true
            };
            context.Permissions.Add(stalePermission);
            context.RolePermissions.Add(new RolePermissionEntity
            {
                Role = planner,
                Permission = stalePermission,
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        await SeedAsync(services);

        DateTimeOffset? plannerUpdatedAt;
        DateTimeOffset? staleRelationUpdatedAt;
        await using (var context = database.NewContext())
        {
            var planner = await context.Roles.SingleAsync(role => role.Code == AuthRoles.Planner);
            Assert.Equal("Planificador", planner.Name);
            Assert.NotNull(planner.UpdatedAtUtc);
            Assert.True(planner.UpdatedAtUtc > mutatedAt);
            plannerUpdatedAt = planner.UpdatedAtUtc;

            var staleRelations = await context.RolePermissions
                .Where(relation => relation.Role.Code == AuthRoles.Planner
                    && relation.Permission.Code == "test.seed.permiso.sobrante")
                .ToListAsync();
            var staleRelation = Assert.Single(staleRelations);
            Assert.False(staleRelation.IsActive);
            Assert.NotNull(staleRelation.UpdatedAtUtc);
            staleRelationUpdatedAt = staleRelation.UpdatedAtUtc;

            var duplicatedActiveRelations = await context.RolePermissions
                .Where(relation => relation.IsActive)
                .GroupBy(relation => new { relation.RoleId, relation.PermissionId })
                .Where(group => group.Count() > 1)
                .AnyAsync();
            Assert.False(duplicatedActiveRelations);
        }

        await Task.WhenAll(SeedAsync(services), SeedAsync(services));

        await using (var context = database.NewContext())
        {
            var planner = await context.Roles.SingleAsync(role => role.Code == AuthRoles.Planner);
            Assert.Equal(plannerUpdatedAt, planner.UpdatedAtUtc);

            var staleRelation = await context.RolePermissions.SingleAsync(relation =>
                relation.Role.Code == AuthRoles.Planner
                && relation.Permission.Code == "test.seed.permiso.sobrante");
            Assert.False(staleRelation.IsActive);
            Assert.Equal(staleRelationUpdatedAt, staleRelation.UpdatedAtUtc);

            var roles = await new PostgreSqlIdentityStore(context).ListRolesAsync(CancellationToken.None);
            AssertRolePermissions(roles, AuthRoles.Admin, AuthPermissions.ManageAssetCatalogs, AuthPermissions.ManageAssetAttributes, AuthPermissions.RegisterAssetReadings, AuthPermissions.CorrectAssetReadings, AuthPermissions.ViewOperationalUnits, AuthPermissions.ManageOperationalUnits, AuthPermissions.ManageOperationalUnitComposition, AuthPermissions.ManageDocumentRequirements);
            AssertRolePermissions(roles, AuthRoles.Planner, AuthPermissions.ManageAssetAttributes, AuthPermissions.RegisterAssetReadings, AuthPermissions.CorrectAssetReadings, AuthPermissions.ViewOperationalUnits, AuthPermissions.ManageOperationalUnits, AuthPermissions.ManageOperationalUnitComposition, AuthPermissions.ManageDocumentRequirements);
            AssertRolePermissions(roles, AuthRoles.MaintenanceSupervisor, AuthPermissions.RegisterAssetReadings, AuthPermissions.CorrectAssetReadings, AuthPermissions.ViewOperationalUnits, AuthPermissions.ManageOperationalUnits, AuthPermissions.ManageOperationalUnitComposition);
            AssertRolePermissions(roles, AuthRoles.Technician, AuthPermissions.RegisterAssetReadings, AuthPermissions.ViewOperationalUnits);
            AssertRolePermissions(roles, AuthRoles.Management, AuthPermissions.ViewOperationalUnits);
            AssertRolePermissions(roles, AuthRoles.FaenaViewer, AuthPermissions.ViewOperationalUnits);
            AssertRolePermissions(roles, AuthRoles.Warehouse);
            AssertRolePermissions(roles, AuthRoles.WarehouseSupervisor);
        }
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var seedService = scope.ServiceProvider.GetRequiredService<IIdentitySeedService>();
        await seedService.SeedAsync(CancellationToken.None);
    }

    private static ServiceProvider CreateServices(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Debug));
        services.AddDbContext<CmmsDbContext>(options => options.UseNpgsql(
            $"Host=localhost;Port=5432;Database={databaseName};Username=cmms_app;Password=cmms_app_password"));
        services.AddScoped<IIdentityStore, PostgreSqlIdentityStore>();
        services.AddSingleton<IIdentitySeedTransaction, PostgreSqlIdentitySeedTransaction>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IIdentitySeedService, IdentitySeedService>();
        services.AddSingleton<IOptions<AuthSeedOptions>>(Options.Create(new AuthSeedOptions
        {
            Username = "admin",
            Email = "admin@example.local",
            DisplayName = "Administrador Tests",
            Password = "Test.Admin123!"
        }));

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static void AssertRolePermissions(
        IReadOnlyCollection<RoleDefinition> roles,
        string roleCode,
        params string[] expectedNewPermissions)
    {
        var newPermissions = new[]
        {
            AuthPermissions.ManageAssetCatalogs,
            AuthPermissions.ManageAssetAttributes,
            AuthPermissions.RegisterAssetReadings,
            AuthPermissions.CorrectAssetReadings,
            AuthPermissions.ViewOperationalUnits,
            AuthPermissions.ManageOperationalUnits,
            AuthPermissions.ManageOperationalUnitComposition,
            AuthPermissions.ManageDocumentRequirements
        };
        var role = roles.Single(item => item.Code == roleCode);
        Assert.Equal(
            expectedNewPermissions.Order(StringComparer.Ordinal),
            role.Permissions.Where(newPermissions.Contains).Order(StringComparer.Ordinal));
    }
}