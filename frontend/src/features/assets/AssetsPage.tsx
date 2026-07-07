import { FormEvent, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  FileText,
  History,
  Package,
  RefreshCw,
  Save,
  Wrench,
  X
} from "lucide-react";
import { AUTH_PERMISSIONS, apiFetch, useAuthStore } from "../auth/authStore";
import { FaenaSelect, type FaenaRecord } from "../faenas/FaenaSelect";

type AssetStatus = "Draft" | "Active" | "InMaintenance" | "Unavailable" | "Retired";

type AssetCompleteness = {
  requiredFields: number;
  completedFields: number;
  percentage: number;
  state: string;
  missingFields: string[];
};

type AssetSummary = {
  codigo: string;
  nombre: string;
  faenaCodigo: string;
  tipoActivo: string;
  estado: AssetStatus;
  ubicacionTecnicaCodigo?: string | null;
  familia?: string | null;
  marca?: string | null;
  modelo?: string | null;
  patente?: string | null;
  numeroSerie?: string | null;
  propiedad?: string | null;
  criticidad?: string | null;
  estadoDocumental: string;
  estadoOperacional: string;
  completitudFicha: AssetCompleteness;
  disponibleDocumentalmente: boolean;
  fichaValidada: boolean;
};

type AssetDetail = AssetSummary & {
  fechaAlta?: string | null;
  fechaActualizacion?: string | null;
  technicalFields: Record<string, string | null>;
  workOrders: AssetWorkOrder[];
  repuestosCompatibles: CompatibleSparePart[];
};

type AssetWorkOrder = {
  numeroOT: string;
  estado: string;
  tipoMantenimiento: string;
  descripcion?: string | null;
  fechaProgramada?: string | null;
};

type CompatibleSparePart = {
  codigo: string;
  descripcion: string;
  familia?: string | null;
  unidadMedida?: string | null;
};

type AssetDocument = {
  tipoDocumento: string;
  estado: string;
  fechaVencimiento?: string | null;
  archivoKey?: string | null;
  critico: boolean;
  vencido: boolean;
  bloqueaDisponibilidad: boolean;
};

type AssetCostSummary = {
  activoCodigo: string;
  total: number;
  currency: string;
  items: AssetCostLine[];
};

type AssetCostLine = {
  source: string;
  tipoCosto: string;
  amount: number;
  currency: string;
  reference?: string | null;
};

type AssetAvailability = {
  activoCodigo: string;
  disponible: boolean;
  disponibleOperacionalmente: boolean;
  disponibleDocumentalmente: boolean;
  estadoOperacional: string;
  estadoDocumental: string;
  bloqueos: string[];
  porcentajeDisponibilidad: number;
};

type AssetHistoryEntry = {
  id: string;
  occurredAtUtc: string;
  action: string;
  source: string;
  userId: string;
  previousValue?: string | null;
  newValue?: string | null;
  detail?: string | null;
};

type AssetFormState = {
  codigo: string;
  nombre: string;
  faenaCodigo: string;
  tipoActivo: string;
  estado: AssetStatus;
  ubicacionTecnicaCodigo: string;
  familia: string;
  marca: string;
  modelo: string;
  patente: string;
  numeroSerie: string;
  propiedad: string;
  criticidad: string;
  estadoDocumental: string;
  estadoOperacional: string;
  technicalFieldsText: string;
  fichaValidada: boolean;
};

type Filters = {
  faenaCodigo: string;
  estado: string;
  familia: string;
  criticidad: string;
};

const emptyFilters: Filters = {
  faenaCodigo: "",
  estado: "",
  familia: "",
  criticidad: ""
};

const emptyForm: AssetFormState = {
  codigo: "",
  nombre: "",
  faenaCodigo: "",
  tipoActivo: "",
  estado: "Active",
  ubicacionTecnicaCodigo: "",
  familia: "",
  marca: "",
  modelo: "",
  patente: "",
  numeroSerie: "",
  propiedad: "Propio",
  criticidad: "Media",
  estadoDocumental: "Pendiente",
  estadoOperacional: "Operativo",
  technicalFieldsText: "",
  fichaValidada: false
};

const statusOptions: AssetStatus[] = ["Draft", "Active", "InMaintenance", "Unavailable", "Retired"];
const tabs = ["General", "Ficha tecnica", "Documentos", "Repuestos", "OT", "Costos", "Disponibilidad", "Historial"] as const;
type AssetTab = (typeof tabs)[number];

export function AssetsPage() {
  const currentUser = useAuthStore((state) => state.user);
  const canViewCosts = Boolean(currentUser?.permissions.includes(AUTH_PERMISSIONS.viewCosts));
  const [assets, setAssets] = useState<AssetSummary[]>([]);
  const [selected, setSelected] = useState<AssetDetail | null>(null);
  const [documents, setDocuments] = useState<AssetDocument[]>([]);
  const [costs, setCosts] = useState<AssetCostSummary | null>(null);
  const [availability, setAvailability] = useState<AssetAvailability | null>(null);
  const [history, setHistory] = useState<AssetHistoryEntry[]>([]);
  const [filters, setFilters] = useState<Filters>(emptyFilters);
  const [form, setForm] = useState<AssetFormState>(emptyForm);
  const [activeTab, setActiveTab] = useState<AssetTab>("General");
  const [stateReason, setStateReason] = useState("");
  const [isCreating, setIsCreating] = useState(false);
  const [isAssetModalOpen, setIsAssetModalOpen] = useState(false);
  const [isEditingAsset, setIsEditingAsset] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    void loadAssets();
  }, []);

  const familias = useMemo(() => uniqueOptions(assets.map((asset) => asset.familia ?? "")), [assets]);
  const criticidades = useMemo(() => uniqueOptions(assets.map((asset) => asset.criticidad ?? "")), [assets]);

  async function loadAssets(nextFilters = filters, preferredCode?: string) {
    setIsLoading(true);
    setError(null);

    try {
      const query = new URLSearchParams();
      if (nextFilters.faenaCodigo) {
        query.set("faenaCodigo", nextFilters.faenaCodigo);
      }
      if (nextFilters.estado) {
        query.set("estado", nextFilters.estado);
      }
      if (nextFilters.familia) {
        query.set("familia", nextFilters.familia);
      }
      if (nextFilters.criticidad) {
        query.set("criticidad", nextFilters.criticidad);
      }

      const data = await apiFetch<AssetSummary[]>(`/api/assets?${query.toString()}`);
      setAssets(data);
      if (preferredCode) {
        await loadAsset(preferredCode);
      } else if (selected && !data.some((asset) => asset.codigo === selected.codigo)) {
        clearSelection();
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar activos.");
    } finally {
      setIsLoading(false);
    }
  }

  async function loadAsset(codigo: string) {
    setError(null);
    try {
      const detail = await apiFetch<AssetDetail>(`/api/assets/${encodeURIComponent(codigo)}`);
      setSelected(detail);
      setForm(toForm(detail));
      setIsCreating(false);
      setIsEditingAsset(false);
      setIsAssetModalOpen(true);

      const [documentsResult, availabilityResult, historyResult] = await Promise.all([
        apiFetch<AssetDocument[]>(`/api/assets/${encodeURIComponent(codigo)}/documents`),
        apiFetch<AssetAvailability>(`/api/assets/${encodeURIComponent(codigo)}/availability`),
        apiFetch<AssetHistoryEntry[]>(`/api/assets/${encodeURIComponent(codigo)}/history`)
      ]);

      setDocuments(documentsResult);
      setAvailability(availabilityResult);
      setHistory(historyResult);

      if (canViewCosts) {
        setCosts(await apiFetch<AssetCostSummary>(`/api/assets/${encodeURIComponent(codigo)}/costs`));
      } else {
        setCosts(null);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar la ficha del activo.");
    }
  }

  function clearSelection() {
    setSelected(null);
    setDocuments([]);
    setAvailability(null);
    setCosts(null);
    setHistory([]);
    setForm(emptyForm);
    setIsAssetModalOpen(false);
    setIsEditingAsset(false);
  }

  function updateFormFaena(faenaCodigo: string, faena?: FaenaRecord) {
    setForm((current) => ({
      ...current,
      faenaCodigo,
      ubicacionTecnicaCodigo: faenaCodigo ? faena?.ubicacionTecnica ?? "" : ""
    }));
  }

  function startCreate() {
    setIsCreating(true);
    setIsEditingAsset(true);
    setIsAssetModalOpen(true);
    setSelected(null);
    setDocuments([]);
    setCosts(null);
    setAvailability(null);
    setHistory([]);
    setActiveTab("General");
    setForm(emptyForm);
    setMessage(null);
    setError(null);
  }

  function closeAssetModal() {
    setIsAssetModalOpen(false);
    setIsCreating(false);
    setIsEditingAsset(false);
    setStateReason("");
    setMessage(null);
    setError(null);
    if (!selected) {
      setForm(emptyForm);
    }
  }

  function startEditAsset() {
    if (selected) {
      setForm(toForm(selected));
    }

    setIsEditingAsset(true);
    setMessage(null);
    setError(null);
  }

  function cancelEditAsset() {
    setIsEditingAsset(false);
    setMessage(null);
    setError(null);
    if (selected) {
      setForm(toForm(selected));
    }
  }

  function applyFilters(nextFilters: Filters) {
    setFilters(nextFilters);
    void loadAssets(nextFilters);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);

    const payload = toPayload(form);
    try {
      const saved = isCreating
        ? await apiFetch<AssetDetail>("/api/assets", {
            method: "POST",
            body: JSON.stringify(payload)
          })
        : await apiFetch<AssetDetail>(`/api/assets/${encodeURIComponent(form.codigo)}`, {
            method: "PUT",
            body: JSON.stringify(payload)
          });

      setMessage(isCreating ? "Activo creado." : "Activo actualizado.");
      setIsCreating(false);
      setIsEditingAsset(false);
      await loadAssets(filters, saved.codigo);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible guardar el activo.");
    } finally {
      setIsSaving(false);
    }
  }

  async function createStateEvent() {
    if (!selected || !stateReason.trim()) {
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      await apiFetch(`/api/assets/${encodeURIComponent(selected.codigo)}/state-events`, {
        method: "POST",
        body: JSON.stringify({
          status: form.estado,
          reason: stateReason
        })
      });
      setStateReason("");
      setMessage("Estado actualizado.");
      await loadAssets(filters, selected.codigo);
    } catch (stateError) {
      setError(stateError instanceof Error ? stateError.message : "No fue posible cambiar el estado.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Activos</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Ficha tecnica, disponibilidad y trazabilidad.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            onClick={() => void loadAssets(filters)}
            type="button"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Actualizar
          </button>
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
            onClick={startCreate}
            type="button"
          >
            Nuevo activo
          </button>
        </div>
      </div>

      <div className="space-y-4">
          <AssetFilters
            filters={filters}
            familias={familias}
            criticidades={criticidades}
            onChange={applyFilters}
          />

          <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
              <h2 className="text-base font-semibold text-slate-950 dark:text-white">Listado</h2>
              <span className="text-sm text-slate-500 dark:text-slate-400">{assets.length} activos</span>
            </div>

            {isLoading ? (
              <div className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando activos...</div>
            ) : (
              <div className="max-h-[620px] overflow-auto">
                <table className="min-w-full text-left text-sm">
                  <thead className="sticky top-0 bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
                    <tr>
                      <th className="px-4 py-3 font-medium">Activo</th>
                      <th className="px-4 py-3 font-medium">Estado</th>
                      <th className="px-4 py-3 font-medium">Ficha</th>
                      <th className="px-4 py-3 font-medium">Criticidad</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {assets.map((asset) => (
                      <tr
                        key={asset.codigo}
                        className={`cursor-pointer transition hover:bg-slate-50 dark:hover:bg-slate-800 ${
                          selected?.codigo === asset.codigo ? "bg-teal-50/70 dark:bg-teal-950/30" : ""
                        }`}
                        onClick={() => void loadAsset(asset.codigo)}
                      >
                        <td className="px-4 py-3">
                          <div className="font-semibold text-slate-900 dark:text-slate-100">{asset.codigo}</div>
                          <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">{asset.nombre}</div>
                          <div className="mt-1 text-xs text-slate-500 dark:text-slate-400">{asset.faenaCodigo}</div>
                        </td>
                        <td className="px-4 py-3">
                          <StatusBadge status={asset.estado} documental={asset.disponibleDocumentalmente} />
                        </td>
                        <td className="px-4 py-3">
                          <CompletenessBadge completeness={asset.completitudFicha} />
                        </td>
                        <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{asset.criticidad ?? "-"}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
      </div>

      {isAssetModalOpen ? (
        <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto bg-slate-950/55 p-3 backdrop-blur-sm sm:p-6">
          <div className="w-full max-w-7xl space-y-4 rounded-lg bg-slate-50 p-3 shadow-2xl dark:bg-slate-950 sm:p-4">
            <div className="flex items-center justify-between gap-3">
              <div>
                <h2 className="text-lg font-semibold text-slate-950 dark:text-white">
                  {isCreating ? "Nuevo activo" : selected ? selected.nombre : "Ficha del activo"}
                </h2>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                  {selected ? `${selected.codigo} / ${selected.faenaCodigo}` : "Completa la ficha para crear el activo."}
                </p>
              </div>
              <div className="flex items-center gap-2">
                {!isCreating && !isEditingAsset ? (
                  <button
                    className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
                    onClick={startEditAsset}
                    type="button"
                  >
                    Editar activo
                  </button>
                ) : !isCreating ? (
                  <button
                    className="inline-flex h-10 items-center rounded-md border border-slate-200 bg-white px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
                    onClick={cancelEditAsset}
                    type="button"
                  >
                    Cancelar edicion
                  </button>
                ) : null}
                <button
                  aria-label="Cerrar ficha de activo"
                  className="inline-flex h-10 w-10 items-center justify-center rounded-md border border-slate-200 bg-white text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200 dark:hover:bg-slate-800"
                  onClick={closeAssetModal}
                  title="Cerrar"
                  type="button"
                >
                  <X className="h-5 w-5" aria-hidden="true" />
                </button>
              </div>
            </div>
          {isCreating || isEditingAsset ? (
          <form
            className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900"
            onSubmit={(event) => void handleSubmit(event)}
          >
            <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
              <div>
                <h2 className="text-base font-semibold text-slate-950 dark:text-white">
                  {isCreating ? "Nuevo activo" : selected ? `${selected.codigo} · ${selected.nombre}` : "Ficha del activo"}
                </h2>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
                  {selected ? `${selected.faenaCodigo} / ${selected.tipoActivo}` : "Selecciona un activo o crea uno nuevo."}
                </p>
              </div>
              {selected ? <CompletenessBadge completeness={selected.completitudFicha} /> : null}
            </div>

            <div className="mt-4 grid gap-3 md:grid-cols-2 xl:grid-cols-4">
              <Field
                disabled={!isCreating}
                label="Codigo"
                value={form.codigo}
                onChange={(value) => setForm({ ...form, codigo: value })}
              />
              <Field label="Nombre" value={form.nombre} onChange={(value) => setForm({ ...form, nombre: value })} />
              <FaenaSelect emptyLabel="Selecciona faena" value={form.faenaCodigo} onChange={updateFormFaena} />
              <Field label="Tipo activo" value={form.tipoActivo} onChange={(value) => setForm({ ...form, tipoActivo: value })} />
              <SelectField
                label="Estado"
                value={form.estado}
                options={statusOptions}
                onChange={(value) => setForm({ ...form, estado: value as AssetStatus })}
              />
              <Field
                label="Ubicacion tecnica"
                value={form.ubicacionTecnicaCodigo}
                onChange={(value) => setForm({ ...form, ubicacionTecnicaCodigo: value })}
              />
              <Field label="Familia" value={form.familia} onChange={(value) => setForm({ ...form, familia: value })} />
              <Field label="Marca" value={form.marca} onChange={(value) => setForm({ ...form, marca: value })} />
              <Field label="Modelo" value={form.modelo} onChange={(value) => setForm({ ...form, modelo: value })} />
              <Field label="Patente" value={form.patente} onChange={(value) => setForm({ ...form, patente: value })} />
              <Field label="Serie" value={form.numeroSerie} onChange={(value) => setForm({ ...form, numeroSerie: value })} />
              <SelectField
                label="Propiedad"
                value={form.propiedad}
                options={["Propio", "Arriendo", "Tercero"]}
                onChange={(value) => setForm({ ...form, propiedad: value })}
              />
              <SelectField
                label="Criticidad"
                value={form.criticidad}
                options={["Alta", "Media", "Baja"]}
                onChange={(value) => setForm({ ...form, criticidad: value })}
              />
              <Field
                label="Estado documental"
                value={form.estadoDocumental}
                onChange={(value) => setForm({ ...form, estadoDocumental: value })}
              />
              <Field
                label="Estado operacional"
                value={form.estadoOperacional}
                onChange={(value) => setForm({ ...form, estadoOperacional: value })}
              />
              <label className="flex min-h-10 items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
                <input
                  checked={form.fichaValidada}
                  onChange={(event) => setForm({ ...form, fichaValidada: event.target.checked })}
                  type="checkbox"
                />
                Ficha validada
              </label>
            </div>

            <label className="mt-4 block text-sm font-medium text-slate-700 dark:text-slate-200">
              Campos tecnicos adicionales
              <textarea
                className="mt-2 min-h-24 w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
                value={form.technicalFieldsText}
                onChange={(event) => setForm({ ...form, technicalFieldsText: event.target.value })}
              />
            </label>

            <div className="mt-4 flex flex-wrap items-center gap-3">
              {selected ? (
                <>
                  <input
                    className="h-10 min-w-72 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
                    placeholder="Motivo cambio de estado"
                    value={stateReason}
                    onChange={(event) => setStateReason(event.target.value)}
                  />
                  <button
                    className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    disabled={isSaving || !stateReason.trim()}
                    onClick={() => void createStateEvent()}
                    type="button"
                  >
                    <Clock className="h-4 w-4" aria-hidden="true" />
                    Registrar estado
                  </button>
                </>
              ) : null}
              {message ? <span className="text-sm text-emerald-700 dark:text-emerald-300">{message}</span> : null}
              {error ? <span className="text-sm text-red-700 dark:text-red-300">{error}</span> : null}
            </div>

            <div className="mt-5 flex justify-end">
              <button
                className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
                disabled={isSaving || (!isCreating && !selected)}
                type="submit"
              >
                <Save className="h-4 w-4" aria-hidden="true" />
                Guardar
              </button>
            </div>
          </form>
          ) : null}

          {!isCreating ? (
          <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="overflow-x-auto border-b border-slate-200 dark:border-slate-800">
              <div className="flex min-w-max gap-1 px-3 py-2">
                {tabs.map((tab) => (
                  <button
                    key={tab}
                    className={`h-9 rounded-md px-3 text-sm font-semibold transition ${
                      activeTab === tab
                        ? "bg-slate-900 text-white dark:bg-white dark:text-slate-950"
                        : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
                    }`}
                    onClick={() => setActiveTab(tab)}
                    type="button"
                  >
                    {tab}
                  </button>
                ))}
              </div>
            </div>
            <div className="p-4">
              <AssetTabContent
                tab={activeTab}
                asset={selected}
                documents={documents}
                costs={costs}
                availability={availability}
                history={history}
                canViewCosts={canViewCosts}
              />
            </div>
          </section>
          ) : null}
          </div>
        </div>
      ) : null}
    </section>
  );
}

function AssetFilters({
  filters,
  familias,
  criticidades,
  onChange
}: {
  filters: Filters;
  familias: string[];
  criticidades: string[];
  onChange: (filters: Filters) => void;
}) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
        <FaenaSelect value={filters.faenaCodigo} onChange={(value) => onChange({ ...filters, faenaCodigo: value })} />
        <FilterSelect label="Estado" value={filters.estado} options={statusOptions} onChange={(value) => onChange({ ...filters, estado: value })} />
        <FilterSelect label="Familia" value={filters.familia} options={familias} onChange={(value) => onChange({ ...filters, familia: value })} />
        <FilterSelect
          label="Criticidad"
          value={filters.criticidad}
          options={criticidades}
          onChange={(value) => onChange({ ...filters, criticidad: value })}
        />
      </div>
    </section>
  );
}

function AssetTabContent({
  tab,
  asset,
  documents,
  costs,
  availability,
  history,
  canViewCosts
}: {
  tab: AssetTab;
  asset: AssetDetail | null;
  documents: AssetDocument[];
  costs: AssetCostSummary | null;
  availability: AssetAvailability | null;
  history: AssetHistoryEntry[];
  canViewCosts: boolean;
}) {
  if (!asset) {
    return <p className="text-sm text-slate-500 dark:text-slate-400">Sin activo seleccionado.</p>;
  }

  if (tab === "General") {
    return (
      <dl className="grid gap-3 text-sm md:grid-cols-2 xl:grid-cols-3">
        <DetailItem label="Codigo" value={asset.codigo} />
        <DetailItem label="Nombre" value={asset.nombre} />
        <DetailItem label="Faena" value={asset.faenaCodigo} />
        <DetailItem label="Estado" value={asset.estado} />
        <DetailItem label="Documental" value={asset.estadoDocumental} />
        <DetailItem label="Operacional" value={asset.estadoOperacional} />
        <DetailItem label="Propiedad" value={asset.propiedad ?? "-"} />
        <DetailItem label="Criticidad" value={asset.criticidad ?? "-"} />
        <DetailItem label="Ubicacion" value={asset.ubicacionTecnicaCodigo ?? "-"} />
      </dl>
    );
  }

  if (tab === "Ficha tecnica") {
    return (
      <div className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-3">
          <Metric label="Completitud" value={`${asset.completitudFicha.percentage}%`} />
          <Metric label="Campos completos" value={`${asset.completitudFicha.completedFields}/${asset.completitudFicha.requiredFields}`} />
          <Metric label="Estado ficha" value={asset.completitudFicha.state} />
        </div>
        <KeyValueTable values={asset.technicalFields} />
        {asset.completitudFicha.missingFields.length > 0 ? (
          <div className="rounded-md border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-200">
            Pendientes: {asset.completitudFicha.missingFields.join(", ")}
          </div>
        ) : null}
      </div>
    );
  }

  if (tab === "Documentos") {
    return documents.length === 0 ? (
      <EmptyState icon={FileText} text="Sin documentos asociados." />
    ) : (
      <SimpleTable
        columns={["Tipo", "Estado", "Vence", "Critico", "Disponibilidad"]}
        rows={documents.map((item) => [
          item.tipoDocumento,
          item.estado,
          item.fechaVencimiento ? formatDate(item.fechaVencimiento) : "-",
          item.critico ? "Si" : "No",
          item.bloqueaDisponibilidad ? "Bloquea" : "OK"
        ])}
      />
    );
  }

  if (tab === "Repuestos") {
    return asset.repuestosCompatibles.length === 0 ? (
      <EmptyState icon={Package} text="Sin repuestos compatibles por familia." />
    ) : (
      <SimpleTable
        columns={["Codigo", "Descripcion", "Familia", "Unidad"]}
        rows={asset.repuestosCompatibles.map((item) => [item.codigo, item.descripcion, item.familia ?? "-", item.unidadMedida ?? "-"])}
      />
    );
  }

  if (tab === "OT") {
    return asset.workOrders.length === 0 ? (
      <EmptyState icon={Wrench} text="Sin ordenes de trabajo asociadas." />
    ) : (
      <SimpleTable
        columns={["OT", "Estado", "Tipo", "Fecha", "Descripcion"]}
        rows={asset.workOrders.map((item) => [
          item.numeroOT,
          item.estado,
          item.tipoMantenimiento,
          item.fechaProgramada ? formatDate(item.fechaProgramada) : "-",
          item.descripcion ?? "-"
        ])}
      />
    );
  }

  if (tab === "Costos") {
    if (!canViewCosts) {
      return <EmptyState icon={AlertTriangle} text="Costos restringidos por permiso." />;
    }

    return (
      <div className="space-y-4">
        <Metric label="Total" value={`${costs?.currency ?? "CLP"} ${formatNumber(costs?.total ?? 0)}`} />
        {costs?.items.length ? (
          <SimpleTable
            columns={["Origen", "Tipo", "Monto", "Referencia"]}
            rows={costs.items.map((item) => [item.source, item.tipoCosto, `${item.currency} ${formatNumber(item.amount)}`, item.reference ?? "-"])}
          />
        ) : (
          <EmptyState icon={AlertTriangle} text="Sin costos operacionales registrados." />
        )}
      </div>
    );
  }

  if (tab === "Disponibilidad") {
    return availability ? (
      <div className="space-y-4">
        <div className="grid gap-3 sm:grid-cols-3">
          <Metric label="Disponibilidad" value={`${availability.porcentajeDisponibilidad}%`} />
          <Metric label="Operacional" value={availability.disponibleOperacionalmente ? "Disponible" : "No disponible"} />
          <Metric label="Documental" value={availability.disponibleDocumentalmente ? "Disponible" : "No disponible"} />
        </div>
        {availability.bloqueos.length > 0 ? (
          <SimpleTable columns={["Bloqueos"]} rows={availability.bloqueos.map((item) => [item])} />
        ) : (
          <EmptyState icon={CheckCircle2} text="Sin bloqueos de disponibilidad." />
        )}
      </div>
    ) : (
      <EmptyState icon={AlertTriangle} text="Sin calculo de disponibilidad." />
    );
  }

  return history.length === 0 ? (
    <EmptyState icon={History} text="Sin historial registrado." />
  ) : (
    <SimpleTable
      columns={["Fecha", "Accion", "Origen", "Usuario", "Detalle"]}
      rows={history.map((item) => [formatDateTime(item.occurredAtUtc), item.action, item.source, item.userId, item.detail ?? "-"])}
    />
  );
}

function Field({
  label,
  value,
  onChange,
  disabled
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  const id = `asset-${label.toLowerCase().replace(/\s+/g, "-")}`;
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <input
        id={id}
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 disabled:bg-slate-100 disabled:text-slate-500 dark:border-slate-700 dark:bg-slate-950 dark:disabled:bg-slate-900"
        disabled={disabled}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function SelectField({
  label,
  value,
  options,
  onChange
}: {
  label: string;
  value: string;
  options: readonly string[];
  onChange: (value: string) => void;
}) {
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
      {label}
      <select
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </label>
  );
}

function FilterSelect({
  label,
  value,
  options,
  onChange
}: {
  label: string;
  value: string;
  options: readonly string[];
  onChange: (value: string) => void;
}) {
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
      {label}
      <select
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">Todos</option>
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </label>
  );
}

function StatusBadge({ status, documental }: { status: AssetStatus; documental: boolean }) {
  const statusClass =
    status === "Active"
      ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
      : status === "InMaintenance"
        ? "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200"
        : status === "Unavailable" || status === "Retired"
          ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
          : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200";

  return (
    <div className="flex flex-col gap-1">
      <span className={`w-fit rounded-full px-2 py-1 text-xs font-semibold ${statusClass}`}>{status}</span>
      {!documental ? (
        <span className="w-fit rounded-full bg-red-50 px-2 py-1 text-xs font-semibold text-red-700 dark:bg-red-950 dark:text-red-200">
          Doc. vencido
        </span>
      ) : null}
    </div>
  );
}

function CompletenessBadge({ completeness }: { completeness: AssetCompleteness }) {
  const className =
    completeness.state === "Completa"
      ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
      : completeness.state === "Parcial"
        ? "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200"
        : "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200";

  return <span className={`inline-flex rounded-full px-2 py-1 text-xs font-semibold ${className}`}>{completeness.state} · {completeness.percentage}%</span>;
}

function DetailItem({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">{label}</dt>
      <dd className="mt-1 break-words text-slate-800 dark:text-slate-100">{value}</dd>
    </div>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-slate-200 p-3 dark:border-slate-800">
      <p className="text-xs font-medium text-slate-500 dark:text-slate-400">{label}</p>
      <p className="mt-2 text-xl font-semibold text-slate-950 dark:text-white">{value}</p>
    </div>
  );
}

function KeyValueTable({ values }: { values: Record<string, string | null> }) {
  const entries = Object.entries(values).filter(([, value]) => value);
  if (entries.length === 0) {
    return <p className="text-sm text-slate-500 dark:text-slate-400">Sin campos tecnicos cargados.</p>;
  }

  return (
    <SimpleTable
      columns={["Campo", "Valor"]}
      rows={entries.map(([key, value]) => [key, value ?? "-"])}
    />
  );
}

function SimpleTable({ columns, rows }: { columns: string[]; rows: string[][] }) {
  return (
    <div className="overflow-x-auto">
      <table className="min-w-full text-left text-sm">
        <thead className="bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
          <tr>
            {columns.map((column) => (
              <th key={column} className="px-4 py-3 font-medium">
                {column}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {rows.map((row, rowIndex) => (
            <tr key={`${row[0]}-${rowIndex}`}>
              {row.map((cell, cellIndex) => (
                <td key={`${cell}-${cellIndex}`} className="px-4 py-3 text-slate-600 dark:text-slate-300">
                  {cell}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function EmptyState({ icon: Icon, text }: { icon: typeof AlertTriangle; text: string }) {
  return (
    <div className="flex min-h-32 items-center justify-center text-sm text-slate-500 dark:text-slate-400">
      <Icon className="mr-2 h-5 w-5" aria-hidden="true" />
      {text}
    </div>
  );
}

function toForm(asset: AssetDetail): AssetFormState {
  return {
    codigo: asset.codigo,
    nombre: asset.nombre,
    faenaCodigo: asset.faenaCodigo,
    tipoActivo: asset.tipoActivo,
    estado: asset.estado,
    ubicacionTecnicaCodigo: asset.ubicacionTecnicaCodigo ?? "",
    familia: asset.familia ?? "",
    marca: asset.marca ?? "",
    modelo: asset.modelo ?? "",
    patente: asset.patente ?? "",
    numeroSerie: asset.numeroSerie ?? "",
    propiedad: asset.propiedad ?? "Propio",
    criticidad: asset.criticidad ?? "Media",
    estadoDocumental: asset.estadoDocumental,
    estadoOperacional: asset.estadoOperacional,
    technicalFieldsText: Object.entries(asset.technicalFields)
      .filter(([key]) => !["TipoActivo", "Familia", "Marca", "Modelo", "Patente", "NumeroSerie", "Propiedad", "Criticidad", "UbicacionTecnicaCodigo"].includes(key))
      .map(([key, value]) => `${key}=${value ?? ""}`)
      .join("\n"),
    fichaValidada: asset.fichaValidada
  };
}

function toPayload(form: AssetFormState) {
  return {
    codigo: form.codigo,
    nombre: form.nombre,
    faenaCodigo: form.faenaCodigo,
    tipoActivo: form.tipoActivo,
    estado: form.estado,
    ubicacionTecnicaCodigo: emptyToNull(form.ubicacionTecnicaCodigo),
    familia: emptyToNull(form.familia),
    marca: emptyToNull(form.marca),
    modelo: emptyToNull(form.modelo),
    patente: emptyToNull(form.patente),
    numeroSerie: emptyToNull(form.numeroSerie),
    propiedad: emptyToNull(form.propiedad),
    criticidad: emptyToNull(form.criticidad),
    estadoDocumental: emptyToNull(form.estadoDocumental),
    estadoOperacional: emptyToNull(form.estadoOperacional),
    technicalFields: parseTechnicalFields(form.technicalFieldsText),
    fichaValidada: form.fichaValidada
  };
}

function parseTechnicalFields(value: string) {
  return value
    .split("\n")
    .map((line) => line.trim())
    .filter(Boolean)
    .reduce<Record<string, string>>((accumulator, line) => {
      const [key, ...rest] = line.split("=");
      if (key?.trim()) {
        accumulator[key.trim()] = rest.join("=").trim();
      }
      return accumulator;
    }, {});
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function uniqueOptions(values: string[]) {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean))).sort((a, b) => a.localeCompare(b));
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("es-CL", { dateStyle: "short" }).format(new Date(value));
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("es-CL", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
}

function formatNumber(value: number) {
  return new Intl.NumberFormat("es-CL").format(value);
}
