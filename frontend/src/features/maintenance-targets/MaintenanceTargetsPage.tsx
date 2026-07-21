import { useEffect, useMemo, useState } from "react";
import { RefreshCw } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";
import { labelFor, type MaintenanceTarget } from "./MaintenanceTargetSelect";

export function MaintenanceTargetsPage() {
  const [items, setItems] = useState<MaintenanceTarget[]>([]);
  const [search, setSearch] = useState("");
  const [faenaCodigo, setFaenaCodigo] = useState("");
  const [includeMounted, setIncludeMounted] = useState(false);
  const [includeDecommissioned, setIncludeDecommissioned] = useState(false);
  const [loading, setLoading] = useState(true);

  async function load() {
    setLoading(true);
    const query = new URLSearchParams({
      scope: includeMounted ? "All" : "Operational",
      incluirDadosDeBaja: String(includeDecommissioned)
    });
    if (faenaCodigo) query.set("faenaCodigo", faenaCodigo);
    if (search.trim()) query.set("search", search.trim());
    try {
      setItems(await apiFetch<MaintenanceTarget[]>(`/api/maintenance-targets?${query}`));
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, [faenaCodigo, includeMounted, includeDecommissioned]);

  const operational = useMemo(() => items.filter((item) => includeMounted || !item.esComponenteMontado), [items, includeMounted]);
  return (
    <section className="stack">
      <header className="page-header">
        <div>
          <p className="eyebrow">Consulta unificada</p>
          <h1>Equipos operacionales</h1>
          <p>Una fila por activo independiente o por unidad operativa compuesta.</p>
        </div>
        <button className="secondary-button" onClick={() => void load()} type="button"><RefreshCw size={18} /> Actualizar</button>
      </header>
      <section className="panel stack">
        <div className="toolbar">
          <input value={search} onChange={(event) => setSearch(event.target.value)} onKeyDown={(event) => event.key === "Enter" && void load()} placeholder="Buscar equipo, tipo o faena" />
          <button className="secondary-button" onClick={() => void load()} type="button">Buscar</button>
          <label className="check-row"><input type="checkbox" checked={includeMounted} onChange={(event) => setIncludeMounted(event.target.checked)} /> Incluir componentes montados</label>
          <label className="check-row"><input type="checkbox" checked={includeDecommissioned} onChange={(event) => setIncludeDecommissioned(event.target.checked)} /> Incluir dados de baja</label>
        </div>
        <FaenaSelect value={faenaCodigo} onChange={setFaenaCodigo} />
        <div className="data-table"><table><thead><tr><th>Equipo</th><th>Representación</th><th>Tipo</th><th>Faena</th><th>Estado</th><th>Criticidad</th><th>Composición</th></tr></thead><tbody>
          {operational.map((item) => <tr key={`${item.tipo}:${item.codigo}`}><td><strong>{item.nombre}</strong><small>{item.esComponenteMontado ? `Componente de ${item.unidadOperativaVigenteNombre}` : labelFor(item)}</small></td><td>{item.esComposicion ? "Unidad compuesta" : item.esComponenteMontado ? "Componente físico" : "Activo individual"}</td><td>{item.categoriaNombre}</td><td>{item.faenaNombre ?? item.faenaCodigo ?? "-"}</td><td>{item.estadoOperacionalNombre}</td><td>{item.criticidad ?? "-"}</td><td>{item.esComposicion ? item.composicionCompleta ? "Completa" : "Incompleta" : item.esComponenteMontado ? item.rolComponenteVigente ?? "Montado" : "Independiente"}</td></tr>)}
          {!operational.length ? <tr><td colSpan={7}><small>{loading ? "Cargando equipos…" : "No hay equipos para los filtros seleccionados."}</small></td></tr> : null}
        </tbody></table></div>
      </section>
    </section>
  );
}
