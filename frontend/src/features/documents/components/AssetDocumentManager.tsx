import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { FilePlus } from "lucide-react";
import { AUTH_PERMISSIONS, useAuthStore } from "../../auth/authStore";
import { DocumentActionsMenu } from "./DocumentActionsMenu";
import { DocumentAnnulDialog } from "./DocumentAnnulDialog";
import { DocumentEditorDialog } from "./DocumentEditorDialog";
import { DocumentReplacementDialog } from "./DocumentReplacementDialog";
import { DocumentReviewDialog } from "./DocumentReviewDialog";
import { DocumentStatusBadge } from "./DocumentStatusBadge";
import { DocumentVersionsDialog } from "./DocumentVersionsDialog";
import { documentApi } from "./documentApi";
import { documentQueryKeys, documentStatusLabel, type DocumentRecord } from "./documentFormTypes";

const date = (value?: string | null) => value ? new Intl.DateTimeFormat("es-CL").format(new Date(value)) : "NA";
const errorText = (error: unknown) => error instanceof Error ? error.message : "No fue posible cargar documentos.";
type Props = { assetCode: string; ownerLabel?: string; matrix?: { tipoDocumento: string; estado: string }[] };

export function AssetDocumentManager({ assetCode, ownerLabel, matrix }: Props) {
  const user = useAuthStore(state => state.user);
  const canManage = user?.permissions.includes(AUTH_PERMISSIONS.manageDocuments) ?? false;
  const canValidate = user?.permissions.includes(AUTH_PERMISSIONS.validateDocuments) ?? false;
  const [editor, setEditor] = useState<"create" | "edit" | null>(null);
  const [review, setReview] = useState<"validate" | "reject" | null>(null);
  const [showReplacement, setShowReplacement] = useState(false);
  const [showAnnul, setShowAnnul] = useState(false);
  const [versionsId, setVersionsId] = useState<string | null>(null);
  const [selected, setSelected] = useState<DocumentRecord | null>(null);
  const documents = useQuery({ queryKey: documentQueryKeys.list(assetCode, true), queryFn: () => documentApi.listForAsset(assetCode) });
  const closeSelection = () => setSelected(null);
  const closeEditor = () => { setEditor(null); closeSelection(); };
  const closeReview = () => { setReview(null); closeSelection(); };
  const closeReplacement = () => { setShowReplacement(false); closeSelection(); };
  const closeAnnul = () => { setShowAnnul(false); closeSelection(); };

  return <section className="space-y-4">
    <div className="grid gap-3 sm:grid-cols-3">{[["Vigentes", documents.data?.filter(item => item.estado === "Vigente").length ?? 0], ["Por vencer", documents.data?.filter(item => item.estado === "PorVencer").length ?? 0], ["Vencidos", documents.data?.filter(item => item.estado === "Vencido").length ?? 0]].map(([name, value]) => <article className="rounded-lg border border-slate-200 bg-slate-50 p-3" key={String(name)}><small className="text-slate-500">{name}</small><b className="block text-xl">{value}</b></article>)}</div>
    {matrix ? <p className="text-xs text-slate-500">Matriz: {matrix.map(item => item.tipoDocumento + " - " + documentStatusLabel(item.estado)).join(" | ") || "Sin requisitos aplicables"}</p> : null}
    <div className="flex justify-between gap-2"><p className="text-sm text-slate-500">{ownerLabel ? "Propietario: " + ownerLabel : "Activo: " + assetCode}</p>{canManage ? <button className="primary-button" type="button" onClick={() => setEditor("create")}><FilePlus className="h-4 w-4" />Cargar documento</button> : null}</div>
    {documents.error ? <p className="error-banner">{errorText(documents.error)}</p> : null}
    <table className="min-w-full text-sm"><thead><tr><th>Documento</th><th>Estado</th><th>Emision</th><th>Vencimiento</th><th>Acciones</th></tr></thead><tbody>{documents.isLoading ? <tr><td className="p-3" colSpan={5}>Cargando...</td></tr> : documents.data?.length ? documents.data.map(document => <tr className="border-t" key={document.documentoId}><td className="p-2">{document.tipoDocumento}{document.versionVigente ? " - v" + document.versionVigente : ""}</td><td className="p-2"><DocumentStatusBadge status={document.estado} /></td><td className="p-2">{date(document.fechaEmision)}</td><td className="p-2">{date(document.fechaVencimiento)}</td><td className="p-2"><DocumentActionsMenu document={document} capabilities={{ canManage, canValidate }} onVersions={() => setVersionsId(document.documentoId)} onEdit={() => { setSelected(document); setEditor("edit"); }} onReplace={() => { setSelected(document); setShowReplacement(true); }} onValidate={() => { setSelected(document); setReview("validate"); }} onReject={() => { setSelected(document); setReview("reject"); }} onAnnul={() => { setSelected(document); setShowAnnul(true); }} /></td></tr>) : <tr><td className="p-4 text-slate-500" colSpan={5}>No hay documentos para este activo.</td></tr>}</tbody></table>
    <DocumentEditorDialog open={editor !== null} mode={editor ?? "create"} document={selected} entityType="Activo" entityCode={assetCode} lockEntity onClose={closeEditor} onSuccess={closeEditor} />
    <DocumentVersionsDialog open={!!versionsId} documentId={versionsId} onClose={() => setVersionsId(null)} />
    <DocumentReplacementDialog open={showReplacement} document={selected} onClose={closeReplacement} onSuccess={closeReplacement} />
    <DocumentReviewDialog open={review !== null} action={review ?? "validate"} document={selected} onClose={closeReview} onSuccess={closeReview} />
    <DocumentAnnulDialog open={showAnnul} document={selected} onClose={closeAnnul} onSuccess={closeAnnul} />
  </section>;
}
