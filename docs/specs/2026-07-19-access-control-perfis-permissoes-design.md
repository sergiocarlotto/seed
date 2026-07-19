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
   um flag técnico (`is_owner`) de dono da organização, **gerido fora da
   aplicação** (via banco no MVP; superadmin externo no futuro) — a aplicação
   nunca cria, remove ou edita owner.
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

**Aviso de segurança (postura B — ver "Segurança / anti-escalada"):**
`profiles.manage` e `profiles.assign` são, no v1, **privilégios
administrativos de fato**, não capacidades granulares inofensivas. Quem tem
`profiles.manage` pode ampliar as permissões de um perfil que já possui
(auto-escalada pela união); quem tem `profiles.assign` pode conceder perfis
poderosos. Trate ambas como confiança de administrador.

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
  **Recusa (4xx) desativar o owner.**

O owner aparece na listagem, mas é **somente-leitura** (não desativável, perfis
não editáveis pela app). `is_system`/`is_owner`/`organization_id`/`status` de
perfil nunca são aceitos do corpo (allow-list — ver "Segurança / anti-escalada").

**Sessão** (autenticado)
- `GET /auth/me` — estende com `permissions` + empresas acessíveis.

Guardas de invariante retornam 4xx claro (ver Regras de negócio).

## Regras de negócio (invariantes)

- **Perfil-semente "Administrador":** `is_system`, todas as permissões `active`,
  atribuído ao owner. Não pode ser arquivado nem perder as meta-permissões.
- **Owner:** tem bypass funcional completo, mas continua sujeito ao eixo de
  empresa (`UserCompanyAccess`). É **gerido fora da aplicação** (banco no MVP;
  superadmin externo depois).
- **Owner é somente-leitura na app:** a gestão de usuários **não pode**
  desativar o owner nem alterar os perfis dele; `is_owner` nunca é setado via
  API. Como o owner é sempre um administrador ativo garantido, ele é o **piso**
  que impede a organização de "trancar por dentro" (elimina o cenário de
  last-admin lockout sem precisar de guard de contagem). Administradores
  **não-owner** podem ser desativados normalmente.
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

## Segurança / anti-escalada

Sistema de perfis configuráveis tem como risco central a **escalada de
privilégio**. Postura adotada no v1:

- **Postura B — perfis `is_system` só são atribuíveis pelo owner.** O perfil
  "Administrador" (todas as permissões) **não** pode ser atribuído por quem tem
  apenas `profiles.assign`; só o owner o concede. Fecha a escalada trivial de
  "atribuo Administrador a mim mesmo".
- **`profiles.manage` e `profiles.assign` são privilégios administrativos** (ver
  aviso na seção de permissões-semente). O v1 aceita que quem os detém é um
  administrador de confiança; não há tentativa de torná-los granulares seguros
  agora.
- **Allow-list de campos (anti mass-assignment):** os campos `is_system`,
  `status` (de perfil), `organization_id` e `is_owner` **nunca** são aceitos do
  cliente; são definidos apenas pelo backend/seed/reconciliador. Enviá-los no
  corpo é ignorado, nunca escala.
- **Validação de tenant nos vínculos:** `PUT /users/{id}/profiles` rejeita (404)
  qualquer `profile_id` fora da org do chamador e `user id` fora da org.

**Melhoria futura registrada (postura A — não fazer agora):** evoluir para a
regra "não conceder além de si" — um usuário só atribui perfis e define
permissões contidas no seu próprio conjunto efetivo. Torna `profiles.manage`/
`profiles.assign` genuinamente granulares e menos perigosas. Fica no backlog.

## Auditoria

Reusa o `AuditEvent` (ADR-0005). No v1 **emitimos** os eventos (custo baixo,
volume pequeno); o **visualizador/gerenciador de auditoria** fica adiado
(backlog) por exigir cuidado de escala e UI própria.

**Contrato padronizado do `AuditEvent`** (para viabilizar relatórios
transversais, ex.: "tudo que um usuário fez num período"). Campos mínimos
consistentes entre todos os módulos:

- `id`
- `occurred_at` (UTC)
- `organization_id` (escopo de tenant)
- `actor_user_id` (quem executou) — chave dos relatórios por usuário
- `action` — taxonomia `<módulo>.<entidade>.<verbo>` (ex.:
  `access_control.profile.permissions_changed`)
- `target_type` + `target_id` (ex.: `Profile` / `{id}`)
- `details` (payload estruturado, ex.: delta de permissões adicionadas/removidas)

Com `actor_user_id` + `occurred_at` + `organization_id` padronizados, o relatório
"atividade de um usuário num intervalo" é uma consulta simples e indexável.

**Eventos deste módulo a emitir:**

- `access_control.profile.created` / `.updated` / `.archived` (com delta de
  permissões no `updated`);
- `access_control.user.profiles_assigned` (perfis atribuídos/removidos de um
  usuário);
- `access_control.user.activated` / `.deactivated`.

**Nota de escopo:** padronizar o `AuditEvent` afeta **todos** os módulos (não só
este) — é decisão de arquitetura e deve ser ratificada por ADR própria (ver
"Trabalho de decisão pendente"). Este design adota o contrato acima como padrão
de trabalho até a ADR formalizá-lo.

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
- **anti-escalada (postura B):** usuário com só `profiles.assign` **não** atribui
  perfil `is_system` ("Administrador") → negado;
- **owner protegido:** não é possível desativar o owner nem editar seus perfis
  pela app → bloqueado;
- **allow-list:** `is_system`/`is_owner`/`organization_id`/`status` enviados no
  corpo são ignorados (não escalam);
- atribuir `profile_id` de outra org via `PUT /users/{id}/profiles` → 404;
- **auditoria** registra alteração do conjunto de permissões de um perfil com
  `actor_user_id` e delta;
- integração com Postgres real (Testcontainers), como no módulo `organizations`.

## Trabalho de decisão pendente (fora deste design)

- **Nova ADR** substituindo/estendendo a ADR-0006 (perfis configuráveis no lugar
  de papéis fixos; racional, tradeoffs, impacto de migração).
- **ADR de padronização do `AuditEvent`** (afeta todos os módulos): contrato
  `actor_user_id`/`occurred_at`/`organization_id`/`action`/`target`/`details`
  para viabilizar relatórios transversais. Este design já adota o contrato como
  padrão de trabalho.
- Documentação do módulo `AccessControl` em `docs/modules/` (padrão ADR-0008).

**Revisão de segurança:** realizada (`security-engineer`). Achados críticos de
escalada de privilégio resolvidos no design via **postura B** (perfis
`is_system` só atribuíveis pelo owner; `profiles.manage`/`profiles.assign`
tratados como privilégio administrativo), **owner externo somente-leitura** na
app, **allow-list** de campos e **auditoria** das mutações sensíveis. A evolução
para **postura A** ("não conceder além de si") fica no backlog.

## Fora de escopo (v1)

- Convite/ativação por email (depende de email transacional).
- Permissões por ação e regras de posse/field-level (desenhadas, adiadas).
- Perfil escopado por empresa (preparado, não implementado).
- UI de conceder/revogar acesso de empresa (pertence ao módulo `organizations`).
- Perfis-modelo adicionais além do "Administrador".
- **Visualizador/gerenciador de auditoria** (os eventos são emitidos no v1, mas a
  UI de consulta/filtro/relatório fica no backlog — exige cuidado de escala).
- **Postura A anti-escalada** ("não conceder além de si") — melhoria futura.
