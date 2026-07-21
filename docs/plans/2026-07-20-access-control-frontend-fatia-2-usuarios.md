# Fatia 2 — Usuários (Frontend Controle de Acesso) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar a área de **Usuários** em `apps/web`: listar os membros da organização e, no detalhe, ativar/desativar (status), atribuir/remover perfis e exibir as empresas acessíveis (somente leitura) — consumindo `GET /users`, `GET /users/{id}`, `PATCH /users/{id}/status` e `PUT /users/{id}/profiles`.

**Architecture:** Igual à Fatia 1: páginas de dados são **Server Components** com prefetch via `serverGet`; a interação (switch de status, checklist de perfis) fica em componentes cliente usando o `api` client + `router.refresh()`. A regra de UX de quem pode atribuir cada perfil (postura B + owner read-only) é isolada numa função pura `canAssignProfile` (`lib/user-profiles.ts`, testada com Vitest). Novos primitivos: `switch` e variantes de status no `badge`.

**Tech Stack:** TypeScript, React 19, Next.js 16, `@base-ui/react`, Tailwind, Vitest, Playwright.

**Referências:**
- Design: `docs/specs/2026-07-20-access-control-frontend-perfis-usuarios-design.md` (Fatia 2).
- Fatia 1 concluída: primitivos `checkbox`/`badge`, `PermissionTree`, `ProfileForm`, `ProfilesList`, páginas RSC de `/profiles`, item de menu Perfis.
- Contratos (camelCase): `GET /users` → `[{ id, fullName, email, status, isOwner, profiles: [{ id, name }], companies: [{ id, name }] }]`; `GET /users/{id}` → mesmo objeto; `PATCH /users/{id}/status` corpo `{ active: boolean }` → retorna o usuário (recusa 4xx desativar owner); `PUT /users/{id}/profiles` corpo `{ profileIds: string[] }` → retorna o usuário (409 em corrida); `GET /profiles` (para o catálogo do checklist) → `ProfileSummary[]` — **gateado por `profiles.manage`**.

**Decisões de tratamento (do design + realidade do contrato):**
- **Owner é somente leitura:** switch de status desabilitado; perfis não editáveis. O switch de status também exige `users.manage`; a edição de perfis exige `profiles.assign`.
- **Postura B:** perfil `is_system` só é atribuível/removível se o operador é `isOwner`.
- **Gap do catálogo:** o checklist de atribuição precisa da lista de perfis da org (`GET /profiles`), que exige `profiles.manage`. A página de detalhe busca esse catálogo **best-effort**: se falhar (operador sem `profiles.manage`), a seção de perfis degrada para **leitura** (mostra os perfis atuais como chips). No v1 o operador típico (owner/Administrador) tem `profiles.manage`. Risco anotado: para um operador não-owner com `profiles.assign` mas o alvo já possui um perfil `is_system`, o `PUT` reenviaria esse id; se o backend rejeitar qualquer conjunto que contenha `is_system` de um não-owner (em vez de checar só o delta), haveria falso 4xx. O caminho feliz do e2e usa o owner (`admin@demo.local`), que não cai nesse caso.

**Nota de arquitetura:** mantém a convenção da Fatia 1 (páginas novas = RSC prefetch; Empresas segue no padrão legado client-side, sem migração).

**Comandos (host, Node/Vitest), da raiz do repo:**
- Unit: `npm --prefix apps/web run test`
- Lint: `npm --prefix apps/web run lint`
- Build/typecheck: `npm --prefix apps/web run build`
- e2e (precisa de stack): `npm --prefix apps/web run e2e`

---

## Arquivos desta fatia

- **Criar** `apps/web/src/components/ui/switch.tsx` — wrapper base-ui.
- **Modificar** `apps/web/src/components/ui/badge.tsx` — variantes `success`, `info`.
- **Modificar** `apps/web/src/lib/api.ts` — adicionar `api.patch`.
- **Modificar** `apps/web/src/lib/types.ts` — `EntityRef`, `UserRow`.
- **Criar** `apps/web/src/lib/user-profiles.ts` — `canAssignProfile` (pura).
- **Criar** `apps/web/src/lib/user-profiles.test.ts` — testes.
- **Criar** `apps/web/src/components/UsersList.tsx` — tabela (client).
- **Criar** `apps/web/src/components/UserProfilesForm.tsx` — checklist de perfis (client).
- **Criar** `apps/web/src/components/UserDetail.tsx` — status + empresas + perfis (client).
- **Criar** `apps/web/src/app/(app)/users/page.tsx` — lista (RSC).
- **Criar** `apps/web/src/app/(app)/users/[id]/page.tsx` — detalhe (RSC).
- **Modificar** `apps/web/src/lib/nav.ts` — item de menu **Usuários**.
- **Criar** `apps/web/e2e/users.spec.ts` — e2e de fumaça (best-effort).

---

## Task 1: Primitivo `switch`, variantes de `badge`, `api.patch`, tipos

Sem lógica testável por unidade — verificação por lint + build.

**Files:**
- Create: `apps/web/src/components/ui/switch.tsx`
- Modify: `apps/web/src/components/ui/badge.tsx`
- Modify: `apps/web/src/lib/api.ts`
- Modify: `apps/web/src/lib/types.ts`

- [ ] **Step 1: Criar `switch.tsx`**

```tsx
"use client"

import { Switch as SwitchPrimitive } from "@base-ui/react/switch"

import { cn } from "@/lib/utils"

/** Switch (base-ui). Usado para ativar/desativar usuário. */
function Switch({ className, ...props }: SwitchPrimitive.Root.Props) {
  return (
    <SwitchPrimitive.Root
      data-slot="switch"
      className={cn(
        "peer inline-flex h-5 w-9 shrink-0 items-center rounded-full border border-transparent p-0.5 outline-none transition-colors",
        "bg-input data-checked:bg-primary",
        "focus-visible:ring-3 focus-visible:ring-ring/50",
        "disabled:cursor-not-allowed disabled:opacity-50",
        className
      )}
      {...props}
    >
      <SwitchPrimitive.Thumb
        data-slot="switch-thumb"
        className="block size-4 rounded-full bg-background shadow transition-transform data-checked:translate-x-4"
      />
    </SwitchPrimitive.Root>
  )
}

export { Switch }
```

- [ ] **Step 2: Adicionar variantes ao `badge.tsx`**

Em `apps/web/src/components/ui/badge.tsx`, no objeto `variants.variant`, acrescentar `success` e `info` (mantendo `neutral` e `system`):

```ts
      variant: {
        neutral: "bg-muted text-muted-foreground ring-border",
        system: "bg-primary/10 text-primary ring-primary/20",
        success:
          "bg-emerald-50 text-emerald-700 ring-emerald-600/20 dark:bg-emerald-950 dark:text-emerald-400",
        info: "bg-blue-50 text-blue-700 ring-blue-600/20 dark:bg-blue-950 dark:text-blue-400",
      },
```

- [ ] **Step 3: Adicionar `api.patch`**

Em `apps/web/src/lib/api.ts`, no objeto `api`, acrescentar a linha `patch` (após `put`):

```ts
  put: <T>(p: string, b?: unknown) => request<T>("PUT", p, b),
  patch: <T>(p: string, b?: unknown) => request<T>("PATCH", p, b),
  del: <T>(p: string) => request<T>("DELETE", p),
```

- [ ] **Step 4: Adicionar os tipos de usuário**

Ao final de `apps/web/src/lib/types.ts`:

```ts
export type EntityRef = { id: string; name: string };

export type UserRow = {
  id: string;
  fullName: string;
  email: string;
  status: string;
  isOwner: boolean;
  profiles: EntityRef[];
  companies: EntityRef[];
};
```

- [ ] **Step 5: Verificar lint + build**

Run: `npm --prefix apps/web run lint`
Expected: sem erros.
Run: `npm --prefix apps/web run build`
Expected: compila. Se `@base-ui/react/switch` ou `SwitchPrimitive.Root.Props`/`Thumb` divergirem, PARE e reporte a mensagem exata como DONE_WITH_CONCERNS (confirmado que o subpath `switch` existe com `Root` + `Thumb` e props `checked`/`onCheckedChange`/`disabled`).

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/switch.tsx apps/web/src/components/ui/badge.tsx apps/web/src/lib/api.ts apps/web/src/lib/types.ts
git commit -m "feat(web): primitivo switch, variantes de badge, api.patch e tipos de usuario"
```

---

## Task 2: Regra pura `canAssignProfile` — TDD

**Files:**
- Create: `apps/web/src/lib/user-profiles.ts`
- Create: `apps/web/src/lib/user-profiles.test.ts`

- [ ] **Step 1: Escrever o teste que falha**

Criar `apps/web/src/lib/user-profiles.test.ts`:

```ts
import { describe, it, expect } from "vitest";
import { canAssignProfile } from "./user-profiles";

const base = { canAssign: true, targetIsOwner: false, meIsOwner: false, profileIsSystem: false };

describe("canAssignProfile", () => {
  it("nega sem profiles.assign", () => {
    expect(canAssignProfile({ ...base, canAssign: false })).toBe(false);
  });
  it("nega quando o alvo é o owner (somente leitura)", () => {
    expect(canAssignProfile({ ...base, targetIsOwner: true })).toBe(false);
  });
  it("perfil is_system: nega se o operador não é owner (postura B)", () => {
    expect(canAssignProfile({ ...base, profileIsSystem: true, meIsOwner: false })).toBe(false);
  });
  it("perfil is_system: permite se o operador é owner", () => {
    expect(canAssignProfile({ ...base, profileIsSystem: true, meIsOwner: true })).toBe(true);
  });
  it("perfil comum com profiles.assign: permite", () => {
    expect(canAssignProfile(base)).toBe(true);
  });
});
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `npm --prefix apps/web run test`
Expected: FAIL — `Cannot find module './user-profiles'`.

- [ ] **Step 3: Implementar `user-profiles.ts`**

```ts
/**
 * Regra de UX (espelho do backend) para habilitar a atribuição/remoção de um
 * perfil a um usuário. O backend é a barreira real; isto só habilita/desabilita
 * o checkbox. Reflete: owner do alvo é somente leitura; postura B (perfil
 * `is_system` só o owner atribui); exige `profiles.assign`.
 */
export function canAssignProfile(args: {
  canAssign: boolean;
  targetIsOwner: boolean;
  meIsOwner: boolean;
  profileIsSystem: boolean;
}): boolean {
  const { canAssign, targetIsOwner, meIsOwner, profileIsSystem } = args;
  if (!canAssign) return false;
  if (targetIsOwner) return false;
  if (profileIsSystem) return meIsOwner;
  return true;
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `npm --prefix apps/web run test`
Expected: PASS nos 5 casos (e os demais testes seguem verdes).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/user-profiles.ts apps/web/src/lib/user-profiles.test.ts
git commit -m "feat(web): regra pura canAssignProfile (postura B + owner read-only)"
```

---

## Task 3: Lista `/users` (RSC) + `UsersList` (client)

**Files:**
- Create: `apps/web/src/components/UsersList.tsx`
- Create: `apps/web/src/app/(app)/users/page.tsx`

- [ ] **Step 1: Criar `UsersList.tsx`**

```tsx
"use client";

import Link from "next/link";
import { useSetPageHeader } from "@/lib/page-header";
import { EmptyState } from "@/components/states";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { UserRow } from "@/lib/types";

/** Lista de membros da organização. Somente leitura aqui — ações vivem no detalhe. */
export function UsersList({ initial }: { initial: UserRow[] }) {
  useSetPageHeader({ title: "Usuários", breadcrumb: ["Administração", "Usuários"] });

  if (initial.length === 0) {
    return <EmptyState title="Nenhum usuário" description="Ainda não há membros nesta organização." />;
  }

  return (
    <div className="rounded-xl ring-1 ring-foreground/10">
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Nome</TableHead>
            <TableHead>Email</TableHead>
            <TableHead>Perfis</TableHead>
            <TableHead>Empresas</TableHead>
            <TableHead>Status</TableHead>
            <TableHead className="text-right">Ações</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {initial.map((u) => (
            <TableRow key={u.id} data-testid={`user-row-${u.id}`}>
              <TableCell className="font-medium">
                <span className="flex items-center gap-2">
                  {u.fullName}
                  {u.isOwner && <Badge variant="info">Owner</Badge>}
                </span>
              </TableCell>
              <TableCell className="text-muted-foreground">{u.email}</TableCell>
              <TableCell>
                {u.profiles.length === 0 ? (
                  <span className="text-sm text-muted-foreground">— sem perfil —</span>
                ) : (
                  <span className="flex flex-wrap gap-1">
                    {u.profiles.map((p) => (
                      <Badge key={p.id}>{p.name}</Badge>
                    ))}
                  </span>
                )}
              </TableCell>
              <TableCell className="text-muted-foreground">
                {u.companies.length === 0 ? "—" : u.companies.map((c) => c.name).join(", ")}
              </TableCell>
              <TableCell>
                {u.status.toLowerCase() === "active" ? (
                  <Badge variant="success">Ativo</Badge>
                ) : (
                  <Badge>Inativo</Badge>
                )}
              </TableCell>
              <TableCell className="text-right">
                <Button variant="outline" size="sm" render={<Link href={`/users/${u.id}`} />}>
                  Ver
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
```

- [ ] **Step 2: Criar `users/page.tsx` (RSC)**

```tsx
import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { UsersList } from "@/components/UsersList";
import type { UserRow } from "@/lib/types";

export default async function UsersPage() {
  let users: UserRow[];
  try {
    users = await serverGet<UserRow[]>("/users");
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar os usuários." />;
  }
  return <UsersList initial={users} />;
}
```

- [ ] **Step 3: Verificar lint + build**

Run: `npm --prefix apps/web run lint` (sem erros).
Run: `npm --prefix apps/web run build` (compila; rota `/users` na saída).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/UsersList.tsx "apps/web/src/app/(app)/users/page.tsx"
git commit -m "feat(web): lista de usuarios (RSC prefetch)"
```

---

## Task 4: `UserProfilesForm` (checklist de perfis + salvar)

Usa `canAssignProfile` (Task 2) e a sessão (`useSession`/`can`). Verificação por lint + build.

**Files:**
- Create: `apps/web/src/components/UserProfilesForm.tsx`

- [ ] **Step 1: Criar `UserProfilesForm.tsx`**

```tsx
"use client";

import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSession } from "@/lib/session";
import { can } from "@/lib/access";
import { canAssignProfile } from "@/lib/user-profiles";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import type { EntityRef, ProfileSummary } from "@/lib/types";

type UserProfilesFormProps = {
  userId: string;
  targetIsOwner: boolean;
  currentProfiles: EntityRef[];
  allProfiles: ProfileSummary[] | null;
};

/**
 * Atribuição de perfis do usuário. Editável só quando o operador tem
 * `profiles.assign`, o catálogo de perfis pôde ser carregado e o alvo não é o
 * owner; caso contrário, mostra os perfis atuais em leitura. `is_system` só o
 * owner marca (postura B, via `canAssignProfile`).
 */
export function UserProfilesForm({ userId, targetIsOwner, currentProfiles, allProfiles }: UserProfilesFormProps) {
  const me = useSession();
  const canAssign = can(me, "profiles.assign");
  const editable = canAssign && !targetIsOwner && allProfiles !== null;
  const [selected, setSelected] = useState<Set<string>>(new Set(currentProfiles.map((p) => p.id)));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  if (!editable) {
    return currentProfiles.length === 0 ? (
      <p className="text-sm text-muted-foreground">Sem perfil.</p>
    ) : (
      <div className="flex flex-wrap gap-1.5">
        {currentProfiles.map((p) => (
          <Badge key={p.id}>{p.name}</Badge>
        ))}
      </div>
    );
  }

  const active = allProfiles.filter((p) => p.status.toLowerCase() === "active");

  function toggle(id: string) {
    setSaved(false);
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  async function handleSave() {
    setSaving(true);
    setError(null);
    setSaved(false);
    try {
      await api.put(`/users/${userId}/profiles`, { profileIds: [...selected] });
      setSaved(true);
    } catch (err) {
      // 409: o usuário mudou (corrida) — a mensagem pede recarregar.
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      <ul className="flex flex-col gap-1.5" data-testid="user-profiles">
        {active.map((p) => {
          const enabled = canAssignProfile({
            canAssign,
            targetIsOwner,
            meIsOwner: me.isOwner,
            profileIsSystem: p.isSystem,
          });
          return (
            <li key={p.id} className="flex items-center gap-2">
              <Checkbox
                checked={selected.has(p.id)}
                disabled={!enabled}
                onCheckedChange={() => toggle(p.id)}
                aria-label={p.name}
              />
              <span className="text-sm">{p.name}</span>
              {p.isSystem && <Badge variant="system">Sistema</Badge>}
            </li>
          );
        })}
      </ul>
      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error} — recarregue a página e tente novamente.
        </p>
      )}
      {saved && <p className="text-sm text-emerald-600">Perfis atualizados.</p>}
      <div>
        <Button onClick={handleSave} disabled={saving} data-testid="save-profiles">
          {saving ? "Salvando..." : "Salvar perfis"}
        </Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Verificar lint + build**

Run: `npm --prefix apps/web run lint` (sem erros).
Run: `npm --prefix apps/web run build` (compila).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/UserProfilesForm.tsx
git commit -m "feat(web): atribuicao de perfis do usuario (checklist, postura B, leitura p/ owner)"
```

---

## Task 5: `UserDetail` (client) + `/users/[id]` (RSC) + item de menu

**Files:**
- Create: `apps/web/src/components/UserDetail.tsx`
- Create: `apps/web/src/app/(app)/users/[id]/page.tsx`
- Modify: `apps/web/src/lib/nav.ts`

- [ ] **Step 1: Criar `UserDetail.tsx`**

```tsx
"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSession } from "@/lib/session";
import { can } from "@/lib/access";
import { useSetPageHeader } from "@/lib/page-header";
import { UserProfilesForm } from "@/components/UserProfilesForm";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { UserRow, ProfileSummary } from "@/lib/types";

/**
 * Detalhe do usuário: status (switch), perfis (checklist) e empresas (leitura).
 * Owner é somente leitura. O switch exige `users.manage`; a edição de perfis é
 * governada pelo `UserProfilesForm`.
 */
export function UserDetail({ user, allProfiles }: { user: UserRow; allProfiles: ProfileSummary[] | null }) {
  useSetPageHeader({ title: user.fullName, breadcrumb: ["Administração", "Usuários", user.fullName] });
  const me = useSession();
  const router = useRouter();
  const canManage = can(me, "users.manage") && !user.isOwner;
  const [active, setActive] = useState(user.status.toLowerCase() === "active");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleToggle(next: boolean) {
    setSaving(true);
    setError(null);
    setActive(next);
    try {
      await api.patch(`/users/${user.id}/status`, { active: next });
      router.refresh();
    } catch (err) {
      setActive(!next); // desfaz o otimismo em caso de falha
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
      <div className="flex items-center justify-end">
        <Button variant="ghost" size="sm" render={<Link href="/users" />}>
          Voltar
        </Button>
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            {user.fullName}
            {user.isOwner && <Badge variant="info">Owner</Badge>}
          </CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <p className="text-sm text-muted-foreground">{user.email}</p>

          <div className="flex items-center justify-between">
            <div className="flex flex-col gap-1">
              <span className="text-xs font-medium uppercase tracking-wide text-muted-foreground">
                Status
              </span>
              {active ? <Badge variant="success">Ativo</Badge> : <Badge>Inativo</Badge>}
            </div>
            <Switch
              checked={active}
              disabled={!canManage || saving}
              onCheckedChange={handleToggle}
              aria-label="Ativar ou desativar usuário"
              data-testid="user-status"
            />
          </div>
          {user.isOwner && (
            <p className="text-xs text-muted-foreground">
              O dono da organização é somente leitura: não pode ser desativado nem ter perfis
              alterados pela aplicação.
            </p>
          )}
          {error && (
            <p role="alert" className="text-sm text-destructive">
              {error}
            </p>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Perfis</CardTitle>
        </CardHeader>
        <CardContent>
          <UserProfilesForm
            userId={user.id}
            targetIsOwner={user.isOwner}
            currentProfiles={user.profiles}
            allProfiles={allProfiles}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Empresas acessíveis</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-2">
          {user.companies.length === 0 ? (
            <p className="text-sm text-muted-foreground">Nenhuma empresa.</p>
          ) : (
            <div className="flex flex-wrap gap-1.5">
              {user.companies.map((c) => (
                <Badge key={c.id}>{c.name}</Badge>
              ))}
            </div>
          )}
          <p className="text-xs text-muted-foreground">
            A concessão de acesso a empresas é gerida no módulo de Empresas.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 2: Criar `users/[id]/page.tsx` (RSC)**

```tsx
import { notFound } from "next/navigation";
import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { UserDetail } from "@/components/UserDetail";
import type { UserRow, ProfileSummary } from "@/lib/types";

export default async function UserDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  let user: UserRow;
  try {
    user = await serverGet<UserRow>(`/users/${id}`);
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 404) notFound();
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar o usuário." />;
  }
  // Catálogo de perfis para o checklist — best-effort (exige profiles.manage).
  // Se falhar, a seção de perfis degrada para leitura (perfis atuais em chips).
  let allProfiles: ProfileSummary[] | null = null;
  try {
    allProfiles = await serverGet<ProfileSummary[]>("/profiles");
  } catch {
    allProfiles = null;
  }
  return <UserDetail user={user} allProfiles={allProfiles} />;
}
```

- [ ] **Step 3: Adicionar o item de menu "Usuários" em `nav.ts`**

No import do lucide, acrescentar `Users`:

```ts
import { Building2, Settings2, ShieldCheck, Users, type LucideIcon } from "lucide-react";
```

E no módulo "Administração", após o item Perfis:

```ts
      { label: "Perfis", href: "/profiles", icon: ShieldCheck, permission: "profiles.manage" },
      { label: "Usuários", href: "/users", icon: Users, permission: "users.manage" },
```

- [ ] **Step 4: Verificar unit + lint + build**

Run: `npm --prefix apps/web run test` (PASS; `nav.test.ts` usa fixture próprio).
Run: `npm --prefix apps/web run lint` (sem erros).
Run: `npm --prefix apps/web run build` (compila; rotas `/users` e `/users/[id]` na saída).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/UserDetail.tsx "apps/web/src/app/(app)/users/[id]/page.tsx" apps/web/src/lib/nav.ts
git commit -m "feat(web): detalhe do usuario (status, perfis, empresas) e item de menu Usuarios"
```

---

## Task 6: e2e de fumaça + verificação final

O e2e precisa de stack (api+db+seed `admin@demo.local`, owner). Escreva o spec; execução **best-effort**.

**Files:**
- Create: `apps/web/e2e/users.spec.ts`

- [ ] **Step 1: Criar `users.spec.ts`**

```ts
import { test, expect, type Page } from "@playwright/test";

const EMAIL = process.env.E2E_EMAIL ?? "admin@demo.local";
const PASSWORD = process.env.E2E_PASSWORD ?? "Admin123!";

async function login(page: Page) {
  await page.goto("/login");
  await page.getByLabel("Email").fill(EMAIL);
  await page.getByLabel("Senha").fill(PASSWORD);
  await page.getByRole("button", { name: "Entrar" }).click();
  await page.waitForURL("**/companies");
}

test.describe("usuários", () => {
  test("lista de usuários mostra o owner logado", async ({ page }) => {
    await login(page);
    await page.goto("/users");
    await expect(page.getByTestId("page-title")).toHaveText("Usuários");
    await expect(page.getByText(EMAIL)).toBeVisible();
    await expect(page.getByText("Owner").first()).toBeVisible();
  });

  test("abre o detalhe e vê o switch de status desabilitado para o owner", async ({ page }) => {
    await login(page);
    await page.goto("/users");
    await page.getByText(EMAIL).click();
    await page.waitForURL("**/users/**");
    // O owner é somente leitura: switch de status desabilitado.
    await expect(page.getByTestId("user-status")).toBeDisabled();
  });
});
```

- [ ] **Step 2: (Best-effort) rodar o e2e se houver stack**

Se `docker compose up` (api+db) estiver de pé:
Run: `npm --prefix apps/web run e2e`
Expected: os testes de `users.spec.ts` passam (mais os de `profiles.spec.ts`/`shell.spec.ts`/`smoke.spec.ts`).
Se não houver stack, pular e registrar no relatório.

- [ ] **Step 3: Verificação final da fatia**

Run: `npm --prefix apps/web run test`
Expected: PASS — inclui `user-profiles.test.ts` (5), `permission-tree.test.ts` (8), `access.test.ts` (4), `nav.test.ts` (4), e os demais.
Run: `npm --prefix apps/web run lint` (sem erros).
Run: `npm --prefix apps/web run build` (rotas `/users*` e `/profiles*` presentes).

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/users.spec.ts
git commit -m "test(web): e2e de fumaca para a area de usuarios"
```

---

## Self-Review (preenchido pelo autor do plano)

- **Cobertura do spec (Fatia 2):**
  - Lista (Nome + chip Owner, Email, Perfis chips/"— sem perfil —", Empresas, Status, Ver) → Task 3 ✓
  - Detalhe: switch de status (`PATCH`), desabilitado p/ owner e sem `users.manage` → Task 5 ✓
  - Atribuição de perfis (checklist, `PUT`), `is_system` só owner (postura B), desabilitado sem `profiles.assign` → Task 4 (+ regra pura Task 2) ✓
  - Empresas somente leitura → Task 5 ✓
  - Owner read-only (switch + perfis) → Tasks 4 e 5 ✓
  - 409 na atribuição → mensagem "recarregue" → Task 4 ✓
  - Primitivo `switch` + variantes de status do `badge` + `api.patch` → Task 1 ✓
  - Erros: 403→NoAccess, 404→notFound, validação inline → Tasks 3 e 5 ✓
  - Item de menu Usuários (`users.manage`) → Task 5 ✓
  - Testes: vitest (`canAssignProfile`) + e2e de fumaça → Tasks 2 e 6 ✓
- **Placeholders:** nenhum — todo passo traz código real.
- **Consistência de tipos:** `UserRow`/`EntityRef`/`ProfileSummary` definidos na Task 1 e usados em `UsersList`, `UserDetail`, `UserProfilesForm` e nas páginas RSC. `canAssignProfile({ canAssign, targetIsOwner, meIsOwner, profileIsSystem })` com a mesma assinatura em teste, helper e `UserProfilesForm`. `api.patch` (Task 1) consumido em `UserDetail`. `serverGet<T>` reusado.
- **Gap do catálogo tratado:** `GET /profiles` best-effort na página de detalhe; `UserProfilesForm` degrada para leitura quando `allProfiles === null` ou sem `profiles.assign` ou alvo owner. Risco do `PUT` reenviar `is_system` para não-owner anotado no cabeçalho (caminho feliz do e2e usa owner).
- **Follow-ups herdados (Minor, não bloqueantes):** tabela → cards no mobile; título do topbar em telas RSC de erro; guard `isServerApiError`. Mesmos da Fatia 1; podem ser fechados juntos num polimento.
