type Metrics = { total: number; composite: number | null; loose: number | null; nonOperational: number | null; expiringDocuments: number | null };
const value = (metric: number | null) => metric === null ? "NA" : new Intl.NumberFormat("es-CL").format(metric);

export function EquipmentSummaryCards({ metrics }: { metrics: Metrics }) {
  const cards = [
    ["Equipos visibles", metrics.total, "Activos + unidades compuestas"],
    ["Camiones fábrica", metrics.composite, "Métrica global no disponible"],
    ["Componentes sueltos", metrics.loose, "Métrica global no disponible"],
    ["No operativos", metrics.nonOperational, "Métrica global no disponible"],
    ["Documentos por vencer", metrics.expiringDocuments, "Métrica global no disponible"]
  ] as const;
  return <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">{cards.map(([label, number, note]) => <article className="rounded-[10px] border border-slate-200 bg-white px-4 py-3 shadow-sm" key={label}><p className="text-xs text-slate-500">{label}</p><strong className="mt-1 block text-[21px] text-slate-900">{value(number)}</strong><p className="mt-1 text-[11px] text-teal-700">{note}</p></article>)}</div>;
}