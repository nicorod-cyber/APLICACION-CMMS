import { useEffect, useState } from "react";
import { Dialog } from "../../../shared/ui/Dialog";
import type { DocumentRecord } from "./documentFormTypes";
import { useDocumentMutations } from "./useDocumentMutations";

const errorText = (error: unknown) => error instanceof Error ? error.message : "No fue posible anular el documento.";
export function DocumentAnnulDialog({ open, document, onClose, onSuccess }: { open: boolean; document?: DocumentRecord | null; onClose: () => void; onSuccess?: () => void }) {
  const operations = useDocumentMutations(document?.entidadCodigo);
  const [reason, setReason] = useState("");
  useEffect(() => { if (open) setReason(""); }, [open, document]);
  const submit = async () => {
    if (!document || operations.annul.isPending || !reason.trim()) return;
    await operations.annul.mutateAsync({ id: document.documentoId, reason });
    onSuccess?.();
    onClose();
  };
  return <Dialog open={open} onClose={onClose} title="Anular documento" busy={operations.annul.isPending} footer={<button className="primary-button" disabled={operations.annul.isPending || !reason.trim()} type="button" onClick={() => void submit().catch(() => undefined)}>{operations.annul.isPending ? "Guardando..." : "Confirmar"}</button>}><label className="block text-sm font-medium">Motivo<textarea aria-label="Motivo" className="input mt-1 min-h-20" required value={reason} onChange={event => setReason(event.target.value)} /></label>{operations.annul.error ? <p className="error-banner mt-3">{errorText(operations.annul.error)}</p> : null}</Dialog>;
}
