import { FormEvent, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Gauge, Pencil, RefreshCw, Search } from "lucide-react";
import { Link, useParams } from "react-router-dom";
import { ApiError, AUTH_PERMISSIONS, apiFetch, useAuthStore } from "../auth/authStore";
import { OperationalStatusBadge } from "../equipment-overview/OperationalStatusBadge";
import type { EquipmentOverviewRow, Page } from "../equipment-overview/types";
import type { FaenaRecord } from "./FaenaSelect";
import { FaenaActivityBadge, FaenaEditorDialog, FaenaSummary } from "./FaenaEditorDialog";

type Tab = "measurements" | "summary";
type MeasurementFilter = "ALL" | "HOROMETRO" | "KILOMETRAJE";
type AssetReadingResponse = { valor: number; fechaLecturaUtc: string };
type UnitReadingResponse = { lecturas: AssetReadingResponse[] };

export function FaenaDetailPage() {
  const params = useParams<{ codigo: string }>();
  const code = params.codigo?.trim() ?? "";
  const queryClient = useQueryClient();
  const user = useAuthStore(state => state.user);
  const canEdit = user?.permissions.includes(AUTH_PERMISSIONS.editFaenas) ?? false;
  const canRegister = user?.permissions.includes(AUTH_PERMISSIONS.registerAssetReadings) ?? false;
  const [tab, setTab] = useState<Tab>("measurements");
  const [editOpen, setEditOpen] = useState(false);
  const faenaQuery = useQuery({ queryKey: ["faena", code], enabled: Boolean(code), queryFn: () => apiFetch<FaenaRecord>(`/api/faenas/${encodeURIComponent(code)}`) });
  const equipmentQuery = useQuery({ queryKey: ["faena-equipment-overview", code], enabled: Boolean(code), queryFn: () => fetchAllEquipment(code) });

  if (!code) return <PageState title="Faena no válida" detail="La URL no contiene un código de faena válido." />;
  if (faenaQuery.isLoading) return <PageState title="Cargando faena…" />;
  if (faenaQuery.error) {
    const status = faenaQuery.error instanceof ApiError ? faenaQuery.error.status : null;
    const title = status === 403 ? "Acceso denegado" : status === 404 ? "Faena no encontrada" : "No fue posible cargar la faena";
    return <PageState title={title} detail={faenaQuery.error instanceof Error ? faenaQuery.error.message : undefined} onRetry={status === 403 || status === 404 ? undefined : () => void faenaQuery.refetch()} />;
  }
  const faena = faenaQuery.data;
  if (!faena) return <PageState title="Faena no encontrada" />;

  return <section className="space-y-4">
    <Link className="inline-flex items-center gap-2 text-sm font-medium text-teal-700 hover:underline dark:text-teal-300" to="/faenas"><ArrowLeft className="h-4 w-4" />Volver a Faenas</Link>
    <header className="panel flex flex-col gap-4 p-5 md:flex-row md:items-start md:justify-between">
      <div><div className="flex flex-wrap items-center gap-3"><h1 className="text-2xl font-semibold text-slate-950 dark:text-white">{faena.nombre}</h1><FaenaActivityBadge active={faena.activo} /></div><p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{[faena.codigo, faena.zona, faena.cliente].filter(Boolean).join(" · ")}</p></div>
      <div className="flex flex-wrap gap-2"><button className="secondary-button" onClick={() => { void faenaQuery.refetch(); void equipmentQuery.refetch(); }} type="button"><RefreshCw className="h-4 w-4" />Actualizar</button>{canEdit ? <button className="primary-button" onClick={() => setEditOpen(true)} type="button"><Pencil className="h-4 w-4" />Editar faena</button> : null}</div>
    </header>
    <div className="flex gap-1 border-b border-slate-200 dark:border-slate-800" role="tablist" aria-label="Vista de faena"><TabButton active={tab === "measurements"} onClick={() => setTab("measurements")}>Mediciones</TabButton><TabButton active={tab === "summary"} onClick={() => setTab("summary")}>Resumen</TabButton></div>
    {tab === "measurements" ? <MeasurementsPanel rows={equipmentQuery.data ?? []} isLoading={equipmentQuery.isLoading} error={equipmentQuery.error} onRetry={() => void equipmentQuery.refetch()} canRegister={canRegister} queryKey={["faena-equipment-overview", code]} /> : <section className="panel p-5"><FaenaSummary faena={faena} /></section>}
    <FaenaEditorDialog open={editOpen} mode="edit" faena={faena} onClose={() => setEditOpen(false)} onSaved={saved => { queryClient.setQueryData(["faena", code], saved); void queryClient.invalidateQueries({ queryKey: ["faenas"] }); }} />
  </section>;
}

function MeasurementsPanel({ rows, isLoading, error, onRetry, canRegister, queryKey }: { rows: EquipmentOverviewRow[]; isLoading: boolean; error: Error | null; onRetry: () => void; canRegister: boolean; queryKey: readonly string[] }) {
  const [search, setSearch] = useState("");
  const [measurement, setMeasurement] = useState<MeasurementFilter>("ALL");
  const visibleRows = useMemo(() => rows.filter(row => {
    const query = normalize(search);
    const matchesSearch = !query || normalize(row.code).includes(query) || normalize(row.name).includes(query);
    const matchesMeasurement = measurement === "ALL" || row.usageMeasurementType === measurement;
    return matchesSearch && matchesMeasurement;
  }), [measurement, rows, search]);
  return <section className="panel overflow-hidden">
    <div className="border-b border-slate-200 p-4 dark:border-slate-800"><div className="grid gap-3 lg:grid-cols-[minmax(280px,1fr)_auto] lg:items-end"><label className="text-sm font-medium text-slate-700 dark:text-slate-200">Buscar equipo<div className="relative mt-2"><Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-slate-400" /><input aria-label="Buscar equipo" className="input pl-9" placeholder="Código o nombre" value={search} onChange={event => setSearch(event.target.value)} /></div></label><div className="flex flex-wrap gap-2" aria-label="Filtrar por medición">{(["ALL", "HOROMETRO", "KILOMETRAJE"] as const).map(value => <button aria-pressed={measurement === value} className={measurement === value ? "primary-button" : "secondary-button"} key={value} onClick={() => setMeasurement(value)} type="button">{value === "ALL" ? "Todos" : value === "HOROMETRO" ? "Horómetros" : "Kilometrajes"}</button>)}</div></div><p className="mt-3 text-xs text-slate-500">{visibleRows.length} de {rows.length} equipos</p></div>
    {error ? <div className="m-4 error-banner">{error.message} <button className="underline" onClick={onRetry} type="button">Reintentar</button></div> : isLoading ? <p className="p-5 text-sm text-slate-500">Cargando equipos y mediciones…</p> : visibleRows.length === 0 ? <p className="p-5 text-sm text-slate-500">No hay equipos que coincidan con los filtros.</p> : <div className="overflow-x-auto"><table className="data-table min-w-[1040px]"><thead><tr><th className="min-w-[190px]">Equipo</th><th className="min-w-[145px]">Tipo</th><th className="min-w-[115px]">Estado</th><th className="min-w-[130px]">Medición</th><th className="min-w-[165px]">Última lectura</th><th className="min-w-[190px]">Nueva lectura</th><th className="min-w-[120px] whitespace-nowrap">Acción</th></tr></thead><tbody>{visibleRows.map(row => <MeasurementRow key={row.rowId} row={row} canRegister={canRegister} queryKey={queryKey} />)}</tbody></table></div>}
  </section>;
}

function MeasurementRow({ row, canRegister, queryKey }: { row: EquipmentOverviewRow; canRegister: boolean; queryKey: readonly string[] }) {
  const client = useQueryClient();
  const [value, setValue] = useState("");
  const numericValue = value.trim() === "" ? Number.NaN : Number(value);
  const isFiniteValue = Number.isFinite(numericValue) && numericValue >= 0;
  const isDecrease = isFiniteValue && row.lastReading != null && numericValue < row.lastReading;
  const canSubmit = canRegister && row.readingAvailable && isFiniteValue && !isDecrease;
  const save = useMutation({
    mutationFn: async () => {
      const root = row.rowType === "COMPOSITE_UNIT" ? "/api/operational-units/" : "/api/assets/";
      return apiFetch<AssetReadingResponse | UnitReadingResponse>(`${root}${encodeURIComponent(row.code)}/readings`, { method: "POST", body: JSON.stringify({ valor: numericValue, origen: "MANUAL" }) });
    },
    onSuccess: response => {
      const reading = "lecturas" in response ? response.lecturas[0] : response;
      client.setQueryData<EquipmentOverviewRow[]>(queryKey, current => current?.map(item => item.rowId === row.rowId ? { ...item, lastReading: reading.valor, lastReadingAtUtc: reading.fechaLecturaUtc } : item));
      setValue("");
      void client.invalidateQueries({ queryKey: ["equipment-overview"] });
      void client.invalidateQueries({ queryKey: [row.rowType === "COMPOSITE_UNIT" ? "equipment-unit-detail" : "equipment-asset-detail", row.code] });
    }
  });
  function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); if (canSubmit && !save.isPending) save.mutate(); }
  const unavailable = !row.readingAvailable ? row.readingUnavailableReason ?? "Lectura no disponible." : !canRegister ? "No tiene permiso para registrar lecturas." : null;
  return <tr>
    <td><span className="font-medium text-slate-950 dark:text-white">{row.name}</span><br /><span className="text-xs text-slate-500">{row.code}</span></td>
    <td>{row.equipmentTypeName}</td><td><OperationalStatusBadge code={row.operationalStateCode} name={row.operationalStateName} /></td>
    <td>{measurementLabel(row.usageMeasurementType)}{row.rowType === "COMPOSITE_UNIT" ? <span className="mt-1 block text-xs text-slate-500">Unidad compuesta</span> : null}</td>
    <td>{row.lastReading == null ? <span className="text-slate-500">Sin lecturas</span> : <><span className="font-medium">{formatNumber(row.lastReading)} {row.usageUnit}</span>{row.lastReadingAtUtc ? <span className="mt-1 block text-xs text-slate-500">{formatDate(row.lastReadingAtUtc)}</span> : null}</>}</td>
    <td><form id={`reading-${row.rowId}`} onSubmit={submit}><input aria-label={`Nueva lectura ${row.code}`} className="input min-w-32" disabled={!canRegister || !row.readingAvailable || save.isPending} min="0" step="0.01" type="number" value={value} onChange={event => { setValue(event.target.value); save.reset(); }} />{isDecrease ? <p className="mt-1 max-w-52 text-xs text-amber-700 dark:text-amber-300">Debe ser igual o mayor que {formatNumber(row.lastReading!)}.</p> : unavailable ? <p className="mt-1 max-w-52 text-xs text-slate-500">{unavailable}</p> : null}{save.error ? <p className="mt-1 max-w-52 text-xs text-red-700 dark:text-red-300" role="alert">{save.error instanceof Error ? save.error.message : "No fue posible guardar."}</p> : null}{save.isSuccess ? <p className="mt-1 text-xs text-emerald-700 dark:text-emerald-300" role="status">Lectura guardada.</p> : null}</form></td>
    <td className="whitespace-nowrap"><button aria-label={`Guardar ${row.code}`} className="primary-button whitespace-nowrap" disabled={!canSubmit || save.isPending} form={`reading-${row.rowId}`} type="submit">{save.isPending ? "Guardando…" : "Guardar"}</button></td>
  </tr>;
}

async function fetchAllEquipment(code: string) {
  const rows: EquipmentOverviewRow[] = [];
  let page = 1;
  let hasNextPage = true;
  while (hasNextPage) {
    const params = new URLSearchParams({ faenaCodigo: code, page: String(page), pageSize: "100" });
    const response = await apiFetch<Page<EquipmentOverviewRow>>(`/api/assets/equipment-overview?${params.toString()}`);
    if (!response || !Array.isArray(response.items)) throw new Error("La respuesta de equipos tiene un formato inválido.");
    rows.push(...response.items);
    hasNextPage = response.hasNextPage;
    page += 1;
  }
  const seen = new Set<string>();
  return rows.filter(row => !seen.has(row.rowId) && (seen.add(row.rowId), true));
}

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) { return <button aria-selected={active} className={`border-b-2 px-4 py-3 text-sm font-semibold ${active ? "border-teal-600 text-teal-700 dark:text-teal-300" : "border-transparent text-slate-500 hover:text-slate-800 dark:hover:text-slate-200"}`} onClick={onClick} role="tab" type="button">{children}</button>; }
function PageState({ title, detail, onRetry }: { title: string; detail?: string; onRetry?: () => void }) { return <section className="panel p-6"><h1 className="text-xl font-semibold text-slate-950 dark:text-white">{title}</h1>{detail ? <p className="mt-2 text-sm text-slate-500">{detail}</p> : null}<div className="mt-4 flex gap-2">{onRetry ? <button className="primary-button" onClick={onRetry} type="button">Reintentar</button> : null}<Link className="secondary-button" to="/faenas">Volver a Faenas</Link></div></section>; }
const normalize = (value: string) => value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").toLocaleLowerCase().trim();
const formatNumber = (value: number) => new Intl.NumberFormat("es-CL", { maximumFractionDigits: 2 }).format(value);
const formatDate = (value: string) => new Intl.DateTimeFormat("es-CL", { dateStyle: "short", timeStyle: "short", timeZone: "America/Santiago" }).format(new Date(value));
const measurementLabel = (value?: string | null) => value === "HOROMETRO" ? "Horómetro" : value === "KILOMETRAJE" ? "Kilometraje" : "Sin medición configurada";
