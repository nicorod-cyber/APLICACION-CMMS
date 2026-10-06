import { FormEvent, useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiError, apiFetch } from "../auth/authStore";

export type NoticeStatus = "Borrador" | "PendientePlanificacion" | "DevueltoFaena" | "EnGestion" | "Cerrado" | "Rechazado" | "Anulado";
export type Notice = { avisoId: string; estado: NoticeStatus; faenaCodigo: string; activoCodigo?: string | null; unidadOperativaCodigo?: string | null; fechaDeteccion: string; trabajos: { id: string }[] };
type Asset = { codigo: string; nombre: string; faenaCodigo: string };
type Unit = { codigo: string; nombre: string; faenaCodigo?: string | null };
type Equipment = { value: string; kind: "asset" | "unit"; codigo: string; nombre: string; faenaCodigo?: string | null };
type DraftItem = { descripcion: string; observaciones: string; rolComponente: "Fabrica" | "Chasis" | "" };

const statusLabel: Record<NoticeStatus, string> = { Borrador: "Borrador", PendientePlanificacion: "Pendiente planificación", DevueltoFaena: "Devuelto a faena", EnGestion: "En gestión", Cerrado: "Cerrado", Rechazado: "Rechazado", Anulado: "Anulado" };

export function WorkNotificationsPage() {
  const navigate = useNavigate();
  const [rows, setRows] = useState<Notice[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [units, setUnits] = useState<Unit[]>([]);
  const [filter, setFilter] = useState({ text: "", status: "", faena: "", equipo: "" });
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [form, setForm] = useState({ faenaCodigo: "", activoCodigo: "", unidadOperativaCodigo: "", fechaDeteccion: "", estadoOperacional: "Operativo", fueraServicioDesde: "", restriccionOperacional: "", lecturaMedidor: "", observaciones: "", trabajos: [{ descripcion: "", observaciones: "", rolComponente: "" } as DraftItem] });

  const load = async () => {
    try {
      setError(null);
      const query = new URLSearchParams();
      if (filter.text) query.set("texto", filter.text);
      if (filter.status) query.set("status", filter.status);
      if (filter.faena) query.set("faenaCodigo", filter.faena);
      if (filter.equipo) query.set("equipoCodigo", filter.equipo);
      setRows(await apiFetch<Notice[]>(`/api/work-notifications/?${query}`));
    } catch (e) { setError(e instanceof Error ? e.message : "No fue posible cargar avisos."); }
  };
  useEffect(() => { void load(); }, []);
  useEffect(() => { void Promise.all([apiFetch<{ items: Asset[] }>("/api/assets?page=1&pageSize=100"), apiFetch<{ items: Unit[] }>("/api/operational-units?page=1&pageSize=100")]).then(([a, u]) => { setAssets(a.items); setUnits(u.items); }); }, []);

  const equipment: Equipment[] = [...assets.map(a => ({ value: `asset:${a.codigo}`, kind: "asset" as const, codigo: a.codigo, nombre: a.nombre, faenaCodigo: a.faenaCodigo })), ...units.map(u => ({ value: `unit:${u.codigo}`, kind: "unit" as const, codigo: u.codigo, nombre: u.nombre, faenaCodigo: u.faenaCodigo }))].sort((a, b) => a.nombre.localeCompare(b.nombre, "es"));
  const selectedEquipment = form.activoCodigo ? `asset:${form.activoCodigo}` : form.unidadOperativaCodigo ? `unit:${form.unidadOperativaCodigo}` : "";
  const selectedIsUnit = form.unidadOperativaCodigo.length > 0;
  const selectEquipment = (value: string) => {
    const selected = equipment.find(item => item.value === value);
    setForm({ ...form, activoCodigo: selected?.kind === "asset" ? selected.codigo : "", unidadOperativaCodigo: selected?.kind === "unit" ? selected.codigo : "", faenaCodigo: selected?.faenaCodigo ?? "" });
  };
  const updateItem = (index: number, changes: Partial<DraftItem>) => setForm({ ...form, trabajos: form.trabajos.map((item, i) => i === index ? { ...item, ...changes } : item) });
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (saving) return;
    try {
      setSaving(true);
      const created = await apiFetch<Notice>("/api/work-notifications/", { method: "POST", body: JSON.stringify({ ...form, fechaDeteccion: form.fechaDeteccion ? new Date(form.fechaDeteccion).toISOString() : null, fueraServicioDesde: form.fueraServicioDesde ? new Date(form.fueraServicioDesde).toISOString() : null, lecturaMedidor: form.lecturaMedidor ? Number(form.lecturaMedidor) : null, trabajos: form.trabajos.map(item => ({ ...item, rolComponente: item.rolComponente || null })) }) });
      navigate(`/avisos/${encodeURIComponent(created.avisoId)}`);
    } catch (e) { setError(e instanceof ApiError && e.status >= 500 ? "No fue posible guardar el borrador. Intenta nuevamente más tarde." : e instanceof Error ? e.message : "No fue posible guardar el borrador."); }
    finally { setSaving(false); }
  };

  return <section className="space-y-4">
    <header className="flex items-end justify-between"><div><h1 className="text-2xl font-semibold">Avisos</h1><p className="text-sm text-slate-500 dark:text-slate-400">Trabajos y fallas levantados por faena.</p></div><button className="primary-button" onClick={() => setCreating(true)}>+ Nuevo Aviso</button></header>
    {error && <p className="text-sm text-red-700 dark:text-red-300">{error}</p>}
    <section className="panel p-4"><div className="grid gap-3 md:grid-cols-4"><input className="input" placeholder="Buscar número o trabajo" value={filter.text} onChange={e => setFilter({ ...filter, text: e.target.value })}/><select className="input" value={filter.status} onChange={e => setFilter({ ...filter, status: e.target.value })}><option value="">Todos los estados</option>{Object.entries(statusLabel).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select><input className="input" placeholder="Faena" value={filter.faena} onChange={e => setFilter({ ...filter, faena: e.target.value })}/><button className="secondary-button" onClick={() => void load()}>Aplicar filtros</button></div></section>
    <section className="panel overflow-x-auto"><table className="data-table min-w-[720px]"><thead><tr><th>Número Aviso</th><th>Equipo</th><th>Faena</th><th>Estado</th><th>Fecha</th><th>Trabajos</th></tr></thead><tbody>{rows.map(n => { const code = n.activoCodigo ?? n.unidadOperativaCodigo; const equipmentName = equipment.find(item => item.codigo === code)?.nombre ?? code ?? "-"; return <tr key={n.avisoId}><td><Link className="text-teal-700 underline dark:text-teal-300" to={`/avisos/${encodeURIComponent(n.avisoId)}`}>{n.avisoId}</Link></td><td>{equipmentName}</td><td>{n.faenaCodigo}</td><td>{statusLabel[n.estado]}</td><td>{new Date(n.fechaDeteccion).toLocaleDateString()}</td><td>{n.trabajos.length}</td></tr>; })}{rows.length === 0 && <tr><td colSpan={6}>No hay avisos que coincidan.</td></tr>}</tbody></table></section>
    {creating && <div className="fixed inset-0 z-50 overflow-auto bg-slate-950/60 p-4 text-slate-900 dark:bg-black/70 dark:text-slate-100 sm:p-6"><form className="mx-auto max-w-3xl rounded-xl border border-slate-200 bg-white p-5 shadow-2xl dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100 sm:p-6" onSubmit={submit}><h2 className="text-xl font-semibold text-slate-950 dark:text-white">Nuevo Aviso</h2><div className="mt-4 grid gap-3 md:grid-cols-2">
      <label className="text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">Equipo<select required className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" value={selectedEquipment} onChange={e => selectEquipment(e.target.value)}><option value="">Seleccione equipo</option>{equipment.map(item => <option key={item.value} value={item.value}>{item.nombre}</option>)}</select></label>
      <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Fecha detección<input className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" type="datetime-local" value={form.fechaDeteccion} onChange={e => setForm({ ...form, fechaDeteccion: e.target.value })}/></label>
      <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Condición operacional<select className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" value={form.estadoOperacional} onChange={e => setForm({ ...form, estadoOperacional: e.target.value })}><option value="Operativo">Operativo</option><option value="OperativoConAlerta">Operativo con alerta</option><option value="FueraDeServicio">Fuera de servicio</option></select></label>
      {form.estadoOperacional === "FueraDeServicio" && <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Fuera de servicio desde<input required className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" type="datetime-local" value={form.fueraServicioDesde} onChange={e => setForm({ ...form, fueraServicioDesde: e.target.value })}/></label>}
      {form.estadoOperacional === "OperativoConAlerta" && <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Restricción operacional<input required className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" value={form.restriccionOperacional} onChange={e => setForm({ ...form, restriccionOperacional: e.target.value })}/></label>}
      <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Lectura (si aplica)<input className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" type="number" min="0" value={form.lecturaMedidor} onChange={e => setForm({ ...form, lecturaMedidor: e.target.value })}/></label><label className="text-sm font-medium text-slate-700 dark:text-slate-200">Observaciones<textarea className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" value={form.observaciones} onChange={e => setForm({ ...form, observaciones: e.target.value })}/></label></div>
      <h3 className="mt-5 font-semibold text-slate-900 dark:text-white">Trabajos</h3>{form.trabajos.map((item, index) => <div key={index} className="mt-2 grid gap-2 rounded border border-slate-200 p-3 dark:border-slate-700 md:grid-cols-3"><input required className="input dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" placeholder="Descripción de falla / trabajo requerido" value={item.descripcion} onChange={e => updateItem(index, { descripcion: e.target.value })}/><input className="input dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" placeholder="Observaciones" value={item.observaciones} onChange={e => updateItem(index, { observaciones: e.target.value })}/>{selectedIsUnit ? <select required className="input dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" value={item.rolComponente} onChange={e => updateItem(index, { rolComponente: e.target.value as DraftItem["rolComponente"] })}><option value="">Fábrica o chasis</option><option value="Fabrica">FÁBRICA</option><option value="Chasis">CHASIS</option></select> : <button type="button" className="secondary-button" disabled={form.trabajos.length === 1} onClick={() => setForm({ ...form, trabajos: form.trabajos.filter((_, i) => i !== index) })}>Eliminar</button>}</div>)}
      <button type="button" className="secondary-button mt-3" onClick={() => setForm({ ...form, trabajos: [...form.trabajos, { descripcion: "", observaciones: "", rolComponente: "" }] })}>+ Agregar trabajo</button><div className="mt-5 flex gap-2"><button className="primary-button" disabled={saving}>{saving ? "Guardando…" : "Guardar borrador"}</button><button className="secondary-button" type="button" disabled={saving} onClick={() => setCreating(false)}>Cancelar</button></div></form></div>}
  </section>;
}
