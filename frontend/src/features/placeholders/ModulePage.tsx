import type { LucideIcon } from "lucide-react";
import type { ColumnDef } from "@tanstack/react-table";
import { DataTable } from "../../shared/table/DataTable";

type ModulePageProps = {
  title: string;
  accent: string;
  Icon: LucideIcon;
};

type ModuleRow = {
  estado: string;
  responsable: string;
  etapa: string;
};

const rows: ModuleRow[] = [
  { estado: "Pendiente", responsable: "Sin asignar", etapa: "Borrador" },
  { estado: "En revision", responsable: "Supervisor", etapa: "Activo" },
  { estado: "Aprobado", responsable: "Planificador", etapa: "Vigente" }
];

const columns: ColumnDef<ModuleRow>[] = [
  { accessorKey: "estado", header: "Estado", cell: (info) => info.getValue<string>() },
  { accessorKey: "responsable", header: "Responsable", cell: (info) => info.getValue<string>() },
  { accessorKey: "etapa", header: "Etapa", cell: (info) => info.getValue<string>() }
];

export function ModulePage({ title, accent, Icon }: ModulePageProps) {
  return (
    <section className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-3">
          <span className={`flex h-11 w-11 items-center justify-center rounded-lg ${accent} text-white`}>
            <Icon className="h-5 w-5" aria-hidden="true" />
          </span>
          <div>
            <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">{title}</h1>
            <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Mantenimiento [Nombre Empresa]</p>
          </div>
        </div>
        <button className="h-10 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400">
          Nuevo
        </button>
      </div>

      <section className="rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="grid min-h-[360px] grid-cols-1 lg:grid-cols-[1fr_280px]">
          <DataTable data={rows} columns={columns} />
          <aside className="border-t border-slate-200 p-4 dark:border-slate-800 lg:border-l lg:border-t-0">
            <h2 className="text-sm font-semibold text-slate-950 dark:text-white">Filtros</h2>
            <div className="mt-4 space-y-3">
              {["Faena", "Estado", "Periodo"].map((filter) => (
                <label key={filter} className="block text-sm font-medium text-slate-700 dark:text-slate-200">
                  {filter}
                  <select className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 focus:ring-2 dark:border-slate-700 dark:bg-slate-950">
                    <option>Todos</option>
                  </select>
                </label>
              ))}
            </div>
          </aside>
        </div>
      </section>
    </section>
  );
}
