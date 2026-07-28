import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
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
const user: CurrentUser = { id: "1", username: "planner", email: "planner@example.com", displayName: "Planner", isActive: true, isLocked: false, roles: ["planificador"], permissions: ["documentos.gestionar", "documentos.validar"], faenas: [] };
const renderUi = (ui: React.ReactElement) => { const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } }); return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>); };

beforeEach(() => { useAuthStore.setState({ user }); vi.spyOn(documentApi, "types").mockResolvedValue([{ codigo: "CERT", nombre: "Certificado", obligatorio: true, critico: false, bloqueaDisponibilidad: false, activo: true }]); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("document dialogs", () => {
  it("creates, edits, preserves errors, and applies locked or global entity context", async () => {
    const create = vi.spyOn(documentApi, "create").mockResolvedValue(document);
    const update = vi.spyOn(documentApi, "update").mockResolvedValue(document);
    const view = renderUi(<DocumentEditorDialog open mode="create" entityType="Activo" entityCode="AC-1" lockEntity onClose={vi.fn()} />);
    expect(screen.queryByLabelText("Codigo de entidad")).not.toBeInTheDocument();
    fireEvent.change(await screen.findByLabelText("Tipo documental"), { target: { value: "CERT" } });
    fireEvent.submit(globalThis.document.querySelector("#document-editor")!);
    await waitFor(() => expect(create).toHaveBeenCalledOnce());
    view.unmount();
    renderUi(<DocumentEditorDialog open mode="edit" document={document} entityCode="AC-1" lockEntity onClose={vi.fn()} />);
    fireEvent.submit(globalThis.document.querySelector("#document-editor")!);
    await waitFor(() => expect(update).toHaveBeenCalledWith("doc-1", expect.any(Object)));
    cleanup();
    vi.spyOn(documentApi, "create").mockRejectedValue(new Error("Conflicto documental"));
    renderUi(<DocumentEditorDialog open mode="create" onClose={vi.fn()} />);
    expect(screen.getByLabelText("Codigo de entidad")).toBeInTheDocument();
    fireEvent.change(await screen.findByLabelText("Tipo documental"), { target: { value: "CERT" } });
    fireEvent.submit(globalThis.document.querySelector("#document-editor")!);
    expect(await screen.findByText("Conflicto documental")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("loads versions, replaces, validates, rejects and annuls without DELETE", async () => {
    const versions = vi.spyOn(documentApi, "versions").mockResolvedValue([{ versionId: "v1", documentoId: "doc-1", numeroVersion: 1, codigoVersion: "v1", archivoId: "a1", archivoKey: "ignored", sharePointUrl: "https://files.example/v1", fechaCargaUtc: "2026-01-01T00:00:00Z", cargadoPor: "tester", vigente: true }]);
    const replace = vi.spyOn(documentApi, "replace").mockResolvedValue(document);
    const validate = vi.spyOn(documentApi, "validate").mockResolvedValue(document);
    const reject = vi.spyOn(documentApi, "reject").mockResolvedValue(document);
    const annul = vi.spyOn(documentApi, "annul").mockResolvedValue(document);
    const view = renderUi(<DocumentVersionsDialog open documentId="doc-1" onClose={vi.fn()} />);
    expect(await screen.findByText("v1")).toBeInTheDocument();
    expect(versions).toHaveBeenCalledWith("doc-1");
    view.unmount();
    renderUi(<DocumentReplacementDialog open document={document} onClose={vi.fn()} />);
    fireEvent.change(screen.getByLabelText("Motivo de reemplazo"), { target: { value: "Actualizacion" } });
    fireEvent.submit(globalThis.document.querySelector("#document-replacement")!);
    await waitFor(() => expect(replace).toHaveBeenCalledWith("doc-1", expect.objectContaining({ reason: "Actualizacion" })));
    cleanup();
    renderUi(<DocumentReviewDialog open action="validate" document={document} onClose={vi.fn()} />);
    fireEvent.click(screen.getByRole("button", { name: "Confirmar" }));
    await waitFor(() => expect(validate).toHaveBeenCalledWith("doc-1", null));
    cleanup();
    renderUi(<DocumentReviewDialog open action="reject" document={document} onClose={vi.fn()} />);
    expect(screen.getByRole("button", { name: "Confirmar" })).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Falta antecedente" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar" }));
    await waitFor(() => expect(reject).toHaveBeenCalledWith("doc-1", "Falta antecedente"));
    cleanup();
    renderUi(<DocumentAnnulDialog open document={document} onClose={vi.fn()} />);
    expect(screen.getByRole("button", { name: "Confirmar" })).toBeDisabled();
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Duplicado" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar" }));
    await waitFor(() => expect(annul).toHaveBeenCalledWith("doc-1", "Duplicado"));
    expect("delete" in documentApi).toBe(false);
  });
});

describe("document consumers", () => {
  it("filters actions and DocumentsPage delegates to AssetDocumentManager", async () => {
    renderUi(<DocumentActionsMenu document={{ ...document, estado: "Anulado" }} capabilities={{ canManage: true, canValidate: true }} onEdit={vi.fn()} onVersions={vi.fn()} onReplace={vi.fn()} onValidate={vi.fn()} onReject={vi.fn()} onAnnul={vi.fn()} />);
    expect(screen.queryByRole("button", { name: "Editar" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Versiones" })).toBeInTheDocument();
    cleanup();
    vi.spyOn(documentApi, "list").mockResolvedValue([document]);
    vi.spyOn(documentApi, "listForAsset").mockResolvedValue([document]);
    renderUi(<DocumentsPage />);
    expect(await screen.findByText("CERT")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Gestionar" }));
    expect(await screen.findByText("Activo: AC-1")).toBeInTheDocument();
  });
});
