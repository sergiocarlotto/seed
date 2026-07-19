import { test, expect } from "@playwright/test";

test("a página de login carrega", async ({ page }) => {
  await page.goto("/login");
  await expect(page.locator('[data-slot="card-title"]')).toHaveText("Entrar");
});
