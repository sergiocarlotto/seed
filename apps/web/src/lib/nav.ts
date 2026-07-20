import { Building2, Settings2, type LucideIcon } from "lucide-react";

export type NavItem = {
  label: string;
  href: string;
  icon: LucideIcon;
  permission?: string; // ausente = visível para todos; presente = exige a chave
};

export type NavModule = {
  label: string;
  icon: LucideIcon;
  items: NavItem[];
};

// Config real de hoje: só o que existe. Perfis e Usuários entram nas Fatias 1 e 2,
// junto das rotas correspondentes.
export const navModules: NavModule[] = [
  {
    label: "Administração",
    icon: Settings2,
    items: [
      { label: "Empresas", href: "/companies", icon: Building2, permission: "companies.access" },
    ],
  },
];

/**
 * Remove itens cuja permissão o usuário não tem; descarta módulos que ficam
 * vazios. `can` é o predicado de UX (ver `lib/access.ts`) — o backend continua a
 * barreira real.
 */
export function visibleNav(
  modules: NavModule[],
  can: (permission: string) => boolean,
): NavModule[] {
  return modules
    .map((m) => ({
      ...m,
      items: m.items.filter((i) => !i.permission || can(i.permission)),
    }))
    .filter((m) => m.items.length > 0);
}
