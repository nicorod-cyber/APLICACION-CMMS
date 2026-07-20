import { useAuthStore } from "../auth/authStore";

export function DashboardPage() {
  const user = useAuthStore((state) => state.user);
  const primaryRole = user?.roles[0]?.replace(/_/g, " ") ?? "sin rol";

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Dashboard</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Mantenimiento</p>
        </div>
        <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-slate-800 dark:bg-slate-900">
          <span className="font-semibold text-slate-950 dark:text-white">{user?.displayName}</span>
          <span className="ml-2 text-slate-500 dark:text-slate-400">{primaryRole}</span>
        </div>
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Indicadores operacionales</h2>
        <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">
          Los indicadores se mostrar\u00e1n cuando existan datos operacionales reales. No se presentan m\u00e9tricas ni \u00f3rdenes simuladas.
        </p>
      </section>
    </section>
  );
}