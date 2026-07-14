import { FormEvent, useEffect, useMemo, useState } from "react";
import { AlertTriangle, CalendarDays, Clock, Gauge, PlayCircle, RefreshCw, Save, Wrench } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";

type PreventiveStatus = "Vigente" | "ProximoAVencer" | "EnVentana" | "Vencido" | "OTGenerada" | "Ejecutado" | "Reprogramado";
type PreventiveFrequencyType = "Horas" | "Kilometros" | "Calendario" | "Mixta";

type AssetSummary = {
  codigo: string;
  nombre: string;
  faenaCodigo?: string | null;
  tipoMedicionUso?: "HOROMETRO" | "KILOMETRAJE" | null;
  ultimaLectura?: number | null;
  unidadLectura?: string | null;
};

type PreventivePlan = {
  codigo: string;
  nombre: string;
  activoCodigo?: string | null;
  activoNombre?: string | null;
  faenaCodigo?: string | null;
  familiaEquipo?: string | null;
  marca?: string | null;
  modelo?: string | null;
  tipoFrecuencia: PreventiveFrequencyType;
  frecuenciaHoras?: number | null;
  frecuenciaKm?: number | null;
  frecuenciaDias?: number | null;
  toleranciaHoras: number;
  toleranciaKm: number;
  toleranciaDias: number;
  checklistCodigo?: string | null;
  repuestosSugeridos?: string | null;
  hhEstimadas: number;
  proximaFecha?: string | null;
  proximaHora?: number | null;
  proximoKm?: number | null;
  estado: PreventiveStatus;
  activo: boolean;
};

type PreventiveDue = {
  planCodigo: string;
  nombre: string;
  activoCodigo: string;
  activoNombre?: string | null;
  faenaCodigo: string;
  estado: PreventiveStatus;
  horasRestantes?: number | null;
  kmRestantes?: number | null;
  diasRestantes?: number | null;
  fechaVencimientoEstimada?: string | null;
  numeroOT?: string | null;
  mensaje: string;
};

type PreventiveReading = {
  id: string;
  activoCodigo: string;
  activoNombre?: string | null;
  faenaCodigo?: string | null;
  fechaLecturaUtc: string;
  valor: number;
  unidad: string;
  esCorreccion: boolean;
  esAnomala: boolean;
  mensajeValidacion?: string | null;
};

type PreventiveCalendarItem = {
  planCodigo: string;
  nombre: string;
  activoCodigo: string;
  activoNombre?: string | null;
  faenaCodigo: string;
  fecha: string;
  estado: PreventiveStatus;
  numeroOT?: string | null;
};

type PreventiveHistory = {
  historyId: string;
  planCodigo: string;
  activoCodigo: string;
  estadoAnterior: PreventiveStatus;
  estadoNuevo: PreventiveStatus;
  fechaUtc: string;
  usuarioId: string;
  motivo: string;
  numeroOT?: string | null;
};

type PreventiveDashboard = {
  plans: PreventivePlan[];
  dueItems: PreventiveDue[];
  calendar: PreventiveCalendarItem[];
  history: PreventiveHistory[];
};

const emptyPlan = {
  codigo: "",
  nombre: "",
  activoCodigo: "",
  familiaEquipo: "",
  marca: "",
  modelo: "",
  frecuenciaHoras: "",
  frecuenciaKm: "",
  frecuenciaDias: "",
  toleranciaHoras: "0",
  toleranciaKm: "0",
  toleranciaDias: "0",
  checklistCodigo: "",
  repuestosSugeridos: "",
  hhEstimadas: "2",
  reason: "Configuracion preventivo"
};

const emptyReading = {
  activoCodigo: "",
  valor: "",
  fechaLectura: new Date().toISOString().slice(0, 16),
  evidencia: "",
  autorizarCorreccion: false,
  motivoCorreccion: ""
};

const statusLabels: Record<PreventiveStatus, string> = {
  Vigente: "Vigente",
  ProximoAVencer: "Proximo",
  EnVentana: "En ventana",
  Vencido: "Vencido",
  OTGenerada: "OT generada",
  Ejecutado: "Ejecutado",
  Reprogramado: "Reprogramado"
};

export function PreventiveMaintenancePage() {
  const [dashboard, setDashboard] = useState<PreventiveDashboard | null>(null);
  const [readings, setReadings] = useState<PreventiveReading[]>([]);
  const [assets, setAssets] = useState<AssetSummary[]>([]);
  const [filters, setFilters] = useState({ faenaCodigo: "", activoCodigo: "" });
  const [planForm, setPlanForm] = useState(emptyPlan);
  const [readingForm, setReadingForm] = useState(emptyReading);
  const [reprogramForm, setReprogramForm] = useState({ planCode: "", proximaFecha: "", proximaHora: "", proximoKm: "", reason: "" });
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    void load();
  }, [filters.faenaCodigo, filters.activoCodigo]);

  const counters = useMemo(() => {
    const due = dashboard?.dueItems ?? [];
    return {
      plans: dashboard?.plans.length ?? 0,
      window: due.filter((item) => item.estado === "EnVentana").length,
      overdue: due.filter((item) => item.estado === "Vencido").length,
      generated: due.filter((item) => item.estado === "OTGenerada").length
    };
  }, [dashboard]);

  async function load() {
    setIsLoading(true);
    setError(null);
    try {
      const query = new URLSearchParams();
      if (filters.faenaCodigo) query.set("faenaCodigo", filters.faenaCodigo);
      if (filters.activoCodigo) query.set("activoCodigo", filters.activoCodigo);
      const [dashboardResult, assetResult] = await Promise.all([
        apiFetch<PreventiveDashboard>(`/api/preventive/dashboard?${query}`),
        apiFetch<AssetSummary[]>(filters.faenaCodigo ? `/api/assets?faenaCodigo=${encodeURIComponent(filters.faenaCodigo)}` : "/api/assets")
      ]);
      const selectedAssets = filters.activoCodigo ? assetResult.filter((asset) => asset.codigo === filters.activoCodigo) : assetResult;
      const readingGroups = await Promise.all(selectedAssets.map(async (asset) => {
        const rows = await apiFetch<Array<{ id: string; fechaLecturaUtc: string; valor: number; unidad: string; esCorreccion: boolean; esAnomala: boolean; mensajeValidacion?: string | null }>>(`/api/assets/${encodeURIComponent(asset.codigo)}/readings`);
        return rows.map((row) => ({ ...row, activoCodigo: asset.codigo, activoNombre: asset.nombre, faenaCodigo: asset.faenaCodigo }));
      }));
      setDashboard(dashboardResult);
      setReadings(readingGroups.flat().sort((a, b) => b.fechaLecturaUtc.localeCompare(a.fechaLecturaUtc)));
      setAssets(assetResult);
      setPlanForm((current) => ({ ...current, activoCodigo: current.activoCodigo || filters.activoCodigo }));
      setReadingForm((current) => ({ ...current, activoCodigo: current.activoCodigo || filters.activoCodigo }));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar preventivos.");
    } finally {
      setIsLoading(false);
    }
  }

  async function savePlan(event: FormEvent) {
    event.preventDefault();
    await save(async () => {
      await apiFetch<PreventivePlan>("/api/preventive/plans", {
        method: "POST",
        body: JSON.stringify({
          codigo: planForm.codigo,
          nombre: planForm.nombre,
          activoCodigo: emptyToNull(planForm.activoCodigo),
          familiaEquipo: emptyToNull(planForm.familiaEquipo),
          marca: emptyToNull(planForm.marca),
          modelo: emptyToNull(planForm.modelo),
          frecuenciaHoras: numberOrNull(planForm.frecuenciaHoras),
          frecuenciaKm: numberOrNull(planForm.frecuenciaKm),
          frecuenciaDias: integerOrNull(planForm.frecuenciaDias),
          toleranciaHoras: Number(planForm.toleranciaHoras || 0),
          toleranciaKm: Number(planForm.toleranciaKm || 0),
          toleranciaDias: Number(planForm.toleranciaDias || 0),
          checklistCodigo: emptyToNull(planForm.checklistCodigo),
          repuestosSugeridos: emptyToNull(planForm.repuestosSugeridos),
          hhEstimadas: Number(planForm.hhEstimadas || 1),
          activo: true,
          reason: planForm.reason
        })
      });
      setPlanForm(emptyPlan);
      setMessage("Plan preventivo guardado.");
    });
  }

  async function saveReading(event: FormEvent) {
    event.preventDefault();
    await save(async () => {
      const reading = await apiFetch<{ esAnomala: boolean; mensajeValidacion?: string | null }>(`/api/assets/${encodeURIComponent(readingForm.activoCodigo)}/readings`, {
        method: "POST",
        body: JSON.stringify({ valor: Number(readingForm.valor), fechaLecturaUtc: new Date(readingForm.fechaLectura).toISOString(), origen: "MANUAL", evidenciaReferencia: emptyToNull(readingForm.evidencia) })
      });
      setReadingForm(emptyReading);
      setMessage(reading.esAnomala ? reading.mensajeValidacion ?? "Lectura guardada con alerta de salto." : "Lectura guardada.");
    });
  }

  async function runEngine() {
    await save(async () => {
      const result = await apiFetch<{ evaluated: number; generatedWorkOrders: number; alertsGenerated: number; warnings: string[] }>("/api/preventive/run", { method: "POST" });
      setMessage(`Motor ejecutado: ${result.evaluated} evaluados, ${result.generatedWorkOrders} OT y ${result.alertsGenerated} alertas.`);
    });
  }

  async function generateOt(item: PreventiveDue) {
    await save(async () => {
      const result = await apiFetch<{ numeroOT: string; warnings: string[] }>(`/api/preventive/plans/${encodeURIComponent(item.planCodigo)}/generate-ot`, {
        method: "POST",
        body: JSON.stringify({ activoCodigo: item.activoCodigo, reason: "Generacion manual desde preventivos" })
      });
      setMessage(result.warnings.length ? `${result.numeroOT}: ${result.warnings.join(" ")}` : `OT ${result.numeroOT} generada.`);
    });
  }

  async function reprogram(event: FormEvent) {
    event.preventDefault();
    await save(async () => {
      await apiFetch(`/api/preventive/plans/${encodeURIComponent(reprogramForm.planCode)}/reprogram`, {
        method: "POST",
        body: JSON.stringify({
          proximaFecha: reprogramForm.proximaFecha ? new Date(reprogramForm.proximaFecha).toISOString() : null,
          proximaHora: numberOrNull(reprogramForm.proximaHora),
          proximoKm: numberOrNull(reprogramForm.proximoKm),
          reason: reprogramForm.reason
        })
      });
      setReprogramForm({ planCode: "", proximaFecha: "", proximaHora: "", proximoKm: "", reason: "" });
      setMessage("Preventivo reprogramado.");
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
          <p className="eyebrow">Mantenimiento</p>
          <h1>Preventivos</h1>
          <p>Planes automaticos, lecturas, vencimientos, OT generadas y calendario preventivo.</p>
        </div>
        <div className="toolbar">
          <button className="secondary-button" type="button" onClick={() => void load()}><RefreshCw size={18} /> Actualizar</button>
          <button className="primary-button" type="button" disabled={isSaving} onClick={() => void runEngine()}><PlayCircle size={18} /> Ejecutar motor</button>
        </div>
      </header>

      <section className="kpi-grid xl:grid-cols-4">
        <Metric icon={<Wrench size={18} />} label="Planes" value={counters.plans} />
        <Metric icon={<Clock size={18} />} label="En ventana" value={counters.window} />
        <Metric icon={<AlertTriangle size={18} />} label="Vencidos" value={counters.overdue} />
        <Metric icon={<CalendarDays size={18} />} label="OT generadas" value={counters.generated} />
      </section>

      {message ? <div className="success-banner">{message}</div> : null}
      {error ? <div className="error-banner">{error}</div> : null}

      <section className="panel stack">
        <div className="toolbar">
          <FaenaSelect value={filters.faenaCodigo} onChange={(value) => setFilters({ ...filters, faenaCodigo: value, activoCodigo: "" })} />
          <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
            Activo
            <select className="mt-2 h-10 rounded-md border border-slate-300 bg-white px-3 text-sm dark:border-slate-700 dark:bg-slate-950" value={filters.activoCodigo} onChange={(event) => setFilters({ ...filters, activoCodigo: event.target.value })}>
              <option value="">Todos</option>
              {assets.map((asset) => <option key={asset.codigo} value={asset.codigo}>{asset.nombre} - {asset.codigo}</option>)}
            </select>
          </label>
        </div>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <form className="panel stack" onSubmit={savePlan}>
          <div className="section-heading"><h2>Plan preventivo</h2><span>Horas, km y calendario</span></div>
          <div className="form-grid">
            <label>Codigo<input value={planForm.codigo} onChange={(event) => setPlanForm({ ...planForm, codigo: event.target.value })} required /></label>
            <label>Nombre<input value={planForm.nombre} onChange={(event) => setPlanForm({ ...planForm, nombre: event.target.value })} required /></label>
            <label>Activo<select value={planForm.activoCodigo} onChange={(event) => setPlanForm({ ...planForm, activoCodigo: event.target.value })}><option value="">Por familia</option>{assets.map((asset) => <option key={asset.codigo} value={asset.codigo}>{asset.nombre} - {asset.codigo}</option>)}</select></label>
            <label>Familia<input value={planForm.familiaEquipo} onChange={(event) => setPlanForm({ ...planForm, familiaEquipo: event.target.value })} /></label>
            <label>Marca<input value={planForm.marca} onChange={(event) => setPlanForm({ ...planForm, marca: event.target.value })} /></label>
            <label>Modelo<input value={planForm.modelo} onChange={(event) => setPlanForm({ ...planForm, modelo: event.target.value })} /></label>
            <label>Frecuencia horas<input type="number" min="0" step="1" value={planForm.frecuenciaHoras} onChange={(event) => setPlanForm({ ...planForm, frecuenciaHoras: event.target.value })} /></label>
            <label>Tolerancia horas<input type="number" min="0" step="1" value={planForm.toleranciaHoras} onChange={(event) => setPlanForm({ ...planForm, toleranciaHoras: event.target.value })} /></label>
            <label>Frecuencia km<input type="number" min="0" step="1" value={planForm.frecuenciaKm} onChange={(event) => setPlanForm({ ...planForm, frecuenciaKm: event.target.value })} /></label>
            <label>Tolerancia km<input type="number" min="0" step="1" value={planForm.toleranciaKm} onChange={(event) => setPlanForm({ ...planForm, toleranciaKm: event.target.value })} /></label>
            <label>Frecuencia dias<input type="number" min="0" step="1" value={planForm.frecuenciaDias} onChange={(event) => setPlanForm({ ...planForm, frecuenciaDias: event.target.value })} /></label>
            <label>Tolerancia dias<input type="number" min="0" step="1" value={planForm.toleranciaDias} onChange={(event) => setPlanForm({ ...planForm, toleranciaDias: event.target.value })} /></label>
            <label>Checklist<input value={planForm.checklistCodigo} onChange={(event) => setPlanForm({ ...planForm, checklistCodigo: event.target.value })} /></label>
            <label>HH estimadas<input type="number" min="0.1" step="0.5" value={planForm.hhEstimadas} onChange={(event) => setPlanForm({ ...planForm, hhEstimadas: event.target.value })} /></label>
            <label className="span-2">Repuestos sugeridos<input placeholder="REP-001:1:UN;REP-002:2:UN" value={planForm.repuestosSugeridos} onChange={(event) => setPlanForm({ ...planForm, repuestosSugeridos: event.target.value })} /></label>
            <label className="span-2">Motivo<input value={planForm.reason} onChange={(event) => setPlanForm({ ...planForm, reason: event.target.value })} required /></label>
          </div>
          <button className="primary-button" type="submit" disabled={isSaving}><Save size={18} /> Guardar plan</button>
        </form>

        <form className="panel stack" onSubmit={saveReading}>
          <div className="section-heading"><h2>Lectura</h2><span>Un �nico valor; la unidad proviene del activo</span></div>
          <div className="form-grid xl:grid-cols-2">
            <label>Activo<select value={readingForm.activoCodigo} onChange={(event) => setReadingForm({ ...readingForm, activoCodigo: event.target.value })} required><option value="">Selecciona activo</option>{assets.map((asset) => <option key={asset.codigo} value={asset.codigo}>{asset.nombre} - {asset.codigo}</option>)}</select></label>
            <label>Fecha<input type="datetime-local" value={readingForm.fechaLectura} onChange={(event) => setReadingForm({ ...readingForm, fechaLectura: event.target.value })} required /></label>
            <label>Valor<input type="number" min="0" step="0.1" value={readingForm.valor} onChange={(event) => setReadingForm({ ...readingForm, valor: event.target.value })} required /></label>
            <label className="span-2">Evidencia<input value={readingForm.evidencia} onChange={(event) => setReadingForm({ ...readingForm, evidencia: event.target.value })} /></label>
            <small className="span-2">Las correcciones se registran desde el historial del activo; no se edita una lectura existente.</small>
          </div>
          <button className="primary-button" type="submit" disabled={isSaving}><Gauge size={18} /> Registrar lectura</button>
        </form>
      </section>

      <section className="panel stack">
        <div className="section-heading"><h2>Vencimientos</h2><span>{isLoading ? "Cargando..." : `${dashboard?.dueItems.length ?? 0} preventivos`}</span></div>
        <div className="data-table">
          <table>
            <thead><tr><th>Preventivo</th><th>Activo</th><th>Estado</th><th>Restante</th><th>Fecha</th><th>OT</th><th></th></tr></thead>
            <tbody>
              {(dashboard?.dueItems ?? []).map((item) => (
                <tr key={`${item.planCodigo}-${item.activoCodigo}`}>
                  <td><strong>{item.nombre}</strong><small>{item.planCodigo}</small></td>
                  <td><strong>{item.activoNombre ?? item.activoCodigo}</strong><small>{item.faenaCodigo}</small></td>
                  <td><span className={`status-pill ${item.estado === "Vencido" ? "danger" : item.estado === "OTGenerada" ? "success" : ""}`}>{statusLabels[item.estado]}</span></td>
                  <td>{formatRemaining(item)}</td>
                  <td>{item.fechaVencimientoEstimada ? formatDate(item.fechaVencimientoEstimada) : "-"}</td>
                  <td>{item.numeroOT ?? "-"}</td>
                  <td><button className="secondary-button" type="button" disabled={isSaving || Boolean(item.numeroOT)} onClick={() => void generateOt(item)}>Generar OT</button></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="grid gap-4 xl:grid-cols-2">
        <section className="panel stack">
          <div className="section-heading"><h2>Calendario preventivo</h2><span>{dashboard?.calendar.length ?? 0}</span></div>
          <div className="grid gap-3 md:grid-cols-2">
            {(dashboard?.calendar ?? []).slice(0, 12).map((item) => (
              <article className="panel-muted" key={`${item.planCodigo}-${item.activoCodigo}-${item.fecha}`}>
                <div className="section-heading"><h3>{formatDate(item.fecha)}</h3><span className="status-pill">{statusLabels[item.estado]}</span></div>
                <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">{item.nombre}</p>
                <small className="text-xs text-slate-500">{item.activoNombre ?? item.activoCodigo} - {item.faenaCodigo}</small>
              </article>
            ))}
          </div>
        </section>

        <form className="panel stack" onSubmit={reprogram}>
          <div className="section-heading"><h2>Reprogramar</h2><span>requiere motivo</span></div>
          <div className="form-grid xl:grid-cols-2">
            <label>Plan<select value={reprogramForm.planCode} onChange={(event) => setReprogramForm({ ...reprogramForm, planCode: event.target.value })} required><option value="">Selecciona plan</option>{(dashboard?.plans ?? []).map((plan) => <option key={plan.codigo} value={plan.codigo}>{plan.nombre}</option>)}</select></label>
            <label>Proxima fecha<input type="datetime-local" value={reprogramForm.proximaFecha} onChange={(event) => setReprogramForm({ ...reprogramForm, proximaFecha: event.target.value })} /></label>
            <label>Proxima hora<input type="number" min="0" step="0.1" value={reprogramForm.proximaHora} onChange={(event) => setReprogramForm({ ...reprogramForm, proximaHora: event.target.value })} /></label>
            <label>Proximo km<input type="number" min="0" step="0.1" value={reprogramForm.proximoKm} onChange={(event) => setReprogramForm({ ...reprogramForm, proximoKm: event.target.value })} /></label>
            <label className="span-2">Motivo<input value={reprogramForm.reason} onChange={(event) => setReprogramForm({ ...reprogramForm, reason: event.target.value })} required /></label>
          </div>
          <button className="secondary-button" type="submit" disabled={isSaving}>Reprogramar</button>
        </form>
      </section>

      <section className="grid gap-4 xl:grid-cols-2">
        <section className="panel stack">
          <div className="section-heading"><h2>Lecturas recientes</h2><span>{readings.length}</span></div>
          <div className="data-table">
            <table>
              <thead><tr><th>Activo</th><th>Fecha</th><th>Valor</th><th>Unidad</th><th>Validaci�n</th></tr></thead>
              <tbody>{readings.slice(0, 12).map((item) => <tr key={item.id}><td><strong>{item.activoNombre ?? item.activoCodigo}</strong><small>{item.faenaCodigo}</small></td><td>{formatDateTime(item.fechaLecturaUtc)}</td><td>{item.valor}</td><td>{item.unidad}</td><td>{item.esAnomala ? item.mensajeValidacion : item.esCorreccion ? "Correcci�n" : "OK"}</td></tr>)}</tbody>
            </table>
          </div>
        </section>

        <section className="panel stack">
          <div className="section-heading"><h2>Historial</h2><span>{dashboard?.history.length ?? 0}</span></div>
          <div className="data-table">
            <table>
              <thead><tr><th>Plan</th><th>Activo</th><th>Cambio</th><th>Usuario</th></tr></thead>
              <tbody>{(dashboard?.history ?? []).slice(0, 12).map((item) => <tr key={item.historyId}><td>{item.planCodigo}</td><td>{item.activoCodigo}</td><td>{statusLabels[item.estadoAnterior]} -&gt; {statusLabels[item.estadoNuevo]}<small>{item.motivo}</small></td><td>{item.usuarioId}<small>{formatDateTime(item.fechaUtc)}</small></td></tr>)}</tbody>
            </table>
          </div>
        </section>
      </section>
    </section>
  );
}

function Metric({ icon, label, value }: { icon: JSX.Element; label: string; value: number }) {
  return <article className="metric-card">{icon}<span>{label}</span><strong>{value}</strong></article>;
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function numberOrNull(value: string) {
  return value.trim() ? Number(value) : null;
}

function integerOrNull(value: string) {
  return value.trim() ? Number.parseInt(value, 10) : null;
}

function formatRemaining(item: PreventiveDue) {
  const parts = [];
  if (item.horasRestantes !== null && item.horasRestantes !== undefined) parts.push(`${item.horasRestantes} h`);
  if (item.kmRestantes !== null && item.kmRestantes !== undefined) parts.push(`${item.kmRestantes} km`);
  if (item.diasRestantes !== null && item.diasRestantes !== undefined) parts.push(`${item.diasRestantes} d`);
  return parts.length ? parts.join(" - ") : "-";
}

function formatDate(value: string) {
  return new Date(value).toLocaleDateString();
}

function formatDateTime(value: string) {
  return new Date(value).toLocaleString([], { dateStyle: "short", timeStyle: "short" });
}
