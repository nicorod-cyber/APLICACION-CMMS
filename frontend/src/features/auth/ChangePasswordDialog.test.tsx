import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ChangePasswordDialog } from "./ChangePasswordDialog";
import { apiFetch } from "./authStore";

vi.mock("./authStore", () => ({ apiFetch: vi.fn() }));

afterEach(() => {
  cleanup();
  vi.clearAllMocks();
});

describe("ChangePasswordDialog", () => {
  it("shows password fields and validates mismatched confirmation without a request", () => {
    render(<ChangePasswordDialog onClose={vi.fn()} />);

    expect(screen.getByLabelText("Contraseña actual")).toHaveAttribute("type", "password");
    fireEvent.click(screen.getByLabelText("Mostrar contraseña actual"));
    expect(screen.getByLabelText("Contraseña actual")).toHaveAttribute("type", "text");

    fireEvent.change(screen.getByLabelText("Contraseña actual"), { target: { value: "Actual.Clave2026!" } });
    fireEvent.change(screen.getByLabelText("Nueva contraseña"), { target: { value: "Nueva.Clave2026!" } });
    fireEvent.change(screen.getByLabelText("Confirmar nueva contraseña"), { target: { value: "Otra.Clave2026!" } });
    fireEvent.click(screen.getByRole("button", { name: "Actualizar contraseña" }));

    expect(screen.getByText("La nueva contraseña y su confirmación no coinciden.")).toBeInTheDocument();
    expect(apiFetch).not.toHaveBeenCalled();
  });

  it("submits only password values and clears the fields after success", async () => {
    vi.mocked(apiFetch).mockResolvedValue(undefined);
    render(<ChangePasswordDialog onClose={vi.fn()} />);

    fireEvent.change(screen.getByLabelText("Contraseña actual"), { target: { value: "Actual.Clave2026!" } });
    fireEvent.change(screen.getByLabelText("Nueva contraseña"), { target: { value: "Nueva.Clave2026!" } });
    fireEvent.change(screen.getByLabelText("Confirmar nueva contraseña"), { target: { value: "Nueva.Clave2026!" } });
    fireEvent.click(screen.getByRole("button", { name: "Actualizar contraseña" }));

    await waitFor(() => expect(apiFetch).toHaveBeenCalledWith("/api/auth/change-password", expect.objectContaining({ method: "POST" })));
    expect(screen.getByText("Tu contraseña fue actualizada correctamente.")).toBeInTheDocument();
    expect(screen.getByLabelText("Contraseña actual")).toHaveValue("");
    expect(screen.getByLabelText("Nueva contraseña")).toHaveValue("");
    expect(screen.getByLabelText("Confirmar nueva contraseña")).toHaveValue("");
  });
});
