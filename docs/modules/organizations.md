# Modulo: organizations

## Objetivo

Estabelecer a base de tenancy multiempresa do Seed: organizacoes (o tenant),
empresas (varias por organizacao), usuarios (pertencem a uma organizacao) e o
acesso explicito de cada usuario as empresas. E o primeiro modulo de negocio;
todos os outros dependem dele para isolar dados por organizacao e por empresa.

Modelo detalhado: ADR-0010 e
`docs/specs/2026-07-18-organizations-multiempresa-design.md`.

## Escopo

- Login, logout e consulta da sessao (`/auth/me`) por email+senha, cookie httpOnly.
- CRUD de empresa (`Company`) dentro da organizacao do usuario, restrito as
  empresas concedidas a ele (o owner alcanca toda a propria organizacao —
  ADR-0014).
- Concessao e revogacao do acesso de usuarios as empresas
  (`companies.grant_access`), nas duas direcoes: pela tela do usuario e pela tela
  da empresa, sobre um unico servico.
- Declaracao das permissoes `companies.access`, `companies.manage` e
  `companies.grant_access` (quem pode ver, quem pode gerir e quem decide quem
  entra na empresa). Os papeis fixos `Admin`/`Member` **sairam**:
  a autorizacao funcional vem de perfis (ADR-0012, modulo `access-control`).
- Enforcement de isolamento (organizacao) e de acesso (empresa) no backend.
- Provisionamento inicial por seed (organizacao Demo + admin + empresa) no MVP.

## Fora de Escopo

- Auto-cadastro de organizacao (organizacoes sao provisionadas por nos;
  super-admin no futuro).
- Convite por email e recuperacao de senha (dependem de email transacional). A
  gestao de usuarios (criar, ativar/desativar, perfis) e do modulo
  `access-control`.
- Campos ricos da empresa (CNPJ, endereco, etc.).

## Entidades Envolvidas

- `Organization` (raiz de tenancy — ADR-0005/ADR-0010): `name`, `status`.
- `Company` (empresa): `organizationId`, `name`, `status`, soft delete. Varias por org.
- `ApplicationUser` (Identity): `email`, `passwordHash`, `fullName`,
  `organizationId`, `isOwner`, `status` (o antigo `orgRole` foi removido pela
  ADR-0012).
- `UserCompanyAccess`: concessao explicita `userId` -> `companyId` (com `organizationId`).
- `AuditEvent`: eventos de negocio (ADR-0005).

## Casos de Uso

- Login / logout / consultar sessao (`me` retorna usuario + organizacao + empresas
  acessiveis).
- Listar (minhas empresas), criar (com `companies.manage`; auto-concede), ver,
  editar e excluir (soft delete) empresas.
- Definir o conjunto de empresas acessiveis por um usuario
  (`PUT /users/{id}/companies`) — a visao "pela pessoa".
- Listar os usuarios da organizacao marcando quem tem acesso a uma empresa e
  definir esse conjunto (`GET /companies/{id}/users`, `PUT /companies/{id}/users`)
  — a visao "pela empresa". O `GET` devolve so `id`, `fullName`, `email` e
  `hasAccess`: o minimo para escolher, sem o restante do cadastro.

## Regras de Negocio

- Isolamento duro por `organizationId`: nada cruza organizacoes.
- Visibilidade de empresa sempre explicita via `UserCompanyAccess` — inclusive
  para quem tem `companies.manage`. **Excecao: o owner** enxerga todas as
  empresas da propria organizacao, com ou sem concessao (ADR-0014); o filtro por
  organizacao nesse ramo vem sempre da sessao e e o que separa "dono da
  organizacao" de "ve tudo no banco".
- Só quem tem `companies.manage` cria/edita/exclui empresa; ao criar, o criador
  recebe acesso. Ver a lista exige `companies.access`.
- Os dois eixos sao independentes: a permissao funcional nao dispensa a concessao
  de acesso a empresa, nem o contrario.
- Acesso a empresa sem concessao ou de outra organizacao responde 404 (para o
  owner, so o caso "de outra organizacao" sobra).
- **Escopo concedivel do chamador** (ADR-0014) governa toda concessao/revogacao:
  o **owner** alcanca todas as empresas ativas da organizacao; o **nao-owner**,
  apenas as empresas do proprio `UserCompanyAccess`. Quem nao acessa nenhuma
  empresa nao concede nenhuma.
- Empresa fora do escopo concedivel (inclusive de outra organizacao ou
  soft-deleted) responde **404, nunca 403**: ela ja e indistinguivel de
  inexistente para o chamador, e um 403 revelaria sua existencia.
- `PUT /users/{id}/companies` define o conjunto de empresas do usuario **dentro
  do escopo do chamador**. Concessoes fora desse escopo sao **preservadas**, nao
  removidas por ausencia no payload — a tela do chamador so lista o que ele pode
  conceder. Para o owner, escopo = organizacao inteira, entao o endpoint se
  comporta como um PUT de conjunto completo comum.
- `PUT /companies/{id}/users` exige a empresa da rota no escopo concedivel; dentro
  dela o payload e o conjunto completo, ja que todo usuario da organizacao e alvo
  legitimo.
- **O owner alvo pode ter suas empresas alteradas** — diferente de status e
  perfis, onde e somente-leitura: ele esta sujeito ao eixo de empresa e sempre
  consegue se reconceder. O escopo total do owner e tambem o que destrava uma
  **empresa orfa**, aquela que ficou sem nenhum usuario com acesso.
- Corrida no indice unico `(userId, companyId)` (`23505`) e remocao concorrente
  sao traduzidas para **409**, no mesmo padrao de `SetProfilesAsync`.

## Autorizacao e Tenancy

- Permissoes declaradas: `companies.access` (ver as empresas concedidas),
  `companies.manage` (criar/editar/excluir) e `companies.grant_access` (conceder e
  revogar o acesso de usuarios as empresas). Quem as concede sao os perfis do
  modulo `access-control` (ADR-0012).
- Gates dos endpoints de concessao: `PUT /users/{id}/companies`,
  `GET /companies/{id}/users` e `PUT /companies/{id}/users` exigem
  `companies.grant_access` — inclusive o primeiro, que mora sob a rota `/users`.
- O owner tem bypass funcional (ADR-0012) e, desde a ADR-0014, tambem bypass no
  **eixo de empresa**, limitado a propria organizacao. Os demais usuarios
  continuam sujeitos a concessao explicita.
- Toda leitura/escrita filtra pela organizacao do usuario e pela concessao de
  acesso; o `organizationId`/`companyId` nunca vem do frontend como fonte de verdade.
- Sessao em cookie httpOnly/SameSite=Lax/Secure(em prod) — ADR-0006.

## Criterios de Aceite

Status: **atendidos** (implementado e verificado em 2026-07-18 na branch
`feat/organizations-login-empresa`).

- [x] Seed cria organizacao Demo + admin (`admin@demo.local`) + empresa concedida.
- [x] Login por email+senha (cookie httpOnly); `/auth/me` sem sessao = 401.
- [x] Usuario so ve/edita empresas concedidas, dentro da sua organizacao
      (a partir da ADR-0014, **o owner e excecao**: alcanca toda a propria org).
- [x] Acesso a empresa de outra org ou sem concessao = 404 (idem: para o owner,
      so a outra organizacao responde 404).
- [x] Quem tem `companies.manage` cria empresa e passa a ve-la; sem a permissao,
      403 (era "Admin cria, Member nao" antes da ADR-0012).
- [x] Exclusao de empresa e soft delete.
- [x] Testes de integracao verdes (acesso explicito, cross-tenant, sem permissao,
      usuario desativado bloqueado).
- [x] Frontend (shadcn/ui): login e CRUD de empresa via Docker (same-origin).

## Eventos de Auditoria

- `organizations.company.created`, `organizations.company.updated`,
  `organizations.company.deleted` (emitidos pela camada de application —
  ADR-0005, no padrao da ADR-0013). Nota: o MVP prioriza o enforcement; a emissao
  dos eventos de auditoria **do CRUD de empresa** e um proximo incremento.
- `organizations.user.company_access_granted` e
  `organizations.user.company_access_revoked` — **estes ja sao emitidos**, um por
  empresa tocada, nos dois sentidos. Forma de vinculo da ADR-0013: `company_id`,
  `company_name` e `old`/`new` booleanos no `metadata`. `entity_type` = `"User"` e
  `entity_id` = id do usuario alvo, porque a ADR-0013 pede que a entidade case com
  a `action` — mesmo precedente de `access_control.user.profile_assigned`.

## Dependencias

- Modulos: `access-control` — passou a depender dele para o gate funcional das
  empresas (as permissoes que este modulo declara so sao concedidas por perfis).
- ADRs: 0003 (camadas), 0005 (dados/tenancy), 0006 (auth/cookie), 0008 (doc de
  modulo), 0010 (modelo multiempresa), 0011 (UI/shadcn), 0012 (perfis), 0013
  (auditoria), 0014 (escopo de concessao de acesso a empresa).

## Validacao Esperada

- Unit: regras de acesso do `CompanyService`.
- Integracao: login (seed), `me`, CRUD de empresa, acesso explicito, cross-tenant
  (404), sem permissao (403), usuario desativado bloqueado, com Postgres real
  (Testcontainers).
- Integracao do eixo de concessao, **nas duas direcoes** (usuario→empresa e
  empresa→usuario), como pede a ADR-0014: conceder e revogar com efeito real;
  403 sem `companies.grant_access`; nao-owner fora do proprio escopo = 404;
  concessao fora do escopo preservada; owner alcancando empresa orfa; bypass de
  leitura do owner limitado a propria organizacao; empresa soft-deleted = 404;
  owner alvo editavel; eventos com `old`/`new` corretos.
- E2E (Playwright): criar usuario e conceder-lhe a primeira empresa sobre a stack
  real.

## Decisoes Relacionadas

- Design: `docs/specs/2026-07-18-organizations-multiempresa-design.md`.
- Plano: `docs/plans/2026-07-18-organizations-multiempresa-rework.md`.
- ADR-0010 (multiempresa), ADR-0011 (UI), ADR-0012 (perfis substituem `orgRole`),
  ADR-0013 (auditoria), ADR-0014 (escopo concedivel, postura A no eixo de
  empresa), ADR-0005/0006/0003/0008.
- Design da concessao de acesso:
  `docs/specs/2026-07-21-user-provisioning-company-access-design.md`.
