import { type FormEvent, useEffect, useMemo, useRef, useState, type PointerEvent as ReactPointerEvent, type ReactNode } from "react";
import {
  AlertTriangle,
  CalendarClock,
  CheckCircle2,
  ClipboardCheck,
  Clock,
  FileUp,
  PauseCircle,
  PenLine,
  PlayCircle,
  Plus,
  RefreshCw,
  Save,
  UserPlus,
  Wrench,
  XCircle
} from "lucide-react";
import { AUTH_ROLES, apiFetch, useAuthStore } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";

type WorkOrderStatus =
  | "OTCreada"
  | "EnPlanificacion"
  | "Programada"
  | "PendienteRepuestos"
  | "PendienteDocumentacion"
  | "EnEjecucion"
  | "Pausada"
  | "FinalizadaTecnico"
  | "EnRevisionSupervisor"
  | "CerradaTecnicamente"
  | "ValidadaPlanificacion"
  | "Anulada";

type SparePartStatus = "Solicitado" | "Reservado" | "Entregado" | "Utilizado" | "Devuelto" | "Cancelado";
type EvidenceType = "FotoAntes" | "FotoDespues" | "Archivo" | "Comentario" | "Otro";
type ChecklistResponseType = "CumpleNoCumpleNoAplica" | "BuenoRegularMalo" | "SiNo" | "Numerico" | "Texto" | "FotoObligatoria" | "Archivo" | "Firma";

type WorkOrderSummary = {
  numeroOT: string;
  estado: WorkOrderStatus;
  activoCodigo: string;
  activoNombre?: string | null;
  unidadOperativaCodigo?: string | null;
  unidadOperativaNombre?: string | null;
  activosRelacionados: { activoCodigo: string; activoNombre: string; rol: string; esPrincipal: boolean }[];
  faenaCodigo: string;
  tipoMantenimiento: string;
  descripcion: string;
  avisoId?: string | null;
  sistema?: string | null;
  subsistema?: string | null;
  componente?: string | null;
  prioridad: string;
  criticidad: string;
  fechaProgramada?: string | null;
  fechaInicioProgramada?: string | null;
  fechaFinProgramada?: string | null;
  esPreventivaAutomatica: boolean;
  requiereFirma: boolean;
  tareasTotal: number;
  tecnicosTotal: number;
  horasRegistradas: number;
  bloqueosCierre: number;
};

type WorkOrderTask = {
  numeroOT: string;
  codigoTarea: string;
  descripcion: string;
  fechaInicioProgramada?: string | null;
  fechaFinProgramada?: string | null;
  requiereEvidencia: boolean;
  requiereHH: boolean;
  checklistObligatorio: boolean;
  observaciones?: string | null;
};

type WorkOrderTechnician = {
  asignacionId: string;
  numeroOT: string;
  codigoTarea: string;
  tecnicoUserId: string;
  tecnicoNombre?: string | null;
};

type WorkOrderLabor = {
  hhId: string;
  codigoTarea: string;
  tecnicoUserId: string;
  horas: number;
  descripcion: string;
  fechaTrabajo: string;
  horaInicio?: string | null;
  horaTermino?: string | null;
  comentario?: string | null;
  validadoSupervisor: boolean;
  validadoPor?: string | null;
  validadoEnUtc?: string | null;
};

type WorkOrderEvidence = {
  evidenciaId: string;
  codigoTarea?: string | null;
  nombre: string;
  archivoKey?: string | null;
  sharePointUrl?: string | null;
  tipoEvidencia: EvidenceType;
  esFoto: boolean;
  esObligatoria: boolean;
  storageProvider?: string | null;
  localPath?: string | null;
  offlineId?: string | null;
  syncStatus?: string | null;
};

type WorkOrderSparePart = {
  itemId: string;
  codigoTarea: string;
  repuestoCodigo: string;
  cantidad: number;
  unidad: string;
  bodegaCodigo?: string | null;
  estado: SparePartStatus;
  cantidadUtilizada: number;
  cantidadDevuelta: number;
};

type WorkOrderChecklist = {
  itemId: string;
  codigoTarea: string;
  item: string;
  obligatorio: boolean;
  completado: boolean;
  tipoRespuesta: ChecklistResponseType;
  respuesta?: string | null;
  valorNumerico?: number | null;
  texto?: string | null;
  evidenciaId?: string | null;
  requiereFoto: boolean;
  requiereArchivo: boolean;
  requiereFirma: boolean;
  firmaId?: string | null;
};

type WorkOrderSignature = {
  firmaId: string;
  usuarioId: string;
  signatureFileKey?: string | null;
  codigoTarea?: string | null;
  scope: string;
  signatureImageDataUrl?: string | null;
};

type ClosureBlocker = {
  code: string;
  message: string;
};

type WorkOrderDetail = {
  summary: WorkOrderSummary;
  tasks: WorkOrderTask[];
  technicians: WorkOrderTechnician[];
  labor: WorkOrderLabor[];
  evidences: WorkOrderEvidence[];
  spareParts: WorkOrderSparePart[];
  checklist: WorkOrderChecklist[];
  signatures: WorkOrderSignature[];
  closureBlockers: ClosureBlocker[];
};

type AssetSummary = {
  codigo: string;
  nombre: string;
  faenaCodigo: string;
  ubicacionTecnicaCodigo?: string | null;
};

type OperationalUnitSummary = { codigo: string; nombre: string; faenaCodigo?: string | null };
type SparePartSummary = {
  codigo: string;
  descripcion: string;
  unidadMedida: string;
};

const statusLabels: Record<WorkOrderStatus, string> = {
  OTCreada: "OT creada",
  EnPlanificacion: "En planificacion",
  Programada: "Programada",
  PendienteRepuestos: "Pendiente repuestos",
  PendienteDocumentacion: "Pendiente documentacion",
  EnEjecucion: "En ejecucion",
  Pausada: "Pausada",
  FinalizadaTecnico: "Finalizada tecnico",
  EnRevisionSupervisor: "Revision supervisor",
  CerradaTecnicamente: "Cerrada tecnica",
  ValidadaPlanificacion: "Validada planificacion",
  Anulada: "Anulada"
};

const kanbanColumns: WorkOrderStatus[] = ["OTCreada", "Programada", "EnEjecucion", "FinalizadaTecnico", "CerradaTecnicamente", "ValidadaPlanificacion"];
const closedStatuses: WorkOrderStatus[] = ["CerradaTecnicamente", "ValidadaPlanificacion", "Anulada"];

const emptyOrderForm = {
  activoCodigo: "",
  unidadOperativaCodigo: "",
  activosRelacionados: [] as string[],
  faenaCodigo: "",
  descripcion: "",
  tipoMantenimiento: "Corrective",
  sistema: "",
  subsistema: "",
  componente: "",
  prioridad: "Media",
  criticidad: "Media",
  fechaProgramada: "",
  requiereFirma: false,
  preventive: false
};

const emptyTaskForm = {
  descripcion: "",
  requiereEvidencia: false,
  requiereHH: true,
  checklistObligatorio: false
};

export function WorkOrdersPage() {
  const currentUser = useAuthStore((state) => state.user);
  const [orders, setOrders] = useState<WorkOrderSummary[]>([]);
  const [assets, setAssets] = useState<AssetSummary[]>([]);
  const [operationalUnits, setOperationalUnits] = useState<OperationalUnitSummary[]>([]);
  const [spareParts, setSpareParts] = useState<SparePartSummary[]>([]);
  const [selectedId, setSelectedId] = useState("");
  const [detail, setDetail] = useState<WorkOrderDetail | null>(null);
  const [filters, setFilters] = useState({ status: "", faenaCodigo: "", technicianId: "", activoCodigo: "", unidadOperativaCodigo: "", includeClosed: false });
  const [orderForm, setOrderForm] = useState(emptyOrderForm);
  const [taskForm, setTaskForm] = useState(emptyTaskForm);
  const [technicianForm, setTechnicianForm] = useState({ codigoTarea: "", tecnicoUserId: currentUser?.id ?? "", tecnicoNombre: "" });
  const [laborForm, setLaborForm] = useState({ codigoTarea: "", tecnicoUserId: currentUser?.id ?? "", fechaTrabajo: "", horaInicio: "", horaTermino: "", horas: "1", descripcion: "", comentario: "" });
  const [evidenceForm, setEvidenceForm] = useState({ codigoTarea: "", nombre: "", tipoEvidencia: "FotoDespues" as EvidenceType, archivoKey: "", sharePointUrl: "", localPath: "", storageProvider: "", offlineId: "", esObligatoria: false });
  const [spareForm, setSpareForm] = useState({ codigoTarea: "", repuestoCodigo: "", cantidad: "1", unidad: "UN", estado: "Solicitado" as SparePartStatus });
  const [checklistForm, setChecklistForm] = useState({ codigoTarea: "", item: "", tipoRespuesta: "CumpleNoCumpleNoAplica" as ChecklistResponseType, requiereFoto: false, requiereArchivo: false, requiereFirma: false });
  const [templateForm, setTemplateForm] = useState({ codigoTarea: "", templateCode: "" });
  const [signatureForm, setSignatureForm] = useState({ codigoTarea: "", scope: "OT", signatureFileKey: "", signatureImageDataUrl: "", comentario: "" });
  const [scheduleForm, setScheduleForm] = useState({ fechaInicioProgramada: "", fechaFinProgramada: "", reason: "" });
  const [actionReason, setActionReason] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const roles = currentUser?.roles ?? [];
  const canPlan = [AUTH_ROLES.admin, AUTH_ROLES.planner, AUTH_ROLES.maintenanceSupervisor].some((role) => roles.includes(role));
  const canClose = [AUTH_ROLES.admin, AUTH_ROLES.maintenanceSupervisor].some((role) => roles.includes(role));
  const canValidate = [AUTH_ROLES.admin, AUTH_ROLES.planner].some((role) => roles.includes(role));

  useEffect(() => {
    void loadAll();
  }, [filters.status, filters.faenaCodigo, filters.technicianId, filters.activoCodigo, filters.unidadOperativaCodigo, filters.includeClosed]);

  useEffect(() => {
    if (selectedId) {
      void loadDetail(selectedId);
    }
  }, [selectedId]);

  const selected = detail?.summary ?? orders.find((item) => item.numeroOT === selectedId) ?? orders[0] ?? null;
  const byStatus = useMemo(() => {
    return kanbanColumns.map((status) => ({
      status,
      items: orders.filter((item) => item.estado === status)
    }));
  }, [orders]);

  async function loadAll() {
    setIsLoading(true);
    setError(null);
    try {
      const query = new URLSearchParams();
      if (filters.status) query.set("status", filters.status);
      if (filters.faenaCodigo) query.set("faenaCodigo", filters.faenaCodigo);
      if (filters.technicianId) query.set("technicianId", filters.technicianId);
      if (filters.activoCodigo) query.set("activoCodigo", filters.activoCodigo);
      if (filters.unidadOperativaCodigo) query.set("unidadOperativaCodigo", filters.unidadOperativaCodigo);
      query.set("includeClosed", String(filters.includeClosed));

      const [orderResult, assetResult, spareResult, unitResult] = await Promise.all([
        apiFetch<WorkOrderSummary[]>(`/api/work-orders?${query}`),
        apiFetch<AssetSummary[]>("/api/assets").catch(() => [] as AssetSummary[]),
        apiFetch<SparePartSummary[]>("/api/inventory/spare-parts?includeObsolete=true").catch(() => [] as SparePartSummary[]),
        apiFetch<OperationalUnitSummary[]>("/api/operational-units").catch(() => [] as OperationalUnitSummary[])
      ]);
      setOrders(orderResult);
      setAssets(assetResult);
      setSpareParts(spareResult);
      setOperationalUnits(unitResult);
      if (!selectedId && orderResult[0]) {
        setSelectedId(orderResult[0].numeroOT);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar las OT.");
    } finally {
      setIsLoading(false);
    }
  }

  async function loadDetail(id: string) {
    try {
      const data = await apiFetch<WorkOrderDetail>(`/api/work-orders/${encodeURIComponent(id)}`);
      setDetail(data);
      const firstTask = data.tasks[0]?.codigoTarea ?? "";
      setTechnicianForm((current) => ({ ...current, codigoTarea: current.codigoTarea || firstTask }));
      setLaborForm((current) => ({ ...current, codigoTarea: current.codigoTarea || firstTask }));
      setEvidenceForm((current) => ({ ...current, codigoTarea: current.codigoTarea || firstTask }));
      setSpareForm((current) => ({ ...current, codigoTarea: current.codigoTarea || firstTask }));
      setChecklistForm((current) => ({ ...current, codigoTarea: current.codigoTarea || firstTask }));
      setTemplateForm((current) => ({ ...current, codigoTarea: current.codigoTarea || firstTask }));
      setSignatureForm((current) => ({ ...current, codigoTarea: current.codigoTarea || firstTask }));
    } catch (detailError) {
      setError(detailError instanceof Error ? detailError.message : "No fue posible cargar la ficha OT.");
    }
  }

  async function createOrder(event: FormEvent) {
    event.preventDefault();
    await saveAction(async () => {
      const body = {
        activoCodigo: emptyToNull(orderForm.activoCodigo),
        unidadOperativaCodigo: emptyToNull(orderForm.unidadOperativaCodigo),
        activosRelacionados: orderForm.activosRelacionados.filter((code) => code !== orderForm.activoCodigo).map((activoCodigo) => ({ activoCodigo, rol: "AFECTADO" })),
        faenaCodigo: emptyToNull(orderForm.faenaCodigo),
        descripcion: orderForm.descripcion,
        tipoMantenimiento: orderForm.tipoMantenimiento,
        sistema: emptyToNull(orderForm.sistema),
        subsistema: emptyToNull(orderForm.subsistema),
        componente: emptyToNull(orderForm.componente),
        prioridad: orderForm.prioridad,
        criticidad: orderForm.criticidad,
        fechaProgramada: toIsoOrNull(orderForm.fechaProgramada),
        requiereFirma: orderForm.requiereFirma
      };
      if (orderForm.preventive && !orderForm.activoCodigo) throw new Error("Una OT preventiva requiere un activo f�sico.");
      const created = orderForm.preventive
        ? await apiFetch<WorkOrderDetail>("/api/work-orders/preventive", { method: "POST", body: JSON.stringify(body) })
        : await apiFetch<WorkOrderDetail>("/api/work-orders", { method: "POST", body: JSON.stringify(body) });
      setOrderForm(emptyOrderForm);
      setSelectedId(created.summary.numeroOT);
      setMessage(`OT ${created.summary.numeroOT} creada.`);
    });
  }

  async function addTask(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/tasks`, {
        method: "POST",
        body: JSON.stringify(taskForm)
      });
      setTaskForm(emptyTaskForm);
      setMessage("Tarea agregada.");
    });
  }

  async function assignTechnician(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/tasks/${encodeURIComponent(technicianForm.codigoTarea)}/technicians`, {
        method: "POST",
        body: JSON.stringify({ tecnicoUserId: technicianForm.tecnicoUserId, tecnicoNombre: emptyToNull(technicianForm.tecnicoNombre) })
      });
      setMessage("Tecnico asignado.");
    });
  }

  async function registerLabor(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/tasks/${encodeURIComponent(laborForm.codigoTarea)}/labor`, {
        method: "POST",
        body: JSON.stringify({
          tecnicoUserId: laborForm.tecnicoUserId,
          horas: laborForm.horaInicio && laborForm.horaTermino ? null : Number(laborForm.horas),
          descripcion: laborForm.descripcion,
          fechaTrabajo: toIsoOrNull(laborForm.fechaTrabajo),
          horaInicio: toIsoDateTimeOrNull(laborForm.fechaTrabajo, laborForm.horaInicio),
          horaTermino: toIsoDateTimeOrNull(laborForm.fechaTrabajo, laborForm.horaTermino),
          comentario: emptyToNull(laborForm.comentario)
        })
      });
      setLaborForm({ ...laborForm, horas: "1", descripcion: "", comentario: "" });
      setMessage("HH registradas.");
    });
  }

  async function validateLabor(item: WorkOrderLabor, validado: boolean) {
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/labor/${encodeURIComponent(item.hhId)}/validate`, {
        method: "POST",
        body: JSON.stringify({ validado, reason: validado ? "HH validada por supervisor" : "HH observada por supervisor" })
      });
      setMessage(validado ? "HH validada." : "Validacion HH retirada.");
    });
  }

  async function registerEvidence(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/evidences`, {
        method: "POST",
        body: JSON.stringify({
          codigoTarea: emptyToNull(evidenceForm.codigoTarea),
          nombre: evidenceForm.nombre,
          archivoKey: emptyToNull(evidenceForm.archivoKey),
          sharePointUrl: emptyToNull(evidenceForm.sharePointUrl),
          localPath: emptyToNull(evidenceForm.localPath),
          storageProvider: emptyToNull(evidenceForm.storageProvider),
          offlineId: emptyToNull(evidenceForm.offlineId),
          tipoEvidencia: evidenceForm.tipoEvidencia,
          esFoto: evidenceForm.tipoEvidencia === "FotoAntes" || evidenceForm.tipoEvidencia === "FotoDespues",
          esObligatoria: evidenceForm.esObligatoria,
          cubreEvidenciaObligatoria: evidenceForm.esObligatoria || evidenceForm.tipoEvidencia === "FotoDespues"
        })
      });
      setEvidenceForm({ ...evidenceForm, nombre: "", archivoKey: "", sharePointUrl: "", localPath: "", offlineId: "" });
      setMessage("Evidencia registrada.");
    });
  }

  async function addSparePart(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/spare-parts`, {
        method: "POST",
        body: JSON.stringify({ ...spareForm, cantidad: Number(spareForm.cantidad) })
      });
      setMessage("Repuesto asociado.");
    });
  }

  async function updateSparePart(item: WorkOrderSparePart, estado: SparePartStatus) {
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/spare-parts/${encodeURIComponent(item.itemId)}`, {
        method: "PUT",
        body: JSON.stringify({
          estado,
          reason: `Cambio a ${estado}`,
          cantidadUtilizada: estado === "Utilizado" ? item.cantidad : item.cantidadUtilizada,
          cantidadDevuelta: estado === "Devuelto" ? item.cantidad : item.cantidadDevuelta
        })
      });
      setMessage("Repuesto actualizado.");
    });
  }

  async function addChecklist(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/checklist`, {
        method: "POST",
        body: JSON.stringify({
          codigoTarea: checklistForm.codigoTarea,
          item: checklistForm.item,
          obligatorio: true,
          tipoRespuesta: checklistForm.tipoRespuesta,
          requiereFoto: checklistForm.requiereFoto,
          requiereArchivo: checklistForm.requiereArchivo,
          requiereFirma: checklistForm.requiereFirma
        })
      });
      setChecklistForm({ ...checklistForm, item: "" });
      setMessage("Checklist agregado.");
    });
  }

  async function applyChecklistTemplate() {
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/checklist/apply-template`, {
        method: "POST",
        body: JSON.stringify(templateForm)
      });
      setTemplateForm({ ...templateForm, templateCode: "" });
      setMessage("Plantilla aplicada.");
    });
  }

  async function toggleChecklist(item: WorkOrderChecklist) {
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/checklist/${encodeURIComponent(item.itemId)}`, {
        method: "PUT",
        body: JSON.stringify({
          completado: !item.completado,
          reason: "Actualizacion checklist",
          respuesta: item.respuesta ?? defaultChecklistResponse(item.tipoRespuesta),
          valorNumerico: item.valorNumerico,
          texto: item.texto,
          evidenciaId: item.evidenciaId,
          firmaId: item.firmaId
        })
      });
      setMessage("Checklist actualizado.");
    });
  }

  async function registerSignature() {
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/signatures`, {
        method: "POST",
        body: JSON.stringify({
          signatureFileKey: emptyToNull(signatureForm.signatureFileKey),
          signatureImageDataUrl: emptyToNull(signatureForm.signatureImageDataUrl),
          usuarioId: currentUser?.id,
          codigoTarea: emptyToNull(signatureForm.codigoTarea),
          scope: signatureForm.scope,
          comentario: emptyToNull(signatureForm.comentario)
        })
      });
      setSignatureForm({ ...signatureForm, signatureFileKey: "", signatureImageDataUrl: "", comentario: "" });
      setMessage("Firma registrada.");
    });
  }

  async function scheduleOrder(event: FormEvent) {
    event.preventDefault();
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}/schedule`, {
        method: "POST",
        body: JSON.stringify({
          fechaInicioProgramada: toIsoDate(scheduleForm.fechaInicioProgramada),
          fechaFinProgramada: toIsoOrNull(scheduleForm.fechaFinProgramada),
          reason: scheduleForm.reason
        })
      });
      setScheduleForm({ fechaInicioProgramada: "", fechaFinProgramada: "", reason: "" });
      setMessage("OT programada.");
    });
  }

  async function runAction(path: string, success: string) {
    if (!selected) return;
    await saveAction(async () => {
      await apiFetch(`/api/work-orders/${encodeURIComponent(selected.numeroOT)}${path}`, {
        method: "POST",
        body: JSON.stringify({ reason: actionReason || success })
      });
      setActionReason("");
      setMessage(success);
    });
  }

  async function saveAction(action: () => Promise<void>) {
    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      await action();
      await loadAll();
      if (selectedId) {
        await loadDetail(selectedId);
      }
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible completar la accion.");
    } finally {
      setIsSaving(false);
    }
  }

  function applyAsset(code: string) {
    const asset = assets.find((item) => item.codigo === code);
    setOrderForm({ ...orderForm, activoCodigo: code, faenaCodigo: asset?.faenaCodigo ?? orderForm.faenaCodigo });
  }

  function applyUnit(code: string) {
    const unit = operationalUnits.find((item) => item.codigo === code);
    setOrderForm({ ...orderForm, unidadOperativaCodigo: code, faenaCodigo: unit?.faenaCodigo ?? orderForm.faenaCodigo });
  }

  function toggleRelatedAsset(code: string) {
    setOrderForm({ ...orderForm, activosRelacionados: orderForm.activosRelacionados.includes(code) ? orderForm.activosRelacionados.filter((item) => item !== code) : [...orderForm.activosRelacionados, code] });
  }

  function targetLabel(item: WorkOrderSummary) {
    return (item.activoNombre ?? item.activoCodigo) || item.unidadOperativaNombre || item.unidadOperativaCodigo || "Sin destino";
  }
  return (
    <section className="stack">
      <header className="page-header">
        <div>
          <p className="eyebrow">Mantenimiento operativo</p>
          <h1>Ordenes de trabajo</h1>
          <p>OT, tareas internas, tecnicos, HH, evidencias, repuestos, checklist y firma.</p>
        </div>
        <button className="secondary-button" type="button" onClick={() => void loadAll()}>
          <RefreshCw size={18} /> Actualizar
        </button>
      </header>

      <section className="kpi-grid xl:grid-cols-4">
        <Metric icon={<ClipboardCheck size={18} />} label="OT abiertas" value={orders.filter((item) => !closedStatuses.includes(item.estado)).length} />
        <Metric icon={<PlayCircle size={18} />} label="En ejecucion" value={orders.filter((item) => item.estado === "EnEjecucion").length} />
        <Metric icon={<AlertTriangle size={18} />} label="Con bloqueos" value={orders.filter((item) => item.bloqueosCierre > 0).length} />
        <Metric icon={<Clock size={18} />} label="HH registradas" value={Math.round(orders.reduce((sum, item) => sum + item.horasRegistradas, 0))} />
      </section>

      {message ? <div className="success-banner">{message}</div> : null}
      {error ? <div className="error-banner">{error}</div> : null}

      <div className="two-column-layout">
        <form className="panel stack" onSubmit={createOrder}>
          <div className="section-heading">
            <h2>Nueva OT</h2>
          </div>
          <div className="form-grid">
            <label>
              Activo principal (opcional si se selecciona unidad)
              <select value={orderForm.activoCodigo} onChange={(event) => applyAsset(event.target.value)}>
                <option value="">Sin activo principal</option>
                {assets.map((item) => (
                  <option key={item.codigo} value={item.codigo}>{item.nombre} ({item.codigo})</option>
                ))}
              </select>
            </label>
            <label>
              Unidad operativa (opcional si se selecciona activo)
              <select value={orderForm.unidadOperativaCodigo} onChange={(event) => applyUnit(event.target.value)}>
                <option value="">Sin unidad operativa</option>
                {operationalUnits.map((item) => <option key={item.codigo} value={item.codigo}>{item.nombre} ({item.codigo})</option>)}
              </select>
            </label>
            <FaenaSelect emptyLabel="Selecciona faena" value={orderForm.faenaCodigo} onChange={(value) => setOrderForm({ ...orderForm, faenaCodigo: value })} />
            <fieldset className="span-2">
              <legend>Activos relacionados (snapshot de la OT)</legend>
              <div className="grid gap-1 md:grid-cols-2">
                {assets.filter((item) => !orderForm.faenaCodigo || item.faenaCodigo === orderForm.faenaCodigo).map((item) => (
                  <label className="check-row" key={item.codigo}><input type="checkbox" checked={orderForm.activosRelacionados.includes(item.codigo)} onChange={() => toggleRelatedAsset(item.codigo)} />{item.nombre} ({item.codigo})</label>
                ))}
              </div>
            </fieldset>            <label>
              Tipo
              <select value={orderForm.tipoMantenimiento} onChange={(event) => setOrderForm({ ...orderForm, tipoMantenimiento: event.target.value })}>
                <option value="Corrective">Correctiva</option>
                <option value="Preventive">Preventiva</option>
                <option value="Inspection">Inspeccion</option>
                <option value="Predictive">Predictiva</option>
              </select>
            </label>
            <label>
              Fecha programada
              <input type="date" value={orderForm.fechaProgramada} onChange={(event) => setOrderForm({ ...orderForm, fechaProgramada: event.target.value })} />
            </label>
            <label>
              Sistema
              <input value={orderForm.sistema} onChange={(event) => setOrderForm({ ...orderForm, sistema: event.target.value })} />
            </label>
            <label>
              Subsistema
              <input value={orderForm.subsistema} onChange={(event) => setOrderForm({ ...orderForm, subsistema: event.target.value })} />
            </label>
            <label>
              Componente
              <input value={orderForm.componente} onChange={(event) => setOrderForm({ ...orderForm, componente: event.target.value })} />
            </label>
            <label>
              Prioridad
              <select value={orderForm.prioridad} onChange={(event) => setOrderForm({ ...orderForm, prioridad: event.target.value })}>
                <option>Baja</option>
                <option>Media</option>
                <option>Alta</option>
                <option>Critica</option>
              </select>
            </label>
            <label className="span-2">
              Descripcion
              <textarea value={orderForm.descripcion} onChange={(event) => setOrderForm({ ...orderForm, descripcion: event.target.value })} required />
            </label>
            <label className="check-row">
              <input type="checkbox" checked={orderForm.preventive} onChange={(event) => setOrderForm({ ...orderForm, preventive: event.target.checked })} />
              Preventiva automatica
            </label>
            <label className="check-row">
              <input type="checkbox" checked={orderForm.requiereFirma} onChange={(event) => setOrderForm({ ...orderForm, requiereFirma: event.target.checked })} />
              Requiere firma
            </label>
          </div>
          <button className="primary-button" type="submit" disabled={isSaving || !canPlan}>
            <Save size={18} /> Crear OT
          </button>
        </form>

        <section className="panel stack">
          <div className="section-heading">
            <h2>Listado</h2>
            <span>{orders.length} OT</span>
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
            <select value={filters.activoCodigo} onChange={(event) => setFilters({ ...filters, activoCodigo: event.target.value })}>
              <option value="">Todos los activos</option>
              {assets.map((item) => <option key={item.codigo} value={item.codigo}>{item.nombre}</option>)}
            </select>
            <select value={filters.unidadOperativaCodigo} onChange={(event) => setFilters({ ...filters, unidadOperativaCodigo: event.target.value })}>
              <option value="">Todas las unidades</option>
              {operationalUnits.map((item) => <option key={item.codigo} value={item.codigo}>{item.nombre}</option>)}
            </select>            <input placeholder="Tecnico" value={filters.technicianId} onChange={(event) => setFilters({ ...filters, technicianId: event.target.value })} />
            <label className="check-row">
              <input type="checkbox" checked={filters.includeClosed} onChange={(event) => setFilters({ ...filters, includeClosed: event.target.checked })} />
              Cerradas
            </label>
          </div>
          <FaenaSelect value={filters.faenaCodigo} onChange={(value) => setFilters({ ...filters, faenaCodigo: value })} />
          {isLoading ? <p className="text-sm text-slate-500 dark:text-slate-400">Cargando OT...</p> : null}
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>OT</th>
                  <th>Destino</th>
                  <th>Estado</th>
                  <th>Plan</th>
                  <th>HH</th>
                </tr>
              </thead>
              <tbody>
                {orders.map((item) => (
                  <tr key={item.numeroOT} className={selected?.numeroOT === item.numeroOT ? "selected-row" : ""} onClick={() => setSelectedId(item.numeroOT)}>
                    <td>
                      <strong>{item.numeroOT}</strong>
                      <small>{item.descripcion}</small>
                    </td>
                    <td>
                      <strong>{targetLabel(item)}</strong>
                      <small>{[item.faenaCodigo, item.sistema, item.subsistema, item.componente].filter(Boolean).join(" / ") || "-"}</small>
                    </td>
                    <td>
                      <span className={`status-pill ${item.estado === "Anulada" ? "danger" : closedStatuses.includes(item.estado) ? "success" : ""}`}>
                        {statusLabels[item.estado]}
                      </span>
                      {item.bloqueosCierre > 0 ? <small>{item.bloqueosCierre} bloqueos</small> : null}
                    </td>
                    <td>{formatDate(item.fechaProgramada)}</td>
                    <td>{item.horasRegistradas}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <section className="panel stack">
        <div className="section-heading">
          <h2>Kanban</h2>
          <span>Estados operativos</span>
        </div>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-6">
          {byStatus.map((column) => (
            <div key={column.status} className="rounded-md border border-slate-200 bg-slate-50 p-3 dark:border-slate-800 dark:bg-slate-950">
              <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">{statusLabels[column.status]}</h3>
              <div className="mt-3 space-y-2">
                {column.items.map((item) => (
                  <button key={item.numeroOT} className="w-full rounded-md border border-slate-200 bg-white p-3 text-left text-sm dark:border-slate-800 dark:bg-slate-900" type="button" onClick={() => setSelectedId(item.numeroOT)}>
                    <strong className="block text-slate-900 dark:text-slate-100">{item.numeroOT}</strong>
                    <span className="mt-1 block text-xs text-slate-500 dark:text-slate-400">{targetLabel(item)}</span>
                  </button>
                ))}
              </div>
            </div>
          ))}
        </div>
      </section>

      {detail ? (
        <section className="panel stack">
          <div className="section-heading">
            <div>
              <h2>{detail.summary.numeroOT} - {detail.summary.descripcion}</h2>
              <p>{targetLabel(detail.summary)} / {detail.summary.faenaCodigo}</p>
            </div>
            <span className={`status-pill ${closedStatuses.includes(detail.summary.estado) ? "success" : ""}`}>{statusLabels[detail.summary.estado]}</span>
          </div>

          {detail.closureBlockers.length > 0 ? (
            <div className="error-banner">
              {detail.closureBlockers.map((item) => item.message).join(" | ")}
            </div>
          ) : null}

          <div className="detail-grid">
            <Info label="Tipo" value={detail.summary.tipoMantenimiento} />
            <Info label="Prioridad" value={detail.summary.prioridad} />
            <Info label="Criticidad" value={detail.summary.criticidad} />
            <Info label="Programada" value={formatDate(detail.summary.fechaProgramada)} />
            <Info label="Tareas" value={String(detail.tasks.length)} />
            <Info label="Tecnicos" value={String(detail.technicians.length)} />
            <Info label="HH" value={String(detail.summary.horasRegistradas)} />
            <Info label="Firma requerida" value={detail.summary.requiereFirma ? "Si" : "No"} />
          </div>

          <div className="form-grid">
            <label>
              Motivo accion
              <input value={actionReason} onChange={(event) => setActionReason(event.target.value)} placeholder="Motivo auditado" />
            </label>
          </div>
          <div className="toolbar">
            <button className="secondary-button" type="button" disabled={isSaving || !canPlan} onClick={() => void runAction("/start", "OT iniciada.")}><PlayCircle size={18} /> Iniciar</button>
            <button className="secondary-button" type="button" disabled={isSaving} onClick={() => void runAction("/pause", "OT pausada.")}><PauseCircle size={18} /> Pausar</button>
            <button className="secondary-button" type="button" disabled={isSaving} onClick={() => void runAction("/finish-technician", "OT finalizada por tecnico.")}><CheckCircle2 size={18} /> Finalizar tecnico</button>
            <button className="secondary-button" type="button" disabled={isSaving || !canClose} onClick={() => void runAction("/close-technical", "OT cerrada tecnicamente.")}><ClipboardCheck size={18} /> Cierre supervisor</button>
            <button className="secondary-button" type="button" disabled={isSaving || !canValidate} onClick={() => void runAction("/validate-planning", "OT validada por planificacion.")}><CalendarClock size={18} /> Validar</button>
            <button className="danger-button" type="button" disabled={isSaving || !canPlan} onClick={() => void runAction("/annul", "OT anulada.")}><XCircle size={18} /> Anular</button>
          </div>

          <form className="panel-muted stack" onSubmit={scheduleOrder}>
            <h3>Programacion</h3>
            <div className="form-grid">
              <label>Inicio<input type="date" value={scheduleForm.fechaInicioProgramada} onChange={(event) => setScheduleForm({ ...scheduleForm, fechaInicioProgramada: event.target.value })} required /></label>
              <label>Fin<input type="date" value={scheduleForm.fechaFinProgramada} onChange={(event) => setScheduleForm({ ...scheduleForm, fechaFinProgramada: event.target.value })} /></label>
              <label className="span-2">Motivo<input value={scheduleForm.reason} onChange={(event) => setScheduleForm({ ...scheduleForm, reason: event.target.value })} required /></label>
            </div>
            <button className="secondary-button" type="submit" disabled={isSaving || !canPlan}><CalendarClock size={18} /> Programar</button>
          </form>

          <section className="grid gap-4 xl:grid-cols-2">
            <form className="panel-muted stack" onSubmit={addTask}>
              <h3>Tareas internas</h3>
              <div className="form-grid">
                <label className="span-2">Descripcion<input value={taskForm.descripcion} onChange={(event) => setTaskForm({ ...taskForm, descripcion: event.target.value })} required /></label>
                <label className="check-row"><input type="checkbox" checked={taskForm.requiereEvidencia} onChange={(event) => setTaskForm({ ...taskForm, requiereEvidencia: event.target.checked })} />Evidencia</label>
                <label className="check-row"><input type="checkbox" checked={taskForm.requiereHH} onChange={(event) => setTaskForm({ ...taskForm, requiereHH: event.target.checked })} />HH</label>
                <label className="check-row"><input type="checkbox" checked={taskForm.checklistObligatorio} onChange={(event) => setTaskForm({ ...taskForm, checklistObligatorio: event.target.checked })} />Checklist</label>
              </div>
              <button className="secondary-button" type="submit" disabled={isSaving || !canPlan}><Plus size={18} /> Agregar tarea</button>
              <MiniTable rows={detail.tasks.map((item) => [item.codigoTarea, item.descripcion, [item.requiereEvidencia ? "Evid" : "", item.requiereHH ? "HH" : "", item.checklistObligatorio ? "Chk" : ""].filter(Boolean).join(" / ")])} />
            </form>

            <form className="panel-muted stack" onSubmit={assignTechnician}>
              <h3>Tecnicos asignados</h3>
              <TaskSelect tasks={detail.tasks} value={technicianForm.codigoTarea} onChange={(value) => setTechnicianForm({ ...technicianForm, codigoTarea: value })} />
              <div className="form-grid">
                <label>Tecnico ID<input value={technicianForm.tecnicoUserId} onChange={(event) => setTechnicianForm({ ...technicianForm, tecnicoUserId: event.target.value })} required /></label>
                <label>Nombre<input value={technicianForm.tecnicoNombre} onChange={(event) => setTechnicianForm({ ...technicianForm, tecnicoNombre: event.target.value })} /></label>
              </div>
              <button className="secondary-button" type="submit" disabled={isSaving || !canPlan}><UserPlus size={18} /> Asignar</button>
              <MiniTable rows={detail.technicians.map((item) => [item.codigoTarea, item.tecnicoUserId, item.tecnicoNombre ?? "-"])} />
            </form>

            <form className="panel-muted stack" onSubmit={registerLabor}>
              <h3>HH</h3>
              <TaskSelect tasks={detail.tasks} value={laborForm.codigoTarea} onChange={(value) => setLaborForm({ ...laborForm, codigoTarea: value })} />
              <div className="form-grid">
                <label>Tecnico<input value={laborForm.tecnicoUserId} onChange={(event) => setLaborForm({ ...laborForm, tecnicoUserId: event.target.value })} required /></label>
                <label>Fecha<input type="date" value={laborForm.fechaTrabajo} onChange={(event) => setLaborForm({ ...laborForm, fechaTrabajo: event.target.value })} /></label>
                <label>Inicio<input type="time" value={laborForm.horaInicio} onChange={(event) => setLaborForm({ ...laborForm, horaInicio: event.target.value })} /></label>
                <label>Termino<input type="time" value={laborForm.horaTermino} onChange={(event) => setLaborForm({ ...laborForm, horaTermino: event.target.value })} /></label>
                <label>HH manual<input type="number" min="0.1" step="0.1" value={laborForm.horas} onChange={(event) => setLaborForm({ ...laborForm, horas: event.target.value })} required={!laborForm.horaInicio || !laborForm.horaTermino} /></label>
                <label className="span-2">Trabajo<input value={laborForm.descripcion} onChange={(event) => setLaborForm({ ...laborForm, descripcion: event.target.value })} required /></label>
                <label className="span-2">Comentario<input value={laborForm.comentario} onChange={(event) => setLaborForm({ ...laborForm, comentario: event.target.value })} /></label>
              </div>
              <button className="secondary-button" type="submit" disabled={isSaving}><Clock size={18} /> Registrar HH</button>
              <MiniTable rows={detail.labor.map((item) => [
                item.codigoTarea,
                item.tecnicoUserId,
                `${item.horas} h`,
                item.validadoSupervisor ? "Validada" : <button key={item.hhId} type="button" className="text-teal-700" onClick={() => void validateLabor(item, true)}>validar</button>
              ])} />
            </form>

            <form className="panel-muted stack" onSubmit={registerEvidence}>
              <h3>Evidencias</h3>
              <TaskSelect tasks={detail.tasks} value={evidenceForm.codigoTarea} onChange={(value) => setEvidenceForm({ ...evidenceForm, codigoTarea: value })} allowEmpty />
              <div className="form-grid">
                <label>Nombre<input value={evidenceForm.nombre} onChange={(event) => setEvidenceForm({ ...evidenceForm, nombre: event.target.value })} required /></label>
                <label>Tipo<select value={evidenceForm.tipoEvidencia} onChange={(event) => setEvidenceForm({ ...evidenceForm, tipoEvidencia: event.target.value as EvidenceType })}><option value="FotoAntes">Foto antes</option><option value="FotoDespues">Foto despues</option><option value="Archivo">Archivo</option><option value="Comentario">Comentario</option><option value="Otro">Otro</option></select></label>
                <label>ArchivoKey<input value={evidenceForm.archivoKey} onChange={(event) => setEvidenceForm({ ...evidenceForm, archivoKey: event.target.value })} /></label>
                <label>SharePoint URL<input value={evidenceForm.sharePointUrl} onChange={(event) => setEvidenceForm({ ...evidenceForm, sharePointUrl: event.target.value })} /></label>
                <label>Ruta local<input value={evidenceForm.localPath} onChange={(event) => setEvidenceForm({ ...evidenceForm, localPath: event.target.value })} /></label>
                <label>Offline ID<input value={evidenceForm.offlineId} onChange={(event) => setEvidenceForm({ ...evidenceForm, offlineId: event.target.value })} /></label>
                <label className="check-row"><input type="checkbox" checked={evidenceForm.esObligatoria} onChange={(event) => setEvidenceForm({ ...evidenceForm, esObligatoria: event.target.checked })} />Obligatoria</label>
              </div>
              <button className="secondary-button" type="submit" disabled={isSaving}><FileUp size={18} /> Registrar evidencia</button>
              <MiniTable rows={detail.evidences.map((item) => [item.codigoTarea ?? "OT", item.tipoEvidencia, item.nombre, item.sharePointUrl ?? item.localPath ?? item.archivoKey ?? "-"])} />
            </form>

            <form className="panel-muted stack" onSubmit={addSparePart}>
              <h3>Repuestos por tarea</h3>
              <TaskSelect tasks={detail.tasks} value={spareForm.codigoTarea} onChange={(value) => setSpareForm({ ...spareForm, codigoTarea: value })} />
              <div className="form-grid">
                <label>Repuesto<select value={spareForm.repuestoCodigo} onChange={(event) => {
                  const selectedSpare = spareParts.find((item) => item.codigo === event.target.value);
                  setSpareForm({ ...spareForm, repuestoCodigo: event.target.value, unidad: selectedSpare?.unidadMedida ?? spareForm.unidad });
                }} required><option value="">Selecciona repuesto</option>{spareParts.map((item) => <option key={item.codigo} value={item.codigo}>{item.codigo} - {item.descripcion}</option>)}</select></label>
                <label>Cantidad<input type="number" min="0.01" step="0.01" value={spareForm.cantidad} onChange={(event) => setSpareForm({ ...spareForm, cantidad: event.target.value })} required /></label>
                <label>Unidad<input value={spareForm.unidad} onChange={(event) => setSpareForm({ ...spareForm, unidad: event.target.value })} required /></label>
                <label>Estado<select value={spareForm.estado} onChange={(event) => setSpareForm({ ...spareForm, estado: event.target.value as SparePartStatus })}><option>Solicitado</option><option>Reservado</option><option>Entregado</option></select></label>
              </div>
              <button className="secondary-button" type="submit" disabled={isSaving || !canPlan}><Wrench size={18} /> Asociar repuesto</button>
              <MiniTable
                rows={detail.spareParts.map((item) => [
                  item.codigoTarea,
                  item.repuestoCodigo,
                  <span key={item.itemId} className="inline-flex gap-2">
                    {item.estado}
                    {item.estado === "Entregado" ? <button type="button" className="text-teal-700" onClick={() => void updateSparePart(item, "Utilizado")}>usar</button> : null}
                    {item.estado === "Entregado" ? <button type="button" className="text-teal-700" onClick={() => void updateSparePart(item, "Devuelto")}>devolver</button> : null}
                  </span>
                ])}
              />
            </form>

            <form className="panel-muted stack" onSubmit={addChecklist}>
              <h3>Checklist y firma</h3>
              <TaskSelect tasks={detail.tasks} value={checklistForm.codigoTarea} onChange={(value) => setChecklistForm({ ...checklistForm, codigoTarea: value })} />
              <div className="form-grid">
                <label className="span-2">Item<input value={checklistForm.item} onChange={(event) => setChecklistForm({ ...checklistForm, item: event.target.value })} required /></label>
                <label>Respuesta<select value={checklistForm.tipoRespuesta} onChange={(event) => setChecklistForm({ ...checklistForm, tipoRespuesta: event.target.value as ChecklistResponseType })}><option value="CumpleNoCumpleNoAplica">Cumple / No cumple / NA</option><option value="BuenoRegularMalo">Bueno / Regular / Malo</option><option value="SiNo">Si / No</option><option value="Numerico">Numerico</option><option value="Texto">Texto</option><option value="FotoObligatoria">Foto obligatoria</option><option value="Archivo">Archivo</option><option value="Firma">Firma</option></select></label>
                <label className="check-row"><input type="checkbox" checked={checklistForm.requiereFoto} onChange={(event) => setChecklistForm({ ...checklistForm, requiereFoto: event.target.checked })} />Foto</label>
                <label className="check-row"><input type="checkbox" checked={checklistForm.requiereArchivo} onChange={(event) => setChecklistForm({ ...checklistForm, requiereArchivo: event.target.checked })} />Archivo</label>
                <label className="check-row"><input type="checkbox" checked={checklistForm.requiereFirma} onChange={(event) => setChecklistForm({ ...checklistForm, requiereFirma: event.target.checked })} />Firma</label>
              </div>
              <button className="secondary-button" type="submit" disabled={isSaving || !canPlan}><Plus size={18} /> Agregar checklist</button>
              <div className="form-grid">
                <TaskSelect tasks={detail.tasks} value={templateForm.codigoTarea} onChange={(value) => setTemplateForm({ ...templateForm, codigoTarea: value })} />
                <label>Plantilla<input value={templateForm.templateCode} onChange={(event) => setTemplateForm({ ...templateForm, templateCode: event.target.value })} placeholder="CHK-PREV-01" required /></label>
                <button className="secondary-button" type="button" disabled={isSaving || !canPlan || !templateForm.templateCode} onClick={() => void applyChecklistTemplate()}><ClipboardCheck size={18} /> Aplicar</button>
              </div>
              <MiniTable rows={detail.checklist.map((item) => [item.codigoTarea, item.item, item.tipoRespuesta, <button key={item.itemId} type="button" className={item.completado ? "text-emerald-700" : "text-amber-700"} onClick={() => void toggleChecklist(item)}>{item.completado ? "Completo" : "Pendiente"}</button>])} />
              <div className="form-grid">
                <TaskSelect tasks={detail.tasks} value={signatureForm.codigoTarea} onChange={(value) => setSignatureForm({ ...signatureForm, codigoTarea: value, scope: value ? "Tarea" : "OT" })} allowEmpty />
                <label>Firma archivo<input value={signatureForm.signatureFileKey} onChange={(event) => setSignatureForm({ ...signatureForm, signatureFileKey: event.target.value })} placeholder="firma/usuario.svg" /></label>
                <label>Comentario<input value={signatureForm.comentario} onChange={(event) => setSignatureForm({ ...signatureForm, comentario: event.target.value })} /></label>
              </div>
              <SignaturePad value={signatureForm.signatureImageDataUrl} onChange={(value) => setSignatureForm({ ...signatureForm, signatureImageDataUrl: value })} />
              <button className="secondary-button" type="button" disabled={isSaving || (!signatureForm.signatureFileKey && !signatureForm.signatureImageDataUrl)} onClick={() => void registerSignature()}><PenLine size={18} /> Registrar firma</button>
              <MiniTable rows={detail.signatures.map((item) => [item.usuarioId, item.codigoTarea ?? "OT", item.signatureFileKey ?? "firma dibujada", item.firmaId])} />
            </form>
          </section>
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

function MiniTable({ rows }: { rows: Array<Array<ReactNode>> }) {
  if (rows.length === 0) {
    return <p className="text-sm text-slate-500 dark:text-slate-400">Sin registros.</p>;
  }

  return (
    <div className="data-table max-h-56">
      <table>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index}>
              {row.map((cell, cellIndex) => (
                <td key={cellIndex}>{cell}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function TaskSelect({ tasks, value, onChange, allowEmpty = false }: { tasks: WorkOrderTask[]; value: string; onChange: (value: string) => void; allowEmpty?: boolean }) {
  return (
    <label className="flex flex-col gap-1 text-sm font-semibold text-slate-700 dark:text-slate-200">
      Tarea
      <select value={value} onChange={(event) => onChange(event.target.value)}>
        {allowEmpty ? <option value="">OT general</option> : null}
        {tasks.map((task) => (
          <option key={task.codigoTarea} value={task.codigoTarea}>
            {task.codigoTarea} - {task.descripcion}
          </option>
        ))}
      </select>
    </label>
  );
}

function SignaturePad({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const canvasRef = useRef<HTMLCanvasElement | null>(null);
  const isDrawingRef = useRef(false);

  function getPoint(event: ReactPointerEvent<HTMLCanvasElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    return {
      x: ((event.clientX - rect.left) / rect.width) * event.currentTarget.width,
      y: ((event.clientY - rect.top) / rect.height) * event.currentTarget.height
    };
  }

  function startDrawing(event: ReactPointerEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (!canvas || !context) return;
    const point = getPoint(event);
    isDrawingRef.current = true;
    context.lineWidth = 2.5;
    context.lineCap = "round";
    context.strokeStyle = "#0f766e";
    context.beginPath();
    context.moveTo(point.x, point.y);
    event.currentTarget.setPointerCapture(event.pointerId);
  }

  function draw(event: ReactPointerEvent<HTMLCanvasElement>) {
    if (!isDrawingRef.current) return;
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (!canvas || !context) return;
    const point = getPoint(event);
    context.lineTo(point.x, point.y);
    context.stroke();
  }

  function stopDrawing() {
    const canvas = canvasRef.current;
    if (!canvas || !isDrawingRef.current) return;
    isDrawingRef.current = false;
    onChange(canvas.toDataURL("image/png"));
  }

  function clearSignature() {
    const canvas = canvasRef.current;
    const context = canvas?.getContext("2d");
    if (!canvas || !context) return;
    context.clearRect(0, 0, canvas.width, canvas.height);
    onChange("");
  }

  return (
    <div className="stack">
      <div className="rounded-md border border-slate-200 bg-white p-2 dark:border-slate-800 dark:bg-slate-950">
        <canvas
          ref={canvasRef}
          className="h-28 w-full touch-none rounded bg-white"
          height={160}
          onPointerDown={startDrawing}
          onPointerLeave={stopDrawing}
          onPointerMove={draw}
          onPointerUp={stopDrawing}
          width={640}
        />
      </div>
      <div className="toolbar">
        <button className="secondary-button" type="button" onClick={clearSignature}>
          Limpiar firma
        </button>
        {value ? <span className="text-sm font-semibold text-emerald-700">Firma capturada</span> : null}
      </div>
    </div>
  );
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function toIsoOrNull(value: string) {
  return value ? new Date(`${value}T00:00:00Z`).toISOString() : null;
}

function toIsoDate(value: string) {
  return new Date(`${value}T00:00:00Z`).toISOString();
}

function toIsoDateTimeOrNull(date: string, time: string) {
  if (!time) {
    return null;
  }

  const effectiveDate = date || new Date().toISOString().slice(0, 10);
  return new Date(`${effectiveDate}T${time}:00`).toISOString();
}

function defaultChecklistResponse(type: ChecklistResponseType) {
  if (type === "BuenoRegularMalo") return "Bueno";
  if (type === "SiNo") return "Si";
  if (type === "CumpleNoCumpleNoAplica") return "Cumple";
  return null;
}

function formatDate(value?: string | null) {
  return value ? new Date(value).toLocaleDateString() : "-";
}
