import { apiFetch } from "../../auth/authStore";
import type { DocumentRecord, DocumentType, DocumentVersion } from "./documentFormTypes";
export type DocumentPayload = { entidadTipo?: "Activo" | "OT" | "Faena"; entidadCodigo?: string; tipoDocumento?: string; fechaEmision?: string | null; fechaVencimiento?: string | null; sharePointUrl?: string | null; critico?: boolean; obligatorio?: boolean; bloqueaDisponibilidad?: boolean; reason?: string | null };
export const documentApi = {
  listForAsset: (assetCode: string, includeHistorical = true) => apiFetch<DocumentRecord[]>("/api/documents?entidadTipo=Activo&entidadCodigo=" + encodeURIComponent(assetCode) + "&includeHistorical=" + includeHistorical),
  list: (parameters: URLSearchParams) => apiFetch<DocumentRecord[]>("/api/documents?" + parameters),
  types: () => apiFetch<DocumentType[]>("/api/documents/types"),
  create: (payload: Required<Pick<DocumentPayload, "entidadTipo" | "entidadCodigo" | "tipoDocumento">> & DocumentPayload) => apiFetch<DocumentRecord>("/api/documents", { method: "POST", body: JSON.stringify(payload) }),
  update: (id: string, payload: DocumentPayload) => apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id), { method: "PUT", body: JSON.stringify(payload) }),
  versions: (id: string) => apiFetch<DocumentVersion[]>("/api/documents/" + encodeURIComponent(id) + "/versions"),
  replace: (id: string, payload: Pick<DocumentPayload, "fechaEmision" | "fechaVencimiento" | "sharePointUrl"> & { reason: string }) => apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id) + "/replace", { method: "POST", body: JSON.stringify(payload) }),
  validate: (id: string, comments?: string | null) => apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id) + "/validate", { method: "POST", body: JSON.stringify({ comments: comments || null }) }),
  reject: (id: string, reason: string) => apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id) + "/reject", { method: "POST", body: JSON.stringify({ reason }) }),
  annul: (id: string, reason: string) => apiFetch<DocumentRecord>("/api/documents/" + encodeURIComponent(id) + "/annul", { method: "POST", body: JSON.stringify({ reason }) }),
};