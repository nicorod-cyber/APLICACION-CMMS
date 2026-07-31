export type OperationalStatusVisual = "operational" | "warning" | "out-of-service" | "corrective" | "preventive" | "documental" | "preparation" | "decommissioned" | "unknown";

const classes: Record<OperationalStatusVisual, string> = {
  operational: "border border-emerald-200 bg-emerald-100 text-emerald-800 dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-200",
  warning: "border border-amber-200 bg-amber-100 text-amber-800 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-200",
  "out-of-service": "border border-red-200 bg-red-100 text-red-800 dark:border-red-800 dark:bg-red-950 dark:text-red-200",
  corrective: "border border-rose-200 bg-rose-100 text-rose-800 dark:border-rose-800 dark:bg-rose-950 dark:text-rose-200",
  preventive: "border border-blue-200 bg-blue-100 text-blue-800 dark:border-blue-800 dark:bg-blue-950 dark:text-blue-200",
  documental: "border border-violet-200 bg-violet-100 text-violet-800 dark:border-violet-800 dark:bg-violet-950 dark:text-violet-200",
  preparation: "border border-cyan-200 bg-cyan-100 text-cyan-800 dark:border-cyan-800 dark:bg-cyan-950 dark:text-cyan-200",
  decommissioned: "border border-slate-300 bg-slate-200 text-slate-700 dark:border-slate-600 dark:bg-slate-700 dark:text-slate-200",
  unknown: "border border-slate-200 bg-slate-100 text-slate-600 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300"
};

const labels: Record<Exclude<OperationalStatusVisual, "unknown">, string> = {
  operational: "Operativo", warning: "Con alerta", "out-of-service": "F/S", corrective: "Correctivo",
  preventive: "Preventivo", documental: "Documental", preparation: "Preparación", decommissioned: "Dado de baja"
};

const normalize = (value?: string | null) => (value ?? "").normalize("NFD").replace(/[\u0300-\u036f]/g, "").toUpperCase().replace(/[\s_\-/]/g, "");

export function resolveOperationalStatusVisual(code?: string | null, name?: string | null): OperationalStatusVisual {
  const value = normalize(code) || normalize(name);
  if (value === "OPERATIVO") return "operational";
  if (value === "CONALERTA") return "warning";
  if (value === "FUERASERVICIO" || value === "FUERADESERVICIO" || value === "FS") return "out-of-service";
  if (value === "CORRECTIVO") return "corrective";
  if (value === "PREVENTIVO") return "preventive";
  if (value === "DOCUMENTAL") return "documental";
  if (value === "PREPARACION" || value === "ENPREPARACION") return "preparation";
  if (value === "DADODEBAJA" || value === "DADOSDEBAJA") return "decommissioned";
  return "unknown";
}

export function OperationalStatusBadge({ code, name }: { code?: string | null; name?: string | null }) {
  const status = resolveOperationalStatusVisual(code, name);
  const label = status === "unknown" ? name?.trim() || code?.trim() || "NA" : labels[status];
  return <span className={`inline-flex max-w-full truncate rounded-full px-2 py-1 text-xs font-semibold ${classes[status]}`} data-status={status} title={code ?? name ?? undefined}>{label}</span>;
}