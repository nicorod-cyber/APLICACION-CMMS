import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { OperationalUnitDocumentManager } from "./OperationalUnitDocumentManager";
import { documentApi, type OperationalUnitDocumentView } from "./documentApi";

const view: OperationalUnitDocumentView = {
  unitCode: "QUADRA-1029", unitName: "QUADRA-1029", faenaCode: "FAE-1", faenaName: "Faena Uno", compositionComplete: true, matrixConfigurationComplete: true,
  summary: { pendingUpload: 1, pendingValidation: 1, expiring: 0, expired: 0, valid: 0, compliant: false, blocksAvailability: true }, configurationWarnings: ["La configuración documental está completa."],
  rows: [
    { requirementKey: "chasis-key", documentTypeCode: "REVTEC", documentTypeName: "Revisión técnica vigente", mandatory: true, critical: true, blocksAvailability: true, requiresExpirationDate: true, alertDays: 30, status: "PendienteCarga", canUpload: true, canReplace: false, canValidate: false, canReject: false, canAnnul: false, technicalOwnerRole: "CHASIS", technicalOwnerAssetCode: "CH-1", technicalOwnerAssetName: "Chasis Quadra 1029", matrixId: "m1", matrixItemId: "i1" },
    { requirementKey: "fabrica-key", documentTypeCode: "SNGM", documentTypeName: "SNGM", mandatory: true, critical: false, blocksAvailability: false, requiresExpirationDate: false, alertDays: 30, status: "PendienteValidacion", versionNumber: 1, documentId: "doc-fab", canUpload: false, canReplace: true, canValidate: true, canReject: true, canAnnul: true, technicalOwnerRole: "FABRICA", technicalOwnerAssetCode: "FAB-1", technicalOwnerAssetName: "Fábrica", matrixId: "m2", matrixItemId: "i2" }
  ]
};
const renderUi = () => { const client = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } }); return render(<QueryClientProvider client={client}><OperationalUnitDocumentManager unitCode="QUADRA-1029" /></QueryClientProvider>); };
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("OperationalUnitDocumentManager", () => {
  it("shows one consolidated table without component selectors and uploads from the selected requirement", async () => {
    vi.spyOn(documentApi, "unitDocuments").mockResolvedValue(view);
    const upload = vi.spyOn(documentApi, "uploadUnitRequirement").mockResolvedValue(view);
    renderUi();
    expect(await screen.findByText("Revisión técnica vigente")).toBeInTheDocument();
    expect(screen.getAllByRole("table")).toHaveLength(1);
    expect(screen.queryByRole("button", { name: "Todos" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Chasis" })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Cargar" }));
    expect(screen.getByText("Unidad:")).toBeInTheDocument();
    expect(screen.getByText("Chasis Quadra 1029")).toBeInTheDocument();
    expect(screen.getByText("Faena Uno")).toBeInTheDocument();
    expect(screen.getAllByText("Revisión técnica vigente")).toHaveLength(2);
    expect(screen.getByLabelText("Vencimiento")).toBeRequired();
    fireEvent.change(screen.getByLabelText("Archivo"), { target: { files: [new File(["pdf"], "revtec.pdf", { type: "application/pdf" })] } });
    fireEvent.change(screen.getByLabelText("Vencimiento"), { target: { value: "2026-12-31" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar" }));
    await waitFor(() => expect(upload).toHaveBeenCalledWith("QUADRA-1029", "chasis-key", expect.objectContaining({ tipoDocumento: "REVTEC" })));
  });
});