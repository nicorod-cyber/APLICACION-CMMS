import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as auth from "../auth/authStore";
import type { EquipmentOverviewRow, Page } from "../equipment-overview/types";
import { FaenaDetailPage } from "./FaenaDetailPage";

const faena = { id: "1", codigo: "FAE_AND", nombre: "Andina", zona: "Zona 4", cliente: "Codelco", activo: true, ubicacionTecnica: null };
const row = (overrides: Partial<EquipmentOverviewRow>): EquipmentOverviewRow => ({ rowId: "asset-1", rowType: "ASSET", assetId: "asset-1", code: "CHF-001", name: "Camión 1", operationalStateCode: "OPERATIVO", operationalStateName: "Operativo", equipmentTypeName: "Camión", usageMeasurementType: "HOROMETRO", usageUnit: "h", lastReading: 100, lastReadingAtUtc: "2026-09-22T12:00:00Z", readingAvailable: true, ...overrides });
const asset = row({});
const unit = row({ rowId: "unit-1", rowType: "COMPOSITE_UNIT", assetId: null, operationalUnitId: "unit-1", code: "CFA-001", name: "Camión fábrica", lastReading: 200 });
const kilometer = row({ rowId: "asset-2", code: "CAM-002", name: "Camioneta", usageMeasurementType: "KILOMETRAJE", usageUnit: "km", lastReading: 5000 });
let mutationOverride: ((path: string, init?: RequestInit) => Promise<unknown> | undefined) | undefined;

function renderPage(permissions = [auth.AUTH_PERMISSIONS.viewFaenas, auth.AUTH_PERMISSIONS.registerAssetReadings]) {
  auth.useAuthStore.setState({ user: { id: "1", username: "tech", email: "tech@example.test", displayName: "Tech", isActive: true, isLocked: false, roles: [auth.AUTH_ROLES.technician], permissions, faenas: ["FAE_AND"] } });
  return render(<MemoryRouter initialEntries={["/faenas/FAE_AND"]}><QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}><Routes><Route path="/faenas/:codigo" element={<FaenaDetailPage />} /></Routes></QueryClientProvider></MemoryRouter>);
}

beforeEach(() => {
  mutationOverride = undefined;
  vi.spyOn(auth, "apiFetch").mockImplementation((path, init) => {
    const overridden = mutationOverride?.(path, init);
    if (overridden) return overridden as never;
    if (path === "/api/faenas/FAE_AND") return Promise.resolve(faena) as never;
    if (path.includes("/api/assets/equipment-overview?") && path.includes("page=1")) return Promise.resolve({ items: [asset, unit], page: 1, pageSize: 100, totalCount: 3, totalPages: 2, hasNextPage: true, hasPreviousPage: false } satisfies Page<EquipmentOverviewRow>) as never;
    if (path.includes("/api/assets/equipment-overview?") && path.includes("page=2")) return Promise.resolve({ items: [kilometer], page: 2, pageSize: 100, totalCount: 3, totalPages: 2, hasNextPage: false, hasPreviousPage: true } satisfies Page<EquipmentOverviewRow>) as never;
    if (path === "/api/assets/CHF-001/readings" && init?.method === "POST") return Promise.resolve({ valor: 110, fechaLecturaUtc: "2026-09-22T13:00:00Z" }) as never;
    if (path === "/api/operational-units/CFA-001/readings" && init?.method === "POST") return Promise.resolve({ lecturas: [{ valor: 210, fechaLecturaUtc: "2026-09-22T13:01:00Z" }] }) as never;
    return Promise.reject(new Error(`Solicitud inesperada: ${path}`)) as never;
  });
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("FaenaDetailPage", () => {
  it("loads directly from the URL and follows every overview page", async () => {
    renderPage();
    expect(await screen.findByRole("heading", { name: "Andina" })).toBeInTheDocument();
    expect(await screen.findByText("Camión 1")).toBeInTheDocument();
    expect(screen.getByText("Camión fábrica")).toBeInTheDocument();
    expect(screen.getByText("Camioneta")).toBeInTheDocument();
    expect(vi.mocked(auth.apiFetch).mock.calls.some(call => String(call[0]).includes("page=2"))).toBe(true);
  });

  it("saves ASSET and COMPOSITE_UNIT through their respective endpoints while preserving other inputs", async () => {
    renderPage();
    const assetInput = await screen.findByLabelText("Nueva lectura CHF-001");
    const unitInput = screen.getByLabelText("Nueva lectura CFA-001");
    fireEvent.change(assetInput, { target: { value: "110" } });
    fireEvent.change(unitInput, { target: { value: "210" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar CHF-001" }));
    await waitFor(() => expect(auth.apiFetch).toHaveBeenCalledWith("/api/assets/CHF-001/readings", expect.objectContaining({ method: "POST" })));
    await waitFor(() => expect(assetInput).toHaveValue(null));
    expect(unitInput).toHaveValue(210);
    fireEvent.click(screen.getByRole("button", { name: "Guardar CFA-001" }));
    await waitFor(() => expect(auth.apiFetch).toHaveBeenCalledWith("/api/operational-units/CFA-001/readings", expect.objectContaining({ method: "POST" })));
    await waitFor(() => expect(unitInput).toHaveValue(null));
  });

  it("keeps the failing row value and leaves other rows untouched", async () => {
    mutationOverride = (path, init) => path === "/api/assets/CHF-001/readings" && init?.method === "POST" ? Promise.reject(new Error("Lectura inferior a la vigente")) : undefined;
    renderPage();
    const assetInput = await screen.findByLabelText("Nueva lectura CHF-001");
    const unitInput = screen.getByLabelText("Nueva lectura CFA-001");
    fireEvent.change(assetInput, { target: { value: "120" } });
    fireEvent.change(unitInput, { target: { value: "220" } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar CHF-001" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Lectura inferior a la vigente");
    expect(assetInput).toHaveValue(120);
    expect(unitInput).toHaveValue(220);
  });

  it("disables saving without permission and filters horometers and kilometers locally", async () => {
    renderPage([auth.AUTH_PERMISSIONS.viewFaenas]);
    const input = await screen.findByLabelText("Nueva lectura CHF-001");
    expect(input).toBeDisabled();
    expect(screen.getByRole("button", { name: "Guardar CHF-001" })).toBeDisabled();
    fireEvent.click(screen.getByRole("button", { name: "Kilometrajes" }));
    expect(screen.getByText("Camioneta")).toBeInTheDocument();
    expect(screen.queryByText("Camión fábrica")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Horómetros" }));
    expect(screen.getByText("Camión fábrica")).toBeInTheDocument();
    expect(screen.queryByText("Camioneta")).not.toBeInTheDocument();
  });
});
