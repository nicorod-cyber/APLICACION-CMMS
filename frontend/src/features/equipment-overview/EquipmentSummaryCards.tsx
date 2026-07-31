type Metrics = { total: number | null; nonOperational: number | null; expiringDocuments: number | null };
const value = (metric: number | null, unavailable: boolean) => metric === null ? (unavailable ? "No disponible" : "—") : new Intl.NumberFormat("es-CL").format(metric);

export function EquipmentSummaryCards({ metrics, unavailable = false }: { metrics: Metrics; unavailable?: boolean }) {
  const cards = [
    ["Equipos visibles", metrics.total, "Activos + unidades compuestas"],
    ["No operativos", metrics.nonOperational, "Excluye equipos dados de baja"],
    ["Documentos por vencer", metrics.expiringDocuments, "Dentro de su ventana de alerta"]
  ] as const;
  return <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">{cards.map(([label, number, note]) => <article className="rounded-[10px] border border-slate-200 bg-white px-4 py-3 shadow-sm" key={label}><p className="text-xs text-slate-500">{label}</p><strong className="mt-1 block text-[21px] text-slate-900">{value(number, unavailable)}</strong><p className="mt-1 text-[11px] text-teal-700">{note}</p></article>)}</div>;
}