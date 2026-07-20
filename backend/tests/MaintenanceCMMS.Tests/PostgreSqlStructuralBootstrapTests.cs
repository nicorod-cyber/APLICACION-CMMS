using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class PostgreSqlStructuralBootstrapTests
{
    [Fact]
    public async Task BootstrapAsync_CreatesOnlyStructuralRecords_AndIsIdempotent()
    {
        var name = $"cmms_test_structural_{Guid.NewGuid():N}";
        var adminConnectionString = await PostgreSqlWorkTestFixture.GetAdminConnectionStringAsync();
        await PostgreSqlWorkTestFixture.CreateDatabaseAsync(name, adminConnectionString);
        await using var db = new CmmsDbContext(new DbContextOptionsBuilder<CmmsDbContext>()
            .UseNpgsql(PostgreSqlWorkTestFixture.ConnectionString(adminConnectionString, name)).Options);

        try
        {
            await db.Database.MigrateAsync();
            var bootstrap = new PostgreSqlStructuralBootstrap(
                db,
                new PasswordHasher(),
                Options.Create(new AuthSeedOptions
                {
                    Username = "pilot-admin",
                    Email = "pilot-admin@example.test",
                    DisplayName = "Administrador Pilot",
                    Password = "Test.Pilot123!"
                }));

            await bootstrap.BootstrapAsync(CancellationToken.None);
            var first = await CountsAsync(db);
            await bootstrap.BootstrapAsync(CancellationToken.None);
            var second = await CountsAsync(db);

            Assert.Equal(first, second);
            Assert.Equal(1, first.Users);
            Assert.True(first.Roles > 0);
            Assert.True(first.Permissions > 0);
            Assert.True(first.RolePermissions > 0);
            Assert.True(first.OperationalStates >= 4);
            Assert.True(first.WorkCatalogs > 0);
            Assert.True(first.InventoryCatalogs > 0);
            Assert.Equal(0, first.Faenas + first.TechnicalLocations + first.Assets + first.TechnicalNodes + first.OperationalUnits + first.OperationalComponents + first.AssetReadings + first.Documents + first.WorkOrders + first.WorkNotifications + first.PreventiveEvaluations + first.AlertRules + first.PdfTemplates + first.ChecklistTemplates);
            Assert.Equal(0, first.AssetTypes + first.DocumentTypes);
        }
        finally
        {
            await db.DisposeAsync();
            await PostgreSqlWorkTestFixture.DropDatabaseAsync(name, adminConnectionString);
        }
    }

    private static async Task<Counts> CountsAsync(CmmsDbContext db) => new(
        await db.Users.CountAsync(),
        await db.Roles.CountAsync(),
        await db.Permissions.CountAsync(),
        await db.RolePermissions.CountAsync(),
        await db.AssetOperationalStates.CountAsync(),
        await db.WorkCatalogs.CountAsync(),
        await db.InventoryCatalogs.CountAsync(),
        await db.AssetTypes.CountAsync(),
        await db.DocumentTypes.CountAsync(),
        await db.Faenas.CountAsync(),
        await db.TechnicalLocations.CountAsync(),
        await db.Assets.CountAsync(),
        await db.TechnicalNodes.CountAsync(),
        await db.OperationalUnits.CountAsync(),
        await db.OperationalUnitComponents.CountAsync(),
        await db.AssetReadings.CountAsync(),
        await db.Documents.CountAsync(),
        await db.WorkOrders.CountAsync(),
        await db.WorkNotifications.CountAsync(),
        await db.PreventiveEvaluations.CountAsync(),
        await db.AlertRules.CountAsync(),
        await db.PdfTemplates.CountAsync(),
        await db.ChecklistTemplates.CountAsync());

    private sealed record Counts(
        int Users, int Roles, int Permissions, int RolePermissions, int OperationalStates, int WorkCatalogs, int InventoryCatalogs, int AssetTypes, int DocumentTypes,
        int Faenas, int TechnicalLocations, int Assets, int TechnicalNodes, int OperationalUnits, int OperationalComponents,
        int AssetReadings, int Documents, int WorkOrders, int WorkNotifications, int PreventiveEvaluations, int AlertRules,
        int PdfTemplates, int ChecklistTemplates);
}