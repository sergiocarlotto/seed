import { describe, it, expect } from "vitest";
import { loginSchema, companySchema, profileSchema, userSchema, firstError } from "./form-schemas";

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

describe("userSchema", () => {
  const valid = {
    fullName: "  Maria Silva  ",
    email: "maria@demo.local",
    password: "Passw0rd!",
    confirm: "Passw0rd!",
  };

  it("aceita o caso válido e apara espaços do nome", () => {
    const r = userSchema.safeParse(valid);
    expect(r.success).toBe(true);
    if (r.success) expect(r.data.fullName).toBe("Maria Silva");
  });

  it("rejeita confirmação diferente da senha", () => {
    const r = userSchema.safeParse({ ...valid, confirm: "Outr0Passw!" });
    expect(r.success).toBe(false);
    // O erro aponta para o campo de confirmação, que é onde o usuário corrige.
    if (!r.success) expect(r.error.issues[0]?.path).toEqual(["confirm"]);
  });

  it("rejeita senha com menos de 8 caracteres", () => {
    const r = userSchema.safeParse({ ...valid, password: "Ab1!def", confirm: "Ab1!def" });
    expect(r.success).toBe(false);
  });

  it("rejeita nome só de espaços", () => {
    expect(userSchema.safeParse({ ...valid, fullName: "   " }).success).toBe(false);
  });

  it("rejeita nome acima de 200 caracteres (limite do backend)", () => {
    expect(userSchema.safeParse({ ...valid, fullName: "x".repeat(201) }).success).toBe(false);
  });

  it("rejeita email malformado", () => {
    expect(userSchema.safeParse({ ...valid, email: "sem-arroba" }).success).toBe(false);
  });
});

describe("firstError", () => {
  it("devolve a mensagem do primeiro problema", () => {
    const r = companySchema.safeParse({ name: "" });
    expect(r.success).toBe(false);
    if (!r.success) expect(firstError(r.error)).toBe("Informe o nome da empresa.");
  });
});
