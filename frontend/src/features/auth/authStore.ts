import { create } from "zustand";
import { createJSONStorage, persist } from "zustand/middleware";

export const AUTH_ROLES = {
  admin: "admin",
  planner: "planificador",
  maintenanceSupervisor: "supervisor_mantenimiento",
  technician: "tecnico",
  warehouse: "bodeguero",
  warehouseSupervisor: "supervisor_bodega",
  management: "gerencia",
  faenaViewer: "consulta_faena"
} as const;

export const AUTH_PERMISSIONS = {
  administration: "administracion",
  approveImports: "importaciones.aprobar",
  changeAssetFaena: "activos.cambiar_faena",
  manageTechnicalHierarchy: "jerarquia.gestionar",
  manageDocuments: "documentos.gestionar",
  validateDocuments: "documentos.validar",
  configureDocumentTypes: "documentos.configurar",
  changeValidatedDocumentExpiry: "documentos.vencimiento_validado.modificar",
  manageAlerts: "alertas.gestionar",
  configureAlerts: "alertas.configurar",
  adjustStock: "stock.ajustar",
  closeWorkOrders: "ot.cerrar",
  finalValidateWorkOrders: "ot.validar_final",
  viewCosts: "costos.ver",
  viewGlobalWarehouses: "bodegas.global"
} as const;

export type CurrentUser = {
  id: string;
  username: string;
  email: string;
  displayName: string;
  isActive: boolean;
  isLocked: boolean;
  roles: string[];
  permissions: string[];
  faenas: string[];
};

type LoginResponse = {
  accessToken: string;
  expiresAtUtc: string;
  user: CurrentUser;
};

type AuthStatus = "idle" | "loading" | "authenticated" | "unauthenticated";

type AuthState = {
  token: string | null;
  expiresAtUtc: string | null;
  user: CurrentUser | null;
  status: AuthStatus;
  login: (username: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
  refresh: () => Promise<void>;
  clearSession: () => void;
  hasRole: (roles: string[]) => boolean;
  hasPermission: (permissions: string[]) => boolean;
};

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      token: null,
      expiresAtUtc: null,
      user: null,
      status: "idle",

      async login(username, password) {
        set({ status: "loading" });

        const response = await fetch("/api/auth/login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ username, password })
        });

        if (!response.ok) {
          set({ token: null, expiresAtUtc: null, user: null, status: "unauthenticated" });
          throw new Error(response.status === 401 ? "Usuario o clave invalidos." : "No fue posible iniciar sesion.");
        }

        const data = (await response.json()) as LoginResponse;
        set({
          token: data.accessToken,
          expiresAtUtc: data.expiresAtUtc,
          user: data.user,
          status: "authenticated"
        });
      },

      async logout() {
        const token = get().token;
        if (token) {
          await fetch("/api/auth/logout", {
            method: "POST",
            headers: { Authorization: `Bearer ${token}` }
          }).catch(() => undefined);
        }

        get().clearSession();
      },

      async refresh() {
        const token = get().token;
        if (!token || isExpired(get().expiresAtUtc)) {
          get().clearSession();
          return;
        }

        set({ status: "loading" });
        const response = await fetch("/api/auth/me", {
          headers: { Authorization: `Bearer ${token}` }
        });

        if (!response.ok) {
          get().clearSession();
          return;
        }

        const user = (await response.json()) as CurrentUser;
        set({ user, status: "authenticated" });
      },

      clearSession() {
        set({ token: null, expiresAtUtc: null, user: null, status: "unauthenticated" });
      },

      hasRole(roles) {
        const userRoles = get().user?.roles ?? [];
        return roles.some((role) => userRoles.includes(role));
      },

      hasPermission(permissions) {
        const userPermissions = get().user?.permissions ?? [];
        return permissions.some((permission) => userPermissions.includes(permission));
      }
    }),
    {
      name: "cmms-auth-session",
      storage: createJSONStorage(() => sessionStorage),
      partialize: (state) => ({
        token: state.token,
        expiresAtUtc: state.expiresAtUtc,
        user: state.user
      })
    }
  )
);

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
  const { token, clearSession } = useAuthStore.getState();
  const headers = new Headers(init?.headers);
  const isFormData = init?.body instanceof FormData;
  if (!isFormData) {
    headers.set("Content-Type", "application/json");
  }
  if (token) {
    headers.set("Authorization", `Bearer ${token}`);
  }

  const response = await fetch(path, {
    ...init,
    headers
  });

  if (response.status === 401) {
    clearSession();
  }

  if (!response.ok) {
    const message = await readErrorMessage(response);
    throw new Error(message);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

function isExpired(expiresAtUtc: string | null) {
  return Boolean(expiresAtUtc && new Date(expiresAtUtc).getTime() <= Date.now());
}

async function readErrorMessage(response: Response) {
  try {
    const payload = (await response.json()) as { message?: string; detail?: string; title?: string; errors?: Record<string, string[]> };
    if (payload.message) {
      return payload.message;
    }

    if (payload.detail) {
      return payload.detail;
    }

    if (payload.errors) {
      const firstError = Object.values(payload.errors).flat()[0];
      if (firstError) {
        return firstError;
      }
    }

    return payload.title ?? "La operacion no pudo completarse.";
  } catch {
    return "La operacion no pudo completarse.";
  }
}
