import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { FilePlus } from "lucide-react";
import { Dialog } from "../../../shared/ui/Dialog";
import { DocumentActionsMenu } from "./DocumentActionsMenu";
import { DocumentForm, blankDocumentForm, documentFormFromRecord, type DocumentFormValue } from "./DocumentForm";
import { DocumentStatusBadge } from "./DocumentStatusBadge";
import { DocumentVersionsDialog } from "./DocumentVersionsDialog";
import { documentApi, type OperationalUnitDocumentRow, type OperationalUnitDocumentView } from "./documentApi";
import type { DocumentRecord, DocumentStatus } from "./documentFormTypes";

const date = (value?: string | null) => value ? new Intl.DateTimeFormat("es-CL").format(new Date(value)) : "NA";
const errorText = (error: unknown) => error instanceof Error ? error.message : "No fue posible completar la operación documental.";
const keyFor = (unitCode: string) => ["operational-unit-documents", unitCode] as const;
type Action = "upload" | "edit" | "replace" | "validate" | "reject" | "annul";
type ActiveAction = { kind: Action; row: OperationalUnitDocumentRow } | null;

function asDocument(row: OperationalUnitDocumentRow): DocumentRecord | null {
  if (!row.documentId) return null;
  return {
    documentoId: row.documentId, entidadTipo: "Activo", entidadCodigo: row.technicalOwnerAssetCode,
    tipoDocumento: row.documentTypeCode, estado: row.status as DocumentStatus,
    fechaEmision: row.issueDate, fechaVencimiento: row.expirationDate, sharePointUrl: row.sharePointUrl,
    critico: row.critical, obligatorio: row.mandatory, bloqueaDisponibilidad: row.blocksAvailability,
    esHistorico: false, fechaVencimientoValidada: !!row.validatedBy, validadoPor: row.validatedBy, validadoPorNombre: row.validatedBy,
    validadoEnUtc: row.validatedAtUtc, motivoRechazo: row.rejectionReason, fechaCargaUtc: row.validatedAtUtc ?? new Date().toISOString(),
    cargadoPor: row.validatedBy ?? "", cargadoPorNombre: row.validatedBy, diasParaVencer: row.daysToExpire, bloqueaDisponibilidadActual: row.blocksAvailability,
    versionVigente: row.versionNumber
  };
}

export function OperationalUnitDocumentManager({ unitCode }: { unitCode: string }) {
  const client = useQueryClient();
  const documents = useQuery({ queryKey: keyFor(unitCode), enabled: !!unitCode, queryFn: () => documentApi.unitDocuments(unitCode) });
  const [active, setActive] = useState<ActiveAction>(null);
  const [form, setForm] = useState<DocumentFormValue>(blankDocumentForm);
  const [reason, setReason] = useState("");
  const [versionsId, setVersionsId] = useState<string | null>(null);
  useEffect(() => {
    const document = active ? asDocument(active.row) : null;
    setForm(active?.kind === "edit" && document ? documentFormFromRecord(document) : blankDocumentForm());
    setReason("");
  }, [active]);
  const mutation = useMutation({
    mutationFn: async ({ action, formValue, text }: { action: Exclude<ActiveAction, null>; formValue: DocumentFormValue; text: string }) => {
      const { row, kind } = action;
      if (kind === "upload") {
        if (!formValue.archivo) throw new Error("Debe seleccionar un archivo.");
        return documentApi.uploadUnitRequirement(unitCode, row.requirementKey, { tipoDocumento: row.documentTypeCode, archivo: formValue.archivo, fechaEmision: formValue.fechaEmision || null, fechaVencimiento: row.requiresExpirationDate ? formValue.fechaVencimiento || null : null, observaciones: formValue.observaciones || null });
      }
      if (!row.documentId) throw new Error("El requisito no tiene un documento actual.");
      if (kind === "replace") {
        if (!formValue.archivo || !formValue.observaciones.trim()) throw new Error("Debe seleccionar un archivo e indicar el motivo de reemplazo.");
        return documentApi.replaceUnitDocument(unitCode, row.documentId, { tipoDocumento: row.documentTypeCode, archivo: formValue.archivo, fechaEmision: formValue.fechaEmision || null, fechaVencimiento: row.requiresExpirationDate ? formValue.fechaVencimiento || null : null, observaciones: formValue.observaciones });
      }
      if (kind === "validate") return documentApi.validateUnitDocument(unitCode, row.documentId, text || null);
      if (kind === "reject") {
        if (!text.trim()) throw new Error("Debe indicar el motivo del rechazo.");
        return documentApi.rejectUnitDocument(unitCode, row.documentId, text);
      }
      if (kind === "annul") {
        if (!text.trim()) throw new Error("Debe indicar el motivo de la anulación.");
        return documentApi.annulUnitDocument(unitCode, row.documentId, text);
      }
      const document = asDocument(row);
      if (!document || !formValue.observaciones.trim()) throw new Error("Debe indicar el motivo del ajuste.");
      return documentApi.updateUnitDocument(unitCode, document.documentoId, { fechaEmision: formValue.fechaEmision || null, fechaVencimiento: row.requiresExpirationDate ? formValue.fechaVencimiento || null : null, reason: formValue.observaciones });
    },
    onSuccess: async (view: OperationalUnitDocumentView) => {
      client.setQueryData(keyFor(unitCode), view);
      await Promise.all([
        client.invalidateQueries({ queryKey: ["equipment-unit-detail", unitCode] }),
        client.invalidateQueries({ queryKey: ["documents"] }),
        client.invalidateQueries({ queryKey: ["alerts"] }),
        client.invalidateQueries({ queryKey: ["equipment-asset-detail"] }),
        client.invalidateQueries({ queryKey: ["equipment-overview"] })
      ]);
      setActive(null);
    }
  });
  const view = documents.data;
  const submit = () => { if (active) void mutation.mutateAsync({ action: active, formValue: form, text: reason }).catch(() => undefined); };
  if (documents.isLoading) return <p className="text-sm text-slate-500 dark:text-slate-300">Cargando documentación consolidada...</p>;
  if (!view) return <p className="error-banner">{documents.error ? errorText(documents.error) : "No fue posible cargar la documentación de la unidad."}</p>;
  const activeDocument = active ? asDocument(active.row) : null;
  const expirationComplete = !active?.row.requiresExpirationDate || !!form.fechaVencimiento;
  const canSubmit = active?.kind === "upload" ? !!form.archivo && expirationComplete : active?.kind === "replace" ? !!form.archivo && !!form.observaciones.trim() && expirationComplete : active?.kind === "edit" ? !!form.observaciones.trim() && expirationComplete : active?.kind === "reject" || active?.kind === "annul" ? !!reason.trim() : true;
  const dialogTitle = active?.kind === "upload" ? "Cargar documento requerido" : active?.kind === "replace" ? "Reemplazar documento" : active?.kind === "edit" ? "Editar metadatos" : active?.kind === "validate" ? "Validar documento" : active?.kind === "reject" ? "Rechazar documento" : "Anular documento";
  return <section className="space-y-4">
    <header className="rounded-xl border border-slate-200 bg-slate-50 p-4 dark:border-slate-700 dark:bg-slate-800/60">
      <div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-[11px] font-extrabold uppercase tracking-[.12em] text-teal-700 dark:text-teal-300">Documentación de unidad</p><h2 className="mt-1 text-lg font-semibold text-slate-900 dark:text-slate-100">Unidad: {view.unitName} <span className="text-slate-500">({view.unitCode})</span></h2><p className="text-sm text-slate-500 dark:text-slate-300">Faena: {view.faenaName || view.faenaCode || "NA"}</p></div><span className={"rounded-full px-3 py-1 text-xs font-bold " + (view.summary.compliant ? "bg-emerald-100 text-emerald-800 dark:bg-emerald-950 dark:text-emerald-200" : "bg-amber-100 text-amber-900 dark:bg-amber-950 dark:text-amber-200")}>{view.summary.compliant ? "Cumplimiento documental" : "Cumplimiento pendiente"}</span></div>
      <p className="mt-3 text-xs text-slate-500 dark:text-slate-300">Composición vigente: {view.compositionComplete ? "completa" : "incompleta"}. La propiedad técnica se conserva en cada componente.</p>
    </header>
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">{[["Pendientes de carga", view.summary.pendingUpload], ["Pendientes de validación", view.summary.pendingValidation], ["Por vencer", view.summary.expiring], ["Vencidos", view.summary.expired]].map(([label, count]) => <article className="rounded-lg border border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-900" key={String(label)}><small className="text-slate-500 dark:text-slate-300">{label}</small><b className="block text-xl text-slate-900 dark:text-slate-100">{count}</b></article>)}</div>
    {view.configurationWarnings.length ? <div className={"rounded-lg border p-3 text-sm " + (view.matrixConfigurationComplete ? "border-emerald-200 bg-emerald-50 text-emerald-900 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100" : "border-amber-200 bg-amber-50 text-amber-900 dark:border-amber-900 dark:bg-amber-950/40 dark:text-amber-100")}><ul className="list-inside list-disc space-y-1">{view.configurationWarnings.map(warning => <li key={warning}>{warning}</li>)}</ul></div> : null}
    <div className="overflow-x-auto rounded-lg border border-slate-200 bg-white dark:border-slate-700 dark:bg-slate-900"><table className="min-w-full text-sm"><thead><tr><th className="p-3 text-left">Requisito</th><th className="p-3 text-left">Condición</th><th className="p-3 text-left">Estado</th><th className="p-3 text-left">Versión y fechas</th><th className="p-3 text-left">Validación o rechazo</th><th className="p-3 text-left">Acciones</th></tr></thead><tbody>{view.rows.length ? view.rows.map(row => { const document = asDocument(row); return <tr className="border-t border-slate-200 align-top dark:border-slate-700" key={row.requirementKey}><td className="p-3"><b className="block text-slate-900 dark:text-slate-100">{row.documentTypeName}</b><small className="text-slate-500 dark:text-slate-300">{row.documentTypeCode}</small><span className="ml-2 inline-block rounded bg-slate-100 px-2 py-0.5 text-[10px] font-semibold text-slate-600 dark:bg-slate-800 dark:text-slate-300" title={row.technicalOwnerAssetCode}>{row.technicalOwnerRole === "CHASIS" ? "Chasis" : row.technicalOwnerRole === "FABRICA" ? "Fábrica" : row.technicalOwnerRole}</span></td><td className="p-3"><div className="flex flex-wrap gap-1 text-xs">{row.mandatory ? <span className="rounded bg-slate-100 px-2 py-1 dark:bg-slate-800">Obligatorio</span> : null}{row.critical ? <span className="rounded bg-amber-100 px-2 py-1 text-amber-800 dark:bg-amber-950 dark:text-amber-200">Crítico</span> : null}{row.blocksAvailability ? <span className="rounded bg-red-100 px-2 py-1 text-red-800 dark:bg-red-950 dark:text-red-200">Bloquea</span> : null}</div></td><td className="p-3"><DocumentStatusBadge status={row.status} />{row.pendingReason ? <small className="mt-1 block max-w-52 text-slate-500 dark:text-slate-300">{row.pendingReason}</small> : null}</td><td className="p-3">{row.versionNumber ? "v" + row.versionNumber : "Sin versión"}<small className="mt-1 block text-slate-500 dark:text-slate-300">Emisión: {date(row.issueDate)}</small><small className="block text-slate-500 dark:text-slate-300">Vence: {date(row.expirationDate)}</small>{row.daysToExpire !== null && row.daysToExpire !== undefined ? <small className="block text-slate-500 dark:text-slate-300">{row.daysToExpire} días</small> : null}</td><td className="p-3 text-xs text-slate-600 dark:text-slate-300">{row.rejectionReason ? <span className="text-red-700 dark:text-red-300">Rechazo: {row.rejectionReason}</span> : row.validatedBy ? <span>Validado por {row.validatedBy}</span> : "Sin validación"}</td><td className="p-3">{document ? <DocumentActionsMenu document={document} capabilities={{ canManage: row.canReplace || row.canAnnul, canValidate: row.canValidate || row.canReject }} onVersions={() => setVersionsId(document.documentoId)} onEdit={() => setActive({ kind: "edit", row })} onReplace={() => setActive({ kind: "replace", row })} onValidate={() => setActive({ kind: "validate", row })} onReject={() => setActive({ kind: "reject", row })} onAnnul={() => setActive({ kind: "annul", row })} /> : row.canUpload ? <button className="primary-button text-xs" type="button" onClick={() => setActive({ kind: "upload", row })}><FilePlus className="h-3 w-3" />Cargar</button> : <span className="text-xs text-slate-500">Sin permiso</span>}</td></tr>; }) : <tr><td className="p-6 text-center text-slate-500 dark:text-slate-300" colSpan={6}>No existen requisitos documentales para esta unidad.</td></tr>}</tbody></table></div>
    <Dialog open={active !== null} onClose={() => !mutation.isPending && setActive(null)} title={dialogTitle} busy={mutation.isPending} footer={<button className="primary-button" disabled={mutation.isPending || !canSubmit} type="button" onClick={submit}>{mutation.isPending ? "Guardando..." : active?.kind === "replace" ? "Reemplazar" : "Confirmar"}</button>}>
      {active && (active.kind === "upload" || active.kind === "replace" || active.kind === "edit") ? <div className="space-y-3"><DocumentForm value={form} onChange={setForm} mode={active.kind === "edit" ? "edit" : active.kind === "replace" ? "replace" : "create"} contextLabel={view.unitName || view.unitCode} assetName={active.row.technicalOwnerAssetName} documentTypeName={active.row.documentTypeName} faenaName={view.faenaName} requiresExpirationDate={active.row.requiresExpirationDate} /><label className="block text-sm font-medium">{active.kind === "edit" ? "Motivo del ajuste" : active.kind === "replace" ? "Motivo de reemplazo" : "Observaciones"}<textarea aria-label="Observaciones" className="input mt-1 min-h-16" required={active.kind !== "upload"} value={form.observaciones} onChange={event => setForm(current => ({ ...current, observaciones: event.target.value }))} /></label></div> : <label className="block text-sm font-medium">{active?.kind === "validate" ? "Comentarios" : "Motivo"}<textarea aria-label={active?.kind === "validate" ? "Comentarios" : "Motivo"} className="input mt-1 min-h-20" required={active?.kind !== "validate"} value={reason} onChange={event => setReason(event.target.value)} /></label>}
      {mutation.error ? <p className="error-banner mt-3">{errorText(mutation.error)}</p> : null}
    </Dialog>
    <DocumentVersionsDialog open={!!versionsId} documentId={versionsId} onClose={() => setVersionsId(null)} />
  </section>;
}