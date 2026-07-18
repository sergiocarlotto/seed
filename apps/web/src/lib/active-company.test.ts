import { describe, it, expect } from "vitest";
import { resolveActiveCompany, ACTIVE_COMPANY_COOKIE } from "./active-company";
import type { Company } from "./types";

function company(id: string, name: string): Company {
  return { id, name, status: "active", createdAt: "", updatedAt: "" };
}

const companies: Company[] = [company("b", "Beta"), company("a", "Alfa")];

describe("resolveActiveCompany", () => {
  it("usa o id do cookie quando ele pertence ao usuário", () => {
    const r = resolveActiveCompany(companies, "b");
    expect(r.active?.id).toBe("b");
    expect(r.corrected).toBe(false);
  });

  it("faz fallback para a primeira empresa (ordenada por nome) quando o cookie é inválido", () => {
    const r = resolveActiveCompany(companies, "inexistente");
    expect(r.active?.id).toBe("a"); // "Alfa" antes de "Beta"
    expect(r.corrected).toBe(true);
  });

  it("faz fallback quando o cookie está ausente", () => {
    const r = resolveActiveCompany(companies, undefined);
    expect(r.active?.id).toBe("a");
    expect(r.corrected).toBe(true);
  });

  it("retorna nulo quando o usuário não tem empresas", () => {
    const r = resolveActiveCompany([], "qualquer");
    expect(r.active).toBeNull();
    expect(r.corrected).toBe(true); // limpa cookie obsoleto
  });

  it("nome do cookie é estável", () => {
    expect(ACTIVE_COMPANY_COOKIE).toBe("active-company");
  });
});
