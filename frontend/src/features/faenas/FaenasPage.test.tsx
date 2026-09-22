import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as auth from "../auth/authStore";
import { FaenasPage } from "./FaenasPage";

const faena = { id: "1", codigo: "FAE_AND", nombre: "Andina", zona: "Zona 4", cliente: "Codelco", activo: true, ubicacionTecnica: null };
function Location() { const location = useLocation(); return <output data-testid="location">{location.pathname}</output>; }
function renderPage() { return render(<MemoryRouter><QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><FaenasPage /><Location /></QueryClientProvider></MemoryRouter>); }

beforeEach(() => {
  auth.useAuthStore.setState({ user: { id: "1", username: "reader", email: "reader@example.test", displayName: "Reader", isActive: true, isLocked: false, roles: [auth.AUTH_ROLES.technician], permissions: [auth.AUTH_PERMISSIONS.viewFaenas], faenas: ["FAE_AND"] } });
  vi.spyOn(auth, "apiFetch").mockImplementation(path => path.startsWith("/api/faenas?") ? Promise.resolve([faena]) as never : Promise.reject(new Error(`Solicitud inesperada: ${path}`)) as never);
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("FaenasPage", () => {
  it("navigates to the operational page when a row is clicked", async () => {
    renderPage();
    fireEvent.click(await screen.findByRole("row", { name: /Abrir faena Andina/ }));
    expect(screen.getByTestId("location")).toHaveTextContent("/faenas/FAE_AND");
  });

  it("allows read-only users to load without requesting responsible-user-options", async () => {
    renderPage();
    await screen.findByText("Andina");
    expect(auth.apiFetch).toHaveBeenCalledWith("/api/faenas?includeInactive=true");
    expect(vi.mocked(auth.apiFetch).mock.calls.some(call => String(call[0]).includes("responsible-user-options"))).toBe(false);
    expect(screen.queryByRole("button", { name: "Nueva faena" })).not.toBeInTheDocument();
  });
});
