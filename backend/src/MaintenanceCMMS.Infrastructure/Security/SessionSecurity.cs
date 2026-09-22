using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Infrastructure.Options;

namespace MaintenanceCMMS.Infrastructure.Security;

public static class SessionSecurity
{
    public const string StampClaim = "session_stamp";

    public static void ValidateOptions(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret) || Encoding.UTF8.GetByteCount(options.Secret) < 32 ||
            string.IsNullOrWhiteSpace(options.Issuer) || string.IsNullOrWhiteSpace(options.Audience) ||
            options.ExpirationMinutes is < 1 or > 1440)
            throw new InvalidOperationException("Configure Jwt:Secret (minimo 32 bytes), Issuer, Audience y ExpirationMinutes (1-1440).");
    }

    public static string Stamp(UserAccount user, JwtOptions options)
    {
        var state = JsonSerializer.Serialize(new { user.Id, user.PasswordHash,
            Updated = user.UpdatedAtUtc?.UtcTicks.ToString(CultureInfo.InvariantCulture), user.IsActive, user.IsLocked,
            Roles = user.Roles.Order(StringComparer.OrdinalIgnoreCase), Faenas = user.Faenas.Order(StringComparer.OrdinalIgnoreCase) });
        return Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(options.Secret), Encoding.UTF8.GetBytes(state)));
    }

    public static async Task<bool> ValidateAsync(ClaimsPrincipal principal, IIdentityStore store, JwtOptions options, CancellationToken ct)
    {
        var account = await store.FindUserByIdAsync(principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty, ct);
        var supplied = principal.FindFirst(StampClaim)?.Value;
        if (account is null || !account.IsActive || account.IsLocked || supplied is null ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(supplied), Encoding.UTF8.GetBytes(Stamp(account, options)))) return false;
        var permissions = RolePermissionCatalog.ResolvePermissions(account.Roles, await store.ListRolesAsync(ct));
        return permissions.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(principal.FindAll("permission").Select(c => c.Value));
    }
}
