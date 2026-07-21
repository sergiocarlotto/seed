import { test, expect, type Page } from "@playwright/test";

const EMAIL = process.env.E2E_EMAIL ?? "admin@demo.local";
const PASSWORD = process.env.E2E_PASSWORD ?? "Admin123!";

async function login(page: Page) {
  await page.goto("/login");
  await page.getByLabel("Email").fill(EMAIL);
  await page.getByLabel("Senha").fill(PASSWORD);
  await page.getByRole("button", { name: "Entrar" }).click();
  await page.waitForURL("**/companies");
}

test.describe("usuários", () => {
  test("lista de usuários mostra o owner logado", async ({ page }) => {
    await login(page);
    await page.goto("/users");
    await expect(page.getByTestId("page-title")).toHaveText("Usuários");
    await expect(page.getByText(EMAIL)).toBeVisible();
    await expect(page.getByText("Owner").first()).toBeVisible();
  });

  test("abre o detalhe e vê o switch de status desabilitado para o owner", async ({ page }) => {
    await login(page);
    await page.goto("/users");
    await page.getByText(EMAIL).click();
    await page.waitForURL("**/users/**");
    // O owner é somente leitura: switch de status desabilitado.
    await expect(page.getByTestId("user-status")).toBeDisabled();
  });
});
