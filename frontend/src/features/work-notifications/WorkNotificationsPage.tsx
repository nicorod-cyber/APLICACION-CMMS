import { FormEvent, useEffect, useMemo, useState } from "react";
import { AlertTriangle, Bell, CheckCircle2, ClipboardList, RefreshCw, Send, Wrench, XCircle } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";
import { MaintenanceTargetSelect, type MaintenanceTargetReference } from "../maintenance-targets/MaintenanceTargetSelect";

type WorkNotificationType =
  | "Falla"
  | "CondicionDetectada"
  | "Documental"
  | "Preventivo"
  | "Mejora"
  | "Inspeccion"
  | "ApoyoOperacional";

type WorkNotificationStatus = "Creado" | "EnEvaluacion" | "Aprobado" | "Rechazado" | "ConvertidoOT" | "Anulado";
type Priority = "Baja" | "Media" | "Alta" | "Critica";
type FailureClassification = "ConDetencion" | "SinDetencion" | "ConRestriccion" | "DocumentalHabilitante" | "Repetitiva";

type WorkNotification = {
  avisoId: string;
  estado: WorkNotificationStatus;
  tipo: WorkNotificationType;
  faenaCodigo: string;
  activoCodigo?: string | null;
  unidadOperativaCodigo?: string | null;
  objetivo?: { tipo: "Asset" | "OperationalUnit"; codigo: string; nombre: string } | null;
  sistema?: string | null;
  subsistema?: string | null;
  componente?: string | null;
  descripcion: string;
  prioridad: Priority;
  criticidad: Priority;
  solicitante: string;
  evidenciaInicial?: string | null;
  fechaDeteccion: string;
  fechaCreacion: string;
  clasificacionFalla: FailureClassification;
  evaluadoPor?: string | null;
  evaluadoEnUtc?: string | null;
  aprobadoPor?: string | null;
  aprobadoEnUtc?: string | null;
  rechazadoPor?: string | null;
  rechazadoEnUtc?: string | null;
  motivoRechazo?: string | null;
  numeroOT?: string | null;
  convertidoPor?: string | null;
  convertidoEnUtc?: string | null;
  observaciones?: string | null;
};

type AssetSummary = {
  codigo: string;
  nombre: string;
  faenaCodigo: string;
  ubicacionTecnicaCodigo?: string | null;
  tipoActivo: string;
  criticidad?: string | null;
  estadoOperacional: string;
};

type OperationalUnitSummary = { codigo: string; nombre: string; faenaCodigo?: string | null };

type ConversionResponse = {
  aviso: WorkNotification;
  numeroOT: string;
};

type NotificationForm = {
  tipo: WorkNotificationType;
  faenaCodigo: string;
  activoCodigo: string;
  unidadOperativaCodigo: string;
  objetivo: MaintenanceTargetReference | null;
  sistema: string;
  subsistema: string;
  componente: string;
  descripcion: string;
  prioridad: Priority;
  criticidad: Priority;
  clasificacionFalla: FailureClassification;
  evidenciaInicial: string;
  fechaDeteccion: string;
};

const emptyForm: NotificationForm = {
  tipo: "Falla",
  faenaCodigo: "",
  activoCodigo: "",
  unidadOperativaCodigo: "",
  objetivo: null,
  sistema: "",
  subsistema: "",
  componente: "",
  descripcion: "",
  prioridad: "Media",
  criticidad: "Media",
  clasificacionFalla: "SinDetencion",
  evidenciaInicial: "",
  fechaDeteccion: new Date().toISOString().slice(0, 10)
};

const typeLabels: Record<WorkNotificationType, string> = {
  Falla: "Falla",
  CondicionDetectada: "Condicion detectada",
  Documental: "Documental",
  Preventivo: "Preventivo",
  Mejora: "Mejora",
  Inspeccion: "Inspeccion",
  ApoyoOperacional: "Apoyo operacional"
};

const statusLabels: Record<WorkNotificationStatus, string> = {
  Creado: "Creado",
  EnEvaluacion: "En evaluacion",
  Aprobado: "Aprobado",
  Rechazado: "Rechazado",
  ConvertidoOT: "Convertido a OT",
  Anulado: "Anulado"
};

const failureLabels: Record<FailureClassification, string> = {
  ConDetencion: "Con detencion",
  SinDetencion: "Sin detencion",
  ConRestriccion: "Con restriccion",
  DocumentalHabilitante: "Documental habilitante",
  Repetitiva: "Repetitiva"
};

const priorityValues: Priority[] = ["Baja", "Media", "Alta", "Critica"];
const closedStatuses: WorkNotificationStatus[] = ["Rechazado", "ConvertidoOT", "Anulado"];

export function WorkNotificationsPage() {
  const [notifications, setNotifications] = useState<WorkNotification[]>([]);
  const [assets, setAssets] = useState<AssetSummary[]>([]);
  const [operationalUnits, setOperationalUnits] = useState<OperationalUnitSummary[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [form, setForm] = useState<NotificationForm>(emptyForm);
  const [filters, setFilters] = useState({ status: "", type: "", priority: "", faenaCodigo: "", includeClosed: false, supervisorInbox: true });
  const [reason, setReason] = useState("");
  const [conversion, setConversion] = useState({ fechaProgramada: "", tipoMantenimiento: "" });
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void loadAll();
  }, [filters.status, filters.type, filters.priority, filters.faenaCodigo, filters.includeClosed, filters.supervisorInbox]);

  const assetByCode = useMemo(() => new Map(assets.map((item) => [item.codigo, item])), [assets]);
  const unitByCode = useMemo(() => new Map(operationalUnits.map((item) => [item.codigo, item])), [operationalUnits]);
  const selected = useMemo(() => notifications.find((item) => item.avisoId === selectedId) ?? notifications[0] ?? null, [notifications, selectedId]);
  const asset = selected?.activoCodigo ? assetByCode.get(selected.activoCodigo) : null;
  const operationalUnit = selected?.unidadOperativaCodigo ? unitByCode.get(selected.unidadOperativaCodigo) : null;

  const counters = useMemo(() => {
    return {
      inbox: notifications.filter((item) => item.estado === "Creado" || item.estado === "EnEvaluacion").length,
      approved: notifications.filter((item) => item.estado === "Aprobado").length,
      critical: notifications.filter((item) => item.prioridad === "Critica" || item.criticidad === "Critica").length,
      converted: notifications.filter((item) => item.estado === "ConvertidoOT").length
    };
  }, [notifications]);

  async function loadAll() {
    setIsLoading(true);
    setError(null);
    try {
      const query = new URLSearchParams();
      if (filters.status) query.set("status", filters.status);
      if (filters.type) query.set("type", filters.type);
      if (filters.priority) query.set("priority", filters.priority);
      if (filters.faenaCodigo) query.set("faenaCodigo", filters.faenaCodigo);
      query.set("includeClosed", String(filters.includeClosed));
      query.set("supervisorInbox", String(filters.supervisorInbox));

      const [notificationResult, assetResult, unitResult] = await Promise.all([
        apiFetch<WorkNotification[]>(`/api/work-notifications?${query}`),
        apiFetch<{ items: AssetSummary[] }>("/api/assets?page=1&pageSize=100").then((page) => page.items).catch(() => [] as AssetSummary[]),
        apiFetch<{ items: OperationalUnitSummary[] }>("/api/operational-units?page=1&pageSize=100").then((page) => page.items).catch(() => [] as OperationalUnitSummary[])
      ]);
      setNotifications(notificationResult);
      setAssets(assetResult);
      setOperationalUnits(unitResult);
      if (!selectedId && notificationResult[0]) {
        setSelectedId(notificationResult[0].avisoId);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar avisos.");
    } finally {
      setIsLoading(false);
    }
  }

  async function submitNotification(event: FormEvent) {
    event.preventDefault();
    await saveAction(async () => {
      const created = await apiFetch<WorkNotification>("/api/work-notifications", {
        method: "POST",
        body: JSON.stringify({
          tipo: form.tipo,
          descripcion: form.descripcion,
          prioridad: form.prioridad,
          criticidad: form.criticidad,
          clasificacionFalla: form.clasificacionFalla,
          faenaCodigo: emptyToNull(form.faenaCodigo),
          activoCodigo: emptyToNull(form.activoCodigo),
          unidadOperativaCodigo: emptyToNull(form.unidadOperativaCodigo),
          sistema: emptyToNull(form.sistema),
          subsistema: emptyToNull(form.subsistema),
          componente: emptyToNull(form.componente),
          evidenciaInicial: emptyToNull(form.evidenciaInicial),
          fechaDeteccion: toIsoOrNull(form.fechaDeteccion)
        })
      });
      setForm(emptyForm);
      setSelectedId(created.avisoId);
      setMessage(`Aviso ${created.avisoId} creado.`);
    });
  }

  async function runNotificationAction(path: string, success: string) {
    if (!selected) return;
    await saveAction(async () => {
      const updated = await apiFetch<WorkNotification>(`/api/work-notifications/${encodeURIComponent(selected.avisoId)}${path}`, {
        method: "POST",
        body: JSON.stringify({ reason })
      });
      setSelectedId(updated.avisoId);
      setReason("");
      setMessage(success);
    });
  }

  async function convertToWorkOrder() {
    if (!selected) return;
    await saveAction(async () => {
      const result = await apiFetch<ConversionResponse>(`/api/work-notifications/${encodeURIComponent(selected.avisoId)}/convert-to-work-order`, {
        method: "POST",
        body: JSON.stringify({
          reason,
          fechaProgramada: toIsoOrNull(conversion.fechaProgramada),
          tipoMantenimiento: emptyToNull(conversion.tipoMantenimiento)
        })
      });
      setSelectedId(result.aviso.avisoId);
      setReason("");
      setConversion({ fechaProgramada: "", tipoMantenimiento: "" });
      setMessage(`Aviso convertido a ${result.numeroOT}.`);
    });
  }

  async function saveAction(action: () => Promise<void>) {
    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      await action();
      await loadAll();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible completar la accion.");
    } finally {
      setIsSaving(false);
    }
  }

  function applyAsset(code: string) {
    const nextAsset = assetByCode.get(code);
    setForm({
      ...form,
      activoCodigo: code,
      faenaCodigo: nextAsset?.faenaCodigo ?? form.faenaCodigo,
      criticidad: normalizePriority(nextAsset?.criticidad) ?? form.criticidad
    });
  }

  function applyOperationalUnit(code: string) {
    const nextUnit = unitByCode.get(code);
    setForm({ ...form, unidadOperativaCodigo: code, faenaCodigo: nextUnit?.faenaCodigo ?? form.faenaCodigo });
  }

  return (
    <section className="stack">
      <header className="page-header">
        <div>
          <p className="eyebrow">Mantenimiento operativo</p>
          <h1>Avisos de trabajo</h1>
          <p>Registro, evaluacion y conversion de condiciones detectadas a ordenes de trabajo.</p>
        </div>
        <button className="secondary-button" type="button" onClick={() => void loadAll()}>
          <RefreshCw size={18} /> Actualizar
        </button>
      </header>

      <section className="kpi-grid xl:grid-cols-4">
        <Metric icon={<Bell size={18} />} label="Bandeja supervisor" value={counters.inbox} />
        <Metric icon={<CheckCircle2 size={18} />} label="Aprobados" value={counters.approved} />
        <Metric icon={<AlertTriangle size={18} />} label="Criticos" value={counters.critical} />
        <Metric icon={<Wrench size={18} />} label="Convertidos OT" value={counters.converted} />
      </section>

      {message ? <div className="success-banner">{message}</div> : null}
      {error ? <div className="error-banner">{error}</div> : null}

      <div className="two-column-layout">
        <form className="panel stack" onSubmit={submitNotification}>
          <div className="section-heading">
            <h2>Crear aviso</h2>
          </div>
          <div className="form-grid">
            <label>
              Tipo
              <select value={form.tipo} onChange={(event) => setForm({ ...form, tipo: event.target.value as WorkNotificationType })}>
                {Object.entries(typeLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
            <FaenaSelect emptyLabel="Selecciona faena" value={form.faenaCodigo} onChange={(value) => setForm({ ...form, faenaCodigo: value })} />
            <MaintenanceTargetSelect
              value={form.objetivo}
              faenaCodigo={form.faenaCodigo}
              onChange={(objetivo, target) => setForm({ ...form, objetivo, faenaCodigo: target?.faenaCodigo ?? form.faenaCodigo, criticidad: normalizePriority(target?.criticidad) ?? form.criticidad })}
              label="Objetivo de mantenimiento"
            />
            <label>
              Fecha deteccion
              <input type="date" value={form.fechaDeteccion} onChange={(event) => setForm({ ...form, fechaDeteccion: event.target.value })} required />
            </label>
            <label>
              Sistema
              <input value={form.sistema} onChange={(event) => setForm({ ...form, sistema: event.target.value })} />
            </label>
            <label>
              Subsistema
              <input value={form.subsistema} onChange={(event) => setForm({ ...form, subsistema: event.target.value })} />
            </label>
            <label>
              Componente
              <input value={form.componente} onChange={(event) => setForm({ ...form, componente: event.target.value })} />
            </label>
            <label>
              Prioridad
              <select value={form.prioridad} onChange={(event) => setForm({ ...form, prioridad: event.target.value as Priority })}>
                {priorityValues.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Criticidad
              <select value={form.criticidad} onChange={(event) => setForm({ ...form, criticidad: event.target.value as Priority })}>
                {priorityValues.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Clasificacion falla
              <select value={form.clasificacionFalla} onChange={(event) => setForm({ ...form, clasificacionFalla: event.target.value as FailureClassification })}>
                {Object.entries(failureLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Evidencia inicial
              <input value={form.evidenciaInicial} onChange={(event) => setForm({ ...form, evidenciaInicial: event.target.value })} placeholder="URL o referencia" />
            </label>
            <label className="span-2">
              Descripcion
              <textarea value={form.descripcion} onChange={(event) => setForm({ ...form, descripcion: event.target.value })} required />
            </label>
          </div>
          <button className="primary-button" type="submit" disabled={isSaving}>
            <Send size={18} /> Crear aviso
          </button>
        </form>

        <section className="panel stack">
          <div className="section-heading">
            <h2>Bandeja supervisores</h2>
            <span>{notifications.length} avisos</span>
          </div>
          <div className="toolbar">
            <select value={filters.status} onChange={(event) => setFilters({ ...filters, status: event.target.value })}>
              <option value="">Todos los estados</option>
              {Object.entries(statusLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
            <select value={filters.type} onChange={(event) => setFilters({ ...filters, type: event.target.value })}>
              <option value="">Todos los tipos</option>
              {Object.entries(typeLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
            <select value={filters.priority} onChange={(event) => setFilters({ ...filters, priority: event.target.value })}>
              <option value="">Todas las prioridades</option>
              {priorityValues.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </select>
            <label className="check-row">
              <input type="checkbox" checked={filters.supervisorInbox} onChange={(event) => setFilters({ ...filters, supervisorInbox: event.target.checked })} />
              Bandeja
            </label>
            <label className="check-row">
              <input type="checkbox" checked={filters.includeClosed} onChange={(event) => setFilters({ ...filters, includeClosed: event.target.checked })} />
              Cerrados
            </label>
          </div>
          <FaenaSelect value={filters.faenaCodigo} onChange={(value) => setFilters({ ...filters, faenaCodigo: value })} />

          {isLoading ? <p className="text-sm text-slate-500 dark:text-slate-400">Cargando avisos...</p> : null}
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>Aviso</th>
                  <th>Contexto</th>
                  <th>Estado</th>
                  <th>Prioridad</th>
                  <th>OT</th>
                </tr>
              </thead>
              <tbody>
                {notifications.map((item) => {
                  const rowAsset = item.activoCodigo ? assetByCode.get(item.activoCodigo) : null;
                  const rowUnit = item.unidadOperativaCodigo ? unitByCode.get(item.unidadOperativaCodigo) : null;
                  return (
                    <tr key={item.avisoId} className={selected?.avisoId === item.avisoId ? "selected-row" : ""} onClick={() => setSelectedId(item.avisoId)}>
                      <td>
                        <strong>{item.avisoId}</strong>
                        <small>{item.descripcion}</small>
                      </td>
                      <td>
                        <strong>{item.objetivo?.nombre ?? rowAsset?.nombre ?? rowUnit?.nombre ?? "Sin objetivo"}</strong>
                        <small>{[item.faenaCodigo, item.sistema, item.subsistema, item.componente].filter(Boolean).join(" / ") || "-"}</small>
                      </td>
                      <td>
                        <span className={`status-pill ${closedStatuses.includes(item.estado) ? (item.estado === "ConvertidoOT" ? "success" : "danger") : ""}`}>
                          {statusLabels[item.estado]}
                        </span>
                      </td>
                      <td>
                        <strong>{item.prioridad}</strong>
                        <small>{failureLabels[item.clasificacionFalla]}</small>
                      </td>
                      <td>{item.numeroOT ?? "-"}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      {selected ? (
        <section className="panel stack">
          <div className="section-heading">
            <div>
              <h2>{selected.avisoId} - {typeLabels[selected.tipo]}</h2>
              <p>{selected.descripcion}</p>
            </div>
            <span className={`status-pill ${selected.estado === "ConvertidoOT" ? "success" : closedStatuses.includes(selected.estado) ? "danger" : ""}`}>
              {statusLabels[selected.estado]}
            </span>
          </div>

          <div className="detail-grid">
            <Info label="Faena" value={selected.faenaCodigo || "-"} />
            <Info label="Activo" value={asset ? `${asset.nombre} (${asset.codigo})` : selected.activoCodigo ?? "-"} />
            <Info label="Unidad operativa" value={operationalUnit ? `${operationalUnit.nombre} (${operationalUnit.codigo})` : selected.unidadOperativaCodigo ?? "-"} />
            <Info label="Ubicacion tecnica" value={asset?.ubicacionTecnicaCodigo ?? "-"} />
            <Info label="Sistema" value={[selected.sistema, selected.subsistema, selected.componente].filter(Boolean).join(" / ") || "-"} />
            <Info label="Prioridad" value={selected.prioridad} />
            <Info label="Criticidad" value={selected.criticidad} />
            <Info label="Clasificacion" value={failureLabels[selected.clasificacionFalla]} />
            <Info label="Fecha deteccion" value={formatDate(selected.fechaDeteccion)} />
            <Info label="Solicitante" value={selected.solicitante} />
            <Info label="Evidencia" value={selected.evidenciaInicial ?? "-"} />
            <Info label="Aprobado por" value={selected.aprobadoPor ?? "-"} />
            <Info label="OT generada" value={selected.numeroOT ?? "-"} />
          </div>

          <div className="form-grid">
            <label>
              Motivo accion
              <input value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Motivo auditado" />
            </label>
            <label>
              Fecha programada OT
              <input type="date" value={conversion.fechaProgramada} onChange={(event) => setConversion({ ...conversion, fechaProgramada: event.target.value })} />
            </label>
            <label>
              Tipo mantenimiento OT
              <select value={conversion.tipoMantenimiento} onChange={(event) => setConversion({ ...conversion, tipoMantenimiento: event.target.value })}>
                <option value="">Automatico</option>
                <option value="Corrective">Correctivo</option>
                <option value="Preventive">Preventivo</option>
                <option value="Inspection">Inspeccion</option>
                <option value="Predictive">Predictivo</option>
              </select>
            </label>
          </div>

          <div className="toolbar">
            <button className="secondary-button" type="button" disabled={isSaving || selected.estado !== "Creado"} onClick={() => void runNotificationAction("/evaluate", "Aviso en evaluacion.")}>
              <ClipboardList size={18} /> Evaluar
            </button>
            <button className="secondary-button" type="button" disabled={isSaving || !["Creado", "EnEvaluacion"].includes(selected.estado)} onClick={() => void runNotificationAction("/approve", "Aviso aprobado.")}>
              <CheckCircle2 size={18} /> Aprobar
            </button>
            <button className="secondary-button" type="button" disabled={isSaving || selected.estado !== "Aprobado"} onClick={() => void convertToWorkOrder()}>
              <Wrench size={18} /> Convertir a OT
            </button>
            <button className="danger-button" type="button" disabled={isSaving || selected.estado === "ConvertidoOT" || selected.estado === "Anulado"} onClick={() => void runNotificationAction("/reject", "Aviso rechazado.")}>
              <XCircle size={18} /> Rechazar
            </button>
          </div>
        </section>
      ) : null}
    </section>
  );
}

function Metric({ icon, label, value }: { icon: JSX.Element; label: string; value: number }) {
  return (
    <article className="metric-card">
      {icon}
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

function Info({ label, value }: { label: string; value: string }) {
  return (
    <div className="info-item">
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function toIsoOrNull(value: string) {
  return value ? new Date(`${value}T00:00:00Z`).toISOString() : null;
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleDateString() : "-";
}

function normalizePriority(value?: string | null): Priority | null {
  if (!value) return null;
  const normalized = value.trim().toLowerCase();
  if (normalized === "critica" || normalized === "critico") return "Critica";
  if (normalized === "alta" || normalized === "alto") return "Alta";
  if (normalized === "baja" || normalized === "bajo") return "Baja";
  if (normalized === "media" || normalized === "medio") return "Media";
  return null;
}
