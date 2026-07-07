import { FormEvent, useEffect, useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2, ClipboardList, PackageCheck, RefreshCw, Save, ShoppingCart, Truck } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";

type ProcurementStatus = "EnviadaAbastecimiento" | "OCAsociada" | "RecepcionParcial" | "Recepcionada" | "Entregada" | "Cerrada" | "Cancelada";

type Supplier = {
  rut: string;
  nombre: string;
  contacto?: string | null;
  email?: string | null;
  telefono?: string | null;
  direccion?: string | null;
  leadTimeEsperadoDias: number;
  activo: boolean;
  observaciones?: string | null;
};

type LeadTime = {
  solicitudAprobacionDias?: number | null;
  aprobacionEnvioDias?: number | null;
  envioOCDias?: number | null;
  ocRecepcionDias?: number | null;
  recepcionEntregaDias?: number | null;
  totalDias?: number | null;
};

type ProcurementRequest = {
  solicitudId: string;
  estado: ProcurementStatus;
  solicitudInternaCmms?: string | null;
  solicitudExternaNumero?: string | null;
  ocNumero?: string | null;
  proveedorRut?: string | null;
  proveedorNombre?: string | null;
  repuestoCodigo?: string | null;
  descripcion: string;
  cantidad: number;
  unidad: string;
  cantidadRecibida: number;
  cantidadEntregada: number;
  faenaCodigo?: string | null;
  bodegaCodigo?: string | null;
  otNumero?: string | null;
  activoCodigo?: string | null;
  motivo: string;
  fechaSolicitudTecnica: string;
  fechaAprobacionMantenimiento?: string | null;
  fechaEnvioAbastecimiento: string;
  fechaOC?: string | null;
  fechaComprometida?: string | null;
  fechaRecepcion?: string | null;
  fechaEntrega?: string | null;
  costoEstimado?: number | null;
  costoOC?: number | null;
  costoReal?: number | null;
  moneda: string;
  documentoRespaldoUrl?: string | null;
  documentoOcUrl?: string | null;
  documentoRecepcionUrl?: string | null;
  documentoEntregaUrl?: string | null;
  leadTime: LeadTime;
  estaVencida: boolean;
};

type SparePart = {
  codigo: string;
  descripcion: string;
  unidadMedida: string;
};

type Warehouse = {
  codigo: string;
  nombre: string;
  faenaCodigo: string;
};

type MaterialRequest = {
  numeroSolicitud: string;
  estado: string;
  descripcionTecnica: string;
  cantidad: number;
  unidad: string;
  repuestoCodigo?: string | null;
  repuestoMaestroCodigo?: string | null;
  faenaCodigo?: string | null;
  bodegaCodigo?: string | null;
  otNumero?: string | null;
  activoCodigo?: string | null;
};

const statusLabels: Record<ProcurementStatus, string> = {
  EnviadaAbastecimiento: "Enviada",
  OCAsociada: "OC asociada",
  RecepcionParcial: "Parcial",
  Recepcionada: "Recepcionada",
  Entregada: "Entregada",
  Cerrada: "Cerrada",
  Cancelada: "Cancelada"
};

const emptyRequestForm = {
  solicitudInternaCmms: "",
  repuestoCodigo: "",
  descripcion: "",
  cantidad: "1",
  unidad: "UN",
  motivo: "",
  faenaCodigo: "",
  bodegaCodigo: "",
  otNumero: "",
  activoCodigo: "",
  costoEstimado: "",
  documentoRespaldoUrl: ""
};

const emptySupplierForm = {
  rut: "",
  nombre: "",
  contacto: "",
  email: "",
  telefono: "",
  direccion: "",
  leadTimeEsperadoDias: "0",
  activo: true,
  observaciones: ""
};

export function ProcurementPage() {
  const [requests, setRequests] = useState<ProcurementRequest[]>([]);
  const [suppliers, setSuppliers] = useState<Supplier[]>([]);
  const [spareParts, setSpareParts] = useState<SparePart[]>([]);
  const [warehouses, setWarehouses] = useState<Warehouse[]>([]);
  const [materialRequests, setMaterialRequests] = useState<MaterialRequest[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [requestForm, setRequestForm] = useState(emptyRequestForm);
  const [supplierForm, setSupplierForm] = useState(emptySupplierForm);
  const [editingSupplierRut, setEditingSupplierRut] = useState("");
  const [filters, setFilters] = useState({ status: "", supplierRut: "", faenaCodigo: "", overdueOnly: false, includeClosed: false });
  const [poForm, setPoForm] = useState({ solicitudExternaNumero: "", ocNumero: "", proveedorRut: "", fechaComprometida: "", fechaOC: "", costoOC: "", documentoOcUrl: "", reason: "" });
  const [receptionForm, setReceptionForm] = useState({ cantidadRecibida: "", bodegaCodigo: "", despachoDirectoOt: false, otNumero: "", activoCodigo: "", faenaCodigo: "", fechaRecepcion: "", fechaEntrega: "", costoReal: "", documentoRecepcionUrl: "", documentoEntregaUrl: "", reason: "" });
  const [deliveryForm, setDeliveryForm] = useState({ cantidadEntregada: "", bodegaCodigo: "", otNumero: "", activoCodigo: "", faenaCodigo: "", fechaEntrega: "", documentoEntregaUrl: "", reason: "" });
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void loadAll();
  }, [filters.status, filters.supplierRut, filters.faenaCodigo, filters.overdueOnly, filters.includeClosed]);

  const selected = useMemo(() => requests.find((item) => item.solicitudId === selectedId) ?? requests[0] ?? null, [requests, selectedId]);
  const pendingReception = requests.filter((item) => item.estado === "OCAsociada" || item.estado === "RecepcionParcial");
  const overdue = requests.filter((item) => item.estaVencida);
  const averageLead = average(requests.map((item) => item.leadTime.totalDias).filter((value): value is number => typeof value === "number"));

  async function loadAll() {
    setIsLoading(true);
    setError(null);
    try {
      const query = new URLSearchParams();
      if (filters.status) query.set("status", filters.status);
      if (filters.supplierRut) query.set("supplierRut", filters.supplierRut);
      if (filters.faenaCodigo) query.set("faenaCodigo", filters.faenaCodigo);
      if (filters.overdueOnly) query.set("overdueOnly", "true");
      if (filters.includeClosed) query.set("includeClosed", "true");

      const [requestResult, supplierResult, spareResult, warehouseResult, materialResult] = await Promise.all([
        apiFetch<ProcurementRequest[]>(`/api/procurement/requests?${query}`),
        apiFetch<Supplier[]>("/api/procurement/suppliers?includeInactive=true"),
        apiFetch<SparePart[]>("/api/inventory/spare-parts?includeObsolete=true"),
        apiFetch<Warehouse[]>("/api/inventory/warehouses?includeInactive=false"),
        apiFetch<MaterialRequest[]>("/api/material-requests?includeClosed=true")
      ]);
      setRequests(requestResult);
      setSuppliers(supplierResult);
      setSpareParts(spareResult);
      setWarehouses(warehouseResult);
      setMaterialRequests(materialResult);
      if (!selectedId && requestResult[0]) {
        setSelectedId(requestResult[0].solicitudId);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar abastecimiento.");
    } finally {
      setIsLoading(false);
    }
  }

  async function createRequest(event: FormEvent) {
    event.preventDefault();
    await saveAction(async () => {
      const created = await apiFetch<ProcurementRequest>("/api/procurement/requests", {
        method: "POST",
        body: JSON.stringify({
          ...requestForm,
          cantidad: Number(requestForm.cantidad),
          costoEstimado: toNumberOrNull(requestForm.costoEstimado),
          solicitudInternaCmms: emptyToNull(requestForm.solicitudInternaCmms),
          repuestoCodigo: emptyToNull(requestForm.repuestoCodigo),
          faenaCodigo: emptyToNull(requestForm.faenaCodigo),
          bodegaCodigo: emptyToNull(requestForm.bodegaCodigo),
          otNumero: emptyToNull(requestForm.otNumero),
          activoCodigo: emptyToNull(requestForm.activoCodigo),
          documentoRespaldoUrl: emptyToNull(requestForm.documentoRespaldoUrl)
        })
      });
      setSelectedId(created.solicitudId);
      setRequestForm(emptyRequestForm);
      setMessage(`Solicitud ${created.solicitudId} enviada a abastecimiento.`);
    });
  }

  async function saveSupplier(event: FormEvent) {
    event.preventDefault();
    await saveAction(async () => {
      const body = {
        ...supplierForm,
        leadTimeEsperadoDias: Number(supplierForm.leadTimeEsperadoDias || 0)
      };
      if (editingSupplierRut) {
        await apiFetch<Supplier>(`/api/procurement/suppliers/${encodeURIComponent(editingSupplierRut)}`, { method: "PUT", body: JSON.stringify(body) });
        setMessage("Proveedor actualizado.");
      } else {
        await apiFetch<Supplier>("/api/procurement/suppliers", { method: "POST", body: JSON.stringify(body) });
        setMessage("Proveedor creado.");
      }
      setSupplierForm(emptySupplierForm);
      setEditingSupplierRut("");
    });
  }

  async function linkPurchaseOrder(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch<ProcurementRequest>(`/api/procurement/requests/${encodeURIComponent(selected.solicitudId)}/purchase-order`, {
        method: "POST",
        body: JSON.stringify({
          ...poForm,
          fechaComprometida: toIsoDate(poForm.fechaComprometida),
          fechaOC: toIsoOrNull(poForm.fechaOC),
          costoOC: toNumberOrNull(poForm.costoOC),
          documentoOcUrl: emptyToNull(poForm.documentoOcUrl),
          solicitudExternaNumero: emptyToNull(poForm.solicitudExternaNumero)
        })
      });
      setPoForm({ solicitudExternaNumero: "", ocNumero: "", proveedorRut: "", fechaComprometida: "", fechaOC: "", costoOC: "", documentoOcUrl: "", reason: "" });
      setMessage("OC asociada.");
    });
  }

  async function registerReception(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch<ProcurementRequest>(`/api/procurement/requests/${encodeURIComponent(selected.solicitudId)}/receptions`, {
        method: "POST",
        body: JSON.stringify({
          ...receptionForm,
          cantidadRecibida: Number(receptionForm.cantidadRecibida),
          fechaRecepcion: toIsoOrNull(receptionForm.fechaRecepcion),
          fechaEntrega: toIsoOrNull(receptionForm.fechaEntrega),
          costoReal: toNumberOrNull(receptionForm.costoReal),
          documentoRecepcionUrl: emptyToNull(receptionForm.documentoRecepcionUrl),
          documentoEntregaUrl: emptyToNull(receptionForm.documentoEntregaUrl),
          otNumero: emptyToNull(receptionForm.otNumero),
          activoCodigo: emptyToNull(receptionForm.activoCodigo),
          faenaCodigo: emptyToNull(receptionForm.faenaCodigo)
        })
      });
      setReceptionForm({ cantidadRecibida: "", bodegaCodigo: "", despachoDirectoOt: false, otNumero: "", activoCodigo: "", faenaCodigo: "", fechaRecepcion: "", fechaEntrega: "", costoReal: "", documentoRecepcionUrl: "", documentoEntregaUrl: "", reason: "" });
      setMessage("Recepcion registrada.");
    });
  }

  async function registerDelivery(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch<ProcurementRequest>(`/api/procurement/requests/${encodeURIComponent(selected.solicitudId)}/delivery`, {
        method: "POST",
        body: JSON.stringify({
          ...deliveryForm,
          cantidadEntregada: Number(deliveryForm.cantidadEntregada),
          fechaEntrega: toIsoOrNull(deliveryForm.fechaEntrega),
          documentoEntregaUrl: emptyToNull(deliveryForm.documentoEntregaUrl),
          otNumero: emptyToNull(deliveryForm.otNumero),
          activoCodigo: emptyToNull(deliveryForm.activoCodigo),
          faenaCodigo: emptyToNull(deliveryForm.faenaCodigo)
        })
      });
      setDeliveryForm({ cantidadEntregada: "", bodegaCodigo: "", otNumero: "", activoCodigo: "", faenaCodigo: "", fechaEntrega: "", documentoEntregaUrl: "", reason: "" });
      setMessage("Entrega registrada.");
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
      setError(saveError instanceof Error ? saveError.message : "No fue posible completar la operacion.");
    } finally {
      setIsSaving(false);
    }
  }

  function applyMaterialRequest(number: string) {
    const source = materialRequests.find((item) => item.numeroSolicitud === number);
    setRequestForm({
      ...requestForm,
      solicitudInternaCmms: number,
      repuestoCodigo: source?.repuestoMaestroCodigo ?? source?.repuestoCodigo ?? requestForm.repuestoCodigo,
      descripcion: source?.descripcionTecnica ?? requestForm.descripcion,
      cantidad: source?.cantidad ? String(source.cantidad) : requestForm.cantidad,
      unidad: source?.unidad ?? requestForm.unidad,
      faenaCodigo: source?.faenaCodigo ?? requestForm.faenaCodigo,
      bodegaCodigo: source?.bodegaCodigo ?? requestForm.bodegaCodigo,
      otNumero: source?.otNumero ?? requestForm.otNumero,
      activoCodigo: source?.activoCodigo ?? requestForm.activoCodigo
    });
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Abastecimiento</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Seguimiento de solicitudes, OC, proveedores, recepciones y lead time.</p>
        </div>
        <button className="secondary-button" type="button" onClick={() => void loadAll()}>
          <RefreshCw className="h-4 w-4" /> Actualizar
        </button>
      </div>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Metric icon={<ShoppingCart className="h-5 w-5 text-slate-400" />} label="Solicitudes" value={requests.length} />
        <Metric icon={<ClipboardList className="h-5 w-5 text-slate-400" />} label="Con OC / parcial" value={pendingReception.length} />
        <Metric icon={<AlertTriangle className="h-5 w-5 text-amber-500" />} label="Vencidas" value={overdue.length} />
        <Metric icon={<Truck className="h-5 w-5 text-slate-400" />} label="Lead prom." value={averageLead} suffix=" dias" />
        <Metric icon={<PackageCheck className="h-5 w-5 text-slate-400" />} label="Proveedores" value={suppliers.length} />
      </section>

      {message ? <div className="success-banner">{message}</div> : null}
      {error ? <div className="error-banner">{error}</div> : null}

      <section className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <form className="panel space-y-4" onSubmit={createRequest}>
          <SectionTitle title="Nueva solicitud a abastecimiento" />
          <div className="form-grid">
            <label>
              Solicitud CMMS
              <select value={requestForm.solicitudInternaCmms} onChange={(event) => applyMaterialRequest(event.target.value)}>
                <option value="">Manual</option>
                {materialRequests.map((item) => (
                  <option key={item.numeroSolicitud} value={item.numeroSolicitud}>
                    {item.numeroSolicitud} - {item.descripcionTecnica}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Repuesto
              <select value={requestForm.repuestoCodigo} onChange={(event) => setRequestForm({ ...requestForm, repuestoCodigo: event.target.value })}>
                <option value="">Sin codigo</option>
                {spareParts.map((item) => (
                  <option key={item.codigo} value={item.codigo}>
                    {item.codigo} - {item.descripcion}
                  </option>
                ))}
              </select>
            </label>
            <FaenaSelect emptyLabel="Selecciona faena" value={requestForm.faenaCodigo} onChange={(value) => setRequestForm({ ...requestForm, faenaCodigo: value })} />
            <label>
              Bodega destino
              <select value={requestForm.bodegaCodigo} onChange={(event) => setRequestForm({ ...requestForm, bodegaCodigo: event.target.value })}>
                <option value="">Selecciona bodega</option>
                {warehouses.map((item) => (
                  <option key={item.codigo} value={item.codigo}>
                    {item.nombre} ({item.codigo})
                  </option>
                ))}
              </select>
            </label>
            <label className="span-2">
              Descripcion
              <input value={requestForm.descripcion} onChange={(event) => setRequestForm({ ...requestForm, descripcion: event.target.value })} required />
            </label>
            <label>
              Cantidad
              <input type="number" min="0.01" step="0.01" value={requestForm.cantidad} onChange={(event) => setRequestForm({ ...requestForm, cantidad: event.target.value })} required />
            </label>
            <label>
              Unidad
              <input value={requestForm.unidad} onChange={(event) => setRequestForm({ ...requestForm, unidad: event.target.value })} required />
            </label>
            <label>
              OT
              <input value={requestForm.otNumero} onChange={(event) => setRequestForm({ ...requestForm, otNumero: event.target.value })} />
            </label>
            <label>
              Activo
              <input value={requestForm.activoCodigo} onChange={(event) => setRequestForm({ ...requestForm, activoCodigo: event.target.value })} />
            </label>
            <label>
              Costo estimado
              <input type="number" min="0" step="1" value={requestForm.costoEstimado} onChange={(event) => setRequestForm({ ...requestForm, costoEstimado: event.target.value })} />
            </label>
            <label>
              Documento respaldo
              <input value={requestForm.documentoRespaldoUrl} onChange={(event) => setRequestForm({ ...requestForm, documentoRespaldoUrl: event.target.value })} />
            </label>
            <label className="span-2">
              Motivo
              <input value={requestForm.motivo} onChange={(event) => setRequestForm({ ...requestForm, motivo: event.target.value })} required />
            </label>
          </div>
          <button className="primary-button" type="submit" disabled={isSaving}>
            <Save className="h-4 w-4" /> Enviar a abastecimiento
          </button>
        </form>

        <section className="panel space-y-4">
          <SectionTitle title="Bandeja" value={`${requests.length} solicitudes`} />
          <div className="toolbar">
            <select value={filters.status} onChange={(event) => setFilters({ ...filters, status: event.target.value })}>
              <option value="">Todos los estados</option>
              {Object.entries(statusLabels).map(([key, label]) => (
                <option key={key} value={key}>
                  {label}
                </option>
              ))}
            </select>
            <select value={filters.supplierRut} onChange={(event) => setFilters({ ...filters, supplierRut: event.target.value })}>
              <option value="">Todos los proveedores</option>
              {suppliers.map((item) => (
                <option key={item.rut} value={item.rut}>
                  {item.nombre}
                </option>
              ))}
            </select>
            <label className="check-row">
              <input type="checkbox" checked={filters.overdueOnly} onChange={(event) => setFilters({ ...filters, overdueOnly: event.target.checked })} />
              Vencidas
            </label>
            <label className="check-row">
              <input type="checkbox" checked={filters.includeClosed} onChange={(event) => setFilters({ ...filters, includeClosed: event.target.checked })} />
              Cerradas
            </label>
          </div>
          <FaenaSelect value={filters.faenaCodigo} onChange={(value) => setFilters({ ...filters, faenaCodigo: value })} />
          {isLoading ? <p className="text-sm text-slate-500 dark:text-slate-400">Cargando abastecimiento...</p> : null}
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>Solicitud</th>
                  <th>Material</th>
                  <th>OC</th>
                  <th>Estado</th>
                  <th>Lead</th>
                </tr>
              </thead>
              <tbody>
                {requests.map((item) => (
                  <tr key={item.solicitudId} className={selected?.solicitudId === item.solicitudId ? "selected-row" : ""} onClick={() => setSelectedId(item.solicitudId)}>
                    <td>
                      <strong>{item.solicitudId}</strong>
                      <small>{item.solicitudInternaCmms ?? item.solicitudExternaNumero ?? "-"}</small>
                    </td>
                    <td>
                      <strong>{item.repuestoCodigo ?? "Sin codigo"}</strong>
                      <small>{item.descripcion}</small>
                    </td>
                    <td>
                      <strong>{item.ocNumero ?? "-"}</strong>
                      <small>{item.proveedorNombre ?? item.proveedorRut ?? "-"}</small>
                    </td>
                    <td>
                      <span className={`status-pill ${item.estaVencida ? "danger" : item.estado === "Entregada" ? "success" : ""}`}>{statusLabels[item.estado]}</span>
                    </td>
                    <td>{item.leadTime.totalDias ?? "-"} dias</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </section>

      {selected ? (
        <section className="panel space-y-4">
          <div className="section-heading">
            <div>
              <h2>{selected.solicitudId} · {selected.descripcion}</h2>
              <p>{selected.cantidadRecibida}/{selected.cantidad} recibidos · {selected.cantidadEntregada} entregados</p>
            </div>
            <span className={`status-pill ${selected.estaVencida ? "danger" : ""}`}>{statusLabels[selected.estado]}</span>
          </div>
          <div className="detail-grid">
            <Info label="Proveedor" value={selected.proveedorNombre ?? selected.proveedorRut ?? "-"} />
            <Info label="OC" value={selected.ocNumero ?? "-"} />
            <Info label="Compromiso" value={formatDate(selected.fechaComprometida)} />
            <Info label="Costo OC" value={formatCurrency(selected.costoOC, selected.moneda)} />
            <Info label="Sol. a aprob." value={formatDays(selected.leadTime.solicitudAprobacionDias)} />
            <Info label="Aprob. a envio" value={formatDays(selected.leadTime.aprobacionEnvioDias)} />
            <Info label="Envio a OC" value={formatDays(selected.leadTime.envioOCDias)} />
            <Info label="OC a recepcion" value={formatDays(selected.leadTime.ocRecepcionDias)} />
            <Info label="Recep. a entrega" value={formatDays(selected.leadTime.recepcionEntregaDias)} />
            <Info label="Lead total" value={formatDays(selected.leadTime.totalDias)} />
          </div>

          <section className="grid gap-4 xl:grid-cols-3">
            <form className="panel-muted space-y-3" onSubmit={linkPurchaseOrder}>
              <h3>Orden de compra</h3>
              <div className="form-grid xl:grid-cols-1">
                <label>Solicitud externa<input value={poForm.solicitudExternaNumero} onChange={(event) => setPoForm({ ...poForm, solicitudExternaNumero: event.target.value })} /></label>
                <label>OC<input value={poForm.ocNumero} onChange={(event) => setPoForm({ ...poForm, ocNumero: event.target.value })} required /></label>
                <label>Proveedor<select value={poForm.proveedorRut} onChange={(event) => setPoForm({ ...poForm, proveedorRut: event.target.value })} required><option value="">Selecciona proveedor</option>{suppliers.map((item) => <option key={item.rut} value={item.rut}>{item.nombre}</option>)}</select></label>
                <label>Fecha OC<input type="date" value={poForm.fechaOC} onChange={(event) => setPoForm({ ...poForm, fechaOC: event.target.value })} /></label>
                <label>Fecha compromiso<input type="date" value={poForm.fechaComprometida} onChange={(event) => setPoForm({ ...poForm, fechaComprometida: event.target.value })} required /></label>
                <label>Costo OC<input type="number" min="0" value={poForm.costoOC} onChange={(event) => setPoForm({ ...poForm, costoOC: event.target.value })} /></label>
                <label>Documento OC<input value={poForm.documentoOcUrl} onChange={(event) => setPoForm({ ...poForm, documentoOcUrl: event.target.value })} /></label>
                <label>Motivo<input value={poForm.reason} onChange={(event) => setPoForm({ ...poForm, reason: event.target.value })} required /></label>
              </div>
              <button className="secondary-button" type="submit" disabled={isSaving}>Asociar OC</button>
            </form>

            <form className="panel-muted space-y-3" onSubmit={registerReception}>
              <h3>Recepcion</h3>
              <div className="form-grid xl:grid-cols-1">
                <label>Cantidad<input type="number" min="0.01" step="0.01" value={receptionForm.cantidadRecibida} onChange={(event) => setReceptionForm({ ...receptionForm, cantidadRecibida: event.target.value })} required /></label>
                <label>Bodega<select value={receptionForm.bodegaCodigo} onChange={(event) => setReceptionForm({ ...receptionForm, bodegaCodigo: event.target.value })} required><option value="">Selecciona bodega</option>{warehouses.map((item) => <option key={item.codigo} value={item.codigo}>{item.nombre}</option>)}</select></label>
                <label>Fecha recepcion<input type="date" value={receptionForm.fechaRecepcion} onChange={(event) => setReceptionForm({ ...receptionForm, fechaRecepcion: event.target.value })} /></label>
                <label>Costo real<input type="number" min="0" value={receptionForm.costoReal} onChange={(event) => setReceptionForm({ ...receptionForm, costoReal: event.target.value })} /></label>
                <label className="check-row"><input type="checkbox" checked={receptionForm.despachoDirectoOt} onChange={(event) => setReceptionForm({ ...receptionForm, despachoDirectoOt: event.target.checked })} />Despacho directo</label>
                <label>OT<input value={receptionForm.otNumero} onChange={(event) => setReceptionForm({ ...receptionForm, otNumero: event.target.value })} /></label>
                <label>Fecha entrega<input type="date" value={receptionForm.fechaEntrega} onChange={(event) => setReceptionForm({ ...receptionForm, fechaEntrega: event.target.value })} /></label>
                <label>Documento recepcion<input value={receptionForm.documentoRecepcionUrl} onChange={(event) => setReceptionForm({ ...receptionForm, documentoRecepcionUrl: event.target.value })} /></label>
                <label>Motivo<input value={receptionForm.reason} onChange={(event) => setReceptionForm({ ...receptionForm, reason: event.target.value })} required /></label>
              </div>
              <button className="secondary-button" type="submit" disabled={isSaving}>Registrar recepcion</button>
            </form>

            <form className="panel-muted space-y-3" onSubmit={registerDelivery}>
              <h3>Entrega a OT</h3>
              <div className="form-grid xl:grid-cols-1">
                <label>Cantidad<input type="number" min="0.01" step="0.01" value={deliveryForm.cantidadEntregada} onChange={(event) => setDeliveryForm({ ...deliveryForm, cantidadEntregada: event.target.value })} required /></label>
                <label>Bodega<select value={deliveryForm.bodegaCodigo} onChange={(event) => setDeliveryForm({ ...deliveryForm, bodegaCodigo: event.target.value })} required><option value="">Selecciona bodega</option>{warehouses.map((item) => <option key={item.codigo} value={item.codigo}>{item.nombre}</option>)}</select></label>
                <label>OT<input value={deliveryForm.otNumero} onChange={(event) => setDeliveryForm({ ...deliveryForm, otNumero: event.target.value })} /></label>
                <label>Fecha entrega<input type="date" value={deliveryForm.fechaEntrega} onChange={(event) => setDeliveryForm({ ...deliveryForm, fechaEntrega: event.target.value })} /></label>
                <label>Documento entrega<input value={deliveryForm.documentoEntregaUrl} onChange={(event) => setDeliveryForm({ ...deliveryForm, documentoEntregaUrl: event.target.value })} /></label>
                <label>Motivo<input value={deliveryForm.reason} onChange={(event) => setDeliveryForm({ ...deliveryForm, reason: event.target.value })} required /></label>
              </div>
              <button className="secondary-button" type="submit" disabled={isSaving}>Registrar entrega</button>
            </form>
          </section>
        </section>
      ) : null}

      <section className="grid gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <form className="panel space-y-4" onSubmit={saveSupplier}>
          <SectionTitle title={editingSupplierRut ? "Editar proveedor" : "Nuevo proveedor"} />
          <div className="form-grid">
            <label>RUT<input value={supplierForm.rut} onChange={(event) => setSupplierForm({ ...supplierForm, rut: event.target.value })} required /></label>
            <label>Nombre<input value={supplierForm.nombre} onChange={(event) => setSupplierForm({ ...supplierForm, nombre: event.target.value })} required /></label>
            <label>Contacto<input value={supplierForm.contacto} onChange={(event) => setSupplierForm({ ...supplierForm, contacto: event.target.value })} /></label>
            <label>Email<input value={supplierForm.email} onChange={(event) => setSupplierForm({ ...supplierForm, email: event.target.value })} /></label>
            <label>Telefono<input value={supplierForm.telefono} onChange={(event) => setSupplierForm({ ...supplierForm, telefono: event.target.value })} /></label>
            <label>Lead esperado<input type="number" min="0" value={supplierForm.leadTimeEsperadoDias} onChange={(event) => setSupplierForm({ ...supplierForm, leadTimeEsperadoDias: event.target.value })} /></label>
            <label className="span-2">Observaciones<input value={supplierForm.observaciones} onChange={(event) => setSupplierForm({ ...supplierForm, observaciones: event.target.value })} /></label>
            <label className="check-row"><input type="checkbox" checked={supplierForm.activo} onChange={(event) => setSupplierForm({ ...supplierForm, activo: event.target.checked })} />Activo</label>
          </div>
          <button className="primary-button" type="submit" disabled={isSaving}>
            <Save className="h-4 w-4" /> Guardar proveedor
          </button>
        </form>

        <section className="panel space-y-4">
          <SectionTitle title="Proveedores" value={`${suppliers.length} registrados`} />
          <div className="data-table">
            <table>
              <thead>
                <tr><th>Proveedor</th><th>Contacto</th><th>Lead</th><th>Estado</th></tr>
              </thead>
              <tbody>
                {suppliers.map((item) => (
                  <tr key={item.rut} onClick={() => {
                    setEditingSupplierRut(item.rut);
                    setSupplierForm({
                      rut: item.rut,
                      nombre: item.nombre,
                      contacto: item.contacto ?? "",
                      email: item.email ?? "",
                      telefono: item.telefono ?? "",
                      direccion: item.direccion ?? "",
                      leadTimeEsperadoDias: String(item.leadTimeEsperadoDias),
                      activo: item.activo,
                      observaciones: item.observaciones ?? ""
                    });
                  }}>
                    <td><strong>{item.nombre}</strong><small>{item.rut}</small></td>
                    <td>{item.contacto ?? item.email ?? "-"}</td>
                    <td>{item.leadTimeEsperadoDias} dias</td>
                    <td><span className={`status-pill ${item.activo ? "success" : "danger"}`}>{item.activo ? "Activo" : "Inactivo"}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </section>
    </section>
  );
}

function SectionTitle({ title, value }: { title: string; value?: string }) {
  return (
    <div className="section-heading">
      <h2>{title}</h2>
      {value ? <span>{value}</span> : null}
    </div>
  );
}

function Metric({ icon, label, value, suffix = "" }: { icon: JSX.Element; label: string; value: number; suffix?: string }) {
  return (
    <article className="metric-card">
      {icon}
      <span>{label}</span>
      <strong>{value}{suffix}</strong>
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

function average(values: number[]) {
  if (values.length === 0) return 0;
  return Math.round(values.reduce((sum, value) => sum + value, 0) / values.length);
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function toNumberOrNull(value: string) {
  return value.trim() ? Number(value) : null;
}

function toIsoOrNull(value: string) {
  return value ? new Date(`${value}T00:00:00Z`).toISOString() : null;
}

function toIsoDate(value: string) {
  return new Date(`${value}T00:00:00Z`).toISOString();
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleDateString() : "-";
}

function formatDays(value?: number | null) {
  return typeof value === "number" ? `${value} dias` : "-";
}

function formatCurrency(value?: number | null, currency = "CLP") {
  return typeof value === "number" ? `${currency} ${new Intl.NumberFormat("es-CL").format(value)}` : "-";
}
