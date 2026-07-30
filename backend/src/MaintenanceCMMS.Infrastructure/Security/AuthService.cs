using System.Security.Claims;
using MaintenanceCMMS.Application.Auditing;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Security;

public sealed class AuthService : IAuthService
{
    private readonly IIdentityStore _identityStore;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditService _auditService;
    private readonly PasswordPolicyOptions _passwordPolicy;

    public AuthService(
        IIdentityStore identityStore,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        IAuditService auditService,
        IOptions<PasswordPolicyOptions> passwordPolicy)
    {
        _identityStore = identityStore;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _auditService = auditService;
        _passwordPolicy = passwordPolicy.Value;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = Normalize(request.Username);
        var user = await _identityStore.FindUserByUsernameAsync(username, cancellationToken);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await _auditService.RecordAsync(new AuditEventRequest(
                username,
                "auth.login_failed",
                AuditModules.Authentication,
                "User",
                username,
                Severity: AuditSeverity.Medium,
                Success: false,
                Detail: "Credenciales invalidas"), cancellationToken);

            throw new UnauthorizedAccessException("Usuario o clave invalidos.");
        }

        if (!user.IsActive || user.IsLocked)
        {
            await _auditService.RecordAsync(new AuditEventRequest(
                user.Id,
                "auth.login_failed",
                AuditModules.Authentication,
                "User",
                user.Id,
                Severity: AuditSeverity.High,
                Success: false,
                Detail: "Usuario bloqueado o inactivo"), cancellationToken);

            throw new InvalidOperationException("Usuario bloqueado o inactivo.");
        }

        var permissions = await ResolvePermissionsAsync(user.Roles, cancellationToken);
        var response = _jwtTokenService.CreateToken(user, permissions);

        await _auditService.RecordAsync(new AuditEventRequest(
            user.Id,
            "auth.login",
            AuditModules.Authentication,
            "User",
            user.Id,
            Severity: AuditSeverity.Low), cancellationToken);

        return response;
    }

    public async Task<LoginResponse> RefreshAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId)) throw new UnauthorizedAccessException("Token sin usuario.");
        var user = await _identityStore.FindUserByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive || user.IsLocked) throw new UnauthorizedAccessException("Usuario no disponible.");
        return _jwtTokenService.CreateToken(user, await ResolvePermissionsAsync(user.Roles, cancellationToken));
    }

    public async Task<bool> ChangeOwnPasswordAsync(
        string userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(userId, out _))
        {
            throw new UnauthorizedAccessException("Token sin usuario válido.");
        }

        EnsurePasswordProvided(request.CurrentPassword, "La contraseña actual es obligatoria.");
        EnsurePasswordProvided(request.NewPassword, "La nueva contraseña es obligatoria.");
        EnsurePasswordProvided(request.ConfirmNewPassword, "La confirmación de la nueva contraseña es obligatoria.");

        if (!string.Equals(request.NewPassword, request.ConfirmNewPassword, StringComparison.Ordinal))
        {
            throw new DomainException("La nueva contraseña y su confirmación no coinciden.");
        }

        var user = await _identityStore.FindUserByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (!user.IsActive || user.IsLocked)
        {
            throw new UnauthorizedAccessException("Usuario bloqueado o inactivo.");
        }

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new DomainException("La contraseña actual es incorrecta.");
        }

        if (_passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw new DomainException("La nueva contraseña debe ser diferente de la contraseña actual.");
        }

        PasswordPolicy.EnsureValid(request.NewPassword, _passwordPolicy.MinimumLength, _passwordPolicy.MaximumLength);

        var updated = user with
        {
            PasswordHash = _passwordHasher.Hash(request.NewPassword),
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await _identityStore.UpsertUserAsync(updated, cancellationToken);
        await _auditService.RecordAsync(new AuditEventRequest(
            userId,
            "auth.password_changed",
            AuditModules.Authentication,
            "User",
            userId,
            Severity: AuditSeverity.High,
            Reason: "Cambio de contraseña propio"), cancellationToken);

        return true;
    }
    public async Task LogoutAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
        await _auditService.RecordAsync(new AuditEventRequest(
            userId,
            "auth.logout",
            AuditModules.Authentication,
            "User",
            userId,
            Severity: AuditSeverity.Low), cancellationToken);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnauthorizedAccessException("Token sin usuario.");
        }

        var account = await _identityStore.FindUserByIdAsync(userId, cancellationToken);
        if (account is null || !account.IsActive || account.IsLocked)
        {
            throw new UnauthorizedAccessException("Usuario no disponible.");
        }

        var permissions = await ResolvePermissionsAsync(account.Roles, cancellationToken);
        return AuthResponseFactory.FromUser(account, permissions);
    }

    private static void EnsurePasswordProvided(string? password, string message)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new DomainException(message);
        }
    }
    private async Task<IReadOnlyCollection<string>> ResolvePermissionsAsync(
        IReadOnlyCollection<string> roles,
        CancellationToken cancellationToken)
    {
        var roleDefinitions = await _identityStore.ListRolesAsync(cancellationToken);
        return RolePermissionCatalog.ResolvePermissions(roles, roleDefinitions);
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
