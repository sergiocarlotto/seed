# Fatia 1 — Perfis (Frontend Controle de Acesso) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar a área de **Perfis** em `apps/web`: listar perfis da organização, criar/editar perfil com um seletor de permissões em acordeão (por `module`), e arquivar perfis — consumindo os endpoints `GET/POST/PUT/DELETE /profiles` e `GET /permissions` da ADR-0012.

**Architecture:** Páginas de dados são **Server Components** que fazem prefetch via `serverGet` (abordagem A do design) e delegam a componentes cliente a interação (formulário, diálogos, cabeçalho de página) usando o `api` client + `router.refresh()`. A lógica pura do seletor de permissões (estado tri-estado do módulo, toggles) vive isolada em `lib/permission-tree.ts` e é testada com Vitest. Os primitivos `checkbox` e `badge` (shadcn/base-ui, escritos à mão como os demais wrappers) são adicionados aqui.

**Tech Stack:** TypeScript, React 19, Next.js 16, `@base-ui/react`, Tailwind, Vitest, Playwright.

**Referências:**
- Design: `docs/specs/2026-07-20-access-control-frontend-perfis-usuarios-design.md` (Fatia 1).
- Fatia 0 já concluída: `lib/access.ts` (`can`), `Me` com `isOwner`+`permissions`, `nav.ts` com `visibleNav(modules, can)`.
- Contratos (camelCase): `GET /permissions` → `[{ module, permissions: [{ key, displayName, description }] }]`; `GET /profiles` → `[{ id, name, description, isSystem, status, userCount }]`; `GET /profiles/{id}` → `{ id, name, description, isSystem, status, permissionKeys[] }`; `POST`/`PUT /profiles` corpo `{ name, description?, permissionKeys?[] }`; `DELETE /profiles/{id}` → 204 (arquiva; 400 se `is_system`).

**Nota de arquitetura (reconciliação com o código atual):** as páginas de Empresas (`companies/[id]`) usam fetch client-side (`useEffect`) — padrão legado. As páginas novas de Perfis adotam **RSC prefetch** (abordagem A aprovada no design): sem flash de loading e alinhado ao `getMeServer`/`serverGet` já usados no layout. As duas convivem; Empresas não é migrada aqui (fora de escopo).

**Comandos (host, Node/Vitest — não bloqueados pelo SAC), da raiz do repo:**
- Unit: `npm --prefix apps/web run test`
- Lint: `npm --prefix apps/web run lint`
- Build/typecheck: `npm --prefix apps/web run build`
- e2e (precisa de stack rodando): `npm --prefix apps/web run e2e`

---

## Arquivos desta fatia

- **Criar** `apps/web/src/components/ui/checkbox.tsx` — wrapper base-ui (check + indeterminate).
- **Criar** `apps/web/src/components/ui/badge.tsx` — badge com variantes (`neutral`, `system`).
- **Criar** `apps/web/src/lib/permission-tree.ts` — lógica pura do seletor (tri-estado + toggles).
- **Criar** `apps/web/src/lib/permission-tree.test.ts` — testes da lógica pura.
- **Criar** `apps/web/src/components/PermissionTree.tsx` — acordeão por módulo (client).
- **Criar** `apps/web/src/components/ProfileForm.tsx` — formulário criar/editar (client).
- **Criar** `apps/web/src/components/ProfilesList.tsx` — tabela/cards + arquivar (client).
- **Criar** `apps/web/src/app/(app)/profiles/page.tsx` — lista (RSC prefetch).
- **Criar** `apps/web/src/app/(app)/profiles/new/page.tsx` — criar (RSC prefetch do catálogo).
- **Criar** `apps/web/src/app/(app)/profiles/[id]/page.tsx` — editar (RSC prefetch catálogo + perfil).
- **Modificar** `apps/web/src/lib/types.ts` — tipos `PermissionItem`, `PermissionGroup`, `ProfileSummary`, `ProfileDetail`.
- **Modificar** `apps/web/src/lib/nav.ts` — item de menu **Perfis** (`profiles.manage`).
- **Criar** `apps/web/e2e/profiles.spec.ts` — e2e de fumaça (best-effort).

---

## Task 1: Tipos + primitivos `checkbox` e `badge`

Sem comportamento testável por unidade — a verificação é `lint` + `build` (typecheck). Não escreva testes unit aqui.

**Files:**
- Modify: `apps/web/src/lib/types.ts`
- Create: `apps/web/src/components/ui/checkbox.tsx`
- Create: `apps/web/src/components/ui/badge.tsx`

- [ ] **Step 1: Adicionar os tipos de Perfil/Permissão**

Ao final de `apps/web/src/lib/types.ts`, acrescentar:

```ts
export type PermissionItem = { key: string; displayName: string; description: string };
export type PermissionGroup = { module: string; permissions: PermissionItem[] };

export type ProfileSummary = {
  id: string;
  name: string;
  description: string;
  isSystem: boolean;
  status: string;
  userCount: number;
};

export type ProfileDetail = {
  id: string;
  name: string;
  description: string;
  isSystem: boolean;
  status: string;
  permissionKeys: string[];
};
```

- [ ] **Step 2: Criar o wrapper `checkbox.tsx`**

Criar `apps/web/src/components/ui/checkbox.tsx`:

```tsx
"use client"

import { Checkbox as CheckboxPrimitive } from "@base-ui/react/checkbox"
import { CheckIcon, MinusIcon } from "lucide-react"

import { cn } from "@/lib/utils"

/**
 * Checkbox (base-ui). Suporta o estado `indeterminate` (traço) usado pelo
 * cabeçalho de módulo do seletor de permissões: quando `indeterminate` é true,
 * o indicador mostra um traço; quando apenas `checked`, mostra o check.
 */
function Checkbox({
  className,
  indeterminate,
  ...props
}: CheckboxPrimitive.Root.Props) {
  return (
    <CheckboxPrimitive.Root
      data-slot="checkbox"
      indeterminate={indeterminate}
      className={cn(
        "peer size-4 shrink-0 rounded border border-input outline-none transition-colors",
        "data-checked:border-primary data-checked:bg-primary data-checked:text-primary-foreground",
        "data-indeterminate:border-primary data-indeterminate:bg-primary data-indeterminate:text-primary-foreground",
        "focus-visible:ring-3 focus-visible:ring-ring/50",
        "disabled:cursor-not-allowed disabled:opacity-50",
        className
      )}
      {...props}
    >
      <CheckboxPrimitive.Indicator
        data-slot="checkbox-indicator"
        className="flex items-center justify-center text-current"
      >
        {indeterminate ? <MinusIcon className="size-3.5" /> : <CheckIcon className="size-3.5" />}
      </CheckboxPrimitive.Indicator>
    </CheckboxPrimitive.Root>
  )
}

export { Checkbox }
```

- [ ] **Step 3: Criar o wrapper `badge.tsx`**

Criar `apps/web/src/components/ui/badge.tsx`:

```tsx
import * as React from "react"
import { cva, type VariantProps } from "class-variance-authority"

import { cn } from "@/lib/utils"

const badgeVariants = cva(
  "inline-flex items-center rounded-md px-2 py-0.5 text-xs font-medium ring-1 ring-inset",
  {
    variants: {
      variant: {
        neutral: "bg-muted text-muted-foreground ring-border",
        system: "bg-primary/10 text-primary ring-primary/20",
      },
    },
    defaultVariants: { variant: "neutral" },
  }
)

function Badge({
  className,
  variant,
  ...props
}: React.ComponentProps<"span"> & VariantProps<typeof badgeVariants>) {
  return <span data-slot="badge" className={cn(badgeVariants({ variant }), className)} {...props} />
}

export { Badge, badgeVariants }
```

- [ ] **Step 4: Verificar lint + build**

Run: `npm --prefix apps/web run lint`
Expected: sem erros.
Run: `npm --prefix apps/web run build`
Expected: compila sem erros de tipo (confirma que os wrappers base-ui e os tipos são válidos).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/types.ts apps/web/src/components/ui/checkbox.tsx apps/web/src/components/ui/badge.tsx
git commit -m "feat(web): tipos de perfil/permissao e primitivos checkbox e badge"
```

---

## Task 2: Lógica pura do seletor (`permission-tree.ts`) — TDD

**Files:**
- Create: `apps/web/src/lib/permission-tree.ts`
- Create: `apps/web/src/lib/permission-tree.test.ts`

- [ ] **Step 1: Escrever o teste que falha**

Criar `apps/web/src/lib/permission-tree.test.ts`:

```ts
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
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `npm --prefix apps/web run test`
Expected: FAIL — `Cannot find module './permission-tree'`.

- [ ] **Step 3: Implementar `permission-tree.ts`**

Criar `apps/web/src/lib/permission-tree.ts`:

```ts
import type { PermissionGroup } from "./types";

export type ModuleCheckState = "checked" | "indeterminate" | "unchecked";

/** Estado do checkbox de cabeçalho de um módulo dado o conjunto selecionado. */
export function moduleState(group: PermissionGroup, selected: ReadonlySet<string>): ModuleCheckState {
  const keys = group.permissions.map((p) => p.key);
  const count = keys.filter((k) => selected.has(k)).length;
  if (count === 0) return "unchecked";
  if (count === keys.length) return "checked";
  return "indeterminate";
}

/** Alterna o módulo inteiro: se todas marcadas, desmarca; senão marca todas. Não muta a entrada. */
export function toggleModule(group: PermissionGroup, selected: ReadonlySet<string>): Set<string> {
  const keys = group.permissions.map((p) => p.key);
  const next = new Set(selected);
  const allOn = keys.every((k) => next.has(k));
  for (const k of keys) {
    if (allOn) next.delete(k);
    else next.add(k);
  }
  return next;
}

/** Alterna uma única permissão. Não muta a entrada. */
export function togglePermission(key: string, selected: ReadonlySet<string>): Set<string> {
  const next = new Set(selected);
  if (next.has(key)) next.delete(key);
  else next.add(key);
  return next;
}
```

- [ ] **Step 4: Rodar e ver passar**

Run: `npm --prefix apps/web run test`
Expected: PASS em todos os casos de `permission-tree.test.ts` (e os demais testes seguem verdes).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/permission-tree.ts apps/web/src/lib/permission-tree.test.ts
git commit -m "feat(web): logica pura do seletor de permissoes (tri-estado + toggles)"
```

---

## Task 3: Componente `PermissionTree` (acordeão)

Componente cliente que usa a lógica pura da Task 2 + o `Checkbox`. Verificação por `build`/`lint`.

**Files:**
- Create: `apps/web/src/components/PermissionTree.tsx`

- [ ] **Step 1: Criar `PermissionTree.tsx`**

```tsx
"use client";

import { useState } from "react";
import { ChevronDown } from "lucide-react";
import { cn } from "@/lib/utils";
import { Checkbox } from "@/components/ui/checkbox";
import { moduleState, toggleModule, togglePermission } from "@/lib/permission-tree";
import type { PermissionGroup } from "@/lib/types";

type PermissionTreeProps = {
  groups: PermissionGroup[];
  selected: Set<string>;
  onChange: (next: Set<string>) => void;
  disabled?: boolean;
};

/**
 * Seletor de permissões agrupado por `module` (acordeão). O checkbox de cabeçalho
 * marca/desmarca o módulo inteiro e mostra estado indeterminado quando parcial.
 * Abre expandido; empilha naturalmente no mobile. `disabled` deixa tudo em leitura
 * (perfil de sistema).
 */
export function PermissionTree({ groups, selected, onChange, disabled = false }: PermissionTreeProps) {
  const [open, setOpen] = useState<Record<string, boolean>>(() =>
    Object.fromEntries(groups.map((g) => [g.module, true]))
  );

  if (groups.length === 0) {
    return <p className="text-sm text-muted-foreground">Nenhuma permissão disponível.</p>;
  }

  return (
    <div className="flex flex-col gap-3" data-testid="permission-tree">
      {groups.map((group) => {
        const state = moduleState(group, selected);
        const isOpen = open[group.module] ?? true;
        return (
          <div key={group.module} className="overflow-hidden rounded-lg ring-1 ring-foreground/10">
            <div className="flex items-center gap-2 bg-muted/50 px-3 py-2">
              <Checkbox
                checked={state === "checked"}
                indeterminate={state === "indeterminate"}
                disabled={disabled}
                onCheckedChange={() => onChange(toggleModule(group, selected))}
                aria-label={`Selecionar todas de ${group.module}`}
              />
              <span className="font-medium capitalize">{group.module}</span>
              <button
                type="button"
                onClick={() => setOpen((o) => ({ ...o, [group.module]: !isOpen }))}
                aria-label={isOpen ? "Recolher" : "Expandir"}
                aria-expanded={isOpen}
                className="ml-auto rounded p-1 text-muted-foreground hover:bg-muted"
              >
                <ChevronDown className={cn("size-4 transition-transform", !isOpen && "-rotate-90")} />
              </button>
            </div>
            {isOpen && (
              <ul className="flex flex-col">
                {group.permissions.map((perm) => (
                  <li key={perm.key} className="flex items-center gap-2 px-3 py-2 pl-8">
                    <Checkbox
                      checked={selected.has(perm.key)}
                      disabled={disabled}
                      onCheckedChange={() => onChange(togglePermission(perm.key, selected))}
                      aria-label={perm.displayName}
                    />
                    <span className="text-sm">{perm.displayName}</span>
                    <span className="text-xs text-muted-foreground">· {perm.key}</span>
                  </li>
                ))}
              </ul>
            )}
          </div>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 2: Verificar lint + build**

Run: `npm --prefix apps/web run lint`
Expected: sem erros.
Run: `npm --prefix apps/web run build`
Expected: compila sem erros de tipo.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/PermissionTree.tsx
git commit -m "feat(web): componente PermissionTree (acordeao por modulo)"
```

---

## Task 4: Componente `ProfileForm` (criar/editar)

Formulário cliente reutilizado por `/profiles/new` e `/profiles/[id]`. Define o cabeçalho de página, gere estado do formulário e chama o `api` client. Verificação por `build`/`lint`.

**Files:**
- Create: `apps/web/src/components/ProfileForm.tsx`

- [ ] **Step 1: Criar `ProfileForm.tsx`**

```tsx
"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSetPageHeader } from "@/lib/page-header";
import { PermissionTree } from "@/components/PermissionTree";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import type { PermissionGroup, ProfileDetail } from "@/lib/types";

type ProfileFormProps =
  | { mode: "create"; groups: PermissionGroup[]; profile?: undefined }
  | { mode: "edit"; groups: PermissionGroup[]; profile: ProfileDetail };

/**
 * Editor de perfil (página cheia). No modo edit de um perfil `is_system`, tudo
 * fica em leitura (nome, descrição e seletor desabilitados) — a app não altera o
 * perfil "Administrador".
 */
export function ProfileForm({ mode, groups, profile }: ProfileFormProps) {
  const readOnly = mode === "edit" && profile.isSystem;
  useSetPageHeader({
    title: mode === "create" ? "Novo perfil" : readOnly ? "Perfil (somente leitura)" : "Editar perfil",
    breadcrumb: ["Administração", "Perfis", mode === "create" ? "Novo" : "Editar"],
  });
  const router = useRouter();
  const [name, setName] = useState(profile?.name ?? "");
  const [description, setDescription] = useState(profile?.description ?? "");
  const [selected, setSelected] = useState<Set<string>>(new Set(profile?.permissionKeys ?? []));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setSaving(true);
    const body = { name: name.trim(), description: description.trim(), permissionKeys: [...selected] };
    try {
      if (mode === "create") await api.post("/profiles", body);
      else await api.put(`/profiles/${profile.id}`, body);
      router.push("/profiles");
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
      setSaving(false);
    }
  }

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <div className="flex items-center justify-between gap-4">
        {readOnly && <Badge variant="system">Sistema</Badge>}
        <Button variant="ghost" size="sm" className="ml-auto" render={<Link href="/profiles" />}>
          Voltar
        </Button>
      </div>

      <form onSubmit={handleSubmit} className="flex flex-col gap-5">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="name">Nome do perfil</Label>
          <Input
            id="name"
            required
            value={name}
            disabled={readOnly}
            onChange={(e) => setName(e.target.value)}
            data-testid="profile-name"
          />
        </div>

        <div className="flex flex-col gap-1.5">
          <Label htmlFor="description">Descrição</Label>
          <Input
            id="description"
            value={description}
            disabled={readOnly}
            onChange={(e) => setDescription(e.target.value)}
          />
        </div>

        <div className="flex flex-col gap-2">
          <Label>Permissões</Label>
          <PermissionTree groups={groups} selected={selected} onChange={setSelected} disabled={readOnly} />
        </div>

        {error && (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        )}

        {!readOnly && (
          <div>
            <Button type="submit" size="lg" disabled={saving} data-testid="profile-submit">
              {saving ? "Salvando..." : mode === "create" ? "Criar perfil" : "Salvar"}
            </Button>
          </div>
        )}
      </form>
    </div>
  );
}
```

- [ ] **Step 2: Verificar lint + build**

Run: `npm --prefix apps/web run lint`
Expected: sem erros.
Run: `npm --prefix apps/web run build`
Expected: compila sem erros de tipo.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ProfileForm.tsx
git commit -m "feat(web): componente ProfileForm (criar/editar, leitura p/ is_system)"
```

---

## Task 5: Lista `/profiles` (RSC) + `ProfilesList` (client, arquivar)

**Files:**
- Create: `apps/web/src/components/ProfilesList.tsx`
- Create: `apps/web/src/app/(app)/profiles/page.tsx`

- [ ] **Step 1: Criar `ProfilesList.tsx` (client)**

```tsx
"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSetPageHeader } from "@/lib/page-header";
import { EmptyState, ErrorState } from "@/components/states";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import type { ProfileSummary } from "@/lib/types";

/**
 * Lista de perfis. Perfil `isSystem` só tem "Ver" (leitura); os demais têm
 * "Editar" e "Arquivar". Arquivar chama DELETE /profiles/{id} (soft) e avisa
 * quantos usuários serão afetados.
 */
export function ProfilesList({ initial }: { initial: ProfileSummary[] }) {
  useSetPageHeader({ title: "Perfis", breadcrumb: ["Administração", "Perfis"] });
  const router = useRouter();
  const [profiles, setProfiles] = useState<ProfileSummary[]>(initial);
  const [error, setError] = useState<string | null>(null);
  const [target, setTarget] = useState<ProfileSummary | null>(null);
  const [archiving, setArchiving] = useState(false);

  async function handleArchive() {
    if (!target) return;
    setArchiving(true);
    setError(null);
    try {
      await api.del<void>(`/profiles/${target.id}`);
      setProfiles((prev) => prev.filter((p) => p.id !== target.id));
      setTarget(null);
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setArchiving(false);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      {profiles.length > 0 && (
        <div>
          <Button render={<Link href="/profiles/new" />} data-testid="new-profile">
            Novo perfil
          </Button>
        </div>
      )}

      {error && <ErrorState message={error} onRetry={handleArchive} />}

      {profiles.length === 0 ? (
        <EmptyState
          title="Nenhum perfil ainda"
          description="Crie o primeiro perfil para conceder permissões aos usuários."
          action={
            <Button render={<Link href="/profiles/new" />} data-testid="new-profile">
              Novo perfil
            </Button>
          }
        />
      ) : (
        <div className="rounded-xl ring-1 ring-foreground/10">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Nome</TableHead>
                <TableHead>Descrição</TableHead>
                <TableHead>Usuários</TableHead>
                <TableHead>Tipo</TableHead>
                <TableHead className="text-right">Ações</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {profiles.map((p) => (
                <TableRow key={p.id} data-testid={`profile-row-${p.id}`}>
                  <TableCell className="font-medium">{p.name}</TableCell>
                  <TableCell className="text-muted-foreground">{p.description}</TableCell>
                  <TableCell>{p.userCount}</TableCell>
                  <TableCell>
                    {p.isSystem ? <Badge variant="system">Sistema</Badge> : <Badge>Custom</Badge>}
                  </TableCell>
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-2">
                      {p.isSystem ? (
                        <Button variant="outline" size="sm" render={<Link href={`/profiles/${p.id}`} />}>
                          Ver
                        </Button>
                      ) : (
                        <>
                          <Button variant="outline" size="sm" render={<Link href={`/profiles/${p.id}`} />}>
                            Editar
                          </Button>
                          <Button variant="destructive" size="sm" onClick={() => setTarget(p)}>
                            Arquivar
                          </Button>
                        </>
                      )}
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}

      <Dialog open={target !== null} onOpenChange={(o) => !o && setTarget(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Arquivar perfil</DialogTitle>
            <DialogDescription>
              Arquivar <strong>{target?.name}</strong>? Ele deixará de conceder permissões
              {target && target.userCount > 0 ? (
                <>
                  {" "}
                  para <strong>{target.userCount}</strong> usuário(s) vinculado(s)
                </>
              ) : null}
              . O vínculo é mantido e a ação é reversível ao reativar.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <DialogClose render={<Button variant="outline" disabled={archiving} />}>Cancelar</DialogClose>
            <Button variant="destructive" onClick={handleArchive} disabled={archiving}>
              {archiving ? "Arquivando..." : "Arquivar"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
```

- [ ] **Step 2: Criar a página RSC `profiles/page.tsx`**

```tsx
import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { ProfilesList } from "@/components/ProfilesList";
import type { ProfileSummary } from "@/lib/types";

// Server Component: prefetch da lista (abordagem A). O enforcement real é do
// backend; aqui 403 vira NoAccess (acesso direto por URL sem profiles.manage).
export default async function ProfilesPage() {
  let profiles: ProfileSummary[];
  try {
    profiles = await serverGet<ProfileSummary[]>("/profiles");
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar os perfis." />;
  }
  return <ProfilesList initial={profiles} />;
}
```

- [ ] **Step 3: Verificar lint + build**

Run: `npm --prefix apps/web run lint`
Expected: sem erros.
Run: `npm --prefix apps/web run build`
Expected: compila; a rota `/profiles` aparece na saída do build.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ProfilesList.tsx "apps/web/src/app/(app)/profiles/page.tsx"
git commit -m "feat(web): lista de perfis (RSC prefetch + arquivar)"
```

---

## Task 6: Editor `/profiles/new` e `/profiles/[id]` (RSC) + item de menu

**Files:**
- Create: `apps/web/src/app/(app)/profiles/new/page.tsx`
- Create: `apps/web/src/app/(app)/profiles/[id]/page.tsx`
- Modify: `apps/web/src/lib/nav.ts`

- [ ] **Step 1: Criar `profiles/new/page.tsx` (RSC prefetch do catálogo)**

```tsx
import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { ProfileForm } from "@/components/ProfileForm";
import type { PermissionGroup } from "@/lib/types";

export default async function NewProfilePage() {
  let groups: PermissionGroup[];
  try {
    groups = await serverGet<PermissionGroup[]>("/permissions");
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar as permissões." />;
  }
  return <ProfileForm mode="create" groups={groups} />;
}
```

- [ ] **Step 2: Criar `profiles/[id]/page.tsx` (RSC prefetch catálogo + perfil)**

```tsx
import { notFound } from "next/navigation";
import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { ProfileForm } from "@/components/ProfileForm";
import type { PermissionGroup, ProfileDetail } from "@/lib/types";

export default async function EditProfilePage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  let groups: PermissionGroup[];
  let profile: ProfileDetail;
  try {
    [groups, profile] = await Promise.all([
      serverGet<PermissionGroup[]>("/permissions"),
      serverGet<ProfileDetail>(`/profiles/${id}`),
    ]);
  } catch (e) {
    const status = (e as { status?: number }).status;
    if (status === 404) notFound();
    if (status === 403) return <NoAccess />;
    return <ErrorState message="Não foi possível carregar o perfil." />;
  }
  return <ProfileForm mode="edit" groups={groups} profile={profile} />;
}
```

- [ ] **Step 3: Adicionar o item de menu "Perfis" em `nav.ts`**

Em `apps/web/src/lib/nav.ts`, no import do lucide, acrescentar `ShieldCheck`:

```ts
import { Building2, Settings2, ShieldCheck, type LucideIcon } from "lucide-react";
```

E no módulo "Administração" de `navModules`, adicionar o item **Perfis** logo após Empresas:

```ts
      { label: "Empresas", href: "/companies", icon: Building2, permission: "companies.access" },
      { label: "Perfis", href: "/profiles", icon: ShieldCheck, permission: "profiles.manage" },
```

- [ ] **Step 4: Verificar unit + lint + build**

Run: `npm --prefix apps/web run test`
Expected: PASS (nada mudou na lógica testada; `nav.test.ts` usa fixture próprio, não `navModules`).
Run: `npm --prefix apps/web run lint`
Expected: sem erros.
Run: `npm --prefix apps/web run build`
Expected: compila; rotas `/profiles`, `/profiles/new`, `/profiles/[id]` aparecem na saída.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/app/(app)/profiles/new/page.tsx" "apps/web/src/app/(app)/profiles/[id]/page.tsx" apps/web/src/lib/nav.ts
git commit -m "feat(web): paginas de criar/editar perfil (RSC) e item de menu Perfis"
```

---

## Task 7: e2e de fumaça + verificação final

O e2e precisa de stack rodando (api + db + seed `admin@demo.local`, que é owner). Escreva o spec; a execução é **best-effort** — se não houver stack, pule o passo de rodar (não bloqueia a fatia, como na Fatia 0).

**Files:**
- Create: `apps/web/e2e/profiles.spec.ts`

- [ ] **Step 1: Criar `profiles.spec.ts`**

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

test.describe("perfis", () => {
  test("lista de perfis mostra o perfil de sistema", async ({ page }) => {
    await login(page);
    await page.goto("/profiles");
    await expect(page.getByTestId("page-title")).toHaveText("Perfis");
    await expect(page.getByText("Administrador")).toBeVisible();
    await expect(page.getByText("Sistema")).toBeVisible();
  });

  test("cria um perfil com uma permissão", async ({ page }) => {
    await login(page);
    await page.goto("/profiles");
    await page.getByTestId("new-profile").click();
    await page.waitForURL("**/profiles/new");

    const nome = `Perfil E2E ${Date.now()}`;
    await page.getByTestId("profile-name").fill(nome);
    // marca a primeira permissão da árvore
    await page.getByTestId("permission-tree").getByRole("checkbox").nth(1).click();
    await page.getByTestId("profile-submit").click();

    await page.waitForURL("**/profiles");
    await expect(page.getByText(nome)).toBeVisible();
  });
});
```

- [ ] **Step 2: (Best-effort) rodar o e2e se houver stack**

Se `docker compose up` (api+db) estiver de pé e o seed disponível:
Run: `npm --prefix apps/web run e2e`
Expected: os 2 testes de `profiles.spec.ts` passam (mais os de `shell.spec.ts`/`smoke.spec.ts`).
Se não houver stack, pular — registrar no relatório que o e2e não foi executado.

- [ ] **Step 3: Verificação final da fatia**

Run: `npm --prefix apps/web run test`
Expected: PASS — inclui `permission-tree.test.ts` (8), `access.test.ts` (4), `nav.test.ts` (4), e os demais.
Run: `npm --prefix apps/web run lint`
Expected: sem erros.
Run: `npm --prefix apps/web run build`
Expected: build ok, rotas de `/profiles*` presentes.

- [ ] **Step 4: Commit**

```bash
git add apps/web/e2e/profiles.spec.ts
git commit -m "test(web): e2e de fumaca para a area de perfis"
```

---

## Self-Review (preenchido pelo autor do plano)

- **Cobertura do spec (Fatia 1):**
  - Lista (Nome/Descrição/Usuários/Tipo/Ações, badge Sistema/Custom, Novo perfil, is_system só "Ver") → Task 5 ✓
  - Arquivar com aviso de nº de usuários (DELETE) → Task 5 ✓
  - Editor página cheia (name, description, seletor), is_system em leitura → Task 4 ✓
  - Seletor em acordeão por module, checkbox tri-estado, chaves em muted, lógica pura isolada e testada → Tasks 2 e 3 ✓
  - Tipos espelhando DTOs → Task 1 ✓
  - Primitivos checkbox/badge → Task 1 ✓
  - Dados via RSC prefetch + api client/router.refresh → Tasks 5 e 6 ✓
  - Erros: 403→NoAccess, 404→notFound, 400 (validação) inline via errorMessage → Tasks 5 e 6 (páginas) e 4 (form) ✓
  - Item de menu Perfis (profiles.manage) → Task 6 ✓
  - Testes: vitest (helpers do seletor) + e2e de fumaça → Tasks 2 e 7 ✓
- **Placeholders:** nenhum — todo passo traz código real.
- **Consistência de tipos:** `PermissionGroup`/`PermissionItem`/`ProfileSummary`/`ProfileDetail` definidos na Task 1 e usados igualmente em `permission-tree.ts`, `PermissionTree`, `ProfileForm`, `ProfilesList` e nas páginas RSC. `moduleState`/`toggleModule`/`togglePermission` com a mesma assinatura em teste, helper e componente. `serverGet<T>(path)` (existente) reusado nas 3 páginas RSC.
- **Nota mobile:** tabela de perfis segue a convenção do `apps/web` (no mobile idealmente vira cards). Esta fatia entrega a tabela desktop + o acordeão já responsivo; o refinamento card/mobile da tabela pode ser um follow-up se o revisor apontar, mas não é bloqueante para o fluxo funcional.
- **Escopo:** só Perfis. Usuários (switch, atribuição, /users) ficam para a Fatia 2, incluindo o primitivo `switch` e as variantes de status do `badge`.
