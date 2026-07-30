import { apiFetch } from "../../auth/authStore";
import type { DocumentMatrixRow, DocumentRecord, DocumentType, DocumentVersion } from "./documentFormTypes";

export type AssetDocumentUpload = { tipoDocumento: string; archivo: File; fechaEmision?: string | null; fechaVencimiento?: string | null; observaciones?: string | null };
export type DocumentPayload = { fechaEmision?: string | null; fechaVencimiento?: string | null; reason?: string | null };

function uploadForm(payload: AssetDocumentUpload) {
  const form = new FormData();
  form.append("file", payload.archivo);
  form.append("tipoDocumento", payload.tipoDocumento);
  if (payload.fechaEmision) form.append("fechaEmision", payload.fechaEmision);
  if (payload.fechaVencimiento) form.append("fechaVencimiento", payload.fechaVencimiento);
  if (payload.observaciones?.trim()) form.append("observaciones", payload.observaciones.trim());
  return form;
}

export type OperationalUnitDocumentRow = {
  requirementKey: string; documentTypeCode: string; documentTypeName: string; mandatory: boolean; critical: boolean;
  blocksAvailability: boolean; requiresExpirationDate: boolean; alertDays: number; status: string; versionNumber?: number | null;
  issueDate?: string | null; expirationDate?: string | null; validationStatus?: string | null; rejectionReason?: string | null;
  documentId?: string | null; currentVersionId?: string | null; canUpload: boolean; canReplace: boolean; canValidate: boolean;
  canReject: boolean; canAnnul: boolean; technicalOwnerRole: string; technicalOwnerAssetCode: string; technicalOwnerAssetName: string;
  matrixId: string; matrixItemId: string; validatedBy?: string | null; validatedAtUtc?: string | null; sharePointUrl?: string | null;
  daysToExpire?: number | null; pendingReason?: string | null;
};
export type OperationalUnitDocumentView = {
  unitCode: string; unitName: string; faenaCode?: string | null; faenaName?: string | null; compositionComplete: boolean;
  matrixConfigurationComplete: boolean; summary: { pendingUpload: number; pendingValidation: number; expiring: number; expired: number; valid: number; compliant: boolean; blocksAvailability: boolean };
  configurationWarnings: string[]; rows: OperationalUnitDocumentRow[];
};
export type OperationalUnitDocumentContext = { unitCode: string; unitName: string; technicalOwnerRole: string; technicalOwnerAssetCode: string };
export const documentApi = {
  listForAsset: (assetCode: string, includeHistorical = true) => apiFetch<DocumentRecord[]>("/api/documents?entidadTipo=Activo&entidadCodigo=" + encodeURIComponent(assetCode) + "&includeHistorical=" + includeHistorical),
  matrixForAsset: (assetCode: string) => apiFetch<DocumentMatrixRow[]>("/api/assets/" + encodeURIComponent(assetCode) + "/document-matrix"),
  list: (parameters: URLSearchParams) => apiFetch<DocumentRecord[]>("/api/documents?" + parameters),
  types: () => apiFetch<DocumentType[]>("/api/documents/types"),
  uploadAsset: (assetCode: string, payload: AssetDocumentUpload) => apiFetch<DocumentRecord>("/api/assets/" + encodeURIComponent(assetCode) + "/documents", { method: "POST", body: uploadForm(payload) }),
  update: (id: string, payload: DocumentPayload) => apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id), { method: "PUT", body: JSON.stringify(payload) }),
  versions: (id: string) => apiFetch<DocumentVersion[]>("/api/documents/" + encodeURIComponent(id) + "/versions"),
  replaceUpload: (id: string, payload: AssetDocumentUpload & { observaciones: string }) => {
    const form = new FormData();
    form.append("file", payload.archivo);
    form.append("tipoDocumento", payload.tipoDocumento);
    if (payload.fechaEmision) form.append("fechaEmision", payload.fechaEmision);
    if (payload.fechaVencimiento) form.append("fechaVencimiento", payload.fechaVencimiento);
    form.append("observaciones", payload.observaciones.trim());
    return apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id) + "/versions", { method: "POST", body: form });
  },
  validate: (id: string, comments?: string | null) => apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id) + "/validate", { method: "POST", body: JSON.stringify({ comments: comments || null }) }),
  reject: (id: string, reason: string) => apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id) + "/reject", { method: "POST", body: JSON.stringify({ reason }) }),
  annul: (id: string, reason: string) => apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id) + "/annul", { method: "POST", body: JSON.stringify({ reason }) }),
  unitDocuments: (unitCode: string) => apiFetch<OperationalUnitDocumentView>("/api/operational-units/" + encodeURIComponent(unitCode) + "/documents"),
  uploadUnitRequirement: (unitCode: string, requirementKey: string, payload: AssetDocumentUpload) => apiFetch<OperationalUnitDocumentView>("/api/operational-units/" + encodeURIComponent(unitCode) + "/document-requirements/" + encodeURIComponent(requirementKey) + "/upload", { method: "POST", body: uploadForm(payload) }),
  replaceUnitDocument: (unitCode: string, documentId: string, payload: AssetDocumentUpload & { observaciones: string }) => apiFetch<OperationalUnitDocumentView>("/api/operational-units/" + encodeURIComponent(unitCode) + "/documents/" + encodeURIComponent(documentId) + "/versions", { method: "POST", body: uploadForm(payload) }),
  updateUnitDocument: (unitCode: string, documentId: string, payload: DocumentPayload) => apiFetch<OperationalUnitDocumentView>("/api/operational-units/" + encodeURIComponent(unitCode) + "/documents/" + encodeURIComponent(documentId), { method: "PUT", body: JSON.stringify(payload) }),
  validateUnitDocument: (unitCode: string, documentId: string, comments?: string | null) => apiFetch<OperationalUnitDocumentView>("/api/operational-units/" + encodeURIComponent(unitCode) + "/documents/" + encodeURIComponent(documentId) + "/validate", { method: "POST", body: JSON.stringify({ comments: comments || null }) }),
  rejectUnitDocument: (unitCode: string, documentId: string, reason: string) => apiFetch<OperationalUnitDocumentView>("/api/operational-units/" + encodeURIComponent(unitCode) + "/documents/" + encodeURIComponent(documentId) + "/reject", { method: "POST", body: JSON.stringify({ reason }) }),
  annulUnitDocument: (unitCode: string, documentId: string, reason: string) => apiFetch<OperationalUnitDocumentView>("/api/operational-units/" + encodeURIComponent(unitCode) + "/documents/" + encodeURIComponent(documentId) + "/annul", { method: "POST", body: JSON.stringify({ reason }) }),
  unitContextForAsset: (assetCode: string) => apiFetch<OperationalUnitDocumentContext>("/api/operational-units/component-context/" + encodeURIComponent(assetCode)),
};
