import { useEffect, useState } from "react";
import { Dialog } from "../../../shared/ui/Dialog";
import { blankDocumentForm, DocumentForm, documentFormFromRecord, type DocumentFormValue } from "./DocumentForm";
import type { DocumentRecord } from "./documentFormTypes";
import { useDocumentMutations } from "./useDocumentMutations";

const errorText = (error: unknown) => error instanceof Error ? error.message : "No fue posible reemplazar el documento.";
type Props = { open: boolean; document?: DocumentRecord | null; faenaCode?: string | null; requiresExpirationDate?: boolean; onClose: () => void; onSuccess?: () => void };
export function DocumentReplacementDialog({ open, document, faenaCode, requiresExpirationDate = false, onClose, onSuccess }: Props) {
  const operations = useDocumentMutations(document?.entidadCodigo);
  const [form, setForm] = useState<DocumentFormValue>(blankDocumentForm);
  useEffect(() => { if (open && document) setForm(documentFormFromRecord(document)); }, [open, document]);
  const submit = async () => {
    if (!document || !form.archivo || !form.observaciones.trim() || operations.replace.isPending) return;
    await operations.replace.mutateAsync({ id: document.documentoId, payload: { tipoDocumento: document.tipoDocumento, archivo: form.archivo, fechaEmision: form.fechaEmision || null, fechaVencimiento: form.fechaVencimiento || null, observaciones: form.observaciones } });
    onSuccess?.(); onClose();
  };
  return <Dialog open={open} onClose={onClose} title="Reemplazar documento" busy={operations.replace.isPending} footer={<button className="primary-button" form="document-replacement" disabled={operations.replace.isPending || !form.archivo || !form.observaciones.trim()} type="submit">{operations.replace.isPending ? "Guardando..." : "Reemplazar"}</button>}><form id="document-replacement" className="space-y-3" onSubmit={event => { event.preventDefault(); void submit().catch(() => undefined); }}><DocumentForm value={form} onChange={setForm} mode="replace" assetCode={document?.entidadCodigo} documentType={document?.tipoDocumento} faenaCode={faenaCode} requiresExpirationDate={requiresExpirationDate} /><label className="block text-sm font-medium">Motivo de reemplazo<textarea aria-label="Motivo de reemplazo" className="input mt-1 min-h-16" required value={form.observaciones} onChange={event => setForm(current => ({ ...current, observaciones: event.target.value }))} /></label>{operations.replace.error ? <p className="error-banner">{errorText(operations.replace.error)}</p> : null}</form></Dialog>;
}
