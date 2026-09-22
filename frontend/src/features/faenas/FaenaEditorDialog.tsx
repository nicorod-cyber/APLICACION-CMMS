import { FormEvent, useEffect, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { MapPinned, Save } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { Dialog } from "../../shared/ui/Dialog";
import type { FaenaRecord } from "./FaenaSelect";
import { FAENA_ZONES } from "./faenaZones";

type ResponsibleUserOption = { id: string; displayName: string; username: string };
type FaenaForm = { codigo: string; nombre: string; zona: string; cliente: string; centroCostes: string; administradorContrato: string; tipoFaena: string; region: string; comuna: string; latitud: string; longitud: string; responsableUsuarioId: string; activo: boolean; ubicacionTecnicaCodigo: string; ubicacionTecnicaNombre: string; ubicacionTecnicaObsoleta: boolean };

type Props = {
  open: boolean;
  mode: "create" | "edit";
  faena?: FaenaRecord | null;
  onClose: () => void;
  onSaved: (faena: FaenaRecord) => void | Promise<void>;
};

const emptyForm = (): FaenaForm => ({ codigo: "", nombre: "", zona: "", cliente: "", centroCostes: "", administradorContrato: "", tipoFaena: "", region: "", comuna: "", latitud: "", longitud: "", responsableUsuarioId: "", activo: true, ubicacionTecnicaCodigo: "", ubicacionTecnicaNombre: "", ubicacionTecnicaObsoleta: false });

export function FaenaEditorDialog({ open, mode, faena, onClose, onSaved }: Props) {
  const [form, setForm] = useState<FaenaForm>(emptyForm);
  const users = useQuery({
    queryKey: ["faena-responsible-user-options"],
    enabled: open,
    queryFn: () => apiFetch<ResponsibleUserOption[]>("/api/faenas/responsible-user-options")
  });
  const save = useMutation({
    mutationFn: () => apiFetch<FaenaRecord>(
      mode === "create" ? "/api/faenas" : `/api/faenas/${encodeURIComponent(faena!.codigo)}`,
      { method: mode === "create" ? "POST" : "PUT", body: JSON.stringify(toPayload(form)) }
    ),
    onSuccess: async saved => { await onSaved(saved); onClose(); }
  });

  useEffect(() => {
    if (!open) return;
    setForm(mode === "edit" && faena ? toForm(faena) : emptyForm());
    save.reset();
  }, [faena, mode, open]); // eslint-disable-line react-hooks/exhaustive-deps

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!save.isPending) save.mutate();
  }

  const error = users.error ?? save.error;
  return <Dialog open={open} title={mode === "create" ? "Nueva faena" : `Editar ${faena?.codigo ?? "faena"}`} onClose={onClose} busy={save.isPending} className="max-w-5xl">
    {error ? <Notice error>{error instanceof Error ? error.message : "No fue posible cargar el editor de faena."}</Notice> : null}
    <form onSubmit={submit}>
      <p className="mb-4 text-sm text-slate-500 dark:text-slate-400">La ubicación técnica se administra en este mismo formulario.</p>
      <div className="grid gap-3 md:grid-cols-2">
        <Field label="Código" required disabled={mode === "edit"} value={form.codigo} onChange={codigo => setForm(current => ({ ...current, codigo }))} />
        <Field label="Nombre" required value={form.nombre} onChange={nombre => setForm(current => ({ ...current, nombre }))} />
        <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Zona<select className="input mt-2" required value={form.zona} onChange={event => setForm(current => ({ ...current, zona: event.target.value }))}><option disabled value="">Seleccione una zona</option>{FAENA_ZONES.map(zona => <option key={zona}>{zona}</option>)}</select></label>
        <Field label="Cliente" required value={form.cliente} onChange={cliente => setForm(current => ({ ...current, cliente }))} />
        <Field label="Centro de costes" value={form.centroCostes} onChange={centroCostes => setForm(current => ({ ...current, centroCostes }))} />
        <Field label="Administrador de contrato" value={form.administradorContrato} onChange={administradorContrato => setForm(current => ({ ...current, administradorContrato }))} />
        <Field label="Tipo de faena" required value={form.tipoFaena} onChange={tipoFaena => setForm(current => ({ ...current, tipoFaena }))} />
        <Field label="Región" required value={form.region} onChange={region => setForm(current => ({ ...current, region }))} />
        <Field label="Comuna" required value={form.comuna} onChange={comuna => setForm(current => ({ ...current, comuna }))} />
        <Field label="Latitud" type="number" step="any" inputMode="decimal" value={form.latitud} onChange={latitud => setForm(current => ({ ...current, latitud }))} />
        <Field label="Longitud" type="number" step="any" inputMode="decimal" value={form.longitud} onChange={longitud => setForm(current => ({ ...current, longitud }))} />
        <label className="text-sm font-medium text-slate-700 dark:text-slate-200">Responsable<select aria-label="Responsable" className="input mt-2" disabled={users.isLoading || users.isError} required value={form.responsableUsuarioId} onChange={event => setForm(current => ({ ...current, responsableUsuarioId: event.target.value }))}><option value="">{users.isLoading ? "Cargando usuarios…" : "Selecciona un usuario activo"}</option>{users.data?.map(user => <option key={user.id} value={user.id}>{user.displayName} ({user.username})</option>)}</select></label>
        <label className="flex items-center gap-2 self-end text-sm font-medium text-slate-700 dark:text-slate-200"><input checked={form.activo} onChange={event => setForm(current => ({ ...current, activo: event.target.checked }))} type="checkbox" />Faena activa</label>
      </div>
      <section className="mt-5 rounded-lg border border-slate-200 p-4 dark:border-slate-800">
        <h3 className="flex items-center gap-2 font-semibold text-slate-950 dark:text-white"><MapPinned className="h-5 w-5 text-teal-700 dark:text-teal-300" />Ubicación técnica única</h3>
        <div className="mt-4 grid gap-3 md:grid-cols-2">
          <Field label="Código de ubicación técnica" required value={form.ubicacionTecnicaCodigo} onChange={ubicacionTecnicaCodigo => setForm(current => ({ ...current, ubicacionTecnicaCodigo }))} />
          <Field label="Nombre de ubicación técnica" required value={form.ubicacionTecnicaNombre} onChange={ubicacionTecnicaNombre => setForm(current => ({ ...current, ubicacionTecnicaNombre }))} />
        </div>
        <label className="mt-4 flex items-center gap-2 text-sm font-medium text-slate-700 dark:text-slate-200"><input checked={form.ubicacionTecnicaObsoleta} onChange={event => setForm(current => ({ ...current, ubicacionTecnicaObsoleta: event.target.checked }))} type="checkbox" />Ubicación técnica obsoleta</label>
      </section>
      <div className="mt-5 flex gap-2">
        <button className="primary-button" disabled={save.isPending || users.isLoading || users.isError} type="submit"><Save className="h-4 w-4" />{save.isPending ? "Guardando…" : "Guardar faena"}</button>
        <button className="secondary-button" disabled={save.isPending} onClick={onClose} type="button">Cancelar</button>
      </div>
    </form>
  </Dialog>;
}

export function FaenaSummary({ faena }: { faena: FaenaRecord }) {
  return <section>
    <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
      <DetailItem label="Código" value={faena.codigo} /><DetailItem label="Nombre" value={faena.nombre} /><DetailItem label="Estado" value={faena.activo ? "Activa" : "Inactiva"} />
      <DetailItem label="Zona" value={faena.zona} /><DetailItem label="Cliente" value={faena.cliente} /><DetailItem label="Centro de costes" value={faena.centroCostes} />
      <DetailItem label="Administrador de contrato" value={faena.administradorContrato} /><DetailItem label="Tipo de faena" value={faena.tipoFaena} /><DetailItem label="Región" value={faena.region} />
      <DetailItem label="Comuna" value={faena.comuna} /><DetailItem label="Responsable" value={faena.responsableNombre} /><DetailItem label="Coordenadas" value={faena.latitud == null || faena.longitud == null ? null : `${faena.latitud}, ${faena.longitud}`} />
    </div>
    <section className="mt-5 rounded-lg border border-slate-200 p-4 dark:border-slate-800">
      <h3 className="flex items-center gap-2 font-semibold text-slate-950 dark:text-white"><MapPinned className="h-5 w-5 text-teal-700 dark:text-teal-300" />Ubicación técnica</h3>
      {faena.ubicacionTecnica ? <div className="mt-3 rounded-md bg-slate-50 p-3 dark:bg-slate-950"><p className="font-medium">{faena.ubicacionTecnica.nombre}</p><p className="text-sm text-slate-500">{faena.ubicacionTecnica.codigo}</p><span className="status-pill mt-2 inline-flex">{faena.ubicacionTecnica.obsoleto ? "Obsoleta" : "Vigente"}</span></div> : <p className="mt-3 text-sm text-amber-700 dark:text-amber-300">Sin ubicación técnica.</p>}
    </section>
  </section>;
}

export function FaenaActivityBadge({ active }: { active: boolean }) {
  return <span className={`status-pill ${active ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200" : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200"}`}>{active ? "Activa" : "Inactiva"}</span>;
}

function Field({ label, value, onChange, required, disabled, type = "text", step, inputMode }: { label: string; value: string; onChange: (value: string) => void; required?: boolean; disabled?: boolean; type?: string; step?: string; inputMode?: "decimal" }) {
  return <label className="text-sm font-medium text-slate-700 dark:text-slate-200">{label}<input className="input mt-2" disabled={disabled} inputMode={inputMode} required={required} step={step} type={type} value={value} onChange={event => onChange(event.target.value)} /></label>;
}
function DetailItem({ label, value }: { label: string; value?: string | null }) { return <div className="rounded-md border border-slate-200 p-3 dark:border-slate-800"><p className="text-xs font-medium text-slate-500">{label}</p><p className="mt-1 text-sm font-semibold text-slate-900 dark:text-slate-100">{value || "-"}</p></div>; }
function Notice({ children, error }: { children: React.ReactNode; error?: boolean }) { return <div className={`mb-4 rounded-md border p-3 text-sm ${error ? "border-red-200 bg-red-50 text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200" : "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200"}`}>{children}</div>; }
function toForm(faena: FaenaRecord): FaenaForm { return { codigo: faena.codigo, nombre: faena.nombre, zona: faena.zona ?? "", cliente: faena.cliente ?? "", centroCostes: faena.centroCostes ?? "", administradorContrato: faena.administradorContrato ?? "", tipoFaena: faena.tipoFaena ?? "", region: faena.region ?? "", comuna: faena.comuna ?? "", latitud: formatCoordinate(faena.latitud), longitud: formatCoordinate(faena.longitud), responsableUsuarioId: faena.responsableUsuarioId ?? "", activo: faena.activo, ubicacionTecnicaCodigo: faena.ubicacionTecnica?.codigo ?? "", ubicacionTecnicaNombre: faena.ubicacionTecnica?.nombre ?? "", ubicacionTecnicaObsoleta: faena.ubicacionTecnica?.obsoleto ?? false }; }
function toPayload(form: FaenaForm) { return { codigo: form.codigo.trim(), nombre: form.nombre.trim(), zona: form.zona, cliente: form.cliente.trim(), centroCostes: emptyToNull(form.centroCostes), administradorContrato: emptyToNull(form.administradorContrato), tipoFaena: form.tipoFaena.trim(), region: form.region.trim(), comuna: form.comuna.trim(), latitud: toNumberOrNull(form.latitud), longitud: toNumberOrNull(form.longitud), responsableUsuarioId: form.responsableUsuarioId, activo: form.activo, ubicacionTecnicaCodigo: form.ubicacionTecnicaCodigo.trim(), ubicacionTecnicaNombre: form.ubicacionTecnicaNombre.trim(), ubicacionTecnicaObsoleta: form.ubicacionTecnicaObsoleta }; }
const formatCoordinate = (value?: number | null) => value == null ? "" : String(value);
const emptyToNull = (value: string) => value.trim() || null;
const toNumberOrNull = (value: string) => { const normalized = value.trim().replace(",", "."); return normalized ? Number(normalized) : null; };
