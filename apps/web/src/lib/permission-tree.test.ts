import { describe, it, expect } from "vitest";
import { moduleState, toggleModule, togglePermission } from "./permission-tree";
import type { PermissionGroup } from "./types";

const group: PermissionGroup = {
  module: "companies",
  permissions: [
    { key: "companies.access", displayName: "Acessar", description: "" },
    { key: "companies.manage", displayName: "Gerenciar", description: "" },
  ],
};

describe("moduleState", () => {
  it("unchecked quando nenhuma permissão do módulo está selecionada", () => {
    expect(moduleState(group, new Set())).toBe("unchecked");
  });
  it("checked quando todas estão selecionadas", () => {
    expect(moduleState(group, new Set(["companies.access", "companies.manage"]))).toBe("checked");
  });
  it("indeterminate quando algumas estão selecionadas", () => {
    expect(moduleState(group, new Set(["companies.access"]))).toBe("indeterminate");
  });
});

describe("toggleModule", () => {
  it("marca todas quando estava parcial", () => {
    const next = toggleModule(group, new Set(["companies.access"]));
    expect(next).toEqual(new Set(["companies.access", "companies.manage"]));
  });
  it("desmarca todas quando estava completo", () => {
    const next = toggleModule(group, new Set(["companies.access", "companies.manage"]));
    expect(next).toEqual(new Set());
  });
  it("não muta o conjunto original", () => {
    const original = new Set(["companies.access"]);
    toggleModule(group, original);
    expect(original).toEqual(new Set(["companies.access"]));
  });
});

describe("togglePermission", () => {
  it("adiciona quando ausente", () => {
    expect(togglePermission("companies.manage", new Set(["companies.access"]))).toEqual(
      new Set(["companies.access", "companies.manage"])
    );
  });
  it("remove quando presente", () => {
    expect(togglePermission("companies.access", new Set(["companies.access"]))).toEqual(new Set());
  });
});
