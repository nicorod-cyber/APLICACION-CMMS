import { Navigate, createBrowserRouter } from "react-router-dom";
import { DashboardPage } from "../features/dashboard/DashboardPage";
import { FaenasPage } from "../features/faenas/FaenasPage";
import { FaenaDetailPage } from "../features/faenas/FaenaDetailPage";
import { LoginPage } from "../features/auth/LoginPage";
import { ProtectedRoute } from "../features/auth/ProtectedRoute";
import { AUTH_PERMISSIONS, AUTH_ROLES } from "../features/auth/authStore";
import { AssetsPage } from "../features/assets/AssetsPage";
import { EquipmentOverviewPage } from "../features/equipment-overview/EquipmentOverviewPage";
import { EquipmentAssetDetailPage } from "../features/equipment-overview/EquipmentAssetDetailPage";
import { CompositeUnitDetailPage } from "../features/equipment-overview/CompositeUnitDetailPage";
import { OperationalUnitsPage } from "../features/operational-units/OperationalUnitsPage";
import { MaintenanceTargetsPage } from "../features/maintenance-targets/MaintenanceTargetsPage";
import { UsersAdminPage } from "../features/admin/UsersAdminPage";
import { AlertsPage } from "../features/alerts/AlertsPage";
import { AuditPage } from "../features/audit/AuditPage";
import { AvailabilityPage } from "../features/availability/AvailabilityPage";
import { DocumentsPage } from "../features/documents/DocumentsPage";
import { CostsPage } from "../features/costs/CostsPage";
import { ImportsPage } from "../features/imports/ImportsPage";
import { InventoryPage } from "../features/inventory/InventoryPage";
import { MaterialRequestsPage } from "../features/material-requests/MaterialRequestsPage";
import { PreventivePlansOverviewPage } from "../features/preventive/PreventivePlansOverviewPage";
import { PreventivePlanDetailPage } from "../features/preventive/PreventivePlanDetailPage";
import { PreventiveCalendarPage } from "../features/preventive/PreventiveCalendarPage";
import { PreventiveReadingsPage } from "../features/preventive/PreventiveReadingsPage";
import { ProcurementPage } from "../features/procurement/ProcurementPage";
import { SchedulingLayoutPage } from "../features/scheduling/SchedulingLayoutPage";
import { SparePartsPage } from "../features/inventory/SparePartsPage";
import { TechnicalHierarchyPage } from "../features/technical-hierarchy/TechnicalHierarchyPage";
import { WorkNotificationsOverviewPage } from "../features/work-notifications/WorkNotificationsOverviewPage";
import { WorkNotificationDetailPage } from "../features/work-notifications/WorkNotificationDetailPage";
import { WorkOrdersOverviewPage } from "../features/work-orders/WorkOrdersOverviewPage";
import { WorkOrderDetailPage } from "../features/work-orders/WorkOrderDetailPage";
import { ModulePage } from "../features/placeholders/ModulePage";
import { AppLayout } from "../shared/layout/AppLayout";
import { navigationItems } from "./navigation";

const protectedModule = (path: string) => {
  const item = navigationItems.find((navigationItem) => navigationItem.path === path);
  if (!item) throw new Error("Missing navigation configuration for " + path);
  return item;
};

const notificationNavigation = protectedModule("/avisos");
const workOrderNavigation = protectedModule("/ot");
const preventiveNavigation = protectedModule("/preventivos");
const schedulingNavigation = protectedModule("/programacion");

export const router = createBrowserRouter([
  {
    path: "/login",
    element: <LoginPage />
  },
  {
    path: "/",
    element: (
      <ProtectedRoute>
        <AppLayout />
      </ProtectedRoute>
    ),
    children: [
      {
        index: true,
        element: <Navigate to="/dashboard" replace />
      },
      {
        path: "dashboard",
        element: <DashboardPage />
      },
      { path: "activos", element: <AssetsPage /> },
      { path: "faenas/:codigo", element: <ProtectedRoute permissions={[AUTH_PERMISSIONS.viewFaenas]}><FaenaDetailPage /></ProtectedRoute> },
      { path: "equipos/activos/:code", element: <EquipmentAssetDetailPage /> },
      { path: "equipos/unidades/:code", element: <CompositeUnitDetailPage /> },
      { path: "equipos-operacionales", element: <ProtectedRoute roles={[AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor, AUTH_ROLES.management, AUTH_ROLES.faenaViewer]}><MaintenanceTargetsPage /></ProtectedRoute> },
      { path: "unidades-operativas", element: <ProtectedRoute permissions={[AUTH_PERMISSIONS.viewOperationalUnits]}><OperationalUnitsPage /></ProtectedRoute> },
      { path: "jerarquia-tecnica", element: <ProtectedRoute roles={[AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor, AUTH_ROLES.faenaViewer]}><TechnicalHierarchyPage /></ProtectedRoute> },
      { path: "avisos", element: <ProtectedRoute roles={notificationNavigation.roles} permissions={notificationNavigation.permissions}><WorkNotificationsOverviewPage /></ProtectedRoute> },
      { path: "avisos/:avisoId", element: <ProtectedRoute roles={notificationNavigation.roles} permissions={notificationNavigation.permissions}><WorkNotificationDetailPage /></ProtectedRoute> },
      { path: "ot", element: <ProtectedRoute roles={workOrderNavigation.roles} permissions={workOrderNavigation.permissions}><WorkOrdersOverviewPage /></ProtectedRoute> },
      { path: "ot/:numeroOT", element: <ProtectedRoute roles={workOrderNavigation.roles} permissions={workOrderNavigation.permissions}><WorkOrderDetailPage /></ProtectedRoute> },
      { path: "preventivos", element: <ProtectedRoute roles={preventiveNavigation.roles} permissions={preventiveNavigation.permissions}><PreventivePlansOverviewPage /></ProtectedRoute> },
      { path: "preventivos/planes/:planCode", element: <ProtectedRoute roles={preventiveNavigation.roles} permissions={preventiveNavigation.permissions}><PreventivePlanDetailPage /></ProtectedRoute> },
      { path: "preventivos/calendario", element: <ProtectedRoute roles={preventiveNavigation.roles} permissions={preventiveNavigation.permissions}><PreventiveCalendarPage /></ProtectedRoute> },
      { path: "preventivos/lecturas", element: <ProtectedRoute roles={preventiveNavigation.roles} permissions={preventiveNavigation.permissions}><PreventiveReadingsPage /></ProtectedRoute> },
      { path: "programacion", element: <ProtectedRoute roles={schedulingNavigation.roles} permissions={schedulingNavigation.permissions}><Navigate to="/programacion/calendario" replace /></ProtectedRoute> },
      { path: "programacion/:view", element: <ProtectedRoute roles={schedulingNavigation.roles} permissions={schedulingNavigation.permissions}><SchedulingLayoutPage /></ProtectedRoute> },
      ...navigationItems
        .filter((item) => !["/dashboard", "/avisos", "/ot", "/preventivos", "/programacion"].includes(item.path))
        .map((item) => ({
          path: item.path.replace("/", ""),
          element:
            item.path === "/administracion" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <UsersAdminPage />
              </ProtectedRoute>
            ) : item.path === "/auditoria" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <AuditPage />
              </ProtectedRoute>
            ) : item.path === "/importaciones" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <ImportsPage />
              </ProtectedRoute>
            ) : item.path === "/faenas" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <FaenasPage />
              </ProtectedRoute>
            ) : item.path === "/equipos" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <EquipmentOverviewPage />
              </ProtectedRoute>
            ) : item.path === "/equipos-operacionales" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <MaintenanceTargetsPage />
              </ProtectedRoute>
            ) : item.path === "/unidades-operativas" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <OperationalUnitsPage />
              </ProtectedRoute>            ) : item.path === "/documentos" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <DocumentsPage />
              </ProtectedRoute>
            ) : item.path === "/bodega" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <InventoryPage />
              </ProtectedRoute>
            ) : item.path === "/repuestos" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <SparePartsPage />
              </ProtectedRoute>
            ) : item.path === "/alertas" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <AlertsPage />
              </ProtectedRoute>
            ) : item.path === "/jerarquia-tecnica" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <TechnicalHierarchyPage />
              </ProtectedRoute>
            ) : item.path === "/solicitudes" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <MaterialRequestsPage />
              </ProtectedRoute>
            ) : item.path === "/abastecimiento" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <ProcurementPage />
              </ProtectedRoute>
            ) : item.path === "/disponibilidad" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <AvailabilityPage />
              </ProtectedRoute>
            ) : item.path === "/costos" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}><CostsPage /></ProtectedRoute>
            ) : (
              <ModulePage title={item.label} accent={item.accent} Icon={item.icon} />
            )
        }))
    ]
  }
]);
