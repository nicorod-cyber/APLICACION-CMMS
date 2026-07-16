import { ChangeEvent, DragEvent, useEffect, useMemo, useState } from "react";
import { CheckCircle2, Download, FileSpreadsheet, RefreshCw, UploadCloud, XCircle } from "lucide-react";
import { apiFetch, useAuthStore } from "../auth/authStore";

type ImportStatus = "Draft" | "Validating" | "PendingApproval" | "Approved" | "Applied" | "Rejected" | "Failed";

type ImportSummary = {
  totalRows: number;
  newRows: number;
  updatedRows: number;
  unchangedRows: number;
  errorRows: number;
  duplicateRows: number;
};

type ImportListItem = {
  id: string;
  entity: string;
  schemaName: string;
  originalFileName: string;
  status: ImportStatus;
  simulateOnly: boolean;
  uploadedAtUtc: string;
  uploadedBy: string;
  appliedAtUtc?: string | null;
  appliedBy?: string | null;
  rejectedAtUtc?: string | null;
  rejectedBy?: string | null;
  rejectReason?: string | null;
  summary: ImportSummary;
};

type ImportError = {
  rowNumber: number;
  columnName: string;
  message: string;
};

type ImportPreviewRow = {
  rowNumber: number;
  values: Record<string, string | null>;
  operation: string;
  errors: ImportError[];
};

type ImportPreview = {
  import: ImportListItem;
  rows: ImportPreviewRow[];
  errors: ImportError[];
};

const importEntities = [
  ["faenas", "Faenas"],
  ["usuarios", "Usuarios"],
  ["bodegas", "Bodegas"],
  ["repuestos", "Repuestos"],
  ["stock_bodegas", "Stock por almacen"],
  ["document_types", "Tipos documentales"],
  ["documentos", "Documentos"],
  ["proveedores", "Proveedores"],
  ["abastecimiento_solicitudes", "Solicitudes abastecimiento"],
  ["ordenes_compra", "Ordenes de compra"],
  ["recepciones_abastecimiento", "Recepciones abastecimiento"],
  ["avisos_trabajo", "Avisos de trabajo"],
  ["ordenes_trabajo", "Ordenes de trabajo"],
  ["tareas_ot", "Tareas de OT"],
  ["ot_tecnicos_tarea", "Tecnicos por tarea OT"],
  ["ot_hh", "HH de OT"],
  ["ot_evidencias", "Evidencias de OT"],
  ["ot_repuestos", "Repuestos de OT"],
  ["ot_checklists", "Checklist de OT"],
  ["ot_firmas", "Firmas de OT"],
  ["ot_estado_historial", "Historial estado OT"],
  ["programacion_talleres", "Talleres programacion"],
  ["programacion_ot", "Programacion OT"],
  ["programacion_dependencias", "Dependencias programacion"],
  ["programacion_alertas", "Alertas programacion"],
  ["sistemas_componentes", "Sistemas / componentes"],
  ["planes_preventivos", "Planes preventivos"],
  ["preventivo_evaluaciones", "Evaluaciones preventivas"],
  ["preventivo_historial", "Historial preventivo"],
  ["disponibilidad_contratos", "Contratos disponibilidad"],
  ["disponibilidad_activos_contrato", "Activos por contrato"],
  ["disponibilidad_eventos", "Eventos disponibilidad"],
  ["disponibilidad_snapshots", "Snapshots disponibilidad"],
  ["checklists", "Checklists"],
  ["ot_historicas", "OT historicas"]
];

export function ImportsPage() {
  const token = useAuthStore((state) => state.token);
  const [imports, setImports] = useState<ImportListItem[]>([]);
  const [selected, setSelected] = useState<ImportPreview | null>(null);
  const [entity, setEntity] = useState(importEntities[0][0]);
  const [simulateOnly, setSimulateOnly] = useState(false);
  const [isDragging, setIsDragging] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [rejectReason, setRejectReason] = useState("");

  useEffect(() => {
    void loadImports();
  }, []);

  const previewColumns = useMemo(() => {
    if (!selected?.rows[0]) {
      return [];
    }

    return Object.keys(selected.rows[0].values);
  }, [selected]);

  async function loadImports() {
    setIsLoading(true);
    setError(null);

    try {
      const data = await apiFetch<ImportListItem[]>("/api/imports");
      setImports(data);
      if (data[0]) {
        await loadPreview(data[0].id);
      }
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar importaciones.");
    } finally {
      setIsLoading(false);
    }
  }

  async function loadPreview(id: string) {
    const data = await apiFetch<ImportPreview>(`/api/imports/${id}/preview`);
    setSelected(data);
  }

  async function uploadFile(file: File) {
    setIsUploading(true);
    setError(null);

    try {
      const effectiveEntity = inferEntityFromFileName(file.name) ?? entity;
      const formData = new FormData();
      formData.append("entity", effectiveEntity);
      formData.append("simulateOnly", String(simulateOnly));
      formData.append("file", file);

      const data = await apiFetch<ImportPreview>("/api/imports/upload", {
        method: "POST",
        body: formData
      });

      setSelected(data);
      setEntity(effectiveEntity);
      await loadImports();
    } catch (uploadError) {
      setError(uploadError instanceof Error ? uploadError.message : "No fue posible cargar el archivo.");
    } finally {
      setIsUploading(false);
    }
  }

  async function approveSelected() {
    if (!selected) {
      return;
    }

    try {
      const data = await apiFetch<ImportPreview>(`/api/imports/${selected.import.id}/approve`, { method: "POST" });
      setSelected(data);
      await loadImports();
    } catch (approveError) {
      setError(approveError instanceof Error ? approveError.message : "No fue posible aprobar.");
    }
  }

  async function rejectSelected() {
    if (!selected) {
      return;
    }

    try {
      const data = await apiFetch<ImportPreview>(`/api/imports/${selected.import.id}/reject`, {
        method: "POST",
        body: JSON.stringify({ reason: rejectReason })
      });
      setSelected(data);
      setRejectReason("");
      await loadImports();
    } catch (rejectError) {
      setError(rejectError instanceof Error ? rejectError.message : "No fue posible rechazar.");
    }
  }

  async function downloadTemplate() {
    const headers = new Headers();
    if (token) {
      headers.set("Authorization", `Bearer ${token}`);
    }

    const response = await fetch(`/api/imports/templates/${entity}`, { headers });
    if (!response.ok) {
      setError("No fue posible descargar la plantilla.");
      return;
    }

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `plantilla_${entity}.xlsx`;
    link.click();
    URL.revokeObjectURL(url);
  }

  function handleFileInput(event: ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file) {
      void uploadFile(file);
    }
  }

  function handleDrop(event: DragEvent<HTMLLabelElement>) {
    event.preventDefault();
    setIsDragging(false);
    const file = event.dataTransfer.files[0];
    if (file) {
      void uploadFile(file);
    }
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Importaciones Excel</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Validacion, preview, aprobacion y auditoria.</p>
        </div>
        <button
          className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          onClick={() => void loadImports()}
          type="button"
        >
          <RefreshCw className="h-4 w-4" aria-hidden="true" />
          Actualizar
        </button>
      </div>

      <section className="grid gap-4 xl:grid-cols-[0.8fr_1.2fr]">
        <div className="space-y-4">
          <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-1">
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
                Entidad
                <select
                  className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
                  value={entity}
                  onChange={(event) => setEntity(event.target.value)}
                >
                  {importEntities.map(([value, label]) => (
                    <option key={value} value={value}>
                      {label}
                    </option>
                  ))}
                </select>
              </label>

              <label className="flex items-center gap-2 rounded-md border border-slate-200 px-3 py-2 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
                <input checked={simulateOnly} onChange={(event) => setSimulateOnly(event.target.checked)} type="checkbox" />
                Simular sin aplicar
              </label>
            </div>

            <label
              className={`mt-4 flex min-h-40 cursor-pointer flex-col items-center justify-center rounded-lg border-2 border-dashed px-4 text-center transition ${
                isDragging
                  ? "border-teal-500 bg-teal-50 dark:bg-teal-950"
                  : "border-slate-300 bg-slate-50 hover:border-teal-500 dark:border-slate-700 dark:bg-slate-950"
              }`}
              onDragLeave={() => setIsDragging(false)}
              onDragOver={(event) => {
                event.preventDefault();
                setIsDragging(true);
              }}
              onDrop={handleDrop}
            >
              <UploadCloud className="h-8 w-8 text-slate-500 dark:text-slate-400" aria-hidden="true" />
              <span className="mt-3 text-sm font-semibold text-slate-800 dark:text-slate-100">
                {isUploading ? "Cargando..." : "Arrastra un .xlsx o selecciona archivo"}
              </span>
              <input accept=".xlsx" className="sr-only" disabled={isUploading} onChange={handleFileInput} type="file" />
            </label>

            <button
              className="mt-4 inline-flex h-10 w-full items-center justify-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
              onClick={() => void downloadTemplate()}
              type="button"
            >
              <Download className="h-4 w-4" aria-hidden="true" />
              Descargar plantilla
            </button>

            {error ? <div className="mt-4 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">{error}</div> : null}
          </div>

          <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="border-b border-slate-200 px-4 py-3 dark:border-slate-800">
              <h2 className="text-base font-semibold text-slate-950 dark:text-white">Historial</h2>
            </div>
            <div className="max-h-96 overflow-y-auto">
              {isLoading ? (
                <p className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando...</p>
              ) : (
                imports.map((item) => (
                  <button
                    key={item.id}
                    className={`block w-full border-b border-slate-100 px-4 py-3 text-left text-sm transition hover:bg-slate-50 dark:border-slate-800 dark:hover:bg-slate-800 ${
                      selected?.import.id === item.id ? "bg-teal-50 dark:bg-teal-950/30" : ""
                    }`}
                    onClick={() => void loadPreview(item.id)}
                    type="button"
                  >
                    <div className="flex items-center justify-between gap-3">
                      <span className="font-semibold text-slate-900 dark:text-slate-100">{item.originalFileName}</span>
                      <StatusBadge status={item.status} />
                    </div>
                    <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{item.schemaName}</p>
                  </button>
                ))
              )}
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <SummaryPanel preview={selected} />

          <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="flex flex-col gap-3 border-b border-slate-200 px-4 py-3 dark:border-slate-800 md:flex-row md:items-center md:justify-between">
              <h2 className="text-base font-semibold text-slate-950 dark:text-white">Vista previa</h2>
              {selected ? (
                <div className="flex flex-wrap items-center gap-2">
                  <input
                    className="h-9 rounded-md border border-slate-300 bg-white px-3 text-xs outline-none ring-teal-500 transition focus:ring-2 dark:border-slate-700 dark:bg-slate-950"
                    placeholder="Motivo rechazo"
                    value={rejectReason}
                    onChange={(event) => setRejectReason(event.target.value)}
                  />
                  <button
                    className="inline-flex h-9 items-center gap-2 rounded-md border border-red-200 px-3 text-xs font-semibold text-red-700 transition hover:bg-red-50 dark:border-red-900 dark:text-red-200 dark:hover:bg-red-950"
                    onClick={() => void rejectSelected()}
                    type="button"
                  >
                    <XCircle className="h-3.5 w-3.5" aria-hidden="true" />
                    Rechazar
                  </button>
                  <button
                    className="inline-flex h-9 items-center gap-2 rounded-md bg-teal-700 px-3 text-xs font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
                    disabled={selected.import.status !== "PendingApproval" && selected.import.status !== "Failed"}
                    onClick={() => void approveSelected()}
                    type="button"
                  >
                    <CheckCircle2 className="h-3.5 w-3.5" aria-hidden="true" />
                    Aprobar
                  </button>
                </div>
              ) : null}
            </div>

            {selected ? (
              <div className="overflow-x-auto">
                <table className="min-w-full text-left text-sm">
                  <thead className="bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
                    <tr>
                      <th className="px-4 py-3 font-medium">Fila</th>
                      <th className="px-4 py-3 font-medium">Operacion</th>
                      {previewColumns.map((column) => (
                        <th key={column} className="px-4 py-3 font-medium">
                          {column}
                        </th>
                      ))}
                      <th className="px-4 py-3 font-medium">Errores</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                    {selected.rows.map((row) => (
                      <tr key={row.rowNumber} className={row.errors.length > 0 ? "bg-red-50/60 dark:bg-red-950/20" : ""}>
                        <td className="px-4 py-3 font-semibold text-slate-900 dark:text-slate-100">{row.rowNumber}</td>
                        <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{row.operation}</td>
                        {previewColumns.map((column) => (
                          <td key={column} className="max-w-60 truncate px-4 py-3 text-slate-600 dark:text-slate-300">
                            {row.values[column] ?? ""}
                          </td>
                        ))}
                        <td className="min-w-64 px-4 py-3 text-xs text-red-700 dark:text-red-300">
                          {row.errors.map((item) => `${item.columnName}: ${item.message}`).join(" | ")}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="flex min-h-64 items-center justify-center text-sm text-slate-500 dark:text-slate-400">
                <FileSpreadsheet className="mr-2 h-5 w-5" aria-hidden="true" />
                Selecciona o carga una importacion.
              </div>
            )}
          </section>
        </div>
      </section>
    </section>
  );
}

function SummaryPanel({ preview }: { preview: ImportPreview | null }) {
  const summary = preview?.import.summary;
  const items = [
    ["Nuevos", summary?.newRows ?? 0],
    ["Actualizados", summary?.updatedRows ?? 0],
    ["Sin cambios", summary?.unchangedRows ?? 0],
    ["Errores", summary?.errorRows ?? 0],
    ["Duplicados", summary?.duplicateRows ?? 0]
  ];

  return (
    <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Resumen</h2>
        {preview ? <StatusBadge status={preview.import.status} /> : null}
      </div>
      {preview?.import.rejectReason ? (
        <div className="mt-3 rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">
          {preview.import.rejectReason}
        </div>
      ) : null}
      <div className="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        {items.map(([label, value]) => (
          <div key={label} className="rounded-md border border-slate-200 p-3 dark:border-slate-800">
            <p className="text-xs font-medium text-slate-500 dark:text-slate-400">{label}</p>
            <p className="mt-2 text-2xl font-semibold text-slate-950 dark:text-white">{value}</p>
          </div>
        ))}
      </div>
    </section>
  );
}

function StatusBadge({ status }: { status: ImportStatus }) {
  const className =
    status === "Applied"
      ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
      : status === "Rejected"
        ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
        : status === "PendingApproval"
          ? "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200"
          : "bg-slate-100 text-slate-700 dark:bg-slate-800 dark:text-slate-200";

  return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${className}`}>{status}</span>;
}

function inferEntityFromFileName(fileName: string) {
  const value = fileName.toLowerCase();
  if (value.includes("faena")) {
    return "faenas";
  }
  if (value.includes("ubicacion")) {
    return "faenas";
  }
  if (value.includes("bodega") || value.includes("almacen")) {
    return "bodegas";
  }
  if (value.includes("document")) {
    return value.includes("type") || value.includes("tipo") ? "document_types" : "documentos";
  }
  if (value.includes("proveedor")) {
    return "proveedores";
  }
  if (value.includes("abastecimiento") || value.includes("procurement")) {
    if (value.includes("recepcion")) {
      return "recepciones_abastecimiento";
    }
    return "abastecimiento_solicitudes";
  }
  if (value.includes("oc") || value.includes("compra")) {
    return "ordenes_compra";
  }
  if (value.includes("aviso")) {
    return "avisos_trabajo";
  }
  if (value.includes("programacion") || value.includes("programación") || value.includes("calendario") || value.includes("gantt")) {
    if (value.includes("taller")) {
      return "programacion_talleres";
    }
    if (value.includes("dependencia")) {
      return "programacion_dependencias";
    }
    if (value.includes("alerta")) {
      return "programacion_alertas";
    }
    return "programacion_ot";
  }
  if (value.includes("ot") || value.includes("orden")) {
    if (value.includes("tecnico") || value.includes("asignacion")) {
      return "ot_tecnicos_tarea";
    }
    if (value.includes("hh") || value.includes("hora")) {
      return "ot_hh";
    }
    if (value.includes("evidencia")) {
      return "ot_evidencias";
    }
    if (value.includes("repuesto")) {
      return "ot_repuestos";
    }
    if (value.includes("firma")) {
      return "ot_firmas";
    }
    if (value.includes("historial") || value.includes("estado")) {
      return "ot_estado_historial";
    }
    if (value.includes("checklist")) {
      return "ot_checklists";
    }
    if (value.includes("tarea")) {
      return "tareas_ot";
    }
    return "ordenes_trabajo";
  }
  if (value.includes("sistema") || value.includes("componente")) {
    return "sistemas_componentes";
  }
  if (value.includes("repuesto")) {
    return "repuestos";
  }
  if (value.includes("stock")) {
    return "stock_bodegas";
  }
  if (value.includes("checklist")) {
    return "checklists";
  }
  if (value.includes("preventivo")) {
    if (value.includes("lectura")) {
      return null;
    }
    if (value.includes("evaluacion") || value.includes("evaluaci")) {
      return "preventivo_evaluaciones";
    }
    if (value.includes("historial")) {
      return "preventivo_historial";
    }
    return "planes_preventivos";
  }
  return null;
}
