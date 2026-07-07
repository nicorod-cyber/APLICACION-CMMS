import { FormEvent, useEffect, useMemo, useState } from "react";
import { Download, Eye, Filter } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";

type AuditSeverity = "Low" | "Medium" | "High" | "Critical";

type AuditLogEntry = {
  auditId: string;
  occurredAtUtc: string;
  userId: string;
  action: string;
  module: string;
  entityName: string;
  entityId: string;
  faenaCodigo?: string | null;
  severity: AuditSeverity;
  previousValue?: string | null;
  newValue?: string | null;
  ipAddress?: string | null;
  device?: string | null;
  reason?: string | null;
  success: boolean;
  detail?: string | null;
  correlationId?: string | null;
};

type AuditQueryResult = {
  totalCount: number;
  items: AuditLogEntry[];
};

type AuditFilters = {
  userId: string;
  module: string;
  entityName: string;
  action: string;
  faenaCodigo: string;
  severity: string;
  fromUtc: string;
  toUtc: string;
};

const emptyFilters: AuditFilters = {
  userId: "",
  module: "",
  entityName: "",
  action: "",
  faenaCodigo: "",
  severity: "",
  fromUtc: "",
  toUtc: ""
};

const severityStyles: Record<AuditSeverity, string> = {
  Low: "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200",
  Medium: "bg-sky-50 text-sky-700 dark:bg-sky-950 dark:text-sky-200",
  High: "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200",
  Critical: "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
};

export function AuditPage() {
  const [filters, setFilters] = useState<AuditFilters>(emptyFilters);
  const [result, setResult] = useState<AuditQueryResult>({ totalCount: 0, items: [] });
  const [selected, setSelected] = useState<AuditLogEntry | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void loadAudit(emptyFilters);
  }, []);

  const modules = useMemo(() => {
    return Array.from(new Set(result.items.map((item) => item.module).filter(Boolean))).sort();
  }, [result.items]);

  async function loadAudit(nextFilters: AuditFilters) {
    setIsLoading(true);
    setError(null);

    try {
      const query = new URLSearchParams();
      Object.entries(nextFilters).forEach(([key, value]) => {
        if (value) {
          query.set(key, normalizeDateValue(key, value));
        }
      });

      const data = await apiFetch<AuditQueryResult>(`/api/audit?${query.toString()}`);
      setResult(data);
      setSelected((current) => current ?? data.items[0] ?? null);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar la auditoria.");
    } finally {
      setIsLoading(false);
    }
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void loadAudit(filters);
  }

  function exportCsv() {
    const header = [
      "Fecha",
      "Usuario",
      "Modulo",
      "Accion",
      "Entidad",
      "Registro",
      "Faena",
      "Criticidad",
      "Motivo",
      "Anterior",
      "Nuevo"
    ];

    const rows = result.items.map((item) => [
      item.occurredAtUtc,
      item.userId,
      item.module,
      item.action,
      item.entityName,
      item.entityId,
      item.faenaCodigo ?? "",
      item.severity,
      item.reason ?? "",
      item.previousValue ?? "",
      item.newValue ?? ""
    ]);

    const csv = [header, ...rows].map((row) => row.map(escapeCsv).join(",")).join("\n");
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
    const link = document.createElement("a");
    link.href = url;
    link.download = `auditoria-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Auditoria</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Eventos criticos y gobierno de datos.</p>
        </div>
        <button
          className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          onClick={exportCsv}
          type="button"
        >
          <Download className="h-4 w-4" aria-hidden="true" />
          Exportar
        </button>
      </div>

      <form
        className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900"
        onSubmit={handleSubmit}
      >
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <FilterField label="Usuario" value={filters.userId} onChange={(value) => setFilters({ ...filters, userId: value })} />
          <FilterField
            label="Modulo"
            value={filters.module}
            onChange={(value) => setFilters({ ...filters, module: value })}
            suggestions={modules}
          />
          <FilterField
            label="Entidad"
            value={filters.entityName}
            onChange={(value) => setFilters({ ...filters, entityName: value })}
          />
          <FilterField label="Accion" value={filters.action} onChange={(value) => setFilters({ ...filters, action: value })} />
          <FaenaSelect value={filters.faenaCodigo} onChange={(value) => setFilters({ ...filters, faenaCodigo: value })} />
          <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
            Criticidad
            <select
              className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
              value={filters.severity}
              onChange={(event) => setFilters({ ...filters, severity: event.target.value })}
            >
              <option value="">Todas</option>
              <option value="Low">Baja</option>
              <option value="Medium">Media</option>
              <option value="High">Alta</option>
              <option value="Critical">Critica</option>
            </select>
          </label>
          <FilterField
            label="Desde"
            type="datetime-local"
            value={filters.fromUtc}
            onChange={(value) => setFilters({ ...filters, fromUtc: value })}
          />
          <FilterField
            label="Hasta"
            type="datetime-local"
            value={filters.toUtc}
            onChange={(value) => setFilters({ ...filters, toUtc: value })}
          />
        </div>

        <div className="mt-4 flex items-center gap-3">
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
            type="submit"
          >
            <Filter className="h-4 w-4" aria-hidden="true" />
            Filtrar
          </button>
          {error ? <span className="text-sm text-red-700 dark:text-red-300">{error}</span> : null}
        </div>
      </form>

      <div className="grid gap-4 xl:grid-cols-[1.2fr_0.8fr]">
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
            <h2 className="text-base font-semibold text-slate-950 dark:text-white">Eventos</h2>
            <span className="text-sm text-slate-500 dark:text-slate-400">{result.totalCount} registros</span>
          </div>

          {isLoading ? (
            <div className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando auditoria...</div>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
                  <tr>
                    <th className="px-4 py-3 font-medium">Fecha</th>
                    <th className="px-4 py-3 font-medium">Modulo</th>
                    <th className="px-4 py-3 font-medium">Accion</th>
                    <th className="px-4 py-3 font-medium">Criticidad</th>
                    <th className="px-4 py-3 font-medium">Detalle</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                  {result.items.map((item) => (
                    <tr key={item.auditId} className={selected?.auditId === item.auditId ? "bg-teal-50/70 dark:bg-teal-950/30" : ""}>
                      <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{formatDate(item.occurredAtUtc)}</td>
                      <td className="px-4 py-3 font-semibold text-slate-900 dark:text-slate-100">{item.module}</td>
                      <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{item.action}</td>
                      <td className="px-4 py-3">
                        <span className={`rounded-full px-2 py-1 text-xs font-semibold ${severityStyles[item.severity]}`}>
                          {item.severity}
                        </span>
                      </td>
                      <td className="px-4 py-3">
                        <button
                          className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-slate-200 text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                          onClick={() => setSelected(item)}
                          type="button"
                          aria-label="Ver detalle"
                          title="Ver detalle"
                        >
                          <Eye className="h-4 w-4" aria-hidden="true" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <AuditDetail entry={selected} />
      </div>
    </section>
  );
}

type FilterFieldProps = {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  suggestions?: string[];
};

function FilterField({ label, value, onChange, type = "text", suggestions }: FilterFieldProps) {
  const id = `audit-${label.toLowerCase().replace(/\s+/g, "-")}`;

  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <input
        id={id}
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
        list={suggestions ? `${id}-list` : undefined}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
      {suggestions ? (
        <datalist id={`${id}-list`}>
          {suggestions.map((item) => (
            <option key={item} value={item} />
          ))}
        </datalist>
      ) : null}
    </label>
  );
}

function AuditDetail({ entry }: { entry: AuditLogEntry | null }) {
  if (!entry) {
    return (
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Detalle</h2>
        <p className="mt-4 text-sm text-slate-500 dark:text-slate-400">Selecciona un evento.</p>
      </section>
    );
  }

  return (
    <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div>
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Detalle</h2>
        <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{entry.auditId}</p>
      </div>

      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <DetailItem label="Usuario" value={entry.userId} />
        <DetailItem label="Faena" value={entry.faenaCodigo ?? "-"} />
        <DetailItem label="Entidad" value={`${entry.entityName} / ${entry.entityId}`} />
        <DetailItem label="IP" value={entry.ipAddress ?? "-"} />
        <DetailItem label="Dispositivo" value={entry.device ?? "-"} />
        <DetailItem label="Motivo" value={entry.reason ?? "-"} />
      </dl>

      <div className="grid gap-3 md:grid-cols-2">
        <CompareBox label="Valor anterior" value={entry.previousValue} />
        <CompareBox label="Valor nuevo" value={entry.newValue} />
      </div>

      {entry.detail ? (
        <div className="rounded-md bg-slate-50 p-3 text-sm text-slate-700 dark:bg-slate-950 dark:text-slate-200">
          {entry.detail}
        </div>
      ) : null}
    </section>
  );
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">{label}</dt>
      <dd className="mt-1 break-words text-slate-800 dark:text-slate-100">{value}</dd>
    </div>
  );
}

function CompareBox({ label, value }: { label: string; value?: string | null }) {
  return (
    <div>
      <h3 className="text-sm font-semibold text-slate-950 dark:text-white">{label}</h3>
      <pre className="mt-2 min-h-32 overflow-auto rounded-md bg-slate-950 p-3 text-xs text-slate-100">{value || "-"}</pre>
    </div>
  );
}

function normalizeDateValue(key: string, value: string) {
  if ((key === "fromUtc" || key === "toUtc") && value) {
    return new Date(value).toISOString();
  }

  return value;
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("es-CL", {
    dateStyle: "short",
    timeStyle: "short"
  }).format(new Date(value));
}

function escapeCsv(value: string) {
  return `"${value.replace(/"/g, '""')}"`;
}
