import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { FileText, RefreshCw } from "lucide-react";
import { AssetDocumentManager } from "./components/AssetDocumentManager";
import { DocumentStatusBadge } from "./components/DocumentStatusBadge";
import { documentApi } from "./components/documentApi";
import type { DocumentRecord } from "./components/documentFormTypes";

type Filters = { entityCode: string; type: string; status: string; history: boolean };
const empty: Filters = { entityCode: "", type: "", status: "", history: false };

export function DocumentsPage() {
  const [draft, setDraft] = useState<Filters>(empty);
  const [filters, setFilters] = useState<Filters>(empty);
  const [selectedAsset, setSelectedAsset] = useState("");
  const query = useQuery({
    queryKey: ["documents", filters],
    queryFn: () => {
      const parameters = new URLSearchParams({ entidadTipo: "Activo", includeHistorical: String(filters.history) });
      if (filters.entityCode) parameters.set("entidadCodigo", filters.entityCode);
      if (filters.type) parameters.set("tipoDocumento", filters.type);
      if (filters.status) parameters.set("estado", filters.status);
      return documentApi.list(parameters);
    },
  });
  const documents = query.data ?? [];
  return <section className="space-y-5">
    <header className="flex flex-wrap items-end justify-between gap-3"><div><h1 className="text-2xl font-semibold">Documentos</h1><p className="text-sm text-slate-500">Consulta global y gestion documental reutilizable por activo.</p></div><button className="secondary-button" type="button" onClick={() => void query.refetch()}><RefreshCw className="h-4 w-4" />Actualizar</button></header>
    <form className="panel grid gap-3 md:grid-cols-4" onSubmit={event => { event.preventDefault(); setFilters({ ...draft }); }}>
      <label className="text-sm font-medium">Activo<input className="input mt-1" value={draft.entityCode} onChange={event => setDraft(current => ({ ...current, entityCode: event.target.value }))} /></label>
      <label className="text-sm font-medium">Tipo documental<input className="input mt-1" value={draft.type} onChange={event => setDraft(current => ({ ...current, type: event.target.value }))} /></label>
      <label className="text-sm font-medium">Estado<select className="input mt-1" value={draft.status} onChange={event => setDraft(current => ({ ...current, status: event.target.value }))}><option value="">Todos</option>{["Vigente", "PorVencer", "Vencido", "PendienteCarga", "PendienteValidacion", "Rechazado", "Reemplazado", "Anulado"].map(status => <option key={status}>{status}</option>)}</select></label>
      <label className="flex items-end gap-2 text-sm"><input checked={draft.history} type="checkbox" onChange={event => setDraft(current => ({ ...current, history: event.target.checked }))} />Incluir hist?ricos</label>
      <button className="primary-button md:col-span-4" type="submit">Aplicar filtros</button>
    </form>
    {query.error ? <p className="error-banner">{query.error instanceof Error ? query.error.message : "No fue posible cargar documentos."}</p> : null}
    <div className="overflow-hidden rounded border bg-white"><table className="min-w-full text-sm"><thead><tr><th>Entidad</th><th>Tipo</th><th>Estado</th><th>Vencimiento</th><th /></tr></thead><tbody>{query.isLoading ? <tr><td className="p-4" colSpan={5}>Cargando...</td></tr> : documents.length ? documents.map(document => <tr className={"border-t " + (selectedAsset === document.entidadCodigo ? "bg-teal-50" : "")} key={document.documentoId}><td className="p-3">{document.entidadCodigo}</td><td className="p-3">{document.tipoDocumento}</td><td className="p-3"><DocumentStatusBadge status={document.estado} /></td><td className="p-3">{document.fechaVencimiento ?? "NA"}</td><td className="p-3"><button className="text-teal-700 underline" type="button" onClick={() => setSelectedAsset(document.entidadCodigo)}>Gestionar</button></td></tr>) : <tr><td className="p-6 text-center text-slate-500" colSpan={5}>No hay documentos.</td></tr>}</tbody></table></div>
    <section className="panel"><h2 className="flex items-center gap-2 text-lg font-semibold"><FileText className="h-5 w-5" />Gestion del activo</h2>{selectedAsset || filters.entityCode ? <AssetDocumentManager assetCode={selectedAsset || filters.entityCode} /> : <p className="mt-2 text-sm text-slate-500">Selecciona un activo de la tabla o filtra por codigo para crear y gestionar sus documentos.</p>}</section>
  </section>;
}
