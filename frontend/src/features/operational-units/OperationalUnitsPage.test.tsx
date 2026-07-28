import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as auth from "../auth/authStore";
import { OperationalUnitsPage } from "./OperationalUnitsPage";

const unit = { codigo: "CFA-1", nombre: "Unidad CFA", tipoUnidadCodigo: "CFA", faenaCodigo: "F001", ubicacionTecnicaCodigo: "LOC-1", estadoOperacionalCodigo: "OPERATIVO", criticidad: "Alta", observaciones: "Prueba", composicionCompleta: true, rolesFaltantes: [], composicion: { completa: true, faltantes: [], vigentes: [{ activoCodigo: "CH-1", activoNombre: "Chasis 1", rolComponenteCodigo: "CHASIS", fechaMontajeUtc: "2026-01-01", vigente: true }, { activoCodigo: "FB-1", activoNombre: "Fabrica 1", rolComponenteCodigo: "FABRICA", fechaMontajeUtc: "2026-01-01", vigente: true }], historial: [] } };
const secondUnit = { ...unit, codigo: "CFA-2", nombre: "Unidad secundaria" };
const permissions = [auth.AUTH_PERMISSIONS.manageOperationalUnits, auth.AUTH_PERMISSIONS.manageOperationalUnitComposition, auth.AUTH_PERMISSIONS.changeAssetFaena];
let responseOverride: ((path: string, init?: RequestInit) => Promise<unknown> | undefined) | undefined;

function Location() { const location = useLocation(); return <output data-testid="location">{location.pathname + location.search}</output>; }
function renderPage() { return render(<MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}><QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}><OperationalUnitsPage /><Location /></QueryClientProvider></MemoryRouter>); }
function mockApi() { return vi.spyOn(auth, "apiFetch").mockImplementation((path: string, init?: RequestInit) => {
  const overridden = responseOverride?.(path, init);
  if (overridden) return overridden as never;
  if (path.startsWith("/api/operational-units?")) return Promise.resolve({ items: [unit], page: 1, hasNextPage: false, totalCount: 1 }) as never;
  if (path === "/api/operational-units/CFA-1") return Promise.resolve(unit) as never;
  if (path.endsWith("/composition-rules")) return Promise.resolve([]) as never;
  if (path.startsWith("/api/faenas")) return Promise.resolve([{ id: "2", codigo: "F002", nombre: "Faena destino", activo: true, ubicacionTecnica: null }]) as never;
  if (path.startsWith("/api/assets?")) return Promise.resolve({ items: [{ codigo: "CH-2", nombre: "Chasis 2", faenaCodigo: "F001" }] }) as never;
  return Promise.resolve({}) as never;
}) as never; }
async function selectUnit() { fireEvent.click(await screen.findByRole("button", { name: /CFA-1/ })); await screen.findByRole("button", { name: "Editar" }); }
async function closeDialog() { fireEvent.click(screen.getByRole("button", { name: /Cerrar di/ })); await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument()); }

beforeEach(() => {
  responseOverride = undefined;
  auth.useAuthStore.setState({ user: { id: "1", username: "tester", email: "tester@example.com", displayName: "Tester", isActive: true, isLocked: false, roles: [], permissions, faenas: ["F001", "F002"] } });
  mockApi();
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("OperationalUnitsPage shared actions", () => {
  it("opens the shared editor for the selected unit and delegates a successful update", async () => {
    const request = vi.mocked(auth.apiFetch);
    renderPage();
    await selectUnit();
    fireEvent.click(screen.getByRole("button", { name: "Editar" }));
    expect(await screen.findByRole("dialog", { name: /Editar identific/ })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Unidad editada" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));
    await waitFor(() => expect(request).toHaveBeenCalledWith("/api/operational-units/CFA-1", expect.objectContaining({ method: "PUT" })));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("keeps the shared editor open with its values when the mutation fails", async () => {
    responseOverride = (path, init) => path === "/api/operational-units/CFA-1" && init?.method === "PUT" ? Promise.reject(new Error("No autorizado")) : undefined;
    renderPage();
    await selectUnit();
    fireEvent.click(screen.getByRole("button", { name: "Editar" }));
    fireEvent.change(screen.getByLabelText("Nombre"), { target: { value: "Unidad editada" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar cambios" }));
    expect(await screen.findByText("No autorizado")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByLabelText("Nombre")).toHaveValue("Unidad editada");
  });

  it("opens the shared transfer dialog and executes one complete-unit transaction", async () => {
    const request = vi.mocked(auth.apiFetch);
    renderPage();
    await selectUnit();
    fireEvent.click(screen.getByRole("button", { name: "Trasladar" }));
    expect(await screen.findByRole("dialog", { name: "Trasladar unidad completa" })).toBeInTheDocument();
    expect(screen.getByText("Chasis 1 (CH-1)")).toBeInTheDocument();
    expect(screen.getByText("Fabrica 1 (FB-1)")).toBeInTheDocument();
    await waitFor(() => expect(screen.getByRole("combobox", { name: "Faena destino" })).not.toBeDisabled());
    fireEvent.change(screen.getByRole("combobox", { name: "Faena destino" }), { target: { value: "F002" } });
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Cambio de contrato" } });
    fireEvent.click(screen.getByRole("button", { name: "Confirmar traslado" }));
    await waitFor(() => expect(request).toHaveBeenCalledWith("/api/assets/CH-1/transfers", expect.objectContaining({ method: "POST" })));
    expect(request.mock.calls.filter(call => String(call[0]).includes("/transfers")).length).toBe(1);
    expect(request.mock.calls.some(call => String(call[0]).includes("/assets/FB-1/transfers"))).toBe(false);
  });

  it("opens the real shared composition dialogs in mount, replace and unmount modes", async () => {
    renderPage();
    await selectUnit();
    fireEvent.click(screen.getByRole("button", { name: "Montar" }));
    expect(await screen.findByRole("dialog", { name: "Montar componente" })).toHaveTextContent("La API valida compatibilidad");
    await closeDialog();
    fireEvent.click(screen.getByRole("button", { name: "Reemplazar" }));
    expect(await screen.findByRole("dialog", { name: "Reemplazar componente" })).toHaveTextContent("CHASIS");
    await closeDialog();
    fireEvent.click(screen.getByRole("button", { name: "Desmontar" }));
    expect(await screen.findByRole("dialog", { name: "Desmontar componente" })).toHaveTextContent("FABRICA");
  });

  it("uses UnitMaintenanceActions navigation with the operational-unit target", async () => {
    renderPage();
    await selectUnit();
    fireEvent.click(screen.getByRole("button", { name: "Crear aviso" }));
    expect(screen.getByTestId("location")).toHaveTextContent("/avisos?targetType=OperationalUnit&targetCode=CFA-1");
    fireEvent.click(screen.getByRole("button", { name: "Crear OT" }));
    expect(screen.getByTestId("location")).toHaveTextContent("/ot?targetType=OperationalUnit&targetCode=CFA-1");
  });

  it("shows loading, error and empty states", async () => {
    let resolveList: ((value: unknown) => void) | undefined;
    responseOverride = path => path.startsWith("/api/operational-units?") ? new Promise(resolve => { resolveList = resolve; }) : undefined;
    renderPage();
    expect(screen.getByText("Cargando...")).toBeInTheDocument();
    resolveList?.({ items: [], page: 1, hasNextPage: false, totalCount: 0 });
    expect(await screen.findByText("No hay unidades para los filtros seleccionados.")).toBeInTheDocument();
  });

  it("shows a list error", async () => {
    responseOverride = path => path.startsWith("/api/operational-units?") ? Promise.reject(new Error("Listado no disponible")) : undefined;
    renderPage();
    expect(await screen.findByText("Listado no disponible")).toBeInTheDocument();
  });

  it("applies the search filter and loads another page", async () => {
    const request = vi.mocked(auth.apiFetch);
    responseOverride = path => {
      if (!path.startsWith("/api/operational-units?")) return undefined;
      return Promise.resolve(path.includes("page=2") ? { items: [secondUnit], page: 2, hasNextPage: false, totalCount: 2 } : { items: [unit], page: 1, hasNextPage: true, totalCount: 2 });
    };
    renderPage();
    await screen.findByRole("button", { name: /CFA-1/ });
    fireEvent.change(screen.getByLabelText("Buscar"), { target: { value: "CFA" } });
    fireEvent.click(screen.getByRole("button", { name: "Filtrar" }));
    await waitFor(() => expect(request).toHaveBeenCalledWith(expect.stringContaining("texto=CFA")));
    fireEvent.click(await screen.findByRole("button", { name: "Cargar mas" }));
    expect(await screen.findByRole("button", { name: /CFA-2/ })).toBeInTheDocument();
  });

  it("prevents users without permissions from opening or mutating protected flows", async () => {
    auth.useAuthStore.setState({ user: { ...auth.useAuthStore.getState().user!, permissions: [] } });
    const request = vi.mocked(auth.apiFetch);
    renderPage();
    await selectUnit();
    for (const action of ["Editar", "Trasladar", "Montar", "Reemplazar", "Desmontar"]) {
      const button = screen.getByRole("button", { name: action });
      expect(button).toBeDisabled();
      fireEvent.click(button);
    }
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(request.mock.calls.filter(call => ["POST", "PUT", "DELETE"].includes(String((call[1] as RequestInit | undefined)?.method))).length).toBe(0);
  });
});

