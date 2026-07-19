import { test, expect, type Page } from "@playwright/test";

// Usuário de seed de desenvolvimento (Admin, com 2 empresas). Ajuste via env se preciso.
const EMAIL = process.env.E2E_EMAIL ?? "admin@demo.local";
const PASSWORD = process.env.E2E_PASSWORD ?? "Admin123!";

async function login(page: Page) {
  await page.goto("/login");
  await page.getByLabel("Email").fill(EMAIL);
  await page.getByLabel("Senha").fill(PASSWORD);
  await page.getByRole("button", { name: "Entrar" }).click();
  await page.waitForURL("**/companies");
}

test.describe("app shell", () => {
  test("login fica fora do shell", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByTestId("desktop-sidebar")).toHaveCount(0);
    await expect(page.getByTestId("menu-toggle")).toHaveCount(0);
  });

  test("/companies renderiza dentro do shell", async ({ page }) => {
    await login(page);
    await expect(page.getByTestId("desktop-sidebar")).toBeVisible();
    await expect(page.getByTestId("menu-toggle")).toBeVisible();
    await expect(page.getByTestId("page-title")).toHaveText("Empresas");
  });

  test("item de menu ativo é destacado", async ({ page }) => {
    await login(page);
    const item = page.getByTestId("nav-/companies");
    await expect(item).toHaveAttribute("aria-current", "page");
  });

  test("busca aparece desabilitada no desktop", async ({ page }) => {
    await login(page);
    await expect(page.getByPlaceholder("Pesquisar no sistema")).toBeDisabled();
  });

  test("menu do usuário faz logout e volta ao login", async ({ page }) => {
    await login(page);
    await page.getByTestId("user-menu").click();
    await page.getByTestId("logout").click();
    await page.waitForURL("**/login");
    await expect(page.locator('[data-slot="card-title"]')).toHaveText("Entrar");
  });

  test("mobile mostra gaveta em vez de sidebar fixa", async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await login(page);
    await expect(page.getByTestId("desktop-sidebar")).toBeHidden();
    await page.getByTestId("menu-toggle").click();
    const drawer = page.getByTestId("mobile-drawer");
    await expect(drawer).toBeVisible();
    await drawer.getByTestId("nav-/companies").click();
    await expect(drawer).toBeHidden();
  });

  test("seletor de empresa troca a ativa", async ({ page }) => {
    await login(page);
    await page.getByTestId("company-switcher").click();
    const options = page.locator('[data-testid^="company-option-"]');
    await expect(options).toHaveCount(2);
    const firstName = (await options.first().textContent())?.trim();
    await options.nth(1).click();
    // Após router.refresh(), o seletor mostra a outra empresa.
    await expect(page.getByTestId("company-switcher")).not.toHaveText(firstName ?? "");
  });
});
