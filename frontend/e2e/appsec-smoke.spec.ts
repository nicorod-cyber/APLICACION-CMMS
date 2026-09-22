import { expect, test } from "@playwright/test";

test("security headers and authenticated navigation work together", async ({ page }) => {
  const username = process.env.E2E_ADMIN_USERNAME;
  const password = process.env.E2E_SMOKE_PASSWORD;
  test.skip(!username || !password, "Synthetic E2E credentials are required.");
  const system = await page.request.get("/api/system/info");
  expect(system.ok()).toBeTruthy();
  test.skip((await system.json()).environment?.toLowerCase() === "pilot", "Do not run against Pilot.");
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  page.on("console", message => {
    if (/violates.*Content Security Policy|Refused to.*Content Security Policy/i.test(message.text())) errors.push(message.text());
  });
  const response = await page.goto("/login");
  expect(response?.headers()["x-content-type-options"]).toBe("nosniff");
  expect(response?.headers()["content-security-policy"]).toContain("frame-ancestors 'none'");
  await page.getByLabel("Usuario").fill(username!);
  await page.getByLabel("Clave").fill(password!);
  await page.getByRole("button", { name: "Entrar" }).click();
  await expect(page).toHaveURL(/\/dashboard$/);
  for (const route of ["/equipos", "/faenas", "/documentos", "/avisos", "/ot", "/preventivos", "/programacion", "/administracion"]) {
    await page.goto(route);
    await expect(page).toHaveURL(new RegExp(route === "/programacion" ? "/programacion/calendario(?:\\?|$)" : `${route}$`));
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await expect(page.locator(".error-banner")).toHaveCount(0);
  }
  expect(errors).toEqual([]);
});
