import { ExternalLink, History, Pencil, Replace, ShieldCheck, XCircle } from "lucide-react";
import type { DocumentRecord } from "./documentFormTypes";

type Capabilities = { canManage: boolean; canValidate: boolean };
type Props = { document: DocumentRecord; capabilities: Capabilities; onEdit: () => void; onVersions: () => void; onReplace: () => void; onValidate: () => void; onReject: () => void; onAnnul: () => void };

export function DocumentActionsMenu({ document, capabilities, onEdit, onVersions, onReplace, onValidate, onReject, onAnnul }: Props) {
  const active = !document.esHistorico && document.estado !== "Anulado" && document.estado !== "Reemplazado";
  return <div className="flex flex-wrap gap-1">
    {document.sharePointUrl ? <a aria-label="Abrir" className="secondary-button text-xs" href={document.sharePointUrl} rel="noreferrer" target="_blank"><ExternalLink className="h-3 w-3" />Abrir</a> : null}
    {document.sharePointUrl ? <a aria-label="Descargar" className="secondary-button text-xs" download href={document.sharePointUrl}>Descargar</a> : null}
    <button aria-label="Versiones" className="secondary-button text-xs" type="button" onClick={onVersions}><History className="h-3 w-3" />Versiones</button>
    {capabilities.canManage && active ? <><button aria-label="Editar" className="secondary-button text-xs" type="button" onClick={onEdit}><Pencil className="h-3 w-3" />Editar</button><button aria-label="Reemplazar" className="secondary-button text-xs" type="button" onClick={onReplace}><Replace className="h-3 w-3" />Reemplazar</button><button aria-label="Anular" className="secondary-button text-xs text-red-700" type="button" onClick={onAnnul}>Anular</button></> : null}
    {capabilities.canValidate && active && document.estado === "PendienteValidacion" ? <><button aria-label="Validar" className="secondary-button text-xs" type="button" onClick={onValidate}><ShieldCheck className="h-3 w-3" />Validar</button><button aria-label="Rechazar" className="secondary-button text-xs text-red-700" type="button" onClick={onReject}><XCircle className="h-3 w-3" />Rechazar</button></> : null}
  </div>;
}
