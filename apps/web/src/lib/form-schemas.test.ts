import { describe, it, expect } from "vitest";
import { loginSchema, companySchema, profileSchema, firstError } from "./form-schemas";

describe("loginSchema", () => {
  it("aceita email e senha preenchidos", () => {
    const r = loginSchema.safeParse({ email: "a@b.local", password: "Passw0rd!" });
    expect(r.success).toBe(true);
  });
  it("rejeita email malformado", () => {
    const r = loginSchema.safeParse({ email: "sem-arroba", password: "Passw0rd!" });
    expect(r.success).toBe(false);
  });
  it("rejeita senha vazia", () => {
    const r = loginSchema.safeParse({ email: "a@b.local", password: "" });
    expect(r.success).toBe(false);
  });
});

describe("companySchema", () => {
  it("apara espaços do nome", () => {
    const r = companySchema.safeParse({ name: "  Acme  " });
    expect(r.success).toBe(true);
    if (r.success) expect(r.data.name).toBe("Acme");
  });
  it("rejeita nome só de espaços", () => {
    expect(companySchema.safeParse({ name: "   " }).success).toBe(false);
  });
  it("rejeita nome acima de 200 caracteres (limite do backend)", () => {
    expect(companySchema.safeParse({ name: "x".repeat(201) }).success).toBe(false);
  });
});

describe("profileSchema", () => {
  it("aceita descrição vazia", () => {
    const r = profileSchema.safeParse({ name: "Financeiro", description: "" });
    expect(r.success).toBe(true);
  });
  it("rejeita nome vazio", () => {
    expect(profileSchema.safeParse({ name: "", description: "x" }).success).toBe(false);
  });
  it("rejeita descrição acima de 500 caracteres (limite do backend)", () => {
    const r = profileSchema.safeParse({ name: "Financeiro", description: "x".repeat(501) });
    expect(r.success).toBe(false);
  });
});

describe("firstError", () => {
  it("devolve a mensagem do primeiro problema", () => {
    const r = companySchema.safeParse({ name: "" });
    expect(r.success).toBe(false);
    if (!r.success) expect(firstError(r.error)).toBe("Informe o nome da empresa.");
  });
});
