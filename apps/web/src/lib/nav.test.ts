import { describe, it, expect } from "vitest";
import { visibleNav, type NavModule } from "./nav";
import { Building2, Users } from "lucide-react";

const fixture: NavModule[] = [
  {
    label: "Administração",
    icon: Building2,
    items: [
      { label: "Empresas", href: "/companies", icon: Building2 }, // sem permission: sempre visível
      { label: "Usuários", href: "/users", icon: Users, permission: "users.manage" },
    ],
  },
  {
    label: "Só com permissão",
    icon: Building2,
    items: [{ label: "Config", href: "/config", icon: Building2, permission: "config.manage" }],
  },
];

describe("visibleNav", () => {
  it("mantém itens sem `permission` para qualquer usuário", () => {
    const result = visibleNav(fixture, () => false);
    expect(result[0].items.map((i) => i.href)).toEqual(["/companies"]);
  });

  it("mostra itens cuja permissão o usuário tem", () => {
    const result = visibleNav(fixture, (key) => key === "users.manage");
    expect(result[0].items.map((i) => i.href)).toEqual(["/companies", "/users"]);
  });

  it("remove módulos que ficam sem itens visíveis", () => {
    const result = visibleNav(fixture, () => false);
    expect(result.map((m) => m.label)).toEqual(["Administração"]);
  });

  it("não muta a config original", () => {
    const before = fixture[0].items.length;
    visibleNav(fixture, () => true);
    expect(fixture[0].items.length).toBe(before);
  });
});
