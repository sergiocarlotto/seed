# Handoff — Controle de Acesso (retomada em outra sessão)

> Documento de continuidade entre sessões (não é doc de produto). Cole o bloco
> abaixo como prompt inicial numa nova sessão. Pode ser removido antes do merge.

---

Retomar o projeto de controle de acesso (perfis e permissões) do backend do Seed.
Antes de tudo, leia CLAUDE.md e docs/decisions/README.md (regra do projeto).

## Onde trabalhar
Todo o trabalho vive no worktree C:/Users/sergi/pessoal/seed/.worktrees/access-control,
branch feat/access-control (criada a partir da main). Trabalhe LÁ, nunca na cópia
principal (main). Tudo está commitado; working tree limpo. Último commit de código:
6828982 (migration DropOrgRole). O backend .NET fica em apps/api/ (solução
apps/api/Seed.slnx).

## Estado atual — BACKEND DA ADR-0012 COMPLETO
Decisão: ADR-0012 (Aceita) — perfis configuráveis por organização substituem os
papéis fixos da ADR-0006. Design de referência (com segurança/anti-escalada e
contrato de auditoria old/new):
docs/specs/2026-07-19-access-control-perfis-permissoes-design.md

Planos implementados, revisados e testados (47 testes verdes: 1 unit + 46 integração),
em docs/plans/:
- Plano 1 — fundação: entidades (Permission/Profile/ProfilePermission/UserProfile),
  catálogo fixo no código, reconciliador no boot, migration, IsOwner no ApplicationUser.
- Plano 2 — enforcement: IEffectivePermissions (união dos perfis ativos + bypass do
  owner), [RequirePermission], endpoint GET /permissions.
- Plano 3a — bootstrap: perfil de sistema "Administrador" (todas as permissões) por org.
- Plano 3b — CRUD de perfis (ProfilesController, protegido por profiles.manage),
  invariantes, helper IAuditLog e auditoria old/new na mesma transação.
- Plano 3c — usuários + atribuição + /auth/me: UsersController (GET /users,
  GET /users/{id}, PATCH /users/{id}/status sob users.manage; PUT /users/{id}/profiles
  sob profiles.assign). UserStatus (Active/Inactive) desativa e bloqueia acesso imediato
  (permissão efetiva vazia + login recusado). Postura B (perfil is_system só o owner
  atribui/remove). Owner read-only. Corrida de atribuição → 409.
- Plano 3d — gate de empresas por permissão: companies.access/companies.manage no
  catálogo; CompaniesController usa [RequirePermission]; CompanyService sem checagem
  de orgRole; /auth/me gateia empresas por companies.access. (Fecha o risco residual
  do 3c: usuário desativado bloqueado em /companies.)
- Plano 3e — drop orgRole (fase 2 da migração): removido ApplicationUser.OrgRole e o
  enum OrganizationRole; AccessControlBootstrapper liga owners por IsOwner;
  DataSeeder/ApiFactory setam IsOwner direto; migration 20260720042422_DropOrgRole.

O /auth/me hoje devolve: { user{Id,Email,FullName}, organizationId, isOwner,
permissions (chaves efetivas), companies (gateadas por companies.access) }.

## Ambiente (Windows + Smart App Control) — CRÍTICO
O SAC bloqueia `dotnet test` e `dotnet ef` no host. Use os wrappers em container,
sempre pela ferramenta PowerShell (o `pwsh` NÃO existe no Git Bash; rodar via Bash
falha com "pwsh: command not found") e com caminho absoluto:
- Testes: & 'C:\Users\sergi\pessoal\seed\.worktrees\access-control\scripts\test.ps1' [--filter ...]
- Migrations: & 'C:\Users\sergi\pessoal\seed\.worktrees\access-control\scripts\ef.ps1' migrations add <Nome> -o Persistence/Migrations
  (requer Docker Desktop rodando; a propriedade nova precisa compilar antes de gerar).
`dotnet build apps/api/Seed.slnx` roda no host. Detalhes em docs/setup/local-environment.md.

## Fluxo de trabalho: subagent-driven-development
Para cada fatia: escrever o plano em docs/plans/ (código completo, TDD, commits por
task), commitar o plano, despachar um subagente implementador (model sonnet), depois
revisão de spec-compliance (você mesmo, lendo o código) e revisão de code-quality
(agente superpowers:code-reviewer), aplicar follow-ups Important/Critical via o mesmo
implementador, verificar os testes verdes via test.ps1, fechar. Confirme cada resultado
com a saída real; nunca afirme verde sem a saída.

## Próximo passo — escolher com o usuário
1) FRONTEND (recomendado como próxima grande etapa): telas de Perfis e Usuários
   (Next.js 16 / shadcn — ler apps/web/CLAUDE.md e a seção "UI" do design). Consome os
   endpoints já prontos e o /auth/me estendido (esconder menus por UX; backend é a
   barreira real). Ponto de atenção do design: seletor de permissões em árvore
   (agrupado por module de GET /permissions) precisa de tratamento responsivo.
   Comece por brainstorming/design próprio, não emende direto no código.
2) Ou fechar pendências de backend antes da UI: documentar o módulo AccessControl em
   docs/modules/ (padrão ADR-0008, a spec lista como pendência); e as ADRs pendentes
   citadas na spec (nova ADR substituindo a ADR-0006 formalmente; ADR de padronização
   do AuditEvent).

Comece confirmando o estado da branch (git log, git status, ./scripts/test.ps1) e
pergunte ao usuário qual das opções acima seguir antes de escrever qualquer plano.
