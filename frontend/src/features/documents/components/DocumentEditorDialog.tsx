import { useEffect, useState } from "react";
import { Dialog } from "../../../shared/ui/Dialog";
import { blankDocumentForm, documentFormFromRecord, DocumentForm, type DocumentFormValue } from "./DocumentForm";
import type { DocumentRecord } from "./documentFormTypes";
import { useDocumentMutations } from "./useDocumentMutations";

type Props = { open: boolean; mode: "create" | "edit"; document?: DocumentRecord | null; entityCode?: string | null; documentType?: string | null; assetName?: string | null; faenaName?: string | null; documentTypeName?: string | null; requiresExpirationDate?: boolean; onClose: () => void; onSuccess?: () => void };
const errorText = (error: unknown) => error instanceof Error ? error.message : "No fue posible completar la operación.";

export function DocumentEditorDialog({ open, mode, document, entityCode, documentType, assetName, faenaName, documentTypeName, requiresExpirationDate = false, onClose, onSuccess }: Props) {
  const assetCode = entityCode ?? document?.entidadCodigo ?? "";
  const typeCode = documentType ?? document?.tipoDocumento ?? "";
  const operations = useDocumentMutations(assetCode);
  const [form, setForm] = useState<DocumentFormValue>(blankDocumentForm);
  useEffect(() => { if (open) setForm(mode === "edit" && document ? documentFormFromRecord(document) : blankDocumentForm()); }, [open, mode, document]);
  const mutation = mode === "create" ? operations.create : operations.update;
  const submit = async () => {
    if (mutation.isPending || (requiresExpirationDate && !form.fechaVencimiento)) return;
    const fechaVencimiento = requiresExpirationDate ? form.fechaVencimiento || null : null;
    if (mode === "create") {
      if (!form.archivo || !assetCode || !typeCode) return;
      await operations.create.mutateAsync({ assetCode, payload: { tipoDocumento: typeCode, archivo: form.archivo, fechaEmision: form.fechaEmision || null, fechaVencimiento, observaciones: form.observaciones || null } });
    } else if (document) {
      await operations.update.mutateAsync({ id: document.documentoId, payload: { fechaEmision: form.fechaEmision || null, fechaVencimiento, reason: form.observaciones || null } });
    } else return;
    onSuccess?.(); onClose();
  };
  const disabled = mutation.isPending || !assetCode || !typeCode || (mode === "create" && !form.archivo) || (mode === "edit" && !form.observaciones.trim()) || (requiresExpirationDate && !form.fechaVencimiento);
  return <Dialog open={open} onClose={onClose} title={mode === "create" ? "Cargar documento requerido" : "Editar metadatos"} busy={mutation.isPending} footer={<button className="primary-button" form="document-editor" disabled={disabled} type="submit">{mutation.isPending ? "Guardando..." : "Guardar"}</button>}><form id="document-editor" className="space-y-3" onSubmit={event => { event.preventDefault(); void submit().catch(() => undefined); }}><DocumentForm value={form} onChange={setForm} mode={mode} assetName={assetName} documentTypeName={documentTypeName} faenaName={faenaName} requiresExpirationDate={requiresExpirationDate} /><label className="block text-sm font-medium">{mode === "edit" ? "Motivo del ajuste" : "Observaciones"}<textarea aria-label="Observaciones" className="input mt-1 min-h-16" required={mode === "edit"} value={form.observaciones} onChange={event => setForm(current => ({ ...current, observaciones: event.target.value }))} /></label>{mutation.error ? <p className="error-banner">{errorText(mutation.error)}</p> : null}</form></Dialog>;
}