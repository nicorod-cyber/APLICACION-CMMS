import { FormEvent, useEffect, useMemo, useState, type ReactNode } from "react";
import { Activity, AlertTriangle, Clock, Download, Gauge, Link2, RefreshCw, Save, ShieldCheck } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";
import { MaintenanceTargetSelect, type MaintenanceTargetReference } from "../maintenance-targets/MaintenanceTargetSelect";

type AvailabilityPeriod = "Dia" | "Semana" | "Mes" | "Acumulado";
type ContractAssetRole = "Comprometido" | "Backup" | "Arriendo" | "Asignado";
type AvailabilityCause =
  | "MantenimientoCorrectivo"
  | "MantenimientoPreventivo"
  | "Repuestos"
  | "DocumentacionVencida"
  | "TrasladoMantenimiento"
  | "ServicioExterno"
  | "PruebaLiberacionTecnica"
  | "PendienteDiagnostico"
  | "FallaRepetitiva"
  | "OperacionalExternaNoAtribuible";

type AssetSummary = {
  codigo: string;
  nombre: string;
  faenaCodigo: string;
  tipoActivo: string;
  estado: string;
  ubicacionTecnicaCodigo?: string | null;
  familia?: string | null;
  criticidad?: string | null;
  estadoDocumental: string;
  estadoOperacional: string;
  disponibleDocumentalmente: boolean;
};

type AvailabilityContractAsset = {
  assignmentId: string;
  contractCode: string;
  activoCodigo: string;
  activoNombre?: string | null;
  faenaCodigo: string;
  rol: ContractAssetRole;
  fechaInicio?: string | null;
  fechaFin?: string | null;
  activo: boolean;
  objetivo?: MaintenanceTargetReference | null;
};

type AvailabilityContract = {
  contractCode: string;
  nombre: string;
  cliente: string;
  faenaCodigo: string;
  horasComprometidasDia: number;
  disponibilidadObjetivo: number;
  fechaInicio?: string | null;
  fechaFin?: string | null;
  reglasCliente?: string | null;
  activo: boolean;
  assets: AvailabilityContractAsset[];
};

type AvailabilityKpi = {
  equiposComprometidos: number;
  equiposCubiertos: number;
  equiposNoDisponibles: number;
  horasComprometidas: number;
  horasDisponibles: number;
  horasNoDisponiblesPenalizadas: number;
  disponibilidadCantidad: number;
  disponibilidadHoras: number;
  disponibilidadObjetivo: number;
  cumpleObjetivo: boolean;
};

type AvailabilityContractSummary = {
  contractCode: string;
  nombre: string;
  cliente: string;
  faenaCodigo: string;
  equiposComprometidos: number;
  equiposCubiertos: number;
  horasComprometidas: number;
  horasDisponibles: number;
  disponibilidadCantidad: number;
  disponibilidadHoras: number;
  disponibilidadObjetivo: number;
  cumpleObjetivo: boolean;
};

type AvailabilityFaenaSummary = {
  faenaCodigo: string;
  equiposComprometidos: number;
  equiposCubiertos: number;
  disponibilidadCantidad: number;
  disponibilidadHoras: number;
};

type AvailabilityCauseSummary = {
  causa: AvailabilityCause;
  horasNoDisponibles: number;
  eventos: number;
  penalizaDisponibilidad: boolean;
};

type UnavailableAsset = {
  contractCode: string;
  activoCodigo: string;
  activoNombre?: string | null;
  faenaCodigo: string;
  causa: AvailabilityCause;
  inicioUtc: string;
  finUtc?: string | null;
  horasNoDisponibles: number;
  penalizaDisponibilidad: boolean;
  cubiertoPorBackup: boolean;
  numeroOT?: string | null;
  objetivo?: MaintenanceTargetReference | null;
};

type AvailabilityTrendPoint = {
  periodKey: string;
  from: string;
  to: string;
  disponibilidadCantidad: number;
  disponibilidadHoras: number;
  equiposComprometidos: number;
  equiposCubiertos: number;
  horasComprometidas: number;
  horasDisponibles: number;
};

type AvailabilityEvent = {
  eventId: string;
  contractCode: string;
  activoCodigo: string;
  activoNombre?: string | null;
  faenaCodigo: string;
  causa: AvailabilityCause;
  inicioUtc: string;
  finUtc?: string | null;
  puedeUtilizarse: boolean;
  atribuibleMantenimiento: boolean;
  penalizaDisponibilidad: boolean;
  numeroOT?: string | null;
  comentario?: string | null;
  usuarioId: string;
  createdAtUtc: string;
  objetivo?: MaintenanceTargetReference | null;
};

type AvailabilityDashboard = {
  kpi: AvailabilityKpi;
  byContract: AvailabilityContractSummary[];
  byFaena: AvailabilityFaenaSummary[];
  byCause: AvailabilityCauseSummary[];
  unavailableAssets: UnavailableAsset[];
  trends: AvailabilityTrendPoint[];
  events: AvailabilityEvent[];
};

const periodLabels: Record<AvailabilityPeriod, string> = {
  Dia: "Dia",
  Semana: "Semana",
  Mes: "Mes",
  Acumulado: "Acumulado"
};

const roleLabels: Record<ContractAssetRole, string> = {
  Comprometido: "Comprometido",
  Backup: "Backup",
  Arriendo: "Arriendo",
  Asignado: "Asignado"
};

const causeLabels: Record<AvailabilityCause, string> = {
  MantenimientoCorrectivo: "Mantenimiento correctivo",
  MantenimientoPreventivo: "Mantenimiento preventivo",
  Repuestos: "Repuestos",
  DocumentacionVencida: "Documentacion vencida",
  TrasladoMantenimiento: "Traslado mantenimiento",
  ServicioExterno: "Servicio externo",
  PruebaLiberacionTecnica: "Prueba / liberacion tecnica",
  PendienteDiagnostico: "Pendiente diagnostico",
  FallaRepetitiva: "Falla repetitiva",
  OperacionalExternaNoAtribuible: "Operacional externa"
};

const emptyContractForm = {
  contractCode: "",
  nombre: "",
  cliente: "",
  faenaCodigo: "",
  horasComprometidasDia: "24",
  disponibilidadObjetivo: "90",
  fechaInicio: "",
  fechaFin: "",
  reglasCliente: "",
  activo: true,
  reason: "Configuracion disponibilidad contractual"
};

const emptyAssignmentForm = {
  contractCode: "",
  activoCodigo: "",
  objetivo: null as MaintenanceTargetReference | null,
  rol: "Comprometido" as ContractAssetRole,
  fechaInicio: "",
  fechaFin: "",
  activo: true,
  reason: "Asignacion contractual"
};

const emptyEventForm = {
  contractCode: "",
  activoCodigo: "",
  objetivo: null as MaintenanceTargetReference | null,
  causa: "MantenimientoCorrectivo" as AvailabilityCause,
  inicioUtc: toInputDateTime(new Date()),
  finUtc: "",
  puedeUtilizarse: false,
  atribuibleMantenimiento: true,
  numeroOT: "",
  comentario: ""
};

export function AvailabilityPage() {
  const [dashboard, setDashboard] = useState<AvailabilityDashboard | null>(null);
  const [contracts, setContracts] = useState<AvailabilityContract[]>([]);
  const [assets, setAssets] = useState<AssetSummary[]>([]);
  const [selectedContractCode, setSelectedContractCode] = useState("");
  const [filters, setFilters] = useState({
    faenaCodigo: "",
    contractCode: "",
    cliente: "",
    period: "Mes" as AvailabilityPeriod,
    from: toInputDateTime(startOfMonth(new Date())),
    to: toInputDateTime(new Date())
  });
  const [contractForm, setContractForm] = useState(emptyContractForm);
  const [assignmentForm, setAssignmentForm] = useState(emptyAssignmentForm);
  const [eventForm, setEventForm] = useState(emptyEventForm);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void loadAll();
  }, [filters.faenaCodigo, filters.contractCode, filters.cliente, filters.period, filters.from, filters.to]);

  const selectedContract = useMemo(
    () => contracts.find((item) => item.contractCode === selectedContractCode) ?? contracts[0] ?? null,
    [contracts, selectedContractCode]
  );

  const filteredAssets = useMemo(() => {
    const contractFaena = selectedContract?.faenaCodigo || contractForm.faenaCodigo || filters.faenaCodigo;
    return contractFaena ? assets.filter((asset) => asset.faenaCodigo === contractFaena) : assets;
  }, [assets, contractForm.faenaCodigo, filters.faenaCodigo, selectedContract?.faenaCodigo]);

  async function loadAll() {
    setIsLoading(true);
    setError(null);

    try {
      const query = buildDashboardQuery(filters);
      const contractQuery = new URLSearchParams();
      if (filters.faenaCodigo) contractQuery.set("faenaCodigo", filters.faenaCodigo);
      if (filters.cliente) contractQuery.set("cliente", filters.cliente);
      contractQuery.set("includeInactive", "true");

      const assetPath = filters.faenaCodigo ? `/api/assets?faenaCodigo=${encodeURIComponent(filters.faenaCodigo)}` : "/api/assets";
      const [dashboardResult, contractResult, assetResult] = await Promise.all([
        apiFetch<AvailabilityDashboard>(`/api/availability/dashboard?${query}`),
        apiFetch<AvailabilityContract[]>(`/api/availability/contracts?${contractQuery}`),
        apiFetch<AssetSummary[]>(assetPath)
      ]);

      setDashboard(dashboardResult);
      setContracts(contractResult);
      setAssets(assetResult);

      const nextSelected =
        contractResult.find((contract) => contract.contractCode === selectedContractCode)?.contractCode ??
        contractResult.find((contract) => contract.contractCode === filters.contractCode)?.contractCode ??
        contractResult[0]?.contractCode ??
        "";
      setSelectedContractCode(nextSelected);
      setAssignmentForm((current) => ({ ...current, contractCode: current.contractCode || nextSelected }));
      setEventForm((current) => ({ ...current, contractCode: current.contractCode || nextSelected }));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar disponibilidad contractual.");
    } finally {
      setIsLoading(false);
    }
  }

  async function saveContract(event: FormEvent) {
    event.preventDefault();
    await saveAction(async () => {
      const saved = await apiFetch<AvailabilityContract>("/api/availability/contracts", {
        method: "POST",
        body: JSON.stringify({
          contractCode: contractForm.contractCode,
          nombre: contractForm.nombre,
          cliente: contractForm.cliente,
          faenaCodigo: contractForm.faenaCodigo,
          horasComprometidasDia: Number(contractForm.horasComprometidasDia || 24),
          disponibilidadObjetivo: Number(contractForm.disponibilidadObjetivo || 90) / 100,
          fechaInicio: toIsoOrNull(contractForm.fechaInicio),
          fechaFin: toIsoOrNull(contractForm.fechaFin),
          reglasCliente: emptyToNull(contractForm.reglasCliente),
          activo: contractForm.activo,
          reason: contractForm.reason
        })
      });
      setSelectedContractCode(saved.contractCode);
      setAssignmentForm((current) => ({ ...current, contractCode: saved.contractCode }));
      setEventForm((current) => ({ ...current, contractCode: saved.contractCode }));
      setContractForm(emptyContractForm);
      setMessage(`Contrato ${saved.contractCode} guardado.`);
      await loadAll();
    }, "No fue posible guardar el contrato.");
  }

  async function assignAsset(event: FormEvent) {
    event.preventDefault();
    await saveAction(async () => {
      const contractCode = assignmentForm.contractCode || selectedContract?.contractCode;
      if (!contractCode) {
        throw new Error("Selecciona un contrato.");
      }

      await apiFetch<AvailabilityContractAsset>(`/api/availability/contracts/${encodeURIComponent(contractCode)}/targets`, {
        method: "POST",
        body: JSON.stringify({
          contractCode,
          objetivo: assignmentForm.objetivo,
          rol: assignmentForm.rol,
          activo: assignmentForm.activo,
          reason: assignmentForm.reason,
          fechaInicio: toIsoOrNull(assignmentForm.fechaInicio),
          fechaFin: toIsoOrNull(assignmentForm.fechaFin)
        })
      });
      setAssignmentForm({ ...emptyAssignmentForm, contractCode });
      setMessage("Objetivo asignado al contrato.");
      await loadAll();
    }, "No fue posible asignar el objetivo.");
  }

  async function registerEvent(event: FormEvent) {
    event.preventDefault();
    await saveAction(async () => {
      const contractCode = eventForm.contractCode || selectedContract?.contractCode;
      if (!contractCode) {
        throw new Error("Selecciona un contrato.");
      }

      await apiFetch<AvailabilityEvent>("/api/availability/events", {
        method: "POST",
        body: JSON.stringify({
          contractCode,
          objetivo: eventForm.objetivo,
          causa: eventForm.causa,
          inicioUtc: toIso(eventForm.inicioUtc),
          finUtc: toIsoOrNull(eventForm.finUtc),
          puedeUtilizarse: eventForm.puedeUtilizarse,
          atribuibleMantenimiento: eventForm.atribuibleMantenimiento,
          numeroOT: emptyToNull(eventForm.numeroOT),
          comentario: emptyToNull(eventForm.comentario)
        })
      });
      setEventForm({ ...emptyEventForm, contractCode });
      setMessage("Evento de disponibilidad registrado.");
      await loadAll();
    }, "No fue posible registrar el evento.");
  }

  async function saveAction(action: () => Promise<void>, fallback: string) {
    setIsSaving(true);
    setError(null);
    try {
      await action();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : fallback);
    } finally {
      setIsSaving(false);
    }
  }

  function applyContract(contract: AvailabilityContract) {
    setSelectedContractCode(contract.contractCode);
    setAssignmentForm((current) => ({ ...current, contractCode: contract.contractCode }));
    setEventForm((current) => ({ ...current, contractCode: contract.contractCode }));
    setContractForm({
      contractCode: contract.contractCode,
      nombre: contract.nombre,
      cliente: contract.cliente,
      faenaCodigo: contract.faenaCodigo,
      horasComprometidasDia: String(contract.horasComprometidasDia),
      disponibilidadObjetivo: String(Math.round(contract.disponibilidadObjetivo * 100)),
      fechaInicio: toInputDateTimeOrEmpty(contract.fechaInicio),
      fechaFin: toInputDateTimeOrEmpty(contract.fechaFin),
      reglasCliente: contract.reglasCliente ?? "",
      activo: contract.activo,
      reason: "Actualizacion disponibilidad contractual"
    });
  }

  function exportCsv() {
    if (!dashboard) {
      return;
    }

    const rows: string[][] = [
      ["Seccion", "Contrato", "Faena", "Cliente", "Indicador", "Valor", "Detalle"]
    ];

    dashboard.byContract.forEach((item) => {
      rows.push(["Contrato", item.contractCode, item.faenaCodigo, item.cliente, "Disponibilidad cantidad", formatPercent(item.disponibilidadCantidad), `${item.equiposCubiertos}/${item.equiposComprometidos}`]);
      rows.push(["Contrato", item.contractCode, item.faenaCodigo, item.cliente, "Disponibilidad horas", formatPercent(item.disponibilidadHoras), `${formatNumber(item.horasDisponibles)}/${formatNumber(item.horasComprometidas)} h`]);
    });

    dashboard.byCause.forEach((item) => {
      rows.push(["Causa", "", "", "", causeLabels[item.causa], formatNumber(item.horasNoDisponibles), item.penalizaDisponibilidad ? "Penaliza" : "No penaliza"]);
    });

    dashboard.unavailableAssets.forEach((item) => {
      rows.push(["No disponible", item.contractCode, item.faenaCodigo, "", availabilityTargetLabel(item), formatNumber(item.horasNoDisponibles), `${causeLabels[item.causa]}${item.cubiertoPorBackup ? " cubierto por backup" : ""}`]);
    });

    dashboard.trends.forEach((item) => {
      rows.push(["Tendencia", "", "", "", item.periodKey, formatPercent(item.disponibilidadHoras), `${formatDate(item.from)} - ${formatDate(item.to)}`]);
    });

    downloadFile(`disponibilidad_contractual_${new Date().toISOString().slice(0, 10)}.csv`, rows.map(toCsvLine).join("\n"));
  }

  return (
    <section className="stack">
      <header className="page-header">
        <div>
          <p className="eyebrow">Contratos y flota comprometida</p>
          <h1>Disponibilidad contractual</h1>
          <p>Control por faena, contrato, cliente, activos comprometidos, backups y causas atribuibles.</p>
        </div>
        <div className="toolbar">
          <button className="secondary-button" type="button" onClick={() => void loadAll()}>
            <RefreshCw size={18} /> Actualizar
          </button>
          <button className="secondary-button" type="button" onClick={exportCsv} disabled={!dashboard}>
            <Download size={18} /> Exportar
          </button>
        </div>
      </header>

      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <Metric icon={<Gauge size={18} />} label="Disponibilidad horas" value={formatPercent(dashboard?.kpi.disponibilidadHoras ?? 0)} />
        <Metric icon={<Activity size={18} />} label="Disponibilidad equipos" value={formatPercent(dashboard?.kpi.disponibilidadCantidad ?? 0)} />
        <Metric icon={<ShieldCheck size={18} />} label="Equipos cubiertos" value={`${dashboard?.kpi.equiposCubiertos ?? 0}/${dashboard?.kpi.equiposComprometidos ?? 0}`} />
        <Metric icon={<Clock size={18} />} label="Horas penalizadas" value={formatNumber(dashboard?.kpi.horasNoDisponiblesPenalizadas ?? 0)} />
      </div>

      {message ? <div className="success-banner">{message}</div> : null}
      {error ? <div className="error-banner">{error}</div> : null}

      <section className="panel stack">
        <div className="section-heading">
          <div>
            <h2>Filtros</h2>
            <span>{isLoading ? "Actualizando calculo..." : "Periodo contractual evaluado"}</span>
          </div>
          <span className={dashboard?.kpi.cumpleObjetivo ? "status-pill success" : "status-pill danger"}>
            Objetivo {formatPercent(dashboard?.kpi.disponibilidadObjetivo ?? 0)}
          </span>
        </div>
        <div className="form-grid">
          <FaenaSelect
            label="Faena"
            value={filters.faenaCodigo}
            onChange={(faenaCodigo) => setFilters({ ...filters, faenaCodigo, contractCode: "" })}
          />
          <label>
            Contrato
            <select value={filters.contractCode} onChange={(event) => setFilters({ ...filters, contractCode: event.target.value })}>
              <option value="">Todos</option>
              {contracts.map((contract) => (
                <option key={contract.contractCode} value={contract.contractCode}>
                  {contract.nombre} ({contract.contractCode})
                </option>
              ))}
            </select>
          </label>
          <label>
            Cliente
            <input value={filters.cliente} onChange={(event) => setFilters({ ...filters, cliente: event.target.value })} placeholder="Cliente" />
          </label>
          <label>
            Periodo
            <select value={filters.period} onChange={(event) => setFilters({ ...filters, period: event.target.value as AvailabilityPeriod })}>
              {Object.entries(periodLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </select>
          </label>
          <label>
            Desde
            <input type="datetime-local" value={filters.from} onChange={(event) => setFilters({ ...filters, from: event.target.value })} />
          </label>
          <label>
            Hasta
            <input type="datetime-local" value={filters.to} onChange={(event) => setFilters({ ...filters, to: event.target.value })} />
          </label>
        </div>
      </section>

      <div className="two-column-layout">
        <form className="panel stack" onSubmit={saveContract}>
          <div className="section-heading">
            <div>
              <h2>Contrato</h2>
              <span>Cliente, faena y regla objetivo</span>
            </div>
            <button className="primary-button" type="submit" disabled={isSaving}>
              <Save size={18} /> Guardar
            </button>
          </div>
          <div className="form-grid">
            <label>
              Codigo
              <input value={contractForm.contractCode} onChange={(event) => setContractForm({ ...contractForm, contractCode: event.target.value })} required />
            </label>
            <label>
              Nombre
              <input value={contractForm.nombre} onChange={(event) => setContractForm({ ...contractForm, nombre: event.target.value })} required />
            </label>
            <label>
              Cliente
              <input value={contractForm.cliente} onChange={(event) => setContractForm({ ...contractForm, cliente: event.target.value })} required />
            </label>
            <FaenaSelect
              label="Faena"
              value={contractForm.faenaCodigo}
              onChange={(faenaCodigo) => setContractForm({ ...contractForm, faenaCodigo })}
              includeEmpty={false}
            />
            <label>
              Horas comprometidas dia
              <input type="number" min="0.1" step="0.1" value={contractForm.horasComprometidasDia} onChange={(event) => setContractForm({ ...contractForm, horasComprometidasDia: event.target.value })} />
            </label>
            <label>
              Objetivo %
              <input type="number" min="0" max="100" step="0.1" value={contractForm.disponibilidadObjetivo} onChange={(event) => setContractForm({ ...contractForm, disponibilidadObjetivo: event.target.value })} />
            </label>
            <label>
              Inicio
              <input type="datetime-local" value={contractForm.fechaInicio} onChange={(event) => setContractForm({ ...contractForm, fechaInicio: event.target.value })} />
            </label>
            <label>
              Fin
              <input type="datetime-local" value={contractForm.fechaFin} onChange={(event) => setContractForm({ ...contractForm, fechaFin: event.target.value })} />
            </label>
            <label className="span-2">
              Reglas cliente
              <textarea value={contractForm.reglasCliente} onChange={(event) => setContractForm({ ...contractForm, reglasCliente: event.target.value })} />
            </label>
            <label className="span-2">
              Motivo auditoria
              <input value={contractForm.reason} onChange={(event) => setContractForm({ ...contractForm, reason: event.target.value })} />
            </label>
            <label className="check-row">
              <input type="checkbox" checked={contractForm.activo} onChange={(event) => setContractForm({ ...contractForm, activo: event.target.checked })} />
              Activo
            </label>
          </div>
        </form>

        <div className="panel stack">
          <div className="section-heading">
            <div>
              <h2>Asignacion y eventos</h2>
              <span>Comprometidos, backups, arriendos y paradas</span>
            </div>
          </div>

          <form className="stack" onSubmit={assignAsset}>
            <div className="form-grid">
              <label>
                Contrato
                <select value={assignmentForm.contractCode || selectedContract?.contractCode || ""} onChange={(event) => setAssignmentForm({ ...assignmentForm, contractCode: event.target.value })}>
                  <option value="">Selecciona contrato</option>
                  {contracts.map((contract) => (
                    <option key={contract.contractCode} value={contract.contractCode}>
                      {contract.nombre}
                    </option>
                  ))}
                </select>
              </label>
              <MaintenanceTargetSelect
                value={assignmentForm.objetivo}
                faenaCodigo={selectedContract?.faenaCodigo}
                soloDisponibilidad
                required
                onChange={(objetivo) => setAssignmentForm({ ...assignmentForm, objetivo })}
              />
              <label>
                Rol
                <select value={assignmentForm.rol} onChange={(event) => setAssignmentForm({ ...assignmentForm, rol: event.target.value as ContractAssetRole })}>
                  {Object.entries(roleLabels).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>
              <button className="secondary-button" type="submit" disabled={isSaving}>
                <Link2 size={18} /> Asignar
              </button>
            </div>
          </form>

          <form className="stack" onSubmit={registerEvent}>
            <div className="form-grid">
              <label>
                Contrato
                <select value={eventForm.contractCode || selectedContract?.contractCode || ""} onChange={(event) => setEventForm({ ...eventForm, contractCode: event.target.value })}>
                  <option value="">Selecciona contrato</option>
                  {contracts.map((contract) => (
                    <option key={contract.contractCode} value={contract.contractCode}>
                      {contract.nombre}
                    </option>
                  ))}
                </select>
              </label>
              <MaintenanceTargetSelect
                value={eventForm.objetivo}
                faenaCodigo={selectedContract?.faenaCodigo}
                soloDisponibilidad
                required
                onChange={(objetivo) => setEventForm({ ...eventForm, objetivo })}
              />
              <label>
                Causa
                <select value={eventForm.causa} onChange={(event) => setEventForm({ ...eventForm, causa: event.target.value as AvailabilityCause })}>
                  {Object.entries(causeLabels).map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Inicio
                <input type="datetime-local" value={eventForm.inicioUtc} onChange={(event) => setEventForm({ ...eventForm, inicioUtc: event.target.value })} required />
              </label>
              <label>
                Fin
                <input type="datetime-local" value={eventForm.finUtc} onChange={(event) => setEventForm({ ...eventForm, finUtc: event.target.value })} />
              </label>
              <label>
                OT
                <input value={eventForm.numeroOT} onChange={(event) => setEventForm({ ...eventForm, numeroOT: event.target.value })} />
              </label>
              <label className="check-row">
                <input type="checkbox" checked={eventForm.puedeUtilizarse} onChange={(event) => setEventForm({ ...eventForm, puedeUtilizarse: event.target.checked })} />
                Puede utilizarse
              </label>
              <label className="check-row">
                <input type="checkbox" checked={eventForm.atribuibleMantenimiento} onChange={(event) => setEventForm({ ...eventForm, atribuibleMantenimiento: event.target.checked })} />
                Atribuible mantenimiento
              </label>
              <label className="span-2">
                Comentario
                <input value={eventForm.comentario} onChange={(event) => setEventForm({ ...eventForm, comentario: event.target.value })} />
              </label>
              <button className="primary-button" type="submit" disabled={isSaving}>
                <AlertTriangle size={18} /> Registrar evento
              </button>
            </div>
          </form>
        </div>
      </div>

      <div className="two-column-layout">
        <section className="panel stack">
          <div className="section-heading">
            <div>
              <h2>Contratos</h2>
              <span>{contracts.length} registros</span>
            </div>
          </div>
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>Contrato</th>
                  <th>Cliente</th>
                  <th>Faena</th>
                  <th>Disponibilidad</th>
                  <th>Equipos</th>
                </tr>
              </thead>
              <tbody>
                {dashboard?.byContract.map((item) => (
                  <tr key={item.contractCode} className={item.contractCode === selectedContract?.contractCode ? "selected-row" : ""} onClick={() => contracts.find((contract) => contract.contractCode === item.contractCode) && applyContract(contracts.find((contract) => contract.contractCode === item.contractCode)!)}>
                    <td><strong>{item.nombre}</strong><small>{item.contractCode}</small></td>
                    <td>{item.cliente}</td>
                    <td>{item.faenaCodigo}</td>
                    <td><span className={item.cumpleObjetivo ? "status-pill success" : "status-pill danger"}>{formatPercent(item.disponibilidadHoras)}</span><small>Cantidad {formatPercent(item.disponibilidadCantidad)}</small></td>
                    <td>{item.equiposCubiertos}/{item.equiposComprometidos}<small>{formatNumber(item.horasDisponibles)} h disp.</small></td>
                  </tr>
                ))}
                {!dashboard?.byContract.length ? <EmptyRow columns={5} text="Sin contratos para el filtro." /> : null}
              </tbody>
            </table>
          </div>
        </section>

        <section className="panel stack">
          <div className="section-heading">
            <div>
              <h2>Activos del contrato</h2>
              <span>{selectedContract?.nombre ?? "Sin seleccion"}</span>
            </div>
          </div>
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>Equipo</th>
                  <th>Rol</th>
                  <th>Faena</th>
                  <th>Estado</th>
                </tr>
              </thead>
              <tbody>
                {selectedContract?.assets.map((asset) => (
                  <tr key={asset.assignmentId}>
                    <td><strong>{availabilityTargetLabel(asset)}</strong><small>{availabilityTargetType(asset)}</small></td>
                    <td><span className="status-pill">{roleLabels[asset.rol]}</span></td>
                    <td>{asset.faenaCodigo}</td>
                    <td>{asset.activo ? "Activo" : "Inactivo"}</td>
                  </tr>
                ))}
                {!selectedContract?.assets.length ? <EmptyRow columns={4} text="Sin activos asignados." /> : null}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <div className="two-column-layout">
        <section className="panel stack">
          <div className="section-heading">
            <div>
              <h2>Causas</h2>
              <span>Horas no disponibles por causa</span>
            </div>
          </div>
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>Causa</th>
                  <th>Horas</th>
                  <th>Eventos</th>
                  <th>Regla</th>
                </tr>
              </thead>
              <tbody>
                {dashboard?.byCause.map((item) => (
                  <tr key={`${item.causa}-${item.penalizaDisponibilidad}`}>
                    <td><strong>{causeLabels[item.causa]}</strong></td>
                    <td>{formatNumber(item.horasNoDisponibles)}</td>
                    <td>{item.eventos}</td>
                    <td><span className={item.penalizaDisponibilidad ? "status-pill danger" : "status-pill success"}>{item.penalizaDisponibilidad ? "Penaliza" : "No penaliza"}</span></td>
                  </tr>
                ))}
                {!dashboard?.byCause.length ? <EmptyRow columns={4} text="Sin causas registradas." /> : null}
              </tbody>
            </table>
          </div>
        </section>

        <section className="panel stack">
          <div className="section-heading">
            <div>
              <h2>Faenas</h2>
              <span>Resumen operacional por faena</span>
            </div>
          </div>
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>Faena</th>
                  <th>Equipos</th>
                  <th>Cantidad</th>
                  <th>Horas</th>
                </tr>
              </thead>
              <tbody>
                {dashboard?.byFaena.map((item) => (
                  <tr key={item.faenaCodigo}>
                    <td><strong>{item.faenaCodigo}</strong></td>
                    <td>{item.equiposCubiertos}/{item.equiposComprometidos}</td>
                    <td>{formatPercent(item.disponibilidadCantidad)}</td>
                    <td>{formatPercent(item.disponibilidadHoras)}</td>
                  </tr>
                ))}
                {!dashboard?.byFaena.length ? <EmptyRow columns={4} text="Sin faenas para el filtro." /> : null}
              </tbody>
            </table>
          </div>
        </section>
      </div>

      <section className="panel stack">
        <div className="section-heading">
          <div>
            <h2>Equipos no disponibles</h2>
            <span>Eventos, documentos vencidos y OT que afectan la regla contractual</span>
          </div>
        </div>
        <div className="data-table">
          <table>
            <thead>
              <tr>
                <th>Equipo</th>
                <th>Contrato</th>
                <th>Causa</th>
                <th>Periodo</th>
                <th>Horas</th>
                <th>Cobertura</th>
              </tr>
            </thead>
            <tbody>
              {dashboard?.unavailableAssets.map((item, index) => (
                <tr key={`${item.contractCode}-${item.activoCodigo}-${item.inicioUtc}-${index}`}>
                  <td><strong>{availabilityTargetLabel(item)}</strong><small>{availabilityTargetType(item)} / {item.faenaCodigo}</small></td>
                  <td>{item.contractCode}</td>
                  <td>{causeLabels[item.causa]}<small>{item.numeroOT ?? ""}</small></td>
                  <td>{formatDateTime(item.inicioUtc)}<small>{item.finUtc ? formatDateTime(item.finUtc) : "En curso"}</small></td>
                  <td>{formatNumber(item.horasNoDisponibles)}</td>
                  <td><span className={item.cubiertoPorBackup ? "status-pill success" : item.penalizaDisponibilidad ? "status-pill danger" : "status-pill"}>{item.cubiertoPorBackup ? "Backup" : item.penalizaDisponibilidad ? "Penaliza" : "No penaliza"}</span></td>
                </tr>
              ))}
              {!dashboard?.unavailableAssets.length ? <EmptyRow columns={6} text="No hay activos no disponibles en el periodo." /> : null}
            </tbody>
          </table>
        </div>
      </section>

      <div className="two-column-layout">
        <section className="panel stack">
          <div className="section-heading">
            <div>
              <h2>Tendencia</h2>
              <span>Dia, semana, mes o acumulado</span>
            </div>
          </div>
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>Periodo</th>
                  <th>Disponibilidad</th>
                  <th>Equipos</th>
                  <th>Horas</th>
                </tr>
              </thead>
              <tbody>
                {dashboard?.trends.map((item) => (
                  <tr key={item.periodKey}>
                    <td><strong>{item.periodKey}</strong><small>{formatDate(item.from)} - {formatDate(item.to)}</small></td>
                    <td>{formatPercent(item.disponibilidadHoras)}<small>Cantidad {formatPercent(item.disponibilidadCantidad)}</small></td>
                    <td>{item.equiposCubiertos}/{item.equiposComprometidos}</td>
                    <td>{formatNumber(item.horasDisponibles)}/{formatNumber(item.horasComprometidas)}</td>
                  </tr>
                ))}
                {!dashboard?.trends.length ? <EmptyRow columns={4} text="Sin tendencia calculada." /> : null}
              </tbody>
            </table>
          </div>
        </section>

        <section className="panel stack">
          <div className="section-heading">
            <div>
              <h2>Eventos recientes</h2>
              <span>Estados registrados manualmente</span>
            </div>
          </div>
          <div className="data-table">
            <table>
              <thead>
                <tr>
                  <th>Equipo</th>
                  <th>Causa</th>
                  <th>Inicio</th>
                  <th>Regla</th>
                </tr>
              </thead>
              <tbody>
                {dashboard?.events.slice(0, 12).map((item) => (
                  <tr key={item.eventId}>
                    <td><strong>{availabilityTargetLabel(item)}</strong><small>{availabilityTargetType(item)} / {item.contractCode}</small></td>
                    <td>{causeLabels[item.causa]}<small>{item.comentario ?? ""}</small></td>
                    <td>{formatDateTime(item.inicioUtc)}</td>
                    <td><span className={item.penalizaDisponibilidad ? "status-pill danger" : "status-pill success"}>{item.penalizaDisponibilidad ? "Penaliza" : "No penaliza"}</span></td>
                  </tr>
                ))}
                {!dashboard?.events.length ? <EmptyRow columns={4} text="Sin eventos manuales registrados." /> : null}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </section>
  );
}

function Metric({ icon, label, value }: { icon: ReactNode; label: string; value: string }) {
  return (
    <div className="metric-card">
      {icon}
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  );
}

function EmptyRow({ columns, text }: { columns: number; text: string }) {
  return (
    <tr>
      <td colSpan={columns}>
        <small>{text}</small>
      </td>
    </tr>
  );
}

function buildDashboardQuery(filters: {
  faenaCodigo: string;
  contractCode: string;
  cliente: string;
  period: AvailabilityPeriod;
  from: string;
  to: string;
}) {
  const query = new URLSearchParams();
  if (filters.faenaCodigo) query.set("faenaCodigo", filters.faenaCodigo);
  if (filters.contractCode) query.set("contractCode", filters.contractCode);
  if (filters.cliente) query.set("cliente", filters.cliente);
  if (filters.period) query.set("period", filters.period);
  if (filters.from) query.set("from", toIso(filters.from));
  if (filters.to) query.set("to", toIso(filters.to));
  return query.toString();
}

function startOfMonth(date: Date) {
  return new Date(date.getFullYear(), date.getMonth(), 1, 0, 0, 0);
}

function availabilityTargetLabel(item: { activoNombre?: string | null; objetivo?: MaintenanceTargetReference | null }) {
  return item.activoNombre ?? "Objetivo no disponible";
}

function availabilityTargetType(item: { objetivo?: MaintenanceTargetReference | null }) {
  return item.objetivo?.tipo === "OperationalUnit" ? "Unidad operativa" : "Activo";
}

function toInputDateTime(date: Date) {
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

function toInputDateTimeOrEmpty(value?: string | null) {
  return value ? toInputDateTime(new Date(value)) : "";
}

function toIso(value: string) {
  return new Date(value).toISOString();
}

function toIsoOrNull(value: string) {
  return value ? toIso(value) : null;
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function formatPercent(value: number) {
  return `${Math.round((value || 0) * 1000) / 10}%`;
}

function formatNumber(value: number) {
  return new Intl.NumberFormat("es-CL", { maximumFractionDigits: 1 }).format(value || 0);
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("es-CL", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("es-CL", { dateStyle: "short" }).format(new Date(value));
}

function toCsvLine(values: string[]) {
  return values.map((value) => `"${value.replace(/"/g, '""')}"`).join(",");
}

function downloadFile(fileName: string, content: string) {
  const blob = new Blob([content], { type: "text/csv;charset=utf-8" });
  const url = URL.createObjectURL(blob);
  const link = document.createElement("a");
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
