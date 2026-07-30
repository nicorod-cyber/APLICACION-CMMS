using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Security;

public sealed class IdentitySeedService : IIdentitySeedService
{
    private readonly IIdentityStore _identityStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly AuthSeedOptions _seedOptions;
    private readonly IIdentitySeedTransaction? _seedTransaction;

    public IdentitySeedService(
        IIdentityStore identityStore,
        IPasswordHasher passwordHasher,
        IOptions<AuthSeedOptions> seedOptions,
        IEnumerable<IIdentitySeedTransaction>? seedTransactions = null)
    {
        _identityStore = identityStore;
        _passwordHasher = passwordHasher;
        _seedOptions = seedOptions.Value;
        _seedTransaction = seedTransactions?.SingleOrDefault();
    }

    public Task SeedAsync(CancellationToken cancellationToken)
    {
        return _seedTransaction is null
            ? SeedCoreAsync(_identityStore, cancellationToken)
            : _seedTransaction.ExecuteAsync(SeedCoreAsync, cancellationToken);
    }

    private static async Task ValidateCanonicalRolePermissionsAsync(IIdentityStore identityStore, CancellationToken cancellationToken)
    {
        var planner = (await identityStore.ListRolesAsync(cancellationToken)).SingleOrDefault(role => string.Equals(role.Code.Trim(), AuthRoles.Planner, StringComparison.OrdinalIgnoreCase));
        var required = new[] { AuthPermissions.ViewFaenas, AuthPermissions.ManageAssets, AuthPermissions.ChangeAssetFaena, AuthPermissions.RegisterAssetReadings, AuthPermissions.CorrectAssetReadings };
        var missing = required.Where(permission => !planner?.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase) == true).ToArray();
        if (planner is null || missing.Length > 0) throw new InvalidOperationException($"Sincronización de identidad incompleta para el rol {AuthRoles.Planner}. Permisos faltantes: {string.Join(", ", missing)}.");
    }

    private async Task SeedCoreAsync(IIdentityStore identityStore, CancellationToken cancellationToken)
    {
        await identityStore.UpsertRolesAsync(RolePermissionCatalog.InitialRoles, cancellationToken);
        await ValidateCanonicalRolePermissionsAsync(identityStore, cancellationToken);

        var users = await identityStore.ListUsersAsync(cancellationToken);
        if (users.Count > 0)
        {
            return;
        }

        DomainGuard.AgainstEmpty(_seedOptions.Username, "Auth:SeedAdmin:Username");
        DomainGuard.AgainstEmpty(_seedOptions.Email, "Auth:SeedAdmin:Email");
        DomainGuard.AgainstEmpty(_seedOptions.Password, "Auth:SeedAdmin:Password");

        var admin = new UserAccount(
            Guid.NewGuid().ToString("D"),
            _seedOptions.Username.Trim().ToLowerInvariant(),
            _seedOptions.Email.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(_seedOptions.DisplayName) ? "Administrador CMMS" : _seedOptions.DisplayName.Trim(),
            true,
            false,
            _passwordHasher.Hash(_seedOptions.Password),
            [AuthRoles.Admin],
            _seedOptions.Faenas,
            DateTimeOffset.UtcNow,
            null);

        await identityStore.UpsertUserAsync(admin, cancellationToken);
    }
}