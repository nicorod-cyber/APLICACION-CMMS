import { useEffect } from "react";
import type { DocumentRecord } from "./documentFormTypes";

export type DocumentFormValue = { archivo: File | null; fechaEmision: string; fechaVencimiento: string; observaciones: string };
export const blankDocumentForm = (): DocumentFormValue => ({ archivo: null, fechaEmision: "", fechaVencimiento: "", observaciones: "" });
export const documentFormFromRecord = (document: DocumentRecord): DocumentFormValue => ({ archivo: null, fechaEmision: document.fechaEmision ?? "", fechaVencimiento: document.fechaVencimiento ?? "", observaciones: "" });

type Props = {
  value: DocumentFormValue;
  onChange: (value: DocumentFormValue) => void;
  mode: "create" | "edit" | "replace";
  contextLabel?: string;
  assetName?: string | null;
  faenaName?: string | null;
  documentTypeName?: string | null;
  requiresExpirationDate?: boolean;
};

export function DocumentForm({ value, onChange, mode, contextLabel, assetName, faenaName, documentTypeName, requiresExpirationDate = false }: Props) {
  useEffect(() => {
    if (!requiresExpirationDate && value.fechaVencimiento) onChange({ ...value, fechaVencimiento: "" });
  }, [requiresExpirationDate, value, onChange]);

  const change = <K extends keyof DocumentFormValue>(key: K, next: DocumentFormValue[K]) => onChange({ ...value, [key]: next });
  const selectFile = (file?: File | null) => change("archivo", file ?? null);
  const needsFile = mode !== "edit";
  const assetLabel = assetName?.trim() || "No disponible";
  const documentLabel = documentTypeName?.trim() || "No disponible";
  const faenaLabel = faenaName?.trim() || "No disponible";

  return <div className="space-y-3">
    <div className="grid gap-2 rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm dark:border-slate-700 dark:bg-slate-800/60 sm:grid-cols-2 xl:grid-cols-4">
      {contextLabel ? <p><span className="text-slate-500 dark:text-slate-300">Unidad:</span> <b className="text-slate-900 dark:text-slate-100">{contextLabel}</b></p> : null}
      <p><span className="text-slate-500 dark:text-slate-300">{contextLabel ? "Activo asociado:" : "Activo:"}</span> <b className="text-slate-900 dark:text-slate-100">{assetLabel}</b></p>
      <p><span className="text-slate-500 dark:text-slate-300">Faena:</span> <b className="text-slate-900 dark:text-slate-100">{faenaLabel}</b></p>
      <p><span className="text-slate-500 dark:text-slate-300">Documento:</span> <b className="text-slate-900 dark:text-slate-100">{documentLabel}</b></p>
    </div>
    {needsFile ? <label className="block rounded-lg border-2 border-dashed border-slate-300 p-4 text-sm font-medium hover:border-teal-500 dark:border-slate-600" onDragOver={event => event.preventDefault()} onDrop={event => { event.preventDefault(); selectFile(event.dataTransfer.files.item(0)); }}><span className="block">Archivo {value.archivo ? `seleccionado: ${value.archivo.name}` : "(arrastra aquí o selecciónalo)"}</span><input aria-label="Archivo" className="mt-2 block w-full text-sm" required type="file" onChange={event => selectFile(event.target.files?.[0] ?? event.target.files?.item?.(0))} /></label> : null}
    <div className={"grid gap-3 " + (requiresExpirationDate ? "sm:grid-cols-2" : "")}>
      <label className="text-sm font-medium">Emisión<input aria-label="Emisión" className="input mt-1" type="date" value={value.fechaEmision} onChange={event => change("fechaEmision", event.target.value)} /></label>
      {requiresExpirationDate ? <label className="text-sm font-medium">Vencimiento <span aria-hidden="true">*</span><input aria-label="Vencimiento" aria-describedby="expiration-required" className="input mt-1" required type="date" value={value.fechaVencimiento} onChange={event => change("fechaVencimiento", event.target.value)} /></label> : null}
    </div>
    {requiresExpirationDate && !value.fechaVencimiento ? <p className="text-sm text-red-700 dark:text-red-300" id="expiration-required">La fecha de vencimiento es obligatoria para este documento.</p> : null}
  </div>;
}