using System.Security.Claims;
using MaintenanceCMMS.Application.Auditing;

namespace MaintenanceCMMS.Application.Auth;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(ClaimsPrincipal user, CancellationToken cancellationToken);

    Task<CurrentUserResponse> GetCurrentUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken);
}

public interface IUserManagementService
{
    Task<IReadOnlyList<CurrentUserResponse>> ListAsync(CancellationToken cancellationToken);

    Task<CurrentUserResponse?> GetByIdAsync(string id, CancellationToken cancellationToken);

    Task<CurrentUserResponse> CreateAsync(CreateUserRequest request, string actorUserId, CancellationToken cancellationToken);

    Task<CurrentUserResponse?> UpdateAsync(string id, UpdateUserRequest request, string actorUserId, CancellationToken cancellationToken);

    Task<CurrentUserResponse?> AssignRolesAsync(string id, IReadOnlyCollection<string> roles, string actorUserId, CancellationToken cancellationToken);

    Task<CurrentUserResponse?> AssignFaenasAsync(string id, IReadOnlyCollection<string> faenas, string actorUserId, CancellationToken cancellationToken);

    Task<CurrentUserResponse?> LockAsync(string id, string actorUserId, CancellationToken cancellationToken);

    Task<CurrentUserResponse?> UnlockAsync(string id, string actorUserId, CancellationToken cancellationToken);
}

public interface IIdentityStore
{
    Task<IReadOnlyList<UserAccount>> ListUsersAsync(CancellationToken cancellationToken);

    Task<UserAccount?> FindUserByIdAsync(string id, CancellationToken cancellationToken);

    Task<UserAccount?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken);

    Task UpsertUserAsync(UserAccount user, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleDefinition>> ListRolesAsync(CancellationToken cancellationToken);

    Task UpsertRolesAsync(IReadOnlyCollection<RoleDefinition> roles, CancellationToken cancellationToken);
}

public interface IIdentitySeedService
{
    Task SeedAsync(CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

public interface IJwtTokenService
{
    LoginResponse CreateToken(UserAccount user, IReadOnlyCollection<string> permissions);
}

public interface IAuthorizationPolicyService
{
    bool CanAccessWorkOrder(UserAccessContext user, WorkOrderAccessContext workOrder);

    bool CanViewFaena(UserAccessContext user, string faenaCodigo);

    bool CanViewWarehouses(UserAccessContext user);

    bool CanViewCosts(UserAccessContext user);

    bool CanAdminister(UserAccessContext user);

    bool CanImport(UserAccessContext user);

    bool CanDeactivateFaena(UserAccessContext user);

    bool CanChangeAssetFaena(UserAccessContext user);

    bool CanManageTechnicalHierarchy(UserAccessContext user);

    bool CanManageDocuments(UserAccessContext user);

    bool CanValidateDocuments(UserAccessContext user);

    bool CanConfigureDocumentTypes(UserAccessContext user);

    bool CanChangeValidatedDocumentExpiry(UserAccessContext user);

    bool CanManageAlerts(UserAccessContext user);

    bool CanConfigureAlerts(UserAccessContext user);

    bool CanAdjustStock(UserAccessContext user);

    bool CanCloseWorkOrder(UserAccessContext user);

    bool CanFinalValidateWorkOrder(UserAccessContext user);
}

public interface IExternalIdentityProvider
{
    Task<ExternalIdentityResult?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);
}
