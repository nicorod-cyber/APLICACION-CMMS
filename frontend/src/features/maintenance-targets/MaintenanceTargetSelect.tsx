import { useEffect, useMemo, useState } from "react";
import { apiFetch } from "../auth/authStore";

export type MaintenanceTargetType = "Asset" | "OperationalUnit";

export type MaintenanceTargetReference = {
  tipo: MaintenanceTargetType;
  codigo: string;
};

export type MaintenanceTarget = MaintenanceTargetReference & {
  nombre: string;
  categoriaCodigo: string;
  categoriaNombre: string;
  faenaCodigo?: string | null;
  faenaNombre?: string | null;
  estadoOperacionalCodigo: string;
  estadoOperacionalNombre: string;
  criticidad?: string | null;
  esComposicion: boolean;
  composicionCompleta?: boolean | null;
  esComponenteMontado: boolean;
  unidadOperativaVigenteCodigo?: string | null;
  unidadOperativaVigenteNombre?: string | null;
  rolComponenteVigente?: string | null;
  participaEnDisponibilidad: boolean;
};

type Props = {
  value: MaintenanceTargetReference | null;
  onChange: (value: MaintenanceTargetReference | null, target?: MaintenanceTarget) => void;
  faenaCodigo?: string;
  scope?: "Operational" | "All";
  soloDisponibilidad?: boolean;
  required?: boolean;
  disabled?: boolean;
  allowMountedComponents?: boolean;
  label?: string;
  error?: string;
  assetOnly?: boolean;
};

export function MaintenanceTargetSelect({
  value,
  onChange,
  faenaCodigo,
  scope = "Operational",
  soloDisponibilidad = false,
  required = false,
  disabled = false,
  allowMountedComponents = false,
  label = "Objetivo de mantenimiento",
  error,
  assetOnly = false
}: Props) {
  const [targets, setTargets] = useState<MaintenanceTarget[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(false);
  const effectiveScope = allowMountedComponents ? "All" : scope;

  useEffect(() => {
    let active = true;
    setLoading(true);
    const query = new URLSearchParams({ scope: effectiveScope, soloDisponibilidad: String(soloDisponibilidad) });
    if (faenaCodigo) query.set("faenaCodigo", faenaCodigo);
    if (search.trim()) query.set("search", search.trim());
    void apiFetch<MaintenanceTarget[]>(`/api/maintenance-targets?${query}`)
      .then((items) => {
        if (active) setTargets(items);
      })
      .catch(() => {
        if (active) setTargets([]);
      })
      .finally(() => {
        if (active) setLoading(false);
      });
    return () => {
      active = false;
    };
  }, [effectiveScope, faenaCodigo, search, soloDisponibilidad]);

  const options = useMemo(
    () => targets.filter((item) => !assetOnly || item.tipo === "Asset"),
    [assetOnly, targets]
  );
  const key = value ? `${value.tipo}:${value.codigo}` : "";

  return (
    <label>
      {label}
      <input
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        placeholder="Buscar por nombre, tipo, faena o código"
        disabled={disabled}
      />
      <select
        value={key}
        required={required}
        disabled={disabled}
        onChange={(event) => {
          const target = options.find((item) => `${item.tipo}:${item.codigo}` === event.target.value);
          onChange(target ? { tipo: target.tipo, codigo: target.codigo } : null, target);
        }}
      >
        <option value="">{loading ? "Cargando objetivos…" : "Selecciona un objetivo"}</option>
        <optgroup label="EQUIPOS OPERACIONALES">
          {options.filter((item) => !item.esComponenteMontado).map((item) => (
            <option key={`${item.tipo}:${item.codigo}`} value={`${item.tipo}:${item.codigo}`}>
              {labelFor(item)}
            </option>
          ))}
        </optgroup>
        {allowMountedComponents ? (
          <optgroup label="COMPONENTES FÍSICOS">
            {options.filter((item) => item.esComponenteMontado).map((item) => (
              <option key={`${item.tipo}:${item.codigo}`} value={`${item.tipo}:${item.codigo}`}>
                {labelFor(item)}
              </option>
            ))}
          </optgroup>
        ) : null}
      </select>
      {allowMountedComponents ? <small>Se incluyen componentes físicos montados para intervenciones específicas.</small> : null}
      {error ? <small className="text-red-600">{error}</small> : null}
    </label>
  );
}

export function labelFor(target: MaintenanceTarget) {
  return [target.nombre, target.categoriaNombre, target.faenaNombre ?? target.faenaCodigo, target.estadoOperacionalNombre]
    .filter(Boolean)
    .join(" · ");
}
