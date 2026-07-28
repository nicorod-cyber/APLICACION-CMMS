import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../auth/authStore";
import { Dialog } from "../../../shared/ui/Dialog";

export type OperationalStateOption = { codigo: string; nombre: string };
type Antecedent = { id: string; codigo: string; descripcion: string; fecha?: string | null; estado?: string | null; detalle?: string | null };
type AntecedentSearch = { items: Antecedent[]; total: number; pagina: number; tamanoPagina: number };
type StateForm = { estadoOperacionalCodigo: string; motivo: string; fechaEventoUtc: string; tipoAntecedente: string; antecedenteId: string; antecedenteDescripcion: string; referenciaAntecedente: string };
const blank = (): StateForm => ({ estadoOperacionalCodigo: "", motivo: "", fechaEventoUtc: new Date().toISOString().slice(0, 16), tipoAntecedente: "NONE", antecedenteId: "", antecedenteDescripcion: "", referenciaAntecedente: "" });
const origins = [{ code: "NONE", name: "Sin antecedente" }, { code: "WORK_ORDER", name: "Orden de trabajo" }, { code: "NOTICE", name: "Aviso" }, { code: "DOCUMENT", name: "Documento" }, { code: "TRANSFER", name: "Traslado" }, { code: "OTHER", name: "Otro" }];
const requiresSelection = (origin: string) => ["WORK_ORDER", "NOTICE", "DOCUMENT", "TRANSFER"].includes(origin);
const errorMessage = (error: unknown) => error instanceof Error ? error.message : "No fue posible registrar el evento de estado.";
const label = (item: Antecedent) => [item.codigo, item.descripcion, item.detalle, item.fecha ? new Date(item.fecha).toLocaleString("es-CL") : null, item.estado].filter(Boolean).join(" · ");

export function AssetStateEventForm({ code, states, currentStateCode, onSaved, submitLabel = "Registrar evento" }: { code: string; states: OperationalStateOption[]; currentStateCode?: string | null; onSaved?: () => void; submitLabel?: string }) {
  const client = useQueryClient();
  const [form, setForm] = useState<StateForm>(blank);
  const [search, setSearch] = useState("");
  const originNeedsSelection = requiresSelection(form.tipoAntecedente);
  const antecedents = useQuery({
    queryKey: ["asset-state-event-antecedents", code, form.tipoAntecedente, search],
    enabled: originNeedsSelection && (form.tipoAntecedente === "TRANSFER" || search.trim().length >= 2),
    queryFn: async () => {
      const params = new URLSearchParams({ origen: form.tipoAntecedente, texto: search.trim(), pagina: "1", tamanoPagina: "10" });
      return apiFetch<AntecedentSearch>("/api/assets/" + encodeURIComponent(code) + "/state-event-antecedents?" + params);
    },
  });

  useEffect(() => { setForm(blank()); setSearch(""); }, [code]);
  useEffect(() => { if (!originNeedsSelection) setSearch(""); }, [originNeedsSelection]);

  const save = useMutation({
    mutationFn: () => apiFetch("/api/assets/" + encodeURIComponent(code) + "/state-events", {
      method: "POST",
      body: JSON.stringify({
        estadoOperacionalCodigo: form.estadoOperacionalCodigo,
        motivo: form.motivo,
        fechaEventoUtc: form.fechaEventoUtc ? new Date(form.fechaEventoUtc).toISOString() : null,
        tipoAntecedente: form.tipoAntecedente === "NONE" ? null : form.tipoAntecedente,
        antecedenteId: originNeedsSelection ? form.antecedenteId : null,
        referenciaAntecedente: form.tipoAntecedente === "OTHER" ? form.referenciaAntecedente.trim() || null : null,
      }),
    }),
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: ["assets"] }),
        client.invalidateQueries({ queryKey: ["equipment-overview"] }),
        client.invalidateQueries({ queryKey: ["equipment-asset-detail", code] }),
        client.invalidateQueries({ queryKey: ["equipment-asset-location", code] }),
        client.invalidateQueries({ queryKey: ["equipment-asset-location-history", code] }),
        client.invalidateQueries({ queryKey: ["asset-history", code] }),
        client.invalidateQueries({ queryKey: ["equipment-unit-detail"] }),
      ]);
      setForm(blank());
      setSearch("");
      onSaved?.();
    },
  });

  const chooseOrigin = (tipoAntecedente: string) => { setForm(current => ({ ...current, tipoAntecedente, antecedenteId: "", antecedenteDescripcion: "", referenciaAntecedente: "" })); setSearch(""); };
  const valid = !!form.estadoOperacionalCodigo && form.estadoOperacionalCodigo !== currentStateCode && !!form.motivo.trim() && (!originNeedsSelection || !!form.antecedenteId) && (form.tipoAntecedente !== "OTHER" || !!form.referenciaAntecedente.trim());

  return <form className="space-y-3" onSubmit={event => { event.preventDefault(); if (valid) save.mutate(); }}>
    <label className="block text-sm font-medium">Nuevo estado<select className="input mt-1" required value={form.estadoOperacionalCodigo} onChange={event => setForm(current => ({ ...current, estadoOperacionalCodigo: event.target.value }))}><option value="">Selecciona</option>{states.filter(state => state.codigo !== currentStateCode).map(state => <option key={state.codigo} value={state.codigo}>{state.nombre}</option>)}</select></label>
    <label className="block text-sm font-medium">Motivo<textarea className="input mt-1 min-h-20" required value={form.motivo} onChange={event => setForm(current => ({ ...current, motivo: event.target.value }))} /></label>
    <label className="block text-sm font-medium">Fecha efectiva<input className="input mt-1" type="datetime-local" value={form.fechaEventoUtc} onChange={event => setForm(current => ({ ...current, fechaEventoUtc: event.target.value }))} /></label>
    <label className="block text-sm font-medium">Origen del cambio<select className="input mt-1" value={form.tipoAntecedente} onChange={event => chooseOrigin(event.target.value)}>{origins.map(origin => <option key={origin.code} value={origin.code}>{origin.name}</option>)}</select></label>
    {form.tipoAntecedente === "OTHER" ? <label className="block text-sm font-medium">Referencia o antecedente<input className="input mt-1" required value={form.referenciaAntecedente} onChange={event => setForm(current => ({ ...current, referenciaAntecedente: event.target.value }))} /></label> : null}
    {originNeedsSelection ? <StateEventAntecedentSelector search={search} setSearch={setSearch} response={antecedents.data} loading={antecedents.isFetching} error={antecedents.error} selected={form.antecedenteDescripcion} onChoose={item => setForm(current => ({ ...current, antecedenteId: item.id, antecedenteDescripcion: label(item) }))} onClear={() => setForm(current => ({ ...current, antecedenteId: "", antecedenteDescripcion: "" }))} /> : null}
    {save.error ? <p className="error-banner">{errorMessage(save.error)}</p> : null}
    <button className="secondary-button" disabled={!valid || save.isPending} type="submit">{save.isPending ? "Registrando…" : submitLabel}</button>
  </form>;
}

export function StateEventAntecedentSelector({ search, setSearch, response, loading, error, selected, onChoose, onClear }: { search: string; setSearch: (value: string) => void; response?: AntecedentSearch; loading: boolean; error: unknown; selected: string; onChoose: (item: Antecedent) => void; onClear: () => void }) {
  return <div className="space-y-2"><label className="block text-sm font-medium">Antecedente relacionado<input className="input mt-1" value={search} onChange={event => setSearch(event.target.value)} placeholder="Busca por código o descripción…" /></label>{selected ? <div className="flex items-center justify-between rounded border border-teal-200 bg-teal-50 p-2 text-sm"><span>{selected}</span><button className="text-teal-700" type="button" onClick={onClear}>Limpiar</button></div> : null}{loading ? <p className="text-sm text-slate-500">Buscando antecedentes…</p> : null}{error ? <p className="error-banner">{errorMessage(error)}</p> : null}{!loading && !error && response && response.items.length === 0 ? <p className="text-sm text-slate-500">No se encontraron antecedentes para este activo.</p> : null}{!loading && !error && response?.items.map(item => <button className="block w-full rounded border p-2 text-left text-sm hover:bg-teal-50" key={item.id} type="button" onClick={() => onChoose(item)}>{label(item)}</button>)}</div>;
}

export function AssetStateEventDialog({ open, onClose, code, states, currentStateCode, onSaved }: { open: boolean; onClose: () => void; code: string; states: OperationalStateOption[]; currentStateCode?: string | null; onSaved?: () => void }) {
  return <Dialog open={open} onClose={onClose} title="Registrar estado"><AssetStateEventForm code={code} states={states} currentStateCode={currentStateCode} onSaved={() => { onSaved?.(); onClose(); }} /></Dialog>;
}
