import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { OperationalStatusBadge, resolveOperationalStatusVisual } from "./OperationalStatusBadge";

afterEach(cleanup);

const status = () => screen.getByText(/./).getAttribute("data-status");

describe("OperationalStatusBadge", () => {
  it.each([
    ["OPERATIVO", "Operativo", "operational"], ["CON_ALERTA", "Con alerta", "warning"],
    ["FUERA_SERVICIO", "F/S", "out-of-service"], ["CORRECTIVO", "Correctivo", "corrective"],
    ["PREVENTIVO", "Preventivo", "preventive"], ["DOCUMENTAL", "Documental", "documental"],
    ["PREPARACION", "Preparación", "preparation"], ["DADO_DE_BAJA", "Dado de baja", "decommissioned"]
  ])("renders %s with its stable visual state", (code, label, visual) => {
    render(<OperationalStatusBadge code={code} />);
    expect(screen.getByText(label)).toBeInTheDocument();
    expect(status()).toBe(visual);
  });

  it("recognizes aliases after normalizing accents, spaces, hyphens and underscores", () => {
    expect(resolveOperationalStatusVisual("fuera de servicio")).toBe("out-of-service");
    expect(resolveOperationalStatusVisual("F/S")).toBe("out-of-service");
    expect(resolveOperationalStatusVisual("en-preparación")).toBe("preparation");
    expect(resolveOperationalStatusVisual("dados_de_baja")).toBe("decommissioned");
  });

  it("uses the API name only when no code exists and remains neutral for unknown values", () => {
    const { rerender } = render(<OperationalStatusBadge name="Correctivo" />);
    expect(status()).toBe("corrective");
    rerender(<OperationalStatusBadge code="ESTADO_NUEVO" name="Estado API" />);
    expect(screen.getByText("Estado API")).toBeInTheDocument();
    expect(status()).toBe("unknown");
    rerender(<OperationalStatusBadge />);
    expect(screen.getByText("NA")).toBeInTheDocument();
    expect(status()).toBe("unknown");
  });
});