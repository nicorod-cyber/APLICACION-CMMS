import type { DocumentEntityType, DocumentRecord, DocumentType } from "./documentFormTypes";

export type DocumentFormValue = {
  entidadTipo: DocumentEntityType;
  entidadCodigo: string;
  tipoDocumento: string;
  fechaEmision: string;
  fechaVencimiento: string;
  sharePointUrl: string;
  critico: boolean;
  obligatorio: boolean;
  bloqueaDisponibilidad: boolean;
  reason: string;
};

export const blankDocumentForm = (entityType: DocumentEntityType = "Activo", entityCode = ""): DocumentFormValue => ({ entidadTipo: entityType, entidadCodigo: entityCode, tipoDocumento: "", fechaEmision: "", fechaVencimiento: "", sharePointUrl: "", critico: false, obligatorio: false, bloqueaDisponibilidad: false, reason: "" });

export const documentFormFromRecord = (document: DocumentRecord, replacement = false): DocumentFormValue => ({
  entidadTipo: document.entidadTipo,
  entidadCodigo: document.entidadCodigo,
  tipoDocumento: document.tipoDocumento,
  fechaEmision: document.fechaEmision ?? "",
  fechaVencimiento: document.fechaVencimiento ?? "",
  sharePointUrl: replacement ? "" : document.sharePointUrl ?? "",
  critico: document.critico,
  obligatorio: document.obligatorio,
  bloqueaDisponibilidad: document.bloqueaDisponibilidad,
  reason: ""
});

type Props = { value: DocumentFormValue; onChange: (value: DocumentFormValue) => void; types?: DocumentType[]; mode: "create" | "edit" | "replace"; lockEntity?: boolean };

export function DocumentForm({ value, onChange, types, mode, lockEntity = false }: Props) {
  const change = <K extends keyof DocumentFormValue>(key: K, next: DocumentFormValue[K]) => onChange({ ...value, [key]: next });
  const typeOptions = types?.filter(type => type.activo && (!type.aplicaA || type.aplicaA === value.entidadTipo)) ?? [];
  return <>
    {lockEntity ? <p className="text-sm text-slate-500">Entidad bloqueada: {value.entidadTipo} {value.entidadCodigo}</p> : <div className="grid gap-3 sm:grid-cols-2"><label className="text-sm font-medium">Tipo de entidad<select aria-label="Tipo de entidad" className="input mt-1" value={value.entidadTipo} onChange={event => change("entidadTipo", event.target.value as DocumentEntityType)}><option value="Activo">Activo</option><option value="OT">OT</option><option value="Faena">Faena</option></select></label><label className="text-sm font-medium">Codigo de entidad<input aria-label="Codigo de entidad" className="input mt-1" required value={value.entidadCodigo} onChange={event => change("entidadCodigo", event.target.value)} /></label></div>}
    {mode === "create" ? <label className="block text-sm font-medium">Tipo documental<select aria-label="Tipo documental" className="input mt-1" required value={value.tipoDocumento} onChange={event => change("tipoDocumento", event.target.value)}><option value="">Selecciona</option>{typeOptions.map(type => <option key={type.codigo} value={type.codigo}>{type.nombre}</option>)}</select></label> : <p className="text-sm font-medium">Tipo documental: {value.tipoDocumento}</p>}
    <div className="grid gap-3 sm:grid-cols-2"><label className="text-sm font-medium">Emision<input aria-label="Emision" className="input mt-1" type="date" value={value.fechaEmision} onChange={event => change("fechaEmision", event.target.value)} /></label><label className="text-sm font-medium">Vencimiento<input aria-label="Vencimiento" className="input mt-1" type="date" value={value.fechaVencimiento} onChange={event => change("fechaVencimiento", event.target.value)} /></label></div>
    <label className="block text-sm font-medium">Enlace de archivo<input aria-label="Enlace de archivo" className="input mt-1" type="url" value={value.sharePointUrl} onChange={event => change("sharePointUrl", event.target.value)} /></label>
    {mode !== "replace" ? <div className="grid gap-2 sm:grid-cols-3"><Check label="Critico" value={value.critico} change={next => change("critico", next)} /><Check label="Obligatorio" value={value.obligatorio} change={next => change("obligatorio", next)} /><Check label="Bloquea disponibilidad" value={value.bloqueaDisponibilidad} change={next => change("bloqueaDisponibilidad", next)} /></div> : null}
  </>;
}

function Check({ label, value, change }: { label: string; value: boolean; change: (value: boolean) => void }) { return <label className="flex items-center gap-2 text-sm"><input type="checkbox" checked={value} onChange={event => change(event.target.checked)} />{label}</label>; }
