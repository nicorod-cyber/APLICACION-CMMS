import { useEffect } from "react";
import { Navigate } from "react-router-dom";
import { useAuthStore } from "./authStore";

type ProtectedRouteProps = {
  children: JSX.Element;
  roles?: string[];
  permissions?: string[];
};

export function ProtectedRoute({ children, roles, permissions }: ProtectedRouteProps) {
  const token = useAuthStore((state) => state.token);
  const user = useAuthStore((state) => state.user);
  const status = useAuthStore((state) => state.status);
  const refresh = useAuthStore((state) => state.refresh);
  const hasRole = useAuthStore((state) => state.hasRole);
  const hasPermission = useAuthStore((state) => state.hasPermission);

  useEffect(() => {
    if (token && status === "idle") {
      void refresh();
    }
  }, [refresh, status, token]);

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  if (!user || status === "loading") {
    return (
      <main className="flex min-h-screen items-center justify-center bg-slate-100 text-sm text-slate-600 dark:bg-slate-950 dark:text-slate-300">
        Cargando sesion...
      </main>
    );
  }

  if (roles && roles.length > 0 && !hasRole(roles)) {
    return <Navigate to="/dashboard" replace />;
  }

  if (permissions && permissions.length > 0 && !hasPermission(permissions)) {
    return <Navigate to="/dashboard" replace />;
  }

  return children;
}
