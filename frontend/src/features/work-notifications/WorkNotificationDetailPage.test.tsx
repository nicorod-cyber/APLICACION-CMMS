import { cleanup, render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { afterEach, expect, it, vi } from "vitest";
import * as auth from "../auth/authStore";
import { WorkNotificationDetailPage } from "./WorkNotificationDetailPage";

afterEach(() => { cleanup(); vi.restoreAllMocks(); });

it("renders technical classification and secure evidence links for each work item", async () => {
  vi.spyOn(auth, "apiFetch").mockImplementation(path => {
    if (path === "/api/technical-hierarchy/nodes?includeObsolete=true") return Promise.resolve([
      { id: "sys", codigo: "S", nombre: "Motor", nivel: "Sistema" },
      { id: "sub", codigo: "SS", nombre: "Lubricación", nivel: "Subsistema" },
      { id: "cmp", codigo: "C", nombre: "Bomba", nivel: "Componente" }
    ]) as never;
    if (path === "/api/work-notifications/AV-000001") return Promise.resolve({ avisoId: "AV-000001", estado: "Borrador", faenaCodigo: "FAE-1", activoCodigo: "ACT-1", fechaDeteccion: "2026-10-06T12:00:00Z", historial: [], trabajos: [{ id: "item-1", secuencia: 1, descripcion: "Reparar fuga", observaciones: "Revisar sello", affectedAssetCodeSnapshot: "ACT-1", affectedAssetNameSnapshot: "Camión", technicalSystemId: "sys", technicalSubsystemId: "sub", technicalComponentId: "cmp", estado: "PendientePlanificacion", evidencias: [{ id: "e-1", fileId: "f-1", fileKey: "Faena/Evidencias/photo.png", nombreArchivo: "photo.png", contentType: "image/png", sizeBytes: 512 }] }] }) as never;
    return Promise.reject(new Error(`Solicitud inesperada: ${path}`)) as never;
  });
  render(<MemoryRouter initialEntries={["/avisos/AV-000001"]}><Routes><Route path="/avisos/:avisoId" element={<WorkNotificationDetailPage/>}/></Routes></MemoryRouter>);

  expect(await screen.findByText("Reparar fuga")).toBeInTheDocument();
  expect(screen.getByText("Motor")).toBeInTheDocument();
  expect(screen.getByText("Lubricación")).toBeInTheDocument();
  expect(screen.getByText("Bomba")).toBeInTheDocument();
  expect(screen.queryByText("Sin evidencias")).not.toBeInTheDocument();
  expect(screen.getByRole("link", { name: "photo.png" })).toHaveAttribute("href", "/api/sharepoint/download?fileKey=Faena%2FEvidencias%2Fphoto.png");
});
