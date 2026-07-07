import { FormEvent, useEffect, useMemo, useState } from "react";
import { AlertTriangle, CheckCircle2, Package, Plus, RefreshCw, Search } from "lucide-react";
import { apiFetch } from "../auth/authStore";

type SparePartStatus = "Activo" | "Obsoleto" | "Bloqueado" | "Reemplazado";

type SparePartSummary = {
  codigo: string;
  codigoSap?: string | null;
  codigoProveedor?: string | null;
  descripcion: string;
  descripcionTecnica: string;
  unidadMedida: string;
  familiaEquipo?: string | null;
  marcaFabricante?: string | null;
  modeloReferencia?: string | null;
  critico: boolean;
  stockMinimo: number;
  stockMaximo: number;
  puntoReposicion: number;
  leadTimeEsperadoDias: number;
  costoUnitarioPromedio?: number | null;
  estado: SparePartStatus;
  esNoCodificado: boolean;
  proveedorPreferente?: string | null;
  reemplazoCodigo?: string | null;
  stockFisicoTotal: number;
  stockReservadoTotal: number;
  stockDisponibleTotal: number;
  bajoMinimo: boolean;
  criticoSinStock: boolean;
};

type StockItem = {
  bodegaCodigo: string;
  bodegaNombre: string;
  stockFisico: number;
  stockReservado: number;
  stockDisponible: number;
  stockMinimo: number;
  bajoMinimo: boolean;
  criticoSinStock: boolean;
};

type StockMovement = {
  movimientoId: string;
  fechaUtc: string;
  type: string;
  bodegaCodigo?: string | null;
  bodegaOrigenCodigo?: string | null;
  bodegaDestinoCodigo?: string | null;
  quantity: number;
  motivo: string;
  usuarioId: string;
};

type SparePartDetail = {
  summary: SparePartSummary;
  stock: StockItem[];
  movements: StockMovement[];
};

type FormState = {
  descripcion: string;
  unidadMedida: string;
  codigoSap: string;
  codigoProveedor: string;
  descripcionTecnica: string;
  familiaEquipo: string;
  marcaFabricante: string;
  modeloReferencia: string;
  critico: boolean;
  stockMinimo: string;
  stockMaximo: string;
  puntoReposicion: string;
  leadTimeEsperadoDias: string;
  costoUnitarioPromedio: string;
  estado: SparePartStatus;
  proveedorPreferente: string;
};

const emptyForm: FormState = {
  descripcion: "",
  unidadMedida: "UN",
  codigoSap: "",
  codigoProveedor: "",
  descripcionTecnica: "",
  familiaEquipo: "",
  marcaFabricante: "",
  modeloReferencia: "",
  critico: false,
  stockMinimo: "0",
  stockMaximo: "0",
  puntoReposicion: "0",
  leadTimeEsperadoDias: "0",
  costoUnitarioPromedio: "0",
  estado: "Activo",
  proveedorPreferente: ""
};

export function SparePartsPage() {
  const [items, setItems] = useState<SparePartSummary[]>([]);
  const [selected, setSelected] = useState<SparePartDetail | null>(null);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [filters, setFilters] = useState({ search: "", familia: "", lowStockOnly: false, criticalOnly: false, includeObsolete: false });
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    void loadItems();
  }, []);

  const families = useMemo(() => unique(items.map((item) => item.familiaEquipo ?? "")), [items]);

  async function loadItems(nextFilters = filters) {
    setIsLoading(true);
    setError(null);
    try {
      const query = toQuery(nextFilters);
      const result = await apiFetch<SparePartSummary[]>(`/api/inventory/spare-parts?${query}`);
      setItems(result);
      if (result[0]) {
        await selectItem(result[0].codigo);
      } else {
        setSelected(null);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar repuestos.");
    } finally {
      setIsLoading(false);
    }
  }

  async function selectItem(code: string) {
    try {
      setSelected(await apiFetch<SparePartDetail>(`/api/inventory/spare-parts/${encodeURIComponent(code)}`));
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar ficha 360.");
    }
  }

  async function createItem(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);
    try {
      const created = await apiFetch<SparePartDetail>("/api/inventory/spare-parts", {
        method: "POST",
        body: JSON.stringify(toPayload(form))
      });
      setForm(emptyForm);
      setMessage(`Repuesto ${created.summary.codigo} creado.`);
      await loadItems();
      setSelected(created);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible crear repuesto.");
    } finally {
      setIsSaving(false);
    }
  }

  function applyFilters(nextFilters: typeof filters) {
    setFilters(nextFilters);
    void loadItems(nextFilters);
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Repuestos</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Maestro, stock consolidado y repuesto 360.</p>
        </div>
        <button
          className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          onClick={() => void loadItems()}
          type="button"
        >
          <RefreshCw className="h-4 w-4" aria-hidden="true" />
          Actualizar
        </button>
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
          <Field label="Buscar" value={filters.search} onChange={(value) => applyFilters({ ...filters, search: value })} icon />
          <Select label="Familia" value={filters.familia} options={["", ...families]} onChange={(value) => applyFilters({ ...filters, familia: value })} />
          <CheckField label="Bajo minimo" checked={filters.lowStockOnly} onChange={(value) => applyFilters({ ...filters, lowStockOnly: value })} />
          <CheckField label="Criticos" checked={filters.criticalOnly} onChange={(value) => applyFilters({ ...filters, criticalOnly: value })} />
          <CheckField label="Obsoletos" checked={filters.includeObsolete} onChange={(value) => applyFilters({ ...filters, includeObsolete: value })} />
        </div>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
            <h2 className="text-base font-semibold text-slate-950 dark:text-white">Maestro</h2>
            <span className="text-sm text-slate-500 dark:text-slate-400">{items.length}</span>
          </div>
          {isLoading ? <p className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando repuestos...</p> : <SparePartsTable items={items} selected={selected?.summary.codigo} onSelect={(code) => void selectItem(code)} />}
        </section>

        <SparePart360 detail={selected} />
      </section>

      <form className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900" onSubmit={createItem}>
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Nuevo repuesto</h2>
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-4">
          <Field label="Descripcion" value={form.descripcion} onChange={(value) => setForm({ ...form, descripcion: value })} />
          <Field label="Unidad" value={form.unidadMedida} onChange={(value) => setForm({ ...form, unidadMedida: value })} />
          <Field label="Codigo SAP" value={form.codigoSap} onChange={(value) => setForm({ ...form, codigoSap: value })} />
          <Field label="Codigo proveedor" value={form.codigoProveedor} onChange={(value) => setForm({ ...form, codigoProveedor: value })} />
          <Field label="Familia equipo" value={form.familiaEquipo} onChange={(value) => setForm({ ...form, familiaEquipo: value })} />
          <Field label="Fabricante" value={form.marcaFabricante} onChange={(value) => setForm({ ...form, marcaFabricante: value })} />
          <Field label="Modelo/ref." value={form.modeloReferencia} onChange={(value) => setForm({ ...form, modeloReferencia: value })} />
          <Select label="Estado" value={form.estado} options={["Activo", "Obsoleto", "Bloqueado", "Reemplazado"]} onChange={(value) => setForm({ ...form, estado: value as SparePartStatus })} />
          <Field label="Stock minimo" type="number" value={form.stockMinimo} onChange={(value) => setForm({ ...form, stockMinimo: value })} />
          <Field label="Stock maximo" type="number" value={form.stockMaximo} onChange={(value) => setForm({ ...form, stockMaximo: value })} />
          <Field label="Reposicion" type="number" value={form.puntoReposicion} onChange={(value) => setForm({ ...form, puntoReposicion: value })} />
          <Field label="Lead time dias" type="number" value={form.leadTimeEsperadoDias} onChange={(value) => setForm({ ...form, leadTimeEsperadoDias: value })} />
          <Field label="Costo promedio" type="number" value={form.costoUnitarioPromedio} onChange={(value) => setForm({ ...form, costoUnitarioPromedio: value })} />
          <Field label="Proveedor pref." value={form.proveedorPreferente} onChange={(value) => setForm({ ...form, proveedorPreferente: value })} />
          <CheckField label="Critico" checked={form.critico} onChange={(value) => setForm({ ...form, critico: value })} />
        </div>
        <Field label="Descripcion tecnica" value={form.descripcionTecnica} onChange={(value) => setForm({ ...form, descripcionTecnica: value })} />
        <button
          className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
          disabled={isSaving}
          type="submit"
        >
          <Plus className="h-4 w-4" aria-hidden="true" />
          Crear
        </button>
      </form>

      {message ? <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">{message}</div> : null}
      {error ? <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">{error}</div> : null}
    </section>
  );
}

function SparePartsTable({ items, selected, onSelect }: { items: SparePartSummary[]; selected?: string; onSelect: (code: string) => void }) {
  return (
    <div className="max-h-[620px] overflow-auto">
      <table className="min-w-full text-left text-sm">
        <thead className="sticky top-0 bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
          <tr>
            <th className="px-4 py-3 font-medium">Codigo</th>
            <th className="px-4 py-3 font-medium">Descripcion</th>
            <th className="px-4 py-3 font-medium">SAP</th>
            <th className="px-4 py-3 font-medium">Disponible</th>
            <th className="px-4 py-3 font-medium">Estado</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {items.map((item) => (
            <tr key={item.codigo} className={selected === item.codigo ? "bg-teal-50/70 dark:bg-teal-950/30" : ""}>
              <td className="px-4 py-3">
                <button className="font-semibold text-slate-900 dark:text-slate-100" onClick={() => onSelect(item.codigo)} type="button">{item.codigo}</button>
              </td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">
                {item.descripcion}
                <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{item.familiaEquipo ?? "-"} · {item.unidadMedida}</p>
              </td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{item.codigoSap ?? (item.esNoCodificado ? "No codificado" : "-")}</td>
              <td className="px-4 py-3 font-semibold text-slate-900 dark:text-slate-100">{formatNumber(item.stockDisponibleTotal)}</td>
              <td className="px-4 py-3"><PartBadge item={item} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function SparePart360({ detail }: { detail: SparePartDetail | null }) {
  if (!detail) {
    return (
      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Repuesto 360</h2>
      </section>
    );
  }

  const item = detail.summary;
  return (
    <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-start justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">{item.codigo}</h2>
          <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{item.descripcion}</p>
        </div>
        <PartBadge item={item} />
      </div>
      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <Detail label="SAP" value={item.codigoSap ?? "-"} />
        <Detail label="Proveedor" value={item.codigoProveedor ?? "-"} />
        <Detail label="Familia" value={item.familiaEquipo ?? "-"} />
        <Detail label="Fabricante" value={item.marcaFabricante ?? "-"} />
        <Detail label="Modelo" value={item.modeloReferencia ?? "-"} />
        <Detail label="Lead time" value={`${item.leadTimeEsperadoDias} dias`} />
        <Detail label="Fisico" value={formatNumber(item.stockFisicoTotal)} />
        <Detail label="Disponible" value={formatNumber(item.stockDisponibleTotal)} />
        <Detail label="Costo" value={item.costoUnitarioPromedio === null || item.costoUnitarioPromedio === undefined ? "-" : formatNumber(item.costoUnitarioPromedio)} />
      </dl>
      <section>
        <h3 className="text-sm font-semibold text-slate-950 dark:text-white">Stock por bodega</h3>
        <div className="mt-2 space-y-2">
          {detail.stock.map((row) => (
            <div key={row.bodegaCodigo} className="rounded-md border border-slate-200 p-3 text-sm dark:border-slate-800">
              <div className="flex items-center justify-between gap-2">
                <span className="font-semibold text-slate-900 dark:text-slate-100">{row.bodegaCodigo}</span>
                <span className="text-slate-600 dark:text-slate-300">{formatNumber(row.stockDisponible)} disp.</span>
              </div>
              <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">Fisico {formatNumber(row.stockFisico)} · Reservado {formatNumber(row.stockReservado)} · Min {formatNumber(row.stockMinimo)}</p>
            </div>
          ))}
        </div>
      </section>
      <section>
        <h3 className="text-sm font-semibold text-slate-950 dark:text-white">Movimientos recientes</h3>
        <div className="mt-2 max-h-56 overflow-auto">
          {detail.movements.map((movement) => (
            <div key={movement.movimientoId} className="border-b border-slate-100 py-2 text-sm dark:border-slate-800">
              <div className="flex items-center justify-between gap-2">
                <span className="font-semibold text-slate-900 dark:text-slate-100">{movement.type}</span>
                <span className="text-slate-600 dark:text-slate-300">{formatNumber(movement.quantity)}</span>
              </div>
              <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{formatDate(movement.fechaUtc)} · {movement.motivo}</p>
            </div>
          ))}
        </div>
      </section>
    </section>
  );
}

function PartBadge({ item }: { item: SparePartSummary }) {
  if (item.criticoSinStock) {
    return <Badge className="bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200" icon="alert" text="Sin stock" />;
  }
  if (item.bajoMinimo) {
    return <Badge className="bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200" icon="alert" text="Bajo minimo" />;
  }
  if (item.estado !== "Activo") {
    return <Badge className="bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200" icon="package" text={item.estado} />;
  }
  return <Badge className="bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200" icon="ok" text="Activo" />;
}

function Badge({ className, icon, text }: { className: string; icon: "alert" | "ok" | "package"; text: string }) {
  const Icon = icon === "alert" ? AlertTriangle : icon === "ok" ? CheckCircle2 : Package;
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-1 text-xs font-semibold ${className}`}>
      <Icon className="h-3.5 w-3.5" aria-hidden="true" />
      {text}
    </span>
  );
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">{label}</dt>
      <dd className="mt-1 break-words text-slate-800 dark:text-slate-100">{value}</dd>
    </div>
  );
}

function Field({ label, value, onChange, type = "text", icon }: { label: string; value: string; onChange: (value: string) => void; type?: string; icon?: boolean }) {
  const id = `spare-${label.toLowerCase().replace(/\s+/g, "-")}`;
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <span className="relative mt-2 block">
        {icon ? <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" aria-hidden="true" /> : null}
        <input id={id} className={`h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950 ${icon ? "pl-9" : ""}`} type={type} value={value} onChange={(event) => onChange(event.target.value)} />
      </span>
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

function toPayload(form: FormState) {
  return {
    descripcion: form.descripcion,
    unidadMedida: form.unidadMedida,
    codigoSap: emptyToNull(form.codigoSap),
    codigoProveedor: emptyToNull(form.codigoProveedor),
    descripcionTecnica: emptyToNull(form.descripcionTecnica),
    familiaEquipo: emptyToNull(form.familiaEquipo),
    marcaFabricante: emptyToNull(form.marcaFabricante),
    modeloReferencia: emptyToNull(form.modeloReferencia),
    critico: form.critico,
    stockMinimo: Number(form.stockMinimo || 0),
    stockMaximo: Number(form.stockMaximo || 0),
    puntoReposicion: Number(form.puntoReposicion || 0),
    leadTimeEsperadoDias: Number(form.leadTimeEsperadoDias || 0),
    costoUnitarioPromedio: Number(form.costoUnitarioPromedio || 0),
    estado: form.estado,
    proveedorPreferente: emptyToNull(form.proveedorPreferente)
  };
}

function toQuery(filters: { search: string; familia: string; lowStockOnly: boolean; criticalOnly: boolean; includeObsolete: boolean }) {
  const query = new URLSearchParams();
  if (filters.search) {
    query.set("search", filters.search);
  }
  if (filters.familia) {
    query.set("familia", filters.familia);
  }
  if (filters.lowStockOnly) {
    query.set("lowStockOnly", "true");
  }
  if (filters.criticalOnly) {
    query.set("criticalOnly", "true");
  }
  if (filters.includeObsolete) {
    query.set("includeObsolete", "true");
  }
  return query.toString();
}

function unique(values: string[]) {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean))).sort((left, right) => left.localeCompare(right));
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function formatNumber(value: number) {
  return new Intl.NumberFormat("es-CL", { maximumFractionDigits: 2 }).format(value);
}

function formatDate(value: string) {
  return new Intl.DateTimeFormat("es-CL", { dateStyle: "short", timeStyle: "short" }).format(new Date(value));
}
