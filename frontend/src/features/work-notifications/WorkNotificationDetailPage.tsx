import { useEffect, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { DocumentLink } from "../../shared/security/DocumentLink";
import { apiFetch } from "../auth/authStore";
import type { Notice, NoticeStatus, TechnicalNode } from "./WorkNotificationsPage";

const labels: Record<NoticeStatus,string> = { Borrador:"Borrador", PendientePlanificacion:"Pendiente planificación", DevueltoFaena:"Devuelto a faena", EnGestion:"En gestión", Cerrado:"Cerrado", Rechazado:"Rechazado", Anulado:"Anulado" };
type Evidence = { id: string; fileId: string; fileKey: string; nombreArchivo: string; contentType: string; sizeBytes: number };
type WorkItem = { id: string; secuencia: number; descripcion: string; observaciones?: string; affectedAssetCodeSnapshot: string; affectedAssetNameSnapshot: string; rolComponente?: string; technicalSystemId?: string | null; technicalSubsystemId?: string | null; technicalComponentId?: string | null; estado: string; evidencias: Evidence[] };
type DetailNotice = Omit<Notice, "trabajos"> & { observaciones?: string; trabajos: WorkItem[]; historial: Array<{ id: string; estadoOrigen?: string; estadoDestino: string; usuarioId: string; fechaUtc: string; motivo?: string }> };

export function WorkNotificationDetailPage() {
  const { avisoId="" } = useParams();
  const [notice,setNotice] = useState<DetailNotice|null>(null);
  const [nodes,setNodes] = useState<TechnicalNode[]>([]);
  const [reason,setReason] = useState("");
  const [error,setError] = useState<string|null>(null);
  const [uploadingItem,setUploadingItem] = useState<string|null>(null);

  const load = async () => {
    try {
      setError(null);
      const [loadedNotice, loadedNodes] = await Promise.all([
        apiFetch<DetailNotice>(`/api/work-notifications/${encodeURIComponent(avisoId)}`),
        apiFetch<TechnicalNode[]>("/api/technical-hierarchy/nodes?includeObsolete=true")
      ]);
      setNotice(loadedNotice);
      setNodes(loadedNodes);
    } catch (reasonValue) { setError(reasonValue instanceof Error ? reasonValue.message : "No fue posible cargar el aviso."); }
  };
  useEffect(() => { void load(); }, [avisoId]);

  const action = async (path:string, body?:object) => {
    try {
      setError(null);
      await apiFetch(`/api/work-notifications/${encodeURIComponent(avisoId)}${path}`, { method:"POST", body:body ? JSON.stringify(body) : undefined });
      setReason("");
      await load();
    } catch (reasonValue) { setError(reasonValue instanceof Error ? reasonValue.message : "No fue posible completar la acción."); }
  };
  const upload = async (item: WorkItem, files: FileList | null) => {
    if (!files?.length || uploadingItem) return;
    setUploadingItem(item.id);
    const failures: string[] = [];
    for (const file of Array.from(files)) {
      const body = new FormData();
      body.append("file", file, file.name);
      try { await apiFetch(`/api/work-notifications/${encodeURIComponent(avisoId)}/items/${encodeURIComponent(item.id)}/evidences`, { method: "POST", body }); }
      catch (reasonValue) { failures.push(`${file.name}: ${reasonValue instanceof Error ? reasonValue.message : "error de carga"}`); }
    }
    setUploadingItem(null);
    if (failures.length) setError(`El aviso no se duplicó. No fue posible cargar: ${failures.join("; ")}`);
    await load();
  };

  if (!notice) return <p className="text-slate-600 dark:text-slate-300">{error ?? "Cargando aviso…"}</p>;
  const requiresReason = ["PendientePlanificacion","EnGestion","Borrador","DevueltoFaena"].includes(notice.estado);
  const canManageEvidence = ["Borrador","DevueltoFaena"].includes(notice.estado);
  const nodeName = (id?: string | null) => nodes.find(node => node.id === id)?.nombre ?? "—";

  return <section className="space-y-4 text-slate-900 dark:text-slate-100">
    <Link className="text-sm text-teal-700 underline dark:text-teal-300" to="/avisos">← Volver a Avisos</Link>
    {error && <p role="alert" className="rounded border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">{error}</p>}
    <header className="panel p-5"><p className="text-sm text-slate-500 dark:text-slate-400">Aviso {notice.avisoId}</p><h1 className="text-2xl font-semibold">{labels[notice.estado]}</h1><div className="mt-3 grid gap-2 md:grid-cols-4"><p>Faena: <b>{notice.faenaCodigo}</b></p><p>Equipo: <b>{notice.activoCodigo ?? notice.unidadOperativaCodigo}</b></p><p>Fecha: <b>{new Date(notice.fechaDeteccion).toLocaleString()}</b></p><p>Trabajos: <b>{notice.trabajos.length}</b></p></div></header>
    <section className="panel p-5"><h2 className="text-lg font-semibold">Trabajos</h2><div className="mt-3 space-y-4">{notice.trabajos.map(work => <article key={work.id} className="rounded-xl border border-slate-200 p-4 dark:border-slate-700 dark:bg-slate-950/40">
      <div className="flex flex-wrap items-center justify-between gap-2"><h3 className="font-semibold">Trabajo {work.secuencia}</h3><span className="rounded-full bg-slate-100 px-2 py-1 text-xs font-semibold dark:bg-slate-800">{work.estado}</span></div>
      <dl className="mt-3 grid gap-3 text-sm sm:grid-cols-2 lg:grid-cols-4"><Detail label="Activo afectado" value={`${work.affectedAssetNameSnapshot} (${work.affectedAssetCodeSnapshot})`}/>{work.rolComponente && <Detail label="Fábrica / Chasis" value={work.rolComponente}/>}<Detail label="Sistema" value={nodeName(work.technicalSystemId)}/><Detail label="Subsistema" value={nodeName(work.technicalSubsystemId)}/><Detail label="Componente" value={nodeName(work.technicalComponentId)}/></dl>
      <div className="mt-3 grid gap-3 text-sm md:grid-cols-2"><Detail label="Descripción de falla / trabajo requerido" value={work.descripcion}/><Detail label="Observaciones" value={work.observaciones ?? "—"}/></div>
      <div className="mt-4 border-t border-slate-200 pt-3 dark:border-slate-700"><div className="flex flex-wrap items-center justify-between gap-2"><h4 className="text-sm font-semibold">Evidencias</h4>{canManageEvidence && <label className={`secondary-button inline-flex cursor-pointer text-xs ${uploadingItem ? "opacity-60" : ""}`}>{uploadingItem === work.id ? "Cargando…" : "+ Agregar fotografías"}<input aria-label={`Agregar evidencias trabajo ${work.secuencia}`} accept=".png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp" className="sr-only" disabled={Boolean(uploadingItem)} multiple onChange={event => { void upload(work, event.target.files); event.target.value = ""; }} type="file"/></label>}</div>
        {work.evidencias.length === 0 ? <p className="mt-2 text-sm text-slate-500 dark:text-slate-400">Sin evidencias</p> : <ul className="mt-2 grid gap-2 sm:grid-cols-2">{work.evidencias.map(evidence => <li className="rounded-md border border-slate-200 bg-slate-50 p-3 dark:border-slate-700 dark:bg-slate-900" key={evidence.id}><DocumentLink className="font-medium text-teal-700 underline dark:text-teal-300" href={`/api/sharepoint/download?fileKey=${encodeURIComponent(evidence.fileKey)}`}>{evidence.nombreArchivo}</DocumentLink><p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{evidence.contentType} · {formatBytes(evidence.sizeBytes)}</p></li>)}</ul>}
      </div>
    </article>)}</div></section>
    <section className="panel p-5"><h2 className="font-semibold">Gestión de Planificación</h2>{requiresReason && <input className="input mt-3" placeholder="Motivo (obligatorio para devolver, rechazar o anular)" value={reason} onChange={event => setReason(event.target.value)}/>}<div className="mt-3 flex flex-wrap gap-2">{["Borrador","DevueltoFaena"].includes(notice.estado) && <button className="primary-button" onClick={() => void action("/submit")}>Enviar a planificación</button>}{notice.estado === "PendientePlanificacion" && <><button className="primary-button" onClick={() => void action("/accept")}>Aceptar para gestión</button><button className="secondary-button" onClick={() => void action("/return",{motivo:reason})}>Devolver a faena</button><button className="danger-button" onClick={() => void action("/reject",{motivo:reason})}>Rechazar</button></>}{!["Cerrado","Rechazado","Anulado"].includes(notice.estado) && <button className="danger-button" onClick={() => void action("/annul",{motivo:reason})}>Anular</button>}</div></section>
    <section className="panel p-5"><h2 className="font-semibold">Historial</h2>{notice.historial.map(history => <p key={history.id} className="mt-2 text-sm">{new Date(history.fechaUtc).toLocaleString()} · {history.usuarioId}: {history.estadoOrigen ?? "—"} → {history.estadoDestino}{history.motivo ? ` (${history.motivo})` : ""}</p>)}</section>
  </section>;
}

function Detail({ label, value }: { label: string; value: string }) { return <div><dt className="text-xs font-medium text-slate-500 dark:text-slate-400">{label}</dt><dd className="mt-0.5 text-sm text-slate-900 dark:text-slate-100">{value}</dd></div>; }
function formatBytes(bytes: number) { if (bytes < 1024) return `${bytes} B`; if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`; return `${(bytes / (1024 * 1024)).toFixed(1)} MB`; }
