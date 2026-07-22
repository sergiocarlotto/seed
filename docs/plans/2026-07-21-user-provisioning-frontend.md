# Provisionamento de Usuários e Acesso a Empresas — Frontend

> **Para agentes:** SUB-SKILL OBRIGATÓRIA: use `superpowers:subagent-driven-development`
> (recomendado) ou `superpowers:executing-plans` para executar tarefa a tarefa.
> Os passos usam checkbox (`- [ ]`) para acompanhamento.

**Objetivo:** entregar as telas de criar usuário e de conceder/revogar acesso a
empresa (no detalhe do usuário e no detalhe da empresa), fechando o caso de uso
de ponta a ponta.

**Arquitetura:** Next.js 16 App Router. Páginas server-side buscam dados com
`serverGet` e delegam a componentes client que mutam via `api`. A regra de UX
de habilitação vive num helper puro e testável (`lib/company-access.ts`), no
mesmo espírito de `lib/user-profiles.ts`. O frontend **nunca é barreira** — só
espelho; o backend já recusa.

**Stack:** TypeScript, React 19, Next.js 16, Tailwind, shadcn/ui (base-ui),
Vitest (unit), Playwright (e2e).

**Pré-requisitos:**

1. `docs/plans/2026-07-21-user-provisioning-backend.md` concluído e verde,
   inclusive a revisão de segurança.
2. `docs/plans/2026-07-21-zod-form-validation.md` concluído — a Task 6 usa o
   `userSchema` criado lá.

**Spec:** `docs/specs/2026-07-21-user-provisioning-company-access-design.md`

## Ambiente

O frontend roda no host:

```
npm --prefix apps/web run test
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

O e2e exige a API de pé. **Não** suba o serviço `web` do compose junto — ele
disputa a porta 3000 com o dev server; se já estiver rodando, pare com
`docker compose stop web` e restaure depois.

Invoque o Playwright **de dentro de `apps/web`**. A forma `npm --prefix apps/web
exec playwright ...` não resolve o `baseURL` do `playwright.config.ts` neste
ambiente e falha com `page.goto: Cannot navigate to invalid URL`:

```
docker compose up -d db api
cd apps/web; npx playwright test
```

Ao rodar `docker compose` a partir do worktree, aponte `--env-file` para o `.env`
do repositório principal — sem isso os containers são recriados com senha vazia.

Se o banco responder `28P01`, a senha do volume `pgdata` diverge do `.env`.
Conserte **sem apagar o volume**:

```
docker compose exec -T db psql -U seed -d seed -c "ALTER USER seed WITH PASSWORD '<a-do-.env>';"
```

## Convenção mobile (obrigatória para tudo que for novo)

`apps/web/CLAUDE.md`: abaixo de `md`, tabela vira lista de cartões; formulário
vira uma coluna; ação secundária vai para menu "⋯". As listas `/users` e
`/profiles` existentes **não** cumprem isso (dívida `ui-polimento-listas-mobile`,
fora do escopo aqui) — mas nada criado neste plano pode aumentar a dívida.

## Estrutura de arquivos

| Arquivo | Responsabilidade |
| --- | --- |
| `apps/web/src/lib/types.ts` | + `CompanyUserAccess` |
| `apps/web/src/lib/company-access.ts` | **novo** — helper puro de habilitação e diff |
| `apps/web/src/lib/company-access.test.ts` | **novo** — unit do helper |
| `apps/web/src/components/UserForm.tsx` | **novo** — formulário de criação |
| `apps/web/src/app/(app)/users/new/page.tsx` | **novo** — rota de criação |
| `apps/web/src/components/UsersList.tsx` | + botão "Novo usuário" |
| `apps/web/src/components/UserCompaniesForm.tsx` | **novo** — checklist de empresas do usuário |
| `apps/web/src/components/UserDetail.tsx` | card de empresas passa a editável |
| `apps/web/src/app/(app)/users/[id]/page.tsx` | passa a carregar as empresas concedíveis |
| `apps/web/src/components/CompanyUsersForm.tsx` | **novo** — checklist de usuários da empresa |
| `apps/web/src/app/(app)/companies/[id]/page.tsx` | + seção "Usuários com acesso" |
| `apps/web/e2e/users.spec.ts` | + e2e de criação e concessão |

---

## Task 5: helper de acesso a empresas

**Arquivos:**
- Criar: `apps/web/src/lib/company-access.ts`
- Criar: `apps/web/src/lib/company-access.test.ts`
- Modificar: `apps/web/src/lib/types.ts`

- [ ] **Passo 1: escrever o teste que falha**

Crie `apps/web/src/lib/company-access.test.ts`:

```ts
import { describe, it, expect } from "vitest";
import { canGrantCompanies, mergePreservingOutOfScope } from "./company-access";

describe("canGrantCompanies", () => {
  it("nega sem companies.grant_access", () => {
    expect(canGrantCompanies({ isOwner: false, permissions: [] })).toBe(false);
  });
  it("permite com companies.grant_access", () => {
    expect(canGrantCompanies({ isOwner: false, permissions: ["companies.grant_access"] })).toBe(true);
  });
  it("permite para o owner (bypass funcional)", () => {
    expect(canGrantCompanies({ isOwner: true, permissions: [] })).toBe(true);
  });
});

describe("mergePreservingOutOfScope", () => {
  const scope = ["a", "b"];

  it("envia apenas o que está no escopo do operador", () => {
    // "z" está no usuário mas fora do escopo: não vai no payload, e o backend
    // o preserva (ADR-0014, regra 2).
    expect(mergePreservingOutOfScope({ selected: ["a", "z"], scope })).toEqual(["a"]);
  });

  it("desmarcar tudo envia lista vazia, sem tocar no que está fora do escopo", () => {
    expect(mergePreservingOutOfScope({ selected: [], scope })).toEqual([]);
  });

  it("preserva a ordem do escopo, sem duplicar", () => {
    expect(mergePreservingOutOfScope({ selected: ["b", "a", "b"], scope })).toEqual(["a", "b"]);
  });
});
```

- [ ] **Passo 2: rodar e confirmar que falha**

```
npm --prefix apps/web run test
```

Esperado: falha de resolução — `Failed to load ./company-access`.

- [ ] **Passo 3: implementar o helper**

Crie `apps/web/src/lib/company-access.ts`:

```ts
import type { AccessSubject } from "./access";
import { can } from "./access";

/**
 * Espelho de UX de `companies.grant_access`. O backend é a barreira real; isto
 * só decide se o checklist aparece editável.
 */
export function canGrantCompanies(subject: AccessSubject): boolean {
  return can(subject, "companies.grant_access");
}

/**
 * Monta o payload de `PUT /users/{id}/companies` a partir do que o operador
 * marcou. Só entram ids do escopo concedível dele: o que está fora nunca é
 * enviado, e o backend preserva essas concessões em vez de removê-las por
 * ausência (ADR-0014, regra 2). Sem esse filtro, um operador que não enxerga a
 * empresa X removeria X sem querer a cada gravação.
 */
export function mergePreservingOutOfScope(args: {
  selected: string[];
  scope: string[];
}): string[] {
  const selected = new Set(args.selected);
  return args.scope.filter((id) => selected.has(id));
}
```

- [ ] **Passo 4: adicionar o tipo compartilhado**

Em `apps/web/src/lib/types.ts`, ao final:

```ts
export type CompanyUserAccess = {
  id: string;
  fullName: string;
  email: string;
  hasAccess: boolean;
};
```

- [ ] **Passo 5: rodar e confirmar que passa**

```
npm --prefix apps/web run test
```

Esperado: **Test Files 7 passed, Tests 33 passed**.

- [ ] **Passo 6: commit**

```bash
git add apps/web/src/lib
git commit -m "feat(web): helper de escopo concedivel de empresas

canGrantCompanies espelha companies.grant_access e
mergePreservingOutOfScope garante que o payload so carregue ids do
escopo do operador, para nao remover por ausencia o que ele nao enxerga.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 6: tela de criar usuário

**Arquivos:**
- Criar: `apps/web/src/components/UserForm.tsx`
- Criar: `apps/web/src/app/(app)/users/new/page.tsx`
- Modificar: `apps/web/src/components/UsersList.tsx`

- [ ] **Passo 1: criar o formulário**

Crie `apps/web/src/components/UserForm.tsx`:

```tsx
"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSetPageHeader } from "@/lib/page-header";
import { userSchema, firstError } from "@/lib/form-schemas";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import type { UserRow } from "@/lib/types";

/**
 * Criação de usuário. O administrador define a senha inicial — não há convite
 * por e-mail (fora do escopo até existir e-mail transacional). A confirmação é
 * conferida só aqui; a política de senha de verdade é do Identity, no backend,
 * que devolve a mensagem. Ao salvar, vai para o detalhe, onde perfis e empresas
 * são configurados.
 */
export function UserForm() {
  useSetPageHeader({ title: "Novo usuário", breadcrumb: ["Administração", "Usuários", "Novo"] });
  const router = useRouter();
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    // O schema apara os campos e confere a confirmação (ADR-0002).
    const parsed = userSchema.safeParse({ fullName, email, password, confirm });
    if (!parsed.success) {
      setError(firstError(parsed.error));
      return;
    }

    setSaving(true);
    try {
      // `confirm` não vai para a API: é regra de tela, não campo do contrato.
      const created = await api.post<UserRow>("/users", {
        fullName: parsed.data.fullName,
        email: parsed.data.email,
        password: parsed.data.password,
      });
      router.push(`/users/${created.id}`);
      router.refresh();
    } catch (err) {
      setError(errorMessage(err));
      setSaving(false);
    }
  }

  return (
    // max-w-sm já é uma coluna em qualquer largura: atende a convenção mobile.
    <div className="mx-auto flex w-full max-w-sm flex-col gap-6">
      <Card>
        <CardHeader>
          <CardTitle className="text-base">Dados do usuário</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <Label htmlFor="fullName">Nome completo</Label>
              <Input
                id="fullName"
                type="text"
                required
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                required
                autoComplete="off"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="password">Senha inicial</Label>
              <Input
                id="password"
                type="password"
                required
                autoComplete="new-password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
              <p className="text-xs text-muted-foreground">
                Mínimo de 8 caracteres, com maiúscula, minúscula, número e símbolo. Combine com a
                pessoa um canal seguro para transmitir esta senha.
              </p>
            </div>

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="confirm">Confirmar senha</Label>
              <Input
                id="confirm"
                type="password"
                required
                autoComplete="new-password"
                value={confirm}
                onChange={(e) => setConfirm(e.target.value)}
              />
            </div>

            {error && (
              <p role="alert" className="text-sm text-destructive">
                {error}
              </p>
            )}

            <Button type="submit" size="lg" disabled={saving} data-testid="save-user">
              {saving ? "Criando..." : "Criar usuário"}
            </Button>

            <p className="text-xs text-muted-foreground">
              O usuário nasce ativo, sem perfis e sem empresas — ou seja, sem nenhum acesso. Configure
              perfis e empresas na tela seguinte.
            </p>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Passo 2: criar a rota**

Crie `apps/web/src/app/(app)/users/new/page.tsx`:

```tsx
import { UserForm } from "@/components/UserForm";

export default function NewUserPage() {
  return <UserForm />;
}
```

- [ ] **Passo 3: adicionar o botão na lista**

Em `apps/web/src/components/UsersList.tsx`, importe o que falta no topo:

```tsx
import { useSession } from "@/lib/session";
import { can } from "@/lib/access";
```

Troque o corpo do componente para envolver a tabela e exibir o botão. Substitua
o `if (initial.length === 0) { ... }` e o `return (...)` existentes por:

```tsx
  const me = useSession();
  const canManage = can(me, "users.manage");

  return (
    <div className="flex flex-col gap-6">
      {canManage && initial.length > 0 && (
        <div>
          <Button render={<Link href="/users/new" />} data-testid="new-user">
            Novo usuário
          </Button>
        </div>
      )}

      {initial.length === 0 ? (
        <EmptyState
          title="Nenhum usuário"
          description="Ainda não há membros nesta organização."
          action={
            canManage ? (
              <Button render={<Link href="/users/new" />} data-testid="new-user">
                Novo usuário
              </Button>
            ) : undefined
          }
        />
      ) : (
        <div className="rounded-xl ring-1 ring-foreground/10">
          {/* a <Table> existente entra aqui, sem alteração */}
        </div>
      )}
    </div>
  );
```

Mantenha o `useSetPageHeader` no topo do componente e reaproveite a `<Table>` já
escrita, apenas movendo-a para dentro do bloco indicado.

- [ ] **Passo 4: verificar lint e build**

```
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

Esperado: sem erro. Se o `EmptyState` reclamar da prop `action`, confira a
assinatura em `apps/web/src/components/states/EmptyState.tsx` — `ProfilesList` já
a usa da mesma forma.

- [ ] **Passo 5: commit**

```bash
git add apps/web/src
git commit -m "feat(web): tela de criacao de usuario

Formulario em coluna unica com senha inicial definida pelo
administrador; ao salvar, vai para o detalhe onde perfis e empresas sao
configurados. Botao Novo usuario sob users.manage.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 7: empresas editáveis no detalhe do usuário

**Arquivos:**
- Criar: `apps/web/src/components/UserCompaniesForm.tsx`
- Modificar: `apps/web/src/app/(app)/users/[id]/page.tsx`
- Modificar: `apps/web/src/components/UserDetail.tsx`

- [ ] **Passo 1: criar o checklist de empresas**

Crie `apps/web/src/components/UserCompaniesForm.tsx`:

```tsx
"use client";

import { useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSession } from "@/lib/session";
import { canGrantCompanies, mergePreservingOutOfScope } from "@/lib/company-access";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import type { Company, EntityRef } from "@/lib/types";

type UserCompaniesFormProps = {
  userId: string;
  currentCompanies: EntityRef[];
  /** Escopo concedível do operador: o que ele pode conceder ou revogar. */
  grantableCompanies: Company[] | null;
};

/**
 * Concessão de acesso a empresas do usuário. Editável só com
 * `companies.grant_access` e quando o escopo pôde ser carregado. Ao contrário de
 * status e perfis, o owner **é** alvo válido aqui: ele está sujeito ao eixo de
 * empresa (ADR-0012) e precisa poder receber acesso.
 *
 * Só se envia o que está no escopo do operador; empresas que ele não enxerga
 * ficam intactas no backend (ADR-0014).
 */
export function UserCompaniesForm({ userId, currentCompanies, grantableCompanies }: UserCompaniesFormProps) {
  const me = useSession();
  const editable = canGrantCompanies(me) && grantableCompanies !== null;
  const [selected, setSelected] = useState<Set<string>>(new Set(currentCompanies.map((c) => c.id)));
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  if (!editable) {
    return currentCompanies.length === 0 ? (
      <p className="text-sm text-muted-foreground">Nenhuma empresa.</p>
    ) : (
      <div className="flex flex-wrap gap-1.5">
        {currentCompanies.map((c) => (
          <Badge key={c.id}>{c.name}</Badge>
        ))}
      </div>
    );
  }

  const scope = grantableCompanies.map((c) => c.id);
  // Empresas do usuário fora do escopo do operador: mostradas em leitura, para
  // ele não achar que "desmarcou" algo que nunca esteve sob seu alcance.
  const outOfScope = currentCompanies.filter((c) => !scope.includes(c.id));

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
      await api.put(`/users/${userId}/companies`, {
        companyIds: mergePreservingOutOfScope({ selected: [...selected], scope }),
      });
      setSaved(true);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {grantableCompanies.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Você não tem acesso a nenhuma empresa, então não há o que conceder.
        </p>
      ) : (
        <ul className="flex flex-col gap-1.5" data-testid="user-companies">
          {grantableCompanies.map((c) => (
            <li key={c.id} className="flex items-center gap-2">
              <Checkbox
                checked={selected.has(c.id)}
                onCheckedChange={() => toggle(c.id)}
                aria-label={c.name}
              />
              <span className="text-sm">{c.name}</span>
            </li>
          ))}
        </ul>
      )}

      {outOfScope.length > 0 && (
        <div className="flex flex-col gap-1.5">
          <span className="text-xs text-muted-foreground">
            Fora do seu acesso (mantidas como estão):
          </span>
          <div className="flex flex-wrap gap-1.5">
            {outOfScope.map((c) => (
              <Badge key={c.id}>{c.name}</Badge>
            ))}
          </div>
        </div>
      )}

      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {saved && <p className="text-sm text-emerald-600 dark:text-emerald-400">Empresas atualizadas.</p>}

      {grantableCompanies.length > 0 && (
        <div>
          <Button onClick={handleSave} disabled={saving} data-testid="save-companies">
            {saving ? "Salvando..." : "Salvar empresas"}
          </Button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Passo 2: carregar o escopo na página**

Substitua o conteúdo de `apps/web/src/app/(app)/users/[id]/page.tsx` por:

```tsx
import { notFound } from "next/navigation";
import { serverGet } from "@/lib/api-server";
import { NoAccess, ErrorState } from "@/components/states";
import { UserDetail } from "@/components/UserDetail";
import type { UserRow, ProfileSummary, Company } from "@/lib/types";

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
  // Escopo concedível do operador. /companies já devolve só as empresas
  // concedidas a ele, que é exatamente o escopo de um não-owner (ADR-0014).
  // Sem companies.access não há o que oferecer: o card cai para leitura, e o
  // backend segue sendo a barreira real.
  let grantableCompanies: Company[] | null = null;
  try {
    grantableCompanies = await serverGet<Company[]>("/companies");
  } catch {
    grantableCompanies = null;
  }
  return (
    <UserDetail user={user} allProfiles={allProfiles} grantableCompanies={grantableCompanies} />
  );
}
```

> Atenção: para o **owner**, `/companies` devolve só as empresas concedidas a
> ele, não a organização inteira. Ou seja, a tela pode oferecer menos do que o
> backend aceitaria. É uma limitação conhecida e aceitável do v1 — o owner
> concede acesso a si mesmo primeiro e passa a enxergar. Não invente um endpoint
> novo para resolver isso aqui.

- [ ] **Passo 3: usar o formulário no detalhe**

Em `apps/web/src/components/UserDetail.tsx`:

1. adicione `grantableCompanies: Company[] | null` às props e importe `Company`
   de `@/lib/types` e `UserCompaniesForm` de `@/components/UserCompaniesForm`;
2. substitua **todo** o conteúdo do card "Empresas acessíveis" (o bloco
   `<CardContent className="flex flex-col gap-2">` com os badges e o parágrafo
   que menciona o "módulo de Empresas") por:

```tsx
        <CardContent>
          <UserCompaniesForm
            userId={user.id}
            currentCompanies={user.companies}
            grantableCompanies={grantableCompanies}
          />
        </CardContent>
```

O parágrafo "A concessão de acesso a empresas é gerida no módulo de Empresas"
**sai**: ele apontava para algo que não existia, e agora a gestão é aqui.

- [ ] **Passo 4: verificar lint e build**

```
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

- [ ] **Passo 5: commit**

```bash
git add apps/web/src
git commit -m "feat(web): concessao de empresas no detalhe do usuario

Card Empresas acessiveis vira checklist sob companies.grant_access.
Empresas fora do escopo do operador aparecem em leitura e nao entram no
payload, para nao serem removidas por ausencia.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 8: usuários com acesso no detalhe da empresa

**Arquivos:**
- Criar: `apps/web/src/components/CompanyUsersForm.tsx`
- Modificar: `apps/web/src/app/(app)/companies/[id]/page.tsx`

- [ ] **Passo 1: criar o checklist de usuários**

Crie `apps/web/src/components/CompanyUsersForm.tsx`:

```tsx
"use client";

import { useEffect, useState } from "react";
import { api, errorMessage } from "@/lib/api";
import { useSession } from "@/lib/session";
import { canGrantCompanies } from "@/lib/company-access";
import { Checkbox } from "@/components/ui/checkbox";
import { Button } from "@/components/ui/button";
import { Loading } from "@/components/states";
import type { CompanyUserAccess } from "@/lib/types";

/**
 * Quem tem acesso a esta empresa. Aqui o conjunto é completo: a empresa já está
 * no escopo do operador (senão o backend responde 404), então todos os usuários
 * da organização são alvo legítimo.
 */
export function CompanyUsersForm({ companyId }: { companyId: string }) {
  const me = useSession();
  const allowed = canGrantCompanies(me);
  const [users, setUsers] = useState<CompanyUserAccess[] | null>(null);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    if (!allowed) {
      setLoading(false);
      return;
    }
    let active = true;
    (async () => {
      try {
        const data = await api.get<CompanyUserAccess[]>(`/companies/${companyId}/users`);
        if (!active) return;
        setUsers(data);
        setSelected(new Set(data.filter((u) => u.hasAccess).map((u) => u.id)));
      } catch (err) {
        if (active) setError(errorMessage(err));
      } finally {
        if (active) setLoading(false);
      }
    })();
    return () => {
      active = false;
    };
  }, [companyId, allowed]);

  if (!allowed) return null;
  if (loading) return <Loading rows={2} />;

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
      await api.put(`/companies/${companyId}/users`, { userIds: [...selected] });
      setSaved(true);
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="flex flex-col gap-3">
      {users === null || users.length === 0 ? (
        <p className="text-sm text-muted-foreground">Nenhum usuário na organização.</p>
      ) : (
        <ul className="flex flex-col gap-1.5" data-testid="company-users">
          {users.map((u) => (
            <li key={u.id} className="flex items-center gap-2">
              <Checkbox
                checked={selected.has(u.id)}
                onCheckedChange={() => toggle(u.id)}
                aria-label={u.fullName}
              />
              <span className="flex min-w-0 flex-col sm:flex-row sm:gap-2">
                <span className="truncate text-sm">{u.fullName}</span>
                <span className="truncate text-xs text-muted-foreground">{u.email}</span>
              </span>
            </li>
          ))}
        </ul>
      )}

      {error && (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      )}
      {saved && <p className="text-sm text-emerald-600 dark:text-emerald-400">Acessos atualizados.</p>}

      {users !== null && users.length > 0 && (
        <div>
          <Button onClick={handleSave} disabled={saving} data-testid="save-company-users">
            {saving ? "Salvando..." : "Salvar acessos"}
          </Button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Passo 2: incluir a seção na página da empresa**

Em `apps/web/src/app/(app)/companies/[id]/page.tsx`, importe o componente:

```tsx
import { CompanyUsersForm } from "@/components/CompanyUsersForm";
```

E adicione um card logo **após** o card "Dados da empresa" (antes do bloco de
erro e do botão de exclusão):

```tsx
          <Card>
            <CardHeader>
              <CardTitle className="text-base">Usuários com acesso</CardTitle>
            </CardHeader>
            <CardContent>
              <CompanyUsersForm companyId={id} />
            </CardContent>
          </Card>
```

Troque também o `max-w-sm` do contêiner da página por `max-w-2xl`, para o
checklist caber — continua em coluna única, então a convenção mobile segue
atendida.

- [ ] **Passo 3: verificar lint e build**

```
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

- [ ] **Passo 4: commit**

```bash
git add apps/web/src
git commit -m "feat(web): usuarios com acesso no detalhe da empresa

Checklist sob companies.grant_access; o componente se esconde sozinho
quando o operador nao tem a permissao, e o backend segue barrando.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 9: e2e de ponta a ponta

**Arquivos:**
- Modificar: `apps/web/e2e/users.spec.ts`

- [ ] **Passo 1: escrever o teste**

Adicione ao final de `apps/web/e2e/users.spec.ts`, dentro do
`test.describe("usuários", ...)`:

```ts
  test("cria usuário e concede acesso a uma empresa", async ({ page }) => {
    await login(page);

    // E-mail único por execução: o banco de dev sobrevive entre rodadas.
    const stamp = Date.now();
    const email = `e2e.novo.${stamp}@demo.local`;

    await page.goto("/users/new");
    await page.getByLabel("Nome completo").fill("Usuário E2E");
    await page.getByLabel("Email").fill(email);
    await page.getByLabel("Senha inicial").fill("Passw0rd!");
    await page.getByLabel("Confirmar senha").fill("Passw0rd!");
    await page.getByTestId("save-user").click();

    // Redireciona para o detalhe do usuário recém-criado.
    await page.waitForURL("**/users/**");
    await expect(page.getByTestId("page-title")).toHaveText("Usuário E2E");

    // Nasce sem empresa: nenhum checkbox marcado no card de empresas.
    const companies = page.getByTestId("user-companies");
    await expect(companies).toBeVisible();
    await expect(companies.getByRole("checkbox", { checked: true })).toHaveCount(0);

    // Concede a primeira empresa do escopo e salva.
    await companies.getByRole("checkbox").first().check();
    await page.getByTestId("save-companies").click();
    await expect(page.getByText("Empresas atualizadas.")).toBeVisible();

    // Persistiu: ao recarregar, o checkbox continua marcado.
    await page.reload();
    await expect(
      page.getByTestId("user-companies").getByRole("checkbox", { checked: true }),
    ).toHaveCount(1);
  });
```

- [ ] **Passo 2: subir a stack e rodar**

```
docker compose up -d db api
npm --prefix apps/web run e2e
```

Esperado: **15 passed** (os 12 anteriores mais os 3 novos arquivos de cenário —
confira o total contra a execução anterior; o que importa é zero falha e o
cenário novo verde).

Se o login falhar com erro de banco, veja a nota sobre `28P01` na seção
"Ambiente" — conserte a senha, **não** apague o volume.

- [ ] **Passo 3: commit**

```bash
git add apps/web/e2e
git commit -m "test(web): e2e de criacao de usuario e concessao de empresa

Cobre o caso de uso que faltava de ponta a ponta: criar conta, verificar
que nasce sem acesso e conceder a primeira empresa.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 10: documentação de módulo e backlog

**Arquivos:**
- Modificar: `docs/modules/access-control.md`
- Modificar: `docs/modules/organizations.md`
- Modificar: `docs/specs/backlog.md`

- [ ] **Passo 1: atualizar `docs/modules/access-control.md`**

- em **Escopo**, adicione: `Criação de usuário pela aplicação (senha inicial
  definida pelo administrador).`
- em **Fora de Escopo**, troque a linha `Convite e cadastro de novos usuarios
  (dependem de email transacional).` por `Convite por email e recuperacao de
  senha (dependem de email transacional). A criacao direta de usuario passou a
  ser suportada.`
- em **Casos de Uso**, adicione: `Criar usuario (POST /users).`
- em **Regras de Negocio**, adicione: `Usuario criado nasce Active, sem perfis e
  sem empresas — permissao efetiva vazia ate ser configurado. E-mail e unico
  globalmente; colisao responde mensagem neutra.`
- em **Autorizacao e Tenancy**, atualize a descrição de `users.manage` para
  incluir "criar".
- em **Eventos de Auditoria**, adicione `access_control.user.created`.

- [ ] **Passo 2: atualizar `docs/modules/organizations.md`**

- em **Escopo**, adicione: `Concessao e revogacao de acesso de usuarios as
  empresas (companies.grant_access).`
- em **Fora de Escopo**, **remova** `UI para conceder/revogar acesso de empresas
  a outros usuarios.`
- em **Casos de Uso**, adicione: `Definir as empresas de um usuario (PUT
  /users/{id}/companies) e os usuarios de uma empresa (GET/PUT
  /companies/{id}/users).`
- em **Regras de Negocio**, adicione o escopo concedível da ADR-0014 e a regra de
  preservação do que está fora do escopo.
- em **Autorizacao e Tenancy**, declare `companies.grant_access`.
- em **Eventos de Auditoria**, adicione
  `organizations.user.company_access_granted` e `..._revoked`.
- em **Decisoes Relacionadas**, adicione a ADR-0014.

- [ ] **Passo 3: atualizar `docs/specs/backlog.md`**

Localize a entrada de e-mail transacional / convite e acrescente que ela passa a
carregar também a **troca obrigatória de senha no primeiro login**: enquanto não
existir, o administrador retém uma credencial válida do usuário que criou, o que
enfraquece o não-repúdio da auditoria (ver "Riscos aceitos" na spec).

- [ ] **Passo 4: commit**

```bash
git add docs
git commit -m "docs(modules): documenta criacao de usuario e concessao de empresa

Atualiza access-control e organizations com os endpoints, permissoes,
regras e eventos novos; o backlog de e-mail transacional passa a carregar
a divida da troca obrigatoria de senha.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Verificação final

- [ ] **Passo 1: backend**

```
& 'C:\Users\sergi\pessoal\seed\.worktrees\user-provisioning\scripts\test.ps1'
```

- [ ] **Passo 2: frontend**

```
npm --prefix apps/web run test
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

- [ ] **Passo 3: e2e**

```
docker compose up -d db api
npm --prefix apps/web run e2e
```

- [ ] **Passo 4:** só depois de ver as três saídas verdes, marque os critérios de
      aceite da spec e use `superpowers:finishing-a-development-branch` para
      decidir a integração. Não afirme conclusão sem a saída dos comandos.
