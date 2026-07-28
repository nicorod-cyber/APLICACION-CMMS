import { documentStatusLabel } from "./documentFormTypes";
export function DocumentStatusBadge({ status }: { status: string }) {
  const normalized = status.trim().replace(/_/g, "").toLowerCase();
  const className = ["validado", "vigente"].includes(normalized) ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200" : ["porvencer", "pendientevalidacion"].includes(normalized) ? "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200" : ["vencido", "rechazado"].includes(normalized) ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200" : "bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-200";
  return <span className={"rounded-full px-2 py-1 text-xs font-semibold " + className}>{documentStatusLabel(status)}</span>;
}
