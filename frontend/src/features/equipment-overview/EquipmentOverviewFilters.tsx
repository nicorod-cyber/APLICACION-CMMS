import { Search, SlidersHorizontal } from "lucide-react";
import { useState } from "react";
import { FaenaSelect } from "../faenas/FaenaSelect";
import type { AssetCatalog, EquipmentOverviewFiltersValue } from "./types";

export function EquipmentOverviewFilters({ value, catalog, onChange, onApply, disabled }: {
  value: EquipmentOverviewFiltersValue;
  catalog: AssetCatalog;
  onChange: (value: EquipmentOverviewFiltersValue) => void;
  onApply: () => void;
  disabled: boolean;
}) {
  const [advanced, setAdvanced] = useState(false);
  return <div className="border-b border-slate-200 p-4">
    <div className="grid gap-2 lg:grid-cols-[minmax(260px,1.3fr)_220px_180px_180px_auto]">
      <label className="relative"><Search className="pointer-events-none absolute left-3 top-3 h-4 w-4 text-slate-400" /><input className="input bg-slate-50 pl-9" placeholder="Buscar por nombre, código, patente o serie" value={value.search} onChange={event => onChange({ ...value, search: event.target.value })} onKeyDown={event => { if (event.key === "Enter") onApply(); }} /></label>
      <FaenaSelect emptyLabel="Todas las faenas" includeInactive={false} value={value.faenaCodigo} onChange={faenaCodigo => onChange({ ...value, faenaCodigo })} />
      <select className="input" value={value.tipoActivoCodigo} onChange={event => onChange({ ...value, tipoActivoCodigo: event.target.value })}><option value="">Todos los tipos</option>{catalog.tiposActivo.map(type => <option key={type.codigo} value={type.codigo}>{type.nombre}</option>)}</select>
      <select className="input" value={value.estadoOperacionalCodigo} onChange={event => onChange({ ...value, estadoOperacionalCodigo: event.target.value })}><option value="">Todos los estados</option>{catalog.estadosOperacionales.map(state => <option key={state.codigo} value={state.codigo}>{state.nombre}</option>)}</select>
      <button className="secondary-button" type="button" onClick={() => setAdvanced(open => !open)}><SlidersHorizontal className="h-4 w-4" />Filtros avanzados</button>
    </div>
    {advanced ? <div className="mt-3 grid gap-2 border-t border-slate-100 pt-3 md:grid-cols-4"><input className="input" placeholder="Zona" value={value.zona} onChange={event => onChange({ ...value, zona: event.target.value })} /><select className="input" value={value.tipoUbicacionFisica} onChange={event => onChange({ ...value, tipoUbicacionFisica: event.target.value })}><option value="">Toda ubicación</option><option value="FAENA">En faena</option><option value="TALLER">Taller</option></select><input className="input" placeholder="Código de taller" value={value.tallerCodigo} onChange={event => onChange({ ...value, tallerCodigo: event.target.value })} /><button className="secondary-button" type="button" onClick={onApply} disabled={disabled}>Aplicar filtros</button></div> : <div className="mt-3"><button className="text-xs font-semibold text-teal-700 underline" type="button" disabled={disabled} onClick={onApply}>Aplicar filtros</button></div>}
  </div>;
}