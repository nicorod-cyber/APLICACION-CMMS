import type { DocumentRequirementStatus } from "./types";

export type DocumentVisualState = "valid" | "expiring" | "expired" | "missing" | "pending-validation" | "rejected" | "not-applicable" | "unknown";

const visualClasses: Record<DocumentVisualState, string> = {
  valid: "border border-emerald-200 bg-emerald-100 text-emerald-800 dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-200",
  expiring: "border border-amber-200 bg-amber-100 text-amber-800 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-200",
  expired: "border border-red-200 bg-red-100 text-red-800 dark:border-red-800 dark:bg-red-950 dark:text-red-200",
  missing: "border border-red-200 bg-red-50 text-red-800 dark:border-red-800 dark:bg-red-950 dark:text-red-200",
  "pending-validation": "border border-blue-200 bg-blue-50 text-blue-800 dark:border-blue-800 dark:bg-blue-950 dark:text-blue-200",
  rejected: "border border-red-200 bg-red-100 text-red-800 dark:border-red-800 dark:bg-red-950 dark:text-red-200",
  "not-applicable": "border border-slate-200 bg-slate-100 text-slate-600 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-300",
  unknown: "border border-slate-200 bg-slate-100 text-slate-500 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-400"
};

const labels: Record<DocumentVisualState, string> = {
  valid: "Vigente", expiring: "Por vencer", expired: "Vencido", missing: "Pendiente de carga",
  "pending-validation": "Pendiente de validaci\u00f3n", rejected: "Rechazado", "not-applicable": "No aplica", unknown: "NA"
};

const normalizedStatus = (value?: string | null) => value?.trim().toUpperCase().replace(/[\s_-]/g, "") || "";

export function resolveDocumentVisualState(value?: DocumentRequirementStatus | null): DocumentVisualState {
  if (!value) return "unknown";
  if (value.applies === false) return "not-applicable";
  const status = normalizedStatus(value.status);
  if (status === "RECHAZADO") return "rejected";
  if (status === "VENCIDO" || (value.daysUntilExpiration ?? 0) < 0) return "expired";
  if (status === "PENDIENTECARGA") return "missing";
  if (status === "PENDIENTEVALIDACION") return "pending-validation";
  if (value.daysUntilExpiration !== null && value.daysUntilExpiration !== undefined && value.daysUntilExpiration >= 0 && value.daysUntilExpiration <= 30) return "expiring";
  if (status === "VIGENTE") return "valid";
  if (status === "PORVENCER") return "expiring";
  return "unknown";
}

function daysLabel(days?: number | null) {
  if (days === null || days === undefined) return null;
  if (days > 1) return `${days} d\u00edas`;
  if (days === 1) return "1 d\u00eda";
  if (days === 0) return "Vence hoy";
  return days === -1 ? "Vencido hace 1 d\u00eda" : `Vencido hace ${Math.abs(days)} d\u00edas`;
}

function dateLabel(value: DocumentRequirementStatus) {
  if (!value.expirationDate) return null;
  const formatted = new Intl.DateTimeFormat("es-CL").format(new Date(`${value.expirationDate}T00:00:00`));
  return `${value.daysUntilExpiration !== null && value.daysUntilExpiration !== undefined && value.daysUntilExpiration < 0 ? "Venci\u00f3" : "Vence"}: ${formatted}`;
}

export function DocumentRequirementCell({ value }: { value?: DocumentRequirementStatus | null }) {
  const state = resolveDocumentVisualState(value);
  const label = labels[state];
  const date = value ? dateLabel(value) : null;
  const days = value?.applies === true ? daysLabel(value.daysUntilExpiration) : null;
  const title = [label, date, days].filter(Boolean).join(" \u00b7 ");
  return <div data-state={state} className={`flex w-full min-w-0 max-w-none break-words flex-col rounded px-2 py-1 text-xs leading-4 ${visualClasses[state]}`} title={title}>
    <span className="font-medium">{label}</span>
    {date ? <span className="text-[11px] opacity-90">{date}</span> : null}
    {days ? <span className="text-[11px] opacity-90">{days}</span> : null}
  </div>;
}