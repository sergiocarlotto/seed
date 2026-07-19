import { describe, it, expect } from "vitest";
import { visibleNav, type NavModule } from "./nav";
import { Building2, Users } from "lucide-react";

const fixture: NavModule[] = [
  {
    label: "Administração",
    icon: Building2,
    items: [
      { label: "Empresas", href: "/companies", icon: Building2 },
      { label: "Usuários", href: "/users", icon: Users, roles: ["Admin"] },
    ],
  },
  {
    label: "Somente Admin",
    icon: Building2,
    items: [{ label: "Config", href: "/config", icon: Building2, roles: ["Admin"] }],
  },
];

describe("visibleNav", () => {
  it("mantém itens sem `roles` para qualquer papel", () => {
    const result = visibleNav(fixture, "Member");
    expect(result[0].items.map((i) => i.href)).toEqual(["/companies"]);
  });

  it("mostra itens restritos ao papel correspondente", () => {
    const result = visibleNav(fixture, "Admin");
    expect(result[0].items.map((i) => i.href)).toEqual(["/companies", "/users"]);
  });

  it("remove módulos que ficam sem itens visíveis", () => {
    const result = visibleNav(fixture, "Member");
    expect(result.map((m) => m.label)).toEqual(["Administração"]);
  });

  it("não muta a config original", () => {
    const before = fixture[0].items.length;
    visibleNav(fixture, "Member");
    expect(fixture[0].items.length).toBe(before);
  });
});
