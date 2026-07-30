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
import { documentQueryKeys, type DocumentMatrixRow, type DocumentRecord } from "./documentFormTypes";

const date = (value?: string | null) => value ? new Intl.DateTimeFormat("es-CL").format(new Date(value)) : "NA";
const errorText = (error: unknown) => error instanceof Error ? error.message : "No fue posible cargar documentos.";
type Props = { assetCode: string; ownerLabel?: string; matrix?: DocumentMatrixRow[] };
type RequirementSelection = { type: string; documentTypeName?: string | null; assetName?: string | null; faenaName?: string | null; requiresExpirationDate: boolean };

export function AssetDocumentManager({ assetCode, ownerLabel, matrix: suppliedMatrix }: Props) {
  const user = useAuthStore(state => state.user);
  const roles = user?.roles ?? [];
  const canManage = (user?.permissions.includes(AUTH_PERMISSIONS.manageDocuments) ?? false) || roles.some(role => ["Administrador", "Planificador"].includes(role));
  const canValidate = (user?.permissions.includes(AUTH_PERMISSIONS.validateDocuments) ?? false) || roles.includes("Administrador");
  const [createRequirement, setCreateRequirement] = useState<RequirementSelection | null>(null);
  const [editing, setEditing] = useState(false);
  const [review, setReview] = useState<"validate" | "reject" | null>(null);
  const [showReplacement, setShowReplacement] = useState(false);
  const [showAnnul, setShowAnnul] = useState(false);
  const [versionsId, setVersionsId] = useState<string | null>(null);
  const [selected, setSelected] = useState<DocumentRecord | null>(null);
  const documents = useQuery({ queryKey: documentQueryKeys.list(assetCode, true), queryFn: () => documentApi.listForAsset(assetCode) });
  const fetchedMatrix = useQuery({ queryKey: documentQueryKeys.matrix(assetCode), queryFn: () => documentApi.matrixForAsset(assetCode), enabled: suppliedMatrix === undefined });
  const types = useQuery({ queryKey: documentQueryKeys.types, queryFn: documentApi.types });
  const requirements = suppliedMatrix ?? fetchedMatrix.data ?? [];
  const typeName = (code: string) => types.data?.find(type => type.codigo === code)?.nombre ?? code;
  const currentDocument = (type: string) => (documents.data ?? []).filter(document => document.tipoDocumento === type && !document.esHistorico && document.estado !== "Anulado" && document.estado !== "Reemplazado").sort((a, b) => b.fechaCargaUtc.localeCompare(a.fechaCargaUtc))[0] ?? null;
  const selectionFor = (row: DocumentMatrixRow): RequirementSelection => ({ type: row.tipoDocumento, documentTypeName: row.tipoDocumentalNombre ?? typeName(row.tipoDocumento), assetName: row.activoNombre ?? ownerLabel, faenaName: row.faenaNombre, requiresExpirationDate: !!row.requiereFechaVencimiento });
  const closeSelection = () => setSelected(null);
  const closeEditor = () => { setCreateRequirement(null); setEditing(false); closeSelection(); };
  const closeReview = () => { setReview(null); closeSelection(); };
  const closeReplacement = () => { setShowReplacement(false); closeSelection(); };
  const closeAnnul = () => { setShowAnnul(false); closeSelection(); };
  const requirementForSelected = selected ? requirements.find(row => row.tipoDocumento === selected.tipoDocumento) : undefined;
  const counts = (status: string) => requirements.filter(row => row.estado === status).length;

  return <section className="space-y-4">
    <div className="flex flex-wrap justify-between gap-2"><p className="text-sm text-slate-500">{ownerLabel ? "Propietario: " + ownerLabel : "Activo: " + assetCode}</p><p className="text-sm text-slate-500">Las condiciones documentales provienen de la matriz vigente de la faena.</p></div>
    <div className="grid gap-3 sm:grid-cols-4">{[["Pendientes", counts("PendienteCarga")], ["Validación pendiente", counts("PendienteValidacion")], ["Por vencer", counts("PorVencer")], ["Vencidos", counts("Vencido")]].map(([name, value]) => <article className="rounded-lg border border-slate-200 bg-slate-50 p-3" key={String(name)}><small className="text-slate-500">{name}</small><b className="block text-xl">{value}</b></article>)}</div>
    {documents.error || fetchedMatrix.error ? <p className="error-banner">{errorText(documents.error ?? fetchedMatrix.error)}</p> : null}
    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white"><table className="min-w-full text-sm"><thead><tr><th className="p-3 text-left">Requisito</th><th className="p-3 text-left">Condición</th><th className="p-3 text-left">Estado</th><th className="p-3 text-left">Versión y fechas</th><th className="p-3 text-left">Validación / rechazo</th><th className="p-3 text-left">Acciones</th></tr></thead><tbody>{documents.isLoading || (suppliedMatrix === undefined && fetchedMatrix.isLoading) ? <tr><td className="p-4" colSpan={6}>Cargando...</td></tr> : requirements.length ? requirements.map(row => { const document = currentDocument(row.tipoDocumento); return <tr className="border-t align-top" key={row.requisitoId ?? row.tipoDocumento}><td className="p-3"><b className="block">{typeName(row.tipoDocumento)}</b><small className="text-slate-500">{row.tipoDocumento}</small></td><td className="p-3"><div className="flex flex-wrap gap-1 text-xs">{row.obligatorio ? <span className="rounded bg-slate-100 px-2 py-1">Obligatorio</span> : null}{row.critico ? <span className="rounded bg-amber-100 px-2 py-1 text-amber-800">Crítico</span> : null}{row.bloqueaDisponibilidad ? <span className="rounded bg-red-100 px-2 py-1 text-red-800">Bloquea</span> : null}</div></td><td className="p-3"><DocumentStatusBadge status={row.estado} />{row.motivoPendencia ? <small className="mt-1 block max-w-52 text-slate-500">{row.motivoPendencia}</small> : null}</td><td className="p-3">{document?.versionVigente ?? row.version ? "v" + (document?.versionVigente ?? row.version) : "Sin versión"}<small className="mt-1 block text-slate-500">Emisión: {date(document?.fechaEmision)}</small><small className="block text-slate-500">Vence: {date(row.fechaVencimiento ?? document?.fechaVencimiento)}</small>{row.diasParaVencer !== null && row.diasParaVencer !== undefined ? <small className="block text-slate-500">{row.diasParaVencer} días</small> : null}</td><td className="p-3 text-xs text-slate-600">{document?.motivoRechazo ? <span className="text-red-700">Rechazo: {document.motivoRechazo}</span> : document?.validadoPor ? <span>Validado por {document.validadoPor}</span> : "Sin validación"}</td><td className="p-3">{document ? <DocumentActionsMenu document={document} capabilities={{ canManage, canValidate }} onVersions={() => setVersionsId(document.documentoId)} onEdit={() => { setSelected(document); setEditing(true); }} onReplace={() => { setSelected(document); setShowReplacement(true); }} onValidate={() => { setSelected(document); setReview("validate"); }} onReject={() => { setSelected(document); setReview("reject"); }} onAnnul={() => { setSelected(document); setShowAnnul(true); }} /> : canManage ? <button className="primary-button text-xs" type="button" onClick={() => setCreateRequirement(selectionFor(row))}><FilePlus className="h-3 w-3" />Cargar</button> : <span className="text-xs text-slate-500">Sin permiso</span>}</td></tr>; }) : <tr><td className="p-6 text-center text-slate-500" colSpan={6}>No existe una matriz documental vigente para la faena, el tipo y la familia del activo. Configure la matriz antes de cargar documentos.</td></tr>}</tbody></table></div>
    <DocumentEditorDialog open={createRequirement !== null} mode="create" entityCode={assetCode} documentType={createRequirement?.type} assetName={createRequirement?.assetName ?? ownerLabel} faenaName={createRequirement?.faenaName} documentTypeName={createRequirement?.documentTypeName} requiresExpirationDate={createRequirement?.requiresExpirationDate} onClose={closeEditor} onSuccess={closeEditor} />
    <DocumentEditorDialog open={editing} mode="edit" document={selected} entityCode={assetCode} documentType={selected?.tipoDocumento} assetName={requirementForSelected?.activoNombre ?? ownerLabel} faenaName={requirementForSelected?.faenaNombre} documentTypeName={requirementForSelected?.tipoDocumentalNombre ?? (selected ? typeName(selected.tipoDocumento) : null)} requiresExpirationDate={requirementForSelected?.requiereFechaVencimiento} onClose={closeEditor} onSuccess={closeEditor} />
    <DocumentVersionsDialog open={!!versionsId} documentId={versionsId} onClose={() => setVersionsId(null)} />
    <DocumentReplacementDialog open={showReplacement} document={selected} assetName={requirementForSelected?.activoNombre ?? ownerLabel} faenaName={requirementForSelected?.faenaNombre} documentTypeName={requirementForSelected?.tipoDocumentalNombre ?? (selected ? typeName(selected.tipoDocumento) : null)} requiresExpirationDate={requirementForSelected?.requiereFechaVencimiento} onClose={closeReplacement} onSuccess={closeReplacement} />
    <DocumentReviewDialog open={review !== null} action={review ?? "validate"} document={selected} onClose={closeReview} onSuccess={closeReview} />
    <DocumentAnnulDialog open={showAnnul} document={selected} onClose={closeAnnul} onSuccess={closeAnnul} />
  </section>;
}
