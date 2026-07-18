import { Building2, Settings2, type LucideIcon } from "lucide-react";

export type OrgRole = "Admin" | "Member";

export type NavItem = {
  label: string;
  href: string;
  icon: LucideIcon;
  roles?: OrgRole[]; // ausente = visível para todos os papéis
};

export type NavModule = {
  label: string;
  icon: LucideIcon;
  items: NavItem[];
};

// Config real de hoje: só o que existe. Novos itens/módulos são acréscimos aqui.
export const navModules: NavModule[] = [
  {
    label: "Administração",
    icon: Settings2,
    items: [{ label: "Empresas", href: "/companies", icon: Building2 }],
  },
];

/** Remove itens que o papel não pode ver; descarta módulos que ficam vazios. */
export function visibleNav(modules: NavModule[], role: OrgRole): NavModule[] {
  return modules
    .map((m) => ({
      ...m,
      items: m.items.filter((i) => !i.roles || i.roles.includes(role)),
    }))
    .filter((m) => m.items.length > 0);
}
