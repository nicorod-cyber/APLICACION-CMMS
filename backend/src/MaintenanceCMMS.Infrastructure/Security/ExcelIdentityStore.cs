using MaintenanceCMMS.Application.Abstractions.Data;
using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Infrastructure.Security;

public sealed class ExcelIdentityStore : IIdentityStore
{
    private const string UsersSchema = "usuarios";
    private const string RolesSchema = "roles";

    private readonly IDataProvider _dataProvider;

    public ExcelIdentityStore(IDataProvider dataProvider)
    {
        _dataProvider = dataProvider;
    }

    public async Task<IReadOnlyList<UserAccount>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(UsersSchema, cancellationToken);

        return rows
            .Select(MapUser)
            .Where(user => !string.IsNullOrWhiteSpace(user.Username))
            .OrderBy(user => user.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<UserAccount?> FindUserByIdAsync(string id, CancellationToken cancellationToken)
    {
        var users = await ListUsersAsync(cancellationToken);
        return users.FirstOrDefault(user => string.Equals(user.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<UserAccount?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = Normalize(username);
        var users = await ListUsersAsync(cancellationToken);

        return users.FirstOrDefault(user =>
            string.Equals(user.Username, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, normalized, StringComparison.OrdinalIgnoreCase));
    }

    public async Task UpsertUserAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var users = (await ListUsersAsync(cancellationToken)).ToList();
        var index = users.FindIndex(item => string.Equals(item.Id, user.Id, StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            users[index] = user;
        }
        else
        {
            users.Add(user);
        }

        await _dataProvider.SaveRowsAsync(UsersSchema, users.Select(MapUserRow).ToArray(), cancellationToken);
    }

    public async Task<IReadOnlyList<RoleDefinition>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var rows = await _dataProvider.ReadRowsAsync(RolesSchema, cancellationToken);

        return rows
            .Select(row => new RoleDefinition(
                Normalize(row.GetValue("Codigo")),
                row.GetValue("Nombre")?.Trim() ?? string.Empty,
                row.GetValue("TipoRol")?.Trim() ?? string.Empty,
                SplitList(row.GetValue("Permisos"))))
            .Where(role => !string.IsNullOrWhiteSpace(role.Code))
            .OrderBy(role => role.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task UpsertRolesAsync(IReadOnlyCollection<RoleDefinition> roles, CancellationToken cancellationToken)
    {
        var existing = (await ListRolesAsync(cancellationToken)).ToDictionary(
            role => role.Code,
            role => role,
            StringComparer.OrdinalIgnoreCase);

        foreach (var role in roles)
        {
            existing[role.Code] = role;
        }

        var rows = existing.Values
            .OrderBy(role => role.Code, StringComparer.OrdinalIgnoreCase)
            .Select(role => new DataRow(new Dictionary<string, string?>
            {
                ["Codigo"] = role.Code,
                ["Nombre"] = role.Name,
                ["TipoRol"] = role.Type,
                ["Permisos"] = JoinList(role.Permissions)
            }))
            .ToArray();

        await _dataProvider.SaveRowsAsync(RolesSchema, rows, cancellationToken);
    }

    private static UserAccount MapUser(DataRow row)
    {
        var id = row.GetValue("UserId")?.Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            id = Guid.NewGuid().ToString("D");
        }

        var username = Normalize(row.GetValue("Username"));
        var email = Normalize(row.GetValue("Email"));
        if (string.IsNullOrWhiteSpace(username))
        {
            username = email;
        }

        return new UserAccount(
            id,
            username,
            email,
            row.GetValue("Nombre")?.Trim() ?? username,
            ParseBool(row.GetValue("Activo"), defaultValue: true),
            ParseBool(row.GetValue("Locked"), defaultValue: false),
            row.GetValue("PasswordHash")?.Trim() ?? string.Empty,
            SplitList(row.GetValue("Roles")),
            SplitList(row.GetValue("Faenas")),
            ParseDate(row.GetValue("CreatedAtUtc")) ?? DateTimeOffset.UtcNow,
            ParseDate(row.GetValue("UpdatedAtUtc")));
    }

    private static DataRow MapUserRow(UserAccount user)
    {
        return new DataRow(new Dictionary<string, string?>
        {
            ["UserId"] = user.Id,
            ["Username"] = Normalize(user.Username),
            ["Email"] = Normalize(user.Email),
            ["Nombre"] = user.DisplayName,
            ["Activo"] = user.IsActive ? "true" : "false",
            ["Locked"] = user.IsLocked ? "true" : "false",
            ["PasswordHash"] = user.PasswordHash,
            ["Faenas"] = JoinList(user.Faenas),
            ["Roles"] = JoinList(user.Roles),
            ["CreatedAtUtc"] = user.CreatedAtUtc.UtcDateTime.ToString("O"),
            ["UpdatedAtUtc"] = user.UpdatedAtUtc?.UtcDateTime.ToString("O")
        });
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static IReadOnlyCollection<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string JoinList(IReadOnlyCollection<string> values)
    {
        return string.Join(';', values.OrderBy(value => value, StringComparer.OrdinalIgnoreCase));
    }

    private static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals("si", StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var result) ? result : null;
    }
}
