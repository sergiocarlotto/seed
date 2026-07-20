import { describe, it, expect } from "vitest";
import { can } from "./access";

describe("can", () => {
  it("libera quando a permissão está na lista", () => {
    expect(can({ isOwner: false, permissions: ["companies.access"] }, "companies.access")).toBe(true);
  });

  it("nega quando a permissão não está na lista", () => {
    expect(can({ isOwner: false, permissions: ["companies.access"] }, "profiles.manage")).toBe(false);
  });

  it("nega para lista vazia (usuário sem perfil)", () => {
    expect(can({ isOwner: false, permissions: [] }, "companies.access")).toBe(false);
  });

  it("owner tem bypass de qualquer permissão", () => {
    expect(can({ isOwner: true, permissions: [] }, "profiles.manage")).toBe(true);
  });
});
