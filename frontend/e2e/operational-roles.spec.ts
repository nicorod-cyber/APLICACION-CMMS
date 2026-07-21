import { expect, test, type Page } from "@playwright/test";

function password(): string {
  const value = process.env.E2E_SMOKE_PASSWORD;
  if (!value) throw new Error("E2E_SMOKE_PASSWORD must be supplied by the runner.");
  return value;
}

test.beforeEach(async ({ page }) => {
  const response = await page.request.get("/api/system/info");
  if (!response.ok()) throw new Error(`E2E target did not expose /api/system/info (${response.status()}).`);
  const system = (await response.json()) as { environment?: string };
  if (system.environment?.toLowerCase() === "pilot") {
    throw new Error("E2E execution against the Pilot environment is blocked.");
  }
});
async function login(page: Page, username: string) {
  await page.context().clearCookies();
  await page.goto("/login");
  await page.getByLabel("Usuario").fill(username);
  await page.getByLabel("Clave").fill(password());
  await page.getByRole("button", { name: "Entrar" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}

async function expectOperationalView(page: Page) {
  await page.goto("/ot");
  await expect(page.getByRole("heading", { name: "Ordenes de trabajo" })).toBeVisible();
  await expect(page.locator(".error-banner")).toHaveCount(0);
}

test("planning loads OT and preventive operational views", async ({ page }) => {
  await login(page, process.env.E2E_PLANNER_USERNAME ?? "");
  await expectOperationalView(page);
  await page.goto("/preventivos");
  await expect(page.getByRole("heading", { name: "Preventivos" })).toBeVisible();
  await expect(page.locator(".error-banner")).toHaveCount(0);
});

test("supervisor loads assigned work-order view", async ({ page }) => {
  await login(page, process.env.E2E_SUPERVISOR_USERNAME ?? "");
  await expectOperationalView(page);
});

test("planning can open document versions and requirement matrices", async ({ page }) => {
  await login(page, process.env.E2E_PLANNER_USERNAME ?? "");
  await page.goto("/documentos");
  await expect(page.getByRole("heading", { name: "Documentos" })).toBeVisible();
  await page.getByRole("button", { name: "Matriz" }).click();
  await expect(page.getByRole("heading", { name: "Versiones de matriz normativa" })).toBeVisible();
  await expect(page.locator(".error-banner")).toHaveCount(0);
});

test("supervisor cannot use document validation or rejection controls", async ({ page }) => {
  await login(page, process.env.E2E_SUPERVISOR_USERNAME ?? "");
  await page.goto("/documentos");
  await expect(page.getByRole("heading", { name: "Documentos" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Validar" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Rechazar" })).toBeDisabled();
  await expect(page.locator(".error-banner")).toHaveCount(0);
});

test("technician loads the work-order view without a frontend error", async ({ page }) => {
  await login(page, process.env.E2E_TECHNICIAN_USERNAME ?? "");
  await expectOperationalView(page);
});
