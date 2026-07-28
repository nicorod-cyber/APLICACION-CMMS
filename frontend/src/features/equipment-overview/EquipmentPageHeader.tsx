import { Plus } from "lucide-react";

export function EquipmentPageHeader({ onNew }: { onNew: () => void }) {
  return <header className="flex flex-wrap items-start justify-between gap-4">
    <div><p className="text-[11px] font-extrabold uppercase tracking-[.12em] text-teal-700">Gestion unificada</p><h1 className="mt-1 text-[26px] font-semibold text-slate-900">Equipos</h1><p className="mt-1 max-w-3xl text-sm text-slate-500">Consulta activos independientes y unidades compuestas en una sola vista.</p></div>
    <div className="flex flex-wrap gap-2"><button className="primary-button" type="button" onClick={onNew}><Plus className="h-4 w-4" />Nuevo activo</button></div>
  </header>;
}