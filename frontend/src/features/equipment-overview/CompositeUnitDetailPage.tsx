import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { ArrowLeft, FileText, Truck, Wrench } from "lucide-react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { apiFetch } from "../auth/authStore";
import { na } from "./formatters";
import { UnitCompositionDialogs, type UnitComponent } from "./UnitCompositionDialogs";

type Unit = {
  codigo: string;
  nombre: string;
  tipoUnidadCodigo: string;
  faenaCodigo?: string | null;
  estadoOperacionalCodigo: string;
  criticidad?: string | null;
  estadoDerivado?: { estadoCodigo: string; activoRestrictivoCodigo?: string | null; rolRestrictivoCodigo?: string | null; motivo?: string | null } | null;
  composicion: { completa: boolean; faltantes: string[]; vigentes: UnitComponent[]; historial: UnitComponent[] };
};

const tabs = [["resumen", "Resumen"], ["composicion", "Composición"], ["historial", "Historial de composición"], ["documentos", "Documentos de componentes"]] as const;

export function CompositeUnitDetailPage() {
  const { code = "" } = useParams();
  const navigate = useNavigate();
  const [search, setSearch] = useSearchParams();
  const [compositionMode, setCompositionMode] = useState<"mount" | "replace" | "unmount" | null>(null);
  const tab = search.get("tab") || "resumen";
  const query = useQuery({ queryKey: ["equipment-unit-detail", code], enabled: !!code, queryFn: () => apiFetch<Unit>("/api/operational-units/" + encodeURIComponent(code)) });
  const unit = query.data;
  if (query.isLoading) return <p className="panel p-5 text-sm text-slate-500">Cargando unidad…</p>;
  if (!unit) return <p className="error-banner">No fue posible cargar la unidad.</p>;

  const setTab = (next: string) => setSearch({ tab: next });
  return <section className="space-y-4">
    <div className="text-xs text-slate-500"><Link className="text-teal-700" to="/equipos">Equipos</Link> / {unit.nombre}</div>
    <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_320px]">
      <section className="overflow-hidden rounded-xl border border-slate-200 bg-white shadow-sm">
        <header className="p-5">
          <div className="flex flex-wrap justify-between gap-4">
            <div className="flex gap-4"><span className="grid h-14 w-14 place-items-center rounded-xl bg-sky-100 text-sky-700"><Truck className="h-7 w-7" /></span><div><p className="text-[11px] font-extrabold uppercase tracking-[.12em] text-teal-700">Unidad operativa compuesta</p><h1 className="mt-1 text-2xl font-semibold text-slate-900">{unit.nombre}</h1><p className="text-sm text-slate-500">{unit.codigo} · {na(unit.faenaCodigo)}</p><div className="mt-2 flex flex-wrap gap-2"><span className="rounded-full bg-slate-100 px-2 py-1 text-xs font-bold">{unit.estadoDerivado?.estadoCodigo || unit.estadoOperacionalCodigo}</span><span className={"rounded-full px-2 py-1 text-xs font-bold " + (unit.composicion.completa ? "bg-emerald-100 text-emerald-800" : "bg-amber-100 text-amber-800")}>{unit.composicion.completa ? "Composición completa" : "Composición incompleta"}</span></div></div></div>
            <button className="primary-button" type="button" onClick={() => setCompositionMode("mount")}><Wrench className="h-4 w-4" />Gestionar composición</button>
          </div>
        </header>
        <nav className="flex overflow-auto border-y border-slate-200 bg-slate-50">{tabs.map(([key, label]) => <button className={"h-12 shrink-0 border-b-2 px-4 text-sm font-semibold " + (tab === key ? "border-teal-500 text-teal-700" : "border-transparent text-slate-500")} key={key} onClick={() => setTab(key)} type="button">{label}</button>)}</nav>
        <div className="p-5">{tab === "resumen" ? <UnitSummary unit={unit} /> : tab === "composicion" ? <Composition unit={unit} openAsset={asset => navigate("/equipos/activos/" + encodeURIComponent(asset))} onAction={setCompositionMode} /> : tab === "historial" ? <CompositionHistory rows={unit.composicion.historial} /> : <UnitDocuments components={unit.composicion.vigentes} />}</div>
      </section>
      <aside className="space-y-3">
        <article className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"><h2 className="font-semibold">Acciones de la unidad</h2><div className="mt-3 grid gap-2"><button className="secondary-button" type="button" onClick={() => setCompositionMode("mount")}>Montar componente</button><button className="secondary-button" type="button" disabled={unit.composicion.vigentes.length === 0} onClick={() => setCompositionMode("replace")}>Reemplazar componente</button><button className="secondary-button text-red-700" type="button" disabled={unit.composicion.vigentes.length === 0} onClick={() => setCompositionMode("unmount")}>Desmontar componente</button></div></article>
        <article className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"><h2 className="font-semibold">Disponibilidad</h2><p className="mt-2 text-3xl font-semibold">NA</p><p className="mt-1 text-xs text-slate-500">No existe una fuente consolidada disponible.</p></article>
        <article className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"><h2 className="font-semibold">Estado derivado</h2><p className="mt-2 text-sm text-slate-600">{unit.estadoDerivado?.motivo || "El componente más restrictivo prevalece cuando existe estado derivado."}</p></article>
      </aside>
    </div>
    <button className="secondary-button" type="button" onClick={() => navigate(-1)}><ArrowLeft className="h-4 w-4" />Volver</button>
    <UnitCompositionDialogs unitCode={unit.codigo} unitSiteCode={unit.faenaCodigo} components={unit.composicion.vigentes} mode={compositionMode} onClose={() => setCompositionMode(null)} />
  </section>;
}

function UnitSummary({ unit }: { unit: Unit }) {
  return <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">{[["Tipo", unit.tipoUnidadCodigo], ["Faena", unit.faenaCodigo], ["Estado", unit.estadoDerivado?.estadoCodigo || unit.estadoOperacionalCodigo], ["Composición", unit.composicion.completa ? "Completa" : "Incompleta"], ["Componentes vigentes", String(unit.composicion.vigentes.length)], ["Preventivo", "NA"], ["Disponibilidad", "NA"], ["Criticidad", unit.criticidad]].map(([label, value]) => <article className="rounded-lg border border-slate-200 bg-slate-50 p-3" key={label}><p className="text-[10px] font-extrabold uppercase tracking-[.05em] text-slate-500">{label}</p><b className="mt-1 block text-sm">{na(value)}</b></article>)}</div>;
}

function Composition({ unit, openAsset, onAction }: { unit: Unit; openAsset: (code: string) => void; onAction: (mode: "mount" | "replace" | "unmount") => void }) {
  const chasis = unit.composicion.vigentes.find(item => item.rolComponenteCodigo.toUpperCase().includes("CHASIS"));
  const fabrica = unit.composicion.vigentes.find(item => item !== chasis);
  return <><div className="rounded-xl border border-dashed border-slate-300 bg-slate-50 p-5"><div className="grid items-center gap-3 lg:grid-cols-[1fr_90px_1fr]"><ComponentCard label="Chasis" component={chasis} openAsset={openAsset} onAction={onAction} /><div className="text-center text-xs text-slate-500"><Truck className="mx-auto mb-2 h-7 w-7 text-sky-700" /><div className="h-0.5 bg-slate-400" /><b className="mt-2 block">Composición vigente</b></div><ComponentCard label="Fábrica" component={fabrica} openAsset={openAsset} onAction={onAction} /></div></div><div className="mt-4 flex flex-wrap gap-2"><button className="secondary-button" type="button" onClick={() => onAction("mount")}>Montar componente</button><button className="secondary-button" disabled={!unit.composicion.vigentes.length} type="button" onClick={() => onAction("replace")}>Reemplazar componente</button></div></>;
}

function ComponentCard({ label, component, openAsset, onAction }: { label: string; component?: UnitComponent; openAsset: (code: string) => void; onAction: (mode: "replace" | "unmount") => void }) {
  return <article className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm"><p className="text-[11px] font-extrabold uppercase tracking-[.12em] text-teal-700">Rol: {label}</p>{component ? <><h3 className="mt-1 font-semibold text-slate-900">{component.activoNombre || "NA"}</h3><p className="text-sm text-slate-500">{component.activoCodigo}</p><div className="mt-3 grid grid-cols-2 gap-2 text-xs"><span className="rounded bg-slate-50 p-2">Estado<b className="mt-1 block">{na(component.estadoOperacionalCodigo)}</b></span><span className="rounded bg-slate-50 p-2">Medición<b className="mt-1 block">NA</b></span></div><div className="mt-4 flex gap-2"><button className="secondary-button text-xs" type="button" onClick={() => openAsset(component.activoCodigo)}>Ver detalle</button><button className="secondary-button text-xs" type="button" onClick={() => onAction("replace")}>Reemplazar</button><button className="secondary-button text-xs text-red-700" type="button" onClick={() => onAction("unmount")}>Desmontar</button></div></> : <p className="mt-3 text-sm text-slate-500">NA</p>}</article>;
}

function CompositionHistory({ rows }: { rows: UnitComponent[] }) {
  return <table className="min-w-full text-sm"><thead><tr><th>Fecha</th><th>Rol</th><th>Activo</th><th>Acción</th><th>Motivo</th></tr></thead><tbody>{rows.map((row, index) => <tr className="border-t" key={row.activoCodigo + index}><td className="p-2">{new Intl.DateTimeFormat("es-CL").format(new Date(row.fechaMontajeUtc))}</td><td className="p-2">{row.rolComponenteCodigo}</td><td className="p-2">{row.activoNombre || row.activoCodigo}</td><td className="p-2">{row.fechaDesmontajeUtc ? "Desmontaje" : "Montaje"}</td><td className="p-2">{na(row.fechaDesmontajeUtc ? row.motivoDesmontaje : row.motivoMontaje)}</td></tr>)}</tbody></table>;
}

function UnitDocuments({ components }: { components: UnitComponent[] }) {
  return <><p className="mb-3 text-sm text-slate-500">Los documentos pertenecen a sus componentes y se gestionan desde su ficha.</p><div className="grid gap-2">{components.map(component => <Link className="rounded-lg border border-slate-200 p-3 text-sm font-semibold text-teal-700 hover:bg-teal-50" key={component.activoCodigo} to={"/equipos/activos/" + encodeURIComponent(component.activoCodigo) + "?tab=documentos"}><FileText className="mr-2 inline h-4 w-4" />{component.activoNombre || component.activoCodigo} · Abrir documentos</Link>)}</div></>;
}