import { useState } from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { useAuthStore, type CurrentUser } from "../../auth/authStore";
import { DocumentsPage } from "../DocumentsPage";
import { DocumentActionsMenu } from "./DocumentActionsMenu";
import { DocumentEditorDialog } from "./DocumentEditorDialog";
import { DocumentForm, type DocumentFormValue } from "./DocumentForm";
import { DocumentReplacementDialog } from "./DocumentReplacementDialog";
import { DocumentReviewDialog } from "./DocumentReviewDialog";
import { DocumentAnnulDialog } from "./DocumentAnnulDialog";
import { DocumentVersionsDialog } from "./DocumentVersionsDialog";
import { Dialog } from "../../../shared/ui/Dialog";
import { documentApi } from "./documentApi";
import type { DocumentRecord } from "./documentFormTypes";

const document: DocumentRecord = { documentoId: "doc-1", entidadTipo: "Activo", entidadCodigo: "AC-1", tipoDocumento: "CERT", estado: "PendienteValidacion", fechaEmision: "2026-01-01", fechaVencimiento: "2026-12-31", sharePointUrl: "https://files.example/cert", critico: false, obligatorio: true, bloqueaDisponibilidad: false, esHistorico: false, fechaVencimientoValidada: false, fechaCargaUtc: "2026-01-01T00:00:00Z", cargadoPor: "tester", bloqueaDisponibilidadActual: false, versionVigente: 1 };
const user: CurrentUser = { id: "1", username: "planner", email: "planner@example.com", displayName: "Planner", isActive: true, isLocked: false, roles: ["Planificador"], permissions: ["documentos.gestionar", "documentos.validar"], faenas: [] };
const renderUi = (ui: React.ReactElement) => { const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } }); return render(<MemoryRouter><QueryClientProvider client={client}>{ui}</QueryClientProvider></MemoryRouter>); };
const file = () => new File(["documento"], "cert.pdf", { type: "application/pdf" });
const names = { assetName: "Camión fábrica 41", faenaName: "El Romeral", documentTypeName: "Mantenimiento chasis 450" };

beforeEach(() => { useAuthStore.setState({ user }); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

function FormHarness({ requiresExpirationDate }: { requiresExpirationDate: boolean }) {
  const [value, setValue] = useState<DocumentFormValue>({ archivo: null, fechaEmision: "2026-01-01", fechaVencimiento: "2026-12-31", observaciones: "" });
  return <DocumentForm value={value} onChange={setValue} mode="create" requiresExpirationDate={requiresExpirationDate} {...names} />;
}

function FocusHarness({ label }: { label: string }) {
  const [value, setValue] = useState("");
  return <Dialog open title="Prueba de foco" onClose={() => undefined}><textarea aria-label={label} value={value} onChange={event => setValue(event.target.value)} /></Dialog>;
}
describe("document dialogs", () => {
  it("shows descriptive names and requires expiration only when the matrix requires it", async () => {
    const upload = vi.spyOn(documentApi, "uploadAsset").mockResolvedValue(document);
    renderUi(<DocumentEditorDialog open mode="create" entityCode="AC-1" documentType="CERT" requiresExpirationDate {...names} onClose={vi.fn()} />);
    expect(screen.getByText("Camión fábrica 41")).toBeInTheDocument();
    expect(screen.getByText("El Romeral")).toBeInTheDocument();
    expect(screen.getByText("Mantenimiento chasis 450")).toBeInTheDocument();
    expect(screen.queryByText("AC-1")).not.toBeInTheDocument();
    expect(screen.queryByText("CERT")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Vencimiento")).toBeRequired();
    expect(screen.getByText("La fecha de vencimiento es obligatoria para este documento.")).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Archivo"), { target: { files: [file()] } });
    fireEvent.change(screen.getByLabelText("Vencimiento"), { target: { value: "2026-12-31" } });
    fireEvent.submit(globalThis.document.querySelector("#document-editor")!);
    await waitFor(() => expect(upload).toHaveBeenCalledWith("AC-1", expect.objectContaining({ tipoDocumento: "CERT", fechaVencimiento: "2026-12-31" })));
  });

  it("keeps focus in upload observations and validation comments while every character rerenders the dialog", () => {
    for (const label of ["Observaciones", "Comentarios"]) {
      const view = renderUi(<FocusHarness label={label} />);
      const textarea = screen.getByLabelText(label);
      textarea.focus();
      let value = "";
      for (const character of "texto continuo") { value += character; fireEvent.change(textarea, { target: { value } }); }
      expect(textarea).toHaveFocus();
      expect(textarea).toHaveValue("texto continuo");
      expect(screen.getByRole("button", { name: "Cerrar diálogo" })).not.toHaveFocus();
      view.unmount();
    }
  });
  it("hides and clears expiration for a requirement that does not expire", async () => {
    const view = renderUi(<FormHarness requiresExpirationDate />);
    expect(screen.getByLabelText("Vencimiento")).toHaveValue("2026-12-31");
    view.rerender(<MemoryRouter><QueryClientProvider client={new QueryClient()}><FormHarness requiresExpirationDate={false} /></QueryClientProvider></MemoryRouter>);
    await waitFor(() => expect(screen.queryByLabelText("Vencimiento")).not.toBeInTheDocument());
    expect(screen.getByLabelText("Emisión").parentElement?.parentElement).not.toHaveClass("sm:grid-cols-2");
  });

  it("uses the same expiration rule for replacement", async () => {
    const replace = vi.spyOn(documentApi, "replaceUpload").mockResolvedValue(document);
    renderUi(<DocumentReplacementDialog open document={document} requiresExpirationDate={false} {...names} onClose={vi.fn()} />);
    expect(screen.queryByLabelText("Vencimiento")).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Archivo"), { target: { files: [file()] } });
    fireEvent.change(screen.getByLabelText("Motivo de reemplazo"), { target: { value: "Actualización" } });
    fireEvent.submit(globalThis.document.querySelector("#document-replacement")!);
    await waitFor(() => expect(replace).toHaveBeenCalledWith("doc-1", expect.objectContaining({ fechaVencimiento: null, observaciones: "Actualización" })));
  });

  it("loads versions, validates, rejects and annuls", async () => {
    const versions = vi.spyOn(documentApi, "versions").mockResolvedValue([{ versionId: "v1", documentoId: "doc-1", numeroVersion: 1, codigoVersion: "v1", archivoId: "a1", archivoKey: "ignored", sharePointUrl: "https://files.example/v1", fechaCargaUtc: "2026-01-01T00:00:00Z", cargadoPor: "0b946e85-c5b8-42b9-aafa-d60344340211", cargadoPorNombre: "Valentina Rojas", vigente: true }, { versionId: "v2", documentoId: "doc-1", numeroVersion: 2, codigoVersion: "v2", archivoId: "a2", archivoKey: "ignored", fechaCargaUtc: "2026-01-02T00:00:00Z", cargadoPor: "8ea24e5d-3695-4438-aab9-9c561f28f521", vigente: false }]);
    const validate = vi.spyOn(documentApi, "validate").mockResolvedValue(document);
    const reject = vi.spyOn(documentApi, "reject").mockResolvedValue(document);
    const annul = vi.spyOn(documentApi, "annul").mockResolvedValue(document);
    const view = renderUi(<DocumentVersionsDialog open documentId="doc-1" onClose={vi.fn()} />);
    expect(await screen.findByText("v1")).toBeInTheDocument(); expect(screen.getByText("Valentina Rojas")).toBeInTheDocument(); expect(screen.getByText("Usuario no disponible")).toBeInTheDocument(); expect(screen.queryByText("0b946e85-c5b8-42b9-aafa-d60344340211")).not.toBeInTheDocument(); expect(versions).toHaveBeenCalledWith("doc-1"); view.unmount();
    renderUi(<DocumentReviewDialog open action="validate" document={document} onClose={vi.fn()} />); fireEvent.click(screen.getByRole("button", { name: "Confirmar" })); await waitFor(() => expect(validate).toHaveBeenCalledWith("doc-1", null));
    cleanup(); renderUi(<DocumentReviewDialog open action="reject" document={document} onClose={vi.fn()} />); fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Falta antecedente" } }); fireEvent.click(screen.getByRole("button", { name: "Confirmar" })); await waitFor(() => expect(reject).toHaveBeenCalledWith("doc-1", "Falta antecedente"));
    cleanup(); renderUi(<DocumentAnnulDialog open document={document} onClose={vi.fn()} />); fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Duplicado" } }); fireEvent.click(screen.getByRole("button", { name: "Confirmar" })); await waitFor(() => expect(annul).toHaveBeenCalledWith("doc-1", "Duplicado"));
  });
});

describe("document consumers", () => {
  it("keeps global page as search and navigation only", async () => {
    renderUi(<DocumentActionsMenu document={{ ...document, estado: "Anulado" }} capabilities={{ canManage: true, canValidate: true }} onEdit={vi.fn()} onVersions={vi.fn()} onReplace={vi.fn()} onValidate={vi.fn()} onReject={vi.fn()} onAnnul={vi.fn()} />);
    expect(screen.queryByRole("button", { name: "Editar" })).not.toBeInTheDocument(); expect(screen.getByRole("button", { name: "Versiones" })).toBeInTheDocument(); cleanup();
    vi.spyOn(documentApi, "list").mockResolvedValue([document]); renderUi(<DocumentsPage />); expect(await screen.findByText("CERT")).toBeInTheDocument(); expect(screen.getByRole("button", { name: "Gestionar" })).toBeInTheDocument(); expect(screen.queryByText("Cargar documento")).not.toBeInTheDocument();
  });
});