import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { apiFetch } from "../auth/authStore";
import { Dialog } from "../../shared/ui/Dialog";

export type UnitComponent = {
  activoCodigo: string;
  activoNombre: string;
  rolComponenteCodigo: string;
  fechaMontajeUtc: string;
  fechaDesmontajeUtc?: string | null;
  estadoOperacionalCodigo?: string | null;
  faenaCodigo?: string | null;
  motivoMontaje?: string | null;
  motivoDesmontaje?: string | null;
  vigente: boolean;
};

type AssetOption = { codigo: string; nombre: string; faenaCodigo?: string | null; tipoActivoNombre?: string | null };
type Page<T> = { items: T[] };

function errorMessage(error: unknown) {
  return error instanceof Error ? error.message : "No fue posible completar la operación.";
}

export function UnitCompositionDialogs({
  unitCode,
  unitSiteCode,
  components,
  mode,
  onClose,
}: {
  unitCode: string;
  unitSiteCode?: string | null;
  components: UnitComponent[];
  mode: "mount" | "replace" | "unmount" | null;
  onClose: () => void;
}) {
  const client = useQueryClient();
  const [assetSearch, setAssetSearch] = useState("");
  const [mount, setMount] = useState({ activoCodigo: "", rolComponenteCodigo: "", ordenTrabajoNumero: "", observaciones: "", motivo: "" });
  const [replace, setReplace] = useState({ activoSalienteCodigo: "", activoEntranteCodigo: "", rolComponenteCodigo: "", ordenTrabajoNumero: "", observaciones: "", motivo: "" });
  const [unmount, setUnmount] = useState({ activoCodigo: "", motivo: "" });

  const candidates = useQuery({
    queryKey: ["operational-unit-component-candidates", unitCode, assetSearch],
    enabled: !!mode && assetSearch.trim().length >= 2,
    queryFn: async () => {
      const params = new URLSearchParams({ page: "1", pageSize: "25", texto: assetSearch.trim() });
      if (unitSiteCode) params.set("faenaCodigo", unitSiteCode);
      const page = await apiFetch<Page<AssetOption>>("/api/assets?" + params);
      return page.items;
    },
  });

  const invalidate = async (...assetCodes: string[]) => {
    await Promise.all([
      client.invalidateQueries({ queryKey: ["equipment-unit-detail", unitCode] }),
      client.invalidateQueries({ queryKey: ["operational-unit", unitCode] }),
      client.invalidateQueries({ queryKey: ["operational-units"] }),
      client.invalidateQueries({ queryKey: ["equipment-overview"] }),
      ...assetCodes.filter(Boolean).flatMap(assetCode => [
        client.invalidateQueries({ queryKey: ["equipment-asset-detail", assetCode] }),
        client.invalidateQueries({ queryKey: ["equipment-asset-location", assetCode] }),
        client.invalidateQueries({ queryKey: ["equipment-asset-location-history", assetCode] }),
      ]),
    ]);
  };

  const mountMutation = useMutation({
    mutationFn: () => apiFetch("/api/operational-units/" + encodeURIComponent(unitCode) + "/components", {
      method: "POST",
      body: JSON.stringify({
        activoCodigo: mount.activoCodigo,
        rolComponenteCodigo: mount.rolComponenteCodigo,
        ordenTrabajoNumero: mount.ordenTrabajoNumero || null,
        observaciones: mount.observaciones || null,
        motivo: mount.motivo,
      }),
    }),
    onSuccess: async () => {
      await invalidate(mount.activoCodigo);
      onClose();
    },
  });

  const replaceMutation = useMutation({
    mutationFn: () => apiFetch("/api/operational-units/" + encodeURIComponent(unitCode) + "/components/replace", {
      method: "POST",
      body: JSON.stringify({
        activoSalienteCodigo: replace.activoSalienteCodigo,
        activoEntranteCodigo: replace.activoEntranteCodigo,
        rolComponenteCodigo: replace.rolComponenteCodigo,
        ordenTrabajoNumero: replace.ordenTrabajoNumero || null,
        observaciones: replace.observaciones || null,
        motivo: replace.motivo,
      }),
    }),
    onSuccess: async () => {
      await invalidate(replace.activoSalienteCodigo, replace.activoEntranteCodigo);
      onClose();
    },
  });

  const unmountMutation = useMutation({
    mutationFn: () => apiFetch("/api/operational-units/" + encodeURIComponent(unitCode) + "/components/" + encodeURIComponent(unmount.activoCodigo) + "/unmount", {
      method: "POST",
      body: JSON.stringify({ motivo: unmount.motivo }),
    }),
    onSuccess: async () => {
      await invalidate(unmount.activoCodigo);
      onClose();
    },
  });

  const busy = mountMutation.isPending || replaceMutation.isPending || unmountMutation.isPending;
  const failure = mountMutation.error || replaceMutation.error || unmountMutation.error;
  const choose = (setter: (value: string) => void) => (
    <label className="block text-sm font-medium">
      Buscar componente elegible
      <input className="input mt-1" value={assetSearch} onChange={event => setAssetSearch(event.target.value)} placeholder="Código o nombre (mínimo 2 caracteres)" />
      <select className="input mt-2" defaultValue="" onChange={event => setter(event.target.value)}>
        <option value="">Selecciona un activo</option>
        {candidates.data?.map(asset => <option key={asset.codigo} value={asset.codigo}>{asset.codigo} · {asset.nombre}{asset.tipoActivoNombre ? " · " + asset.tipoActivoNombre : ""}</option>)}
      </select>
      {candidates.isFetching ? <small className="mt-1 block text-slate-500">Buscando activos elegibles…</small> : null}
    </label>
  );
  const reason = (value: string, setValue: (value: string) => void) => <label className="mt-3 block text-sm font-medium">Motivo auditable<textarea className="input mt-1 min-h-20" value={value} onChange={event => setValue(event.target.value)} /></label>;
  const optional = (value: string, setValue: (value: string) => void) => <label className="mt-3 block text-sm font-medium">OT u observaciones<input className="input mt-1" value={value} onChange={event => setValue(event.target.value)} /></label>;

  return <>
    <Dialog open={mode === "mount"} onClose={onClose} title="Montar componente" busy={busy} footer={<button className="primary-button" type="button" disabled={!mount.activoCodigo || !mount.rolComponenteCodigo || !mount.motivo || busy} onClick={() => mountMutation.mutate()}>Confirmar montaje</button>}>
      <p className="text-sm text-slate-500">La API valida compatibilidad de tipo, faena y reglas de composición antes de confirmar.</p>
      <div className="mt-3">{choose(activoCodigo => setMount(current => ({ ...current, activoCodigo })))}</div>
      <label className="mt-3 block text-sm font-medium">Rol de componente<input className="input mt-1" value={mount.rolComponenteCodigo} onChange={event => setMount(current => ({ ...current, rolComponenteCodigo: event.target.value }))} /></label>
      {optional(mount.ordenTrabajoNumero, ordenTrabajoNumero => setMount(current => ({ ...current, ordenTrabajoNumero })))}
      {reason(mount.motivo, motivo => setMount(current => ({ ...current, motivo })))}
      {failure ? <p className="error-banner mt-3">{errorMessage(failure)}</p> : null}
    </Dialog>

    <Dialog open={mode === "replace"} onClose={onClose} title="Reemplazar componente" busy={busy} footer={<button className="primary-button" type="button" disabled={!replace.activoSalienteCodigo || !replace.activoEntranteCodigo || !replace.rolComponenteCodigo || !replace.motivo || busy} onClick={() => replaceMutation.mutate()}>Confirmar reemplazo</button>}>
      <label className="block text-sm font-medium">Componente saliente<select className="input mt-1" value={replace.activoSalienteCodigo} onChange={event => {
        const component = components.find(item => item.activoCodigo === event.target.value);
        setReplace(current => ({ ...current, activoSalienteCodigo: event.target.value, rolComponenteCodigo: component?.rolComponenteCodigo ?? current.rolComponenteCodigo }));
      }}><option value="">Selecciona</option>{components.map(component => <option key={component.activoCodigo} value={component.activoCodigo}>{component.rolComponenteCodigo} · {component.activoNombre || component.activoCodigo}</option>)}</select></label>
      <div className="mt-3">{choose(activoEntranteCodigo => setReplace(current => ({ ...current, activoEntranteCodigo })))}</div>
      <label className="mt-3 block text-sm font-medium">Rol de componente<input className="input mt-1" value={replace.rolComponenteCodigo} onChange={event => setReplace(current => ({ ...current, rolComponenteCodigo: event.target.value }))} /></label>
      {optional(replace.ordenTrabajoNumero, ordenTrabajoNumero => setReplace(current => ({ ...current, ordenTrabajoNumero })))}
      {reason(replace.motivo, motivo => setReplace(current => ({ ...current, motivo })))}
      {failure ? <p className="error-banner mt-3">{errorMessage(failure)}</p> : null}
    </Dialog>

    <Dialog open={mode === "unmount"} onClose={onClose} title="Desmontar componente" busy={busy} footer={<button className="primary-button" type="button" disabled={!unmount.activoCodigo || !unmount.motivo || busy} onClick={() => unmountMutation.mutate()}>Confirmar desmontaje</button>}>
      <label className="block text-sm font-medium">Componente<select className="input mt-1" value={unmount.activoCodigo} onChange={event => setUnmount(current => ({ ...current, activoCodigo: event.target.value }))}><option value="">Selecciona</option>{components.map(component => <option key={component.activoCodigo} value={component.activoCodigo}>{component.rolComponenteCodigo} · {component.activoNombre || component.activoCodigo}</option>)}</select></label>
      {reason(unmount.motivo, motivo => setUnmount(current => ({ ...current, motivo })))}
      {failure ? <p className="error-banner mt-3">{errorMessage(failure)}</p> : null}
    </Dialog>
  </>;
}