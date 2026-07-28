import { expect, test, type Page } from "@playwright/test";

const password = process.env.E2E_SMOKE_PASSWORD;
const admin = process.env.E2E_ADMIN_USERNAME;
const viewer = process.env.E2E_VIEWER_USERNAME;
const assetCode = process.env.E2E_ASSET_CODE;
const unitCode = process.env.E2E_UNIT_CODE;

function requireValue(value: string | undefined, name: string) {
  if (!value) test.skip(true, `${name} must be supplied by the runner.`);
  return value!;
}

async function login(page: Page, username = admin) {
  const resolvedUsername = requireValue(username, "E2E_ADMIN_USERNAME");
  const resolvedPassword = requireValue(password, "E2E_SMOKE_PASSWORD");
  await page.goto("/login");
  await page.getByLabel("Usuario").fill(resolvedUsername);
  await page.getByLabel("Clave").fill(resolvedPassword);
  await page.getByRole("button", { name: "Entrar" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
}

async function openAsset(page: Page) {
  await login(page);
  await page.goto(`/equipos/activos/${encodeURIComponent(requireValue(assetCode, "E2E_ASSET_CODE"))}`);
  await expect(page.getByRole("heading")).toBeVisible();
}

async function openUnit(page: Page) {
  await login(page);
  await page.goto(`/equipos/unidades/${encodeURIComponent(requireValue(unitCode, "E2E_UNIT_CODE"))}`);
  await expect(page.getByRole("heading")).toBeVisible();
}

test.beforeEach(async ({ page }) => {
  const response = await page.request.get("/api/system/info");
  if (!response.ok()) test.skip(true, `E2E target did not expose /api/system/info (${response.status()}).`);
  const system = (await response.json()) as { environment?: string };
  if (system.environment?.toLowerCase() === "pilot") test.skip(true, "E2E execution against Pilot is blocked.");
});

test("opens the unified equipment list", async ({ page }) => {
  await login(page);
  await page.goto("/equipos");
  await expect(page.getByRole("heading", { name: "Equipos" })).toBeVisible();
});

test("opens asset detail and shared editor", async ({ page }) => {
  await openAsset(page);
  await page.getByRole("button", { name: "Editar ficha" }).click();
  await expect(page.getByRole("dialog", { name: /Editar activo/ })).toBeVisible();
});

test("opens shared reading flow from asset detail", async ({ page }) => {
  await openAsset(page);
  await page.getByRole("button", { name: "Registrar lectura" }).click();
  await expect(page.getByRole("dialog", { name: "Registrar lectura" })).toBeVisible();
});

test("opens state event flow with auditable antecedents", async ({ page }) => {
  await openAsset(page);
  await page.getByRole("button", { name: "Registrar estado" }).click();
  await expect(page.getByRole("dialog", { name: "Registrar estado" })).toBeVisible();
});

test("opens asset movement flow", async ({ page }) => {
  await openAsset(page);
  await page.getByRole("button", { name: "Trasladar, ingresar o retornar" }).click();
  await expect(page.getByRole("dialog", { name: "Movimiento de activo" })).toBeVisible();
});

test("opens document management and versions", async ({ page }) => {
  await login(page);
  await page.goto("/documentos");
  await expect(page.getByRole("heading", { name: "Documentos" })).toBeVisible();
  await expect(page.getByText(/Gestion del activo/)).toBeVisible();
});

test("opens composite-unit detail and shared editor", async ({ page }) => {
  await openUnit(page);
  await page.getByRole("button", { name: "Editar identificacion" }).click();
  await expect(page.getByRole("dialog", { name: /Editar identific/ })).toBeVisible();
});

test("opens transactional unit transfer with affected components", async ({ page }) => {
  await openUnit(page);
  await page.getByRole("button", { name: "Trasladar unidad completa" }).click();
  await expect(page.getByRole("dialog", { name: "Trasladar unidad completa" })).toBeVisible();
});

test("opens unit composition flow", async ({ page }) => {
  await openUnit(page);
  await page.getByRole("button", { name: "Montar componente" }).first().click();
  await expect(page.getByRole("dialog", { name: "Montar componente" })).toBeVisible();
});

test("preserves canonical notice and work-order targets from unit", async ({ page }) => {
  await openUnit(page);
  await page.getByRole("button", { name: "Crear aviso" }).click();
  await expect(page).toHaveURL(/\/avisos\?targetType=OperationalUnit&targetCode=/);
  await page.goBack();
  await page.getByRole("button", { name: "Crear OT" }).click();
  await expect(page).toHaveURL(/\/ot\?targetType=OperationalUnit&targetCode=/);
});

test("viewer cannot open restricted unit mutations", async ({ page }) => {
  const username = requireValue(viewer, "E2E_VIEWER_USERNAME");
  await login(page, username);
  await page.goto(`/equipos/unidades/${encodeURIComponent(requireValue(unitCode, "E2E_UNIT_CODE"))}`);
  await expect(page.getByRole("button", { name: "Editar identificacion" })).toBeDisabled();
  await expect(page.getByRole("button", { name: "Trasladar unidad completa" })).toBeDisabled();
});

test("does not redirect legacy operations after opening unified details", async ({ page }) => {
  await openUnit(page);
  await expect(page).not.toHaveURL(/\/(activos|documentos|unidades-operativas)$/);
});