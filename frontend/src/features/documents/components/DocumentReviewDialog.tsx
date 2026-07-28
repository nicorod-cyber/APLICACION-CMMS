import { useEffect, useState } from "react";
import { Dialog } from "../../../shared/ui/Dialog";
import type { DocumentRecord } from "./documentFormTypes";
import { useDocumentMutations } from "./useDocumentMutations";

type Props = { open: boolean; action: "validate" | "reject"; document?: DocumentRecord | null; onClose: () => void; onSuccess?: () => void };
const errorText = (error: unknown) => error instanceof Error ? error.message : "No fue posible completar la revisi?n.";
export function DocumentReviewDialog({ open, action, document, onClose, onSuccess }: Props) {
  const operations = useDocumentMutations(document?.entidadCodigo);
  const [reason, setReason] = useState("");
  useEffect(() => { if (open) setReason(""); }, [open, action, document]);
  const mutation = action === "validate" ? operations.validate : operations.reject;
  const submit = async () => {
    if (!document || mutation.isPending) return;
    if (action === "validate") await operations.validate.mutateAsync({ id: document.documentoId, comments: reason || null });
    else await operations.reject.mutateAsync({ id: document.documentoId, reason });
    onSuccess?.();
    onClose();
  };
  return <Dialog open={open} onClose={onClose} title={action === "validate" ? "Validar documento" : "Rechazar documento"} busy={mutation.isPending} footer={<button className="primary-button" disabled={mutation.isPending || (action === "reject" && !reason.trim())} type="button" onClick={() => void submit().catch(() => undefined)}>{mutation.isPending ? "Guardando..." : "Confirmar"}</button>}><label className="block text-sm font-medium">{action === "validate" ? "Comentarios" : "Motivo"}<textarea aria-label={action === "validate" ? "Comentarios" : "Motivo"} className="input mt-1 min-h-20" required={action === "reject"} value={reason} onChange={event => setReason(event.target.value)} /></label>{mutation.error ? <p className="error-banner mt-3">{errorText(mutation.error)}</p> : null}</Dialog>;
}
