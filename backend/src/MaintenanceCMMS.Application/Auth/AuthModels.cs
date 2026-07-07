using System.Security.Claims;

namespace MaintenanceCMMS.Application.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    CurrentUserResponse User);

public sealed record CurrentUserResponse(
    string Id,
    string Username,
    string Email,
    string DisplayName,
    bool IsActive,
    bool IsLocked,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> Faenas);

public sealed record CreateUserRequest(
    string Username,
    string Email,
    string DisplayName,
    string Password,
    IReadOnlyCollection<string>? Roles,
    IReadOnlyCollection<string>? Faenas,
    bool IsActive = true);

public sealed record UpdateUserRequest(
    string Email,
    string DisplayName,
    bool IsActive);

public sealed record AssignUserRolesRequest(IReadOnlyCollection<string> Roles);

public sealed record AssignUserFaenasRequest(IReadOnlyCollection<string> Faenas);

public sealed record UserAccount(
    string Id,
    string Username,
    string Email,
    string DisplayName,
    bool IsActive,
    bool IsLocked,
    string PasswordHash,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Faenas,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);

public sealed record RoleDefinition(
    string Code,
    string Name,
    string Type,
    IReadOnlyCollection<string> Permissions);

public sealed record ExternalIdentityResult(
    string ExternalId,
    string Username,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Groups);

public sealed record WorkOrderAccessContext(
    string NumeroOt,
    string FaenaCodigo,
    IReadOnlyCollection<string> AssignedUserIds);

public sealed record UserAccessContext(
    string UserId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> Faenas)
{
    public static UserAccessContext From(CurrentUserResponse user)
    {
        return new UserAccessContext(user.Id, user.Roles, user.Permissions, user.Faenas);
    }

    public static UserAccessContext FromClaims(ClaimsPrincipal user)
    {
        return new UserAccessContext(
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            user.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray(),
            user.FindAll("permission").Select(claim => claim.Value).ToArray(),
            user.FindAll("faena").Select(claim => claim.Value).ToArray());
    }
}
