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

test.describe("perfis", () => {
  test("lista de perfis mostra o perfil de sistema", async ({ page }) => {
    await login(page);
    await page.goto("/profiles");
    await expect(page.getByTestId("page-title")).toHaveText("Perfis");
    await expect(page.getByText("Administrador")).toBeVisible();
    // `exact` evita casar com a descrição do perfil ("Perfil de sistema com...").
    await expect(page.getByText("Sistema", { exact: true })).toBeVisible();
  });

  test("cria um perfil com uma permissão", async ({ page }) => {
    await login(page);
    await page.goto("/profiles");
    await page.getByTestId("new-profile").click();
    await page.waitForURL("**/profiles/new");

    const nome = `Perfil E2E ${Date.now()}`;
    await page.getByTestId("profile-name").fill(nome);
    // marca a primeira permissão da árvore
    await page.getByTestId("permission-tree").getByRole("checkbox").nth(1).click();
    await page.getByTestId("profile-submit").click();

    await page.waitForURL("**/profiles");
    await expect(page.getByText(nome)).toBeVisible();
  });
});
