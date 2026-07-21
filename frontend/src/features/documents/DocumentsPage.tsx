import { FormEvent, useEffect, useMemo, useState } from "react";
import {
  Ban,
  CheckCircle2,
  ExternalLink,
  FileCheck2,
  FileClock,
  FileText,
  Link2,
  RefreshCw,
  Save,
  ShieldAlert,
  UploadCloud,
  XCircle
} from "lucide-react";
import type { LucideIcon } from "lucide-react";
import { AUTH_PERMISSIONS, AUTH_ROLES, apiFetch, useAuthStore } from "../auth/authStore";
import { FaenaSelect } from "../faenas/FaenaSelect";

type DocumentEntityType = "Activo" | "OT" | "Faena";
type DocumentStatus =
  | "Vigente"
  | "PorVencer"
  | "Vencido"
  | "PendienteCarga"
  | "PendienteValidacion"
  | "Rechazado"
  | "Reemplazado"
  | "Anulado";

type DocumentType = {
  codigo: string;
  nombre: string;
  aplicaA?: DocumentEntityType | null;
  obligatorio: boolean;
  critico: boolean;
  bloqueaDisponibilidad: boolean;
  plazoAlertaDias: number;
  rolesResponsables: string[];
  requierePdfAlerta: boolean;
  plantillaHtmlCodigo?: string | null;
  activo: boolean;
};

type DocumentRecord = {
  documentoId: string;
  entidadTipo: DocumentEntityType;
  entidadCodigo: string;
  tipoDocumento: string;
  estado: DocumentStatus;
  fechaEmision?: string | null;
  fechaVencimiento?: string | null;
  archivoKey?: string | null;
  sharePointUrl?: string | null;
  critico: boolean;
  obligatorio: boolean;
  bloqueaDisponibilidad: boolean;
  esHistorico: boolean;
  fechaVencimientoValidada: boolean;
  validadoPor?: string | null;
  validadoEnUtc?: string | null;
  rechazadoPor?: string | null;
  rechazadoEnUtc?: string | null;
  motivoRechazo?: string | null;
  reemplazaDocumentoId?: string | null;
  reemplazadoPorDocumentoId?: string | null;
  anuladoPor?: string | null;
  anuladoEnUtc?: string | null;
  motivoAnulacion?: string | null;
  fechaCargaUtc: string;
  cargadoPor: string;
  diasParaVencer?: number | null;
  bloqueaDisponibilidadActual: boolean;
};

type DocumentVersion = {
  versionId: string;
  documentoId: string;
  numeroVersion: number;
  codigoVersion: string;
  archivoId: string;
  archivoKey: string;
  sharePointUrl?: string | null;
  fechaCargaUtc: string;
  cargadoPor: string;
  observaciones?: string | null;
  vigente: boolean;
  fechaEmision?: string | null;
  fechaVencimiento?: string | null;
  estadoValidacion?: string | null;
  validadoPor?: string | null;
  validadoEnUtc?: string | null;
  rechazadoPor?: string | null;
  rechazadoEnUtc?: string | null;
  motivoRechazo?: string | null;
  reemplazaVersionId?: string | null;
  responsableCorreccion?: string | null;
  estadoCorreccion?: string | null;
  observacionCorreccion?: string | null;
  cicloCorreccionId?: string | null;
};

type RequirementMatrixVersion = {
  id: string;
  codigo: string;
  numeroVersion: number;
  tipoActivoCodigo: string;
  familiaEquipoCodigo?: string | null;
  vigenciaDesde: string;
  vigenciaHasta?: string | null;
  estado: string;
  creadoPor: string;
  motivoCambio?: string | null;
  requisitos: { id: string; tipoDocumentoCodigo: string; obligatorio: boolean; critico: boolean; bloqueaDisponibilidad: boolean; requiereFechaVencimiento: boolean; diasAnticipacion: number }[];
};
type DocumentMatrixRow = {
  entidadTipo: DocumentEntityType;
  entidadCodigo: string;
  nombreEntidad: string;
  tipoDocumento: string;
  obligatorio: boolean;
  bloqueaDisponibilidad: boolean;
  estado: DocumentStatus;
  documentoId?: string | null;
  fechaVencimiento?: string | null;
  bloqueaDisponibilidadActual: boolean;
};

type DocumentSummary = {
  total: number;
  vigentes: number;
  porVencer: number;
  vencidos: number;
  pendientesCarga: number;
  pendientesValidacion: number;
  rechazados: number;
  reemplazados: number;
  anulados: number;
  bloqueanDisponibilidad: number;
};

type DocumentStorageMode = "ManualLink" | "LocalSimulation" | "GraphApiReady";
type DocumentStorageStatus = "Stored" | "ManualLink" | "PendingManualLink" | "GraphApiReady" | "InvalidPath";
type DocumentStoragePurpose = "Document" | "AlertPdf" | "Evidence" | "ImportBackup";

type StorageProviderInfo = {
  mode: DocumentStorageMode;
  provider: string;
  supportsUpload: boolean;
  requiresManualLink: boolean;
  graphConfigured: boolean;
  rootPath: string;
  siteUrl?: string | null;
};

type DocumentStorageInfo = {
  fileKey: string;
  fileName: string;
  contentType: string;
  mode: DocumentStorageMode;
  purpose: DocumentStoragePurpose;
  status: DocumentStorageStatus;
  module: string;
  entityType: string;
  entityId: string;
  faenaCodigo?: string | null;
  activoCodigo?: string | null;
  otNumero?: string | null;
  relativePath: string;
  localPath?: string | null;
  url: string;
  sizeBytes: number;
  version: number;
  createdAtUtc: string;
  createdBy: string;
  metadataJson?: string | null;
};

type Filters = {
  entidadTipo: string;
  entidadCodigo: string;
  faenaCodigo: string;
  tipoDocumento: string;
  estado: string;
  includeHistorical: boolean;
};

type DocumentForm = {
  entidadTipo: DocumentEntityType;
  entidadCodigo: string;
  tipoDocumento: string;
  fechaEmision: string;
  fechaVencimiento: string;
  archivoKey: string;
  sharePointUrl: string;
  critico: boolean;
  obligatorio: boolean;
  bloqueaDisponibilidad: boolean;
  reason: string;
};

type TypeForm = {
  codigo: string;
  nombre: string;
  aplicaA: string;
  obligatorio: boolean;
  critico: boolean;
  bloqueaDisponibilidad: boolean;
  plazoAlertaDias: string;
  rolesResponsables: string;
  requierePdfAlerta: boolean;
  plantillaHtmlCodigo: string;
  activo: boolean;
  reason: string;
};

type Tab = "documentos" | "matriz" | "vencidos" | "porVencer" | "configuracion";

const emptyFilters: Filters = {
  entidadTipo: "",
  entidadCodigo: "",
  faenaCodigo: "",
  tipoDocumento: "",
  estado: "",
  includeHistorical: false
};

const emptyDocumentForm: DocumentForm = {
  entidadTipo: "Activo",
  entidadCodigo: "",
  tipoDocumento: "",
  fechaEmision: "",
  fechaVencimiento: "",
  archivoKey: "",
  sharePointUrl: "",
  critico: false,
  obligatorio: false,
  bloqueaDisponibilidad: false,
  reason: ""
};

const emptyTypeForm: TypeForm = {
  codigo: "",
  nombre: "",
  aplicaA: "",
  obligatorio: false,
  critico: false,
  bloqueaDisponibilidad: false,
  plazoAlertaDias: "30",
  rolesResponsables: "",
  requierePdfAlerta: false,
  plantillaHtmlCodigo: "",
  activo: true,
  reason: ""
};

const statusOptions: DocumentStatus[] = [
  "Vigente",
  "PorVencer",
  "Vencido",
  "PendienteCarga",
  "PendienteValidacion",
  "Rechazado",
  "Reemplazado",
  "Anulado"
];

const tabs: Array<[Tab, string]> = [
  ["documentos", "Documentos"],
  ["matriz", "Matriz"],
  ["vencidos", "Vencidos"],
  ["porVencer", "Por vencer"],
  ["configuracion", "Configuracion"]
];

export function DocumentsPage() {
  const user = useAuthStore((state) => state.user);
  const [types, setTypes] = useState<DocumentType[]>([]);
  const [documents, setDocuments] = useState<DocumentRecord[]>([]);
  const [expired, setExpired] = useState<DocumentRecord[]>([]);
  const [expiring, setExpiring] = useState<DocumentRecord[]>([]);
  const [matrix, setMatrix] = useState<DocumentMatrixRow[]>([]);
  const [requirementMatrices, setRequirementMatrices] = useState<RequirementMatrixVersion[]>([]);
  const [versions, setVersions] = useState<DocumentVersion[]>([]);
  const [summary, setSummary] = useState<DocumentSummary | null>(null);
  const [filters, setFilters] = useState<Filters>(emptyFilters);
  const [selected, setSelected] = useState<DocumentRecord | null>(null);
  const [documentForm, setDocumentForm] = useState<DocumentForm>(emptyDocumentForm);
  const [typeForm, setTypeForm] = useState<TypeForm>(emptyTypeForm);
  const [storageInfo, setStorageInfo] = useState<StorageProviderInfo | null>(null);
  const [activeTab, setActiveTab] = useState<Tab>("documentos");
  const [actionReason, setActionReason] = useState("");
  const [replaceMode, setReplaceMode] = useState(false);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const canManage = hasPermission(user?.permissions, AUTH_PERMISSIONS.manageDocuments);
  const canValidate = Boolean(user?.roles.includes(AUTH_ROLES.planner)) && hasPermission(user?.permissions, AUTH_PERMISSIONS.validateDocuments);
  const canConfigure = hasPermission(user?.permissions, AUTH_PERMISSIONS.configureDocumentTypes);

  useEffect(() => {
    void loadAll();
    void loadStorageStatus();
  }, []);

  useEffect(() => {
    if (!selected) {
      setVersions([]);
      return;
    }

    let active = true;
    void apiFetch<DocumentVersion[]>(`/api/documents/${encodeURIComponent(selected.documentoId)}/versions`)
      .then((items) => { if (active) setVersions(items); })
      .catch((loadError) => { if (active) setError(loadError instanceof Error ? loadError.message : "No fue posible cargar versiones."); });
    return () => { active = false; };
  }, [selected?.documentoId]);

  const typeCodes = useMemo(() => types.map((type) => type.codigo).sort((left, right) => left.localeCompare(right)), [types]);

  async function loadStorageStatus() {
    try {
      setStorageInfo(await apiFetch<StorageProviderInfo>("/api/sharepoint/status"));
    } catch {
      setStorageInfo(null);
    }
  }

  async function loadAll(nextFilters = filters) {
    setIsLoading(true);
    setError(null);

    try {
      const query = toQuery(nextFilters);
      const faenaQuery = nextFilters.faenaCodigo ? `?faenaCodigo=${encodeURIComponent(nextFilters.faenaCodigo)}` : "";
      const [typeResult, documentResult, expiredResult, expiringResult, matrixResult, summaryResult, requirementMatrixResult] = await Promise.all([
        apiFetch<DocumentType[]>("/api/documents/types"),
        apiFetch<DocumentRecord[]>(`/api/documents?${query}`),
        apiFetch<DocumentRecord[]>(`/api/documents/expired${faenaQuery}`),
        apiFetch<DocumentRecord[]>(`/api/documents/expiring${faenaQuery}`),
        apiFetch<DocumentMatrixRow[]>(`/api/documents/matrix${faenaQuery}`),
        apiFetch<DocumentSummary>(`/api/documents/summary${faenaQuery}`),
        apiFetch<RequirementMatrixVersion[]>("/api/documents/requirement-matrices?incluirHistoricas=true")
      ]);

      setTypes(typeResult);
      setDocuments(documentResult);
      setExpired(expiredResult);
      setExpiring(expiringResult);
      setMatrix(matrixResult);
      setSummary(summaryResult);
      setRequirementMatrices(requirementMatrixResult);

      const nextSelected = selected
        ? documentResult.find((document) => document.documentoId === selected.documentoId) ?? null
        : documentResult[0] ?? null;
      setSelected(nextSelected);
      setDocumentForm(nextSelected ? toDocumentForm(nextSelected) : emptyDocumentForm);
    } catch (loadError) {
      setError(loadError instanceof Error ? loadError.message : "No fue posible cargar documentos.");
    } finally {
      setIsLoading(false);
    }
  }

  function applyFilters(nextFilters: Filters) {
    setFilters(nextFilters);
    void loadAll(nextFilters);
  }

  function selectDocument(document: DocumentRecord) {
    setSelected(document);
    setDocumentForm(toDocumentForm(document));
    setReplaceMode(false);
    setActionReason("");
    setMessage(null);
    setError(null);
  }

  function startNewDocument() {
    setActiveTab("documentos");
    setSelected(null);
    setDocumentForm(emptyDocumentForm);
    setReplaceMode(false);
    setActionReason("");
    setMessage(null);
    setError(null);
  }

  function selectType(type: DocumentType) {
    setTypeForm({
      codigo: type.codigo,
      nombre: type.nombre,
      aplicaA: type.aplicaA ?? "",
      obligatorio: type.obligatorio,
      critico: type.critico,
      bloqueaDisponibilidad: type.bloqueaDisponibilidad,
      plazoAlertaDias: String(type.plazoAlertaDias),
      rolesResponsables: type.rolesResponsables.join("; "),
      requierePdfAlerta: type.requierePdfAlerta,
      plantillaHtmlCodigo: type.plantillaHtmlCodigo ?? "",
      activo: type.activo,
      reason: ""
    });
  }

  async function saveDocument(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      if (selected && replaceMode) {
        const replaced = await apiFetch<DocumentRecord>(`/api/documents/${encodeURIComponent(selected.documentoId)}/replace`, {
          method: "POST",
          body: JSON.stringify(toReplacePayload(documentForm))
        });
        setMessage("Documento reemplazado.");
        await loadAll(filters);
        selectDocument(replaced);
        return;
      }

      const saved = selected
        ? await apiFetch<DocumentRecord>(`/api/documents/${encodeURIComponent(selected.documentoId)}`, {
            method: "PUT",
            body: JSON.stringify(toUpdatePayload(documentForm))
          })
        : await apiFetch<DocumentRecord>("/api/documents", {
            method: "POST",
            body: JSON.stringify(toCreatePayload(documentForm))
          });

      setMessage(selected ? "Documento actualizado." : "Documento creado.");
      await loadAll(filters);
      selectDocument(saved);
    } catch (saveError) {
      setError(saveError instanceof Error ? saveError.message : "No fue posible guardar documento.");
    } finally {
      setIsSaving(false);
    }
  }

  async function validateSelected() {
    if (!selected) {
      return;
    }

    await runDocumentAction(
      () =>
        apiFetch<DocumentRecord>(`/api/documents/${encodeURIComponent(selected.documentoId)}/validate`, {
          method: "POST",
          body: JSON.stringify({ comments: actionReason || null })
        }),
      "Documento validado."
    );
  }

  async function rejectSelected() {
    if (!selected) {
      return;
    }

    await runDocumentAction(
      () =>
        apiFetch<DocumentRecord>(`/api/documents/${encodeURIComponent(selected.documentoId)}/reject`, {
          method: "POST",
          body: JSON.stringify({ reason: actionReason })
        }),
      "Documento rechazado."
    );
  }

  async function annulSelected() {
    if (!selected) {
      return;
    }

    await runDocumentAction(
      () =>
        apiFetch<DocumentRecord>(`/api/documents/${encodeURIComponent(selected.documentoId)}/annul`, {
          method: "POST",
          body: JSON.stringify({ reason: actionReason })
        }),
      "Documento anulado."
    );
  }

  async function runDocumentAction(action: () => Promise<DocumentRecord>, successMessage: string) {
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const updated = await action();
      setActionReason("");
      setMessage(successMessage);
      await loadAll(filters);
      selectDocument(updated);
    } catch (actionError) {
      setError(actionError instanceof Error ? actionError.message : "No fue posible completar la accion.");
    } finally {
      setIsSaving(false);
    }
  }

  async function uploadDocumentFile(file: File) {
    if (!documentForm.entidadCodigo.trim()) {
      setError("Debe indicar codigo de entidad antes de subir archivo.");
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const context = buildStorageContext(documentForm);
      const payload = new FormData();
      payload.append("file", file);
      payload.append("module", "Documents");
      payload.append("entityType", documentForm.entidadTipo);
      payload.append("entityId", documentForm.entidadCodigo);
      payload.append("purpose", "Document");
      payload.append("metadata.tipoDocumento", documentForm.tipoDocumento);
      if (context.faenaCodigo) {
        payload.append("faenaCodigo", context.faenaCodigo);
      }
      if (context.activoCodigo) {
        payload.append("activoCodigo", context.activoCodigo);
      }
      if (context.otNumero) {
        payload.append("otNumero", context.otNumero);
      }

      const stored = await apiFetch<DocumentStorageInfo>("/api/sharepoint/files/upload", {
        method: "POST",
        body: payload
      });

      setDocumentForm((current) => ({
        ...current,
        archivoKey: stored.fileKey,
        sharePointUrl: stored.url
      }));
      setMessage("Archivo cargado en almacenamiento documental.");
    } catch (uploadError) {
      setError(uploadError instanceof Error ? uploadError.message : "No fue posible cargar archivo.");
    } finally {
      setIsSaving(false);
    }
  }

  async function registerManualLink() {
    if (!documentForm.entidadCodigo.trim()) {
      setError("Debe indicar codigo de entidad antes de registrar enlace.");
      return;
    }

    if (!documentForm.sharePointUrl.trim()) {
      setError("Debe pegar un enlace SharePoint.");
      return;
    }

    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const context = buildStorageContext(documentForm);
      const stored = await apiFetch<DocumentStorageInfo>("/api/sharepoint/files/manual-link", {
        method: "POST",
        body: JSON.stringify({
          module: "Documents",
          entityType: documentForm.entidadTipo,
          entityId: documentForm.entidadCodigo,
          fileName: documentForm.archivoKey || `${documentForm.tipoDocumento || "documento"}.url`,
          url: documentForm.sharePointUrl,
          purpose: "Document",
          faenaCodigo: context.faenaCodigo || null,
          activoCodigo: context.activoCodigo || null,
          otNumero: context.otNumero || null,
          metadata: {
            tipoDocumento: documentForm.tipoDocumento
          }
        })
      });

      setDocumentForm((current) => ({
        ...current,
        archivoKey: stored.fileKey,
        sharePointUrl: stored.url
      }));
      setMessage("Enlace SharePoint registrado.");
    } catch (linkError) {
      setError(linkError instanceof Error ? linkError.message : "No fue posible registrar enlace.");
    } finally {
      setIsSaving(false);
    }
  }

  async function saveType(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setIsSaving(true);
    setError(null);
    setMessage(null);

    try {
      const exists = types.some((type) => type.codigo.localeCompare(typeForm.codigo, undefined, { sensitivity: "accent" }) === 0);
      const saved = exists
        ? await apiFetch<DocumentType>(`/api/documents/types/${encodeURIComponent(typeForm.codigo)}`, {
            method: "PUT",
            body: JSON.stringify(toTypePayload(typeForm, true))
          })
        : await apiFetch<DocumentType>("/api/documents/types", {
            method: "POST",
            body: JSON.stringify(toTypePayload(typeForm, false))
          });

      setMessage(exists ? "Tipo documental actualizado." : "Tipo documental creado.");
      setTypeForm(toTypeForm(saved));
      await loadAll(filters);
    } catch (typeError) {
      setError(typeError instanceof Error ? typeError.message : "No fue posible guardar tipo documental.");
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <section className="space-y-6">
      <div className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-slate-950 dark:text-white">Documentos</h1>
          <p className="mt-1 text-sm text-slate-500 dark:text-slate-400">Vencimientos, validacion y disponibilidad documental.</p>
        </div>
        <div className="flex flex-wrap gap-2">
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-4 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            onClick={() => void loadAll(filters)}
            type="button"
          >
            <RefreshCw className="h-4 w-4" aria-hidden="true" />
            Actualizar
          </button>
          <button
            className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
            disabled={!canManage}
            onClick={startNewDocument}
            type="button"
          >
            <UploadCloud className="h-4 w-4" aria-hidden="true" />
            Cargar
          </button>
        </div>
      </div>

      <SummaryCards summary={summary} />

      <section className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="grid gap-3 md:grid-cols-2 xl:grid-cols-6">
          <SelectFilter
            label="Entidad"
            value={filters.entidadTipo}
            options={["Activo", "OT", "Faena"]}
            onChange={(value) => applyFilters({ ...filters, entidadTipo: value, entidadCodigo: "" })}
          />
          {filters.entidadTipo === "Faena" ? (
            <FaenaSelect label="Codigo" value={filters.entidadCodigo} onChange={(value) => applyFilters({ ...filters, entidadCodigo: value })} />
          ) : (
            <TextField label="Codigo" value={filters.entidadCodigo} onChange={(value) => applyFilters({ ...filters, entidadCodigo: value })} />
          )}
          <FaenaSelect value={filters.faenaCodigo} onChange={(value) => applyFilters({ ...filters, faenaCodigo: value })} />
          <SelectFilter
            label="Tipo"
            value={filters.tipoDocumento}
            options={typeCodes}
            onChange={(value) => applyFilters({ ...filters, tipoDocumento: value })}
          />
          <SelectFilter label="Estado" value={filters.estado} options={statusOptions} onChange={(value) => applyFilters({ ...filters, estado: value })} />
          <label className="flex h-10 items-center gap-2 self-end rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
            <input
              checked={filters.includeHistorical}
              onChange={(event) => applyFilters({ ...filters, includeHistorical: event.target.checked })}
              type="checkbox"
            />
            Historicos
          </label>
        </div>
      </section>

      <div className="flex flex-wrap gap-2">
        {tabs.map(([value, label]) => (
          <button
            key={value}
            className={`inline-flex h-9 items-center rounded-md px-3 text-sm font-semibold transition ${
              activeTab === value
                ? "bg-slate-900 text-white dark:bg-white dark:text-slate-950"
                : "text-slate-600 hover:bg-slate-100 dark:text-slate-300 dark:hover:bg-slate-800"
            }`}
            onClick={() => setActiveTab(value)}
            type="button"
          >
            {label}
          </button>
        ))}
      </div>

      {activeTab === "documentos" ? (
        <section className="grid gap-4 xl:grid-cols-[1.15fr_0.85fr]">
          <DocumentsTable documents={documents} isLoading={isLoading} selectedId={selected?.documentoId} onSelect={selectDocument} />
          <DocumentEditor
            form={documentForm}
            types={types}
            selected={selected}
            versions={versions}
            storageInfo={storageInfo}
            replaceMode={replaceMode}
            canManage={canManage}
            canValidate={canValidate}
            isSaving={isSaving}
            actionReason={actionReason}
            onForm={setDocumentForm}
            onReplaceMode={setReplaceMode}
            onActionReason={setActionReason}
            onSave={saveDocument}
            onUploadFile={uploadDocumentFile}
            onRegisterManualLink={registerManualLink}
            onValidate={() => void validateSelected()}
            onReject={() => void rejectSelected()}
            onAnnul={() => void annulSelected()}
          />
        </section>
      ) : null}

      {activeTab === "matriz" ? <MatrixTable rows={matrix} versions={requirementMatrices} /> : null}
      {activeTab === "vencidos" ? <DocumentsTable documents={expired} isLoading={isLoading} selectedId={selected?.documentoId} onSelect={selectDocument} /> : null}
      {activeTab === "porVencer" ? <DocumentsTable documents={expiring} isLoading={isLoading} selectedId={selected?.documentoId} onSelect={selectDocument} /> : null}
      {activeTab === "configuracion" ? (
        <TypeConfiguration
          types={types}
          form={typeForm}
          canConfigure={canConfigure}
          isSaving={isSaving}
          onForm={setTypeForm}
          onSelect={selectType}
          onSave={saveType}
        />
      ) : null}

      {message ? <div className="rounded-md border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200">{message}</div> : null}
      {error ? <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-900 dark:bg-red-950 dark:text-red-200">{error}</div> : null}
    </section>
  );
}

function SummaryCards({ summary }: { summary: DocumentSummary | null }) {
  const items: Array<[string, number, LucideIcon]> = [
    ["Total", summary?.total ?? 0, FileText],
    ["Vigentes", summary?.vigentes ?? 0, FileCheck2],
    ["Por vencer", summary?.porVencer ?? 0, FileClock],
    ["Vencidos", summary?.vencidos ?? 0, ShieldAlert],
    ["Bloquean", summary?.bloqueanDisponibilidad ?? 0, Ban]
  ];

  return (
    <section className="grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
      {items.map(([label, value, Icon]) => (
        <div key={label} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div className="flex items-center justify-between gap-3">
            <p className="text-sm font-medium text-slate-500 dark:text-slate-400">{label}</p>
            <Icon className="h-5 w-5 text-slate-400" aria-hidden="true" />
          </div>
          <p className="mt-3 text-2xl font-semibold text-slate-950 dark:text-white">{value}</p>
        </div>
      ))}
    </section>
  );
}

function DocumentsTable({
  documents,
  isLoading,
  selectedId,
  onSelect
}: {
  documents: DocumentRecord[];
  isLoading: boolean;
  selectedId?: string;
  onSelect: (document: DocumentRecord) => void;
}) {
  return (
    <section className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Registros</h2>
        <span className="text-sm text-slate-500 dark:text-slate-400">{documents.length}</span>
      </div>
      {isLoading ? (
        <p className="p-4 text-sm text-slate-500 dark:text-slate-400">Cargando documentos...</p>
      ) : (
        <div className="max-h-[620px] overflow-auto">
          <table className="min-w-full text-left text-sm">
            <thead className="sticky top-0 bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
              <tr>
                <th className="px-4 py-3 font-medium">Entidad</th>
                <th className="px-4 py-3 font-medium">Tipo</th>
                <th className="px-4 py-3 font-medium">Estado</th>
                <th className="px-4 py-3 font-medium">Vence</th>
                <th className="px-4 py-3 font-medium">Archivo</th>
                <th className="px-4 py-3 font-medium">Bloqueo</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
              {documents.map((document) => (
                <tr key={document.documentoId} className={selectedId === document.documentoId ? "bg-teal-50/70 dark:bg-teal-950/30" : ""}>
                  <td className="px-4 py-3">
                    <button className="text-left font-semibold text-slate-900 dark:text-slate-100" onClick={() => onSelect(document)} type="button">
                      {document.entidadTipo}/{document.entidadCodigo}
                    </button>
                  </td>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{document.tipoDocumento}</td>
                  <td className="px-4 py-3"><StatusBadge status={document.estado} /></td>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{formatDate(document.fechaVencimiento)}</td>
                  <td className="max-w-64 truncate px-4 py-3 text-slate-600 dark:text-slate-300">
                    {document.sharePointUrl ? (
                      <a className="font-medium text-teal-700 hover:underline dark:text-teal-300" href={document.sharePointUrl} target="_blank" rel="noreferrer">
                        SharePoint
                      </a>
                    ) : document.archivoKey ?? "-"}
                  </td>
                  <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{document.bloqueaDisponibilidadActual ? "Si" : "No"}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}

function DocumentEditor({
  form,
  types,
  selected,
  versions,
  storageInfo,
  replaceMode,
  canManage,
  canValidate,
  isSaving,
  actionReason,
  onForm,
  onReplaceMode,
  onActionReason,
  onSave,
  onUploadFile,
  onRegisterManualLink,
  onValidate,
  onReject,
  onAnnul
}: {
  form: DocumentForm;
  types: DocumentType[];
  selected: DocumentRecord | null;
  versions: DocumentVersion[];
  storageInfo: StorageProviderInfo | null;
  replaceMode: boolean;
  canManage: boolean;
  canValidate: boolean;
  isSaving: boolean;
  actionReason: string;
  onForm: (form: DocumentForm) => void;
  onReplaceMode: (value: boolean) => void;
  onActionReason: (value: string) => void;
  onSave: (event: FormEvent<HTMLFormElement>) => void;
  onUploadFile: (file: File) => Promise<void>;
  onRegisterManualLink: () => Promise<void>;
  onValidate: () => void;
  onReject: () => void;
  onAnnul: () => void;
}) {
  const [fileToUpload, setFileToUpload] = useState<File | null>(null);

  return (
    <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div>
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">{selected ? "Ficha documental" : "Nuevo documento"}</h2>
        {selected ? <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">{selected.documentoId}</p> : null}
      </div>

      <form className="space-y-4" onSubmit={onSave}>
        <div className="grid gap-3 md:grid-cols-2">
          <SelectField
            label="Entidad"
            disabled={Boolean(selected)}
            value={form.entidadTipo}
            options={["Activo", "OT", "Faena"]}
            onChange={(value) => onForm({ ...form, entidadTipo: value as DocumentEntityType, entidadCodigo: "" })}
          />
          {form.entidadTipo === "Faena" ? (
            <FaenaSelect
              disabled={Boolean(selected)}
              emptyLabel="Selecciona faena"
              label="Codigo entidad"
              value={form.entidadCodigo}
              onChange={(value) => onForm({ ...form, entidadCodigo: value })}
            />
          ) : (
            <Field disabled={Boolean(selected)} label="Codigo entidad" value={form.entidadCodigo} onChange={(value) => onForm({ ...form, entidadCodigo: value })} />
          )}
          <SelectField label="Tipo" value={form.tipoDocumento} options={types.map((type) => type.codigo)} onChange={(value) => onForm({ ...form, tipoDocumento: value })} />
          <Field label="Fecha emision" type="date" value={form.fechaEmision} onChange={(value) => onForm({ ...form, fechaEmision: value })} />
          <Field label="Fecha vencimiento" type="date" value={form.fechaVencimiento} onChange={(value) => onForm({ ...form, fechaVencimiento: value })} />
          <Field label="Archivo key" value={form.archivoKey} onChange={(value) => onForm({ ...form, archivoKey: value })} />
          <div className="md:col-span-2">
            <Field label="SharePoint URL" value={form.sharePointUrl} onChange={(value) => onForm({ ...form, sharePointUrl: value })} />
          </div>
        </div>

        <div className="rounded-md border border-slate-200 p-3 dark:border-slate-800">
          <div className="flex flex-col gap-3 xl:flex-row xl:items-end xl:justify-between">
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <span className="rounded-full bg-slate-100 px-2 py-1 text-xs font-semibold text-slate-700 dark:bg-slate-800 dark:text-slate-200">
                  {storageInfo?.mode ?? "Sin estado"}
                </span>
                <span className="text-xs text-slate-500 dark:text-slate-400">{storageInfo?.provider ?? "SharePoint"}</span>
              </div>
              <p className="mt-2 break-words text-sm text-slate-700 dark:text-slate-200">{form.archivoKey || storageInfo?.rootPath || "-"}</p>
            </div>
            <div className="flex flex-wrap gap-2">
              {form.sharePointUrl ? (
                <a
                  className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                  href={form.sharePointUrl}
                  rel="noreferrer"
                  target="_blank"
                >
                  <ExternalLink className="h-4 w-4" aria-hidden="true" />
                  Abrir
                </a>
              ) : null}
              <button
                className="inline-flex h-10 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                disabled={!canManage || isSaving || !form.sharePointUrl.trim()}
                onClick={() => void onRegisterManualLink()}
                type="button"
              >
                <Link2 className="h-4 w-4" aria-hidden="true" />
                Registrar enlace
              </button>
            </div>
          </div>
          {storageInfo?.supportsUpload ? (
            <div className="mt-3 grid gap-2 md:grid-cols-[1fr_auto] md:items-end">
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">
                Archivo
                <input
                  className="mt-2 block w-full rounded-md border border-slate-300 bg-white px-3 py-2 text-sm outline-none ring-teal-500 transition file:mr-3 file:rounded-md file:border-0 file:bg-slate-100 file:px-3 file:py-1.5 file:text-sm file:font-semibold file:text-slate-700 focus:ring-2 dark:border-slate-700 dark:bg-slate-950 dark:file:bg-slate-800 dark:file:text-slate-100"
                  onChange={(event) => setFileToUpload(event.target.files?.[0] ?? null)}
                  type="file"
                />
              </label>
              <button
                className="inline-flex h-10 items-center justify-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
                disabled={!canManage || isSaving || !fileToUpload}
                onClick={() => {
                  if (fileToUpload) {
                    void onUploadFile(fileToUpload).then(() => setFileToUpload(null));
                  }
                }}
                type="button"
              >
                <UploadCloud className="h-4 w-4" aria-hidden="true" />
                Subir archivo
              </button>
            </div>
          ) : null}
        </div>

        <div className="grid gap-2 sm:grid-cols-3">
          <CheckField label="Critico" checked={form.critico} onChange={(value) => onForm({ ...form, critico: value })} />
          <CheckField label="Obligatorio" checked={form.obligatorio} onChange={(value) => onForm({ ...form, obligatorio: value })} />
          <CheckField label="Bloquea disponibilidad" checked={form.bloqueaDisponibilidad} onChange={(value) => onForm({ ...form, bloqueaDisponibilidad: value })} />
        </div>

        <div className="grid gap-2 md:grid-cols-[1fr_auto] md:items-end">
          <Field label="Motivo" value={form.reason} onChange={(value) => onForm({ ...form, reason: value })} />
          <label className="flex h-10 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
            <input checked={replaceMode} disabled={!selected} onChange={(event) => onReplaceMode(event.target.checked)} type="checkbox" />
            Reemplazar
          </label>
        </div>

        <button
          className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
          disabled={!canManage || isSaving}
          type="submit"
        >
          <Save className="h-4 w-4" aria-hidden="true" />
          Guardar
        </button>
      </form>

      {selected ? (
        <div className="rounded-md border border-slate-200 p-3 dark:border-slate-800">
          <h3 className="font-semibold">Historial inmutable de versiones</h3>
          <div className="mt-2 max-h-72 space-y-2 overflow-auto">
            {versions.map((version) => (
              <article className="rounded border border-slate-200 p-2 text-sm dark:border-slate-700" key={version.versionId}>
                <div className="flex items-center justify-between gap-2"><b>Version {version.numeroVersion} / {version.codigoVersion}</b><span className="status-pill">{version.estadoValidacion ?? (version.vigente ? "Vigente" : "Historica")}</span></div>
                <p>Carga: {new Date(version.fechaCargaUtc).toLocaleString()} por {version.cargadoPor}</p>
                <p>Emision: {formatDate(version.fechaEmision)} / vencimiento: {formatDate(version.fechaVencimiento)}</p>
                {version.validadoPor ? <p>Validada por {version.validadoPor} el {version.validadoEnUtc ? new Date(version.validadoEnUtc).toLocaleString() : "-"}</p> : null}
                {version.rechazadoPor ? <p className="text-red-700">Rechazada por {version.rechazadoPor}: {version.motivoRechazo ?? "sin motivo"}</p> : null}
                {version.responsableCorreccion ? <p>Correccion: {version.estadoCorreccion ?? "pendiente"} / responsable {version.responsableCorreccion}</p> : null}
                <p className="truncate text-slate-500">Archivo: {version.archivoKey}</p>
              </article>
            ))}
            {versions.length === 0 ? <p className="text-sm text-slate-500">Sin versiones registradas.</p> : null}
          </div>
        </div>
      ) : null}

      <div className="rounded-md border border-slate-200 p-3 dark:border-slate-800">
        <div className="grid gap-3 md:grid-cols-[1fr_auto_auto_auto] md:items-end">
          <Field label="Motivo accion" value={actionReason} onChange={onActionReason} />
          <button
            className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-emerald-200 px-3 text-sm font-semibold text-emerald-700 transition hover:bg-emerald-50 disabled:cursor-not-allowed disabled:opacity-60 dark:border-emerald-900 dark:text-emerald-200 dark:hover:bg-emerald-950"
            disabled={!selected || !canValidate || isSaving}
            onClick={onValidate}
            type="button"
          >
            <CheckCircle2 className="h-4 w-4" aria-hidden="true" />
            Validar
          </button>
          <button
            className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-red-200 px-3 text-sm font-semibold text-red-700 transition hover:bg-red-50 disabled:cursor-not-allowed disabled:opacity-60 dark:border-red-900 dark:text-red-200 dark:hover:bg-red-950"
            disabled={!selected || !canValidate || !actionReason.trim() || isSaving}
            onClick={onReject}
            type="button"
          >
            <XCircle className="h-4 w-4" aria-hidden="true" />
            Rechazar
          </button>
          <button
            className="inline-flex h-10 items-center justify-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            disabled={!selected || !canManage || !actionReason.trim() || isSaving}
            onClick={onAnnul}
            type="button"
          >
            <Ban className="h-4 w-4" aria-hidden="true" />
            Anular
          </button>
        </div>
      </div>
    </section>
  );
}

function MatrixTable({ rows, versions }: { rows: DocumentMatrixRow[]; versions: RequirementMatrixVersion[] }) {
  return (
    <section className="space-y-4">
      <div className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <h2 className="font-semibold">Versiones de matriz normativa</h2>
        <div className="mt-3 grid gap-2 md:grid-cols-2 xl:grid-cols-3">
          {versions.map((version) => (
            <article className="rounded border border-slate-200 p-3 text-sm dark:border-slate-700" key={version.id}>
              <div className="flex justify-between"><b>{version.codigo} v{version.numeroVersion}</b><span>{version.estado}</span></div>
              <p>{version.tipoActivoCodigo}{version.familiaEquipoCodigo ? ` / ${version.familiaEquipoCodigo}` : ""}</p>
              <p>{formatDate(version.vigenciaDesde)} a {formatDate(version.vigenciaHasta)}</p>
              <p>{version.requisitos.length} requisitos / {version.motivoCambio ?? "sin motivo informado"}</p>
            </article>
          ))}
        </div>
      </div>
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
      <div className="flex items-center justify-between border-b border-slate-200 px-4 py-3 dark:border-slate-800">
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Matriz documental</h2>
        <span className="text-sm text-slate-500 dark:text-slate-400">{rows.length}</span>
      </div>
      <div className="max-h-[660px] overflow-auto">
        <table className="min-w-full text-left text-sm">
          <thead className="sticky top-0 bg-slate-50 text-slate-500 dark:bg-slate-950 dark:text-slate-400">
            <tr>
              <th className="px-4 py-3 font-medium">Entidad</th>
              <th className="px-4 py-3 font-medium">Nombre</th>
              <th className="px-4 py-3 font-medium">Tipo</th>
              <th className="px-4 py-3 font-medium">Estado</th>
              <th className="px-4 py-3 font-medium">Vence</th>
              <th className="px-4 py-3 font-medium">Reglas</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
            {rows.map((row) => (
              <tr key={`${row.entidadTipo}-${row.entidadCodigo}-${row.tipoDocumento}`}>
                <td className="px-4 py-3 font-semibold text-slate-900 dark:text-slate-100">{row.entidadTipo}/{row.entidadCodigo}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{row.nombreEntidad}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{row.tipoDocumento}</td>
                <td className="px-4 py-3"><StatusBadge status={row.estado} /></td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{formatDate(row.fechaVencimiento)}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">
                  {row.obligatorio ? "Obligatorio" : "Opcional"} / {row.bloqueaDisponibilidad ? "Bloqueante" : "No bloqueante"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      </div>
    </section>
  );
}

function TypeConfiguration({
  types,
  form,
  canConfigure,
  isSaving,
  onForm,
  onSelect,
  onSave
}: {
  types: DocumentType[];
  form: TypeForm;
  canConfigure: boolean;
  isSaving: boolean;
  onForm: (form: TypeForm) => void;
  onSelect: (type: DocumentType) => void;
  onSave: (event: FormEvent<HTMLFormElement>) => void;
}) {
  return (
    <section className="grid gap-4 xl:grid-cols-[1fr_1fr]">
      <div className="overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="border-b border-slate-200 px-4 py-3 dark:border-slate-800">
          <h2 className="text-base font-semibold text-slate-950 dark:text-white">Tipos documentales</h2>
        </div>
        <div className="max-h-[620px] overflow-auto">
          {types.map((type) => (
            <button
              key={type.codigo}
              className="block w-full border-b border-slate-100 px-4 py-3 text-left text-sm transition hover:bg-slate-50 dark:border-slate-800 dark:hover:bg-slate-800"
              onClick={() => onSelect(type)}
              type="button"
            >
              <div className="flex items-center justify-between gap-3">
                <span className="font-semibold text-slate-900 dark:text-slate-100">{type.codigo}</span>
                <span className="text-xs text-slate-500 dark:text-slate-400">{type.activo ? "Activo" : "Inactivo"}</span>
              </div>
              <p className="mt-1 text-slate-600 dark:text-slate-300">{type.nombre}</p>
            </button>
          ))}
        </div>
      </div>

      <form className="space-y-4 rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900" onSubmit={onSave}>
        <h2 className="text-base font-semibold text-slate-950 dark:text-white">Configuracion</h2>
        <div className="grid gap-3 md:grid-cols-2">
          <Field label="Codigo" value={form.codigo} onChange={(value) => onForm({ ...form, codigo: value })} />
          <Field label="Nombre" value={form.nombre} onChange={(value) => onForm({ ...form, nombre: value })} />
          <SelectField label="Aplica a" value={form.aplicaA} options={["", "Activo", "OT", "Faena"]} onChange={(value) => onForm({ ...form, aplicaA: value })} />
          <Field label="Alerta dias" type="number" value={form.plazoAlertaDias} onChange={(value) => onForm({ ...form, plazoAlertaDias: value })} />
          <Field label="Roles responsables" value={form.rolesResponsables} onChange={(value) => onForm({ ...form, rolesResponsables: value })} />
          <Field label="Plantilla HTML" value={form.plantillaHtmlCodigo} onChange={(value) => onForm({ ...form, plantillaHtmlCodigo: value })} />
        </div>
        <div className="grid gap-2 sm:grid-cols-2 xl:grid-cols-5">
          <CheckField label="Obligatorio" checked={form.obligatorio} onChange={(value) => onForm({ ...form, obligatorio: value })} />
          <CheckField label="Critico" checked={form.critico} onChange={(value) => onForm({ ...form, critico: value })} />
          <CheckField label="Bloquea" checked={form.bloqueaDisponibilidad} onChange={(value) => onForm({ ...form, bloqueaDisponibilidad: value })} />
          <CheckField label="PDF alerta" checked={form.requierePdfAlerta} onChange={(value) => onForm({ ...form, requierePdfAlerta: value })} />
          <CheckField label="Activo" checked={form.activo} onChange={(value) => onForm({ ...form, activo: value })} />
        </div>
        <Field label="Motivo" value={form.reason} onChange={(value) => onForm({ ...form, reason: value })} />
        <button
          className="inline-flex h-10 items-center gap-2 rounded-md bg-teal-700 px-4 text-sm font-semibold text-white transition hover:bg-teal-800 disabled:cursor-not-allowed disabled:opacity-60 dark:bg-teal-500 dark:text-slate-950 dark:hover:bg-teal-400"
          disabled={!canConfigure || isSaving}
          type="submit"
        >
          <Save className="h-4 w-4" aria-hidden="true" />
          Guardar tipo
        </button>
      </form>
    </section>
  );
}

function StatusBadge({ status }: { status: DocumentStatus }) {
  const className =
    status === "Vencido"
      ? "bg-red-50 text-red-700 dark:bg-red-950 dark:text-red-200"
      : status === "PorVencer"
        ? "bg-amber-50 text-amber-700 dark:bg-amber-950 dark:text-amber-200"
        : status === "Vigente"
          ? "bg-emerald-50 text-emerald-700 dark:bg-emerald-950 dark:text-emerald-200"
          : status === "Rechazado" || status === "Anulado"
            ? "bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-200"
            : "bg-sky-50 text-sky-700 dark:bg-sky-950 dark:text-sky-200";

  return <span className={`rounded-full px-2 py-1 text-xs font-semibold ${className}`}>{status}</span>;
}

function Field({
  label,
  value,
  onChange,
  type = "text",
  disabled
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  disabled?: boolean;
}) {
  const id = `document-${label.toLowerCase().replace(/\s+/g, "-")}`;
  return (
    <label className="block text-sm font-medium text-slate-700 dark:text-slate-200" htmlFor={id}>
      {label}
      <input
        id={id}
        className="mt-2 h-10 w-full rounded-md border border-slate-300 bg-white px-3 text-sm outline-none ring-teal-500 transition focus:ring-2 disabled:bg-slate-100 disabled:text-slate-500 dark:border-slate-700 dark:bg-slate-950 dark:disabled:bg-slate-900"
        disabled={disabled}
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
    </label>
  );
}

function TextField({ label, value, onChange }: { label: string; value: string; onChange: (value: string) => void }) {
  return <Field label={label} value={value} onChange={onChange} />;
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
          <option key={option || "all"} value={option}>
            {option || "Todos"}
          </option>
        ))}
      </select>
    </label>
  );
}

function SelectFilter({
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

function CheckField({ label, checked, onChange }: { label: string; checked: boolean; onChange: (value: boolean) => void }) {
  return (
    <label className="flex min-h-10 items-center gap-2 rounded-md border border-slate-200 px-3 text-sm font-medium text-slate-700 dark:border-slate-700 dark:text-slate-200">
      <input checked={checked} onChange={(event) => onChange(event.target.checked)} type="checkbox" />
      {label}
    </label>
  );
}

function toDocumentForm(document: DocumentRecord): DocumentForm {
  return {
    entidadTipo: document.entidadTipo,
    entidadCodigo: document.entidadCodigo,
    tipoDocumento: document.tipoDocumento,
    fechaEmision: document.fechaEmision ?? "",
    fechaVencimiento: document.fechaVencimiento ?? "",
    archivoKey: document.archivoKey ?? "",
    sharePointUrl: document.sharePointUrl ?? "",
    critico: document.critico,
    obligatorio: document.obligatorio,
    bloqueaDisponibilidad: document.bloqueaDisponibilidad,
    reason: ""
  };
}

function toCreatePayload(form: DocumentForm) {
  return {
    entidadTipo: form.entidadTipo,
    entidadCodigo: form.entidadCodigo,
    tipoDocumento: form.tipoDocumento,
    fechaEmision: emptyToNull(form.fechaEmision),
    fechaVencimiento: emptyToNull(form.fechaVencimiento),
    archivoKey: emptyToNull(form.archivoKey),
    sharePointUrl: emptyToNull(form.sharePointUrl),
    critico: form.critico,
    obligatorio: form.obligatorio,
    bloqueaDisponibilidad: form.bloqueaDisponibilidad,
    reason: emptyToNull(form.reason)
  };
}

function toUpdatePayload(form: DocumentForm) {
  return {
    fechaEmision: emptyToNull(form.fechaEmision),
    fechaVencimiento: emptyToNull(form.fechaVencimiento),
    archivoKey: emptyToNull(form.archivoKey),
    sharePointUrl: emptyToNull(form.sharePointUrl),
    critico: form.critico,
    obligatorio: form.obligatorio,
    bloqueaDisponibilidad: form.bloqueaDisponibilidad,
    reason: emptyToNull(form.reason)
  };
}

function toReplacePayload(form: DocumentForm) {
  return {
    fechaEmision: emptyToNull(form.fechaEmision),
    fechaVencimiento: emptyToNull(form.fechaVencimiento),
    archivoKey: emptyToNull(form.archivoKey),
    sharePointUrl: emptyToNull(form.sharePointUrl),
    reason: form.reason
  };
}

function toTypePayload(form: TypeForm, includeReason: boolean) {
  return {
    codigo: form.codigo,
    nombre: form.nombre,
    aplicaA: emptyToNull(form.aplicaA),
    obligatorio: form.obligatorio,
    critico: form.critico,
    bloqueaDisponibilidad: form.bloqueaDisponibilidad,
    plazoAlertaDias: Number(form.plazoAlertaDias || 0),
    rolesResponsables: parseList(form.rolesResponsables),
    requierePdfAlerta: form.requierePdfAlerta,
    plantillaHtmlCodigo: emptyToNull(form.plantillaHtmlCodigo),
    activo: form.activo,
    reason: includeReason ? emptyToNull(form.reason) : undefined
  };
}

function toTypeForm(type: DocumentType): TypeForm {
  return {
    codigo: type.codigo,
    nombre: type.nombre,
    aplicaA: type.aplicaA ?? "",
    obligatorio: type.obligatorio,
    critico: type.critico,
    bloqueaDisponibilidad: type.bloqueaDisponibilidad,
    plazoAlertaDias: String(type.plazoAlertaDias),
    rolesResponsables: type.rolesResponsables.join("; "),
    requierePdfAlerta: type.requierePdfAlerta,
    plantillaHtmlCodigo: type.plantillaHtmlCodigo ?? "",
    activo: type.activo,
    reason: ""
  };
}

function toQuery(filters: Filters) {
  const query = new URLSearchParams();
  if (filters.entidadTipo) {
    query.set("entidadTipo", filters.entidadTipo);
  }
  if (filters.entidadCodigo) {
    query.set("entidadCodigo", filters.entidadCodigo);
  }
  if (filters.faenaCodigo) {
    query.set("faenaCodigo", filters.faenaCodigo);
  }
  if (filters.tipoDocumento) {
    query.set("tipoDocumento", filters.tipoDocumento);
  }
  if (filters.estado) {
    query.set("estado", filters.estado);
  }
  if (filters.includeHistorical) {
    query.set("includeHistorical", "true");
  }

  return query.toString();
}

function buildStorageContext(form: DocumentForm) {
  return {
    faenaCodigo: form.entidadTipo === "Faena" ? form.entidadCodigo : "",
    activoCodigo: form.entidadTipo === "Activo" ? form.entidadCodigo : "",
    otNumero: form.entidadTipo === "OT" ? form.entidadCodigo : ""
  };
}

function formatDate(value?: string | null) {
  return value ? value : "-";
}

function parseList(value: string) {
  return value
    .split(/[;,]/)
    .map((item) => item.trim())
    .filter(Boolean);
}

function emptyToNull(value: string) {
  return value.trim() ? value.trim() : null;
}

function hasPermission(userPermissions: string[] | undefined, permission: string) {
  return Boolean(userPermissions?.includes(permission) || userPermissions?.includes(AUTH_PERMISSIONS.administration));
}
