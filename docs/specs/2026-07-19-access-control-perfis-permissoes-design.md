# Design — Controle de Acesso: Perfis e Permissões

- **Data:** 2026-07-19
- **Status:** Aprovado (brainstorming) — pré-plano
- **Módulo alvo:** `AccessControl` (novo), sobre o módulo `organizations` existente
- **Relacionados:** ADR-0006 (auth/autorização), ADR-0010 (multiempresa),
  ADR-0005 (dados/tenancy), `docs/modules/organizations.md`,
  `docs/specs/backlog.md#gestao-usuarios-perfis-permissoes`

## Contexto e problema

O Seed precisa de um cadastro de **usuários** e de **perfis de usuário** por
organização, em que cada perfil carrega um conjunto de **permissões** e o usuário
é vinculado a um ou mais perfis.

O modelo hoje implementado (ADR-0010 + módulo `organizations`) usa papéis fixos
de gestão em `ApplicationUser.orgRole` (`Admin` | `Member`) e concessão de
empresa via `UserCompanyAccess`. A ADR-0006 previa papéis fixos
(`owner`/`admin`/`member`) e deixava **explicitamente** fora do MVP as permissões
configuráveis e granulares. Este design substitui esses papéis fixos por um
modelo de **perfis configuráveis com permissões**, e portanto **exigirá uma nova
ADR** que substitui/estende a ADR-0006 (racional, tradeoffs e migração). Tem peso
alto de **segurança + arquitetura**.

## Decisões-chave (travadas no brainstorming)

1. **Perfis substituem papéis.** `orgRole` (`Admin`/`Member`) sai. Toda
   autorização funcional passa a vir de perfis. `owner` deixa de ser papel e vira
   um flag técnico (`is_owner`) de dono da organização.
2. **Abordagem de representação:** `Permission` é entidade no banco, mas o
   **código é a fonte de verdade**; a tabela é uma **projeção reconciliada no
   boot** (upsert idempotente). A foreign key `ProfilePermission → Permission` é
   a trava contra permissões "fantasma".
3. **Dois eixos ortogonais de autorização**, avaliados juntos:
   - **Funcional** — o que a pessoa pode fazer → vem dos **perfis**, escopo
     **organização** (por ora igual para todas as empresas).
   - **Dados/empresa** — onde ela pode fazer → vem de `UserCompanyAccess` (já
     existe no módulo `organizations`).
4. **Granularidade v1:** acesso **por funcionalidade** (item de menu). As camadas
   de **ação** (`ver`/`criar-editar`/`excluir`) e de **posse/field-level**
   (editar só o que criou; editar campo específico) ficam **desenhadas mas
   adiadas** — o modelo não as impede.
5. **Vários perfis por usuário**; permissão efetiva = **união** das permissões
   dos perfis vinculados.
6. **Catálogo fixo no código:** cada módulo declara suas `permission_key`
   estáveis; o admin só escolhe quais um perfil concede, não inventa chaves.
7. **Bootstrap:** toda organização nasce com um perfil-semente **"Administrador"**
   (`is_system`, todas as permissões), atribuído ao owner. Perfis-modelo
   adicionais ficam para evolução futura.
8. **Lado usuários (v1):** listar membros, atribuir/remover perfis, ativar/
   desativar. **Convite por email fica adiado** (depende de estratégia de email
   transacional — bloqueio herdado da ADR-0006/ADR-0010).

## Modelo de dados

### Entidades novas (módulo `AccessControl`)

- **`Permission`** — projeção do catálogo do código. Global à instância (sem
  `organization_id`).
  - `key` (única, estável — ex.: `tasks.access`)
  - `module` (agrupador para a UI — ex.: `tasks`)
  - `display_name`, `description` (português — audiência de produto, ADR-0004)
  - `status` (`active` | `obsolete`)
- **`Profile`** — perfil configurável, **escopo organização**.
  - `id`, `organization_id` (FK, tenancy)
  - `name`, `description`
  - `is_system` (bool — marca o perfil-semente "Administrador")
  - `status` (`active` | `archived`) — segue ADR-0005 (estado, não exclusão)
  - Único por (`organization_id`, `name`).
- **`ProfilePermission`** — M:N `Profile` ↔ `Permission`.
  - `profile_id` (FK), `permission_key` (FK → `Permission.key` — a trava)
- **`UserProfile`** — M:N `ApplicationUser` ↔ `Profile`.
  - `application_user_id` (FK), `profile_id` (FK)
  - Permissão efetiva do usuário = união das permissões dos perfis `active`
    vinculados.

### Mudança no existente

- `ApplicationUser` ganha `is_owner` (bool). O `orgRole` (`Admin`/`Member`) é
  **removido** ao final da migração (ver "Migração").
- O que hoje é "só `orgRole=Admin` cria empresa" passa a ser a permissão
  `companies.manage`.

### Preparado para evoluir (fora do v1)

- **Perfil por empresa:** futuramente `UserProfile` (ou tabela dedicada) pode
  ganhar `company_id` opcional; enquanto `null`, o perfil vale para toda a
  organização, como agora.
- **Ações e posse/field-level:** novas `permission_key` (ex.: `tasks.create`,
  `tasks.delete`) e regras contextuais entram sem quebrar o modelo.

## Catálogo de permissões e reconciliador

- Cada módulo declara suas permissões localmente (ex.: classe estática com
  constantes + metadados). Um `IPermissionCatalog` central agrega tudo. Cada
  permissão: `key` (`<módulo>.<capacidade>`, imutável), `module`, `display_name`,
  `description`.
- Renomear uma `key` = obsoletar a antiga + criar nova (nunca renomear in-place).
- **Reconciliador no boot** (`IHostedService`/passo de inicialização no
  `Seed.Api`, após migrations, antes de servir tráfego; idempotente, seguro com
  múltiplas réplicas):
  - `key` nova → insere `active`;
  - `key` existente → atualiza `module`/`display_name`/`description`;
  - `key` sumida do código → `status = obsolete` (não deleta — preserva
    `ProfilePermission` e não quebra FK);
  - `key` obsoleta reaparecida → volta a `active`.
- **Efeito de `obsolete`:** permanece no banco e em perfis que já a tinham, mas
  não aparece no seletor de novos perfis e é **ignorada no enforcement**.

### Permissões-semente do módulo `AccessControl`

Três permissões **distintas** (isoláveis em perfis diferentes):

- `profiles.manage` — criar/editar/excluir perfis e **definir as permissões** de
  cada perfil.
- `profiles.assign` — **atribuir/remover** perfis dos usuários.
- `users.manage` — gerir usuários (listar, ativar/desativar).

(O módulo `organizations` passa a declarar `companies.access` — ver/acessar a
funcionalidade de empresas — e `companies.manage` — criar/editar/excluir. A
visibilidade continua também condicionada ao eixo de empresa via
`UserCompanyAccess`.)

## Enforcement

Autorização no **backend**, camada de application (ADR-0006). Frontend só
esconde por UX; nunca é a barreira.

- **Resolução da permissão efetiva (por request):** a partir do cookie httpOnly,
  resolve `ApplicationUser` + organização; permissão efetiva funcional = união
  das `permission_key` **`active`** dos perfis `active` vinculados. Permissões/
  perfis não-`active` são ignorados.
- **Owner:** `is_owner` recebe o conjunto funcional **completo** (bypass), mas
  continua sujeito ao eixo de empresa.
- **Dois eixos, juntos:**
  1. Funcional — "tem a permissão `X`?" via *authorization policy* parametrizada
     por `key` (`IAuthorizationRequirement` + handler; helper ergonômico do tipo
     `[RequirePermission("tasks.access")]`).
  2. Empresa — filtro `UserCompanyAccess` existente; sem concessão ou cross-tenant
     → **404** (não vaza existência — ADR-0010).
- **Cache:** curto **por request** é permitido; **entre requests não**, para
  manter revogação imediata (ADR-0006).
- **Usuário desativado** → bloqueado imediatamente, independente de perfil.
  **Sem perfil** → zero permissão funcional (salvo bypass de owner).
- `GET /auth/me` passa a devolver `permissions` (keys efetivas) + empresas
  acessíveis, para o frontend esconder menus/ações (espelho de UX).

## APIs / endpoints

Todos sob a organização da sessão (isolamento por `organization_id`);
`organization_id`/`company_id` nunca vêm do corpo como fonte de verdade; recurso
de outra org → 404.

**Catálogo** (exige `profiles.manage`)
- `GET /permissions` — lista `Permission` `active` agrupadas por `module`.

**Perfis** (CRUD — exige `profiles.manage`)
- `GET /profiles` — perfis da org (+ contagem de usuários).
- `GET /profiles/{id}` — detalhe + `permission_key`s concedidas.
- `POST /profiles` — cria (`name`, `description`, `permission_key[]`).
- `PUT /profiles/{id}` — edita nome/descrição/permissões.
- `DELETE /profiles/{id}` — **arquiva** (`status=archived`); bloqueado se
  `is_system`.

**Atribuição de perfis** (exige `profiles.assign`)
- `PUT /users/{id}/profiles` — define o conjunto de perfis do usuário
  (`profile_id[]`).

**Usuários** (exige `users.manage`)
- `GET /users` — membros da org (nome, email, status, perfis, empresas).
- `GET /users/{id}` — detalhe.
- `PATCH /users/{id}/status` — ativar/desativar (soft; bloqueia acesso imediato).

**Sessão** (autenticado)
- `GET /auth/me` — estende com `permissions` + empresas acessíveis.

Guardas de invariante retornam 4xx claro (ver Regras de negócio).

## Regras de negócio (invariantes)

- **Perfil-semente "Administrador":** `is_system`, todas as permissões `active`,
  atribuído ao owner. Não pode ser arquivado nem perder as meta-permissões.
- **Owner:** `is_owner` sempre resolve `profiles.manage`, `profiles.assign` e
  `users.manage`; a atribuição de perfis do owner nunca as remove. Owner tem
  bypass funcional completo, mas continua sujeito ao eixo de empresa
  (`UserCompanyAccess`).
- **Org sempre administrável:** deve existir ≥1 usuário capaz de `profiles.manage`
  (garantido pelo owner). Operações que violariam isso são rejeitadas.
- **Arquivar perfil com usuários vinculados:** permitido. **Mantém o vínculo
  `UserProfile`**, mas o perfil `archived` **deixa de conceder** permissões (o
  enforcement ignora perfis não-`active`). Reversível ao reativar. A UI avisa
  quantos usuários serão afetados.
- **Nome de perfil único por org.** `permission_key` inválida ou `obsolete` é
  rejeitada na criação/edição.
- **Empresa:** conceder/revogar `UserCompanyAccess` permanece governado pelo
  módulo `organizations` (UI de concessão fora do escopo deste design); aqui as
  empresas do usuário são apenas **exibidas**.
- **Member sem perfil:** usuário migrado de `orgRole=Member` fica **sem perfil**
  (zero permissão funcional) até receber um. Não há perfil "Membro" padrão.

## UI (Next.js, shadcn/ui — ADR-0011)

Sob o app shell existente. Duas áreas novas no menu, visíveis conforme permissão:

- **Perfis** (visível com `profiles.manage`): lista (nome, descrição, nº de
  usuários, badge "Sistema"); editor com nome, descrição e **seletor de
  permissões agrupado por `module`** (árvore/checkboxes) alimentado por
  `GET /permissions`. Perfil `is_system` abre em leitura.
- **Usuários** (visível com `users.manage`): lista (nome, email, status, perfis
  como chips, empresas acessíveis); detalhe com ativar/desativar, **atribuição de
  perfis** (multiseleção — habilitada só com `profiles.assign`) e empresas
  acessíveis (leitura).

**Adaptação mobile:** segue a convenção registrada no `apps/web` (tabelas → cards/
listas). O seletor de permissões em árvore precisa de tratamento responsivo
específico (ponto de atenção para a implementação).

## Migração (a partir do estado da ADR-0010)

Em **duas fases** para não quebrar o seed existente (org Demo + admin + member):

1. **Fase 1 — cria estruturas e popula perfis:**
   - migration cria `Permission`, `Profile`, `ProfilePermission`, `UserProfile`;
     adiciona `is_owner` a `ApplicationUser`;
   - reconciliador popula `Permission` a partir do catálogo do código;
   - data migration: cada org ganha o perfil "Administrador" (`is_system`, todas
     as permissões); usuários `orgRole=Admin` → `is_owner=true` + vínculo ao
     "Administrador"; usuários `orgRole=Member` → **sem perfil**.
2. **Fase 2 — remove `orgRole`:** após os dados migrados e validados, migration
   que dropa a coluna `orgRole`.

**Consequência assumida:** usuários migrados de `orgRole=Member` (sem perfil)
perdem a permissão funcional de ver empresas (`companies.access`) até receberem
um perfil que a conceda — mesmo que ainda tenham `UserCompanyAccess`. É esperado
(decisão de "nenhuma permissão padrão para membro"); na prática, após a migração
o owner atribui perfis aos membros. Um perfil simples de "acesso a empresas" pode
ser criado manualmente na org, se desejado.

## Testes obrigatórios

Herda a lista da ADR-0006 e acrescenta específicos:

- acesso permitido / negado / **cross-tenant** (404, sem vazar existência);
- permissão efetiva = união de vários perfis; perfil `archived` deixa de conceder
  imediatamente;
- **dois eixos:** ter a permissão funcional mas sem `UserCompanyAccess` →
  bloqueado na empresa (404);
- **revogação imediata:** remover perfil/permissão bloqueia no próximo request
  (sem cache entre requests);
- **invariantes:** não arquivar `is_system`; owner não perde as três
  meta-permissões; org sempre com ≥1 usuário `profiles.manage`;
- as três permissões `profiles.manage`/`profiles.assign`/`users.manage` realmente
  separam capacidades (perfil só com `profiles.assign` atribui mas não redefine
  permissões);
- **reconciliador idempotente:** `key` sumida → `obsolete` e ignorada; reaparecida
  → `active`;
- **FK** barra `permission_key` inexistente em `ProfilePermission`;
- integração com Postgres real (Testcontainers), como no módulo `organizations`.

## Trabalho de decisão pendente (fora deste design)

- **Nova ADR** substituindo/estendendo a ADR-0006 (perfis configuráveis no lugar
  de papéis fixos; racional, tradeoffs, impacto de migração).
- Revisão de **segurança** (`security-engineer`) antes de virar plano de
  implementação — este design toca o coração da autorização.
- Documentação do módulo `AccessControl` em `docs/modules/` (padrão ADR-0008).

## Fora de escopo (v1)

- Convite/ativação por email (depende de email transacional).
- Permissões por ação e regras de posse/field-level (desenhadas, adiadas).
- Perfil escopado por empresa (preparado, não implementado).
- UI de conceder/revogar acesso de empresa (pertence ao módulo `organizations`).
- Perfis-modelo adicionais além do "Administrador".
