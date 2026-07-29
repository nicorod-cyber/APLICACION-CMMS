using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
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
    public async Task ChangeOwnPasswordAsync_ReplacesHash_RejectsPreviousPassword_AndAuditsChange()
    {
        var fixture = await CreateFixtureAsync();
        var admin = await fixture.IdentityStore.FindUserByUsernameAsync("admin", CancellationToken.None);
        Assert.NotNull(admin);

        var changed = await fixture.AuthService.ChangeOwnPasswordAsync(
            admin!.Id,
            new ChangePasswordRequest("Test.Admin123!", "Nueva.Clave2026!", "Nueva.Clave2026!"),
            CancellationToken.None);

        Assert.True(changed);
        var updated = await fixture.IdentityStore.FindUserByIdAsync(admin.Id, CancellationToken.None);
        Assert.NotNull(updated);
        Assert.False(new PasswordHasher().Verify("Test.Admin123!", updated!.PasswordHash));
        Assert.True(new PasswordHasher().Verify("Nueva.Clave2026!", updated.PasswordHash));
        Assert.Equal(admin.Roles, updated.Roles);
        Assert.Equal(admin.Faenas, updated.Faenas);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.AuthService.LoginAsync(new LoginRequest("admin", "Test.Admin123!"), CancellationToken.None));
        var response = await fixture.AuthService.LoginAsync(new LoginRequest("admin", "Nueva.Clave2026!"), CancellationToken.None);
        Assert.Equal(admin.Id, response.User.Id);

        var audit = await fixture.AuditService.QueryAsync(new AuditQuery(UserId: admin.Id, Action: "auth.password_changed"), CancellationToken.None);
        var entry = Assert.Single(audit.Items);
        Assert.Null(entry.PreviousValue);
        Assert.Null(entry.NewValue);
    }

    [Theory]
    [InlineData("incorrecta", "Nueva.Clave2026!", "Nueva.Clave2026!", "contraseña actual es incorrecta")]
    [InlineData("Test.Admin123!", "Nueva.Clave2026!", "Otra.Clave2026!", "no coinciden")]
    [InlineData("Test.Admin123!", "Test.Admin123!", "Test.Admin123!", "debe ser diferente")]
    [InlineData("Test.Admin123!", "corta", "corta", "al menos 12 caracteres")]
    [InlineData("Test.Admin123!", "sinmayuscula2026!", "sinmayuscula2026!", "mayúscula")]
    [InlineData("Test.Admin123!", "SINMINUSCULA2026!", "SINMINUSCULA2026!", "minúscula")]
    [InlineData("Test.Admin123!", "SinNumeroEspecial!", "SinNumeroEspecial!", "número")]
    [InlineData("Test.Admin123!", "SinEspecial2026A", "SinEspecial2026A", "carácter especial")]
    public async Task ChangeOwnPasswordAsync_RejectsInvalidPasswordInputs(string current, string next, string confirmation, string message)
    {
        var fixture = await CreateFixtureAsync();
        var admin = await fixture.IdentityStore.FindUserByUsernameAsync("admin", CancellationToken.None);
        Assert.NotNull(admin);

        var exception = await Assert.ThrowsAsync<DomainException>(() => fixture.AuthService.ChangeOwnPasswordAsync(
            admin!.Id,
            new ChangePasswordRequest(current, next, confirmation),
            CancellationToken.None));

        Assert.Contains(message, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangeOwnPasswordAsync_RejectsInactiveAndLockedUsers()
    {
        var fixture = await CreateFixtureAsync();
        var admin = await fixture.IdentityStore.FindUserByUsernameAsync("admin", CancellationToken.None);
        Assert.NotNull(admin);

        await fixture.IdentityStore.UpsertUserAsync(admin! with { IsActive = false }, CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.AuthService.ChangeOwnPasswordAsync(
            admin.Id,
            new ChangePasswordRequest("Test.Admin123!", "Nueva.Clave2026!", "Nueva.Clave2026!"),
            CancellationToken.None));

        await fixture.IdentityStore.UpsertUserAsync(admin with { IsActive = true, IsLocked = true }, CancellationToken.None);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.AuthService.ChangeOwnPasswordAsync(
            admin.Id,
            new ChangePasswordRequest("Test.Admin123!", "Nueva.Clave2026!", "Nueva.Clave2026!"),
            CancellationToken.None));
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
            new AuthService(identityStore, passwordHasher, jwtTokenService, auditService, Options.Create(new PasswordPolicyOptions())),
            auditService);
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), "maintenance-cmms-auth-tests", Guid.NewGuid().ToString("N"));
    }

    private sealed record AuthFixture(
        IIdentityStore IdentityStore,
        IdentitySeedService Seed,
        IAuthService AuthService,
        IAuditService AuditService);
}
