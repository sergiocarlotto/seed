# Fatia 0 — Re-sync da Sessão (Frontend Controle de Acesso) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-sincronizar o frontend (`apps/web`) com o backend da ADR-0012, trocando o modelo antigo de papel fixo (`orgRole`) pelo novo (`isOwner` + `permissions[]`) e passando o gating de UX a ser por permissão.

**Architecture:** Uma função pura `can(subject, key)` (owner tem bypass, espelhando o backend) centraliza a checagem de UX. O tipo `Me` da sessão passa a expor `isOwner` e `permissions`; o menu (`nav.ts`/`visibleNav`) filtra itens por `permission` em vez de `roles`. Os consumidores (sidebar, menu do usuário, tela de Empresas) passam a usar `can`. Nenhuma tela nova aqui — só a fundação que destrava as Fatias 1 (Perfis) e 2 (Usuários).

**Tech Stack:** TypeScript, React 19, Next.js 16, Vitest, shadcn/ui.

**Referência:** `docs/specs/2026-07-20-access-control-frontend-perfis-usuarios-design.md` (Fatia 0). Contrato do `/auth/me` confirmado em `apps/api/src/Seed.Api/Controllers/AuthController.cs`: `{ user{id,email,fullName}, organizationId, isOwner, permissions, companies }` (camelCase).

**Como rodar os testes de frontend (host):** os testes de `apps/web` rodam em Node/Vitest no host (o bloqueio do Smart App Control é só para `dotnet`). Comandos a partir da raiz do repositório:
- Unit: `npm --prefix apps/web run test`
- Lint: `npm --prefix apps/web run lint`
- Build/typecheck: `npm --prefix apps/web run build`

---

## Arquivos desta fatia

- **Criar** `apps/web/src/lib/access.ts` — função pura `can(subject, key)`.
- **Criar** `apps/web/src/lib/access.test.ts` — testes de `can`.
- **Modificar** `apps/web/src/lib/types.ts` — `Me` ganha `isOwner`/`permissions`, perde `orgRole`.
- **Modificar** `apps/web/src/lib/nav.ts` — `NavItem.permission` no lugar de `roles`; `visibleNav` recebe predicado `can`; remove o tipo `OrgRole`; Empresas gateada por `companies.access`.
- **Modificar** `apps/web/src/lib/nav.test.ts` — reescrito para gating por permissão.
- **Modificar** `apps/web/src/components/shell/AppSidebar.tsx` — usa `can` + novo `visibleNav`.
- **Modificar** `apps/web/src/components/shell/UserMenu.tsx` — substitui a linha `orgRole` por "Dono" quando `isOwner`.
- **Modificar** `apps/web/src/app/(app)/companies/page.tsx` — `isAdmin` (via `orgRole`) → `can(me, "companies.manage")`.

`apps/web/src/lib/session.tsx` **não muda** (continua entregando o `Me`); os consumidores chamam `can(me, key)`.

---

## Task 1: Tipo `Me` novo + função pura `can()`

**Files:**
- Create: `apps/web/src/lib/access.ts`
- Create: `apps/web/src/lib/access.test.ts`
- Modify: `apps/web/src/lib/types.ts`

- [ ] **Step 1: Escrever o teste que falha**

Criar `apps/web/src/lib/access.test.ts`:

```ts
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
```

- [ ] **Step 2: Rodar o teste e ver falhar**

Run: `npm --prefix apps/web run test`
Expected: FAIL — `Cannot find module './access'` (o arquivo ainda não existe).

- [ ] **Step 3: Criar `access.ts`**

Criar `apps/web/src/lib/access.ts`:

```ts
// Sujeito mínimo para a checagem de acesso de UX. Estrutural de propósito, para
// não acoplar a `Me` (evita import circular) e facilitar o teste.
export type AccessSubject = { isOwner: boolean; permissions: string[] };

/**
 * Espelho de UX da autorização do backend: o owner tem bypass funcional
 * completo; os demais precisam ter a permissão na lista efetiva. NUNCA é a
 * barreira real — o backend é. Serve só para esconder menus/ações.
 */
export function can(subject: AccessSubject, key: string): boolean {
  return subject.isOwner || subject.permissions.includes(key);
}
```

- [ ] **Step 4: Atualizar o tipo `Me`**

Modificar `apps/web/src/lib/types.ts` — trocar o bloco `Me` (a definição atual tem `orgRole: string`) por:

```ts
export type Me = {
  user: User;
  organizationId: string;
  isOwner: boolean;
  permissions: string[];
  companies: Company[];
};
```

(Os tipos `User` e `Company` do arquivo permanecem inalterados.)

- [ ] **Step 5: Rodar o teste e ver passar**

Run: `npm --prefix apps/web run test`
Expected: PASS nos 4 casos de `access.test.ts` (e os demais testes do projeto continuam passando; `nav.test.ts` ainda é o antigo — será reescrito na Task 2).

Obs.: se `nav.test.ts` já quebrar aqui por causa da mudança de tipo, tudo bem — ele é reescrito na Task 2. O objetivo desta task é `access.test.ts` verde.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/access.ts apps/web/src/lib/access.test.ts apps/web/src/lib/types.ts
git commit -m "feat(web): modelo de sessao por permissao (Me.isOwner+permissions) e helper can()"
```

---

## Task 2: `nav.ts` gateado por permissão

**Files:**
- Modify: `apps/web/src/lib/nav.ts`
- Modify: `apps/web/src/lib/nav.test.ts`

- [ ] **Step 1: Reescrever o teste (falha contra a assinatura nova)**

Substituir todo o conteúdo de `apps/web/src/lib/nav.test.ts` por:

```ts
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
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `npm --prefix apps/web run test`
Expected: FAIL — `nav.test.ts` não compila/erra porque `visibleNav` ainda tem a assinatura antiga (`(modules, role)`) e `NavItem` ainda usa `roles`.

- [ ] **Step 3: Reescrever `nav.ts`**

Substituir todo o conteúdo de `apps/web/src/lib/nav.ts` por:

```ts
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
```

- [ ] **Step 4: Rodar e ver passar**

Run: `npm --prefix apps/web run test`
Expected: PASS em `nav.test.ts` e `access.test.ts`. (`AppSidebar.tsx` ainda usa a API antiga — será corrigido na Task 3; testes unit não o exercitam, mas o `build`/`lint` da Task 4 vai. Não rode `build` ainda.)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/nav.ts apps/web/src/lib/nav.test.ts
git commit -m "feat(web): menu gateado por permissao (NavItem.permission + visibleNav(can))"
```

---

## Task 3: Consumidores (sidebar, menu do usuário, Empresas)

Componentes cliente; sem teste unit (a lógica pura já está coberta em `can`/`visibleNav`). A rede de segurança é o `build` (typecheck) e o `lint` da Task 4, além dos e2e existentes.

**Files:**
- Modify: `apps/web/src/components/shell/AppSidebar.tsx`
- Modify: `apps/web/src/components/shell/UserMenu.tsx`
- Modify: `apps/web/src/app/(app)/companies/page.tsx`

- [ ] **Step 1: `AppSidebar.tsx` — usar `can` + novo `visibleNav`**

No topo do arquivo, trocar o import de `nav` (que hoje importa `type OrgRole`):

```ts
import { navModules, visibleNav } from "@/lib/nav";
```

Adicionar o import de `can`:

```ts
import { can } from "@/lib/access";
```

No corpo do componente, substituir as duas linhas atuais:

```ts
  const { orgRole } = useSession();
  const modules = visibleNav(navModules, orgRole as OrgRole);
```

por:

```ts
  const me = useSession();
  const modules = visibleNav(navModules, (key) => can(me, key));
```

- [ ] **Step 2: `UserMenu.tsx` — trocar a linha de papel por "Dono"**

Substituir a desestruturação atual:

```ts
  const { user, orgRole } = useSession();
```

por:

```ts
  const { user, isOwner } = useSession();
```

E substituir a linha que renderiza `{orgRole}`:

```tsx
              <span className="text-xs font-normal text-muted-foreground">{orgRole}</span>
```

por (mostra "Dono" só para o owner; membros comuns não têm mais um papel único a exibir):

```tsx
              {isOwner && (
                <span className="text-xs font-normal text-muted-foreground">Dono</span>
              )}
```

- [ ] **Step 3: `companies/page.tsx` — gating por `companies.manage`**

Adicionar o import de `can` junto aos imports de `@/lib`:

```ts
import { can } from "@/lib/access";
```

Substituir as duas linhas atuais:

```ts
  const { companies: sessionCompanies, orgRole } = useSession();
  const isAdmin = orgRole === "Admin";
```

por:

```ts
  const me = useSession();
  const { companies: sessionCompanies } = me;
  const isAdmin = can(me, "companies.manage");
```

(O restante da página — que usa `isAdmin` para mostrar "Nova empresa", a coluna Ações e os botões Editar/Excluir — permanece igual; agora o gate reflete `companies.manage`.)

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/shell/AppSidebar.tsx apps/web/src/components/shell/UserMenu.tsx apps/web/src/app/\(app\)/companies/page.tsx
git commit -m "refactor(web): sidebar, menu do usuario e Empresas usam can()/permissoes"
```

---

## Task 4: Verificação final (unit + lint + build)

Sem mudanças de código — só confirma que a fatia está íntegra e não sobrou referência ao modelo antigo.

- [ ] **Step 1: Garantir que não restou `orgRole`/`OrgRole` no `src`**

Run (a partir da raiz do repo): `grep -rn "orgRole\|OrgRole" apps/web/src`
Expected: nenhuma ocorrência (saída vazia).

- [ ] **Step 2: Unit tests verdes**

Run: `npm --prefix apps/web run test`
Expected: PASS — inclui `access.test.ts` (4) e `nav.test.ts` (4), além dos demais (`active-company.test.ts`, `smoke.test.ts`) que não foram tocados.

- [ ] **Step 3: Lint limpo**

Run: `npm --prefix apps/web run lint`
Expected: sem erros.

- [ ] **Step 4: Build/typecheck**

Run: `npm --prefix apps/web run build`
Expected: build conclui sem erros de tipo (confirma que os consumidores da Task 3 compilam com o novo `Me`/`visibleNav`).

- [ ] **Step 5: (Opcional) e2e de fumaça**

Se houver stack rodando (`docker compose up` com api+db) e o seed `admin@demo.local` disponível, rodar:
Run: `npm --prefix apps/web run e2e`
Expected: os specs de `e2e/shell.spec.ts` continuam verdes (login, sidebar, menu do usuário, logout, troca de empresa). O usuário de seed agora é owner, então o menu Empresas segue visível. Se não houver stack, pular este passo — não bloqueia a fatia.

---

## Self-Review (preenchido pelo autor do plano)

- **Cobertura do spec (Fatia 0):** `Me` novo (Task 1) ✓; helper `can` (Task 1) ✓; `nav.ts` por permissão + itens de Administração (Task 2, Empresas gateada; Perfis/Usuários ficam para as Fatias 1/2 junto das rotas) ✓; `AppSidebar` (Task 3) ✓; `companies/page` `isAdmin`→`companies.manage` (Task 3) ✓; `UserMenu` exibindo `orgRole` tratado (Task 3) ✓; testes unit de `visibleNav` e do gating (Tasks 1–2) ✓.
- **Placeholders:** nenhum — todo passo traz o código real.
- **Consistência de tipos:** `can(subject, key)` e `visibleNav(modules, can)` usados com a mesma assinatura em todas as tasks; `Me` com `isOwner`/`permissions` consumido por `AppSidebar`, `UserMenu` e `companies/page`.
- **Nota:** `session.tsx` intencionalmente inalterado; os consumidores chamam `can(useSession(), key)`.
