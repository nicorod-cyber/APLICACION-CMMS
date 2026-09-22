import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, expect, it, vi } from "vitest";
import { DocumentLink } from "./DocumentLink";
import { useAuthStore } from "../../features/auth/authStore";

afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); useAuthStore.getState().clearSession(); });

it("never attaches the bearer token to external document hosts", () => {
  const fetch = vi.fn(); vi.stubGlobal("fetch", fetch);
  render(<DocumentLink href="https://tenant.sharepoint.com/file.pdf">Open</DocumentLink>);
  expect(screen.getByRole("link")).toHaveAttribute("href", "https://tenant.sharepoint.com/file.pdf");
  expect(screen.getByRole("link")).toHaveAttribute("rel", "noopener noreferrer");
  expect(fetch).not.toHaveBeenCalled();
});

it("uses bearer only on the local endpoint and refuses HTTP redirects", async () => {
  useAuthStore.setState({ token: "test-only-token" });
  const fetch = vi.fn().mockResolvedValue({ ok: false }); vi.stubGlobal("fetch", fetch);
  render(<DocumentLink href="/api/sharepoint/download?fileKey=test.pdf">Open</DocumentLink>);
  fireEvent.click(screen.getByRole("link"));
  await waitFor(() => expect(fetch).toHaveBeenCalledWith("/api/sharepoint/download?fileKey=test.pdf", { headers: { Authorization: "Bearer test-only-token" }, redirect: "error" }));
  expect(await screen.findByRole("alert")).toHaveTextContent("No fue posible acceder");
});

it("does not render a link to an executable scheme", () => {
  render(<DocumentLink href="javascript:alert(1)">Open</DocumentLink>);
  expect(screen.queryByRole("link")).toBeNull();
});
