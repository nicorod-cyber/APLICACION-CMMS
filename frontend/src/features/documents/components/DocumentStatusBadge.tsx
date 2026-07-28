import { documentStatusLabel } from "./documentFormTypes";
export function DocumentStatusBadge({ status }: { status: string }) {
  const className = status === "Vencido" ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200" : status === "PorVencer" ? "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200" : status === "Vigente" ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200" : status === "Rechazado" || status === "Anulado" ? "bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-200" : "bg-sky-50 text-sky-700 dark:bg-sky-950 dark:text-sky-200";
  return <span className={"rounded-full px-2 py-1 text-xs font-semibold " + className}>{documentStatusLabel(status)}</span>;
}
