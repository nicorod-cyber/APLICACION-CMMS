using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;

namespace MaintenanceCMMS.Infrastructure.Security;

public sealed class PostgreSqlIdentityStore : IIdentityStore
{
    private readonly CmmsDbContext _dbContext;

    public PostgreSqlIdentityStore(CmmsDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<UserAccount>> ListUsersAsync(CancellationToken cancellationToken)
    {
        var users = await _dbContext.Users
            .AsNoTracking()
            .Include(user => user.Roles.Where(role => role.IsActive)).ThenInclude(role => role.Role)
            .Include(user => user.Faenas.Where(faena => faena.IsActive)).ThenInclude(faena => faena.Faena)
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);

        return users.Select(MapUser).ToArray();
    }

    public async Task<UserAccount?> FindUserByIdAsync(string id, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var userId))
        {
            return null;
        }

        var user = await QueryUsers().FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is null ? null : MapUser(user);
    }

    public async Task<UserAccount?> FindUserByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = Normalize(username);
        var user = await QueryUsers()
            .FirstOrDefaultAsync(item => item.Username == normalized || item.Email == normalized, cancellationToken);

        return user is null ? null : MapUser(user);
    }

    public async Task UpsertUserAsync(UserAccount user, CancellationToken cancellationToken)
    {
        var normalizedUsername = Normalize(user.Username);
        var normalizedEmail = Normalize(user.Email);
        var entity = Guid.TryParse(user.Id, out var parsedId)
            ? await _dbContext.Users
                .Include(item => item.Roles)
                .Include(item => item.Faenas)
                .FirstOrDefaultAsync(item => item.Id == parsedId, cancellationToken)
            : null;

        entity ??= await _dbContext.Users
            .Include(item => item.Roles)
            .Include(item => item.Faenas)
            .FirstOrDefaultAsync(item => item.Username == normalizedUsername || item.Email == normalizedEmail, cancellationToken);

        if (entity is null)
        {
            entity = new AppUserEntity
            {
                Id = Guid.TryParse(user.Id, out var id) ? id : Guid.NewGuid(),
                CreatedAtUtc = user.CreatedAtUtc
            };
            _dbContext.Users.Add(entity);
        }
        else
        {
            entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        entity.Username = normalizedUsername;
        entity.Email = normalizedEmail;
        entity.DisplayName = user.DisplayName;
        entity.IsActive = user.IsActive;
        entity.IsLocked = user.IsLocked;
        entity.PasswordHash = user.PasswordHash;

        await SyncUserRolesAsync(entity, user.Roles, cancellationToken);
        await SyncUserFaenasAsync(entity, user.Faenas, cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoleDefinition>> ListRolesAsync(CancellationToken cancellationToken)
    {
        var roles = await _dbContext.Roles
            .AsNoTracking()
            .Include(role => role.Permissions.Where(permission => permission.IsActive)).ThenInclude(permission => permission.Permission)
            .Where(role => role.IsActive)
            .OrderBy(role => role.Code)
            .ToListAsync(cancellationToken);

        return roles
            .Select(role => new RoleDefinition(role.Code, role.Name, role.Type, role.Permissions.Select(permission => permission.Permission.Code).Order().ToArray()))
            .ToArray();
    }

    public async Task UpsertRolesAsync(IReadOnlyCollection<RoleDefinition> roles, CancellationToken cancellationToken)
    {
        var definitions = roles
            .Select(definition => new SeedRoleDefinition(
                Normalize(definition.Code),
                definition.Name,
                definition.Type,
                RolePermissionCatalog.SeedPermissions(definition)
                    .Select(Normalize)
                    .Where(static permission => !string.IsNullOrWhiteSpace(permission))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)))
            .ToArray();

        var roleCodes = definitions.Select(definition => definition.Code).ToArray();
        var permissionCodes = definitions.SelectMany(definition => definition.PermissionCodes).ToArray();

        var existingRoles = await _dbContext.Roles
            .Include(role => role.Permissions)
                .ThenInclude(permission => permission.Permission)
            .Where(role => roleCodes.Contains(role.Code))
            .ToListAsync(cancellationToken);
        var rolesByCode = existingRoles.ToDictionary(role => role.Code, StringComparer.OrdinalIgnoreCase);

        var existingPermissions = await _dbContext.Permissions
            .Where(permission => permissionCodes.Contains(permission.Code))
            .ToListAsync(cancellationToken);
        var permissionsByCode = existingPermissions.ToDictionary(permission => permission.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            var now = DateTimeOffset.UtcNow;
            var isNewRole = !rolesByCode.TryGetValue(definition.Code, out var role);
            if (isNewRole)
            {
                role = new RoleEntity
                {
                    Code = definition.Code,
                    Name = definition.Name,
                    Type = definition.Type,
                    IsActive = true
                };
                _dbContext.Roles.Add(role);
                rolesByCode.Add(role.Code, role);
            }
            else
            {
                var roleChanged = !string.Equals(role!.Name, definition.Name, StringComparison.Ordinal)
                    || !string.Equals(role.Type, definition.Type, StringComparison.Ordinal)
                    || !role.IsActive;

                if (roleChanged)
                {
                    role.Name = definition.Name;
                    role.Type = definition.Type;
                    role.IsActive = true;
                    role.UpdatedAtUtc = now;
                }
            }

            var permissionsChanged = false;
            foreach (var permissionCode in definition.PermissionCodes)
            {
                if (!permissionsByCode.TryGetValue(permissionCode, out var permission))
                {
                    permission = new PermissionEntity
                    {
                        Code = permissionCode,
                        Name = permissionCode,
                        IsActive = true
                    };
                    _dbContext.Permissions.Add(permission);
                    permissionsByCode.Add(permission.Code, permission);
                }
                else if (!permission.IsActive)
                {
                    permission.IsActive = true;
                    permission.UpdatedAtUtc = now;
                }

                var rolePermission = role!.Permissions.FirstOrDefault(item => item.PermissionId == permission.Id && item.IsActive);
                if (rolePermission is null)
                {
                    rolePermission = role.Permissions.FirstOrDefault(item => item.PermissionId == permission.Id && !item.IsActive);
                    if (rolePermission is null)
                    {
                        _dbContext.RolePermissions.Add(new RolePermissionEntity
                        {
                            Role = role,
                            Permission = permission,
                            IsActive = true
                        });
                    }
                    else
                    {
                        rolePermission.IsActive = true;
                        rolePermission.UpdatedAtUtc = now;
                    }

                    permissionsChanged = true;
                }
            }

            foreach (var rolePermission in role!.Permissions.Where(item => item.IsActive && !definition.PermissionCodes.Contains(item.Permission.Code)).ToArray())
            {
                rolePermission.IsActive = false;
                rolePermission.UpdatedAtUtc = now;
                permissionsChanged = true;
            }

            if (!isNewRole && permissionsChanged)
            {
                role.UpdatedAtUtc = now;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed record SeedRoleDefinition(
        string Code,
        string Name,
        string Type,
        HashSet<string> PermissionCodes);

    private IQueryable<AppUserEntity> QueryUsers()
    {
        return _dbContext.Users
            .AsNoTracking()
            .Include(user => user.Roles.Where(role => role.IsActive)).ThenInclude(role => role.Role)
            .Include(user => user.Faenas.Where(faena => faena.IsActive)).ThenInclude(faena => faena.Faena);
    }

    private async Task SyncUserRolesAsync(AppUserEntity user, IReadOnlyCollection<string> roleCodes, CancellationToken cancellationToken)
    {
        var activeCodes = roleCodes.Select(Normalize).Where(code => !string.IsNullOrWhiteSpace(code)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in user.Roles)
        {
            existing.IsActive = activeCodes.Contains(existing.Role.Code);
            if (!existing.IsActive)
            {
                existing.UnassignedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        foreach (var roleCode in activeCodes)
        {
            var role = await _dbContext.Roles.FirstOrDefaultAsync(item => item.Code == roleCode, cancellationToken);
            if (role is null)
            {
                continue;
            }

            if (!user.Roles.Any(item => item.RoleId == role.Id && item.IsActive))
            {
                user.Roles.Add(new UserRoleEntity { User = user, Role = role, IsActive = true });
            }
        }
    }

    private async Task SyncUserFaenasAsync(AppUserEntity user, IReadOnlyCollection<string> faenaCodes, CancellationToken cancellationToken)
    {
        var activeCodes = faenaCodes.Select(NormalizeCode).Where(code => !string.IsNullOrWhiteSpace(code)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var existing in user.Faenas)
        {
            existing.IsActive = activeCodes.Contains(existing.Faena.Code);
            if (!existing.IsActive)
            {
                existing.UnassignedAtUtc = DateTimeOffset.UtcNow;
            }
        }

        foreach (var faenaCode in activeCodes)
        {
            var faena = await _dbContext.Faenas.FirstOrDefaultAsync(item => item.Code == faenaCode, cancellationToken);
            if (faena is null)
            {
                throw new DomainException($"La faena '{faenaCode}' no existe. Debe crearse con su responsable y ubicaci?n t?cnica.");
            }

            if (!user.Faenas.Any(item => item.FaenaId == faena.Id && item.IsActive))
            {
                user.Faenas.Add(new UserFaenaEntity { User = user, Faena = faena, IsActive = true });
            }
        }
    }

    private static UserAccount MapUser(AppUserEntity user)
    {
        return new UserAccount(
            user.Id.ToString("D"),
            user.Username,
            user.Email,
            user.DisplayName,
            user.IsActive,
            user.IsLocked,
            user.PasswordHash,
            user.Roles.Where(role => role.IsActive).Select(role => role.Role.Code).Order().ToArray(),
            user.Faenas.Where(faena => faena.IsActive).Select(faena => faena.Faena.Code).Order().ToArray(),
            user.CreatedAtUtc,
            user.UpdatedAtUtc);
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private static string NormalizeCode(string? value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }
}
