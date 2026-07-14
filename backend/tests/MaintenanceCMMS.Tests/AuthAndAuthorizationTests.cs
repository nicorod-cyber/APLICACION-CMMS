using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Infrastructure.Auditing;
using MaintenanceCMMS.Infrastructure.Data.Excel;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaintenanceCMMS.Tests;

public sealed class AuthAndAuthorizationTests
{
    [Fact]
    public async Task LoginAsync_ReturnsToken_ForValidCredentials()
    {
        var fixture = await CreateFixtureAsync();

        var response = await fixture.AuthService.LoginAsync(
            new LoginRequest("admin", "Test.Admin123!"),
            CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(response.AccessToken));
        Assert.Equal("admin", response.User.Username);
        Assert.Contains(AuthRoles.Admin, response.User.Roles);
    }

    [Fact]
    public async Task LoginAsync_RejectsInvalidCredentials()
    {
        var fixture = await CreateFixtureAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.AuthService.LoginAsync(new LoginRequest("admin", "incorrecta"), CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_RejectsLockedUser()
    {
        var fixture = await CreateFixtureAsync();
        var admin = await fixture.IdentityStore.FindUserByUsernameAsync("admin", CancellationToken.None);
        Assert.NotNull(admin);

        await fixture.IdentityStore.UpsertUserAsync(admin! with { IsLocked = true }, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.AuthService.LoginAsync(new LoginRequest("admin", "Test.Admin123!"), CancellationToken.None));
    }

    [Fact]
    public async Task IdentitySeed_AssignsAuthorizedOperationalPermissionMatrixIdempotently()
    {
        var fixture = await CreateFixtureAsync();
        await fixture.Seed.SeedAsync(CancellationToken.None);
        var roles = (await fixture.IdentityStore.ListRolesAsync(CancellationToken.None)).ToDictionary(role => role.Code, StringComparer.OrdinalIgnoreCase);

        AssertNewPermissions(roles[AuthRoles.Admin], [AuthPermissions.ManageAssetCatalogs, AuthPermissions.ManageAssetAttributes, AuthPermissions.RegisterAssetReadings, AuthPermissions.CorrectAssetReadings, AuthPermissions.ViewOperationalUnits, AuthPermissions.ManageOperationalUnits, AuthPermissions.ManageOperationalUnitComposition, AuthPermissions.ManageDocumentRequirements]);
        AssertNewPermissions(roles[AuthRoles.Planner], [AuthPermissions.ManageAssetAttributes, AuthPermissions.RegisterAssetReadings, AuthPermissions.CorrectAssetReadings, AuthPermissions.ViewOperationalUnits, AuthPermissions.ManageOperationalUnits, AuthPermissions.ManageOperationalUnitComposition, AuthPermissions.ManageDocumentRequirements]);
        AssertNewPermissions(roles[AuthRoles.MaintenanceSupervisor], [AuthPermissions.RegisterAssetReadings, AuthPermissions.CorrectAssetReadings, AuthPermissions.ViewOperationalUnits, AuthPermissions.ManageOperationalUnits, AuthPermissions.ManageOperationalUnitComposition]);
        AssertNewPermissions(roles[AuthRoles.Technician], [AuthPermissions.RegisterAssetReadings, AuthPermissions.ViewOperationalUnits]);
        AssertNewPermissions(roles[AuthRoles.Management], [AuthPermissions.ViewOperationalUnits]);
        AssertNewPermissions(roles[AuthRoles.FaenaViewer], [AuthPermissions.ViewOperationalUnits]);
        AssertNewPermissions(roles[AuthRoles.Warehouse], []);
        AssertNewPermissions(roles[AuthRoles.WarehouseSupervisor], []);
    }
    [Fact]
    public void CanAccessWorkOrder_ReturnsFalse_WhenTechnicianIsNotAssigned()
    {
        var service = new AuthorizationPolicyService();
        var user = new UserAccessContext("tecnico-1", [AuthRoles.Technician], [], ["FAENA-1"]);
        var workOrder = new WorkOrderAccessContext("OT-1", "FAENA-1", ["tecnico-2"]);

        Assert.False(service.CanAccessWorkOrder(user, workOrder));
    }

    [Fact]
    public void CanAccessWorkOrder_ReturnsFalse_WhenSupervisorUsesAnotherFaena()
    {
        var service = new AuthorizationPolicyService();
        var user = new UserAccessContext("supervisor-1", [AuthRoles.MaintenanceSupervisor], [], ["FAENA-1"]);
        var workOrder = new WorkOrderAccessContext("OT-2", "FAENA-2", []);

        Assert.False(service.CanAccessWorkOrder(user, workOrder));
        Assert.False(service.CanViewFaena(user, "FAENA-2"));
    }

    [Fact]
    public void CanViewWarehouses_ReturnsTrue_ForWarehouseRole()
    {
        var service = new AuthorizationPolicyService();
        var user = new UserAccessContext("bodega-1", [AuthRoles.Warehouse], [], []);

        Assert.True(service.CanViewWarehouses(user));
    }

    private static void AssertNewPermissions(RoleDefinition role, IReadOnlyCollection<string> expected)
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
        Assert.Equal(expected.Order(StringComparer.Ordinal), role.Permissions.Where(newPermissions.Contains).Order(StringComparer.Ordinal));
        Assert.Equal(role.Permissions.Count, role.Permissions.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }
    private static async Task<AuthFixture> CreateFixtureAsync()
    {
        var provider = new ExcelDataProvider(
            new ExcelSchemaRegistry(),
            Options.Create(new DataProviderSettings
            {
                Provider = "Excel",
                ExcelPath = CreateTempPath()
            }));

        await provider.InitializeAsync(CancellationToken.None);

        var identityStore = new ExcelIdentityStore(provider);
        var passwordHasher = new PasswordHasher();
        var auditService = new ExcelAuditService(provider, new AuditContextAccessor());
        var jwtTokenService = new JwtTokenService(Options.Create(new JwtOptions
        {
            Issuer = "MaintenanceCMMS.Tests",
            Audience = "MaintenanceCMMS.Tests",
            Secret = "test-maintenance-cmms-jwt-secret-with-32-characters",
            ExpirationMinutes = 30
        }));

        var seed = new IdentitySeedService(
            identityStore,
            passwordHasher,
            Options.Create(new AuthSeedOptions
            {
                Username = "admin",
                Email = "admin@example.local",
                DisplayName = "Administrador Tests",
                Password = "Test.Admin123!"
            }));

        await seed.SeedAsync(CancellationToken.None);

        return new AuthFixture(
            identityStore,
            seed,
            new AuthService(identityStore, passwordHasher, jwtTokenService, auditService));
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "maintenance-cmms-auth-tests", Guid.NewGuid().ToString("N"));
    }

    private sealed record AuthFixture(
        IIdentityStore IdentityStore,
        IdentitySeedService Seed,
        IAuthService AuthService);
}
