# Modulo: organizations

## Objetivo

Estabelecer a base de tenancy do Seed: organizacoes (empresas), usuarios,
vinculo usuario-organizacao com papel, e autenticacao por email+senha. E o
primeiro modulo de negocio; todos os outros dependem dele para isolar dados por
organizacao.

## Escopo

- Cadastro (register) que cria uma organizacao e o usuario owner.
- Login, logout e consulta da sessao atual (email+senha, cookie httpOnly).
- CRUD de empresa (`Organization`) restrito as organizacoes do usuario logado.
- Papeis por organizacao: owner, admin, member.
- Enforcement de tenant e autorizacao no backend.

## Fora de Escopo

- Convite de usuarios e recuperacao de senha (dependem de email transacional).
- Permissoes granulares por campo/recurso.
- Multiplos usuarios por organizacao geridos por UI (o modelo suporta; a UI do
  MVP foca no owner que cria/gerencia suas organizacoes).

## Entidades Envolvidas

- `Organization` (`organization_id` e a raiz de tenancy — ADR-0005): nome, status,
  timestamps, soft delete.
- `OrganizationMembership`: vinculo usuario-organizacao, com papel e status.
- `ApplicationUser` (ASP.NET Core Identity): email, senha (hash), nome.
- `AuditEvent`: eventos de criacao/edicao/exclusao (ADR-0005).

## Casos de Uso

- Registrar (cria organizacao + usuario owner e autentica).
- Login / logout / consultar sessao (`/auth/me`).
- Listar, criar, ver, editar e excluir (soft delete) organizacoes do usuario.

## Regras de Negocio

- Criar organizacao torna o usuario `owner` dela.
- Editar exige papel `owner` ou `admin`; excluir exige `owner`.
- Exclusao e sempre soft delete; a organizacao some das listagens, nao do banco.
- Acesso por id a organizacao da qual o usuario nao e membro responde 404 (nao
  vaza existencia).

## Autorizacao e Tenancy

- Papeis: owner (total), admin (edita), member (visualiza) — ADR-0006.
- Toda leitura/escrita passa por verificacao de `OrganizationMembership` do
  usuario atual; o `organization_id` nunca vem do frontend como fonte de verdade.
- Sessao em cookie httpOnly/SameSite=Lax/Secure(em prod) — ADR-0006.

## Criterios de Aceite

Status: **atendidos** (implementado e verificado em 2026-07-18, na branch
`feat/organizations-login-empresa`).

- [x] Cadastro cria organizacao + owner e autentica.
- [x] Login define cookie httpOnly; logout limpa a sessao; `/auth/me` sem sessao = 401.
- [x] Usuario so ve/edita organizacoes das quais e membro; cross-tenant = 404.
- [x] owner cria/edita/exclui; admin edita; exclusao e soft delete.
- [x] Testes de integracao (auth + CRUD + cross-tenant) verdes (6/6).
- [x] Frontend: login, registro e CRUD de empresa funcionam via Docker (same-origin).

## Eventos de Auditoria

- `organization.created`, `organization.updated`, `organization.deleted`
  (emitidos pela camada de application — ADR-0005).

## Dependencias

- Modulos: nenhum (e a base).
- ADRs: 0003 (backend em camadas), 0005 (dados/tenancy/auditoria),
  0006 (auth/papeis/cookie), 0008 (padrao deste documento).

## Validacao Esperada

- Unit: regras de papel/tenant do `OrganizationService`.
- Integracao: register/login/me, CRUD e tentativa cross-tenant, com Postgres
  real (Testcontainers).

## Decisoes Relacionadas

- Plano de implementacao detalhado: `docs/plans/2026-07-18-organizations-login-empresa.md`.
- ADR-0006 (auth), ADR-0005 (dados), ADR-0003 (camadas), ADR-0008 (doc de modulo).
