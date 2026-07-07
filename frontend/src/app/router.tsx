import { Navigate, createBrowserRouter } from "react-router-dom";
import { DashboardPage } from "../features/dashboard/DashboardPage";
import { LoginPage } from "../features/auth/LoginPage";
import { ProtectedRoute } from "../features/auth/ProtectedRoute";
import { AssetsPage } from "../features/assets/AssetsPage";
import { UsersAdminPage } from "../features/admin/UsersAdminPage";
import { AlertsPage } from "../features/alerts/AlertsPage";
import { AuditPage } from "../features/audit/AuditPage";
import { AvailabilityPage } from "../features/availability/AvailabilityPage";
import { DocumentsPage } from "../features/documents/DocumentsPage";
import { ImportsPage } from "../features/imports/ImportsPage";
import { InventoryPage } from "../features/inventory/InventoryPage";
import { MaterialRequestsPage } from "../features/material-requests/MaterialRequestsPage";
import { PreventiveMaintenancePage } from "../features/preventive/PreventiveMaintenancePage";
import { ProcurementPage } from "../features/procurement/ProcurementPage";
import { SchedulingPage } from "../features/scheduling/SchedulingPage";
import { SparePartsPage } from "../features/inventory/SparePartsPage";
import { TechnicalHierarchyPage } from "../features/technical-hierarchy/TechnicalHierarchyPage";
import { WorkNotificationsPage } from "../features/work-notifications/WorkNotificationsPage";
import { WorkOrdersPage } from "../features/work-orders/WorkOrdersPage";
import { ModulePage } from "../features/placeholders/ModulePage";
import { AppLayout } from "../shared/layout/AppLayout";
import { navigationItems } from "./navigation";

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
      ...navigationItems
        .filter((item) => item.path !== "/dashboard")
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
            ) : item.path === "/activos" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <AssetsPage />
              </ProtectedRoute>
            ) : item.path === "/documentos" ? (
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
            ) : item.path === "/avisos" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <WorkNotificationsPage />
              </ProtectedRoute>
            ) : item.path === "/ot" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <WorkOrdersPage />
              </ProtectedRoute>
            ) : item.path === "/preventivos" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <PreventiveMaintenancePage />
              </ProtectedRoute>
            ) : item.path === "/disponibilidad" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <AvailabilityPage />
              </ProtectedRoute>
            ) : item.path === "/programacion" ? (
              <ProtectedRoute roles={item.roles} permissions={item.permissions}>
                <SchedulingPage />
              </ProtectedRoute>
            ) : (
              <ModulePage title={item.label} accent={item.accent} Icon={item.icon} />
            )
        }))
    ]
  }
]);
