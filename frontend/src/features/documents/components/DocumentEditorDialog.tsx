import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Dialog } from "../../../shared/ui/Dialog";
import { documentApi } from "./documentApi";
import { blankDocumentForm, documentFormFromRecord, DocumentForm, type DocumentFormValue } from "./DocumentForm";
import { documentQueryKeys, type DocumentEntityType, type DocumentRecord } from "./documentFormTypes";
import { useDocumentMutations } from "./useDocumentMutations";

type Props = { open: boolean; mode: "create" | "edit"; document?: DocumentRecord | null; entityType?: DocumentEntityType | null; entityCode?: string | null; lockEntity?: boolean; onClose: () => void; onSuccess?: () => void };
const errorText = (error: unknown) => error instanceof Error ? error.message : "No fue posible completar la operacion.";

export function DocumentEditorDialog({ open, mode, document, entityType, entityCode, lockEntity = false, onClose, onSuccess }: Props) {
  const operations = useDocumentMutations(entityCode ?? document?.entidadCodigo);
  const [form, setForm] = useState<DocumentFormValue>(() => blankDocumentForm(entityType ?? "Activo", entityCode ?? ""));
  const types = useQuery({ queryKey: documentQueryKeys.types, enabled: open && mode === "create", queryFn: documentApi.types });
  useEffect(() => { if (open) setForm(mode === "edit" && document ? documentFormFromRecord(document) : blankDocumentForm(entityType ?? "Activo", entityCode ?? "")); }, [open, mode, document, entityType, entityCode]);
  const mutation = mode === "create" ? operations.create : operations.update;
  const submit = async () => {
    if (mutation.isPending) return;
    if (mode === "create") await operations.create.mutateAsync({ entidadTipo: form.entidadTipo, entidadCodigo: form.entidadCodigo, tipoDocumento: form.tipoDocumento, fechaEmision: form.fechaEmision || null, fechaVencimiento: form.fechaVencimiento || null, sharePointUrl: form.sharePointUrl || null, critico: form.critico, obligatorio: form.obligatorio, bloqueaDisponibilidad: form.bloqueaDisponibilidad, reason: form.reason || null });
    else if (document) await operations.update.mutateAsync({ id: document.documentoId, payload: { fechaEmision: form.fechaEmision || null, fechaVencimiento: form.fechaVencimiento || null, sharePointUrl: form.sharePointUrl || null, critico: form.critico, obligatorio: form.obligatorio, bloqueaDisponibilidad: form.bloqueaDisponibilidad, reason: form.reason || null } });
    else return;
    onSuccess?.();
    onClose();
  };
  return <Dialog open={open} onClose={onClose} title={mode === "create" ? "Cargar documento" : "Editar metadatos"} busy={mutation.isPending} footer={<button className="primary-button" form="document-editor" disabled={mutation.isPending || !form.tipoDocumento || !form.entidadCodigo} type="submit">{mutation.isPending ? "Guardando..." : "Guardar"}</button>}><form id="document-editor" className="space-y-3" onSubmit={event => { event.preventDefault(); void submit().catch(() => undefined); }}><DocumentForm value={form} onChange={setForm} types={types.data} mode={mode} lockEntity={lockEntity} /><label className="block text-sm font-medium">Observaciones<textarea aria-label="Observaciones" className="input mt-1 min-h-16" value={form.reason} onChange={event => setForm(current => ({ ...current, reason: event.target.value }))} /></label>{types.error ? <p className="error-banner">{errorText(types.error)}</p> : null}{mutation.error ? <p className="error-banner">{errorText(mutation.error)}</p> : null}</form></Dialog>;
}
