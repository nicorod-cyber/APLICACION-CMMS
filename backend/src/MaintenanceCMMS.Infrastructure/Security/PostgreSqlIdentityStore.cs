using MaintenanceCMMS.Application.Auth;
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
        foreach (var roleDefinition in roles)
        {
            var roleCode = Normalize(roleDefinition.Code);
            var role = await _dbContext.Roles
                .Include(item => item.Permissions)
                .FirstOrDefaultAsync(item => item.Code == roleCode, cancellationToken);

            if (role is null)
            {
                role = new RoleEntity { Code = roleCode };
                _dbContext.Roles.Add(role);
            }
            else
            {
                role.UpdatedAtUtc = DateTimeOffset.UtcNow;
            }

            role.Name = roleDefinition.Name;
            role.Type = roleDefinition.Type;
            role.IsActive = true;

            var permissions = string.Equals(roleCode, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase)
                ? roleDefinition.Permissions.Append(AuthPermissions.ManageEquipmentFamilies)
                : roleDefinition.Permissions;

            foreach (var permissionCode in permissions.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var normalizedPermission = Normalize(permissionCode);
                var permission = _dbContext.Permissions.Local.FirstOrDefault(item => item.Code == normalizedPermission)
                    ?? await _dbContext.Permissions.FirstOrDefaultAsync(item => item.Code == normalizedPermission, cancellationToken);
                if (permission is null)
                {
                    permission = new PermissionEntity
                    {
                        Code = normalizedPermission,
                        Name = normalizedPermission,
                        IsActive = true
                    };
                    _dbContext.Permissions.Add(permission);
                }

                if (!role.Permissions.Any(item => item.PermissionId == permission.Id && item.IsActive))
                {
                    role.Permissions.Add(new RolePermissionEntity { Role = role, Permission = permission, IsActive = true });
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

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
                faena = new FaenaEntity { Code = faenaCode, Name = faenaCode, IsActive = true };
                _dbContext.Faenas.Add(faena);
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
