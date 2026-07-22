import { FormEvent, useEffect, useMemo, useState } from "react";
import {
  AlertTriangle,
  Check,
  GitMerge,
  Layers3,
  ListTree,
  Network,
  RefreshCw,
  Save,
  Search,
  Table2,
  Trash2
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { apiFetch } from "../auth/authStore";
import { FaenaSelect, type FaenaRecord, useFaenas } from "../faenas/FaenaSelect";

type TechnicalHierarchyLevel = "Sistema" | "Subsistema" | "Componente" | "Subcomponente";

type TechnicalNode = {
  codigo: string;
  nombre: string;
  nombreNormalizado: string;
  nivel: TechnicalHierarchyLevel;
  codigoPadre?: string | null;
  faenaCodigo?: string | null;
  familiasEquipo: string[];
  activosAsignados: string[];
  aliasHistoricos: string[];
  obsoleto: boolean;
  fusionadoEnCodigo?: string | null;
  fechaCreacionUtc?: string | null;
  fechaActualizacionUtc?: string | null;
  ruta: string;
  tieneHijos: boolean;
  enUso: boolean;
};

type TreeNode = {
  node: TechnicalNode;
  children: TreeNode[];
};

type SimilarNode = {
  node: TechnicalNode;
  candidate: TechnicalNode;
  similarity: number;
  reason: string;
};

type Filters = {
  faenaCodigo: string;
  familia: string;
  sistemaCodigo: string;
  nivel: string;
  includeObsolete: boolean;
};

type FormState = {
  codigo: string;
  nombre: string;
  nivel: TechnicalHierarchyLevel;
  codigoPadre: string;
  faenaCodigo: string;
  familiasEquipo: string;
  activosAsignados: string;
  aliasHistoricos: string;
  reason: string;
};

const emptyFilters: Filters = {
  faenaCodigo: "",
  familia: "",
  sistemaCodigo: "",
  nivel: "",
  includeObsolete: false
};

const emptyForm: FormState = {
  codigo: "",
  nombre: "",
  nivel: "Sistema",
  codigoPadre: "",
  faenaCodigo: "",
  familiasEquipo: "",
  activosAsignados: "",
  aliasHistoricos: "",
  reason: ""
};

const levelOptions: TechnicalHierarchyLevel[] = ["Sistema", "Subsistema", "Componente", "Subcomponente"];

export function TechnicalHierarchyPage() {
  const [nodes, setNodes] = useState<TechnicalNode[]>([]);
  const [tree, setTree] = useState<TreeNode[]>([]);
  const [duplicates, setDuplicates] = useState<SimilarNode[]>([]);
  const [selected, setSelected] = useState<TechnicalNode | null>(null);
  const [selectedCodes, setSelectedCodes] = useState<string[]>([]);
  const [filters, setFilters] = useState<Filters>(emptyFilters);
  const [form, setForm] = useState<FormState>(emptyForm);
  const [view, setView] = useState<"tree" | "table">("tree");
  const [isCreating, setIsCreating] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [familyBulk, setFamilyBulk] = useState("");
  const [assetAssignment, setAssetAssignment] = useState("");
  const [mergeReason, setMergeReason] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { faenas: faenaOptions } = useFaenas(true);

  useEffect(() => {
    void loadHierarchy();
  }, []);

  const systemOptions = useMemo(
    () => nodes.filter((node) => node.nivel === "Sistema").map((node) => node.codigo).sort((a, b) => a.localeCompare(b)),
    [nodes]
  );

  const familyOptions = useMemo(
    () => unique(nodes.flatMap((node) => node.familiasEquipo)),
    [nodes]
  );

  const selectedFormFaena = useMemo(
    () => faenaOptions.find((faena) => faena.codigo === form.faenaCodigo) ?? null,
    [faenaOptions, form.faenaCodigo]
  );

  async function loadHierarchy(nextFilters = filters) {
    setIsLoading(true);
    setError(null);

    try {
      const query = toQuery(nextFilters);
      const [nodeResult, treeResult, duplicateResult] = await Promise.all([
        apiFetch<TechnicalNode[]>(`/api/technical-hierarchy/nodes?${query}`),
        apiFetch<TreeNode[]>(`/api/technical-hierarchy/tree?${query}`),
        apiFetch<SimilarNode[]>(`/api/technical-hierarchy/duplicates?${query}`)
      ]);

      setNodes(nodeResult);
      setTree(treeResult);
      setDuplicates(duplicateResult);

      const stillSelected = selected ? nodeResult.find((node) => node.codigo === selected.codigo) : null;
      if (stillSelected) {
        selectNode(stillSelected);
      } else if (nodeResult[0]) {
        selectNode(nodeResult[0]);
      } else {
        setSelected(null);
        setForm(emptyForm);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar jerarquia tecnica.");
    } finally {
      setIsLoading(false);
    }
  }

  function applyFilters(nextFilters: Filters) {
    setFilters(nextFilters);
    void loadHierarchy(nextFilters);
  }

  function selectNode(node: TechnicalNode) {
    setSelected(node);
    setIsCreating(false);
    setForm(toForm(node));
    setAssetAssignment(node.activosAsignados.join("; "));
    setMessage(null);
    setError(null);
  }

  function startCreate(level?: TechnicalHierarchyLevel, parentCode?: string) {
    setSelected(null);
    setIsCreating(true);
    setForm({
      ...emptyForm,
      nivel: level ?? "Sistema",
      codigoPadre: parentCode ?? "",
      familia: ""
    } as FormState);
    setAssetAssignment("");
    setMessage(null);
    setError(null);
  }

  async function saveNode(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const payload = toPayload(form);
      const saved = isCreating
        ? await apiFetch<TechnicalNode>("/api/technical-hierarchy/nodes", {
            method: "POST",
            body: JSON.stringify(payload)
          })
        : await apiFetch<TechnicalNode>(`/api/technical-hierarchy/nodes/${encodeURIComponent(form.codigo)}`, {
            method: "PUT",
            body: JSON.stringify(payload)
          });

      setMessage(isCreating ? "Nodo creado." : "Nodo actualizado.");
      await loadHierarchy(filters);
      selectNode(saved);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible guardar el nodo.");
    } finally {
      setIsSaving(false);
    }
  }

  async function markObsolete() {
    if (!selected) {
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const updated = await apiFetch<TechnicalNode>(`/api/technical-hierarchy/nodes/${encodeURIComponent(selected.codigo)}/obsolete`, {
        method: "POST",
        body: JSON.stringify({ reason: form.reason || "Maestro cerrado; marcado obsoleto" })
      });
      setMessage("Nodo marcado obsoleto.");
      await loadHierarchy({ ...filters, includeObsolete: true });
      selectNode(updated);
    } catch (obsoleteError) {
      setError(obsoleteError instanceof Error ? obsoleteError.message : "No fue posible marcar obsoleto.");
    } finally {
      setIsSaving(false);
    }
  }

  async function assignFamilies() {
    if (selectedCodes.length === 0 || !familyBulk.trim()) {
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      await apiFetch<TechnicalNode[]>("/api/technical-hierarchy/families", {
        method: "POST",
        body: JSON.stringify({
          nodeCodes: selectedCodes,
          families: parseList(familyBulk),
          append: true
        })
      });
      setFamilyBulk("");
      setSelectedCodes([]);
      setMessage("Familias asignadas.");
      await loadHierarchy(filters);
    } catch (assignError) {
      setError(assignError instanceof Error ? assignError.message : "No fue posible asignar familias.");
    } finally {
      setIsSaving(false);
    }
  }

  async function assignAssets() {
    if (!selected) {
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const updated = await apiFetch<TechnicalNode>(`/api/technical-hierarchy/nodes/${encodeURIComponent(selected.codigo)}/assets`, {
        method: "POST",
        body: JSON.stringify({
          assetCodes: parseList(assetAssignment),
          append: false
        })
      });
      setMessage("Activos asignados.");
      await loadHierarchy(filters);
      selectNode(updated);
    } catch (assignError) {
      setError(assignError instanceof Error ? assignError.message : "No fue posible asignar activos.");
    } finally {
      setIsSaving(false);
    }
  }

  async function merge(sourceCode: string, targetCode: string) {
    if (!mergeReason.trim()) {
      setError("Debe indicar motivo de fusion.");
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const merged = await apiFetch<TechnicalNode>("/api/technical-hierarchy/merge", {
        method: "POST",
        body: JSON.stringify({
          sourceCode,
          targetCode,
          reason: mergeReason
        })
      });
      setMergeReason("");
      setMessage("Duplicado fusionado.");
      await loadHierarchy({ ...filters, includeObsolete: true });
      selectNode(merged);
    } catch (mergeError) {
      setError(mergeError instanceof Error ? mergeError.message : "No fue posible fusionar.");
    } finally {
      setIsSaving(false);
    }
  }

  function toggleSelected(code: string) {
    setSelectedCodes((current) => (current.includes(code) ? current.filter((item) => item !== code) : [...current, code]));
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Jerarquia tecnica</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Sistemas, subsistemas, componentes y subcomponentes.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            onClick={() => void loadHierarchy(filters)}
            type="button"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Actualizar
          </button>
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
            onClick={() => startCreate()}
            type="button"
          >
            Nuevo nodo
          </button>
        </div>
      </div>

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-5">
          <FaenaSelect value={filters.faenaCodigo} onChange={(value) => applyFilters({ ...filters, faenaCodigo: value })} />
          <FilterSelect label="Familia" value={filters.familia} options={familyOptions} onChange={(value) => applyFilters({ ...filters, familia: value })} />
          <FilterSelect
            label="Sistema"
            value={filters.sistemaCodigo}
            options={systemOptions}
            onChange={(value) => applyFilters({ ...filters, sistemaCodigo: value })}
          />
          <FilterSelect label="Nivel" value={filters.nivel} options={levelOptions} onChange={(value) => applyFilters({ ...filters, nivel: value })} />
          <label className="flex h-10 items-center gap-2 self-end rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
            <input
              checked={filters.includeObsolete}
              onChange={(event) => applyFilters({ ...filters, includeObsolete: event.target.checked })}
              type="checkbox"
            />
            Obsoletos
          </label>
        </div>
      </section>

      <section className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
        <div className="space-y-4">
          <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="flex flex-col gap-3 border-b border-slate-200 px-4 py-3 dark:border-slate-800 md:flex-row md:items-center md:justify-between">
              <div className="flex items-center gap-2">
                <button
                  className={`inline-flex h-9 items-center gap-2 rounded-md px-3 text-sm font-semibold transition ${
                    view === "tree" ? "bg-slate-900 text-white dark:bg-white dark:text-slate-950" : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
                  }`}
                  onClick={() => setView("tree")}
                  type="button"
                >
                  <ListTree className="h-4 w-4" aria-hidden="true" />
                  Arbol
                </button>
                <button
                  className={`inline-flex h-9 items-center gap-2 rounded-md px-3 text-sm font-semibold transition ${
                    view === "table" ? "bg-slate-900 text-white dark:bg-white dark:text-slate-950" : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
                  }`}
                  onClick={() => setView("table")}
                  type="button"
                >
                  <Table2 className="h-4 w-4" aria-hidden="true" />
                  Tabla
                </button>
              </div>
              <span className="text-sm text-slate-500 dark:text-slate-400">{nodes.length} nodos</span>
            </div>

            {isLoading ? (
              <div className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando jerarquia...</div>
            ) : view === "tree" ? (
              <div className="max-h-[620px] overflow-auto p-3">
                {tree.length === 0 ? <EmptyState icon={Network} text="Sin nodos." /> : tree.map((item) => <TreeItem key={item.node.codigo} item={item} selected={selected?.codigo} onSelect={selectNode} onCreateChild={startCreate} />)}
              </div>
            ) : (
              <HierarchyTable
                nodes={nodes}
                selectedCode={selected?.codigo}
                selectedCodes={selectedCodes}
                onSelect={selectNode}
                onToggle={toggleSelected}
              />
            )}
          </section>

          <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
              <div>
                <h2 className="text-base font-semibold text-slate-950 dark:text-white">Asignacion masiva a familias</h2>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{selectedCodes.length} nodos seleccionados.</p>
              </div>
              <div className="flex flex-1 flex-wrap gap-2 md:justify-end">
                <input
                  className="h-10 min-w-64 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
                  placeholder="Familias separadas por ;"
                  value={familyBulk}
                  onChange={(event) => setFamilyBulk(event.target.value)}
                />
                <button
                  className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
                  disabled={isSaving || selectedCodes.length === 0 || !familyBulk.trim()}
                  onClick={() => void assignFamilies()}
                  type="button"
                >
                  <Check className="h-4 w-4" aria-hidden="true" />
                  Asignar
                </button>
              </div>
            </div>
          </section>

          <DuplicatesPanel duplicates={duplicates} mergeReason={mergeReason} onReason={setMergeReason} onMerge={merge} />
        </div>

        <div className="space-y-4">
          <form
            className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900"
            onSubmit={(event) => void saveNode(event)}
          >
            <div className="flex items-start justify-between gap-3">
              <div>
                <h2 className="text-base font-semibold text-slate-950 dark:text-white">
                  {isCreating ? "Nuevo nodo" : selected ? `${selected.codigo} · ${selected.nombre}` : "Ficha de componente"}
                </h2>
                <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{selected?.ruta ?? "Selecciona o crea un nodo."}</p>
              </div>
              {selected?.obsoleto ? <span className="rounded-full bg-red-50 px-2 py-1 text-xs font-semibold text-red-700 dark:bg-red-950 dark:text-red-200">Obsoleto</span> : null}
            </div>

            <div className="mt-4 grid gap-3 md:grid-cols-2">
              <Field disabled={!isCreating} label="Codigo" value={form.codigo} onChange={(value) => setForm({ ...form, codigo: value })} />
              <Field label="Nombre" value={form.nombre} onChange={(value) => setForm({ ...form, nombre: value })} />
              <SelectField label="Nivel" disabled={!isCreating} value={form.nivel} options={levelOptions} onChange={(value) => setForm({ ...form, nivel: value as TechnicalHierarchyLevel, codigoPadre: value === "Sistema" ? "" : form.codigoPadre })} />
              <Field disabled={form.nivel === "Sistema"} label="Codigo padre" value={form.nivel === "Sistema" ? "" : form.codigoPadre} onChange={(value) => setForm({ ...form, codigoPadre: value })} />
              <FaenaSelect
                emptyLabel="Selecciona faena"
                includeInactive={false}
                value={form.faenaCodigo}
                onChange={(value) => setForm({ ...form, faenaCodigo: value })}
              />
              <DerivedTechnicalLocation faena={selectedFormFaena} />
            </div>

            <div className="mt-4 grid gap-3">
              <Field label="Familias" value={form.familiasEquipo} onChange={(value) => setForm({ ...form, familiasEquipo: value })} />
              <Field label="Alias historicos" value={form.aliasHistoricos} onChange={(value) => setForm({ ...form, aliasHistoricos: value })} />
              <Field label="Motivo" value={form.reason} onChange={(value) => setForm({ ...form, reason: value })} />
            </div>

            {selected ? (
              <div className="mt-4 rounded-md border border-slate-200 p-3 dark:border-slate-800">
                <div className="grid gap-2 text-sm md:grid-cols-2">
                  <Detail label="Normalizado" value={selected.nombreNormalizado} />
                  <Detail label="En uso" value={selected.enUso ? "Si" : "No"} />
                  <Detail label="Hijos" value={selected.tieneHijos ? "Si" : "No"} />
                  <Detail label="Fusionado en" value={selected.fusionadoEnCodigo ?? "-"} />
                </div>
              </div>
            ) : null}

            <div className="mt-4 flex flex-wrap items-center gap-2">
              <button
                className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
                disabled={isSaving}
                type="submit"
              >
                <Save className="h-4 w-4" aria-hidden="true" />
                Guardar
              </button>
              {selected ? (
                <button
                  className="inline-flex h-10 items-center gap-2 rounded-md border border-red-200 px-4 text-sm font-semibold text-red-700 transition hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60 dark:border-red-900 dark:text-red-200 dark:hover:bg-red-950"
                  disabled={isSaving || selected.obsoleto}
                  onClick={() => void markObsolete()}
                  type="button"
                >
                  <Trash2 className="h-4 w-4" aria-hidden="true" />
                  Obsoleto
                </button>
              ) : null}
            </div>
          </form>

          <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <h2 className="text-base font-semibold text-slate-950 dark:text-white">Asignacion a activos</h2>
            <div className="mt-3 flex flex-col gap-2">
              <textarea
                className="min-h-24 rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
                placeholder="Codigos de activos separados por ;"
                value={assetAssignment}
                onChange={(event) => setAssetAssignment(event.target.value)}
              />
              <button
                className="inline-flex h-10 w-fit items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                disabled={isSaving || !selected}
                onClick={() => void assignAssets()}
                type="button"
              >
                <Layers3 className="h-4 w-4" aria-hidden="true" />
                Guardar activos
              </button>
            </div>
          </section>

          {message ? <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">{message}</div> : null}
          {error ? <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">{error}</div> : null}
        </div>
      </section>
    </section>
  );
}

function TreeItem({
  item,
  selected,
  onSelect,
  onCreateChild,
  depth = 0
}: {
  item: TreeNode;
  selected?: string;
  onSelect: (node: TechnicalNode) => void;
  onCreateChild: (level?: TechnicalHierarchyLevel, parentCode?: string) => void;
  depth?: number;
}) {
  const nextLevel = getNextLevel(item.node.nivel);
  return (
    <div>
      <div
        className={`flex items-center justify-between gap-2 rounded-md px-3 py-2 text-sm transition hover:bg-slate-100 dark:hover:bg-slate-800 ${
          selected === item.node.codigo ? "bg-teal-50 dark:bg-teal-950/30" : ""
        }`}
        style={{ marginLeft: depth * 18 }}
      >
        <button className="min-w-0 flex-1 text-left" onClick={() => onSelect(item.node)} type="button">
          <span className="font-semibold text-slate-900 dark:text-slate-100">{item.node.codigo}</span>
          <span className="ml-2 text-slate-600 dark:text-slate-300">{item.node.nombre}</span>
          <span className="ml-2 text-xs text-slate-500 dark:text-slate-400">{item.node.nivel}</span>
        </button>
        {nextLevel ? (
          <button
            className="h-8 rounded-md border border-slate-200 px-2 text-xs font-semibold text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            onClick={() => onCreateChild(nextLevel, item.node.codigo)}
            type="button"
          >
            +
          </button>
        ) : null}
      </div>
      {item.children.map((child) => (
        <TreeItem key={child.node.codigo} item={child} selected={selected} onSelect={onSelect} onCreateChild={onCreateChild} depth={depth + 1} />
      ))}
    </div>
  );
}

function HierarchyTable({
  nodes,
  selectedCode,
  selectedCodes,
  onSelect,
  onToggle
}: {
  nodes: TechnicalNode[];
  selectedCode?: string;
  selectedCodes: string[];
  onSelect: (node: TechnicalNode) => void;
  onToggle: (code: string) => void;
}) {
  return (
    <div className="max-h-[620px] overflow-auto">
      <table className="min-w-full text-left text-sm">
        <thead className="sticky top-0 bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
          <tr>
            <th className="px-4 py-3 font-medium">Sel.</th>
            <th className="px-4 py-3 font-medium">Codigo</th>
            <th className="px-4 py-3 font-medium">Nombre</th>
            <th className="px-4 py-3 font-medium">Nivel</th>
            <th className="px-4 py-3 font-medium">Familias</th>
            <th className="px-4 py-3 font-medium">Estado</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
          {nodes.map((node) => (
            <tr key={node.codigo} className={selectedCode === node.codigo ? "bg-teal-50/70 dark:bg-teal-950/30" : ""}>
              <td className="px-4 py-3">
                <input checked={selectedCodes.includes(node.codigo)} onChange={() => onToggle(node.codigo)} type="checkbox" />
              </td>
              <td className="px-4 py-3 font-semibold text-slate-900 dark:text-slate-100">
                <button onClick={() => onSelect(node)} type="button">{node.codigo}</button>
              </td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{node.nombre}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{node.nivel}</td>
              <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{node.familiasEquipo.join(", ") || "-"}</td>
              <td className="px-4 py-3">
                <StatusPill node={node} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function DuplicatesPanel({
  duplicates,
  mergeReason,
  onReason,
  onMerge
}: {
  duplicates: SimilarNode[];
  mergeReason: string;
  onReason: (value: string) => void;
  onMerge: (source: string, target: string) => void;
}) {
  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Duplicados similares</h2>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">{duplicates.length} coincidencias detectadas.</p>
        </div>
        <input
          className="h-10 min-w-72 rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
          placeholder="Motivo fusion autorizada"
          value={mergeReason}
          onChange={(event) => onReason(event.target.value)}
        />
      </div>
      {duplicates.length === 0 ? (
        <EmptyState icon={Search} text="Sin duplicados visibles." />
      ) : (
        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
              <tr>
                <th className="px-4 py-3 font-medium">Origen</th>
                <th className="px-4 py-3 font-medium">Destino</th>
                <th className="px-4 py-3 font-medium">Similitud</th>
                <th className="px-4 py-3 font-medium">Accion</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {duplicates.map((item) => (
                <tr key={`${item.node.codigo}-${item.candidate.codigo}`}>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{item.candidate.codigo} · {item.candidate.nombre}</td>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{item.node.codigo} · {item.node.nombre}</td>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{Math.round(item.similarity * 100)}%</td>
                  <td className="px-4 py-3">
                    <button
                      className="inline-flex h-9 items-center gap-2 rounded-md border border-slate-200 px-3 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      onClick={() => onMerge(item.candidate.codigo, item.node.codigo)}
                      type="button"
                    >
                      <GitMerge className="h-3.5 w-3.5" aria-hidden="true" />
                      Fusionar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function StatusPill({ node }: { node: TechnicalNode }) {
  const className = node.obsoleto
    ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
    : node.enUso
      ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
      : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200";

  return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${className}`}>{node.obsoleto ? "Obsoleto" : node.enUso ? "En uso" : "Disponible"}</span>;
}

function Field({
  label,
  value,
  onChange,
  disabled
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  const id = `hierarchy-${label.toLowerCase().replace(/\s+/g, "-")}`;
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <input
        id={id}
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 disabled:bg-slate-100 disabled:text-slate-500 dark:border-slate-700 dark:bg-slate-950 dark:disabled:bg-slate-900"
        disabled={disabled}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function SelectField({
  label,
  value,
  options,
  onChange,
  disabled
}: {
  label: string;
  value: string;
  options: readonly string[];
  onChange: (value: string) => void;
  disabled?: boolean;
}) {
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
      {label}
      <select
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 disabled:bg-slate-100 disabled:text-slate-500 dark:border-slate-700 dark:bg-slate-950 dark:disabled:bg-slate-900"
        disabled={disabled}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </label>
  );
}

function FilterSelect({
  label,
  value,
  options,
  onChange
}: {
  label: string;
  value: string;
  options: readonly string[];
  onChange: (value: string) => void;
}) {
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
      {label}
      <select
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        <option value="">Todos</option>
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    </label>
  );
}

function DerivedTechnicalLocation({ faena }: { faena: FaenaRecord | null }) {
  const location = faena?.ubicacionTecnica;
  return (
    <div className="rounded-md border border-slate-200 px-3 py-2 text-sm dark:border-slate-700">
      <span className="block text-xs font-medium text-slate-500 dark:text-slate-400">Ubicación técnica derivada</span>
      {location ? (
        <span className="block font-medium text-slate-800 dark:text-slate-100">
          {location.codigo} · {location.nombre}{location.obsoleto ? " (obsoleta)" : ""}
        </span>
      ) : (
        <span className="block text-slate-500 dark:text-slate-400">Selecciona una faena con ubicación técnica.</span>
      )}
    </div>
  );
}
function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-semibold uppercase text-slate-500 dark:text-slate-400">{label}</dt>
      <dd className="mt-1 break-words text-slate-800 dark:text-slate-100">{value}</dd>
    </div>
  );
}

function EmptyState({ icon: Icon, text }: { icon: LucideIcon; text: string }) {
  return (
    <div className="flex min-h-28 items-center justify-center text-sm text-slate-500 dark:text-slate-400">
      <Icon className="mr-2 h-5 w-5" aria-hidden="true" />
      {text}
    </div>
  );
}

function getNextLevel(level: TechnicalHierarchyLevel) {
  if (level === "Sistema") {
    return "Subsistema";
  }

  if (level === "Subsistema") {
    return "Componente";
  }

  if (level === "Componente") {
    return "Subcomponente";
  }

  return null;
}

function toForm(node: TechnicalNode): FormState {
  return {
    codigo: node.codigo,
    nombre: node.nombre,
    nivel: node.nivel,
    codigoPadre: node.codigoPadre ?? "",
    faenaCodigo: node.faenaCodigo ?? "",
    familiasEquipo: node.familiasEquipo.join("; "),
    activosAsignados: node.activosAsignados.join("; "),
    aliasHistoricos: node.aliasHistoricos.join("; "),
    reason: ""
  };
}

function toPayload(form: FormState) {
  return {
    codigo: form.codigo,
    nombre: form.nombre,
    nivel: form.nivel,
    codigoPadre: form.nivel === "Sistema" ? null : emptyToNull(form.codigoPadre),
    faenaCodigo: emptyToNull(form.faenaCodigo),
    familiasEquipo: parseList(form.familiasEquipo),
    activosAsignados: parseList(form.activosAsignados),
    aliasHistoricos: parseList(form.aliasHistoricos),
    reason: emptyToNull(form.reason)
  };
}

function toQuery(filters: Filters) {
  const query = new URLSearchParams();
  if (filters.faenaCodigo) {
    query.set("faenaCodigo", filters.faenaCodigo);
  }
  if (filters.familia) {
    query.set("familia", filters.familia);
  }
  if (filters.sistemaCodigo) {
    query.set("sistemaCodigo", filters.sistemaCodigo);
  }
  if (filters.nivel) {
    query.set("nivel", filters.nivel);
  }
  if (filters.includeObsolete) {
    query.set("includeObsolete", "true");
  }
  return query.toString();
}

function parseList(value: string) {
  return value
    .split(/[;,]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function unique(values: string[]) {
  return Array.from(new Set(values.map((value) => value.trim()).filter(Boolean))).sort((a, b) => a.localeCompare(b));
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}
