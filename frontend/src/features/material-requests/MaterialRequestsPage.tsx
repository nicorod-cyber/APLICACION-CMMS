import { FormEvent, useEffect, useMemo, useState } from "react";
import { CheckCircle2, ClipboardCheck, ClipboardList, PackagePlus, RefreshCw, Send, Truck, XCircle } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";

type RequestStatus =
  | "Solicitada"
  | "PendienteAprobacionMantenimiento"
  | "AprobadaPorMantenimiento"
  | "EnRevisionBodega"
  | "Reservada"
  | "PendienteStock"
  | "PendienteAbastecimiento"
  | "EnPreparacion"
  | "Entregada"
  | "RecibidaPorTecnico"
  | "Cerrada"
  | "Rechazada";

type RequestSource = "OT" | "Tarea" | "Bodega";
type RequestType = "RepuestoCodificado" | "MaterialNoCodificado";

type MaterialRequest = {
  numeroSolicitud: string;
  estado: RequestStatus;
  tipo: RequestType;
  origen: RequestSource;
  solicitante: string;
  solicitadoEnUtc: string;
  descripcionTecnica: string;
  cantidad: number;
  unidad: string;
  motivo: string;
  repuestoCodigo?: string | null;
  repuestoMaestroCodigo?: string | null;
  fotoReferencia?: string | null;
  activoCodigo?: string | null;
  otNumero?: string | null;
  tareaCodigo?: string | null;
  faenaCodigo?: string | null;
  bodegaCodigo?: string | null;
  reservaId?: string | null;
  movimientoEntregaId?: string | null;
  stockDecision?: string | null;
  aprobadorMantenimiento?: string | null;
  aprobadorBodega?: string | null;
  observaciones?: string | null;
};

type SparePartSummary = {
  codigo: string;
  descripcion: string;
  unidadMedida: string;
  stockDisponibleTotal: number;
  critico: boolean;
};

type WarehouseRecord = {
  codigo: string;
  nombre: string;
  faenaCodigo: string;
  activa: boolean;
};

type RequestForm = {
  source: RequestSource;
  type: RequestType;
  repuestoCodigo: string;
  descripcionTecnica: string;
  cantidad: string;
  unidad: string;
  motivo: string;
  fotoReferencia: string;
  activoCodigo: string;
  otNumero: string;
  tareaCodigo: string;
  faenaCodigo: string;
  bodegaCodigo: string;
};

const emptyForm: RequestForm = {
  source: "OT",
  type: "RepuestoCodificado",
  repuestoCodigo: "",
  descripcionTecnica: "",
  cantidad: "1",
  unidad: "UN",
  motivo: "",
  fotoReferencia: "",
  activoCodigo: "",
  otNumero: "",
  tareaCodigo: "",
  faenaCodigo: "",
  bodegaCodigo: ""
};

const statusLabels: Record<RequestStatus, string> = {
  Solicitada: "Solicitada",
  PendienteAprobacionMantenimiento: "Pendiente mant.",
  AprobadaPorMantenimiento: "Aprobada mant.",
  EnRevisionBodega: "Revision bodega",
  Reservada: "Reservada",
  PendienteStock: "Pendiente stock",
  PendienteAbastecimiento: "Abastecimiento",
  EnPreparacion: "En preparacion",
  Entregada: "Entregada",
  RecibidaPorTecnico: "Recibida",
  Cerrada: "Cerrada",
  Rechazada: "Rechazada"
};

export function MaterialRequestsPage() {
  const [requests, setRequests] = useState<MaterialRequest[]>([]);
  const [spareParts, setSpareParts] = useState<SparePartSummary[]>([]);
  const [warehouses, setWarehouses] = useState<WarehouseRecord[]>([]);
  const [selectedId, setSelectedId] = useState<string>("");
  const [form, setForm] = useState<RequestForm>(emptyForm);
  const [filters, setFilters] = useState({ status: "", type: "", source: "", faenaCodigo: "", includeClosed: false });
  const [reason, setReason] = useState("");
  const [reviewWarehouse, setReviewWarehouse] = useState("");
  const [convertForm, setConvertForm] = useState({ descripcion: "", unidadMedida: "UN", codigoSap: "", familiaEquipo: "", proveedorPreferente: "" });
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void loadAll();
  }, [filters.status, filters.type, filters.source, filters.faenaCodigo, filters.includeClosed]);

  const selected = useMemo(
    () => requests.find((item) => item.numeroSolicitud === selectedId) ?? requests[0] ?? null,
    [requests, selectedId]
  );

  const counters = useMemo(() => {
    return {
      approval: requests.filter((item) => item.estado === "PendienteAprobacionMantenimiento").length,
      warehouse: requests.filter((item) => ["AprobadaPorMantenimiento", "Reservada", "PendienteStock", "PendienteAbastecimiento", "EnPreparacion"].includes(item.estado)).length,
      noCode: requests.filter((item) => item.tipo === "MaterialNoCodificado" && !item.repuestoMaestroCodigo).length
    };
  }, [requests]);

  async function loadAll() {
    setIsLoading(true);
    setError(null);
    try {
      const query = new URLSearchParams();
      if (filters.status) query.set("status", filters.status);
      if (filters.type) query.set("type", filters.type);
      if (filters.source) query.set("source", filters.source);
      if (filters.faenaCodigo) query.set("faenaCodigo", filters.faenaCodigo);
      if (filters.includeClosed) query.set("includeClosed", "true");

      const [requestResult, spareResult, warehouseResult] = await Promise.all([
        apiFetch<MaterialRequest[]>(`/api/material-requests?${query}`),
        apiFetch<SparePartSummary[]>("/api/inventory/spare-parts?includeObsolete=true"),
        apiFetch<WarehouseRecord[]>("/api/inventory/warehouses?includeInactive=false")
      ]);
      setRequests(requestResult);
      setSpareParts(spareResult);
      setWarehouses(warehouseResult);
      if (!selectedId && requestResult[0]) {
        setSelectedId(requestResult[0].numeroSolicitud);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "No fue posible cargar las solicitudes.");
    } finally {
      setIsLoading(false);
    }
  }

  async function submitRequest(event: FormEvent) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    try {
      const created = await apiFetch<MaterialRequest>("/api/material-requests", {
        method: "POST",
        body: JSON.stringify({
          source: form.source,
          type: form.type,
          repuestoCodigo: form.type === "RepuestoCodificado" ? form.repuestoCodigo : null,
          descripcionTecnica: form.descripcionTecnica,
          cantidad: Number(form.cantidad),
          unidad: form.unidad,
          motivo: form.motivo,
          fotoReferencia: form.fotoReferencia || null,
          activoCodigo: form.activoCodigo || null,
          otNumero: form.otNumero || null,
          tareaCodigo: form.source === "Tarea" ? form.tareaCodigo : null,
          faenaCodigo: form.faenaCodigo || null,
          bodegaCodigo: form.bodegaCodigo || null
        })
      });
      setForm(emptyForm);
      setSelectedId(created.numeroSolicitud);
      setMessage(`Solicitud ${created.numeroSolicitud} creada.`);
      await loadAll();
    } catch (err) {
      setError(err instanceof Error ? err.message : "No fue posible crear la solicitud.");
    } finally {
      setIsSaving(false);
    }
  }

  async function runAction(path: string, body: unknown, success: string) {
    if (!selected) return;
    setIsSaving(true);
    setError(null);
    try {
      const updated = await apiFetch<MaterialRequest>(`/api/material-requests/${encodeURIComponent(selected.numeroSolicitud)}${path}`, {
        method: "POST",
        body: JSON.stringify(body)
      });
      setSelectedId(updated.numeroSolicitud);
      setReason("");
      setMessage(success);
      await loadAll();
    } catch (err) {
      setError(err instanceof Error ? err.message : "No fue posible completar la accion.");
    } finally {
      setIsSaving(false);
    }
  }

  function applySpare(code: string) {
    const spare = spareParts.find((item) => item.codigo === code);
    setForm({
      ...form,
      repuestoCodigo: code,
      descripcionTecnica: spare?.descripcion ?? form.descripcionTecnica,
      unidad: spare?.unidadMedida ?? form.unidad
    });
  }

  return (
    <section className="stack">
      <header className="page-header">
        <div>
          <p className="eyebrow">Repuestos y materiales</p>
          <h1>Solicitudes</h1>
          <p>Flujo completo desde OT, tarea o bodega hasta reserva, entrega, recepcion y cierre.</p>
        </div>
        <button className="secondary-button" type="button" onClick={() => void loadAll()}>
          <RefreshCw size={18} /> Actualizar
        </button>
      </header>

      <div className="kpi-grid">
        <Metric icon={<ClipboardCheck size={18} />} label="Aprobacion mantenimiento" value={counters.approval} />
        <Metric icon={<Truck size={18} />} label="Bandeja bodega" value={counters.warehouse} />
        <Metric icon={<PackagePlus size={18} />} label="No codificados" value={counters.noCode} />
      </div>

      {message ? <div className="success-banner">{message}</div> : null}
      {error ? <div className="error-banner">{error}</div> : null}

      <div className="two-column-layout">
        <form className="panel stack" onSubmit={submitRequest}>
          <div className="section-heading">
            <h2>Nueva solicitud</h2>
          </div>
          <div className="form-grid">
            <label>
              Origen
              <select value={form.source} onChange={(event) => setForm({ ...form, source: event.target.value as RequestSource })}>
                <option value="OT">OT</option>
                <option value="Tarea">Tarea</option>
                <option value="Bodega">Bodega</option>
              </select>
            </label>
            <label>
              Tipo
              <select value={form.type} onChange={(event) => setForm({ ...form, type: event.target.value as RequestType })}>
                <option value="RepuestoCodificado">Repuesto codificado</option>
                <option value="MaterialNoCodificado">Material no codificado</option>
              </select>
            </label>
            {form.type === "RepuestoCodificado" ? (
              <label>
                Repuesto
                <select value={form.repuestoCodigo} onChange={(event) => applySpare(event.target.value)} required>
                  <option value="">Selecciona repuesto</option>
                  {spareParts.map((item) => (
                    <option key={item.codigo} value={item.codigo}>
                      {item.codigo} - {item.descripcion}
                    </option>
                  ))}
                </select>
              </label>
            ) : null}
            <FaenaSelect emptyLabel="Selecciona faena" value={form.faenaCodigo} onChange={(value) => setForm({ ...form, faenaCodigo: value })} />
            <label>
              Bodega
              <select value={form.bodegaCodigo} onChange={(event) => setForm({ ...form, bodegaCodigo: event.target.value })}>
                <option value="">Selecciona bodega</option>
                {warehouses.map((item) => (
                  <option key={item.codigo} value={item.codigo}>
                    {item.nombre} ({item.codigo})
                  </option>
                ))}
              </select>
            </label>
            <label>
              OT
              <input value={form.otNumero} onChange={(event) => setForm({ ...form, otNumero: event.target.value })} required={form.source !== "Bodega"} />
            </label>
            <label>
              Tarea
              <input value={form.tareaCodigo} onChange={(event) => setForm({ ...form, tareaCodigo: event.target.value })} required={form.source === "Tarea"} />
            </label>
            <label>
              Activo
              <input value={form.activoCodigo} onChange={(event) => setForm({ ...form, activoCodigo: event.target.value })} />
            </label>
            <label>
              Cantidad
              <input type="number" min="0.01" step="0.01" value={form.cantidad} onChange={(event) => setForm({ ...form, cantidad: event.target.value })} required />
            </label>
            <label>
              Unidad
              <input value={form.unidad} onChange={(event) => setForm({ ...form, unidad: event.target.value })} required />
            </label>
            <label className="span-2">
              Descripcion tecnica
              <textarea value={form.descripcionTecnica} onChange={(event) => setForm({ ...form, descripcionTecnica: event.target.value })} required />
            </label>
            <label>
              Foto o referencia
              <input value={form.fotoReferencia} onChange={(event) => setForm({ ...form, fotoReferencia: event.target.value })} />
            </label>
            <label>
              Motivo
              <input value={form.motivo} onChange={(event) => setForm({ ...form, motivo: event.target.value })} required />
            </label>
          </div>
          <button className="primary-button" type="submit" disabled={isSaving}>
            <Send size={18} /> Crear solicitud
          </button>
        </form>

        <section className="panel stack">
          <div className="section-heading">
            <h2>Bandejas</h2>
            <span>{requests.length} solicitudes</span>
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
              <option value="RepuestoCodificado">Codificado</option>
              <option value="MaterialNoCodificado">No codificado</option>
            </select>
            <select value={filters.source} onChange={(event) => setFilters({ ...filters, source: event.target.value })}>
              <option value="">Todos los origenes</option>
              <option value="OT">OT</option>
              <option value="Tarea">Tarea</option>
              <option value="Bodega">Bodega</option>
            </select>
            <label className="check-row">
              <input type="checkbox" checked={filters.includeClosed} onChange={(event) => setFilters({ ...filters, includeClosed: event.target.checked })} />
              Cerradas
            </label>
          </div>
          <FaenaSelect value={filters.faenaCodigo} onChange={(value) => setFilters({ ...filters, faenaCodigo: value })} />

          {isLoading ? <p>Cargando solicitudes...</p> : null}
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>Solicitud</th>
                  <th>Material</th>
                  <th>Estado</th>
                  <th>Contexto</th>
                  <th>Stock</th>
                </tr>
              </thead>
              <tbody>
                {requests.map((item) => (
                  <tr key={item.numeroSolicitud} className={selected?.numeroSolicitud === item.numeroSolicitud ? "selected-row" : ""} onClick={() => setSelectedId(item.numeroSolicitud)}>
                    <td>
                      <strong>{item.numeroSolicitud}</strong>
                      <small>{new Date(item.solicitadoEnUtc).toLocaleDateString()}</small>
                    </td>
                    <td>
                      <strong>{item.repuestoCodigo ?? "No codificado"}</strong>
                      <small>{item.descripcionTecnica}</small>
                    </td>
                    <td>
                      <span className={`status-pill ${item.estado === "Rechazada" ? "danger" : item.estado === "Cerrada" ? "success" : ""}`}>
                        {statusLabels[item.estado]}
                      </span>
                    </td>
                    <td>
                      <span>{item.origen}</span>
                      <small>{[item.otNumero, item.tareaCodigo, item.activoCodigo, item.faenaCodigo].filter(Boolean).join(" / ") || "Sin contexto"}</small>
                    </td>
                    <td>{item.stockDecision ?? "-"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      {selected ? (
        <section className="panel stack">
          <div className="section-heading">
            <div>
              <h2>{selected.numeroSolicitud}</h2>
              <p>
                {selected.cantidad} {selected.unidad} · {selected.descripcionTecnica}
              </p>
            </div>
            <span className="status-pill">{statusLabels[selected.estado]}</span>
          </div>

          <div className="detail-grid">
            <Info label="Solicitante" value={selected.solicitante} />
            <Info label="Origen" value={selected.origen} />
            <Info label="Tipo" value={selected.tipo === "MaterialNoCodificado" ? "Material no codificado" : "Repuesto codificado"} />
            <Info label="Repuesto" value={selected.repuestoCodigo ?? "-"} />
            <Info label="Faena" value={selected.faenaCodigo ?? "-"} />
            <Info label="Bodega" value={selected.bodegaCodigo ?? "-"} />
            <Info label="Reserva" value={selected.reservaId ?? "-"} />
            <Info label="Movimiento entrega" value={selected.movimientoEntregaId ?? "-"} />
            <Info label="Motivo" value={selected.motivo} />
            <Info label="Observaciones" value={selected.observaciones ?? "-"} />
          </div>

          <div className="form-grid">
            <label>
              Motivo accion
              <input value={reason} onChange={(event) => setReason(event.target.value)} placeholder="Motivo auditado" />
            </label>
            <label>
              Bodega revision/entrega
              <select value={reviewWarehouse || selected.bodegaCodigo || ""} onChange={(event) => setReviewWarehouse(event.target.value)}>
                <option value="">Selecciona bodega</option>
                {warehouses.map((item) => (
                  <option key={item.codigo} value={item.codigo}>
                    {item.nombre} ({item.codigo})
                  </option>
                ))}
              </select>
            </label>
          </div>

          <div className="toolbar">
            <button className="secondary-button" type="button" disabled={isSaving} onClick={() => void runAction("/maintenance-approval", { reason }, "Solicitud aprobada por mantenimiento.")}>
              <CheckCircle2 size={18} /> Aprobar mant.
            </button>
            <button className="secondary-button" type="button" disabled={isSaving} onClick={() => void runAction("/warehouse-review", { bodegaCodigo: reviewWarehouse || selected.bodegaCodigo, reason }, "Revision de bodega completada.")}>
              <ClipboardList size={18} /> Revisar bodega
            </button>
            <button className="secondary-button" type="button" disabled={isSaving} onClick={() => void runAction("/prepare", { reason }, "Solicitud en preparacion.")}>
              <PackagePlus size={18} /> Preparar
            </button>
            <button className="secondary-button" type="button" disabled={isSaving} onClick={() => void runAction("/deliver", { bodegaCodigo: reviewWarehouse || selected.bodegaCodigo, reason }, "Material entregado.")}>
              <Truck size={18} /> Entregar
            </button>
            <button className="secondary-button" type="button" disabled={isSaving} onClick={() => void runAction("/receive", { reason }, "Material recibido por tecnico.")}>
              <ClipboardCheck size={18} /> Recibir
            </button>
            <button className="secondary-button" type="button" disabled={isSaving} onClick={() => void runAction("/close", { reason }, "Solicitud cerrada.")}>
              <CheckCircle2 size={18} /> Cerrar
            </button>
            <button className="danger-button" type="button" disabled={isSaving} onClick={() => void runAction("/reject", { reason }, "Solicitud rechazada.")}>
              <XCircle size={18} /> Rechazar
            </button>
          </div>

          {selected.tipo === "MaterialNoCodificado" ? (
            <div className="panel-muted stack">
              <h3>Convertir material no codificado</h3>
              <div className="form-grid">
                <label>
                  Descripcion maestro
                  <input value={convertForm.descripcion} onChange={(event) => setConvertForm({ ...convertForm, descripcion: event.target.value })} />
                </label>
                <label>
                  Unidad
                  <input value={convertForm.unidadMedida} onChange={(event) => setConvertForm({ ...convertForm, unidadMedida: event.target.value })} />
                </label>
                <label>
                  Codigo SAP
                  <input value={convertForm.codigoSap} onChange={(event) => setConvertForm({ ...convertForm, codigoSap: event.target.value })} />
                </label>
                <label>
                  Familia equipo
                  <input value={convertForm.familiaEquipo} onChange={(event) => setConvertForm({ ...convertForm, familiaEquipo: event.target.value })} />
                </label>
                <label>
                  Proveedor
                  <input value={convertForm.proveedorPreferente} onChange={(event) => setConvertForm({ ...convertForm, proveedorPreferente: event.target.value })} />
                </label>
              </div>
              <button
                className="secondary-button"
                type="button"
                disabled={isSaving || Boolean(selected.repuestoMaestroCodigo) || !selected.aprobadorMantenimiento || !selected.aprobadorBodega}
                onClick={() =>
                  void runAction(
                    "/convert-to-spare-part",
                    {
                      ...convertForm,
                      descripcion: convertForm.descripcion || selected.descripcionTecnica,
                      descripcionTecnica: selected.descripcionTecnica,
                      codigoSap: convertForm.codigoSap || null,
                      familiaEquipo: convertForm.familiaEquipo || null,
                      proveedorPreferente: convertForm.proveedorPreferente || null
                    },
                    "Material convertido a repuesto maestro."
                  )
                }
              >
                <PackagePlus size={18} /> Convertir a maestro
              </button>
            </div>
          ) : null}
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
