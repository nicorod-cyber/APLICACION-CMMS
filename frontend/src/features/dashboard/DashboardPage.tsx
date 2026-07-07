import { useAuthStore } from "../auth/authStore";

const metrics = [
  { label: "OT abiertas", value: "24", tone: "border-teal-500" },
  { label: "Alertas documentales", value: "7", tone: "border-amber-500" },
  { label: "Reservas pendientes", value: "12", tone: "border-sky-500" },
  { label: "Disponibilidad", value: "96.4%", tone: "border-emerald-500" }
];

const workOrders = [
  { code: "OT-0001", asset: "Chancador primario", status: "Planificada", owner: "Supervisor Norte" },
  { code: "OT-0002", asset: "Correa CV-03", status: "En ejecucion", owner: "Tecnico Turno A" },
  { code: "OT-0003", asset: "Bomba impulsion", status: "Pendiente repuesto", owner: "Bodega Central" }
];

export function DashboardPage() {
  const user = useAuthStore((state) => state.user);
  const primaryRole = user?.roles[0]?.replace(/_/g, " ") ?? "sin rol";

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Dashboard</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Mantenimiento [Nombre Empresa]</p>
        </div>
        <div className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-slate-800 dark:bg-slate-900">
          <span className="font-semibold text-slate-950 dark:text-white">{user?.displayName}</span>
          <span className="ml-2 text-slate-500 dark:text-slate-400">{primaryRole}</span>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        {metrics.map((metric) => (
          <article
            key={metric.label}
            className={`rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900 ${metric.tone} border-l-4`}
          >
            <p className="text-sm font-medium text-slate-500 dark:text-slate-400">{metric.label}</p>
            <p className="mt-3 text-3xl font-semibold text-slate-950 dark:text-white">{metric.value}</p>
          </article>
        ))}
      </div>

      <div className="grid gap-4 xl:grid-cols-[1.3fr_0.7fr]">
        <section className="rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div className="border-b border-slate-200 px-4 py-3 dark:border-slate-800">
            <h2 className="text-base font-semibold text-slate-950 dark:text-white">Ordenes recientes</h2>
          </div>
          <div className="overflow-x-auto">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
                <tr>
                  <th className="px-4 py-3 font-medium">Codigo</th>
                  <th className="px-4 py-3 font-medium">Activo</th>
                  <th className="px-4 py-3 font-medium">Estado</th>
                  <th className="px-4 py-3 font-medium">Responsable</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {workOrders.map((order) => (
                  <tr key={order.code}>
                    <td className="px-4 py-3 font-semibold text-slate-900 dark:text-slate-100">{order.code}</td>
                    <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{order.asset}</td>
                    <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{order.status}</td>
                    <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{order.owner}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Estado operativo</h2>
          <div className="mt-5 space-y-4">
            {[
              ["Preventivos", "72%"],
              ["Bodega", "64%"],
              ["Documentos", "81%"]
            ].map(([label, value]) => (
              <div key={label}>
                <div className="mb-2 flex items-center justify-between text-sm">
                  <span className="text-slate-600 dark:text-slate-300">{label}</span>
                  <span className="font-semibold text-slate-900 dark:text-white">{value}</span>
                </div>
                <div className="h-2 rounded-full bg-slate-100 dark:bg-slate-800">
                  <div className="h-2 rounded-full bg-teal-500" style={{ width: value }} />
                </div>
              </div>
            ))}
          </div>
        </section>
      </div>
    </section>
  );
}
