import { useEffect, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../auth/authStore";
import { Dialog } from "../../../shared/ui/Dialog";

type Unit = { codigo: string; nombre: string; criticidad?: string | null; observaciones?: string | null };

export function UnitEditorDialog({ open, unit, onClose }: { open: boolean; unit: Unit; onClose: () => void }) {
  const client = useQueryClient();
  const [form, setForm] = useState({ nombre: unit.nombre, criticidad: unit.criticidad ?? "", observaciones: unit.observaciones ?? "" });
  useEffect(() => { if (open) setForm({ nombre: unit.nombre, criticidad: unit.criticidad ?? "", observaciones: unit.observaciones ?? "" }); }, [open, unit]);
  const save = useMutation({
    mutationFn: () => apiFetch("/api/operational-units/" + encodeURIComponent(unit.codigo), { method: "PUT", body: JSON.stringify({ nombre: form.nombre, criticidad: form.criticidad.trim() || null, observaciones: form.observaciones.trim() || null }) }),
    onSuccess: async () => { await Promise.all([client.invalidateQueries({ queryKey: ["equipment-unit-detail", unit.codigo] }), client.invalidateQueries({ queryKey: ["operational-unit", unit.codigo] }), client.invalidateQueries({ queryKey: ["operational-units"] }), client.invalidateQueries({ queryKey: ["equipment-overview"] })]); onClose(); },
  });
  return <Dialog open={open} onClose={onClose} title="Editar identificación de unidad" busy={save.isPending} footer={<button className="primary-button" form="unit-editor" disabled={!form.nombre.trim() || save.isPending} type="submit">{save.isPending ? "Guardando…" : "Guardar cambios"}</button>}>
    <form id="unit-editor" className="space-y-3" onSubmit={event => { event.preventDefault(); save.mutate(); }}>
      <p className="text-sm text-slate-500">La faena, composición, ubicaciones e historial se administran con sus flujos auditables; no se modifican aquí.</p>
      <label className="block text-sm font-medium">Nombre<input className="input mt-1" required value={form.nombre} onChange={event => setForm(current => ({ ...current, nombre: event.target.value }))} /></label>
      <label className="block text-sm font-medium">Criticidad<input className="input mt-1" value={form.criticidad} onChange={event => setForm(current => ({ ...current, criticidad: event.target.value }))} /></label>
      <label className="block text-sm font-medium">Observaciones<textarea className="input mt-1 min-h-20" value={form.observaciones} onChange={event => setForm(current => ({ ...current, observaciones: event.target.value }))} /></label>
      {save.error ? <p className="error-banner">{save.error instanceof Error ? save.error.message : "No fue posible guardar la unidad."}</p> : null}
    </form>
  </Dialog>;
}
