import { expect, test, type Page } from "@playwright/test";

const password = () => process.env.E2E_SMOKE_PASSWORD;
const roleUser = (name: string) => process.env[name];
test.beforeEach(async ({ page }) => {
  if (!password()) test.skip(true, "E2E_SMOKE_PASSWORD is not configured.");
  const response = await page.request.get("/api/system/info");
  if (!response.ok()) test.skip(true, `E2E target did not expose /api/system/info (${response.status()}).`);
  const system = (await response.json()) as { environment?: string };
  if (system.environment?.toLowerCase() === "pilot") test.skip(true, "E2E execution against the Pilot environment is blocked.");
});
async function login(page: Page, username: string | undefined) { if (!username) test.skip(true, "The role username is not configured."); await page.context().clearCookies(); await page.goto("/login"); await page.getByLabel("Usuario").fill(username!); await page.getByLabel("Clave").fill(password()!); await page.getByRole("button", { name: "Entrar" }).click(); await expect(page).toHaveURL(/\/dashboard$/); }
async function expectOperationalView(page: Page) { await page.goto("/ot"); await expect(page.getByRole("heading", { name: "Ordenes de trabajo" })).toBeVisible(); await expect(page.locator(".error-banner")).toHaveCount(0); }
test("planning loads OT and preventive operational views", async ({ page }) => { await login(page, roleUser("E2E_PLANNER_USERNAME")); await expectOperationalView(page); await page.goto("/preventivos"); await expect(page.getByRole("heading", { name: "Preventivos" })).toBeVisible(); });
test("supervisor loads assigned work-order view", async ({ page }) => { await login(page, roleUser("E2E_SUPERVISOR_USERNAME")); await expectOperationalView(page); });
test("planning can open document versions and requirement matrices", async ({ page }) => { await login(page, roleUser("E2E_PLANNER_USERNAME")); await page.goto("/documentos"); await expect(page.getByRole("heading", { name: "Documentos" })).toBeVisible(); await page.getByRole("button", { name: "Matriz" }).click(); await expect(page.getByRole("heading", { name: "Versiones de matriz normativa" })).toBeVisible(); });
test("supervisor cannot use document validation or rejection controls", async ({ page }) => { await login(page, roleUser("E2E_SUPERVISOR_USERNAME")); await page.goto("/documentos"); await expect(page.getByRole("button", { name: "Validar" })).toBeDisabled(); await expect(page.getByRole("button", { name: "Rechazar" })).toBeDisabled(); });
test("technician loads the work-order view without a frontend error", async ({ page }) => { await login(page, roleUser("E2E_TECHNICIAN_USERNAME")); await expectOperationalView(page); });
test("asset state event uses the canonical equipment detail route", async ({ page }) => { await login(page, roleUser("E2E_PLANNER_USERNAME")); const code = process.env.E2E_ASSET_CODE; if (!code) test.skip(true, "E2E_ASSET_CODE is not configured."); await page.goto("/equipos/activos/" + encodeURIComponent(code!)); await expect(page.getByRole("heading", { name: "Acciones del activo" })).toBeVisible(); await page.getByRole("button", { name: "Registrar estado" }).click(); await expect(page.getByText("Evento de estado")).toBeVisible(); await expect(page.getByLabel("Origen del cambio")).toBeVisible(); await expect(page.getByRole("button", { name: "Registrar evento" })).toBeDisabled(); });