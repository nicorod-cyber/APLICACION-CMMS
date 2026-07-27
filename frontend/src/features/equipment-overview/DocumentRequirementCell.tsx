import { documentLabel } from "./formatters";
import type { DocumentRequirementStatus } from "./types";

const colors: Record<string, string> = {
  Vigente: "bg-emerald-100 text-emerald-800",
  PorVencer: "bg-amber-100 text-amber-800",
  Vencido: "bg-red-100 text-red-800",
  Pendiente: "bg-slate-100 text-slate-700",
  PendienteCarga: "bg-slate-100 text-slate-700",
  PendienteValidacion: "bg-amber-100 text-amber-800",
  Rechazado: "bg-red-100 text-red-800",
  "No aplica": "bg-slate-100 text-slate-600",
  NA: "bg-slate-100 text-slate-500"
};

export function DocumentRequirementCell({ value }: { value?: DocumentRequirementStatus | null }) {
  const label = documentLabel(value);
  const title = value?.expirationDate ? label + " · vence " + value.expirationDate : label;
  return <span className={"inline-flex whitespace-nowrap rounded px-2 py-1 text-xs font-medium " + (colors[label] || "bg-slate-100 text-slate-700")} title={title}>{label}</span>;
}