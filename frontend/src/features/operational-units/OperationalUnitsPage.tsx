import { FormEvent, useEffect, useState } from "react";
import { Plus, RefreshCw, RotateCcw, Wrench } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect, type FaenaRecord } from "../faenas/FaenaSelect";

type Component = {
  activoCodigo: string;
  activoNombre: string;
  rolComponenteCodigo: string;
  fechaMontajeUtc: string;
  fechaDesmontajeUtc?: string | null;
  ordenTrabajoMontaje?: string | null;
  ordenTrabajoDesmontaje?: string | null;
  observaciones?: string | null;
  estadoOperacionalCodigo?: string | null;
  faenaCodigo?: string | null;
  ubicacionTecnicaCodigo?: string | null;
  montadoPor?: string | null;
  motivoMontaje?: string | null;
  desmontadoPor?: string | null;
  motivoDesmontaje?: string | null;
  vigente: boolean;
};

type AssetOption = { codigo: string; nombre?: string | null; numeroSerie?: string | null; tipoActivoNombre?: string | null };

type Unit = {
  codigo: string;
  nombre: string;
  tipoUnidadCodigo: string;
  faenaCodigo?: string | null;
  ubicacionTecnicaCodigo?: string | null;
  estadoOperacionalCodigo: string;
  criticidad?: string | null;
  estadoDerivado?: { estadoCodigo: string; activoRestrictivoCodigo?: string | null; rolRestrictivoCodigo?: string | null; motivo?: string | null; calculadoEnUtc?: string | null } | null;
  composicion: { completa: boolean; faltantes: string[]; vigentes: Component[]; historial: Component[] };
};

const unitBlank = {
  codigo: "",
  nombre: "",
  tipoUnidadCodigo: "",
  faenaCodigo: "",
  estadoOperacionalCodigo: "",
  criticidad: "",
  observaciones: ""
};

export function OperationalUnitsPage() {
  const [units, setUnits] = useState<Unit[]>([]);
  const [assets, setAssets] = useState<AssetOption[]>([]);
  const [selected, setSelected] = useState<Unit | null>(null);
  const [unitForm, setUnitForm] = useState(unitBlank);
  const [selectedUnitFaena, setSelectedUnitFaena] = useState<FaenaRecord | null>(null);
  const [typeForm, setTypeForm] = useState({ codigo: "", nombre: "", descripcion: "" });
  const [roleForm, setRoleForm] = useState({ codigo: "", nombre: "", descripcion: "" });
  const [ruleForm, setRuleForm] = useState({
    tipoUnidadCodigo: "",
    rolComponenteCodigo: "",
    cantidadMinima: "0",
    cantidadMaxima: "1",
    obligatorio: true,
    tipoActivoCodigo: "",
    familiaEquipoCodigo: ""
  });
  const [mount, setMount] = useState({ activoCodigo: "", rolComponenteCodigo: "", ordenTrabajoNumero: "", observaciones: "", motivo: "" });
  const [replace, setReplace] = useState({ activoSalienteCodigo: "", activoEntranteCodigo: "", rolComponenteCodigo: "", ordenTrabajoNumero: "", observaciones: "", motivo: "" });
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    void load();
  }, []);

  async function load(preferred?: string) {
    try {
      const [list, assetList] = await Promise.all([
        apiFetch<Unit[]>("/api/operational-units"),
        apiFetch<AssetOption[]>("/api/assets")
      ]);
      setUnits(list);
      setAssets(assetList);
      const next = preferred ? list.find((unit) => unit.codigo === preferred) ?? null : selected ? list.find((unit) => unit.codigo === selected.codigo) ?? null : list[0] ?? null;
      setSelected(next);
    } catch (loadError) {
      setError(message(loadError, "No fue posible cargar unidades."));
    }
  }

  async function action(work: () => Promise<unknown>, success: string) {
    setSaving(true);
    setError("");

    try {
      await work();
      setNotice(success);
      await load(selected?.codigo);
    } catch (actionError) {
      setError(message(actionError, "Operacion no valida."));
    } finally {
      setSaving(false);
    }
  }

  function submitUnit(event: FormEvent) {
    event.preventDefault();
    void action(
      () =>
        apiFetch("/api/operational-units", {
          method: "POST",
          body: JSON.stringify({
            ...unitForm,
            faenaCodigo: empty(unitForm.faenaCodigo),
            criticidad: empty(unitForm.criticidad),
            observaciones: empty(unitForm.observaciones)
          })
        }),
      "Unidad creada."
    );
    setUnitForm(unitBlank);
    setSelectedUnitFaena(null);
  }

  function submitType(event: FormEvent) {
    event.preventDefault();
    void action(() => apiFetch("/api/operational-units/types", { method: "POST", body: JSON.stringify(typeForm) }), "Tipo de unidad creado.");
  }

  function submitRole(event: FormEvent) {
    event.preventDefault();
    void action(() => apiFetch("/api/operational-units/roles", { method: "POST", body: JSON.stringify(roleForm) }), "Rol de componente creado.");
  }

  function submitRule(event: FormEvent) {
    event.preventDefault();
    void action(
      () =>
        apiFetch("/api/operational-units/rules", {
          method: "PUT",
          body: JSON.stringify({
            ...ruleForm,
            cantidadMinima: Number(ruleForm.cantidadMinima),
            cantidadMaxima: Number(ruleForm.cantidadMaxima),
            permitidos:
              ruleForm.tipoActivoCodigo || ruleForm.familiaEquipoCodigo
                ? [{ tipoActivoCodigo: empty(ruleForm.tipoActivoCodigo), familiaEquipoCodigo: empty(ruleForm.familiaEquipoCodigo) }]
                : []
          })
        }),
      "Regla de composicion guardada."
    );
  }

  function mountComponent(event: FormEvent) {
    event.preventDefault();
    if (!selected) {
      return;
    }

    void action(
      () =>
        apiFetch(`/api/operational-units/${encodeURIComponent(selected.codigo)}/components`, {
          method: "POST",
          body: JSON.stringify({ ...mount, ordenTrabajoNumero: empty(mount.ordenTrabajoNumero), observaciones: empty(mount.observaciones), motivo: mount.motivo.trim() })
        }),
      "Componente montado."
    );
  }

  function replaceComponent(event: FormEvent) {
    event.preventDefault();
    if (!selected) {
      return;
    }

    void action(
      () =>
        apiFetch(`/api/operational-units/${encodeURIComponent(selected.codigo)}/components/replace`, {
          method: "POST",
          body: JSON.stringify({ ...replace, ordenTrabajoNumero: empty(replace.ordenTrabajoNumero), observaciones: empty(replace.observaciones), motivo: replace.motivo.trim() })
        }),
      "Componente reemplazado; el historial se conserva."
    );
  }

  function unmount(component: Component) {
    if (!selected) {
      return;
    }

    const motivo = window.prompt(`Motivo del desmontaje de ${component.activoNombre || component.activoCodigo}:`);
    if (!motivo?.trim()) {
      return;
    }

    void action(
      () =>
        apiFetch(`/api/operational-units/${encodeURIComponent(selected.codigo)}/components/${encodeURIComponent(component.activoCodigo)}/unmount`, {
          method: "POST",
          body: JSON.stringify({ motivo: motivo.trim() })
        }),
      "Componente desmontado."
    );
  }

  return (
    <section className="space-y-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-semibold">Unidades operativas</h1>
          <p className="text-sm text-slate-500">Composicion de portador, componentes, reglas y trazabilidad de montaje.</p>
        </div>
        <button className="secondary-button" type="button" onClick={() => void load()}>
          <RefreshCw className="h-4 w-4" />
          Actualizar
        </button>
      </header>

      {error ? <p className="error-banner">{error}</p> : null}
      {notice ? <p className="success-banner">{notice}</p> : null}

      <div className="grid gap-5 xl:grid-cols-[.8fr_1.2fr]">
        <section className="panel stack">
          <h2>Unidades</h2>
          {units.map((unit) => (
            <button
              className={`rounded border p-3 text-left ${selected?.codigo === unit.codigo ? "border-teal-500 bg-teal-50" : ""}`}
              key={unit.codigo}
              onClick={() => setSelected(unit)}
              type="button"
            >
              <b>{unit.codigo}</b>
              <div>{unit.nombre}</div>
              <small>{unit.tipoUnidadCodigo} Ã‚Â· {unit.composicion.completa ? "Composicion completa" : `Faltan: ${unit.composicion.faltantes.join(", ")}`}</small>
            </button>
          ))}
        </section>

        <section className="panel stack">
          {selected ? (
            <>
              <div className="section-heading">
                <div>
                  <h2>{selected.nombre}</h2>
                  <p>{selected.codigo} / {selected.faenaCodigo ?? "Sin faena"} / ubicacion {selected.ubicacionTecnicaCodigo ?? "sin asignar"}</p><p className="text-sm font-medium">Estado derivado: {selected.estadoDerivado?.estadoCodigo ?? selected.estadoOperacionalCodigo}{selected.estadoDerivado?.activoRestrictivoCodigo ? ` por ${selected.estadoDerivado.activoRestrictivoCodigo} (${selected.estadoDerivado.rolRestrictivoCodigo ?? "componente"})` : ""}</p><p className="text-xs text-slate-500">{selected.estadoDerivado?.motivo ?? "Estado base de la unidad"}</p>
                </div>
                <span className={`status-pill ${selected.composicion.completa ? "success" : "danger"}`}>{selected.composicion.completa ? "Completa" : "Incompleta"}</span>
              </div>
              <div>
                <h3>Composicion vigente</h3>
                {selected.composicion.vigentes.map((component) => (
                  <div className="mt-2 flex flex-wrap items-center justify-between gap-2 rounded border p-2" key={component.activoCodigo}>
                    <span><b>{component.rolComponenteCodigo}</b>: {component.activoNombre || "Activo sin nombre"}<small className="block text-slate-500">{component.estadoOperacionalCodigo ?? "Sin estado"} / {component.faenaCodigo ?? "Sin faena"} / {component.ubicacionTecnicaCodigo ?? "Sin ubicacion"}{component.motivoMontaje ? ` / montaje: ${component.motivoMontaje}` : ""}</small></span>
                    <button className="secondary-button" disabled={saving} onClick={() => unmount(component)} type="button">Desmontar</button>
                  </div>
                ))}
              </div>
              <form className="form-grid" onSubmit={mountComponent}>
                <h3 className="span-2">Montar componente</h3>
                <AssetSelect label="Activo" value={mount.activoCodigo} assets={assets} onChange={(activoCodigo) => setMount({ ...mount, activoCodigo })} />
                <Field label="Rol de componente" value={mount.rolComponenteCodigo} change={(rolComponenteCodigo) => setMount({ ...mount, rolComponenteCodigo })} />
                <Field optional label="OT origen (opcional)" value={mount.ordenTrabajoNumero} change={(ordenTrabajoNumero) => setMount({ ...mount, ordenTrabajoNumero })} />
                <Field optional label="Observaciones" value={mount.observaciones} change={(observaciones) => setMount({ ...mount, observaciones })} /><Field label="Motivo auditable" value={mount.motivo} change={(motivo) => setMount({ ...mount, motivo })} />
                <button className="primary-button" disabled={saving}><Wrench className="h-4 w-4" />Montar</button>
              </form>
              <form className="form-grid" onSubmit={replaceComponent}>
                <h3 className="span-2">Reemplazar componente</h3>
                <AssetSelect label="Activo saliente" value={replace.activoSalienteCodigo} assets={assets} onChange={(activoSalienteCodigo) => setReplace({ ...replace, activoSalienteCodigo })} />
                <AssetSelect label="Activo entrante" value={replace.activoEntranteCodigo} assets={assets} onChange={(activoEntranteCodigo) => setReplace({ ...replace, activoEntranteCodigo })} />
                <Field label="Rol" value={replace.rolComponenteCodigo} change={(rolComponenteCodigo) => setReplace({ ...replace, rolComponenteCodigo })} />
                <Field optional label="OT origen" value={replace.ordenTrabajoNumero} change={(ordenTrabajoNumero) => setReplace({ ...replace, ordenTrabajoNumero })} /><Field optional label="Observaciones" value={replace.observaciones} change={(observaciones) => setReplace({ ...replace, observaciones })} /><Field label="Motivo auditable" value={replace.motivo} change={(motivo) => setReplace({ ...replace, motivo })} />
                <button className="secondary-button" disabled={saving}><RotateCcw className="h-4 w-4" />Reemplazar</button>
              </form>
              <div>
                <h3>Historial</h3>
                {selected.composicion.historial.map((component, index) => (
                  <p className="text-sm" key={`${component.activoCodigo}-${index}`}>
                    {component.rolComponenteCodigo}: {component.activoNombre || "Activo sin nombre"} / {new Date(component.fechaMontajeUtc).toLocaleString()} {component.fechaDesmontajeUtc ? `a ${new Date(component.fechaDesmontajeUtc).toLocaleString()}` : "(vigente)"} / {component.montadoPor ?? "usuario desconocido"}{component.motivoMontaje ? ` / ${component.motivoMontaje}` : ""}{component.motivoDesmontaje ? ` / desmontaje: ${component.motivoDesmontaje}` : ""}
                  </p>
                ))}
              </div>
            </>
          ) : (
            <p>Selecciona una unidad.</p>
          )}
        </section>
      </div>

      <div className="grid gap-5 xl:grid-cols-2">
        <form className="panel form-grid" onSubmit={submitUnit}>
          <h2 className="span-2">Crear unidad</h2>
          <Field label="Codigo" value={unitForm.codigo} change={(codigo) => setUnitForm({ ...unitForm, codigo })} />
          <Field label="Nombre" value={unitForm.nombre} change={(nombre) => setUnitForm({ ...unitForm, nombre })} />
          <Field label="Tipo de unidad" value={unitForm.tipoUnidadCodigo} change={(tipoUnidadCodigo) => setUnitForm({ ...unitForm, tipoUnidadCodigo })} />
          <FaenaSelect
            emptyLabel="Selecciona faena"
            includeInactive={false}
            value={unitForm.faenaCodigo}
            onChange={(faenaCodigo, faena) => {
              setUnitForm({ ...unitForm, faenaCodigo });
              setSelectedUnitFaena(faena ?? null);
            }}
          />
          <DerivedTechnicalLocation faena={selectedUnitFaena} />
          <Field label="Estado operacional" value={unitForm.estadoOperacionalCodigo} change={(estadoOperacionalCodigo) => setUnitForm({ ...unitForm, estadoOperacionalCodigo })} />
          <Field label="Criticidad" value={unitForm.criticidad} change={(criticidad) => setUnitForm({ ...unitForm, criticidad })} />
          <Field label="Observaciones" value={unitForm.observaciones} change={(observaciones) => setUnitForm({ ...unitForm, observaciones })} />
          <button className="primary-button" disabled={saving}><Plus className="h-4 w-4" />Crear unidad</button>
        </form>

        <div className="grid gap-5">
          <form className="panel form-grid" onSubmit={submitType}>
            <h2 className="span-2">Catalogo de tipos y roles</h2>
            <Field label="Tipo codigo" value={typeForm.codigo} change={(codigo) => setTypeForm({ ...typeForm, codigo })} />
            <Field label="Tipo nombre" value={typeForm.nombre} change={(nombre) => setTypeForm({ ...typeForm, nombre })} />
            <button className="secondary-button" disabled={saving}>Crear tipo</button>
          </form>
          <form className="panel form-grid" onSubmit={submitRole}>
            <Field label="Rol codigo" value={roleForm.codigo} change={(codigo) => setRoleForm({ ...roleForm, codigo })} />
            <Field label="Rol nombre" value={roleForm.nombre} change={(nombre) => setRoleForm({ ...roleForm, nombre })} />
            <button className="secondary-button" disabled={saving}>Crear rol</button>
          </form>
          <form className="panel form-grid" onSubmit={submitRule}>
            <h2 className="span-2">Regla de composicion</h2>
            <Field label="Tipo unidad" value={ruleForm.tipoUnidadCodigo} change={(tipoUnidadCodigo) => setRuleForm({ ...ruleForm, tipoUnidadCodigo })} />
            <Field label="Rol" value={ruleForm.rolComponenteCodigo} change={(rolComponenteCodigo) => setRuleForm({ ...ruleForm, rolComponenteCodigo })} />
            <Field label="Minimo" value={ruleForm.cantidadMinima} change={(cantidadMinima) => setRuleForm({ ...ruleForm, cantidadMinima })} />
            <Field label="Maximo" value={ruleForm.cantidadMaxima} change={(cantidadMaxima) => setRuleForm({ ...ruleForm, cantidadMaxima })} />
            <Field label="Tipo activo permitido" value={ruleForm.tipoActivoCodigo} change={(tipoActivoCodigo) => setRuleForm({ ...ruleForm, tipoActivoCodigo })} />
            <Field label="Familia permitida" value={ruleForm.familiaEquipoCodigo} change={(familiaEquipoCodigo) => setRuleForm({ ...ruleForm, familiaEquipoCodigo })} />
            <button className="secondary-button" disabled={saving}>Guardar regla</button>
          </form>
        </div>
      </div>
    </section>
  );
}

function DerivedTechnicalLocation({ faena }: { faena: FaenaRecord | null }) {
  const location = faena?.ubicacionTecnica;
  return (
    <div className="rounded border border-slate-200 px-3 py-2 text-sm dark:border-slate-700">
      <span className="block text-xs font-medium text-slate-500 dark:text-slate-400">Ubicacion tecnica derivada</span>
      {location ? (
        <span className="block font-medium">{location.codigo} Â· {location.nombre}{location.obsoleto ? " (obsoleta)" : ""}</span>
      ) : (
        <span className="block text-slate-500 dark:text-slate-400">Selecciona una faena con ubicacion tecnica.</span>
      )}
    </div>
  );
}

function AssetSelect({ label, value, assets, onChange }: { label: string; value: string; assets: AssetOption[]; onChange: (value: string) => void }) {
  return <label>{label}<select required className="input mt-1" value={value} onChange={(event) => onChange(event.target.value)}><option value="">Selecciona un activo</option>{assets.map((asset) => <option key={asset.codigo} value={asset.codigo}>{asset.nombre || "Activo sin nombre"}{asset.numeroSerie ? ` Â· ${asset.numeroSerie}` : ""}{asset.tipoActivoNombre ? ` Â· ${asset.tipoActivoNombre}` : ""}</option>)}</select></label>;
}

function Field({ label, value, change, optional = false }: { label: string; value: string; change: (value: string) => void; optional?: boolean }) {
  return (
    <label>
      {label}
      <input required={!optional} className="input mt-1" value={value} onChange={(event) => change(event.target.value)} />
    </label>
  );
}

function empty(value: string) {
  return value.trim() || null;
}

function message(error: unknown, fallback: string) {
  return error instanceof Error ? error.message : fallback;
}
