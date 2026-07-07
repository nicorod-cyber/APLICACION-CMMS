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
            AuthPermissions.ApproveImports,
            AuthPermissions.ChangeAssetFaena,
            AuthPermissions.ManageTechnicalHierarchy,
            AuthPermissions.ManageDocuments,
            AuthPermissions.ValidateDocuments,
            AuthPermissions.ConfigureDocumentTypes,
            AuthPermissions.ChangeValidatedDocumentExpiry,
            AuthPermissions.ManageAlerts,
            AuthPermissions.ConfigureAlerts,
            AuthPermissions.AdjustStock,
            AuthPermissions.CloseWorkOrders,
            AuthPermissions.FinalValidateWorkOrders,
            AuthPermissions.ViewCosts,
            AuthPermissions.ViewGlobalWarehouses),

        Role(AuthRoles.Planner, "Planificador", "Planner",
            AuthPermissions.ApproveImports,
            AuthPermissions.ChangeAssetFaena,
            AuthPermissions.ManageTechnicalHierarchy,
            AuthPermissions.ManageDocuments,
            AuthPermissions.ValidateDocuments,
            AuthPermissions.ChangeValidatedDocumentExpiry,
            AuthPermissions.ManageAlerts,
            AuthPermissions.FinalValidateWorkOrders,
            AuthPermissions.ViewCosts),

        Role(AuthRoles.MaintenanceSupervisor, "Supervisor mantenimiento", "Supervisor",
            AuthPermissions.ManageDocuments,
            AuthPermissions.ValidateDocuments,
            AuthPermissions.ManageAlerts,
            AuthPermissions.CloseWorkOrders),

        Role(AuthRoles.Technician, "Tecnico", "Technician",
            AuthPermissions.ViewAssignedWorkOrders),

        Role(AuthRoles.Warehouse, "Bodeguero", "Warehouse",
            AuthPermissions.ViewGlobalWarehouses),

        Role(AuthRoles.WarehouseSupervisor, "Supervisor bodega", "Warehouse",
            AuthPermissions.ViewGlobalWarehouses,
            AuthPermissions.ManageAlerts,
            AuthPermissions.AdjustStock),

        Role(AuthRoles.Management, "Gerencia", "CostController",
            AuthPermissions.ViewCosts),

        Role(AuthRoles.FaenaViewer, "Consulta faena", "Viewer")
    ];

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

    private static RoleDefinition Role(string code, string name, string type, params string[] permissions)
    {
        return new RoleDefinition(code, name, type, permissions);
    }
}
