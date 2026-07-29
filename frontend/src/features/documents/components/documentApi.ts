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
};
