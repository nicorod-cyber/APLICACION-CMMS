import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useAuthStore, type CurrentUser } from "../../auth/authStore";
import { DocumentsPage } from "../DocumentsPage";
import { DocumentActionsMenu } from "./DocumentActionsMenu";
import { DocumentAnnulDialog } from "./DocumentAnnulDialog";
import { DocumentEditorDialog } from "./DocumentEditorDialog";
import { DocumentReplacementDialog } from "./DocumentReplacementDialog";
import { DocumentReviewDialog } from "./DocumentReviewDialog";
import { DocumentVersionsDialog } from "./DocumentVersionsDialog";
import { documentApi } from "./documentApi";
import type { DocumentRecord } from "./documentFormTypes";

const document: DocumentRecord = { documentoId: "doc-1", entidadTipo: "Activo", entidadCodigo: "AC-1", tipoDocumento: "CERT", estado: "PendienteValidacion", fechaEmision: "2026-01-01", fechaVencimiento: "2026-12-31", sharePointUrl: "https://files.example/cert", critico: false, obligatorio: true, bloqueaDisponibilidad: false, esHistorico: false, fechaVencimientoValidada: false, fechaCargaUtc: "2026-01-01T00:00:00Z", cargadoPor: "tester", bloqueaDisponibilidadActual: false, versionVigente: 1 };
const user: CurrentUser = { id: "1", username: "planner", email: "planner@example.com", displayName: "Planner", isActive: true, isLocked: false, roles: ["Planificador"], permissions: ["documentos.gestionar", "documentos.validar"], faenas: [] };
const renderUi = (ui: React.ReactElement) => { const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } }); return render(<MemoryRouter><QueryClientProvider client={client}>{ui}</QueryClientProvider></MemoryRouter>); };
const file = () => new File(["documento"], "cert.pdf", { type: "application/pdf" });

beforeEach(() => { useAuthStore.setState({ user }); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("document dialogs", () => {
  it("uploads only the file associated with the matrix requirement and updates metadata", async () => {
    const upload = vi.spyOn(documentApi, "uploadAsset").mockResolvedValue(document);
    const update = vi.spyOn(documentApi, "update").mockResolvedValue(document);
    const view = renderUi(<DocumentEditorDialog open mode="create" entityCode="AC-1" documentType="CERT" faenaCode="FAE-1" requiresExpirationDate onClose={vi.fn()} />);
    fireEvent.change(screen.getByLabelText("Archivo"), { target: { files: [file()] } });
    fireEvent.submit(globalThis.document.querySelector("#document-editor")!);
    await waitFor(() => expect(upload).toHaveBeenCalledWith("AC-1", expect.objectContaining({ tipoDocumento: "CERT" })));
    view.unmount();
    renderUi(<DocumentEditorDialog open mode="edit" document={document} entityCode="AC-1" documentType="CERT" onClose={vi.fn()} />);
    fireEvent.change(screen.getByLabelText("Observaciones"), { target: { value: "Corrección de fecha" } });
    fireEvent.submit(globalThis.document.querySelector("#document-editor")!);
    await waitFor(() => expect(update).toHaveBeenCalledWith("doc-1", expect.any(Object)));
  });

  it("loads versions, replaces with a file, validates, rejects and annuls", async () => {
    const versions = vi.spyOn(documentApi, "versions").mockResolvedValue([{ versionId: "v1", documentoId: "doc-1", numeroVersion: 1, codigoVersion: "v1", archivoId: "a1", archivoKey: "ignored", sharePointUrl: "https://files.example/v1", fechaCargaUtc: "2026-01-01T00:00:00Z", cargadoPor: "tester", vigente: true }]);
    const replace = vi.spyOn(documentApi, "replaceUpload").mockResolvedValue(document);
    const validate = vi.spyOn(documentApi, "validate").mockResolvedValue(document);
    const reject = vi.spyOn(documentApi, "reject").mockResolvedValue(document);
    const annul = vi.spyOn(documentApi, "annul").mockResolvedValue(document);
    const view = renderUi(<DocumentVersionsDialog open documentId="doc-1" onClose={vi.fn()} />);
    expect(await screen.findByText("v1")).toBeInTheDocument(); expect(versions).toHaveBeenCalledWith("doc-1"); view.unmount();
    renderUi(<DocumentReplacementDialog open document={document} onClose={vi.fn()} />);
    fireEvent.change(screen.getByLabelText("Archivo"), { target: { files: [file()] } }); fireEvent.change(screen.getByLabelText("Motivo de reemplazo"), { target: { value: "Actualización" } }); fireEvent.submit(globalThis.document.querySelector("#document-replacement")!);
    await waitFor(() => expect(replace).toHaveBeenCalledWith("doc-1", expect.objectContaining({ observaciones: "Actualización" })));
    cleanup(); renderUi(<DocumentReviewDialog open action="validate" document={document} onClose={vi.fn()} />); fireEvent.click(screen.getByRole("button", { name: "Confirmar" })); await waitFor(() => expect(validate).toHaveBeenCalledWith("doc-1", null));
    cleanup(); renderUi(<DocumentReviewDialog open action="reject" document={document} onClose={vi.fn()} />); fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Falta antecedente" } }); fireEvent.click(screen.getByRole("button", { name: "Confirmar" })); await waitFor(() => expect(reject).toHaveBeenCalledWith("doc-1", "Falta antecedente"));
    cleanup(); renderUi(<DocumentAnnulDialog open document={document} onClose={vi.fn()} />); fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Duplicado" } }); fireEvent.click(screen.getByRole("button", { name: "Confirmar" })); await waitFor(() => expect(annul).toHaveBeenCalledWith("doc-1", "Duplicado")); expect("delete" in documentApi).toBe(false);
  });
});

describe("document consumers", () => {
  it("keeps global page as search and navigation only", async () => {
    renderUi(<DocumentActionsMenu document={{ ...document, estado: "Anulado" }} capabilities={{ canManage: true, canValidate: true }} onEdit={vi.fn()} onVersions={vi.fn()} onReplace={vi.fn()} onValidate={vi.fn()} onReject={vi.fn()} onAnnul={vi.fn()} />);
    expect(screen.queryByRole("button", { name: "Editar" })).not.toBeInTheDocument(); expect(screen.getByRole("button", { name: "Versiones" })).toBeInTheDocument(); cleanup();
    vi.spyOn(documentApi, "list").mockResolvedValue([document]); renderUi(<DocumentsPage />); expect(await screen.findByText("CERT")).toBeInTheDocument(); expect(screen.getByRole("button", { name: "Gestionar" })).toBeInTheDocument(); expect(screen.queryByText("Cargar documento")).not.toBeInTheDocument();
  });
});
