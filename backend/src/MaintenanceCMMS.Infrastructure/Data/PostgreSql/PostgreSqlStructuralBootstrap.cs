using MaintenanceCMMS.Application.Auth;
using MaintenanceCMMS.Application.Inventory;
using MaintenanceCMMS.Application.WorkNotifications;
using MaintenanceCMMS.Application.WorkOrders;
using MaintenanceCMMS.Domain.Common;
using MaintenanceCMMS.Domain.Enums;
using MaintenanceCMMS.Infrastructure.Data.PostgreSql.Entities;
using MaintenanceCMMS.Infrastructure.Options;
using MaintenanceCMMS.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MaintenanceCMMS.Infrastructure.Data.PostgreSql;

public interface IPostgreSqlStructuralBootstrap
{
    Task BootstrapAsync(CancellationToken cancellationToken);
}

public sealed class PostgreSqlStructuralBootstrap : IPostgreSqlStructuralBootstrap
{
    private const long BootstrapLockKey = 7_144_260_118_247_903_412;
    private readonly CmmsDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly AuthSeedOptions _admin;

    public PostgreSqlStructuralBootstrap(CmmsDbContext db, IPasswordHasher hasher, IOptions<AuthSeedOptions> admin)
    {
        _db = db;
        _hasher = hasher;
        _admin = admin.Value;
    }

    public async Task BootstrapAsync(CancellationToken ct)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(ct);
        await _db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({BootstrapLockKey});", ct);
        var roles = await EnsureIdentityAsync(ct);
        await EnsureStatesAsync(ct);
        await EnsureWorkCatalogsAsync(ct);
        await EnsureInventoryCatalogsAsync(ct);
        await EnsureAdministratorAsync(roles, ct);
        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Dictionary<string, RoleEntity>> EnsureIdentityAsync(CancellationToken ct)
    {
        var definitions = RolePermissionCatalog.InitialRoles.Select(definition => new
        {
            Code = Normalize(definition.Code), definition.Name, definition.Type,
            Permissions = RolePermissionCatalog.SeedPermissions(definition).Select(Normalize).ToArray()
        }).ToArray();
        var roleCodes = definitions.Select(item => item.Code).ToArray();
        var permissionCodes = definitions.SelectMany(item => item.Permissions).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var roles = await _db.Roles.Include(role => role.Permissions).ThenInclude(item => item.Permission)
            .Where(role => roleCodes.Contains(role.Code)).ToListAsync(ct);
        var rolesByCode = roles.ToDictionary(role => role.Code, StringComparer.OrdinalIgnoreCase);
        var permissions = await _db.Permissions.Where(permission => permissionCodes.Contains(permission.Code)).ToListAsync(ct);
        var permissionsByCode = permissions.ToDictionary(permission => permission.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var definition in definitions)
        {
            if (!rolesByCode.TryGetValue(definition.Code, out var role))
            {
                role = new RoleEntity { Code = definition.Code, Name = definition.Name, Type = definition.Type, IsActive = true };
                _db.Roles.Add(role);
                rolesByCode.Add(role.Code, role);
            }
            foreach (var permissionCode in definition.Permissions)
            {
                if (!permissionsByCode.TryGetValue(permissionCode, out var permission))
                {
                    permission = new PermissionEntity { Code = permissionCode, Name = permissionCode, IsActive = true };
                    _db.Permissions.Add(permission);
                    permissionsByCode.Add(permission.Code, permission);
                }
                if (!role.Permissions.Any(item => string.Equals(item.Permission.Code, permissionCode, StringComparison.OrdinalIgnoreCase)))
                    role.Permissions.Add(new RolePermissionEntity { Role = role, Permission = permission, IsActive = true });
            }
        }
        return rolesByCode;
    }

    private async Task EnsureAdministratorAsync(IReadOnlyDictionary<string, RoleEntity> roles, CancellationToken ct)
    {
        if (await _db.Users.AnyAsync(ct)) return;
        DomainGuard.AgainstEmpty(_admin.Username, "Auth:SeedAdmin:Username");
        DomainGuard.AgainstEmpty(_admin.Email, "Auth:SeedAdmin:Email");
        DomainGuard.AgainstEmpty(_admin.Password, "Auth:SeedAdmin:Password");
        var user = new AppUserEntity
        {
            Username = Normalize(_admin.Username), Email = Normalize(_admin.Email),
            DisplayName = string.IsNullOrWhiteSpace(_admin.DisplayName) ? "Administrador CMMS" : _admin.DisplayName.Trim(),
            IsActive = true, PasswordHash = _hasher.Hash(_admin.Password)
        };
        user.Roles.Add(new UserRoleEntity { User = user, Role = roles[Normalize(AuthRoles.Admin)], IsActive = true });
        _db.Users.Add(user);
    }

    private async Task EnsureStatesAsync(CancellationToken ct)
    {
        var definitions = new[]
        {
            ("OPERATIVO_FAENA", "Operativo en Faena"), ("ALERTA_FAENA", "Con alerta en Faena"),
            ("FUERA_SERVICIO_FAENA", "Fuera de servicio en Faena"), ("FUERA_SERVICIO_TALLER", "Fuera de servicio en Taller")
        };
        var codes = definitions.Select(item => item.Item1).ToArray();
        var existing = await _db.AssetOperationalStates.Where(item => codes.Contains(item.Code)).Select(item => item.Code).ToListAsync(ct);
        foreach (var definition in definitions.Where(item => !existing.Contains(item.Item1, StringComparer.OrdinalIgnoreCase)))
            _db.AssetOperationalStates.Add(new AssetOperationalStateEntity { Code = definition.Item1, Name = definition.Item2, IsActive = true });
    }

    private async Task EnsureWorkCatalogsAsync(CancellationToken ct)
    {
        var definitions = new List<(string Category, string Code, int SortOrder)>();
        AddEnum<WorkNotificationType>(definitions, "WorkNotificationType");
        AddEnum<WorkNotificationStatus>(definitions, "WorkNotificationStatus");
        AddEnum<WorkNotificationPriority>(definitions, "WorkNotificationPriority");
        AddEnum<WorkNotificationCriticality>(definitions, "WorkNotificationCriticality");
        AddEnum<WorkFailureClassification>(definitions, "WorkFailureClassification");
        AddEnum<WorkOrderLifecycleStatus>(definitions, "WorkOrderLifecycleStatus");
        AddEnum<WorkOrderTaskStatus>(definitions, "WorkOrderTaskStatus");
        AddEnum<WorkOrderSparePartStatus>(definitions, "WorkOrderSparePartStatus");
        AddEnum<WorkOrderEvidenceType>(definitions, "WorkOrderEvidenceType");
        AddEnum<WorkOrderChecklistResponseType>(definitions, "WorkOrderChecklistResponseType");
        AddEnum<MaintenanceType>(definitions, "MaintenanceType");
        var categories = definitions.Select(item => item.Category).Distinct().ToArray();
        var existing = await _db.WorkCatalogs.Where(item => categories.Contains(item.Category)).Select(item => new { item.Category, item.Code }).ToListAsync(ct);
        foreach (var definition in definitions.Where(definition => !existing.Any(item => item.Category == definition.Category && item.Code == definition.Code)))
            _db.WorkCatalogs.Add(new WorkCatalogEntity { Category = definition.Category, Code = definition.Code, Name = definition.Code, IsActive = true, SortOrder = definition.SortOrder });
    }

    private async Task EnsureInventoryCatalogsAsync(CancellationToken ct)
    {
        var definitions = new List<(string Category, string Code, string Name, int SortOrder)> { ("Unit", "UN", "Unidad", 1) };
        AddInventoryEnum<WarehouseType>(definitions, "WarehouseType");
        AddInventoryEnum<StockMovementType>(definitions, "MovementType");
        var categories = definitions.Select(item => item.Category).Distinct().ToArray();
        var existing = await _db.InventoryCatalogs.Where(item => categories.Contains(item.Category)).Select(item => new { item.Category, item.Code }).ToListAsync(ct);
        foreach (var definition in definitions.Where(definition => !existing.Any(item => item.Category == definition.Category && item.Code == definition.Code)))
            _db.InventoryCatalogs.Add(new InventoryCatalogEntity { Category = definition.Category, Code = definition.Code, Name = definition.Name, IsActive = true, SortOrder = definition.SortOrder });
    }

    private static void AddEnum<TEnum>(ICollection<(string Category, string Code, int SortOrder)> target, string category) where TEnum : struct, Enum
    {
        var order = 1;
        foreach (var value in Enum.GetNames<TEnum>()) target.Add((category, value, order++));
    }

    private static void AddInventoryEnum<TEnum>(ICollection<(string Category, string Code, string Name, int SortOrder)> target, string category) where TEnum : struct, Enum
    {
        var order = 1;
        foreach (var value in Enum.GetNames<TEnum>()) target.Add((category, value.ToUpperInvariant(), value, order++));
    }

    private static string Normalize(string? value) => value?.Trim().ToLowerInvariant() ?? string.Empty;
}