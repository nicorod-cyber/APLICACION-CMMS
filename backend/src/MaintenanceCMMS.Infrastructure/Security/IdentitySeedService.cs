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

    public IdentitySeedService(
        IIdentityStore identityStore,
        IPasswordHasher passwordHasher,
        IOptions<AuthSeedOptions> seedOptions)
    {
        _identityStore = identityStore;
        _passwordHasher = passwordHasher;
        _seedOptions = seedOptions.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await _identityStore.UpsertRolesAsync(RolePermissionCatalog.InitialRoles, cancellationToken);

        var users = await _identityStore.ListUsersAsync(cancellationToken);
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

        await _identityStore.UpsertUserAsync(admin, cancellationToken);
    }
}
