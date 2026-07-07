using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Infrastructure.Security;

internal static class AuthResponseFactory
{
    public static CurrentUserResponse FromUser(UserAccount user, IReadOnlyCollection<string> permissions)
    {
        return new CurrentUserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.IsLocked,
            user.Roles,
            permissions,
            user.Faenas);
    }
}
