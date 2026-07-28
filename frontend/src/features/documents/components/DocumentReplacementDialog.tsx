import { useEffect, useState } from "react";
import { Dialog } from "../../../shared/ui/Dialog";
import { blankDocumentForm, DocumentForm, documentFormFromRecord, type DocumentFormValue } from "./DocumentForm";
import type { DocumentRecord } from "./documentFormTypes";
import { useDocumentMutations } from "./useDocumentMutations";

const errorText = (error: unknown) => error instanceof Error ? error.message : "No fue posible reemplazar el documento.";
export function DocumentReplacementDialog({ open, document, onClose, onSuccess }: { open: boolean; document?: DocumentRecord | null; onClose: () => void; onSuccess?: () => void }) {
  const operations = useDocumentMutations(document?.entidadCodigo);
  const [form, setForm] = useState<DocumentFormValue>(() => document ? documentFormFromRecord(document, true) : blankDocumentForm());
  useEffect(() => { if (open && document) setForm(documentFormFromRecord(document, true)); }, [open, document]);
  const submit = async () => {
    if (!document || operations.replace.isPending) return;
    await operations.replace.mutateAsync({ id: document.documentoId, payload: { fechaEmision: form.fechaEmision || null, fechaVencimiento: form.fechaVencimiento || null, sharePointUrl: form.sharePointUrl || null, reason: form.reason } });
    onSuccess?.();
    onClose();
  };
  return <Dialog open={open} onClose={onClose} title="Reemplazar documento" busy={operations.replace.isPending} footer={<button className="primary-button" form="document-replacement" disabled={operations.replace.isPending || !form.reason.trim()} type="submit">{operations.replace.isPending ? "Guardando..." : "Guardar"}</button>}><form id="document-replacement" className="space-y-3" onSubmit={event => { event.preventDefault(); void submit().catch(() => undefined); }}><DocumentForm value={form} onChange={setForm} mode="replace" lockEntity /> <label className="block text-sm font-medium">Motivo de reemplazo<textarea aria-label="Motivo de reemplazo" className="input mt-1 min-h-16" required value={form.reason} onChange={event => setForm(current => ({ ...current, reason: event.target.value }))} /></label>{operations.replace.error ? <p className="error-banner">{errorText(operations.replace.error)}</p> : null}</form></Dialog>;
}
