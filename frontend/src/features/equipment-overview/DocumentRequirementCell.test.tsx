import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { DocumentRequirementCell } from "./DocumentRequirementCell";

const status = (daysUntilExpiration: number | null, status = "VIGENTE") => ({ code: "TechnicalReview", status, applies: true, expirationDate: "2026-12-25", daysUntilExpiration });
const state = () => screen.getByTitle(/./).getAttribute("data-state");
afterEach(cleanup);

describe("DocumentRequirementCell", () => {
  it("uses green for valid documents over the alert window", () => {
    const { rerender } = render(<DocumentRequirementCell value={status(148)} />);
    expect(screen.getByText("Vigente")).toBeInTheDocument(); expect(screen.getByText("148 d\u00edas")).toBeInTheDocument(); expect(state()).toBe("valid");
    rerender(<DocumentRequirementCell value={status(31)} />); expect(screen.getByText("Vigente")).toBeInTheDocument(); expect(state()).toBe("valid");
  });

  it("uses amber and the expiring label from zero through thirty days", () => {
    const { rerender } = render(<DocumentRequirementCell value={status(30)} />);
    expect(screen.getByText("Por vencer")).toBeInTheDocument(); expect(state()).toBe("expiring");
    rerender(<DocumentRequirementCell value={status(1)} />); expect(screen.getByText("1 d\u00eda")).toBeInTheDocument(); expect(state()).toBe("expiring");
    rerender(<DocumentRequirementCell value={status(0)} />); expect(screen.getByText("Vence hoy")).toBeInTheDocument(); expect(state()).toBe("expiring");
  });

  it("uses red for expired and missing documents", () => {
    const { rerender } = render(<DocumentRequirementCell value={status(-1)} />);
    expect(screen.getByText("Vencido")).toBeInTheDocument(); expect(state()).toBe("expired");
    rerender(<DocumentRequirementCell value={status(null, "PENDIENTE_CARGA")} />); expect(screen.getByText("Pendiente de carga")).toBeInTheDocument(); expect(state()).toBe("missing");
  });

  it("uses blue, neutral and unknown visual states consistently", () => {
    const { rerender } = render(<DocumentRequirementCell value={status(null, "PENDIENTE_VALIDACION")} />);
    expect(state()).toBe("pending-validation");
    rerender(<DocumentRequirementCell value={{ code: "TechnicalReview", applies: false }} />); expect(screen.getByText("No aplica")).toBeInTheDocument(); expect(state()).toBe("not-applicable");
    rerender(<DocumentRequirementCell value={null} />); expect(screen.getByText("NA")).toBeInTheDocument(); expect(state()).toBe("unknown");
  });
});