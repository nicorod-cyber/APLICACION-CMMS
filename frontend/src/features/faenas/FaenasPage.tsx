import { FormEvent, useEffect, useMemo, useState } from "react";
import { MapPinned, Pencil, Plus, RefreshCw, Save, X } from "lucide-react";
import { apiFetch, type CurrentUser } from "../auth/authStore";
import type { FaenaRecord } from "./FaenaSelect";

type FaenaForm = {
  codigo: string;
  nombre: string;
  zona: string;
  cliente: string;
  centroCostes: string;
  tipoFaena: string;
  region: string;
  comuna: string;
  latitud: string;
  longitud: string;
  responsableUsuarioId: string;
  activo: boolean;
  ubicacionTecnicaCodigo: string;
  ubicacionTecnicaNombre: string;
  ubicacionTecnicaObsoleta: boolean;
};

type EditorMode = "detail" | "create" | "edit";
type ActivityFilter = "all" | "active" | "inactive";

const emptyForm = (): FaenaForm => ({
  codigo: "",
  nombre: "",
  zona: "",
  cliente: "",
  centroCostes: "",
  tipoFaena: "",
  region: "",
  comuna: "",
  latitud: "",
  longitud: "",
  responsableUsuarioId: "",
  activo: true,
  ubicacionTecnicaCodigo: "",
  ubicacionTecnicaNombre: "",
  ubicacionTecnicaObsoleta: false
});

export function FaenasPage() {
  const [faenas, setFaenas] = useState<FaenaRecord[]>([]);
  const [users, setUsers] = useState<CurrentUser[]>([]);
  const [selectedCode, setSelectedCode] = useState("");
  const [form, setForm] = useState<FaenaForm>(emptyForm);
  const [mode, setMode] = useState<EditorMode>("detail");
  const [search, setSearch] = useState("");
  const [activityFilter, setActivityFilter] = useState<ActivityFilter>("all");
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const selected = useMemo(
    () => faenas.find((faena) => faena.codigo === selectedCode) ?? null,
    [faenas, selectedCode]
  );

  const visibleFaenas = useMemo(() => {
    const query = search.trim().toLocaleLowerCase();
    return faenas.filter((faena) => {
      if (activityFilter === "active" && !faena.activo) {
        return false;
      }

      if (activityFilter === "inactive" && faena.activo) {
        return false;
      }

      if (!query) {
        return true;
      }

      return [faena.codigo, faena.nombre, faena.zona, faena.cliente, faena.responsableNombre]
        .filter(Boolean)
        .some((value) => value!.toLocaleLowerCase().includes(query));
    });
  }, [activityFilter, faenas, search]);

  useEffect(() => {
    void loadData();
  }, []);

  async function loadData(preferredCode?: string) {
    setIsLoading(true);
    setError(null);

    try {
      const [faenaResult, userResult] = await Promise.all([
        apiFetch<FaenaRecord[]>("/api/faenas?includeInactive=true"),
        apiFetch<CurrentUser[]>("/api/users")
      ]);
      const sortedFaenas = sortFaenas(faenaResult);
      const activeUsers = userResult
        .filter((user) => user.isActive)
        .sort((left, right) => left.displayName.localeCompare(right.displayName));
      const next =
        sortedFaenas.find((faena) => faena.codigo === (preferredCode ?? selectedCode)) ?? sortedFaenas[0] ?? null;

      setFaenas(sortedFaenas);
      setUsers(activeUsers);
      setSelectedCode(next?.codigo ?? "");
      if (next) {
        setForm(toForm(next));
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar las faenas.");
    } finally {
      setIsLoading(false);
    }
  }

  function selectFaena(faena: FaenaRecord) {
    setSelectedCode(faena.codigo);
    setForm(toForm(faena));
    setMode("detail");
    setError(null);
    setMessage(null);
  }

  function beginCreate() {
    setForm(emptyForm());
    setMode("create");
    setError(null);
    setMessage(null);
  }

  function beginEdit() {
    if (!selected) {
      return;
    }

    setForm(toForm(selected));
    setMode("edit");
    setError(null);
    setMessage(null);
  }

  function cancelEditor() {
    if (selected) {
      setForm(toForm(selected));
      setMode("detail");
    } else {
      setForm(emptyForm());
      setMode("detail");
    }

    setError(null);
  }

  async function saveFaena(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const saved = await apiFetch<FaenaRecord>(
        mode === "create" ? "/api/faenas" : `/api/faenas/${encodeURIComponent(form.codigo)}`,
        {
          method: mode === "create" ? "POST" : "PUT",
          body: JSON.stringify(toPayload(form))
        }
      );

      setFaenas((current) => sortFaenas([...current.filter((faena) => faena.codigo !== saved.codigo), saved]));
      setSelectedCode(saved.codigo);
      setForm(toForm(saved));
      setMode("detail");
      setMessage(mode === "create" ? "Faena creada." : "Faena actualizada.");
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible guardar la faena.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="space-y-6">
      <header className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Administración de faenas</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">
            Datos operacionales, responsable y ubicación técnica única por faena.
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            onClick={() => void loadData(selectedCode)}
            type="button"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Actualizar
          </button>
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
            onClick={beginCreate}
            type="button"
          >
            <Plus className="h-4 w-4" aria-hidden="true" />
            Nueva faena
          </button>
        </div>
      </header>

      {message ? <Notice>{message}</Notice> : null}
      {error ? <Notice error>{error}</Notice> : null}

      <section className="grid gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <div className="space-y-4">
          <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="grid gap-3 sm:grid-cols-[1fr_auto]">
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
                Buscar
                <input
                  className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
                  placeholder="Código, nombre, cliente o responsable"
                  value={search}
                  onChange={(event) => setSearch(event.target.value)}
                />
              </label>
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
                Estado
                <select
                  className="mt-2 h-10 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
                  value={activityFilter}
                  onChange={(event) => setActivityFilter(event.target.value as ActivityFilter)}
                >
                  <option value="all">Todas</option>
                  <option value="active">Activas</option>
                  <option value="inactive">Inactivas</option>
                </select>
              </label>
            </div>
          </section>

          <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
              <h2 className="text-base font-semibold text-slate-950 dark:text-white">Faenas</h2>
              <span className="text-sm text-slate-500 dark:text-slate-400">{visibleFaenas.length}</span>
            </div>
            {isLoading ? (
              <p className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando faenas...</p>
            ) : visibleFaenas.length === 0 ? (
              <p className="p-4 text-sm text-slate-500 dark:text-slate-400">No hay faenas que coincidan con el filtro.</p>
            ) : (
              <div className="max-h-[680px] overflow-y-auto">
                {visibleFaenas.map((faena) => (
                  <button
                    key={faena.id}
                    className={`block w-full border-b border-slate-100 px-4 py-3 text-left transition hover:bg-slate-50 dark:border-slate-800 dark:hover:bg-slate-800 ${
                      selectedCode === faena.codigo ? "bg-teal-50 dark:bg-teal-950/30" : ""
                    }`}
                    onClick={() => selectFaena(faena)}
                    type="button"
                  >
                    <div className="flex items-start justify-between gap-3">
                      <div>
                        <p className="font-semibold text-slate-900 dark:text-slate-100">{faena.nombre}</p>
                        <p className="mt-0.5 text-xs text-slate-500 dark:text-slate-400">
                          {faena.codigo} · {faena.cliente || "Sin cliente"}
                        </p>
                      </div>
                      <ActivityBadge active={faena.activo} />
                    </div>
                    <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">
                      {faena.responsableNombre || "Sin responsable"} · {formatTechnicalLocation(faena)}
                    </p>
                  </button>
                ))}
              </div>
            )}
          </section>
        </div>

        {mode === "create" || mode === "edit" ? (
          <FaenaEditor
            form={form}
            isCreating={mode === "create"}
            isSaving={isSaving}
            users={users}
            onCancel={cancelEditor}
            onChange={(patch) => setForm((current) => ({ ...current, ...patch }))}
            onSubmit={saveFaena}
          />
        ) : selected ? (
          <FaenaDetail faena={selected} onEdit={beginEdit} />
        ) : (
          <EmptyDetail onCreate={beginCreate} />
        )}
      </section>
    </section>
  );
}

function FaenaEditor({
  form,
  isCreating,
  isSaving,
  users,
  onCancel,
  onChange,
  onSubmit
}: {
  form: FaenaForm;
  isCreating: boolean;
  isSaving: boolean;
  users: CurrentUser[];
  onCancel: () => void;
  onChange: (patch: Partial<FaenaForm>) => void;
  onSubmit: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <form className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900" onSubmit={onSubmit}>
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold text-slate-950 dark:text-white">{isCreating ? "Nueva faena" : `Editar ${form.codigo}`}</h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">La ubicación técnica se administra en este mismo formulario.</p>
        </div>
        <button
          className="inline-flex h-9 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          onClick={onCancel}
          type="button"
        >
          <X className="h-4 w-4" aria-hidden="true" />
          Cancelar
        </button>
      </div>

      <div className="mt-5 grid gap-3 md:grid-cols-2">
        <Field label="Código" required disabled={!isCreating} value={form.codigo} onChange={(codigo) => onChange({ codigo })} />
        <Field label="Nombre" required value={form.nombre} onChange={(nombre) => onChange({ nombre })} />
        <Field label="Zona" required value={form.zona} onChange={(zona) => onChange({ zona })} />
        <Field label="Cliente" required value={form.cliente} onChange={(cliente) => onChange({ cliente })} />
        <Field label="Centro de costes" value={form.centroCostes} onChange={(centroCostes) => onChange({ centroCostes })} />
        <Field label="Tipo de faena" required value={form.tipoFaena} onChange={(tipoFaena) => onChange({ tipoFaena })} />
        <Field label="Región" required value={form.region} onChange={(region) => onChange({ region })} />
        <Field label="Comuna" required value={form.comuna} onChange={(comuna) => onChange({ comuna })} />
        <Field label="Latitud" inputMode="decimal" type="number" step="any" value={form.latitud} onChange={(latitud) => onChange({ latitud })} />
        <Field label="Longitud" inputMode="decimal" type="number" step="any" value={form.longitud} onChange={(longitud) => onChange({ longitud })} />
        <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
          Responsable
          <select
            className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
            required
            value={form.responsableUsuarioId}
            onChange={(event) => onChange({ responsableUsuarioId: event.target.value })}
          >
            <option value="">Selecciona un usuario activo</option>
            {users.map((user) => (
              <option key={user.id} value={user.id}>
                {user.displayName} ({user.username})
              </option>
            ))}
          </select>
        </label>
        <label className="mt-7 flex h-10 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
          <input checked={form.activo} onChange={(event) => onChange({ activo: event.target.checked })} type="checkbox" />
          Faena activa
        </label>
      </div>

      <section className="mt-5 rounded-lg border border-slate-200 p-4 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <MapPinned className="h-5 w-5 text-teal-700 dark:text-teal-300" aria-hidden="true" />
          <div>
            <h3 className="font-semibold text-slate-950 dark:text-white">Ubicación técnica única</h3>
            <p className="text-sm text-slate-500 dark:text-slate-400">Sin padre, tipo, nombre normalizado ni jerarquía.</p>
          </div>
        </div>
        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <Field
            label="Código de ubicación técnica"
            required
            value={form.ubicacionTecnicaCodigo}
            onChange={(ubicacionTecnicaCodigo) => onChange({ ubicacionTecnicaCodigo })}
          />
          <Field
            label="Nombre de ubicación técnica"
            required
            value={form.ubicacionTecnicaNombre}
            onChange={(ubicacionTecnicaNombre) => onChange({ ubicacionTecnicaNombre })}
          />
        </div>
        <label className="mt-4 flex h-10 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
          <input
            checked={form.ubicacionTecnicaObsoleta}
            onChange={(event) => onChange({ ubicacionTecnicaObsoleta: event.target.checked })}
            type="checkbox"
          />
          Ubicación técnica obsoleta
        </label>
      </section>

      <button
        className="mt-5 inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
        disabled={isSaving}
        type="submit"
      >
        <Save className="h-4 w-4" aria-hidden="true" />
        {isSaving ? "Guardando..." : "Guardar faena"}
      </button>
    </form>
  );
}

function FaenaDetail({ faena, onEdit }: { faena: FaenaRecord; onEdit: () => void }) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-start justify-between gap-3">
        <div>
          <div className="flex flex-wrap items-center gap-2">
            <h2 className="text-xl font-semibold text-slate-950 dark:text-white">{faena.nombre}</h2>
            <ActivityBadge active={faena.activo} />
          </div>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{faena.codigo}</p>
        </div>
        <button
          className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
          onClick={onEdit}
          type="button"
        >
          <Pencil className="h-4 w-4" aria-hidden="true" />
          Editar
        </button>
      </div>

      <div className="mt-5 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        <DetailItem label="Zona" value={faena.zona} />
        <DetailItem label="Cliente" value={faena.cliente} />
        <DetailItem label="Centro de costes" value={faena.centroCostes} />
        <DetailItem label="Tipo de faena" value={faena.tipoFaena} />
        <DetailItem label="Región" value={faena.region} />
        <DetailItem label="Comuna" value={faena.comuna} />
        <DetailItem label="Latitud" value={formatCoordinate(faena.latitud)} />
        <DetailItem label="Longitud" value={formatCoordinate(faena.longitud)} />
        <DetailItem label="Responsable" value={faena.responsableNombre} />
      </div>

      <section className="mt-5 rounded-lg border border-slate-200 p-4 dark:border-slate-800">
        <div className="flex items-center gap-2">
          <MapPinned className="h-5 w-5 text-teal-700 dark:text-teal-300" aria-hidden="true" />
          <h3 className="font-semibold text-slate-950 dark:text-white">Ubicación técnica única</h3>
        </div>
        {faena.ubicacionTecnica ? (
          <div className="mt-3 flex flex-wrap items-center justify-between gap-3 rounded-md bg-slate-50 p-3 dark:bg-slate-950">
            <div>
              <p className="font-medium text-slate-900 dark:text-slate-100">{faena.ubicacionTecnica.nombre}</p>
              <p className="text-sm text-slate-500 dark:text-slate-400">{faena.ubicacionTecnica.codigo}</p>
            </div>
            <span
              className={`rounded-full px-2 py-1 text-xs font-semibold ${
                faena.ubicacionTecnica.obsoleto
                  ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
                  : "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
              }`}
            >
              {faena.ubicacionTecnica.obsoleto ? "Obsoleta" : "Vigente"}
            </span>
          </div>
        ) : (
          <p className="mt-3 text-sm text-amber-700 dark:text-amber-300">La faena no tiene una ubicación técnica configurada.</p>
        )}
      </section>
    </section>
  );
}

function EmptyDetail({ onCreate }: { onCreate: () => void }) {
  return (
    <section className="flex min-h-80 flex-col items-center justify-center rounded-lg border border-dashed border-slate-300 bg-white p-6 text-center shadow-sm dark:border-slate-700 dark:bg-slate-900">
      <MapPinned className="h-9 w-9 text-slate-400" aria-hidden="true" />
      <h2 className="mt-3 text-lg font-semibold text-slate-950 dark:text-white">Selecciona una faena</h2>
      <p className="mt-1 max-w-md text-sm text-slate-500 dark:text-slate-400">También puedes crear una faena con su ubicación técnica única.</p>
      <button
        className="mt-4 inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
        onClick={onCreate}
        type="button"
      >
        <Plus className="h-4 w-4" aria-hidden="true" />
        Nueva faena
      </button>
    </section>
  );
}

function Field({
  label,
  value,
  onChange,
  required,
  disabled,
  type = "text",
  step,
  inputMode
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  required?: boolean;
  disabled?: boolean;
  type?: string;
  step?: string;
  inputMode?: "decimal";
}) {
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
      {label}
      <input
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 disabled:bg-slate-100 disabled:text-slate-500 dark:border-slate-700 dark:bg-slate-950 dark:disabled:bg-slate-800"
        disabled={disabled}
        inputMode={inputMode}
        required={required}
        step={step}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function DetailItem({ label, value }: { label: string; value?: string | null }) {
  return (
    <div className="rounded-md border border-slate-200 p-3 dark:border-slate-800">
      <p className="text-xs font-medium text-slate-500 dark:text-slate-400">{label}</p>
      <p className="mt-1 text-sm font-semibold text-slate-900 dark:text-slate-100">{value || "-"}</p>
    </div>
  );
}

function ActivityBadge({ active }: { active: boolean }) {
  return (
    <span
      className={`rounded-full px-2 py-1 text-xs font-semibold ${
        active
          ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
          : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200"
      }`}
    >
      {active ? "Activa" : "Inactiva"}
    </span>
  );
}

function Notice({ children, error }: { children: React.ReactNode; error?: boolean }) {
  return (
    <div
      className={`rounded-md border p-3 text-sm ${
        error
          ? "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200"
          : "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200"
      }`}
    >
      {children}
    </div>
  );
}

function toForm(faena: FaenaRecord): FaenaForm {
  return {
    codigo: faena.codigo,
    nombre: faena.nombre,
    zona: faena.zona ?? "",
    cliente: faena.cliente ?? "",
    centroCostes: faena.centroCostes ?? "",
    tipoFaena: faena.tipoFaena ?? "",
    region: faena.region ?? "",
    comuna: faena.comuna ?? "",
    latitud: formatCoordinate(faena.latitud),
    longitud: formatCoordinate(faena.longitud),
    responsableUsuarioId: faena.responsableUsuarioId ?? "",
    activo: faena.activo,
    ubicacionTecnicaCodigo: faena.ubicacionTecnica?.codigo ?? "",
    ubicacionTecnicaNombre: faena.ubicacionTecnica?.nombre ?? "",
    ubicacionTecnicaObsoleta: faena.ubicacionTecnica?.obsoleto ?? false
  };
}

function toPayload(form: FaenaForm) {
  return {
    codigo: form.codigo.trim(),
    nombre: form.nombre.trim(),
    zona: form.zona.trim(),
    cliente: form.cliente.trim(),
    centroCostes: emptyToNull(form.centroCostes),
    tipoFaena: form.tipoFaena.trim(),
    region: form.region.trim(),
    comuna: form.comuna.trim(),
    latitud: toNumberOrNull(form.latitud),
    longitud: toNumberOrNull(form.longitud),
    responsableUsuarioId: form.responsableUsuarioId,
    activo: form.activo,
    ubicacionTecnicaCodigo: form.ubicacionTecnicaCodigo.trim(),
    ubicacionTecnicaNombre: form.ubicacionTecnicaNombre.trim(),
    ubicacionTecnicaObsoleta: form.ubicacionTecnicaObsoleta
  };
}

function sortFaenas(faenas: FaenaRecord[]) {
  return [...faenas].sort((left, right) => left.nombre.localeCompare(right.nombre) || left.codigo.localeCompare(right.codigo));
}

function formatTechnicalLocation(faena: FaenaRecord) {
  return faena.ubicacionTecnica ? `${faena.ubicacionTecnica.codigo} · ${faena.ubicacionTecnica.nombre}` : "Sin ubicación técnica";
}

function formatCoordinate(value?: number | null) {
  return value == null ? "" : String(value);
}

function emptyToNull(value: string) {
  const trimmed = value.trim();
  return trimmed || null;
}

function toNumberOrNull(value: string) {
  const normalized = value.trim().replace(",", ".");
  return normalized ? Number(normalized) : null;
}
