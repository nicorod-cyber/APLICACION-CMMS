import { LogOut, Menu, Search, UserCircle } from "lucide-react";
import { useState } from "react";
import { NavLink, Outlet, useNavigate } from "react-router-dom";
import { getVisibleNavigationItems } from "../../app/navigation";
import { useAuthStore } from "../../features/auth/authStore";
import { Logo } from "../ui/Logo";
import { ThemeToggle } from "./ThemeToggle";

export function AppLayout() {
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);
  const logout = useAuthStore((state) => state.logout);
  const navigationItems = getVisibleNavigationItems(user);

  async function handleLogout() {
    await logout();
    navigate("/login", { replace: true });
  }

  return (
    <div className="min-h-screen bg-slate-100 text-slate-950 dark:bg-slate-950 dark:text-slate-100">
      <aside
        className={`fixed inset-y-0 left-0 z-30 flex w-72 flex-col border-r border-slate-200 bg-white transition-transform dark:border-slate-800 dark:bg-slate-900 lg:translate-x-0 ${
          sidebarOpen ? "translate-x-0" : "-translate-x-full"
        }`}
      >
        <div className="flex h-16 items-center border-b border-slate-200 px-4 dark:border-slate-800">
          <Logo compact />
        </div>
        <nav className="flex-1 space-y-1 overflow-y-auto px-3 py-4">
          {navigationItems.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              onClick={() => setSidebarOpen(false)}
              className={({ isActive }) =>
                [
                  "flex h-10 items-center gap-3 rounded-md px-3 text-sm font-medium transition",
                  isActive
                    ? "bg-slate-900 text-white dark:bg-white dark:text-slate-950"
                    : "text-slate-600 hover:bg-slate-100 hover:text-slate-950 dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-white"
                ].join(" ")
              }
            >
              <span className={`flex h-6 w-6 items-center justify-center rounded ${item.accent} text-white`}>
                <item.icon className="h-3.5 w-3.5" aria-hidden="true" />
              </span>
              <span className="truncate">{item.label}</span>
            </NavLink>
          ))}
        </nav>
      </aside>

      {sidebarOpen ? (
        <button
          aria-label="Cerrar menu"
          className="fixed inset-0 z-20 bg-slate-950/40 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      ) : null}

      <div className="lg:pl-72">
        <header className="sticky top-0 z-10 flex h-16 items-center gap-3 border-b border-slate-200 bg-white/90 px-4 backdrop-blur dark:border-slate-800 dark:bg-slate-900/90">
          <button
            aria-label="Abrir menu"
            className="flex h-10 w-10 items-center justify-center rounded-md border border-slate-200 text-slate-600 dark:border-slate-700 dark:text-slate-300 lg:hidden"
            onClick={() => setSidebarOpen(true)}
          >
            <Menu className="h-5 w-5" aria-hidden="true" />
          </button>
          <div className="relative min-w-0 flex-1">
            <Search
              className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400"
              aria-hidden="true"
            />
            <input
              className="h-10 w-full rounded-md border border-slate-200 bg-slate-50 pl-9 pr-3 text-sm outline-none ring-teal-500 transition placeholder:text-slate-400 focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
              placeholder="Busqueda global"
              type="search"
            />
          </div>
          <ThemeToggle />
          <div className="hidden min-w-0 items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-sm dark:border-slate-700 md:flex">
            <UserCircle className="h-4 w-4 text-slate-500 dark:text-slate-400" aria-hidden="true" />
            <span className="max-w-44 truncate text-slate-700 dark:text-slate-200">{user?.displayName}</span>
          </div>
          <button
            aria-label="Cerrar sesion"
            title="Cerrar sesion"
            className="flex h-10 w-10 items-center justify-center rounded-md border border-slate-200 text-slate-600 transition hover:bg-slate-100 hover:text-slate-950 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800 dark:hover:text-white"
            onClick={() => void handleLogout()}
            type="button"
          >
            <LogOut className="h-4 w-4" aria-hidden="true" />
          </button>
        </header>

        <main className="mx-auto max-w-7xl px-4 py-6 md:px-6">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
