using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;

namespace MaintenanceCMMS.Infrastructure.Security;

public sealed class UserManagementService : IUserManagementService
{
    private readonly IIdentityStore _identityStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditService _auditService;

    public UserManagementService(
        IIdentityStore identityStore,
        IPasswordHasher passwordHasher,
        IAuditService auditService)
    {
        _identityStore = identityStore;
        _passwordHasher = passwordHasher;
        _auditService = auditService;
    }

    public async Task<IReadOnlyList<CurrentUserResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var users = await _identityStore.ListUsersAsync(cancellationToken);
        return await MapUsersAsync(users, cancellationToken);
    }

    public async Task<CurrentUserResponse?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        var user = await _identityStore.FindUserByIdAsync(id, cancellationToken);
        return user is null ? null : await MapUserAsync(user, cancellationToken);
    }

    public async Task<CurrentUserResponse> CreateAsync(
        CreateUserRequest request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        ValidateUserInput(request.Username, request.Email, request.DisplayName);
        DomainGuard.AgainstEmpty(request.Password, nameof(request.Password));

        var existingUsername = await _identityStore.FindUserByUsernameAsync(request.Username, cancellationToken);
        if (existingUsername is not null)
        {
            throw new DomainException("Ya existe un usuario con ese username.");
        }

        var users = await _identityStore.ListUsersAsync(cancellationToken);
        if (users.Any(user => string.Equals(user.Email, Normalize(request.Email), StringComparison.OrdinalIgnoreCase)))
        {
            throw new DomainException("Ya existe un usuario con ese email.");
        }

        var user = new UserAccount(
            Guid.NewGuid().ToString("D"),
            Normalize(request.Username),
            Normalize(request.Email),
            request.DisplayName.Trim(),
            request.IsActive,
            false,
            _passwordHasher.Hash(request.Password),
            NormalizeRoles(request.Roles),
            NormalizeList(request.Faenas),
            DateTimeOffset.UtcNow,
            null);

        await _identityStore.UpsertUserAsync(user, cancellationToken);
        await _auditService.RecordAsync(new AuditEventRequest(
            actorUserId,
            "user.created",
            AuditModules.Users,
            "User",
            user.Id,
            NewValue: user.Username,
            Severity: AuditSeverity.High), cancellationToken);

        return await MapUserAsync(user, cancellationToken);
    }

    public async Task<CurrentUserResponse?> UpdateAsync(
        string id,
        UpdateUserRequest request,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var user = await _identityStore.FindUserByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        ValidateUserInput(user.Username, request.Email, request.DisplayName);
        var updated = user with
        {
            Email = Normalize(request.Email),
            DisplayName = request.DisplayName.Trim(),
            IsActive = request.IsActive,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _identityStore.UpsertUserAsync(updated, cancellationToken);
        await _auditService.RecordAsync(new AuditEventRequest(
            actorUserId,
            "user.updated",
            AuditModules.Users,
            "User",
            user.Id,
            PreviousValue: user.Email,
            NewValue: updated.Email,
            Severity: AuditSeverity.Medium), cancellationToken);

        return await MapUserAsync(updated, cancellationToken);
    }

    public async Task<CurrentUserResponse?> AssignRolesAsync(
        string id,
        IReadOnlyCollection<string> roles,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var user = await _identityStore.FindUserByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var normalizedRoles = NormalizeRoles(roles);
        var roleDefinitions = await _identityStore.ListRolesAsync(cancellationToken);
        var invalid = normalizedRoles
            .Where(role => roleDefinitions.All(definition => !string.Equals(definition.Code, role, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        if (invalid.Length > 0)
        {
            throw new DomainException($"Roles no registrados: {string.Join(", ", invalid)}.");
        }

        var updated = user with
        {
            Roles = normalizedRoles,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _identityStore.UpsertUserAsync(updated, cancellationToken);
        await _auditService.RecordAsync(new AuditEventRequest(
            actorUserId,
            "user.roles_changed",
            AuditModules.Users,
            "User",
            user.Id,
            PreviousValue: string.Join(';', user.Roles),
            NewValue: string.Join(';', updated.Roles),
            Severity: AuditSeverity.Critical,
            Reason: "Cambio de roles"), cancellationToken);

        return await MapUserAsync(updated, cancellationToken);
    }

    public async Task<CurrentUserResponse?> AssignFaenasAsync(
        string id,
        IReadOnlyCollection<string> faenas,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var user = await _identityStore.FindUserByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var updated = user with
        {
            Faenas = NormalizeList(faenas),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _identityStore.UpsertUserAsync(updated, cancellationToken);
        await _auditService.RecordAsync(new AuditEventRequest(
            actorUserId,
            "user.faenas_changed",
            AuditModules.Users,
            "User",
            user.Id,
            PreviousValue: string.Join(';', user.Faenas),
            NewValue: string.Join(';', updated.Faenas),
            Severity: AuditSeverity.High,
            Reason: "Cambio de faenas autorizadas"), cancellationToken);

        return await MapUserAsync(updated, cancellationToken);
    }

    public Task<CurrentUserResponse?> LockAsync(string id, string actorUserId, CancellationToken cancellationToken)
    {
        return SetLockStateAsync(id, true, actorUserId, cancellationToken);
    }

    public Task<CurrentUserResponse?> UnlockAsync(string id, string actorUserId, CancellationToken cancellationToken)
    {
        return SetLockStateAsync(id, false, actorUserId, cancellationToken);
    }

    private async Task<CurrentUserResponse?> SetLockStateAsync(
        string id,
        bool isLocked,
        string actorUserId,
        CancellationToken cancellationToken)
    {
        var user = await _identityStore.FindUserByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var updated = user with
        {
            IsLocked = isLocked,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _identityStore.UpsertUserAsync(updated, cancellationToken);
        await _auditService.RecordAsync(new AuditEventRequest(
            actorUserId,
            isLocked ? "user.locked" : "user.unlocked",
            AuditModules.Users,
            "User",
            user.Id,
            PreviousValue: user.IsLocked.ToString(),
            NewValue: updated.IsLocked.ToString(),
            Severity: AuditSeverity.High,
            Reason: isLocked ? "Bloqueo de usuario" : "Desbloqueo de usuario"), cancellationToken);

        return await MapUserAsync(updated, cancellationToken);
    }

    private async Task<IReadOnlyList<CurrentUserResponse>> MapUsersAsync(
        IReadOnlyCollection<UserAccount> users,
        CancellationToken cancellationToken)
    {
        var roleDefinitions = await _identityStore.ListRolesAsync(cancellationToken);

        return users
            .Select(user => AuthResponseFactory.FromUser(
                user,
                RolePermissionCatalog.ResolvePermissions(user.Roles, roleDefinitions)))
            .ToArray();
    }

    private async Task<CurrentUserResponse> MapUserAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var roleDefinitions = await _identityStore.ListRolesAsync(cancellationToken);
        return AuthResponseFactory.FromUser(user, RolePermissionCatalog.ResolvePermissions(user.Roles, roleDefinitions));
    }

    private static void ValidateUserInput(string username, string email, string displayName)
    {
        DomainGuard.AgainstEmpty(username, nameof(username));
        DomainGuard.AgainstEmpty(email, nameof(email));
        DomainGuard.AgainstEmpty(displayName, nameof(displayName));
    }

    private static IReadOnlyCollection<string> NormalizeRoles(IReadOnlyCollection<string>? roles)
    {
        var normalized = NormalizeList(roles);
        return normalized.Count == 0 ? [AuthRoles.FaenaViewer] : normalized;
    }

    private static IReadOnlyCollection<string> NormalizeList(IReadOnlyCollection<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        return values
            .Select(value => value.Trim().ToLowerInvariant())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }
}
