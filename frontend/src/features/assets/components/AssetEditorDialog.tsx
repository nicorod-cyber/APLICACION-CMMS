import { FormEvent, useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiFetch } from "../../auth/authStore";
import { FaenaSelect, useFaenas } from "../../faenas/FaenaSelect";
import { Dialog } from "../../../shared/ui/Dialog";

type Item = { codigo: string; nombre: string; tipoActivoCodigo?: string | null };
type Catalog = { tiposActivo: Item[]; familiasEquipo: Item[]; estadosOperacionales: Item[]; criticidades: Item[] };
type Definition = { codigo: string; nombre: string; tipoDato: "TEXTO" | "OPCION" | "NUMERO" | "ENTERO" | "BOOLEANO" | "FECHA"; unidad?: string | null; obligatorio: boolean; opcionesJson?: string | null };
type Value = { definicionCodigo: string; valorTexto?: string | null; valorNumerico?: number | null; valorBooleano?: boolean | null; valorFecha?: string | null };
type Detail = { resumen: { codigo: string; nombre: string; tipoActivoCodigo: string; familiaEquipoCodigo?: string | null; faenaCodigo?: string | null; estadoOperacionalCodigo: string; criticidad?: string | null; tipoMedicionUso?: string | null }; marca?: string | null; modelo?: string | null; numeroSerie?: string | null; propiedad?: string | null; anioFabricacion?: number | null; fechaAdquisicion?: string | null; fechaPuestaServicio?: string | null; fechaBaja?: string | null; observaciones?: string | null; atributos: Value[]; definicionesAplicables: Definition[] };
type Attr = { texto: string; numero: string; booleano: string; fecha: string };
type Form = { nombre: string; tipoActivoCodigo: string; familiaEquipoCodigo: string; faenaCodigo: string; estadoOperacionalCodigo: string; marca: string; modelo: string; numeroSerie: string; propiedad: string; criticidad: string; anioFabricacion: string; fechaAdquisicion: string; fechaPuestaServicio: string; fechaBaja: string; tipoMedicionUso: string; observaciones: string; atributos: Record<string, Attr> };

const blank = (): Form => ({ nombre: "", tipoActivoCodigo: "", familiaEquipoCodigo: "", faenaCodigo: "", estadoOperacionalCodigo: "", marca: "", modelo: "", numeroSerie: "", propiedad: "", criticidad: "", anioFabricacion: "", fechaAdquisicion: "", fechaPuestaServicio: "", fechaBaja: "", tipoMedicionUso: "", observaciones: "", atributos: {} });
const clean = (value: string) => value.trim() || null;
const emptyAttr = (): Attr => ({ texto: "", numero: "", booleano: "", fecha: "" });
const errorMessage = (error: unknown) => error instanceof Error ? error.message : "No fue posible guardar el activo.";

function fromDetail(detail: Detail): Form {
  const values: Record<string, Attr> = {};
  detail.atributos.forEach(value => { values[value.definicionCodigo] = { texto: value.valorTexto ?? "", numero: value.valorNumerico?.toString() ?? "", booleano: value.valorBooleano == null ? "" : String(value.valorBooleano), fecha: value.valorFecha?.slice(0, 10) ?? "" }; });
  const summary = detail.resumen;
  return { nombre: summary.nombre, tipoActivoCodigo: summary.tipoActivoCodigo, familiaEquipoCodigo: summary.familiaEquipoCodigo ?? "", faenaCodigo: summary.faenaCodigo ?? "", estadoOperacionalCodigo: summary.estadoOperacionalCodigo, marca: detail.marca ?? "", modelo: detail.modelo ?? "", numeroSerie: detail.numeroSerie ?? "", propiedad: detail.propiedad ?? "", criticidad: summary.criticidad ?? "", anioFabricacion: detail.anioFabricacion?.toString() ?? "", fechaAdquisicion: detail.fechaAdquisicion?.slice(0, 10) ?? "", fechaPuestaServicio: detail.fechaPuestaServicio?.slice(0, 10) ?? "", fechaBaja: detail.fechaBaja?.slice(0, 10) ?? "", tipoMedicionUso: summary.tipoMedicionUso ?? "", observaciones: detail.observaciones ?? "", atributos: values };
}

function payload(form: Form, definitions: Definition[]) {
  return { ...form, familiaEquipoCodigo: clean(form.familiaEquipoCodigo), faenaCodigo: clean(form.faenaCodigo), marca: clean(form.marca), modelo: clean(form.modelo), numeroSerie: clean(form.numeroSerie), propiedad: clean(form.propiedad), criticidad: clean(form.criticidad), anioFabricacion: form.anioFabricacion ? Number(form.anioFabricacion) : null, fechaAdquisicion: clean(form.fechaAdquisicion), fechaPuestaServicio: clean(form.fechaPuestaServicio), fechaBaja: clean(form.fechaBaja), tipoMedicionUso: clean(form.tipoMedicionUso), observaciones: clean(form.observaciones), atributos: definitions.map(definition => {
    const value = form.atributos[definition.codigo] ?? emptyAttr();
    return { definicionCodigo: definition.codigo, valorTexto: definition.tipoDato === "TEXTO" || definition.tipoDato === "OPCION" ? clean(value.texto) : null, valorNumerico: definition.tipoDato === "NUMERO" || definition.tipoDato === "ENTERO" ? value.numero ? Number(value.numero) : null : null, valorBooleano: definition.tipoDato === "BOOLEANO" && value.booleano ? value.booleano === "true" : null, valorFecha: definition.tipoDato === "FECHA" ? clean(value.fecha) : null };
  }).filter(value => value.valorTexto !== null || value.valorNumerico !== null || value.valorBooleano !== null || value.valorFecha !== null) };
}

export function AssetEditorDialog({ code, open, onClose, onSaved }: { code?: string; open: boolean; onClose: () => void; onSaved: (assetCode: string) => void }) {
  const creating = !code;
  const client = useQueryClient();
  const { faenas } = useFaenas(false);
  const [form, setForm] = useState<Form>(blank);
  const [definitions, setDefinitions] = useState<Definition[]>([]);
  const [error, setError] = useState("");
  const catalog = useQuery({ queryKey: ["asset-catalog"], queryFn: () => apiFetch<Catalog>("/api/assets/catalog") });
  const detail = useQuery({ queryKey: ["equipment-asset-edit", code], enabled: open && !!code, queryFn: () => apiFetch<Detail>("/api/assets/" + encodeURIComponent(code!)) });

  useEffect(() => {
    if (!open) return;
    if (creating) { setForm(blank()); setDefinitions([]); setError(""); return; }
    if (detail.data) { setForm(fromDetail(detail.data)); setDefinitions(detail.data.definicionesAplicables); setError(""); }
  }, [open, creating, detail.data]);

  useEffect(() => {
    if (!open || !form.tipoActivoCodigo) return;
    const params = new URLSearchParams({ tipoActivoCodigo: form.tipoActivoCodigo });
    if (form.familiaEquipoCodigo) params.set("familiaEquipoCodigo", form.familiaEquipoCodigo);
    apiFetch<Definition[]>("/api/assets/attribute-definitions?" + params).then(setDefinitions).catch(setError);
  }, [open, form.tipoActivoCodigo, form.familiaEquipoCodigo]);

  const save = useMutation({
    mutationFn: () => apiFetch<Detail>(creating ? "/api/assets" : "/api/assets/" + encodeURIComponent(code!), { method: creating ? "POST" : "PUT", body: JSON.stringify(payload(form, definitions)) }),
    onSuccess: async saved => {
      const assetCode = saved.resumen.codigo;
      await Promise.all([
        client.invalidateQueries({ queryKey: ["assets"] }),
        client.invalidateQueries({ queryKey: ["equipment-overview"] }),
        client.invalidateQueries({ queryKey: ["equipment-asset-detail", assetCode] }),
        client.invalidateQueries({ queryKey: ["equipment-asset-edit", assetCode] }),
      ]);
      onSaved(assetCode);
    },
    onError: failure => setError(errorMessage(failure)),
  });
  const families = catalog.data?.familiasEquipo.filter(item => !item.tipoActivoCodigo || item.tipoActivoCodigo === form.tipoActivoCodigo) ?? [];
  const update = <K extends keyof Form>(key: K, value: Form[K]) => setForm(current => ({ ...current, [key]: value }));
  const updateAttr = (definitionCode: string, patch: Partial<Attr>) => setForm(current => ({ ...current, atributos: { ...current.atributos, [definitionCode]: { ...(current.atributos[definitionCode] ?? emptyAttr()), ...patch } } }));
  const submit = (event: FormEvent) => { event.preventDefault(); setError(""); save.mutate(); };
  const selectedFaena = faenas.find(faena => faena.codigo === form.faenaCodigo);

  return <Dialog open={open} onClose={onClose} title={creating ? "Nuevo activo" : "Editar activo"} busy={save.isPending} footer={<button className="primary-button" type="submit" form="asset-editor" disabled={save.isPending || !form.nombre || !form.tipoActivoCodigo || !form.estadoOperacionalCodigo}>{save.isPending ? "Guardando…" : "Guardar activo"}</button>}>
    {detail.isLoading ? <p className="text-sm text-slate-500">Cargando ficha completa…</p> : <form id="asset-editor" className="space-y-4" onSubmit={submit}>
      {!creating ? <p className="rounded border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">Faena y estado se modifican mediante sus eventos auditables correspondientes.</p> : null}
      <div className="grid gap-3 md:grid-cols-2">
        <Field label="Nombre" required value={form.nombre} change={value => update("nombre", value)} />
        <Select label="Tipo de activo" required value={form.tipoActivoCodigo} change={value => update("tipoActivoCodigo", value)}><option value="">Selecciona</option>{catalog.data?.tiposActivo.map(item => <option key={item.codigo} value={item.codigo}>{item.codigo} · {item.nombre}</option>)}</Select>
        <Select label="Familia" value={form.familiaEquipoCodigo} change={value => update("familiaEquipoCodigo", value)}><option value="">Sin familia</option>{families.map(item => <option key={item.codigo} value={item.codigo}>{item.codigo} · {item.nombre}</option>)}</Select>
        <FaenaSelect disabled={!creating} label="Faena" value={form.faenaCodigo} emptyLabel="Sin faena" includeInactive={false} onChange={faenaCodigo => update("faenaCodigo", faenaCodigo)} />
        <Field label="Ubicación técnica" disabled value={selectedFaena?.ubicacionTecnica ? selectedFaena.ubicacionTecnica.codigo + " · " + selectedFaena.ubicacionTecnica.nombre : form.faenaCodigo ? "Sin ubicación técnica asignada" : "Selecciona una faena"} change={() => undefined} />
        <Select disabled={!creating || form.faenaCodigo.toUpperCase() === "FAE_EDB"} label="Estado operacional" required value={form.estadoOperacionalCodigo} change={value => update("estadoOperacionalCodigo", value)}><option value="">Selecciona</option>{catalog.data?.estadosOperacionales.map(item => <option key={item.codigo} value={item.codigo}>{item.codigo} · {item.nombre}</option>)}</Select>
        <Select label="Medición de uso" value={form.tipoMedicionUso} change={value => update("tipoMedicionUso", value)}><option value="">Ninguna / solo calendario</option><option value="HOROMETRO">Horómetro</option><option value="KILOMETRAJE">Kilometraje</option></Select>
        <Field label="Marca" value={form.marca} change={value => update("marca", value)} /><Field label="Modelo" value={form.modelo} change={value => update("modelo", value)} /><Field label="Número de serie" value={form.numeroSerie} change={value => update("numeroSerie", value)} /><Field label="Propiedad" value={form.propiedad} change={value => update("propiedad", value)} />
        <Select label="Criticidad" value={form.criticidad} change={value => update("criticidad", value)}><option value="">Sin criticidad</option>{catalog.data?.criticidades.map(item => <option key={item.codigo} value={item.nombre}>{item.nombre}</option>)}</Select>
        <Field label="Año fabricación" type="number" value={form.anioFabricacion} change={value => update("anioFabricacion", value)} /><Field label="Fecha adquisición" type="date" value={form.fechaAdquisicion} change={value => update("fechaAdquisicion", value)} /><Field label="Puesta en servicio" type="date" value={form.fechaPuestaServicio} change={value => update("fechaPuestaServicio", value)} /><Field label="Fecha baja" type="date" value={form.fechaBaja} change={value => update("fechaBaja", value)} />
      </div>
      <label className="block text-sm font-medium">Observaciones<textarea className="input mt-1 min-h-20" value={form.observaciones} onChange={event => update("observaciones", event.target.value)} /></label>
      <div><h3 className="mb-2 font-semibold">Atributos dinámicos</h3>{!form.tipoActivoCodigo ? <p className="text-sm text-slate-500">Selecciona un tipo para cargar atributos.</p> : <div className="grid gap-3 md:grid-cols-2">{definitions.map(definition => <Dynamic key={definition.codigo} definition={definition} value={form.atributos[definition.codigo] ?? emptyAttr()} change={patch => updateAttr(definition.codigo, patch)} />)}</div>}</div>
      {error ? <p className="error-banner">{error}</p> : null}
    </form>}
  </Dialog>;
}

function Dynamic({ definition, value, change }: { definition: Definition; value: Attr; change: (patch: Partial<Attr>) => void }) {
  const label = definition.nombre + (definition.obligatorio ? " *" : "") + (definition.unidad ? " (" + definition.unidad + ")" : "");
  if (definition.tipoDato === "BOOLEANO") return <Select label={label} value={value.booleano} change={booleano => change({ booleano })}><option value="">Sin definir</option><option value="true">Sí</option><option value="false">No</option></Select>;
  if (definition.tipoDato === "OPCION") { let options: string[] = []; try { const parsed: unknown = JSON.parse(definition.opcionesJson ?? "[]"); options = Array.isArray(parsed) ? parsed.map(String) : []; } catch { options = []; } return <Select label={label} value={value.texto} change={texto => change({ texto })}><option value="">Selecciona</option>{options.map(option => <option key={option}>{option}</option>)}</Select>; }
  if (definition.tipoDato === "FECHA") return <Field label={label} type="date" value={value.fecha} change={fecha => change({ fecha })} />;
  const numeric = definition.tipoDato === "NUMERO" || definition.tipoDato === "ENTERO";
  return <Field label={label} type={numeric ? "number" : "text"} value={numeric ? value.numero : value.texto} change={next => change(numeric ? { numero: next } : { texto: next })} />;
}
function Field({ label, value, change, type = "text", required = false, disabled = false }: { label: string; value: string; change: (value: string) => void; type?: string; required?: boolean; disabled?: boolean }) { return <label className="block text-sm font-medium">{label}<input className="input mt-1" disabled={disabled} required={required} type={type} value={value} onChange={event => change(event.target.value)} /></label>; }
function Select({ label, value, change, children, required = false, disabled = false }: { label: string; value: string; change: (value: string) => void; children: React.ReactNode; required?: boolean; disabled?: boolean }) { return <label className="block text-sm font-medium">{label}<select className="input mt-1" disabled={disabled} required={required} value={value} onChange={event => change(event.target.value)}>{children}</select></label>; }
