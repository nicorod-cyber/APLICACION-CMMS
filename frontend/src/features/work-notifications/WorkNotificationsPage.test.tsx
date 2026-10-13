import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import * as auth from "../auth/authStore";
import { WorkNotificationsPage, measurementOf } from "./WorkNotificationsPage";

const nodes = [
  { id: "00000000-0000-0000-0000-000000000001", codigo: "SYS-1", nombre: "Motor", nivel: "Sistema", codigoPadre: null, faenaCodigo: null },
  { id: "00000000-0000-0000-0000-000000000002", codigo: "SUB-1", nombre: "Lubricación", nivel: "Subsistema", codigoPadre: "SYS-1", faenaCodigo: null },
  { id: "00000000-0000-0000-0000-000000000003", codigo: "CMP-1", nombre: "Bomba", nivel: "Componente", codigoPadre: "SUB-1", faenaCodigo: null }
] as const;
let failUpload = false;

function renderPage() {
  return render(<MemoryRouter initialEntries={["/avisos"]}><Routes><Route path="/avisos" element={<WorkNotificationsPage/>}/><Route path="/avisos/:avisoId" element={<p>Detalle cargado</p>}/></Routes></MemoryRouter>);
}

beforeEach(() => {
  failUpload = false;
  vi.spyOn(auth, "apiFetch").mockImplementation((path, init) => {
    if (path.startsWith("/api/work-notifications/?")) return Promise.resolve([]) as never;
    if (path === "/api/assets?page=1&pageSize=100") return Promise.resolve({ items: [{ codigo: "ACT-1", nombre: "Camión simple", faenaCodigo: "FAE-1" }] }) as never;
    if (path === "/api/operational-units?page=1&pageSize=100") return Promise.resolve({ items: [{ codigo: "UNIT-1", nombre: "Equipo compuesto", faenaCodigo: "FAE-1" }] }) as never;
    if (path === "/api/technical-hierarchy/nodes?includeObsolete=false") return Promise.resolve(nodes) as never;
    if (path === "/api/assets/ACT-1") return Promise.resolve({ resumen: { tipoActivoNombre: "Camión", tipoMedicionUso: "HOROMETRO", faenaNombre: "Faena Uno" }, marca: "Volvo", modelo: "FMX", numeroSerie: "VIN-1", anioFabricacion: 2025, atributos: [{ definicionCodigo: "PATENTE", nombre: "Patente", valorTexto: "ABCD-12" }] }) as never;
    if (path === "/api/operational-units/UNIT-1") return Promise.resolve({ tipoUnidadNombre: "Camión fábrica", faenaNombre: "Faena Uno", unidadLectura: "horas", composicion: { vigentes: [{ activoCodigo: "CH-1", rolComponenteCodigo: "CHASIS" }, { activoCodigo: "FB-1", rolComponenteCodigo: "FABRICA" }] } }) as never;
    if (path === "/api/assets/CH-1") return Promise.resolve({ resumen: { tipoActivoNombre: "Chasis", tipoMedicionUso: "HOROMETRO", faenaNombre: "Faena Uno" }, marca: "Mercedes", modelo: "Arocs", numeroSerie: "VIN-CH", anioFabricacion: 2024, atributos: [] }) as never;
    if (path === "/api/work-notifications/" && init?.method === "POST") return Promise.resolve({ avisoId: "AV-000001", estado: "Borrador", faenaCodigo: "FAE-1", activoCodigo: "ACT-1", fechaDeteccion: "2026-10-06T12:00:00Z", trabajos: [{ id: "item-1", secuencia: 1 }] }) as never;
    if (path.includes("/items/item-1/evidences") && init?.method === "POST") return failUpload ? Promise.reject(new Error("Archivo inválido")) as never : Promise.resolve({}) as never;
    return Promise.reject(new Error(`Solicitud inesperada: ${path}`)) as never;
  });
});
afterEach(() => { cleanup(); vi.restoreAllMocks(); });

describe("WorkNotificationsPage", () => {
  it("shows the measurement label for hours and kilometers", () => {
    expect(measurementOf("HOROMETRO")).toBe("hours");
    expect(measurementOf("KILOMETRAJE")).toBe("kilometers");
    expect(measurementOf(null)).toBeNull();
  });

  it("filters the technical hierarchy and hides component role for a simple asset", async () => {
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "+ Nuevo Aviso" }));
    fireEvent.change(screen.getByLabelText("Equipo"), { target: { value: "asset:ACT-1" } });
    expect(await screen.findByLabelText("Horómetro actual")).toBeInTheDocument();
    expect(screen.queryByLabelText("Fábrica o chasis trabajo 1")).not.toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Sistema trabajo 1"), { target: { value: nodes[0].id } });
    expect(screen.getByRole("option", { name: "Lubricación" })).toBeInTheDocument();
    fireEvent.change(screen.getByLabelText("Subsistema trabajo 1"), { target: { value: nodes[1].id } });
    expect(screen.getByRole("option", { name: "Bomba" })).toBeInTheDocument();
  });

  it("shows component role for a composed unit and labels additional work cards", async () => {
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "+ Nuevo Aviso" }));
    fireEvent.change(screen.getByLabelText("Equipo"), { target: { value: "unit:UNIT-1" } });
    expect(await screen.findByLabelText("Fábrica o chasis trabajo 1")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "+ Agregar trabajo" }));
    expect(screen.getByRole("heading", { name: "Trabajo 2" })).toBeInTheDocument();
    expect(screen.getByLabelText("Fábrica o chasis trabajo 2")).toBeInTheDocument();
  });

  it("retries a failed upload without posting a second notice", async () => {
    failUpload = true;
    renderPage();
    fireEvent.click(await screen.findByRole("button", { name: "+ Nuevo Aviso" }));
    fireEvent.change(screen.getByLabelText("Equipo"), { target: { value: "asset:ACT-1" } });
    await screen.findByLabelText("Horómetro actual");
    fireEvent.change(screen.getByLabelText("Sistema trabajo 1"), { target: { value: nodes[0].id } });
    fireEvent.change(screen.getByLabelText("Subsistema trabajo 1"), { target: { value: nodes[1].id } });
    fireEvent.change(screen.getByLabelText("Componente trabajo 1"), { target: { value: nodes[2].id } });
    fireEvent.change(screen.getByLabelText("Descripción trabajo 1"), { target: { value: "Fuga" } });
    const file = new File(["not-a-real-png"], "evidence.png", { type: "image/png" });
    fireEvent.change(screen.getByLabelText("Adjuntar evidencias trabajo 1"), { target: { files: [file] } });
    fireEvent.click(screen.getByRole("button", { name: "Guardar borrador" }));
    expect(await screen.findByText(/quedó guardado como borrador/)).toBeInTheDocument();
    failUpload = false;
    fireEvent.click(screen.getByRole("button", { name: "Reintentar evidencias" }));
    await screen.findByText("Detalle cargado");
    expect(vi.mocked(auth.apiFetch).mock.calls.filter(call => call[0] === "/api/work-notifications/")).toHaveLength(1);
  });
});
