using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Infrastructure.Security;

internal static class RolePermissionCatalog
{
    public static IReadOnlyCollection<RoleDefinition> InitialRoles =>
    [
        Role(AuthRoles.Admin, "Administrador", "Administrator",
            AuthPermissions.Administration,
            AuthPermissions.ManageUsers,
            AuthPermissions.AssignFaenas,
            AuthPermissions.ViewFaenas,
            AuthPermissions.CreateFaenas,
            AuthPermissions.EditFaenas,
            AuthPermissions.DeactivateFaenas,
            AuthPermissions.ApproveImports,
            AuthPermissions.ChangeAssetFaena,
            AuthPermissions.ManageAssetCatalogs,
            AuthPermissions.ManageAssetAttributes,
            AuthPermissions.RegisterAssetReadings,
            AuthPermissions.CorrectAssetReadings,
            AuthPermissions.ViewOperationalUnits,
            AuthPermissions.ManageOperationalUnits,
            AuthPermissions.ManageOperationalUnitComposition,
            AuthPermissions.ManageDocumentRequirements,
            AuthPermissions.ManageTechnicalHierarchy,
            AuthPermissions.ManageDocuments,
            AuthPermissions.UploadDocuments,
            AuthPermissions.ConfigureDocumentTypes,
            AuthPermissions.ChangeValidatedDocumentExpiry,
            AuthPermissions.ManageAlerts,
            AuthPermissions.ConfigureAlerts,
            AuthPermissions.AdjustStock,
            AuthPermissions.CreateWorkOrders,
            AuthPermissions.AssignWorkOrderSupervisor,
            AuthPermissions.SendWorkOrderToSupervisor,
            AuthPermissions.ManageWorkOrderTasks,
            AuthPermissions.ManageWorkOrderTechnicians,
            AuthPermissions.ExecuteAssignedWorkOrders,
            AuthPermissions.RegisterWorkOrderLabor,
            AuthPermissions.ReviewWorkOrderLabor,
            AuthPermissions.RegisterWorkOrderEvidence,
            AuthPermissions.SignWorkOrders,
            AuthPermissions.ReviewWorkOrderTasks,
            AuthPermissions.CloseWorkOrders,
            AuthPermissions.CreateWorkOrders,
            AuthPermissions.AssignWorkOrderSupervisor,
            AuthPermissions.SendWorkOrderToSupervisor,
            AuthPermissions.FinalValidateWorkOrders,
            AuthPermissions.ViewCosts,
            AuthPermissions.ViewGlobalWarehouses),

        Role(AuthRoles.Planner, "Planificador", "Planner",
            AuthPermissions.ApproveImports,
            AuthPermissions.ViewFaenas,
            AuthPermissions.CreateFaenas,
            AuthPermissions.EditFaenas,
            AuthPermissions.ChangeAssetFaena,
            AuthPermissions.ManageAssetAttributes,
            AuthPermissions.RegisterAssetReadings,
            AuthPermissions.CorrectAssetReadings,
            AuthPermissions.ViewOperationalUnits,
            AuthPermissions.ManageOperationalUnits,
            AuthPermissions.ManageOperationalUnitComposition,
            AuthPermissions.ManageDocumentRequirements,
            AuthPermissions.ManageTechnicalHierarchy,
            AuthPermissions.ManageDocuments,
            AuthPermissions.UploadDocuments,
            AuthPermissions.ReviewDocuments,
            AuthPermissions.ValidateDocuments,
            AuthPermissions.RejectDocuments,
            AuthPermissions.ChangeValidatedDocumentExpiry,
            AuthPermissions.ManageAlerts,
            AuthPermissions.CreateWorkOrders,
            AuthPermissions.AssignWorkOrderSupervisor,
            AuthPermissions.SendWorkOrderToSupervisor,
            AuthPermissions.FinalValidateWorkOrders,
            AuthPermissions.ViewCosts),

        Role(AuthRoles.MaintenanceSupervisor, "Supervisor mantenimiento", "Supervisor",
            AuthPermissions.RegisterAssetReadings,
            AuthPermissions.CorrectAssetReadings,
            AuthPermissions.ViewOperationalUnits,
            AuthPermissions.ManageOperationalUnits,
            AuthPermissions.ManageOperationalUnitComposition,
            AuthPermissions.ManageDocuments,
            AuthPermissions.UploadDocuments,
            AuthPermissions.ManageAlerts,
            AuthPermissions.ManageWorkOrderTasks,
            AuthPermissions.ManageWorkOrderTechnicians,
            AuthPermissions.ReviewWorkOrderLabor,
            AuthPermissions.ReviewWorkOrderTasks,
            AuthPermissions.CloseWorkOrders),

        Role(AuthRoles.Technician, "Tecnico", "Technician",
            AuthPermissions.ViewAssignedWorkOrders,
            AuthPermissions.ExecuteAssignedWorkOrders,
            AuthPermissions.RegisterWorkOrderLabor,
            AuthPermissions.RegisterWorkOrderEvidence,
            AuthPermissions.SignWorkOrders,
            AuthPermissions.RegisterAssetReadings,
            AuthPermissions.ViewOperationalUnits),

        Role(AuthRoles.Warehouse, "Bodeguero", "Warehouse",
            AuthPermissions.ViewGlobalWarehouses),

        Role(AuthRoles.WarehouseSupervisor, "Supervisor bodega", "Warehouse",
            AuthPermissions.ViewGlobalWarehouses,
            AuthPermissions.ManageAlerts,
            AuthPermissions.AdjustStock),

        Role(AuthRoles.Management, "Gerencia", "CostController",
            AuthPermissions.ViewCosts,
            AuthPermissions.ViewOperationalUnits),

        Role(AuthRoles.FaenaViewer, "Consulta faena", "Viewer",
            AuthPermissions.ViewOperationalUnits)
    ];

    public static IReadOnlyCollection<string> SeedPermissions(RoleDefinition role)
    {
        ArgumentNullException.ThrowIfNull(role);

        var permissions = string.Equals(role.Code, AuthRoles.Admin, StringComparison.OrdinalIgnoreCase)
            ? role.Permissions.Append(AuthPermissions.ManageEquipmentFamilies)
            : role.Permissions;

        return permissions
            .Select(Normalize)
            .Where(static permission => !string.IsNullOrWhiteSpace(permission))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool MatchesSeededRoles(IReadOnlyCollection<RoleDefinition> activeRoles)
    {
        var rolesByCode = activeRoles
            .GroupBy(role => Normalize(role.Code), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return InitialRoles.All(definition =>
        {
            if (!rolesByCode.TryGetValue(Normalize(definition.Code), out var current))
            {
                return false;
            }

            return string.Equals(current.Name, definition.Name, StringComparison.Ordinal)
                && current.Type == definition.Type
                && SeedPermissions(current).OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
                    .SequenceEqual(SeedPermissions(definition).OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase);
        });
    }
    public static IReadOnlyCollection<string> ResolvePermissions(
        IReadOnlyCollection<string> userRoles,
        IReadOnlyCollection<RoleDefinition> roleDefinitions)
    {
        return roleDefinitions
            .Where(role => userRoles.Contains(role.Code, StringComparer.OrdinalIgnoreCase))
            .SelectMany(role => role.Permissions)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string Normalize(string? value)
    {
        return value?.Trim().ToLowerInvariant() ?? string.Empty;
    }
    private static RoleDefinition Role(string code, string name, string type, params string[] permissions)
    {
        return new RoleDefinition(code, name, type, permissions);
    }
}
