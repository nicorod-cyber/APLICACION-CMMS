import { useMemo, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Plus, RefreshCw } from "lucide-react";
import { useNavigate } from "react-router-dom";
import { AUTH_PERMISSIONS, apiFetch, useAuthStore } from "../auth/authStore";
import type { FaenaRecord } from "./FaenaSelect";
import { FaenaActivityBadge, FaenaEditorDialog } from "./FaenaEditorDialog";

type ActivityFilter = "all" | "active" | "inactive";

export function FaenasPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const canCreate = useAuthStore(state => state.user?.permissions.includes(AUTH_PERMISSIONS.createFaenas) ?? false);
  const [search, setSearch] = useState("");
  const [activityFilter, setActivityFilter] = useState<ActivityFilter>("all");
  const [createOpen, setCreateOpen] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const faenas = useQuery({
    queryKey: ["faenas", "all"],
    queryFn: () => apiFetch<FaenaRecord[]>("/api/faenas?includeInactive=true"),
    select: records => [...records].sort((left, right) => left.nombre.localeCompare(right.nombre) || left.codigo.localeCompare(right.codigo))
  });
  const visibleFaenas = useMemo(() => (faenas.data ?? []).filter(faena => {
    if ((activityFilter === "active" && !faena.activo) || (activityFilter === "inactive" && faena.activo)) return false;
    const query = normalizeSearch(search);
    if (!query) return true;
    return [faena.codigo, faena.nombre, faena.zona, faena.cliente, faena.administradorContrato, faena.responsableNombre, faena.ubicacionTecnica?.nombre, faena.ubicacionTecnica?.codigo]
      .filter(Boolean).some(value => normalizeSearch(value!).includes(query));
  }), [activityFilter, faenas.data, search]);

  return <section className="space-y-4">
    <header className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
      <div><h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Faenas</h1><p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Selecciona una faena para consultar y registrar sus mediciones operacionales.</p></div>
      <div className="flex flex-wrap gap-2"><button className="secondary-button" onClick={() => void faenas.refetch()} type="button"><RefreshCw className="h-4 w-4" aria-hidden="true" />Actualizar</button>{canCreate ? <button className="primary-button" onClick={() => { setMessage(null); setCreateOpen(true); }} type="button"><Plus className="h-4 w-4" aria-hidden="true" />Nueva faena</button> : null}</div>
    </header>
    {message ? <Notice>{message}</Notice> : null}
    {faenas.error ? <Notice error>{faenas.error instanceof Error ? faenas.error.message : "No fue posible cargar las faenas."} <button className="underline" onClick={() => void faenas.refetch()} type="button">Reintentar</button></Notice> : null}
    <section className="panel p-4"><div className="grid gap-3 md:grid-cols-[minmax(260px,1fr)_180px_auto] md:items-end"><label className="text-sm font-medium text-slate-700 dark:text-slate-200">Buscar<input aria-label="Buscar faenas" className="input mt-2" placeholder="Código, nombre, zona, cliente, responsable o ubicación" value={search} onChange={event => setSearch(event.target.value)} /></label><label className="text-sm font-medium text-slate-700 dark:text-slate-200">Estado<select className="input mt-2" value={activityFilter} onChange={event => setActivityFilter(event.target.value as ActivityFilter)}><option value="all">Todas</option><option value="active">Activas</option><option value="inactive">Inactivas</option></select></label><p className="pb-2 text-sm text-slate-500 dark:text-slate-400">{visibleFaenas.length} resultado{visibleFaenas.length === 1 ? "" : "s"}</p></div></section>
    <section className="panel overflow-hidden"><div className="overflow-x-auto"><table className="data-table min-w-[900px]"><thead><tr><th>Estado</th><th>Nombre</th><th>Zona</th><th>Cliente</th><th>Administrador de contrato</th></tr></thead><tbody>{faenas.isLoading ? <tr><td colSpan={5} className="p-5 text-center text-sm text-slate-500">Cargando faenas…</td></tr> : visibleFaenas.length === 0 ? <tr><td colSpan={5} className="p-5 text-center text-sm text-slate-500">No hay faenas que coincidan con el filtro.</td></tr> : visibleFaenas.map(faena => <tr key={faena.id} className="cursor-pointer transition hover:bg-teal-50 focus-within:bg-teal-50 dark:hover:bg-teal-950/30 dark:focus-within:bg-teal-950/30" onClick={() => navigate(`/faenas/${encodeURIComponent(faena.codigo)}`)} onKeyDown={event => { if (event.key === "Enter" || event.key === " ") { event.preventDefault(); navigate(`/faenas/${encodeURIComponent(faena.codigo)}`); } }} tabIndex={0} aria-label={`Abrir faena ${faena.nombre}`}><td><FaenaActivityBadge active={faena.activo} /></td><td><span className="font-medium">{faena.nombre}</span><br /><span className="text-xs text-slate-500">{faena.codigo}</span></td><td>{faena.zona || "-"}</td><td>{faena.cliente || "-"}</td><td>{faena.administradorContrato || "-"}</td></tr>)}</tbody></table></div></section>
    <FaenaEditorDialog open={createOpen} mode="create" onClose={() => setCreateOpen(false)} onSaved={async saved => { setMessage("Faena creada."); queryClient.setQueryData<FaenaRecord[]>(["faenas", "all"], current => current ? [...current.filter(item => item.codigo !== saved.codigo), saved] : [saved]); }} />
  </section>;
}

function Notice({ children, error }: { children: React.ReactNode; error?: boolean }) { return <div className={`rounded-md border p-3 text-sm ${error ? "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200" : "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200"}`}>{children}</div>; }
const normalizeSearch = (value: string) => value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase().trim();
