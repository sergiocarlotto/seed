import { defineConfig } from "vitest/config";
import { resolve } from "node:path";

// Ambiente "node": os testes cobrem lógica pura (sem DOM). Fluxos de UI ficam no
// Playwright. O alias "@" espelha o tsconfig para os imports "@/lib/...".
export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts"],
  },
  resolve: {
    alias: { "@": resolve(__dirname, "./src") },
  },
});
