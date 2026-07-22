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
    // O caminho para o detalhe é a ação "Ver" da linha (o e-mail não é link).
    await page.getByRole("row").filter({ hasText: EMAIL }).getByRole("link", { name: "Ver" }).click();
    await page.waitForURL("**/users/**");
    // O owner é somente leitura: switch de status desabilitado.
    await expect(page.getByTestId("user-status")).toBeDisabled();
  });

  test("cria usuário e concede acesso a uma empresa", async ({ page }) => {
    await login(page);

    // E-mail único por execução: o banco de dev sobrevive entre rodadas.
    const stamp = Date.now();
    const email = `e2e.novo.${stamp}@demo.local`;

    await page.goto("/users/new");
    await page.getByLabel("Nome completo").fill("Usuário E2E");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Senha inicial").fill("Passw0rd!");
    await page.getByLabel("Confirmar senha").fill("Passw0rd!");
    await page.getByTestId("save-user").click();

    // Redireciona para o detalhe do usuário recém-criado.
    await page.waitForURL("**/users/**");
    await expect(page.getByTestId("page-title")).toHaveText("Usuário E2E");

    // Nasce sem empresa: nenhum checkbox marcado no card de empresas.
    const companies = page.getByTestId("user-companies");
    await expect(companies).toBeVisible();
    await expect(companies.getByRole("checkbox", { checked: true })).toHaveCount(0);

    // Concede a primeira empresa do escopo e salva.
    await companies.getByRole("checkbox").first().check();
    await page.getByTestId("save-companies").click();
    await expect(page.getByText("Empresas atualizadas.")).toBeVisible();

    // Persistiu: ao recarregar, o checkbox continua marcado.
    await page.reload();
    await expect(
      page.getByTestId("user-companies").getByRole("checkbox", { checked: true }),
    ).toHaveCount(1);
  });
});
