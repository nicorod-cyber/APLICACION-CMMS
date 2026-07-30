import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as auth from "../auth/authStore";
import { useAuthStore, type CurrentUser } from "../auth/authStore";
import { AssetActionsPanel } from "./AssetActionsPanel";

const user: CurrentUser = { id: "1", username: "planner", email: "planner@example.com", displayName: "Planner", isActive: true, isLocked: false, roles: ["planificador"], permissions: ["activos.lecturas.registrar", "activos.lecturas.corregir", "activos.cambiar_faena", "activos.administrar"], faenas: [] };
const renderUi = () => render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } })}><AssetActionsPanel code="AC-1" measurementType="HORAS" unit="h" lastReading={100} readings={[{ id: "read-1", valor: 100, unidad: "h", fechaLecturaUtc: "2026-01-01T00:00:00Z" }]} locationType="FAENA" faenaCode="F001" currentStateCode="OPERATIVO" /></QueryClientProvider>);

beforeEach(() => { useAuthStore.setState({ user }); vi.spyOn(auth, "apiFetch").mockImplementation((path: string) => Promise.resolve(path === "/api/assets/catalog" ? { estadosOperacionales: [] } : []) as never); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("AssetActionsPanel", () => {
  it("uses the canonical immutable correction endpoint", async () => {
    const request = vi.spyOn(auth, "apiFetch").mockImplementation((path: string) => Promise.resolve(path === "/api/assets/catalog" ? { estadosOperacionales: [] } : []) as never);
    renderUi();
    fireEvent.click(screen.getByRole("button", { name: /^Corregir lectura/ }));
    fireEvent.change(screen.getByLabelText("Lectura a corregir"), { target: { value: "read-1" } });
    fireEvent.change(screen.getByLabelText("Valor corregido"), { target: { value: "105" } });
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Ajuste validado" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar corrección" }));
    await waitFor(() => expect(request).toHaveBeenCalledWith("/api/assets/AC-1/readings/read-1/corrections", expect.objectContaining({ method: "POST" })));
  });

  it("filters the manual state selector by the current physical location and shows visible names", async () => {
    vi.spyOn(auth, "apiFetch").mockImplementation((path: string) => {
      if (path.includes("operational-states")) return Promise.resolve([
        { codigo: "OPERATIVO", nombre: "Operativo" },
        { codigo: "CON_ALERTA", nombre: "Con alerta" },
        { codigo: "FUERA_SERVICIO", nombre: "F/S" },
        { codigo: "PREPARACION", nombre: "Preparación" },
        { codigo: "DADO_DE_BAJA", nombre: "Dado de baja" }
      ]) as never;
      return Promise.resolve([]) as never;
    });
    renderUi();
    fireEvent.click(screen.getByRole("button", { name: /^Registrar estado/ }));
    const selector = await screen.findByLabelText("Nuevo estado");
    await waitFor(() => {
      expect(selector).toHaveTextContent("Con alerta");
      expect(selector).toHaveTextContent("F/S");
      expect(selector).toHaveTextContent("Preparación");
      expect(selector).not.toHaveTextContent("OPERATIVO");
    });
  });
  it("keeps the correction dialog and values visible after a forbidden response", async () => {
    vi.spyOn(auth, "apiFetch").mockImplementation((path: string) => path === "/api/assets/catalog" ? Promise.resolve({ estadosOperacionales: [] }) : path.includes("/corrections") ? Promise.reject(new Error("403 Sin permiso")) : Promise.resolve([]) as never);
    renderUi();
    fireEvent.click(screen.getByRole("button", { name: /^Corregir lectura/ }));
    fireEvent.change(screen.getByLabelText("Lectura a corregir"), { target: { value: "read-1" } });
    fireEvent.change(screen.getByLabelText("Valor corregido"), { target: { value: "105" } });
    fireEvent.change(screen.getByLabelText("Motivo"), { target: { value: "Ajuste validado" } });
    fireEvent.click(screen.getByRole("button", { name: "Registrar corrección" }));
    expect(await screen.findByText("403 Sin permiso")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByRole("dialog")).toHaveClass("dark:bg-slate-900");
    expect(screen.getByLabelText("Motivo")).toHaveValue("Ajuste validado");
  });
});