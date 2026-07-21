import {
  Activity,
  BarChart3,
  Bell,
  Boxes,
  CalendarDays,
  DollarSign,
  ClipboardList,
  FileText,
  GitBranch,
  History,
  LayoutDashboard,
  MapPinned,
  Package,
  PackageCheck,
  Settings,
  ShoppingCart,
  Upload,
  Warehouse,
  Wrench
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { AUTH_PERMISSIONS, AUTH_ROLES, type CurrentUser } from "../features/auth/authStore";

export type NavigationItem = {
  label: string;
  path: string;
  icon: LucideIcon;
  accent: string;
  roles?: string[];
  permissions?: string[];
};

export const navigationItems: NavigationItem[] = [
  { label: "Dashboard", path: "/dashboard", icon: LayoutDashboard, accent: "bg-teal-500" },
  {
    label: "Faenas",
    path: "/faenas",
    icon: MapPinned,
    accent: "bg-teal-600",
    roles: [AUTH_ROLES.admin]
  },
  {
    label: "Activos",
    path: "/activos",
    icon: Boxes,
    accent: "bg-sky-500",
    roles: [
      AUTH_ROLES.admin,
      AUTH_ROLES.planner,
      AUTH_ROLES.maintenanceSupervisor,
      AUTH_ROLES.management,
      AUTH_ROLES.faenaViewer
    ]
  },
  {
    label: "Equipos operacionales",
    path: "/equipos-operacionales",
    icon: Activity,
    accent: "bg-cyan-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor, AUTH_ROLES.management, AUTH_ROLES.faenaViewer]
  },
  {
    label: "Unidades operativas",
    path: "/unidades-operativas",
    icon: Boxes,
    accent: "bg-cyan-600",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor, AUTH_ROLES.faenaViewer]
  },  {
    label: "Jerarquia",
    path: "/jerarquia-tecnica",
    icon: GitBranch,
    accent: "bg-sky-600",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor, AUTH_ROLES.faenaViewer]
  },
  {
    label: "Documentos",
    path: "/documentos",
    icon: FileText,
    accent: "bg-amber-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor, AUTH_ROLES.faenaViewer]
  },
  {
    label: "Bodega",
    path: "/bodega",
    icon: Warehouse,
    accent: "bg-emerald-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.warehouse, AUTH_ROLES.warehouseSupervisor],
    permissions: [AUTH_PERMISSIONS.viewGlobalWarehouses]
  },
  {
    label: "Repuestos",
    path: "/repuestos",
    icon: Package,
    accent: "bg-cyan-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.warehouse, AUTH_ROLES.warehouseSupervisor]
  },
  {
    label: "Solicitudes",
    path: "/solicitudes",
    icon: ShoppingCart,
    accent: "bg-orange-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.warehouse, AUTH_ROLES.warehouseSupervisor]
  },
  {
    label: "Abastecimiento",
    path: "/abastecimiento",
    icon: PackageCheck,
    accent: "bg-teal-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.warehouseSupervisor]
  },
  {
    label: "Avisos",
    path: "/avisos",
    icon: Bell,
    accent: "bg-rose-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor, AUTH_ROLES.technician]
  },
  {
    label: "OT",
    path: "/ot",
    icon: ClipboardList,
    accent: "bg-indigo-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor, AUTH_ROLES.technician]
  },
  {
    label: "Preventivos",
    path: "/preventivos",
    icon: Wrench,
    accent: "bg-lime-600",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor]
  },
  {
    label: "Disponibilidad",
    path: "/disponibilidad",
    icon: Activity,
    accent: "bg-emerald-600",
    roles: [
      AUTH_ROLES.admin,
      AUTH_ROLES.planner,
      AUTH_ROLES.maintenanceSupervisor,
      AUTH_ROLES.management,
      AUTH_ROLES.faenaViewer
    ]
  },
  {
    label: "Programacion",
    path: "/programacion",
    icon: CalendarDays,
    accent: "bg-blue-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor]
  },
  {
    label: "Reportes",
    path: "/reportes",
    icon: BarChart3,
    accent: "bg-violet-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.management, AUTH_ROLES.planner],
    permissions: [AUTH_PERMISSIONS.viewCosts]
  },
  {
    label: "Alertas",
    path: "/alertas",
    icon: Bell,
    accent: "bg-red-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor]
  },
  {
    label: "Importaciones",
    path: "/importaciones",
    icon: Upload,
    accent: "bg-yellow-500",
    roles: [AUTH_ROLES.admin, AUTH_ROLES.planner],
    permissions: [AUTH_PERMISSIONS.approveImports]
  },
  {
    label: "Administracion",
    path: "/administracion",
    icon: Settings,
    accent: "bg-slate-500",
    roles: [AUTH_ROLES.admin],
    permissions: [AUTH_PERMISSIONS.administration]
  },
  {
    label: "Auditoria",
    path: "/auditoria",
    icon: History,
    accent: "bg-zinc-500",
    roles: [AUTH_ROLES.admin],
    permissions: [AUTH_PERMISSIONS.administration]
  }
];

export function getVisibleNavigationItems(user: CurrentUser | null) {
  return navigationItems.filter((item) => {
    if (!user) {
      return false;
    }

    const roleAllowed = !item.roles || item.roles.some((role) => user.roles.includes(role));
    const permissionAllowed =
      !item.permissions || item.permissions.some((permission) => user.permissions.includes(permission));

    return roleAllowed || permissionAllowed;
  });
}
