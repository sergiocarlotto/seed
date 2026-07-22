import { describe, it, expect } from "vitest";
import { canGrantCompanies, mergePreservingOutOfScope } from "./company-access";

describe("canGrantCompanies", () => {
  it("nega sem companies.grant_access", () => {
    expect(canGrantCompanies({ isOwner: false, permissions: [] })).toBe(false);
  });
  it("permite com companies.grant_access", () => {
    expect(canGrantCompanies({ isOwner: false, permissions: ["companies.grant_access"] })).toBe(true);
  });
  it("permite para o owner (bypass funcional)", () => {
    expect(canGrantCompanies({ isOwner: true, permissions: [] })).toBe(true);
  });
});

describe("mergePreservingOutOfScope", () => {
  const scope = ["a", "b"];

  it("envia apenas o que está no escopo do operador", () => {
    // "z" está no usuário mas fora do escopo: não vai no payload, e o backend
    // o preserva (ADR-0014, regra 2).
    expect(mergePreservingOutOfScope({ selected: ["a", "z"], scope })).toEqual(["a"]);
  });

  it("desmarcar tudo envia lista vazia, sem tocar no que está fora do escopo", () => {
    expect(mergePreservingOutOfScope({ selected: [], scope })).toEqual([]);
  });

  it("preserva a ordem do escopo, sem duplicar", () => {
    expect(mergePreservingOutOfScope({ selected: ["b", "a", "b"], scope })).toEqual(["a", "b"]);
  });
});
