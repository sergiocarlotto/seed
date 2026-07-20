# Design — Frontend de Controle de Acesso: Perfis e Usuários

- **Data:** 2026-07-20
- **Status:** Aprovado (brainstorming) — pré-plano
- **Área:** `apps/web` (Next.js 16, shadcn/ui — ADR-0002, ADR-0011)
- **Backend consumido:** módulo `AccessControl` (ADR-0012), já implementado e testado
- **Relacionados:**
  - `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md` (design de backend — seção "UI")
  - ADR-0012 (perfis configuráveis), ADR-0006 (auth/cookie), ADR-0010 (multiempresa)
  - `apps/web/CLAUDE.md` (Next.js 16 + convenção mobile do app shell)

## Contexto e problema

O backend da ADR-0012 está pronto: perfis configuráveis com permissões por
organização substituíram os papéis fixos (`orgRole`) da ADR-0006. Os endpoints de
`AccessControl` existem e o `GET /auth/me` foi estendido. **Falta a interface.**

Além das duas telas novas (Perfis, Usuários), há um problema imediato: **o
frontend ainda está no modelo antigo** e quebraria contra o backend atual —
`lib/types.ts` declara `Me.orgRole`, `lib/nav.ts` filtra o menu por `OrgRole`, e
`companies/page.tsx` usa `orgRole === "Admin"`. O backend já **dropou `orgRole`**
(migration `DropOrgRole`); o `/auth/me` agora devolve `isOwner` + `permissions`.
Re-sincronizar a sessão é, portanto, pré-requisito inseparável deste trabalho.

O frontend é **espelho de UX**: esconde menus/ações conforme permissão para uma
experiência limpa, mas **o backend continua a única barreira real** (ADR-0006).
Nada aqui re-implementa enforcement.

## Contratos consumidos (confirmados no backend)

Serialização camelCase (padrão ASP.NET Core; consistente com os tipos já usados).

- `GET /auth/me` →
  `{ user: { id, email, fullName }, organizationId, isOwner: boolean,
     permissions: string[], companies: Company[] }`
  (empresas já vêm gateadas por `companies.access` no backend).
- `GET /permissions` (exige `profiles.manage`) → `PermissionGroup[]`:
  `[{ module: string, permissions: [{ key, displayName, description }] }]`.
- `GET /profiles` (exige `profiles.manage`) → `ProfileSummary[]`:
  `{ id, name, description, isSystem: boolean, status: string, userCount: number }`.
- `GET /profiles/{id}` → `ProfileDetail`:
  `{ id, name, description, isSystem, status, permissionKeys: string[] }`.
- `POST /profiles` / `PUT /profiles/{id}` → corpo
  `{ name, description?, permissionKeys?: string[] }`; retorna o perfil.
- `DELETE /profiles/{id}` → 204 (arquiva; 400 se `is_system`).
- `GET /users` (exige `users.manage`) → `UserDto[]`:
  `{ id, fullName, email, status, isOwner,
     profiles: [{ id, name }], companies: [{ id, name }] }`.
- `GET /users/{id}` → `UserDto`.
- `PATCH /users/{id}/status` (exige `users.manage`) → corpo `{ active: boolean }`;
  retorna `UserDto`. Recusa (4xx) desativar o owner.
- `PUT /users/{id}/profiles` (exige `profiles.assign`) → corpo
  `{ profileIds: string[] }`; retorna `UserDto`. `409` em corrida de atribuição.

Chaves de permissão relevantes no catálogo atual: `companies.access`,
`companies.manage`, `profiles.manage`, `profiles.assign`, `users.manage`.

## Decisões-chave (travadas no brainstorming)

1. **Abordagem de dados (A):** páginas de lista/detalhe são **Server Components**
   que fazem prefetch via um `serverGet<T>(path)` genérico (repassando o cookie,
   como o layout do `(app)` já faz); mutações via `api` client + `router.refresh()`
   (padrão da tela de Empresas). Sem flash de loading no first paint; sem inventar
   padrão novo (nem Server Actions, nem tudo client-side).
2. **Seletor de permissões: acordeão por módulo.** Cada `module` do
   `GET /permissions` é uma seção recolhível; o checkbox do cabeçalho marca/
   desmarca o módulo inteiro e exibe estado **indeterminado** quando parcial.
   Abre expandido. No mobile, empilha naturalmente.
3. **Editor e detalhe são páginas cheias** (`/profiles/[id]`, `/users/[id]`),
   consistentes com `/companies/[id]` — não sheets/dialogs. O seletor de
   permissões é grande e pede espaço de página.
4. **Gating de UX = `isOwner || permissions.includes(key)`** (owner tem bypass,
   espelhando o backend). Vale para menu, botões e controles.
5. **Postura B refletida na UI:** no checklist de perfis do usuário, um perfil
   `is_system` só fica habilitado se o operador é `isOwner`. Sem `profiles.assign`,
   o checklist inteiro fica desabilitado (leitura).
6. **Owner é somente-leitura na app:** switch de status desabilitado; perfis não
   editáveis.

## Fatiamento da implementação

Um spec, **três fatias** em ordem (a Fatia 0 é bloqueante para as demais):

### Fatia 0 — Re-sync da sessão (fundação)

Corrige a dessincronia com o backend. Sem features novas de tela.

- `lib/types.ts`: `Me` passa a ser
  `{ user: User; organizationId: string; isOwner: boolean; permissions: string[];
     companies: Company[] }`. Remove `orgRole`.
- `lib/session.tsx`: `useSession()` mantém o `Me`; adiciona helpers ergonômicos
  `has(key: string): boolean` (= `me.isOwner || me.permissions.includes(key)`) e
  exposição de `isOwner`. (Helpers como funções puras testáveis em `lib/`.)
- `lib/nav.ts`: `NavItem.roles?: OrgRole[]` → `NavItem.permission?: string`
  (ausente = visível para todos). `visibleNav(modules, gate)` onde
  `gate = { isOwner, permissions }` (ou recebe o predicado `has`). Remove o tipo
  `OrgRole`. Adiciona os itens **Perfis** (`permission: "profiles.manage"`) e
  **Usuários** (`permission: "users.manage"`) no módulo "Administração".
- `components/shell/AppSidebar.tsx`: consumir o novo `visibleNav` via `has`.
- `companies/page.tsx`: `isAdmin` (via `orgRole`) → `canManage = has("companies.manage")`.
- **Testes:** reescrever `lib/nav.test.ts` para o gating por permissão (owner vê
  tudo; usuário sem a chave não vê o item; item sem `permission` sempre visível).

### Fatia 1 — Perfis

Rota `/profiles` (lista), `/profiles/new` e `/profiles/[id]` (editor).

- **Lista** (Server Component, prefetch `GET /profiles`): tabela
  Nome · Descrição · Usuários · Tipo · Ações. Badge "Sistema" para `isSystem`,
  "Custom" caso contrário. `isSystem` → só **Ver**; custom → **Editar** e
  **Arquivar**. Botão "Novo perfil" no topo. Vazio → `EmptyState`.
- **Arquivar:** `Dialog` de confirmação que informa `userCount` afetado; chama
  `DELETE /profiles/{id}`; `router.refresh()`. 400 (é `is_system`) → mensagem.
- **Editor** (página cheia, cliente; padrão `CompanyForm`): campos Nome,
  Descrição e o **seletor em acordeão**. `new` = `POST`; `[id]` = prefetch
  `GET /profiles/{id}` (RSC) + `PUT`. `isSystem` abre em **leitura** (campos e
  seletor desabilitados).
- **Seletor de permissões** (componente próprio, ex.: `PermissionTree`):
  - entrada: `PermissionGroup[]` (de `GET /permissions`) + `selected: Set<string>`;
  - por módulo: cabeçalho com checkbox tri-estado (marcado / indeterminado /
    vazio) + lista de permissões (checkbox + `displayName` + `key` em
    `text-muted`);
  - marcar/desmarcar o cabeçalho alterna todas as chaves do módulo;
  - saída: `permissionKeys[]` para o `POST`/`PUT`.
  - **Lógica pura isolada** (marcar módulo, derivar estado tri-estado) para
    testar em vitest sem render.

### Fatia 2 — Usuários

Rota `/users` (lista) e `/users/[id]` (detalhe).

- **Lista** (Server Component, prefetch `GET /users`): tabela
  Nome (+ chip "Owner" se `isOwner`) · Email · Perfis (chips) · Empresas
  (nomes) · Status (badge Ativo/Inativo) · **Ver**. Sem perfil → "— sem perfil —".
- **Detalhe** (`/users/[id]`, prefetch `GET /users/{id}`; página cheia):
  - **Status:** `Switch` ativar/desativar → `PATCH /users/{id}/status`
    `{ active }`. Desabilitado se `isOwner`. Requer `has("users.manage")`.
  - **Perfis:** checklist dos perfis `active` da org (carrega `GET /profiles` e
    marca os do usuário). Salvar → `PUT /users/{id}/profiles { profileIds }`.
    Item `is_system` só habilitado se `isOwner` (postura B). Checklist inteiro
    desabilitado sem `has("profiles.assign")`. `409` → aviso "recarregue".
  - **Empresas:** lista só-leitura (nomes). Nota: concessão vive no módulo de
    Empresas.
  - Owner → status e perfis não editáveis (somente leitura).

## Componentes e arquivos

**Primitivos shadcn a adicionar** (via CLI shadcn, base-ui): `checkbox`, `switch`,
`badge`. Reuso: `EmptyState`, `ErrorState`, `Loading`, `NoAccess`, `Dialog`,
`Table`, `Button`, `Card`, `Label`, `Input`.

**Novos (frontend):**
- `app/(app)/profiles/page.tsx`, `profiles/new/page.tsx`, `profiles/[id]/page.tsx`
- `app/(app)/users/page.tsx`, `users/[id]/page.tsx`
- `components/ProfileForm.tsx`, `components/PermissionTree.tsx`
- `components/UserProfilesForm.tsx` (checklist + salvar)
- helpers de árvore de permissão em `lib/` (lógica pura testável)

**Alterados:** `lib/types.ts`, `lib/session.tsx`, `lib/nav.ts`,
`components/shell/AppSidebar.tsx`, `app/(app)/companies/page.tsx`,
`lib/nav.test.ts`. (`lib/api-server.ts` já tem `serverGet<T>(path)` genérico —
nenhuma mudança necessária.)

## Tipos (frontend, em `lib/types.ts`)

Espelham os DTOs do backend:

```ts
type PermissionItem = { key: string; displayName: string; description: string };
type PermissionGroup = { module: string; permissions: PermissionItem[] };
type ProfileSummary = { id: string; name: string; description: string;
  isSystem: boolean; status: string; userCount: number };
type ProfileDetail = { id: string; name: string; description: string;
  isSystem: boolean; status: string; permissionKeys: string[] };
type Ref = { id: string; name: string };
type UserRow = { id: string; fullName: string; email: string; status: string;
  isOwner: boolean; profiles: Ref[]; companies: Ref[] };
```

## Mobile (convenção `apps/web`)

- Tabelas (Perfis, Usuários) → **cartões empilhados** abaixo de `md`.
- Formulários (editor de perfil, detalhe de usuário) → **uma coluna**.
- Ações secundárias → menu "⋯".
- O acordeão de permissões já empilha; garantir alvos de toque confortáveis.
- Primitivo genérico de tabela responsiva **não** será criado agora (YAGNI);
  cada tela resolve seu mobile localmente, como manda o `apps/web/CLAUDE.md`.

## Tratamento de erros

- **403** (sem permissão para o endpoint) → `NoAccess` na página. O menu já
  esconde o item, mas o acesso direto por URL é possível; a tela degrada.
- **404** (recurso de outra org / inexistente) → `notFound()` do Next.
- **409** (corrida de atribuição de perfis) → aviso inline "o usuário mudou,
  recarregue" + `router.refresh()`.
- **400** (validação: nome duplicado, arquivar `is_system`, chave inválida) →
  mensagem inline a partir de `errorMessage(err)`.

## Testes

- **vitest (lógica pura):**
  - `visibleNav` com gating por permissão (owner vê tudo; sem a chave não vê;
    item sem `permission` sempre visível);
  - helper de árvore de permissão (marcar/desmarcar módulo; derivação do estado
    tri-estado marcado/indeterminado/vazio; união de chaves).
- **Playwright e2e (fumaça, caminhos felizes):**
  - listar perfis; criar um perfil com permissões; ver que aparece na lista;
  - listar usuários; abrir detalhe; atribuir um perfil e salvar.
  - Reutilizar o padrão dos specs existentes (`e2e/shell.spec.ts`,
    `e2e/smoke.spec.ts`).
- **Não** re-testar enforcement no frontend — é responsabilidade do backend
  (46 testes de integração já cobrem os dois eixos e a anti-escalada).

## Fora de escopo (v1)

- Convite/ativação de usuário por email (depende de email transacional).
- UI de conceder/revogar acesso de **empresa** (pertence ao módulo de Empresas).
- Permissões por ação / field-level; perfil escopado por empresa.
- Visualizador/gerenciador de **auditoria** (eventos já são emitidos no backend;
  a UI de consulta fica no backlog).
- Perfis-modelo além do "Administrador".

## Trabalho relacionado (fora deste design)

- Documentação do módulo `AccessControl` em `docs/modules/` (padrão ADR-0008) —
  pendência de backend já registrada.
- ADR formal substituindo a ADR-0006 e ADR de padronização do `AuditEvent` —
  pendências já listadas no design de backend.
