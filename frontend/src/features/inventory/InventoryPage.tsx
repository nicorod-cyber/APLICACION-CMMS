import { FormEvent, ReactNode, useEffect, useMemo, useState } from "react";
import { AlertTriangle, ArrowRightLeft, Boxes, CheckCircle2, ClipboardList, PackageCheck, Plus, RefreshCw, SlidersHorizontal, Truck, Undo2, Warehouse } from "lucide-react";
import { AUTH_PERMISSIONS, apiFetch, useAuthStore } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";

type WarehouseType = "Central" | "Taller" | "Faena" | "Transito";
type StockMovementType =
  | "Reception"
  | "MaintenanceConsumption"
  | "Reservation"
  | "ReservationRelease"
  | "TransferOut"
  | "TransferIn"
  | "Adjustment"
  | "CountCorrection"
  | "ReturnFromWorkOrder"
  | "PositiveAdjustment"
  | "NegativeAdjustment"
  | "MaterialWriteOff"
  | "InTransit"
  | "TransferReception";

type OperationMode = "reservation" | "delivery" | "transfer" | "receiveTransfer" | "return" | "adjustment" | "writeOff" | "generic";

type WarehouseRecord = {
  codigo: string;
  nombre: string;
  faenaCodigo: string;
  tipo: WarehouseType;
  ubicacion?: string | null;
  ubicacionesInternas: string[];
  activa: boolean;
  responsable?: string | null;
  permiteStockNegativo: boolean;
};

type SparePartSummary = {
  codigo: string;
  codigoSap?: string | null;
  descripcion: string;
  unidadMedida: string;
  critico: boolean;
  stockDisponibleTotal: number;
  bajoMinimo: boolean;
  criticoSinStock: boolean;
};

type StockItem = {
  bodegaCodigo: string;
  bodegaNombre: string;
  faenaCodigo: string;
  repuestoCodigo: string;
  repuestoDescripcion: string;
  unidadMedida: string;
  repuestoCritico: boolean;
  stockFisico: number;
  stockReservado: number;
  stockDisponible: number;
  stockMinimo: number;
  stockMaximo: number;
  puntoReposicion: number;
  bajoMinimo: boolean;
  criticoSinStock: boolean;
  actualizadoEnUtc?: string | null;
};

type StockAlert = {
  alertKey: string;
  severity: string;
  repuestoCodigo: string;
  repuestoDescripcion: string;
  bodegaCodigo?: string | null;
  message: string;
};

type StockMovement = {
  movimientoId: string;
  fechaUtc: string;
  type: StockMovementType;
  repuestoCodigo: string;
  bodegaCodigo?: string | null;
  bodegaOrigenCodigo?: string | null;
  bodegaDestinoCodigo?: string | null;
  quantity: number;
  motivo: string;
  referenceType?: string | null;
  referenceId?: string | null;
};

type StockReservation = {
  reservaId: string;
  estado: string;
  fechaUtc: string;
  repuestoCodigo: string;
  bodegaCodigo: string;
  cantidadReservada: number;
  cantidadEntregada: number;
  cantidadLiberada: number;
  cantidadPendiente: number;
  workOrderId: string;
  solicitante: string;
};

type StockTransfer = {
  transferenciaId: string;
  estado: string;
  fechaSolicitudUtc: string;
  fechaRecepcionUtc?: string | null;
  repuestoCodigo: string;
  bodegaOrigenCodigo: string;
  bodegaTransitoCodigo: string;
  bodegaDestinoCodigo: string;
  cantidad: number;
  motivo: string;
};

type Dashboard = {
  totalRepuestos: number;
  repuestosCriticos: number;
  repuestosNoCodificados: number;
  repuestosBajoMinimo: number;
  criticosSinStock: number;
  totalBodegas: number;
  stockFisicoTotal: number;
  stockDisponibleTotal: number;
  alerts: StockAlert[];
};

type WarehouseForm = {
  codigo: string;
  nombre: string;
  faenaCodigo: string;
  tipo: WarehouseType;
  ubicacion: string;
  ubicacionesInternas: string;
  responsable: string;
  permiteStockNegativo: boolean;
};

type MovementForm = {
  type: StockMovementType;
  repuestoCodigo: string;
  bodegaCodigo: string;
  sourceWarehouseCode: string;
  targetWarehouseCode: string;
  quantity: string;
  reason: string;
  referenceType: string;
  referenceId: string;
  allowNegativeException: boolean;
  reusableReturn: boolean;
  supervisorApprovalUserId: string;
};

const emptyWarehouse: WarehouseForm = {
  codigo: "",
  nombre: "",
  faenaCodigo: "",
  tipo: "Faena",
  ubicacion: "",
  ubicacionesInternas: "",
  responsable: "",
  permiteStockNegativo: false
};

const emptyMovement: MovementForm = {
  type: "Reception",
  repuestoCodigo: "",
  bodegaCodigo: "",
  sourceWarehouseCode: "",
  targetWarehouseCode: "",
  quantity: "",
  reason: "",
  referenceType: "",
  referenceId: "",
  allowNegativeException: false,
  reusableReturn: true,
  supervisorApprovalUserId: ""
};

export function InventoryPage() {
  const user = useAuthStore((state) => state.user);
  const [dashboard, setDashboard] = useState<Dashboard | null>(null);
  const [warehouses, setWarehouses] = useState<WarehouseRecord[]>([]);
  const [spareParts, setSpareParts] = useState<SparePartSummary[]>([]);
  const [stock, setStock] = useState<StockItem[]>([]);
  const [movements, setMovements] = useState<StockMovement[]>([]);
  const [reservations, setReservations] = useState<StockReservation[]>([]);
  const [transfers, setTransfers] = useState<StockTransfer[]>([]);
  const [filters, setFilters] = useState({ bodegaCodigo: "", faenaCodigo: "", lowStockOnly: false, criticalOnly: false });
  const [warehouseForm, setWarehouseForm] = useState<WarehouseForm>(emptyWarehouse);
  const [movementForm, setMovementForm] = useState<MovementForm>(emptyMovement);
  const [operationMode, setOperationMode] = useState<OperationMode>("delivery");
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canAdjustStock = Boolean(user?.permissions.includes(AUTH_PERMISSIONS.adjustStock) || user?.permissions.includes(AUTH_PERMISSIONS.administration));

  useEffect(() => {
    void loadAll();
  }, []);

  const filteredStock = useMemo(() => {
    return stock.filter((item) => {
      if (filters.bodegaCodigo && item.bodegaCodigo !== filters.bodegaCodigo) {
        return false;
      }
      if (filters.faenaCodigo && item.faenaCodigo !== filters.faenaCodigo) {
        return false;
      }
      if (filters.lowStockOnly && !item.bajoMinimo && !item.criticoSinStock) {
        return false;
      }
      if (filters.criticalOnly && !item.repuestoCritico) {
        return false;
      }
      return true;
    });
  }, [filters, stock]);

  const transitWarehouses = useMemo(() => warehouses.filter((item) => item.tipo === "Transito"), [warehouses]);
  const openTransfers = useMemo(() => transfers.filter((item) => item.estado === "EnTransito"), [transfers]);

  async function loadAll() {
    setIsLoading(true);
    setError(null);
    try {
      const [dashboardResult, warehouseResult, stockResult, spareResult, movementResult, reservationResult, transferResult] = await Promise.all([
        apiFetch<Dashboard>("/api/inventory/dashboard"),
        apiFetch<WarehouseRecord[]>("/api/inventory/warehouses"),
        apiFetch<StockItem[]>("/api/inventory/stock"),
        apiFetch<SparePartSummary[]>("/api/inventory/spare-parts?includeObsolete=true"),
        apiFetch<StockMovement[]>("/api/inventory/stock/movements?take=80"),
        apiFetch<StockReservation[]>("/api/inventory/reservations"),
        apiFetch<StockTransfer[]>("/api/inventory/transfers")
      ]);
      setDashboard(dashboardResult);
      setWarehouses(warehouseResult);
      setStock(stockResult);
      setSpareParts(spareResult);
      setMovements(movementResult);
      setReservations(reservationResult);
      setTransfers(transferResult);
      setMovementForm((current) => ({
        ...current,
        bodegaCodigo: current.bodegaCodigo || warehouseResult[0]?.codigo || "",
        repuestoCodigo: current.repuestoCodigo || spareResult[0]?.codigo || ""
      }));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar bodega.");
    } finally {
      setIsLoading(false);
    }
  }

  async function createWarehouse(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      await apiFetch<WarehouseRecord>("/api/inventory/warehouses", {
        method: "POST",
        body: JSON.stringify({
          codigo: warehouseForm.codigo,
          nombre: warehouseForm.nombre,
          faenaCodigo: warehouseForm.faenaCodigo,
          tipo: warehouseForm.tipo,
          ubicacion: emptyToNull(warehouseForm.ubicacion),
          ubicacionesInternas: parseList(warehouseForm.ubicacionesInternas),
          activa: true,
          responsable: emptyToNull(warehouseForm.responsable),
          permiteStockNegativo: warehouseForm.permiteStockNegativo
        })
      });
      setWarehouseForm(emptyWarehouse);
      setMessage("Bodega creada.");
      await loadAll();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible crear bodega.");
    } finally {
      setIsSaving(false);
    }
  }

  async function registerMovement(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      await submitInventoryOperation();
      setMovementForm((current) => ({ ...emptyMovement, bodegaCodigo: current.bodegaCodigo, repuestoCodigo: current.repuestoCodigo }));
      setMessage("Operacion registrada.");
      await loadAll();
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible registrar la operacion.");
    } finally {
      setIsSaving(false);
    }
  }

  async function submitInventoryOperation() {
    const quantity = Number(movementForm.quantity);
    if (operationMode === "reservation") {
      await apiFetch("/api/inventory/reservations", {
        method: "POST",
        body: JSON.stringify({
          repuestoCodigo: movementForm.repuestoCodigo,
          bodegaCodigo: movementForm.bodegaCodigo,
          quantity,
          workOrderId: movementForm.referenceId,
          requestedBy: user?.username ?? user?.id ?? "usuario",
          reason: movementForm.reason
        })
      });
      return;
    }

    if (operationMode === "delivery") {
      await apiFetch("/api/inventory/deliveries", {
        method: "POST",
        body: JSON.stringify({
          repuestoCodigo: movementForm.repuestoCodigo,
          bodegaCodigo: movementForm.bodegaCodigo,
          quantity,
          reason: movementForm.reason,
          workOrderId: movementForm.referenceType === "OT" || !movementForm.referenceType ? emptyToNull(movementForm.referenceId) : null,
          assetCode: movementForm.referenceType === "Activo" ? emptyToNull(movementForm.referenceId) : null,
          faenaCodigo: movementForm.referenceType === "Faena" ? emptyToNull(movementForm.referenceId) : null,
          costCenter: movementForm.referenceType === "CentroCosto" ? emptyToNull(movementForm.referenceId) : null,
          reservationId: movementForm.referenceType === "ReservaOT" ? emptyToNull(movementForm.referenceId) : null
        })
      });
      return;
    }

    if (operationMode === "transfer") {
      await apiFetch("/api/inventory/transfers", {
        method: "POST",
        body: JSON.stringify({
          repuestoCodigo: movementForm.repuestoCodigo,
          sourceWarehouseCode: movementForm.sourceWarehouseCode,
          transitWarehouseCode: movementForm.bodegaCodigo,
          targetWarehouseCode: movementForm.targetWarehouseCode,
          quantity,
          reason: movementForm.reason
        })
      });
      return;
    }

    if (operationMode === "receiveTransfer") {
      await apiFetch(`/api/inventory/transfers/${encodeURIComponent(movementForm.referenceId)}/receive`, {
        method: "POST",
        body: JSON.stringify({ reason: movementForm.reason })
      });
      return;
    }

    if (operationMode === "return") {
      await apiFetch("/api/inventory/returns", {
        method: "POST",
        body: JSON.stringify({
          repuestoCodigo: movementForm.repuestoCodigo,
          bodegaCodigo: movementForm.bodegaCodigo,
          quantity,
          reusable: movementForm.reusableReturn,
          reason: movementForm.reason,
          workOrderId: movementForm.referenceType === "OT" || !movementForm.referenceType ? emptyToNull(movementForm.referenceId) : null,
          assetCode: movementForm.referenceType === "Activo" ? emptyToNull(movementForm.referenceId) : null
        })
      });
      return;
    }

    if (operationMode === "adjustment") {
      await apiFetch("/api/inventory/adjustments", {
        method: "POST",
        body: JSON.stringify({
          repuestoCodigo: movementForm.repuestoCodigo,
          bodegaCodigo: movementForm.bodegaCodigo,
          quantity: movementForm.type === "NegativeAdjustment" ? -Math.abs(quantity) : quantity,
          reason: movementForm.reason,
          allowNegativeException: movementForm.allowNegativeException,
          requiresSupervisorApproval: Boolean(movementForm.supervisorApprovalUserId.trim()),
          supervisorApprovalUserId: emptyToNull(movementForm.supervisorApprovalUserId)
        })
      });
      return;
    }

    if (operationMode === "writeOff") {
      await apiFetch("/api/inventory/write-offs", {
        method: "POST",
        body: JSON.stringify({
          repuestoCodigo: movementForm.repuestoCodigo,
          bodegaCodigo: movementForm.bodegaCodigo,
          quantity,
          reason: movementForm.reason,
          referenceType: emptyToNull(movementForm.referenceType),
          referenceId: emptyToNull(movementForm.referenceId),
          allowNegativeException: movementForm.allowNegativeException
        })
      });
      return;
    }

    await apiFetch("/api/inventory/stock/movements", {
      method: "POST",
      body: JSON.stringify({
        type: movementForm.type,
        repuestoCodigo: movementForm.repuestoCodigo,
        quantity,
        reason: movementForm.reason,
        bodegaCodigo: emptyToNull(movementForm.bodegaCodigo),
        sourceWarehouseCode: emptyToNull(movementForm.sourceWarehouseCode),
        targetWarehouseCode: emptyToNull(movementForm.targetWarehouseCode),
        referenceType: emptyToNull(movementForm.referenceType),
        referenceId: emptyToNull(movementForm.referenceId),
        allowNegativeException: movementForm.allowNegativeException
      })
    });
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Bodega</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Centro de control de stock, bodegas y movimientos.</p>
        </div>
        <button
          className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          onClick={() => void loadAll()}
          type="button"
        >
          <RefreshCw className="h-4 w-4" aria-hidden="true" />
          Actualizar
        </button>
      </div>

      <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Metric label="Bodegas" value={dashboard?.totalBodegas ?? 0} />
        <Metric label="Repuestos" value={dashboard?.totalRepuestos ?? 0} />
        <Metric label="Criticos" value={dashboard?.repuestosCriticos ?? 0} />
        <Metric label="Bajo minimo" value={dashboard?.repuestosBajoMinimo ?? 0} />
        <Metric label="Sin stock" value={dashboard?.criticosSinStock ?? 0} />
      </section>

      <KanbanBoard reservations={reservations} transfers={transfers} />

      <section className="grid gap-4 xl:grid-cols-[1.1fr_0.9fr]">
        <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div className="flex items-center justify-between gap-3">
            <h2 className="text-base font-semibold text-slate-950 dark:text-white">Alertas de stock</h2>
            <AlertTriangle className="h-5 w-5 text-amber-500" aria-hidden="true" />
          </div>
          <div className="mt-4 space-y-2">
            {(dashboard?.alerts ?? []).length === 0 ? (
              <p className="text-sm text-slate-500 dark:text-slate-400">Sin alertas visibles.</p>
            ) : (
              dashboard!.alerts.map((alert) => (
                <div key={alert.alertKey} className="rounded-md border border-slate-200 p-3 text-sm dark:border-slate-800">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-semibold text-slate-900 dark:text-slate-100">{alert.repuestoCodigo}</span>
                    <span className={alert.severity === "Critical" ? "text-red-600 dark:text-red-300" : "text-amber-600 dark:text-amber-300"}>{alert.severity}</span>
                  </div>
                  <p className="mt-1 text-slate-600 dark:text-slate-300">{alert.message}</p>
                </div>
              ))
            )}
          </div>
        </section>

        <form className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900" onSubmit={registerMovement}>
          <div className="flex items-center justify-between gap-3">
            <h2 className="text-base font-semibold text-slate-950 dark:text-white">Operacion</h2>
            <SlidersHorizontal className="h-5 w-5 text-slate-400" aria-hidden="true" />
          </div>
          <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
            {operationModes.map((mode) => (
              <button
                className={`h-10 rounded-md border px-3 text-sm font-semibold transition ${operationMode === mode.id ? "border-teal-700 bg-teal-50 text-teal-800 dark:border-teal-400 dark:bg-teal-950 dark:text-teal-200" : "border-slate-200 text-slate-600 hover:bg-slate-50 dark:border-slate-700 dark:text-slate-300 dark:hover:bg-slate-800"}`}
                key={mode.id}
                onClick={() => {
                  setOperationMode(mode.id);
                  setMovementForm((current) => ({ ...current, type: mode.defaultType }));
                }}
                type="button"
              >
                {mode.label}
              </button>
            ))}
          </div>
          <div className="grid gap-3 md:grid-cols-2">
            {operationMode === "generic" || operationMode === "adjustment" ? (
              <Select label="Tipo" value={movementForm.type} options={operationMode === "adjustment" ? adjustmentTypes : movementTypes} onChange={(value) => setMovementForm({ ...movementForm, type: value as StockMovementType })} />
            ) : null}
            <Select label="Repuesto" value={movementForm.repuestoCodigo} options={spareParts.map((item) => item.codigo)} onChange={(value) => setMovementForm({ ...movementForm, repuestoCodigo: value })} />
            {operationMode === "transfer" ? (
              <>
                <Select label="Origen" value={movementForm.sourceWarehouseCode} options={["", ...warehouses.map((item) => item.codigo)]} onChange={(value) => setMovementForm({ ...movementForm, sourceWarehouseCode: value })} />
                <Select label="Transito" value={movementForm.bodegaCodigo} options={["", ...transitWarehouses.map((item) => item.codigo)]} onChange={(value) => setMovementForm({ ...movementForm, bodegaCodigo: value })} />
                <Select label="Destino" value={movementForm.targetWarehouseCode} options={["", ...warehouses.map((item) => item.codigo)]} onChange={(value) => setMovementForm({ ...movementForm, targetWarehouseCode: value })} />
              </>
            ) : operationMode === "receiveTransfer" ? (
              <Select label="Transferencia" value={movementForm.referenceId} options={["", ...openTransfers.map((item) => item.transferenciaId)]} onChange={(value) => setMovementForm({ ...movementForm, referenceId: value })} />
            ) : (
              <Select label="Bodega" value={movementForm.bodegaCodigo} options={["", ...warehouses.map((item) => item.codigo)]} onChange={(value) => setMovementForm({ ...movementForm, bodegaCodigo: value })} />
            )}
            {operationMode !== "receiveTransfer" ? <Field label="Cantidad" type="number" value={movementForm.quantity} onChange={(value) => setMovementForm({ ...movementForm, quantity: value })} /> : null}
            {operationMode === "reservation" ? (
              <Field label="OT" value={movementForm.referenceId} onChange={(value) => setMovementForm({ ...movementForm, referenceId: value })} />
            ) : operationMode === "delivery" || operationMode === "return" ? (
              <>
                <Select label="Referencia" value={movementForm.referenceType || "OT"} options={operationMode === "delivery" ? deliveryReferenceTypes : returnReferenceTypes} onChange={(value) => setMovementForm({ ...movementForm, referenceType: value })} />
                <Field label="Codigo ref." value={movementForm.referenceId} onChange={(value) => setMovementForm({ ...movementForm, referenceId: value })} />
              </>
            ) : operationMode === "writeOff" || operationMode === "generic" ? (
              <>
                <Field label="Referencia" value={movementForm.referenceId} onChange={(value) => setMovementForm({ ...movementForm, referenceId: value })} />
                <Field label="Tipo ref." value={movementForm.referenceType} onChange={(value) => setMovementForm({ ...movementForm, referenceType: value })} />
              </>
            ) : null}
            {operationMode === "adjustment" ? (
              <Field label="Aprobador" value={movementForm.supervisorApprovalUserId} onChange={(value) => setMovementForm({ ...movementForm, supervisorApprovalUserId: value })} />
            ) : null}
          </div>
          <Field label="Motivo" value={movementForm.reason} onChange={(value) => setMovementForm({ ...movementForm, reason: value })} />
          <div className="grid gap-2 md:grid-cols-2">
            {operationMode === "return" ? <CheckField label="Reutilizable" checked={movementForm.reusableReturn} onChange={(value) => setMovementForm({ ...movementForm, reusableReturn: value })} /> : null}
            {operationMode === "adjustment" || operationMode === "writeOff" || operationMode === "generic" ? (
              <CheckField label="Excepcion negativa auditada" checked={movementForm.allowNegativeException} onChange={(value) => setMovementForm({ ...movementForm, allowNegativeException: value })} />
            ) : null}
          </div>
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
            disabled={!canAdjustStock || isSaving}
            type="submit"
          >
            {operationIcon(operationMode)}
            Guardar
          </button>
        </form>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
          <Select label="Bodega" value={filters.bodegaCodigo} options={["", ...warehouses.map((item) => item.codigo)]} onChange={(value) => setFilters({ ...filters, bodegaCodigo: value })} />
          <FaenaSelect value={filters.faenaCodigo} onChange={(value) => setFilters({ ...filters, faenaCodigo: value })} />
          <CheckField label="Bajo minimo" checked={filters.lowStockOnly} onChange={(value) => setFilters({ ...filters, lowStockOnly: value })} />
          <CheckField label="Criticos" checked={filters.criticalOnly} onChange={(value) => setFilters({ ...filters, criticalOnly: value })} />
        </div>
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Stock por bodega</h2>
          <span className="text-sm text-slate-500 dark:text-slate-400">{filteredStock.length}</span>
        </div>
        {isLoading ? <p className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando stock...</p> : <StockTable rows={filteredStock} />}
      </section>

      <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Historial de movimientos</h2>
          <span className="text-sm text-slate-500 dark:text-slate-400">{movements.length}</span>
        </div>
        <MovementTable rows={movements} />
      </section>

      <section className="grid gap-4 xl:grid-cols-[1fr_0.8fr]">
        <WarehouseList warehouses={warehouses} />
        <form className="space-y-3 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900" onSubmit={createWarehouse}>
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Nueva bodega</h2>
          <div className="grid gap-3 md:grid-cols-2">
            <Field label="Codigo" value={warehouseForm.codigo} onChange={(value) => setWarehouseForm({ ...warehouseForm, codigo: value })} />
            <Field label="Nombre" value={warehouseForm.nombre} onChange={(value) => setWarehouseForm({ ...warehouseForm, nombre: value })} />
            <FaenaSelect emptyLabel="Selecciona faena" value={warehouseForm.faenaCodigo} onChange={(value) => setWarehouseForm({ ...warehouseForm, faenaCodigo: value })} />
            <Select label="Tipo" value={warehouseForm.tipo} options={["Central", "Taller", "Faena", "Transito"]} onChange={(value) => setWarehouseForm({ ...warehouseForm, tipo: value as WarehouseType })} />
            <Field label="Ubicacion" value={warehouseForm.ubicacion} onChange={(value) => setWarehouseForm({ ...warehouseForm, ubicacion: value })} />
            <Field label="Responsable" value={warehouseForm.responsable} onChange={(value) => setWarehouseForm({ ...warehouseForm, responsable: value })} />
          </div>
          <Field label="Ubicaciones internas" value={warehouseForm.ubicacionesInternas} onChange={(value) => setWarehouseForm({ ...warehouseForm, ubicacionesInternas: value })} />
          <CheckField label="Permite negativo excepcional" checked={warehouseForm.permiteStockNegativo} onChange={(value) => setWarehouseForm({ ...warehouseForm, permiteStockNegativo: value })} />
          <button className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800" disabled={isSaving} type="submit">
            <Plus className="h-4 w-4" aria-hidden="true" />
            Crear bodega
          </button>
        </form>
      </section>

      {message ? <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">{message}</div> : null}
      {error ? <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">{error}</div> : null}
    </section>
  );
}

const movementTypes: StockMovementType[] = [
  "Reception",
  "MaintenanceConsumption",
  "Reservation",
  "ReservationRelease",
  "TransferOut",
  "TransferIn",
  "Adjustment",
  "CountCorrection",
  "ReturnFromWorkOrder",
  "PositiveAdjustment",
  "NegativeAdjustment",
  "MaterialWriteOff",
  "InTransit",
  "TransferReception"
];

const adjustmentTypes: StockMovementType[] = ["PositiveAdjustment", "NegativeAdjustment", "CountCorrection"];
const deliveryReferenceTypes = ["OT", "Activo", "Faena", "CentroCosto", "ReservaOT"];
const returnReferenceTypes = ["OT", "Activo"];

const operationModes: { id: OperationMode; label: string; defaultType: StockMovementType }[] = [
  { id: "reservation", label: "Reservar", defaultType: "Reservation" },
  { id: "delivery", label: "Entregar", defaultType: "MaintenanceConsumption" },
  { id: "transfer", label: "Transferir", defaultType: "TransferOut" },
  { id: "receiveTransfer", label: "Recepcionar", defaultType: "TransferReception" },
  { id: "return", label: "Devolver", defaultType: "ReturnFromWorkOrder" },
  { id: "adjustment", label: "Ajustar", defaultType: "PositiveAdjustment" },
  { id: "writeOff", label: "Dar baja", defaultType: "MaterialWriteOff" },
  { id: "generic", label: "Manual", defaultType: "Reception" }
];

function operationIcon(mode: OperationMode) {
  const className = "h-4 w-4";
  if (mode === "reservation") {
    return <ClipboardList className={className} aria-hidden="true" />;
  }
  if (mode === "delivery") {
    return <PackageCheck className={className} aria-hidden="true" />;
  }
  if (mode === "transfer" || mode === "receiveTransfer") {
    return <Truck className={className} aria-hidden="true" />;
  }
  if (mode === "return") {
    return <Undo2 className={className} aria-hidden="true" />;
  }
  if (mode === "adjustment") {
    return <SlidersHorizontal className={className} aria-hidden="true" />;
  }
  return <ArrowRightLeft className={className} aria-hidden="true" />;
}

function KanbanBoard({ reservations, transfers }: { reservations: StockReservation[]; transfers: StockTransfer[] }) {
  const activeReservations = reservations.filter((item) => item.estado === "Activa" || item.estado === "ParcialmenteEntregada");
  const inTransit = transfers.filter((item) => item.estado === "EnTransito");
  const received = transfers.filter((item) => item.estado === "Recibida").slice(0, 8);

  return (
    <section className="grid gap-3 lg:grid-cols-3">
      <KanbanColumn title="Reservas" count={activeReservations.length}>
        {activeReservations.map((item) => (
          <div key={item.reservaId} className="rounded-md border border-slate-200 p-3 text-sm dark:border-slate-800">
            <div className="flex items-center justify-between gap-2">
              <span className="font-semibold text-slate-900 dark:text-slate-100">{item.reservaId}</span>
              <span className="text-xs text-slate-500 dark:text-slate-400">{formatNumber(item.cantidadPendiente)}</span>
            </div>
            <p className="mt-1 text-slate-600 dark:text-slate-300">{item.repuestoCodigo} · {item.bodegaCodigo}</p>
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{item.workOrderId}</p>
          </div>
        ))}
      </KanbanColumn>
      <KanbanColumn title="En transito" count={inTransit.length}>
        {inTransit.map((item) => (
          <div key={item.transferenciaId} className="rounded-md border border-slate-200 p-3 text-sm dark:border-slate-800">
            <div className="flex items-center justify-between gap-2">
              <span className="font-semibold text-slate-900 dark:text-slate-100">{item.transferenciaId}</span>
              <Truck className="h-4 w-4 text-amber-500" aria-hidden="true" />
            </div>
            <p className="mt-1 text-slate-600 dark:text-slate-300">{item.bodegaOrigenCodigo} {"->"} {item.bodegaDestinoCodigo}</p>
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{item.repuestoCodigo} · {formatNumber(item.cantidad)}</p>
          </div>
        ))}
      </KanbanColumn>
      <KanbanColumn title="Recepcionadas" count={received.length}>
        {received.map((item) => (
          <div key={item.transferenciaId} className="rounded-md border border-slate-200 p-3 text-sm dark:border-slate-800">
            <div className="flex items-center justify-between gap-2">
              <span className="font-semibold text-slate-900 dark:text-slate-100">{item.transferenciaId}</span>
              <CheckCircle2 className="h-4 w-4 text-emerald-500" aria-hidden="true" />
            </div>
            <p className="mt-1 text-slate-600 dark:text-slate-300">{item.bodegaDestinoCodigo}</p>
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{formatDate(item.fechaRecepcionUtc ?? item.fechaSolicitudUtc)}</p>
          </div>
        ))}
      </KanbanColumn>
    </section>
  );
}

function KanbanColumn({ title, count, children }: { title: string; count: number; children: ReactNode }) {
  return (
    <section className="min-h-40 rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">{title}</h2>
        <span className="text-sm text-slate-500 dark:text-slate-400">{count}</span>
      </div>
      <div className="space-y-2 p-3">
        {count === 0 ? <p className="text-sm text-slate-500 dark:text-slate-400">Sin registros.</p> : children}
      </div>
    </section>
  );
}

function Metric({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm font-medium text-slate-500 dark:text-slate-400">{label}</p>
        <Boxes className="h-5 w-5 text-slate-400" aria-hidden="true" />
      </div>
      <p className="mt-3 text-2xl font-semibold text-slate-950 dark:text-white">{formatNumber(value)}</p>
    </div>
  );
}

function StockTable({ rows }: { rows: StockItem[] }) {
  return (
    <div className="max-h-[620px] overflow-auto">
      <table className="min-w-full text-left text-sm">
        <thead className="sticky top-0 bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
          <tr>
            <th className="px-4 py-3 font-medium">Bodega</th>
            <th className="px-4 py-3 font-medium">Repuesto</th>
            <th className="px-4 py-3 font-medium">Fisico</th>
            <th className="px-4 py-3 font-medium">Reservado</th>
            <th className="px-4 py-3 font-medium">Disponible</th>
            <th className="px-4 py-3 font-medium">Min.</th>
            <th className="px-4 py-3 font-medium">Estado</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {rows.map((row) => (
            <tr key={`${row.bodegaCodigo}-${row.repuestoCodigo}`}>
              <td className="px-4 py-3 font-semibold text-slate-900 dark:text-slate-100">{row.bodegaCodigo}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">
                <span className="font-semibold text-slate-900 dark:text-slate-100">{row.repuestoCodigo}</span>
                <p className="mt-1 max-w-md truncate text-xs text-slate-500 dark:text-slate-400">{row.repuestoDescripcion}</p>
              </td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{formatNumber(row.stockFisico)}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{formatNumber(row.stockReservado)}</td>
              <td className="px-4 py-3 font-semibold text-slate-900 dark:text-slate-100">{formatNumber(row.stockDisponible)}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{formatNumber(row.stockMinimo)}</td>
              <td className="px-4 py-3"><StockBadge item={row} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function MovementTable({ rows }: { rows: StockMovement[] }) {
  if (rows.length === 0) {
    return <p className="p-4 text-sm text-slate-500 dark:text-slate-400">Sin movimientos.</p>;
  }

  return (
    <div className="max-h-[420px] overflow-auto">
      <table className="min-w-full text-left text-sm">
        <thead className="sticky top-0 bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
          <tr>
            <th className="px-4 py-3 font-medium">Fecha</th>
            <th className="px-4 py-3 font-medium">Tipo</th>
            <th className="px-4 py-3 font-medium">Repuesto</th>
            <th className="px-4 py-3 font-medium">Bodega</th>
            <th className="px-4 py-3 font-medium">Cantidad</th>
            <th className="px-4 py-3 font-medium">Referencia</th>
            <th className="px-4 py-3 font-medium">Motivo</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {rows.map((row) => (
            <tr key={row.movimientoId}>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{formatDate(row.fechaUtc)}</td>
              <td className="px-4 py-3 font-semibold text-slate-900 dark:text-slate-100">{row.type}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{row.repuestoCodigo}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{row.bodegaCodigo ?? row.bodegaOrigenCodigo ?? "-"}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{formatNumber(row.quantity)}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{row.referenceId ?? row.referenceType ?? "-"}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{row.motivo}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function WarehouseList({ warehouses }: { warehouses: WarehouseRecord[] }) {
  return (
    <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Bodegas</h2>
        <Warehouse className="h-5 w-5 text-slate-400" aria-hidden="true" />
      </div>
      <div className="max-h-[520px] overflow-auto">
        {warehouses.map((warehouse) => (
          <div key={warehouse.codigo} className="border-b border-slate-100 px-4 py-3 text-sm dark:border-slate-800">
            <div className="flex items-center justify-between gap-3">
              <span className="font-semibold text-slate-900 dark:text-slate-100">{warehouse.codigo}</span>
              <span className="text-xs text-slate-500 dark:text-slate-400">{warehouse.tipo}</span>
            </div>
            <p className="mt-1 text-slate-600 dark:text-slate-300">{warehouse.nombre}</p>
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{warehouse.faenaCodigo} · {warehouse.ubicacionesInternas.join(", ") || warehouse.ubicacion || "-"}</p>
          </div>
        ))}
      </div>
    </section>
  );
}

function StockBadge({ item }: { item: StockItem }) {
  const text = item.criticoSinStock ? "Critico sin stock" : item.bajoMinimo ? "Bajo minimo" : "OK";
  const className = item.criticoSinStock
    ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
    : item.bajoMinimo
      ? "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200"
      : "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200";
  return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${className}`}>{text}</span>;
}

function Field({ label, value, onChange, type = "text" }: { label: string; value: string; onChange: (value: string) => void; type?: string }) {
  const id = `inventory-${label.toLowerCase().replace(/\s+/g, "-")}`;
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <input id={id} className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950" type={type} value={value} onChange={(event) => onChange(event.target.value)} />
    </label>
  );
}

function Select({ label, value, options, onChange }: { label: string; value: string; options: readonly string[]; onChange: (value: string) => void }) {
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
      {label}
      <select className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950" value={value} onChange={(event) => onChange(event.target.value)}>
        {options.map((option) => <option key={option || "empty"} value={option}>{option || "Todos"}</option>)}
      </select>
    </label>
  );
}

function CheckField({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="flex min-h-10 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
      <input checked={checked} onChange={(event) => onChange(event.target.checked)} type="checkbox" />
      {label}
    </label>
  );
}

function parseList(value: string) {
  return value.split(/[;,]/).map((item) => item.trim()).filter(Boolean);
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function formatNumber(value: number) {
  return new Intl.NumberFormat("es-CL", { maximumFractionDigits: 2 }).format(value);
}

function formatDate(value?: string | null) {
  if (!value) {
    return "-";
  }

  return new Intl.DateTimeFormat("es-CL", {
    dateStyle: "short",
    timeStyle: "short"
  }).format(new Date(value));
}
