import { FormEvent, useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ApiError, apiFetch } from "../auth/authStore";

export type NoticeStatus = "Borrador" | "PendientePlanificacion" | "DevueltoFaena" | "EnGestion" | "Cerrado" | "Rechazado" | "Anulado";
export type Notice = { avisoId: string; estado: NoticeStatus; faenaCodigo: string; activoCodigo?: string | null; unidadOperativaCodigo?: string | null; fechaDeteccion: string; trabajos: { id: string; secuencia?: number }[] };
type Asset = { codigo: string; nombre: string; faenaCodigo: string };
type Unit = { codigo: string; nombre: string; faenaCodigo?: string | null };
type Equipment = { value: string; kind: "asset" | "unit"; codigo: string; nombre: string; faenaCodigo?: string | null };
export type TechnicalNode = { id?: string | null; codigo: string; nombre: string; nivel: "Sistema" | "Subsistema" | "Componente" | "Subcomponente"; codigoPadre?: string | null; faenaCodigo?: string | null };
type AssetAttribute = { definicionCodigo: string; nombre: string; valorTexto?: string | null; valorNumerico?: number | null };
type AssetDetail = { resumen: { tipoActivoNombre?: string | null; tipoMedicionUso?: string | null; faenaNombre?: string | null }; marca?: string | null; modelo?: string | null; numeroSerie?: string | null; anioFabricacion?: number | null; atributos?: AssetAttribute[] };
type UnitDetail = { tipoUnidadNombre?: string | null; faenaNombre?: string | null; unidadLectura?: string | null; composicion: { vigentes: Array<{ activoCodigo: string; rolComponenteCodigo: string }> } };
type EquipmentInfo = { ppu: string; tipo: string; marca: string; modelo: string; anio: string; vin: string; faena: string; measurement: "hours" | "kilometers" | null; composite: boolean };
type EvidenceStatus = "pending" | "uploading" | "uploaded" | "error";
type DraftEvidence = { key: string; file: File; status: EvidenceStatus; error?: string };
export type DraftItem = { key: string; descripcion: string; observaciones: string; rolComponente: "Fabrica" | "Chasis" | ""; technicalSystemId: string; technicalSubsystemId: string; technicalComponentId: string; evidencias: DraftEvidence[] };

const statusLabel: Record<NoticeStatus, string> = { Borrador: "Borrador", PendientePlanificacion: "Pendiente planificación", DevueltoFaena: "Devuelto a faena", EnGestion: "En gestión", Cerrado: "Cerrado", Rechazado: "Rechazado", Anulado: "Anulado" };
let draftSequence = 0;
export const createDraftItem = (): DraftItem => ({ key: `work-${++draftSequence}`, descripcion: "", observaciones: "", rolComponente: "", technicalSystemId: "", technicalSubsystemId: "", technicalComponentId: "", evidencias: [] });
const initialForm = () => ({ faenaCodigo: "", activoCodigo: "", unidadOperativaCodigo: "", fechaDeteccion: "", estadoOperacional: "Operativo", fueraServicioDesde: "", restriccionOperacional: "", lecturaMedidor: "", observaciones: "", trabajos: [createDraftItem()] });

export function WorkNotificationsPage() {
  const navigate = useNavigate();
  const [rows, setRows] = useState<Notice[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [units, setUnits] = useState<Unit[]>([]);
  const [technicalNodes, setTechnicalNodes] = useState<TechnicalNode[]>([]);
  const [filter, setFilter] = useState({ text: "", status: "", faena: "", equipo: "" });
  const [error, setError] = useState<string | null>(null);
  const [creating, setCreating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [createdDraft, setCreatedDraft] = useState<Notice | null>(null);
  const [equipmentInfo, setEquipmentInfo] = useState<EquipmentInfo | null>(null);
  const [loadingEquipmentInfo, setLoadingEquipmentInfo] = useState(false);
  const [form, setForm] = useState(initialForm);

  const load = async () => {
    try {
      setError(null);
      const query = new URLSearchParams();
      if (filter.text) query.set("texto", filter.text);
      if (filter.status) query.set("status", filter.status);
      if (filter.faena) query.set("faenaCodigo", filter.faena);
      if (filter.equipo) query.set("equipoCodigo", filter.equipo);
      setRows(await apiFetch<Notice[]>(`/api/work-notifications/?${query}`));
    } catch (reason) { setError(messageOf(reason, "No fue posible cargar avisos.")); }
  };

  useEffect(() => { void load(); }, []);
  useEffect(() => {
    void Promise.all([apiFetch<{ items: Asset[] }>("/api/assets?page=1&pageSize=100"), apiFetch<{ items: Unit[] }>("/api/operational-units?page=1&pageSize=100")])
      .then(([assetResult, unitResult]) => { setAssets(assetResult.items); setUnits(unitResult.items); })
      .catch(reason => setError(messageOf(reason, "No fue posible cargar los equipos.")));
  }, []);
  useEffect(() => {
    if (!creating || technicalNodes.length > 0) return;
    void apiFetch<TechnicalNode[]>("/api/technical-hierarchy/nodes?includeObsolete=false").then(setTechnicalNodes).catch(reason => setError(messageOf(reason, "No fue posible cargar la jerarquía técnica.")));
  }, [creating, technicalNodes.length]);

  const equipment: Equipment[] = useMemo(() => [...assets.map(asset => ({ value: `asset:${asset.codigo}`, kind: "asset" as const, codigo: asset.codigo, nombre: asset.nombre, faenaCodigo: asset.faenaCodigo })), ...units.map(unit => ({ value: `unit:${unit.codigo}`, kind: "unit" as const, codigo: unit.codigo, nombre: unit.nombre, faenaCodigo: unit.faenaCodigo }))].sort((left, right) => left.nombre.localeCompare(right.nombre, "es")), [assets, units]);
  const selectedEquipment = form.activoCodigo ? `asset:${form.activoCodigo}` : form.unidadOperativaCodigo ? `unit:${form.unidadOperativaCodigo}` : "";
  const selectedIsComposite = Boolean(form.unidadOperativaCodigo && equipmentInfo?.composite);

  useEffect(() => {
    let active = true;
    async function loadInfo() {
      if (!form.activoCodigo && !form.unidadOperativaCodigo) { setEquipmentInfo(null); return; }
      setLoadingEquipmentInfo(true);
      try {
        if (form.activoCodigo) {
          const detail = await apiFetch<AssetDetail>(`/api/assets/${encodeURIComponent(form.activoCodigo)}`);
          if (active) setEquipmentInfo(assetInfo(detail, form.faenaCodigo));
        } else {
          const unit = await apiFetch<UnitDetail>(`/api/operational-units/${encodeURIComponent(form.unidadOperativaCodigo)}`);
          const chassis = unit.composicion.vigentes.find(component => component.rolComponenteCodigo.toUpperCase() === "CHASIS");
          const detail = chassis ? await apiFetch<AssetDetail>(`/api/assets/${encodeURIComponent(chassis.activoCodigo)}`) : null;
          if (active) setEquipmentInfo(unitInfo(unit, detail, form.faenaCodigo));
        }
      } catch (reason) {
        if (active) { setEquipmentInfo(null); setError(messageOf(reason, "No fue posible cargar la ficha del equipo.")); }
      } finally { if (active) setLoadingEquipmentInfo(false); }
    }
    void loadInfo();
    return () => { active = false; };
  }, [form.activoCodigo, form.unidadOperativaCodigo, form.faenaCodigo]);

  const selectEquipment = (value: string) => {
    const selected = equipment.find(item => item.value === value);
    setEquipmentInfo(null);
    setForm(current => ({ ...current, activoCodigo: selected?.kind === "asset" ? selected.codigo : "", unidadOperativaCodigo: selected?.kind === "unit" ? selected.codigo : "", faenaCodigo: selected?.faenaCodigo ?? "", lecturaMedidor: "", trabajos: current.trabajos.map(item => ({ ...item, rolComponente: "", technicalSystemId: "", technicalSubsystemId: "", technicalComponentId: "" })) }));
  };
  const updateItem = (index: number, changes: Partial<DraftItem>) => setForm(current => ({ ...current, trabajos: current.trabajos.map((item, itemIndex) => itemIndex === index ? { ...item, ...changes } : item) }));
  const updateEvidence = (itemIndex: number, evidenceKey: string, changes: Partial<DraftEvidence>) => setForm(current => ({ ...current, trabajos: current.trabajos.map((item, index) => index !== itemIndex ? item : { ...item, evidencias: item.evidencias.map(evidence => evidence.key === evidenceKey ? { ...evidence, ...changes } : evidence) }) }));
  const addEvidence = (itemIndex: number, files: FileList | null) => {
    if (!files?.length) return;
    const evidencias = Array.from(files).map((file, index) => ({ key: `${Date.now()}-${index}-${file.name}`, file, status: "pending" as const }));
    updateItem(itemIndex, { evidencias: [...form.trabajos[itemIndex].evidencias, ...evidencias] });
  };

  const uploadEvidences = async (notice: Notice) => {
    let failures = 0;
    const persistedItems = [...notice.trabajos].sort((left, right) => (left.secuencia ?? 0) - (right.secuencia ?? 0));
    for (let itemIndex = 0; itemIndex < form.trabajos.length; itemIndex++) {
      const persistedItem = persistedItems[itemIndex];
      if (!persistedItem) { failures++; continue; }
      for (const evidence of form.trabajos[itemIndex].evidencias.filter(candidate => candidate.status !== "uploaded")) {
        updateEvidence(itemIndex, evidence.key, { status: "uploading", error: undefined });
        const body = new FormData();
        body.append("file", evidence.file, evidence.file.name);
        try {
          await apiFetch<Notice>(`/api/work-notifications/${encodeURIComponent(notice.avisoId)}/items/${encodeURIComponent(persistedItem.id)}/evidences`, { method: "POST", body });
          updateEvidence(itemIndex, evidence.key, { status: "uploaded", error: undefined });
        } catch (reason) {
          failures++;
          updateEvidence(itemIndex, evidence.key, { status: "error", error: messageOf(reason, "No fue posible cargar el archivo.") });
        }
      }
    }
    if (failures === 0) navigate(`/avisos/${encodeURIComponent(notice.avisoId)}`);
    else setError(`El aviso ${notice.avisoId} quedó guardado como borrador. ${failures} evidencia${failures === 1 ? "" : "s"} no se pudo cargar; puedes reintentar sin crear otro aviso.`);
  };

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    if (saving) return;
    try {
      setSaving(true);
      setError(null);
      if (createdDraft) { await uploadEvidences(createdDraft); return; }
      const created = await apiFetch<Notice>("/api/work-notifications/", { method: "POST", body: JSON.stringify({ ...form, fechaDeteccion: form.fechaDeteccion ? new Date(form.fechaDeteccion).toISOString() : null, fueraServicioDesde: form.fueraServicioDesde ? new Date(form.fueraServicioDesde).toISOString() : null, lecturaMedidor: form.lecturaMedidor ? Number(form.lecturaMedidor) : null, trabajos: form.trabajos.map(item => ({ descripcion: item.descripcion, observaciones: item.observaciones || null, rolComponente: selectedIsComposite ? item.rolComponente || null : null, technicalSystemId: item.technicalSystemId, technicalSubsystemId: item.technicalSubsystemId, technicalComponentId: item.technicalComponentId })) }) });
      setCreatedDraft(created);
      await uploadEvidences(created);
    } catch (reason) {
      setError(reason instanceof ApiError && reason.status >= 500 ? "No fue posible guardar el borrador. Intenta nuevamente más tarde." : messageOf(reason, "No fue posible guardar el borrador."));
    } finally { setSaving(false); }
  };

  const openCreate = () => { setForm(initialForm()); setCreatedDraft(null); setEquipmentInfo(null); setError(null); setCreating(true); };
  const siteNodes = technicalNodes.filter(node => !node.faenaCodigo || node.faenaCodigo.toUpperCase() === form.faenaCodigo.toUpperCase());
  const measurementLabel = equipmentInfo?.measurement === "hours" ? "Horómetro actual" : equipmentInfo?.measurement === "kilometers" ? "Kilometraje actual" : null;

  return <section className="space-y-4">
    <header className="flex items-end justify-between"><div><h1 className="text-2xl font-semibold">Avisos</h1><p className="text-sm text-slate-500 dark:text-slate-400">Trabajos y fallas levantados por faena.</p></div><button className="primary-button" onClick={openCreate}>+ Nuevo Aviso</button></header>
    {!creating && error && <p className="text-sm text-red-700 dark:text-red-300">{error}</p>}
    <section className="panel p-4"><div className="grid gap-3 md:grid-cols-4"><input className="input" placeholder="Buscar número o trabajo" value={filter.text} onChange={event => setFilter({ ...filter, text: event.target.value })}/><select className="input" value={filter.status} onChange={event => setFilter({ ...filter, status: event.target.value })}><option value="">Todos los estados</option>{Object.entries(statusLabel).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select><input className="input" placeholder="Faena" value={filter.faena} onChange={event => setFilter({ ...filter, faena: event.target.value })}/><button className="secondary-button" onClick={() => void load()}>Aplicar filtros</button></div></section>
    <section className="panel overflow-x-auto"><table className="data-table min-w-[720px]"><thead><tr><th>Número Aviso</th><th>Equipo</th><th>Faena</th><th>Estado</th><th>Fecha</th><th>Trabajos</th></tr></thead><tbody>{rows.map(notice => { const code = notice.activoCodigo ?? notice.unidadOperativaCodigo; const equipmentName = equipment.find(item => item.codigo === code)?.nombre ?? code ?? "-"; return <tr key={notice.avisoId}><td><Link className="text-teal-700 underline dark:text-teal-300" to={`/avisos/${encodeURIComponent(notice.avisoId)}`}>{notice.avisoId}</Link></td><td>{equipmentName}</td><td>{notice.faenaCodigo}</td><td>{statusLabel[notice.estado]}</td><td>{new Date(notice.fechaDeteccion).toLocaleDateString()}</td><td>{notice.trabajos.length}</td></tr>; })}{rows.length === 0 && <tr><td colSpan={6}>No hay avisos que coincidan.</td></tr>}</tbody></table></section>
    {creating && <div className="fixed inset-0 z-50 overflow-auto bg-slate-950/60 p-4 text-slate-900 dark:bg-black/75 dark:text-slate-100 sm:p-6"><form aria-label="Nuevo Aviso" className="mx-auto max-w-4xl rounded-xl border border-slate-200 bg-white p-5 shadow-2xl dark:border-slate-700 dark:bg-slate-900 sm:p-6" onSubmit={submit}>
      <div className="flex items-start justify-between gap-3"><div><h2 className="text-xl font-semibold text-slate-950 dark:text-white">Nuevo Aviso</h2>{createdDraft && <p className="mt-1 text-sm text-amber-700 dark:text-amber-300">Borrador {createdDraft.avisoId} creado. El formulario quedó bloqueado para evitar duplicados.</p>}</div><button aria-label="Cerrar" className="secondary-button" disabled={saving} onClick={() => setCreating(false)} type="button">×</button></div>
      {error && <p role="alert" className="mt-4 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">{error}</p>}
      <fieldset disabled={Boolean(createdDraft) || saving}><div className="mt-4 grid gap-3 md:grid-cols-2">
        <label className="text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">Equipo<select aria-label="Equipo" required className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" value={selectedEquipment} onChange={event => selectEquipment(event.target.value)}><option value="">Seleccione equipo</option>{equipment.map(item => <option key={item.value} value={item.value}>{item.nombre}</option>)}</select></label>
        {(loadingEquipmentInfo || equipmentInfo) && <EquipmentSummary info={equipmentInfo} loading={loadingEquipmentInfo}/>}
        <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Fecha detección<input className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" type="datetime-local" value={form.fechaDeteccion} onChange={event => setForm({ ...form, fechaDeteccion: event.target.value })}/></label>
        <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Condición operacional<select className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" value={form.estadoOperacional} onChange={event => setForm({ ...form, estadoOperacional: event.target.value })}><option value="Operativo">Operativo</option><option value="OperativoConAlerta">Operativo con alerta</option><option value="FueraDeServicio">Fuera de servicio</option></select></label>
        {form.estadoOperacional === "FueraDeServicio" && <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Fuera de servicio desde<input required className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" type="datetime-local" value={form.fueraServicioDesde} onChange={event => setForm({ ...form, fueraServicioDesde: event.target.value })}/></label>}
        {form.estadoOperacional === "OperativoConAlerta" && <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Restricción operacional<input required className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" value={form.restriccionOperacional} onChange={event => setForm({ ...form, restriccionOperacional: event.target.value })}/></label>}
        {measurementLabel && <label className="text-sm font-medium text-slate-700 dark:text-slate-200">{measurementLabel}<input aria-label={measurementLabel} className="input mt-1 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" type="number" min="0" step="0.01" value={form.lecturaMedidor} onChange={event => setForm({ ...form, lecturaMedidor: event.target.value })}/></label>}
        <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Observaciones<textarea className="input mt-1 min-h-20 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100" value={form.observaciones} onChange={event => setForm({ ...form, observaciones: event.target.value })}/></label>
      </div><h3 className="mt-6 text-lg font-semibold text-slate-900 dark:text-white">Trabajos</h3><div className="space-y-4">{form.trabajos.map((item, index) => <WorkItemCard key={item.key} index={index} item={item} composite={selectedIsComposite} nodes={siteNodes} onChange={changes => updateItem(index, changes)} onFiles={files => addEvidence(index, files)} onRemoveEvidence={key => updateItem(index, { evidencias: item.evidencias.filter(evidence => evidence.key !== key) })} onRemove={() => setForm(current => ({ ...current, trabajos: current.trabajos.filter((_, itemIndex) => itemIndex !== index) }))} canRemove={form.trabajos.length > 1}/>)}</div><button type="button" className="secondary-button mt-4" onClick={() => setForm(current => ({ ...current, trabajos: [...current.trabajos, createDraftItem()] }))}>+ Agregar trabajo</button></fieldset>
      <div className="mt-6 flex flex-wrap gap-2"><button className="primary-button" disabled={saving}>{saving ? "Procesando…" : createdDraft ? "Reintentar evidencias" : "Guardar borrador"}</button>{createdDraft && <button className="secondary-button" type="button" disabled={saving} onClick={() => navigate(`/avisos/${encodeURIComponent(createdDraft.avisoId)}`)}>Abrir aviso</button>}<button className="secondary-button" type="button" disabled={saving} onClick={() => setCreating(false)}>Cerrar</button></div>
    </form></div>}
  </section>;
}

function WorkItemCard({ index, item, composite, nodes, onChange, onFiles, onRemoveEvidence, onRemove, canRemove }: { index: number; item: DraftItem; composite: boolean; nodes: TechnicalNode[]; onChange: (changes: Partial<DraftItem>) => void; onFiles: (files: FileList | null) => void; onRemoveEvidence: (key: string) => void; onRemove: () => void; canRemove: boolean }) {
  const systems = nodes.filter(node => node.nivel === "Sistema" && node.id);
  const system = systems.find(node => node.id === item.technicalSystemId);
  const subsystems = nodes.filter(node => node.nivel === "Subsistema" && node.id && node.codigoPadre === system?.codigo);
  const subsystem = subsystems.find(node => node.id === item.technicalSubsystemId);
  const components = nodes.filter(node => node.nivel === "Componente" && node.id && node.codigoPadre === subsystem?.codigo);
  return <article className="mt-3 rounded-xl border border-slate-200 bg-slate-50/70 p-4 dark:border-slate-700 dark:bg-slate-950/50"><div className="flex items-center justify-between"><h4 className="font-semibold text-slate-900 dark:text-white">Trabajo {index + 1}</h4><button className="text-sm font-medium text-red-700 disabled:text-slate-400 dark:text-red-300" disabled={!canRemove} onClick={onRemove} type="button">Eliminar trabajo</button></div><div className="mt-3 grid gap-3 md:grid-cols-2">
    {composite && <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Fábrica / Chasis<select aria-label={`Fábrica o chasis trabajo ${index + 1}`} required className="input mt-1 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100" value={item.rolComponente} onChange={event => onChange({ rolComponente: event.target.value as DraftItem["rolComponente"] })}><option value="">Seleccione</option><option value="Fabrica">Fábrica</option><option value="Chasis">Chasis</option></select></label>}
    <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Sistema<select aria-label={`Sistema trabajo ${index + 1}`} required className="input mt-1 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100" value={item.technicalSystemId} onChange={event => onChange({ technicalSystemId: event.target.value, technicalSubsystemId: "", technicalComponentId: "" })}><option value="">Seleccione sistema</option>{systems.map(node => <option key={node.id!} value={node.id!}>{node.nombre}</option>)}</select></label>
    <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Subsistema<select aria-label={`Subsistema trabajo ${index + 1}`} required disabled={!item.technicalSystemId} className="input mt-1 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100" value={item.technicalSubsystemId} onChange={event => onChange({ technicalSubsystemId: event.target.value, technicalComponentId: "" })}><option value="">Seleccione subsistema</option>{subsystems.map(node => <option key={node.id!} value={node.id!}>{node.nombre}</option>)}</select></label>
    <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Componente<select aria-label={`Componente trabajo ${index + 1}`} required disabled={!item.technicalSubsystemId} className="input mt-1 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100" value={item.technicalComponentId} onChange={event => onChange({ technicalComponentId: event.target.value })}><option value="">Seleccione componente</option>{components.map(node => <option key={node.id!} value={node.id!}>{node.nombre}</option>)}</select></label>
    <label className="text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">Descripción de falla / trabajo requerido<textarea aria-label={`Descripción trabajo ${index + 1}`} required className="input mt-1 min-h-20 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100" value={item.descripcion} onChange={event => onChange({ descripcion: event.target.value })}/></label>
    <label className="text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">Observaciones<textarea aria-label={`Observaciones trabajo ${index + 1}`} className="input mt-1 min-h-16 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100" value={item.observaciones} onChange={event => onChange({ observaciones: event.target.value })}/></label>
  </div><div className="mt-4 border-t border-slate-200 pt-3 dark:border-slate-700"><p className="text-sm font-semibold text-slate-800 dark:text-slate-100">Evidencias <span className="font-normal text-slate-500 dark:text-slate-400">(opcional)</span></p><label className="secondary-button mt-2 inline-flex cursor-pointer"><span>+ Adjuntar fotografías</span><input aria-label={`Adjuntar evidencias trabajo ${index + 1}`} accept=".png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp" className="sr-only" multiple onChange={event => { onFiles(event.target.files); event.target.value = ""; }} type="file"/></label>{item.evidencias.length > 0 && <ul className="mt-3 space-y-2">{item.evidencias.map(evidence => <li className="flex items-center justify-between gap-3 rounded-md border border-slate-200 bg-white px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-900" key={evidence.key}><div className="min-w-0"><p className="truncate font-medium">{evidence.file.name}</p><p className="text-xs text-slate-500 dark:text-slate-400">{evidence.file.type || "Tipo desconocido"} · {formatBytes(evidence.file.size)} · {evidenceStatus(evidence)}</p>{evidence.error && <p role="alert" className="text-xs text-red-700 dark:text-red-300">{evidence.error}</p>}</div>{evidence.status !== "uploaded" && evidence.status !== "uploading" && <button className="text-red-700 dark:text-red-300" onClick={() => onRemoveEvidence(evidence.key)} type="button">Quitar</button>}</li>)}</ul>}</div></article>;
}

function EquipmentSummary({ info, loading }: { info: EquipmentInfo | null; loading: boolean }) {
  if (loading) return <p className="md:col-span-2 rounded-lg border border-slate-200 p-3 text-sm text-slate-500 dark:border-slate-700 dark:text-slate-400">Cargando ficha del equipo…</p>;
  if (!info) return null;
  const values = [["PPU", info.ppu], ["Tipo de equipo", info.tipo], ["Marca", info.marca], ["Modelo", info.modelo], ["Año", info.anio], ["VIN", info.vin], ["Faena", info.faena]];
  return <dl className="md:col-span-2 grid gap-2 rounded-lg border border-slate-200 bg-slate-50 p-3 sm:grid-cols-4 dark:border-slate-700 dark:bg-slate-950/60">{values.map(([label, value]) => <div key={label}><dt className="text-xs font-medium text-slate-500 dark:text-slate-400">{label}</dt><dd className="truncate text-sm font-semibold text-slate-900 dark:text-slate-100" title={value}>{value}</dd></div>)}</dl>;
}

function assetInfo(detail: AssetDetail, fallbackFaena: string): EquipmentInfo { const vin = attribute(detail, ["VIN", "NUMERO_CHASIS"]); return { ppu: attribute(detail, ["PPU", "PATENTE", "PLACA_PATENTE"]), tipo: detail.resumen.tipoActivoNombre ?? "—", marca: detail.marca ?? "—", modelo: detail.modelo ?? "—", anio: detail.anioFabricacion?.toString() ?? "—", vin: vin === "—" ? detail.numeroSerie ?? "—" : vin, faena: detail.resumen.faenaNombre ?? (fallbackFaena || "—"), measurement: measurementOf(detail.resumen.tipoMedicionUso), composite: false }; }
function unitInfo(unit: UnitDetail, chassis: AssetDetail | null, fallbackFaena: string): EquipmentInfo { const base = chassis ? assetInfo(chassis, fallbackFaena) : { ppu: "—", tipo: "—", marca: "—", modelo: "—", anio: "—", vin: "—", faena: fallbackFaena || "—", measurement: null, composite: false }; return { ...base, tipo: unit.tipoUnidadNombre ?? base.tipo, faena: unit.faenaNombre ?? base.faena, measurement: measurementOf(unit.unidadLectura) ?? base.measurement, composite: unit.composicion.vigentes.some(component => ["FABRICA", "CHASIS"].includes(component.rolComponenteCodigo.toUpperCase())) }; }
function attribute(detail: AssetDetail, codes: string[]) { const result = detail.atributos?.find(item => codes.includes(item.definicionCodigo.toUpperCase()) || codes.some(code => item.nombre.toUpperCase().includes(code.replace("_", " ")))); return result?.valorTexto ?? (result?.valorNumerico === undefined || result.valorNumerico === null ? "—" : String(result.valorNumerico)); }
export function measurementOf(value?: string | null): "hours" | "kilometers" | null { const normalized = value?.trim().toUpperCase() ?? ""; if (normalized.includes("HOR") || normalized.includes("HORA")) return "hours"; if (normalized.includes("KIL") || normalized === "KM") return "kilometers"; return null; }
function evidenceStatus(evidence: DraftEvidence) { return evidence.status === "uploading" ? "Cargando…" : evidence.status === "uploaded" ? "Cargada" : evidence.status === "error" ? "Error de carga" : "Pendiente"; }
function formatBytes(bytes: number) { if (bytes < 1024) return `${bytes} B`; if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`; return `${(bytes / (1024 * 1024)).toFixed(1)} MB`; }
function messageOf(reason: unknown, fallback: string) { return reason instanceof Error ? reason.message : fallback; }
