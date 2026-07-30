import type { DocumentRecord } from "./documentFormTypes";

export type DocumentFormValue = { archivo: File | null; fechaEmision: string; fechaVencimiento: string; observaciones: string };
export const blankDocumentForm = (): DocumentFormValue => ({ archivo: null, fechaEmision: "", fechaVencimiento: "", observaciones: "" });
export const documentFormFromRecord = (document: DocumentRecord): DocumentFormValue => ({ archivo: null, fechaEmision: document.fechaEmision ?? "", fechaVencimiento: document.fechaVencimiento ?? "", observaciones: "" });

type Props = { value: DocumentFormValue; onChange: (value: DocumentFormValue) => void; mode: "create" | "edit" | "replace"; assetCode?: string; contextLabel?: string; documentType?: string; faenaCode?: string | null; requiresExpirationDate?: boolean };

export function DocumentForm({ value, onChange, mode, assetCode, contextLabel, documentType, faenaCode, requiresExpirationDate = false }: Props) {
  const change = <K extends keyof DocumentFormValue>(key: K, next: DocumentFormValue[K]) => onChange({ ...value, [key]: next });
  const selectFile = (file?: File | null) => change("archivo", file ?? null);
  const needsFile = mode !== "edit";
  return <div className="space-y-3">
    <div className="grid gap-2 rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm sm:grid-cols-3"><p><span className="text-slate-500">{contextLabel ? "Unidad:" : "Activo:"}</span> <b>{contextLabel ?? assetCode ?? "NA"}</b></p><p><span className="text-slate-500">Faena:</span> <b>{faenaCode || "NA"}</b></p><p><span className="text-slate-500">Tipo:</span> <b>{documentType || "NA"}</b></p></div>
    {needsFile ? <label className="block rounded-lg border-2 border-dashed border-slate-300 p-4 text-sm font-medium hover:border-teal-500" onDragOver={event => event.preventDefault()} onDrop={event => { event.preventDefault(); selectFile(event.dataTransfer.files.item(0)); }}><span className="block">Archivo {value.archivo ? `seleccionado: ${value.archivo.name}` : "(arrastra aquí o selecciónalo)"}</span><input aria-label="Archivo" className="mt-2 block w-full text-sm" required type="file" onChange={event => selectFile(event.target.files?.[0] ?? event.target.files?.item?.(0))} /></label> : null}
    <div className="grid gap-3 sm:grid-cols-2"><label className="text-sm font-medium">Emisión<input aria-label="Emisión" className="input mt-1" type="date" value={value.fechaEmision} onChange={event => change("fechaEmision", event.target.value)} /></label><label className="text-sm font-medium">Vencimiento{requiresExpirationDate ? " *" : ""}<input aria-label="Vencimiento" className="input mt-1" required={requiresExpirationDate} type="date" value={value.fechaVencimiento} onChange={event => change("fechaVencimiento", event.target.value)} /></label></div>
  </div>;
}
