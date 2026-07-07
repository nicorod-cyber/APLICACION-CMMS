using MaintenanceCMMS.Application.Auth;

namespace MaintenanceCMMS.Infrastructure.Security;

public sealed class AuthorizationPolicyService : IAuthorizationPolicyService
{
    public bool CanAccessWorkOrder(UserAccessContext user, WorkOrderAccessContext workOrder)
    {
        if (CanAdminister(user) || HasRole(user, AuthRoles.Planner))
        {
            return IsAuthorizedForFaena(user, workOrder.FaenaCodigo) || HasRole(user, AuthRoles.Admin);
        }

        if (HasRole(user, AuthRoles.Technician))
        {
            return workOrder.AssignedUserIds.Contains(user.UserId, StringComparer.OrdinalIgnoreCase);
        }

        if (HasRole(user, AuthRoles.MaintenanceSupervisor) || HasRole(user, AuthRoles.FaenaViewer))
        {
            return IsAuthorizedForFaena(user, workOrder.FaenaCodigo);
        }

        return false;
    }

    public bool CanViewFaena(UserAccessContext user, string faenaCodigo)
    {
        return CanAdminister(user) || IsAuthorizedForFaena(user, faenaCodigo);
    }

    public bool CanViewWarehouses(UserAccessContext user)
    {
        return CanAdminister(user) ||
               HasRole(user, AuthRoles.Warehouse) ||
               HasRole(user, AuthRoles.WarehouseSupervisor) ||
               HasPermission(user, AuthPermissions.ViewGlobalWarehouses);
    }

    public bool CanViewCosts(UserAccessContext user)
    {
        return CanAdminister(user) ||
               HasRole(user, AuthRoles.Management) ||
               HasPermission(user, AuthPermissions.ViewCosts);
    }

    public bool CanAdminister(UserAccessContext user)
    {
        return HasRole(user, AuthRoles.Admin) || HasPermission(user, AuthPermissions.Administration);
    }

    public bool CanImport(UserAccessContext user)
    {
        return CanAdminister(user) || HasPermission(user, AuthPermissions.ApproveImports);
    }

    public bool CanChangeAssetFaena(UserAccessContext user)
    {
        return CanAdminister(user) || HasPermission(user, AuthPermissions.ChangeAssetFaena);
    }

    public bool CanManageTechnicalHierarchy(UserAccessContext user)
    {
        return CanAdminister(user) ||
               HasRole(user, AuthRoles.Planner) ||
               HasPermission(user, AuthPermissions.ManageTechnicalHierarchy);
    }

    public bool CanManageDocuments(UserAccessContext user)
    {
        return CanAdminister(user) ||
               HasRole(user, AuthRoles.Planner) ||
               HasRole(user, AuthRoles.MaintenanceSupervisor) ||
               HasPermission(user, AuthPermissions.ManageDocuments);
    }

    public bool CanValidateDocuments(UserAccessContext user)
    {
        return CanAdminister(user) ||
               HasRole(user, AuthRoles.Planner) ||
               HasRole(user, AuthRoles.MaintenanceSupervisor) ||
               HasPermission(user, AuthPermissions.ValidateDocuments);
    }

    public bool CanConfigureDocumentTypes(UserAccessContext user)
    {
        return CanAdminister(user) || HasPermission(user, AuthPermissions.ConfigureDocumentTypes);
    }

    public bool CanChangeValidatedDocumentExpiry(UserAccessContext user)
    {
        return CanAdminister(user) || HasPermission(user, AuthPermissions.ChangeValidatedDocumentExpiry);
    }

    public bool CanManageAlerts(UserAccessContext user)
    {
        return CanAdminister(user) ||
               HasRole(user, AuthRoles.Planner) ||
               HasRole(user, AuthRoles.MaintenanceSupervisor) ||
               HasRole(user, AuthRoles.WarehouseSupervisor) ||
               HasPermission(user, AuthPermissions.ManageAlerts);
    }

    public bool CanConfigureAlerts(UserAccessContext user)
    {
        return CanAdminister(user) || HasPermission(user, AuthPermissions.ConfigureAlerts);
    }

    public bool CanAdjustStock(UserAccessContext user)
    {
        return CanAdminister(user) || HasPermission(user, AuthPermissions.AdjustStock);
    }

    public bool CanCloseWorkOrder(UserAccessContext user)
    {
        return CanAdminister(user) ||
               HasRole(user, AuthRoles.MaintenanceSupervisor) ||
               HasPermission(user, AuthPermissions.CloseWorkOrders);
    }

    public bool CanFinalValidateWorkOrder(UserAccessContext user)
    {
        return CanAdminister(user) ||
               HasRole(user, AuthRoles.Planner) ||
               HasPermission(user, AuthPermissions.FinalValidateWorkOrders);
    }

    private static bool IsAuthorizedForFaena(UserAccessContext user, string faenaCodigo)
    {
        return user.Faenas.Contains(faenaCodigo, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasRole(UserAccessContext user, string role)
    {
        return user.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    }

    private static bool HasPermission(UserAccessContext user, string permission)
    {
        return user.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }
}
