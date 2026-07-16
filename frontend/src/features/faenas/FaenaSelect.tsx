import { useEffect, useId, useMemo, useState } from "react";
import { apiFetch } from "../auth/authStore";

export type FaenaRecord = {
  id: string;
  codigo: string;
  nombre: string;
  zona?: string | null;
  cliente?: string | null;
  centroCostes?: string | null;
  tipoFaena?: string | null;
  region?: string | null;
  comuna?: string | null;
  latitud?: number | null;
  longitud?: number | null;
  responsableUsuarioId?: string | null;
  responsableNombre?: string | null;
  activo: boolean;
  ubicacionTecnica: TechnicalLocationRecord | null;
};

export type TechnicalLocationRecord = {
  id: string;
  codigo: string;
  nombre: string;
  obsoleto: boolean;
};

type UseFaenasResult = {
  faenas: FaenaRecord[];
  isLoading: boolean;
  error: string | null;
};

type FaenaSelectProps = {
  label?: string;
  value: string;
  onChange: (value: string, faena?: FaenaRecord) => void;
  includeEmpty?: boolean;
  emptyLabel?: string;
  includeInactive?: boolean;
  disabled?: boolean;
};

type FaenaChecklistProps = {
  label?: string;
  value: string[];
  onChange: (value: string[]) => void;
  includeInactive?: boolean;
  compact?: boolean;
};

const faenaRequests = new Map<string, Promise<FaenaRecord[]>>();

export function useFaenas(includeInactive = true): UseFaenasResult {
  const [faenas, setFaenas] = useState<FaenaRecord[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    const cacheKey = includeInactive ? "all" : "active";

    async function loadFaenas() {
      setIsLoading(true);
      setError(null);

      try {
        let request = faenaRequests.get(cacheKey);
        if (!request) {
          const query = includeInactive ? "?includeInactive=true" : "";
          request = apiFetch<FaenaRecord[]>(`/api/faenas${query}`);
          faenaRequests.set(cacheKey, request);
        }

        const data = await request;
        if (isMounted) {
          setFaenas(data);
        }
      } catch (loadError) {
        faenaRequests.delete(cacheKey);
        if (isMounted) {
          setError(loadError instanceof Error ? loadError.message : "No fue posible cargar faenas.");
        }
      } finally {
        faenaRequests.delete(cacheKey);
        if (isMounted) {
          setIsLoading(false);
        }
      }
    }

    void loadFaenas();

    return () => {
      isMounted = false;
    };
  }, [includeInactive]);

  return { faenas, isLoading, error };
}

export function FaenaSelect({
  label = "Faena",
  value,
  onChange,
  includeEmpty = true,
  emptyLabel = "Todas",
  includeInactive = true,
  disabled
}: FaenaSelectProps) {
  const { faenas, isLoading, error } = useFaenas(includeInactive);
  const generatedId = useId();
  const id = `faena-select-${label.toLowerCase().replace(/\s+/g, "-")}-${generatedId}`;

  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <select
        id={id}
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 disabled:bg-slate-100 disabled:text-slate-500 dark:border-slate-700 dark:bg-slate-950 dark:disabled:bg-slate-900"
        disabled={disabled || isLoading}
        value={value}
        onChange={(event) => {
          const selectedValue = event.target.value;
          onChange(selectedValue, faenas.find((faena) => faena.codigo === selectedValue));
        }}
      >
        {includeEmpty ? <option value="">{isLoading ? "Cargando faenas..." : emptyLabel}</option> : null}
        {faenas.map((faena) => (
          <option key={faena.codigo} value={faena.codigo} title={faena.codigo}>
            {faena.nombre || faena.codigo}
          </option>
        ))}
      </select>
      {error ? <span className="mt-1 block text-xs text-red-700 dark:text-red-300">{error}</span> : null}
    </label>
  );
}

export function FaenaChecklist({ label = "Faenas", value, onChange, includeInactive = true, compact }: FaenaChecklistProps) {
  const { faenas, isLoading, error } = useFaenas(includeInactive);
  const selected = useMemo(() => new Set(value.map((item) => item.toLowerCase())), [value]);
  const catalogCodes = useMemo(() => new Set(faenas.map((faena) => faena.codigo.toLowerCase())), [faenas]);
  const unknownCodes = value.filter((code) => !catalogCodes.has(code.toLowerCase()));

  function toggle(code: string) {
    if (selected.has(code.toLowerCase())) {
      onChange(value.filter((item) => item.toLowerCase() !== code.toLowerCase()));
      return;
    }

    onChange([...value, code]);
  }

  return (
    <fieldset className="space-y-2">
      <legend className="text-sm font-medium text-slate-700 dark:text-slate-200">{label}</legend>
      <div className={`grid max-h-48 gap-2 overflow-y-auto rounded-md border border-slate-200 p-2 dark:border-slate-700 ${compact ? "grid-cols-1" : "sm:grid-cols-2"}`}>
        {isLoading ? <div className="text-xs text-slate-500 dark:text-slate-400">Cargando faenas...</div> : null}
        {faenas.map((faena) => (
          <label
            key={faena.codigo}
            className="flex min-h-9 items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-xs font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200"
            title={faena.codigo}
          >
            <input checked={selected.has(faena.codigo.toLowerCase())} onChange={() => toggle(faena.codigo)} type="checkbox" />
            <span>{faena.nombre || faena.codigo}</span>
          </label>
        ))}
        {unknownCodes.map((code) => (
          <label
            key={code}
            className="flex min-h-9 items-center gap-2 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-xs font-medium text-amber-800 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-200"
          >
            <input checked disabled type="checkbox" />
            <span>{code} sin catalogar</span>
          </label>
        ))}
      </div>
      {error ? <span className="block text-xs text-red-700 dark:text-red-300">{error}</span> : null}
    </fieldset>
  );
}
