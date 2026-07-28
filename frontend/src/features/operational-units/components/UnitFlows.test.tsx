import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, describe, expect, it, vi } from "vitest";
import * as auth from "../../auth/authStore";
import { UnitCompositionDialogs } from "../../equipment-overview/UnitCompositionDialogs";
import { UnitMaintenanceActions } from "./UnitMaintenanceActions";
import { UnitTransferDialog } from "./UnitTransferDialog";

const components = [{ activoCodigo: "CH-1", activoNombre: "Chasis 1", rolComponenteCodigo: "CHASIS", fechaMontajeUtc: "2026-01-01", vigente: true }, { activoCodigo: "FB-1", activoNombre: "Fabrica 1", rolComponenteCodigo: "FABRICA", fechaMontajeUtc: "2026-01-01", vigente: true }];
const unit = { codigo: "CFA-1", faenaCodigo: "F001", ubicacionTecnicaCodigo: "FAENA-F001", composicion: { completa: true, vigentes: components } };
const renderQuery = (node: React.ReactElement) => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}>{node}</QueryClientProvider>);
function Location() { const location = useLocation(); return <output data-testid="location">{location.pathname + location.search}</output>; }
async function chooseCandidate() { fireEvent.change(screen.getByRole("textbox", { name: /Buscar componente elegible/ }), { target: { value: "CH" } }); await screen.findByRole("option", { name: /CH-2/ }); fireEvent.change(screen.getByRole("combobox", { name: "Seleccionar componente elegible" }), { target: { value: "CH-2" } }); }
function compositionApi() { return vi.spyOn(auth, "apiFetch").mockImplementation((path: string) => Promise.resolve(path.startsWith("/api/assets?") ? { items: [{ codigo: "CH-2", nombre: "Chasis 2", faenaCodigo: "F001" }] } : {}) as never); }
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("UnitTransferDialog", () => {
  it("shows the complete unit and sends one transactional transfer", async () => {
    const request = vi.spyOn(auth, "apiFetch").mockImplementation((path: string) => Promise.resolve(path.startsWith("/api/faenas") ? [{ id: "1", codigo: "F002", nombre: "Faena destino", activo: true, ubicacionTecnica: null }] : {} as never));
    renderQuery(<UnitTransferDialog open unit={unit} onClose={vi.fn()} />);
    expect(screen.getByText("Chasis 1 (CH-1)")).toBeInTheDocument();
    expect(screen.getByText("Fabrica 1 (FB-1)")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("combobox")).not.toBeDisabled());
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "F002" } });
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Cambio de contrato" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar traslado" }));
    await waitFor(() => expect(request).toHaveBeenCalledWith("/api/assets/CH-1/transfers", expect.objectContaining({ method: "POST" })));
    expect(request.mock.calls.filter(call => String(call[0]).includes("/transfers")).length).toBe(1);
  });

  it("keeps values and dialog open when the transfer fails, including a 403 response message", async () => {
    vi.spyOn(auth, "apiFetch").mockImplementation((path: string) => path.startsWith("/api/faenas") ? Promise.resolve([{ id: "1", codigo: "F002", nombre: "Faena destino", activo: true, ubicacionTecnica: null }]) : Promise.reject(new Error("403 Sin permiso")) as never);
    renderQuery(<UnitTransferDialog open unit={unit} onClose={vi.fn()} />);
    await waitFor(() => expect(screen.getByRole("combobox")).not.toBeDisabled());
    fireEvent.change(screen.getByRole("combobox"), { target: { value: "F002" } });
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Cambio" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar traslado" }));
    expect(await screen.findByText("403 Sin permiso")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByLabelText("Motivo")).toHaveValue("Cambio");
  });
});

describe("UnitCompositionDialogs", () => {
  it("mounts through the shared composition mutation", async () => {
    const request = compositionApi();
    renderQuery(<UnitCompositionDialogs unitCode="CFA-1" unitSiteCode="F001" components={components} mode="mount" onClose={vi.fn()} />);
    await chooseCandidate();
    fireEvent.change(screen.getByLabelText("Rol de componente"), { target: { value: "CHASIS" } });
    fireEvent.change(screen.getByLabelText("Motivo auditable"), { target: { value: "Instalacion" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar montaje" }));
    await waitFor(() => expect(request).toHaveBeenCalledWith("/api/operational-units/CFA-1/components", expect.objectContaining({ method: "POST" })));
  });

  it("unmounts the selected installed component without native confirmation", async () => {
    const request = compositionApi();
    renderQuery(<UnitCompositionDialogs unitCode="CFA-1" components={components} mode="unmount" onClose={vi.fn()} />);
    fireEvent.change(screen.getByLabelText("Componente"), { target: { value: "FB-1" } });
    fireEvent.change(screen.getByLabelText("Motivo auditable"), { target: { value: "Reparacion" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar desmontaje" }));
    await waitFor(() => expect(request).toHaveBeenCalledWith("/api/operational-units/CFA-1/components/FB-1/unmount", expect.objectContaining({ method: "POST" })));
  });

  it("replaces with one shared transaction instead of separate mount and unmount calls", async () => {
    const request = compositionApi();
    renderQuery(<UnitCompositionDialogs unitCode="CFA-1" unitSiteCode="F001" components={components} mode="replace" onClose={vi.fn()} />);
    fireEvent.change(screen.getByLabelText("Componente saliente"), { target: { value: "CH-1" } });
    await chooseCandidate();
    fireEvent.change(screen.getByLabelText("Motivo auditable"), { target: { value: "Renovacion" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar reemplazo" }));
    await waitFor(() => expect(request).toHaveBeenCalledWith("/api/operational-units/CFA-1/components/replace", expect.objectContaining({ method: "POST" })));
    expect(request.mock.calls.filter(call => String(call[0]).includes("/unmount") || String(call[0]).endsWith("/components")).length).toBe(0);
  });
});

describe("UnitMaintenanceActions", () => {
  it("navigates to canonical notice and work-order forms with the unit target", () => {
    render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}><UnitMaintenanceActions unitCode="CFA-1" /><Location /></MemoryRouter>);
    fireEvent.click(screen.getByRole("button", { name: "Crear aviso" }));
    expect(screen.getByTestId("location")).toHaveTextContent("/avisos?targetType=OperationalUnit&targetCode=CFA-1");
    fireEvent.click(screen.getByRole("button", { name: "Crear OT" }));
    expect(screen.getByTestId("location")).toHaveTextContent("/ot?targetType=OperationalUnit&targetCode=CFA-1");
  });
});

