# Modulo: access-control

## Objetivo

Controlar **o que** cada usuario pode fazer dentro da sua organizacao por meio de
**perfis configuraveis**: a organizacao monta seus proprios perfis a partir de um
catalogo de permissoes declarado no codigo, e atribui esses perfis aos usuarios.
Substitui os papeis fixos (`Admin`/`Member`) da ADR-0006 pelo modelo da ADR-0012.

O modulo responde por dois eixos independentes de autorizacao:

- **funcional** (este modulo): a permissao vem dos perfis do usuario;
- **empresa** (modulo `organizations`): o acesso vem de `UserCompanyAccess`.

Ter a permissao funcional **nao** dispensa o acesso a empresa, e vice-versa.

Modelo detalhado: ADR-0012 e
`docs/specs/2026-07-19-access-control-perfis-permissoes-design.md`.

## Escopo

- Catalogo de permissoes declarado no codigo e reconciliado no banco no boot.
- CRUD de perfis (`Profile`) por organizacao, com o conjunto de permissoes de cada um.
- Atribuicao de perfis a usuarios.
- Listagem, consulta e ativacao/desativacao de usuarios da organizacao.
- Resolucao da **permissao efetiva** do usuario da sessao e enforcement por
  `[RequirePermission]` nos endpoints.
- Perfil de sistema "Administrador" (`is_system`) garantido por organizacao.
- Data migration do estado da ADR-0010 (`orgRole`) para perfis.

## Fora de Escopo

- **Criar, remover ou editar owner** (`is_owner`): e gerido fora da aplicacao
  (banco no MVP; superadmin externo no futuro). Nenhum endpoint o altera.
- Convite e cadastro de novos usuarios (dependem de email transacional).
- Conceder/revogar acesso a **empresas** (é do modulo `organizations`).
- Permissoes por campo, por registro ou por empresa (fora do MVP).
- UI de consulta dos eventos de auditoria.
- Perfis-modelo prontos alem do "Administrador".

## Entidades Envolvidas

- `Permission`: catalogo global (nao pertence a organizacao). PK `key`
  (ex.: `profiles.manage`), `module`, `display_name`, `description`, `status`
  (`Active`|`Obsolete`). A **fonte de verdade e o codigo**; a tabela e projecao.
- `Profile`: perfil da organizacao (`organization_id` — ADR-0005). `name`
  (unico por org, indice parcial ignorando soft delete), `description`,
  `is_system`, `status` (`Active`|`Archived`), soft delete.
- `ProfilePermission`: quais permissoes um perfil concede (PK composta
  `profile_id` + `permission_key`; FK `Restrict` para `Permission`).
- `UserProfile`: vinculo usuario -> perfil (PK composta). Um usuario pode ter
  **varios** perfis.
- `ApplicationUser` (Identity): ganha `is_owner` (dono da org, bypass funcional)
  e `status` (`Active`|`Inactive`); **perde** `orgRole`.
- `AuditEvent`: eventos de perfil e de usuario (ADR-0005).

## Casos de Uso

- Listar o catalogo de permissoes ativas agrupado por modulo (`GET /permissions`).
- Listar, ver, criar, editar e arquivar perfis (`/profiles`).
- Listar e ver usuarios da organizacao (`/users`).
- Ativar/desativar usuario (`PATCH /users/{id}/status`).
- Definir o conjunto de perfis de um usuario (`PUT /users/{id}/profiles`).
- Resolver a permissao efetiva da sessao (`GET /auth/me` devolve `isOwner`,
  `permissions` e `companies`).

## Regras de Negocio

- **Permissao efetiva** = uniao das permissoes `Active` dos perfis `Active`
  vinculados ao usuario. Sem perfil → nenhuma permissao funcional.
- **Owner** (`is_owner`) tem bypass funcional total (todas as permissoes ativas),
  mas continua sujeito ao eixo de empresa.
- **Usuario `Inactive`** tem permissao efetiva **vazia**, independente de perfis
  ou de ser owner.
- **Revogacao imediata:** a permissao e resolvida por request (memoizada apenas
  dentro do request), entao remover um perfil bloqueia no request seguinte.
- **Perfil `is_system`** nao pode ser editado nem arquivado; e garantido por
  organizacao no boot com **todas** as permissoes ativas.
- **Postura B (anti-escalada):** apenas o **owner** atribui ou remove um perfil
  `is_system`. Quem tem so `profiles.assign` recebe 403 ao tentar.
- **Owner e somente-leitura na app:** nao pode ser ativado/desativado nem ter
  perfis alterados por endpoint. E o piso que evita "organizacao trancada por fora".
- **Nome de perfil unico por organizacao**; a corrida no indice unico (23505) e
  traduzida para erro de validacao, nao 500.
- **Permissao obsoleta** nao pode ser concedida e e ignorada na resolucao.
- O catalogo e **reconciliado no boot**: chave declarada some do codigo →
  `Obsolete`; reaparece → `Active`. Idempotente.

## Autorizacao e Tenancy

- Permissoes declaradas por este modulo: `profiles.manage` (criar/editar/arquivar
  perfis e definir suas permissoes), `profiles.assign` (atribuir perfis a
  usuarios), `users.manage` (listar, ativar e desativar usuarios).
- Gates por endpoint: `/permissions` e `/profiles` exigem `profiles.manage`;
  `/users` (listar, ver, status) exige `users.manage`; `PUT /users/{id}/profiles`
  exige `profiles.assign`.
- Enforcement por `[RequirePermission]` (policy provider dinamico) **no backend**;
  o frontend nunca e barreira, apenas espelho de UX.
- Tenancy: a organizacao vem sempre do usuario da sessao, nunca do request.
  Recurso de outra organizacao responde **404** (nao vaza existencia).
- No v1, `profiles.manage` e `profiles.assign` sao privilegios administrativos de
  fato (quem os detem e um administrador de confianca), nao capacidades
  granulares seguras — a evolucao para a postura A esta no backlog.

## Criterios de Aceite

Status: **atendidos** (implementado e verificado em 2026-07-20 na branch
`feat/access-control`; 48 testes verdes e e2e 12/12).

- [x] Catalogo reconciliado no boot; chave sumida vira `Obsolete` e reaparecida volta a `Active`.
- [x] FK barra `permission_key` inexistente em `ProfilePermission`.
- [x] Cada organizacao tem o perfil "Administrador" (`is_system`) com todas as permissoes ativas.
- [x] Permissao efetiva = uniao dos perfis; perfil arquivado ou soft-deleted deixa de conceder.
- [x] Owner recebe todas as permissoes ativas mesmo sem perfil.
- [x] Usuario desativado e bloqueado imediatamente (inclusive em `/auth/me`).
- [x] Endpoint sem a permissao exigida = 403; sem sessao = 401.
- [x] Perfil/usuario de outra organizacao = 404.
- [x] Nao-owner nao atribui **nem remove** perfil `is_system` (403); owner consegue.
- [x] Owner nao pode ser desativado nem ter perfis editados pela app.
- [x] Perfil `is_system` nao pode ser editado nem arquivado.
- [x] Data migration: org com varios admins → exatamente **um** `is_owner`, todos
      os ex-admins vinculados ao "Administrador", member sem perfil.
- [x] Dois eixos: ter a permissao funcional sem `UserCompanyAccess` nao da acesso a empresa.

## Eventos de Auditoria

Emitidos na mesma unidade de trabalho da alteracao (ADR-0005):

- `access_control.profile.created`
- `access_control.profile.updated` (por campo alterado: `field`, `old`, `new`)
- `access_control.profile.archived`
- `access_control.profile.permission_granted` / `...permission_revoked`
- `access_control.user.status_changed`
- `access_control.user.profile_assigned` / `...profile_removed`

O contrato `old`/`new` usado aqui e a referencia para a padronizacao formal do
`AuditEvent` (ADR-0013).

## Dependencias

- Modulos: `organizations` (organizacao, usuario, empresa e o eixo de acesso a
  empresa). O `organizations` passou a depender deste modulo para o gate de
  empresas (`companies.access` / `companies.manage`) no lugar de `orgRole=Admin`.
- ADRs: 0003 (camadas), 0005 (dados/tenancy/auditoria), 0006 (auth/cookie —
  parcialmente substituida), 0008 (doc de modulo), 0010 (multiempresa),
  0012 (perfis configuraveis), 0013 (padrao de AuditEvent).

## Validacao Esperada

- Unit: nucleo do reconciliador de catalogo com catalogos arbitrarios.
- Integracao (Postgres real via Testcontainers): bootstrap do perfil de sistema e
  idempotencia; reconciliacao do catalogo; permissao efetiva (uniao, arquivado,
  obsoleto, soft delete, owner, desativado); enforcement 401/403 por endpoint;
  CRUD de perfis e invariantes de `is_system`; gestao de usuarios (status,
  atribuicao de perfis, postura B, owner protegido); cross-tenant 404; data
  migration do `orgRole`.
- E2E (Playwright): telas de perfis e usuarios sobre a stack real.

## Decisoes Relacionadas

- ADR-0012 (perfis configuraveis e permissoes) — decisao principal.
- ADR-0013 (padrao de `AuditEvent`) — contrato dos eventos acima.
- ADR-0006 (auth) — parcialmente substituida no eixo de autorizacao.
- ADR-0010 (multiempresa) — origem do eixo de empresa e do estado migrado.
- Design backend: `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md`.
- Design frontend: `docs/specs/2026-07-20-access-control-frontend-perfis-usuarios-design.md`.
