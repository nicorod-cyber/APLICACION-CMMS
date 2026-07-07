import { FormEvent, DragEvent, useEffect, useMemo, useState } from "react";
import { AlertTriangle, CalendarDays, GitBranch, KanbanSquare, RefreshCw, Save, Wrench } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";

type ScheduleViewMode = "Diario" | "Semanal" | "Mensual";
type ScheduleItemStatus = "Programado" | "EnProceso" | "Atrasado" | "EsperandoCupo" | "Completado";
type ScheduleAlertType = "TallerSobrecargado" | "OTVencida" | "ProgramacionExcedeCapacidad" | "EquipoEsperandoCupo" | "TrabajoCriticoAtrasado";

type Workshop = {
  tallerCodigo: string;
  nombre: string;
  faenaCodigo: string;
  capacidadDiariaHH: number;
  capacidadEquipos: number;
  horario: string;
  especialidad: string;
  activo: boolean;
};

type ScheduleItem = {
  programacionId: string;
  numeroOT: string;
  tallerCodigo: string;
  tallerNombre: string;
  faenaCodigo: string;
  activoCodigo: string;
  activoNombre?: string | null;
  tecnicoUserId?: string | null;
  fechaInicio: string;
  fechaFin: string;
  hhEstimadas: number;
  estado: ScheduleItemStatus;
  prioridad: string;
  criticidad: string;
  descripcion: string;
};

type WorkshopLoad = {
  tallerCodigo: string;
  tallerNombre: string;
  fecha: string;
  capacidadHH: number;
  hhProgramadas: number;
  capacidadEquipos: number;
  equiposProgramados: number;
  sobrecargado: boolean;
};

type KanbanColumn = {
  estado: ScheduleItemStatus;
  items: ScheduleItem[];
};

type GanttDependency = {
  dependenciaId: string;
  predecessorNumeroOT: string;
  successorNumeroOT: string;
  tipo: string;
  motivo?: string | null;
};

type ScheduleAlert = {
  alertId: string;
  tipo: ScheduleAlertType;
  severity: string;
  message: string;
  tallerCodigo?: string | null;
  numeroOT?: string | null;
  faenaCodigo?: string | null;
  createdAtUtc: string;
  resolved: boolean;
};

type ScheduleBoard = {
  workshops: Workshop[];
  items: ScheduleItem[];
  loads: WorkshopLoad[];
  kanban: KanbanColumn[];
  dependencies: GanttDependency[];
  alerts: ScheduleAlert[];
};

type WorkOrderSummary = {
  numeroOT: string;
  estado: string;
  activoCodigo: string;
  activoNombre?: string | null;
  faenaCodigo: string;
  descripcion: string;
  criticidad: string;
  prioridad: string;
  fechaInicioProgramada?: string | null;
  fechaFinProgramada?: string | null;
};

const statusLabels: Record<ScheduleItemStatus, string> = {
  Programado: "Programado",
  EnProceso: "En proceso",
  Atrasado: "Atrasado",
  EsperandoCupo: "Esperando cupo",
  Completado: "Completado"
};

const emptyWorkshop = {
  tallerCodigo: "",
  nombre: "",
  faenaCodigo: "",
  capacidadDiariaHH: "8",
  capacidadEquipos: "2",
  horario: "08:00-18:00",
  especialidad: "Mecanica"
};

const emptySchedule = {
  numeroOT: "",
  tallerCodigo: "",
  fechaInicio: "",
  fechaFin: "",
  hhEstimadas: "2",
  tecnicoUserId: "",
  reason: "",
  overrideCapacity: false
};

export function SchedulingPage() {
  const [board, setBoard] = useState<ScheduleBoard | null>(null);
  const [orders, setOrders] = useState<WorkOrderSummary[]>([]);
  const [filters, setFilters] = useState({ view: "Semanal" as ScheduleViewMode, faenaCodigo: "", tallerCodigo: "", from: "", to: "", includeClosed: false });
  const [workshopForm, setWorkshopForm] = useState(emptyWorkshop);
  const [scheduleForm, setScheduleForm] = useState(emptySchedule);
  const [dependencyForm, setDependencyForm] = useState({ predecessorNumeroOT: "", successorNumeroOT: "", motivo: "" });
  const [dragged, setDragged] = useState<ScheduleItem | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    void load();
  }, [filters.view, filters.faenaCodigo, filters.tallerCodigo, filters.from, filters.to, filters.includeClosed]);

  const loadsByDay = useMemo(() => {
    const groups = new Map<string, WorkshopLoad[]>();
    for (const load of board?.loads ?? []) {
      const key = load.fecha.slice(0, 10);
      groups.set(key, [...(groups.get(key) ?? []), load]);
    }
    return [...groups.entries()].sort(([left], [right]) => left.localeCompare(right));
  }, [board]);

  const totalOverloaded = board?.loads.filter((item) => item.sobrecargado).length ?? 0;
  const delayed = board?.items.filter((item) => item.estado === "Atrasado").length ?? 0;
  const inProgress = board?.items.filter((item) => item.estado === "EnProceso").length ?? 0;

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const query = new URLSearchParams();
      query.set("view", filters.view);
      query.set("includeClosed", String(filters.includeClosed));
      if (filters.faenaCodigo) query.set("faenaCodigo", filters.faenaCodigo);
      if (filters.tallerCodigo) query.set("tallerCodigo", filters.tallerCodigo);
      if (filters.from) query.set("from", toIsoDate(filters.from));
      if (filters.to) query.set("to", toIsoDate(filters.to));
      const [boardResult, orderResult] = await Promise.all([
        apiFetch<ScheduleBoard>(`/api/scheduling/board?${query}`),
        apiFetch<WorkOrderSummary[]>("/api/work-orders?includeClosed=false")
      ]);
      setBoard(boardResult);
      setOrders(orderResult);
      setScheduleForm((current) => ({ ...current, tallerCodigo: current.tallerCodigo || boardResult.workshops[0]?.tallerCodigo || "" }));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar programacion.");
    } finally {
      setIsLoading(false);
    }
  }

  async function saveWorkshop(event: FormEvent) {
    event.preventDefault();
    await save(async () => {
      await apiFetch("/api/scheduling/workshops", {
        method: "POST",
        body: JSON.stringify({
          tallerCodigo: workshopForm.tallerCodigo,
          nombre: workshopForm.nombre,
          faenaCodigo: workshopForm.faenaCodigo,
          capacidadDiariaHH: Number(workshopForm.capacidadDiariaHH),
          capacidadEquipos: Number(workshopForm.capacidadEquipos),
          horario: workshopForm.horario,
          especialidad: workshopForm.especialidad,
          activo: true,
          reason: "Configuracion taller"
        })
      });
      setWorkshopForm(emptyWorkshop);
      setMessage("Taller guardado.");
    });
  }

  async function scheduleOrder(event: FormEvent) {
    event.preventDefault();
    await save(async () => {
      const result = await apiFetch<{ warnings: string[] }>(`/api/scheduling/work-orders/${encodeURIComponent(scheduleForm.numeroOT)}`, {
        method: "POST",
        body: JSON.stringify({
          tallerCodigo: scheduleForm.tallerCodigo,
          fechaInicio: toIsoDateTime(scheduleForm.fechaInicio),
          fechaFin: toIsoDateTime(scheduleForm.fechaFin),
          hhEstimadas: Number(scheduleForm.hhEstimadas),
          tecnicoUserId: emptyToNull(scheduleForm.tecnicoUserId),
          reason: scheduleForm.reason,
          overrideCapacity: scheduleForm.overrideCapacity
        })
      });
      setMessage(result.warnings.length ? result.warnings.join(" ") : "OT programada.");
    });
  }

  async function addDependency(event: FormEvent) {
    event.preventDefault();
    await save(async () => {
      await apiFetch("/api/scheduling/dependencies", {
        method: "POST",
        body: JSON.stringify({ ...dependencyForm, tipo: "FinishToStart" })
      });
      setDependencyForm({ predecessorNumeroOT: "", successorNumeroOT: "", motivo: "" });
      setMessage("Dependencia agregada.");
    });
  }

  async function dropOnDay(event: DragEvent<HTMLDivElement>, load: WorkshopLoad) {
    event.preventDefault();
    if (!dragged) return;
    const durationMs = Math.max(60 * 60 * 1000, new Date(dragged.fechaFin).getTime() - new Date(dragged.fechaInicio).getTime());
    const start = new Date(`${load.fecha.slice(0, 10)}T08:00:00`);
    const end = new Date(start.getTime() + durationMs);
    await save(async () => {
      await apiFetch(`/api/scheduling/work-orders/${encodeURIComponent(dragged.numeroOT)}`, {
        method: "POST",
        body: JSON.stringify({
          tallerCodigo: load.tallerCodigo,
          fechaInicio: start.toISOString(),
          fechaFin: end.toISOString(),
          hhEstimadas: dragged.hhEstimadas,
          tecnicoUserId: dragged.tecnicoUserId,
          reason: "Reprogramacion por drag/drop",
          overrideCapacity: false
        })
      });
      setMessage(`OT ${dragged.numeroOT} reprogramada.`);
      setDragged(null);
    });
  }

  async function save(action: () => Promise<void>) {
    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      await action();
      await load();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible guardar.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="stack">
      <header className="page-header">
        <div>
          <p className="eyebrow">Planificacion</p>
          <h1>Programacion</h1>
          <p>Calendario, Kanban, Gantt, talleres, capacidad y alertas operativas.</p>
        </div>
        <button className="secondary-button" type="button" onClick={() => void load()}>
          <RefreshCw size={18} /> Actualizar
        </button>
      </header>

      <section className="kpi-grid xl:grid-cols-4">
        <Metric icon={<CalendarDays size={18} />} label="OT programadas" value={board?.items.length ?? 0} />
        <Metric icon={<Wrench size={18} />} label="En proceso" value={inProgress} />
        <Metric icon={<AlertTriangle size={18} />} label="Atrasadas" value={delayed} />
        <Metric icon={<AlertTriangle size={18} />} label="Sobrecargas" value={totalOverloaded} />
      </section>

      {message ? <div className="success-banner">{message}</div> : null}
      {error ? <div className="error-banner">{error}</div> : null}

      <section className="panel stack">
        <div className="toolbar">
          <select value={filters.view} onChange={(event) => setFilters({ ...filters, view: event.target.value as ScheduleViewMode })}>
            <option value="Diario">Diario</option>
            <option value="Semanal">Semanal</option>
            <option value="Mensual">Mensual</option>
          </select>
          <input type="date" value={filters.from} onChange={(event) => setFilters({ ...filters, from: event.target.value })} />
          <input type="date" value={filters.to} onChange={(event) => setFilters({ ...filters, to: event.target.value })} />
          <select value={filters.tallerCodigo} onChange={(event) => setFilters({ ...filters, tallerCodigo: event.target.value })}>
            <option value="">Todos los talleres</option>
            {(board?.workshops ?? []).map((item) => <option key={item.tallerCodigo} value={item.tallerCodigo}>{item.nombre}</option>)}
          </select>
          <label className="check-row"><input type="checkbox" checked={filters.includeClosed} onChange={(event) => setFilters({ ...filters, includeClosed: event.target.checked })} />Cerradas</label>
        </div>
        <FaenaSelect value={filters.faenaCodigo} onChange={(value) => setFilters({ ...filters, faenaCodigo: value })} />
      </section>

      <section className="grid gap-4 xl:grid-cols-2">
        <form className="panel stack" onSubmit={saveWorkshop}>
          <div className="section-heading"><h2>Taller</h2></div>
          <div className="form-grid">
            <label>Codigo<input value={workshopForm.tallerCodigo} onChange={(event) => setWorkshopForm({ ...workshopForm, tallerCodigo: event.target.value })} required /></label>
            <label>Nombre<input value={workshopForm.nombre} onChange={(event) => setWorkshopForm({ ...workshopForm, nombre: event.target.value })} required /></label>
            <FaenaSelect emptyLabel="Faena" value={workshopForm.faenaCodigo} onChange={(value) => setWorkshopForm({ ...workshopForm, faenaCodigo: value })} />
            <label>HH/dia<input type="number" min="0" step="0.5" value={workshopForm.capacidadDiariaHH} onChange={(event) => setWorkshopForm({ ...workshopForm, capacidadDiariaHH: event.target.value })} required /></label>
            <label>Equipos<input type="number" min="0" value={workshopForm.capacidadEquipos} onChange={(event) => setWorkshopForm({ ...workshopForm, capacidadEquipos: event.target.value })} required /></label>
            <label>Horario<input value={workshopForm.horario} onChange={(event) => setWorkshopForm({ ...workshopForm, horario: event.target.value })} required /></label>
            <label>Especialidad<input value={workshopForm.especialidad} onChange={(event) => setWorkshopForm({ ...workshopForm, especialidad: event.target.value })} required /></label>
          </div>
          <button className="primary-button" type="submit" disabled={isSaving}><Save size={18} /> Guardar taller</button>
        </form>

        <form className="panel stack" onSubmit={scheduleOrder}>
          <div className="section-heading"><h2>Programar OT</h2></div>
          <div className="form-grid">
            <label>OT<select value={scheduleForm.numeroOT} onChange={(event) => setScheduleForm({ ...scheduleForm, numeroOT: event.target.value })} required><option value="">Selecciona OT</option>{orders.map((item) => <option key={item.numeroOT} value={item.numeroOT}>{item.numeroOT} - {item.activoNombre ?? item.activoCodigo}</option>)}</select></label>
            <label>Taller<select value={scheduleForm.tallerCodigo} onChange={(event) => setScheduleForm({ ...scheduleForm, tallerCodigo: event.target.value })} required><option value="">Selecciona taller</option>{(board?.workshops ?? []).map((item) => <option key={item.tallerCodigo} value={item.tallerCodigo}>{item.nombre}</option>)}</select></label>
            <label>Inicio<input type="datetime-local" value={scheduleForm.fechaInicio} onChange={(event) => setScheduleForm({ ...scheduleForm, fechaInicio: event.target.value })} required /></label>
            <label>Fin<input type="datetime-local" value={scheduleForm.fechaFin} onChange={(event) => setScheduleForm({ ...scheduleForm, fechaFin: event.target.value })} required /></label>
            <label>HH estimadas<input type="number" min="0.1" step="0.1" value={scheduleForm.hhEstimadas} onChange={(event) => setScheduleForm({ ...scheduleForm, hhEstimadas: event.target.value })} required /></label>
            <label>Tecnico<input value={scheduleForm.tecnicoUserId} onChange={(event) => setScheduleForm({ ...scheduleForm, tecnicoUserId: event.target.value })} /></label>
            <label className="span-2">Motivo<input value={scheduleForm.reason} onChange={(event) => setScheduleForm({ ...scheduleForm, reason: event.target.value })} required /></label>
            <label className="check-row"><input type="checkbox" checked={scheduleForm.overrideCapacity} onChange={(event) => setScheduleForm({ ...scheduleForm, overrideCapacity: event.target.checked })} />Advertido por capacidad</label>
          </div>
          <button className="primary-button" type="submit" disabled={isSaving}><CalendarDays size={18} /> Programar</button>
        </form>
      </section>

      <section className="panel stack">
        <div className="section-heading"><h2>Calendario</h2><span>{isLoading ? "Cargando..." : `${board?.loads.length ?? 0} bloques`}</span></div>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          {loadsByDay.map(([date, loads]) => (
            <div key={date} className="rounded-md border border-slate-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-950">
              <h3 className="text-sm font-semibold text-slate-900 dark:text-slate-100">{formatDate(date)}</h3>
              <div className="mt-3 space-y-2">
                {loads.map((load) => (
                  <div key={`${load.tallerCodigo}-${load.fecha}`} className={`rounded-md border p-2 text-sm ${load.sobrecargado ? "border-amber-300 bg-amber-50 dark:border-amber-900 dark:bg-amber-950/30" : "border-slate-200 dark:border-slate-800"}`} onDragOver={(event) => event.preventDefault()} onDrop={(event) => void dropOnDay(event, load)}>
                    <strong>{load.tallerNombre}</strong>
                    <p>{load.hhProgramadas}/{load.capacidadHH} HH</p>
                    <p>{load.equiposProgramados}/{load.capacidadEquipos} equipos</p>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </section>

      <section className="grid gap-4 xl:grid-cols-[0.9fr_1.1fr]">
        <section className="panel stack">
          <div className="section-heading"><h2><KanbanSquare size={18} /> Kanban</h2></div>
          <div className="grid gap-3 md:grid-cols-2">
            {(board?.kanban ?? []).map((column) => (
              <div key={column.estado} className="rounded-md border border-slate-200 bg-slate-50 p-3 dark:border-slate-800 dark:bg-slate-950">
                <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">{statusLabels[column.estado]}</h3>
                <div className="mt-3 space-y-2">
                  {column.items.map((item) => <ScheduleCard key={item.programacionId} item={item} onDragStart={() => setDragged(item)} />)}
                </div>
              </div>
            ))}
          </div>
        </section>

        <section className="panel stack">
          <div className="section-heading"><h2><GitBranch size={18} /> Gantt</h2></div>
          <Gantt items={board?.items ?? []} dependencies={board?.dependencies ?? []} />
          <form className="form-grid" onSubmit={addDependency}>
            <label>Predecesora<select value={dependencyForm.predecessorNumeroOT} onChange={(event) => setDependencyForm({ ...dependencyForm, predecessorNumeroOT: event.target.value })} required><option value="">OT</option>{(board?.items ?? []).map((item) => <option key={item.numeroOT} value={item.numeroOT}>{item.numeroOT}</option>)}</select></label>
            <label>Sucesora<select value={dependencyForm.successorNumeroOT} onChange={(event) => setDependencyForm({ ...dependencyForm, successorNumeroOT: event.target.value })} required><option value="">OT</option>{(board?.items ?? []).map((item) => <option key={item.numeroOT} value={item.numeroOT}>{item.numeroOT}</option>)}</select></label>
            <label>Motivo<input value={dependencyForm.motivo} onChange={(event) => setDependencyForm({ ...dependencyForm, motivo: event.target.value })} /></label>
            <button className="secondary-button" type="submit" disabled={isSaving}>Agregar dependencia</button>
          </form>
        </section>
      </section>

      <section className="panel stack">
        <div className="section-heading"><h2>Alertas</h2><span>{board?.alerts.length ?? 0}</span></div>
        <div className="data-table">
          <table>
            <thead><tr><th>Tipo</th><th>Mensaje</th><th>Taller</th><th>OT</th></tr></thead>
            <tbody>{(board?.alerts ?? []).map((item) => <tr key={item.alertId}><td>{item.tipo}</td><td>{item.message}</td><td>{item.tallerCodigo ?? "-"}</td><td>{item.numeroOT ?? "-"}</td></tr>)}</tbody>
          </table>
        </div>
      </section>
    </section>
  );
}

function ScheduleCard({ item, onDragStart }: { item: ScheduleItem; onDragStart: () => void }) {
  return (
    <article draggable className="rounded-md border border-slate-200 bg-white p-3 text-sm dark:border-slate-800 dark:bg-slate-900" onDragStart={onDragStart}>
      <strong className="block text-slate-900 dark:text-slate-100">{item.numeroOT}</strong>
      <span className="block text-xs text-slate-500 dark:text-slate-400">{item.activoNombre ?? item.activoCodigo}</span>
      <span className="mt-2 block">{item.tallerNombre}</span>
      <span className="block text-xs">{formatDateTime(item.fechaInicio)} - {formatDateTime(item.fechaFin)}</span>
    </article>
  );
}

function Gantt({ items, dependencies }: { items: ScheduleItem[]; dependencies: GanttDependency[] }) {
  if (items.length === 0) return <p className="text-sm text-slate-500 dark:text-slate-400">Sin OT programadas.</p>;
  const min = Math.min(...items.map((item) => new Date(item.fechaInicio).getTime()));
  const max = Math.max(...items.map((item) => new Date(item.fechaFin).getTime()));
  const span = Math.max(1, max - min);

  return (
    <div className="stack">
      <div className="space-y-2">
        {items.map((item) => {
          const start = new Date(item.fechaInicio).getTime();
          const end = new Date(item.fechaFin).getTime();
          const left = ((start - min) / span) * 100;
          const width = Math.max(6, ((end - start) / span) * 100);
          return (
            <div key={item.programacionId} className="grid grid-cols-[7rem_1fr] items-center gap-3 text-sm">
              <span className="truncate font-semibold">{item.numeroOT}</span>
              <div className="relative h-8 rounded bg-slate-100 dark:bg-slate-800">
                <div className="absolute top-1 h-6 rounded bg-teal-600" style={{ left: `${left}%`, width: `${width}%` }} title={`${item.numeroOT} ${formatDateTime(item.fechaInicio)}`} />
              </div>
            </div>
          );
        })}
      </div>
      {dependencies.length ? <p className="text-xs text-slate-500 dark:text-slate-400">Dependencias: {dependencies.map((item) => `${item.predecessorNumeroOT} -> ${item.successorNumeroOT}`).join(" | ")}</p> : null}
    </div>
  );
}

function Metric({ icon, label, value }: { icon: JSX.Element; label: string; value: number }) {
  return <article className="metric-card">{icon}<span>{label}</span><strong>{value}</strong></article>;
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function toIsoDate(value: string) {
  return new Date(`${value}T00:00:00`).toISOString();
}

function toIsoDateTime(value: string) {
  return new Date(value).toISOString();
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString();
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString([], { dateStyle: "short", timeStyle: "short" });
}
