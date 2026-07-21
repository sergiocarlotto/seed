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
  empresas concedidas a ele.
- Declaracao das permissoes `companies.access` e `companies.manage` (quem pode
  ver e quem pode gerir empresas). Os papeis fixos `Admin`/`Member` **sairam**:
  a autorizacao funcional vem de perfis (ADR-0012, modulo `access-control`).
- Enforcement de isolamento (organizacao) e de acesso (empresa) no backend.
- Provisionamento inicial por seed (organizacao Demo + admin + empresa) no MVP.

## Fora de Escopo

- Auto-cadastro de organizacao (organizacoes sao provisionadas por nos;
  super-admin no futuro).
- Convite e gestao de usuarios (dependem de email transacional).
- UI para conceder/revogar acesso de empresas a outros usuarios.
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

## Regras de Negocio

- Isolamento duro por `organizationId`: nada cruza organizacoes.
- Visibilidade de empresa sempre explicita via `UserCompanyAccess` — inclusive
  para quem tem `companies.manage`.
- Só quem tem `companies.manage` cria/edita/exclui empresa; ao criar, o criador
  recebe acesso. Ver a lista exige `companies.access`.
- Os dois eixos sao independentes: a permissao funcional nao dispensa a concessao
  de acesso a empresa, nem o contrario.
- Acesso a empresa sem concessao ou de outra organizacao responde 404.

## Autorizacao e Tenancy

- Permissoes declaradas: `companies.access` (ver as empresas concedidas) e
  `companies.manage` (criar/editar/excluir). Quem as concede sao os perfis do
  modulo `access-control` (ADR-0012); o owner tem bypass funcional, mas continua
  sujeito a concessao de empresa.
- Toda leitura/escrita filtra pela organizacao do usuario e pela concessao de
  acesso; o `organizationId`/`companyId` nunca vem do frontend como fonte de verdade.
- Sessao em cookie httpOnly/SameSite=Lax/Secure(em prod) — ADR-0006.

## Criterios de Aceite

Status: **atendidos** (implementado e verificado em 2026-07-18 na branch
`feat/organizations-login-empresa`).

- [x] Seed cria organizacao Demo + admin (`admin@demo.local`) + empresa concedida.
- [x] Login por email+senha (cookie httpOnly); `/auth/me` sem sessao = 401.
- [x] Usuario so ve/edita empresas concedidas, dentro da sua organizacao.
- [x] Acesso a empresa de outra org ou sem concessao = 404.
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
  dos eventos de auditoria de empresa e um proximo incremento.

## Dependencias

- Modulos: `access-control` — passou a depender dele para o gate funcional das
  empresas (as permissoes que este modulo declara so sao concedidas por perfis).
- ADRs: 0003 (camadas), 0005 (dados/tenancy), 0006 (auth/cookie), 0008 (doc de
  modulo), 0010 (modelo multiempresa), 0011 (UI/shadcn), 0012 (perfis), 0013
  (auditoria).

## Validacao Esperada

- Unit: regras de acesso do `CompanyService`.
- Integracao: login (seed), `me`, CRUD de empresa, acesso explicito, cross-tenant
  (404), sem permissao (403), usuario desativado bloqueado, com Postgres real
  (Testcontainers).

## Decisoes Relacionadas

- Design: `docs/specs/2026-07-18-organizations-multiempresa-design.md`.
- Plano: `docs/plans/2026-07-18-organizations-multiempresa-rework.md`.
- ADR-0010 (multiempresa), ADR-0011 (UI), ADR-0012 (perfis substituem `orgRole`),
  ADR-0013 (auditoria), ADR-0005/0006/0003/0008.
