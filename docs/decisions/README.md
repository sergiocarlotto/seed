# Registros de Decisao Arquitetural

Este diretorio contem as decisoes arquiteturais do projeto.

Futuras IAs devem consultar este indice antes de sugerir mudancas de stack,
arquitetura, planos de implementacao ou escolhas de dependencias.

## Decisoes Aceitas

| ADR | Status | Decisao |
| --- | --- | --- |
| [ADR-0000](ADR-0000-product-foundation.md) | Aceita | Fundacao do produto: `docs/foundation/marco-zero.md` |
| [ADR-0001](ADR-0001-repository-consolidation.md) | Aceita (parcialmente substituida pela ADR-0009) | Consolidacao do repositorio: prompts e skills vivem neste repositorio |
| [ADR-0002](ADR-0002-frontend-base.md) | Aceita | Base frontend: TypeScript, React, Next.js, Tailwind CSS, shadcn/ui, Zod, Playwright |
| [ADR-0003](ADR-0003-backend-base.md) | Aceita | Base backend: C#, ASP.NET Core, API separada, monolito modular, OpenAPI |
| [ADR-0004](ADR-0004-language-and-documentation-standard.md) | Aceita | Padrao de idioma: codigo e contratos tecnicos em ingles, documentacao interna de produto em portugues |
| [ADR-0005](ADR-0005-data-storage-and-ownership.md) | Aceita | Banco de dados, propriedade dos dados, persistencia, tenancy e auditoria |
| [ADR-0006](ADR-0006-authentication-and-authorization.md) | Aceita | Autenticacao (ASP.NET Core Identity, email+senha, cookie httpOnly), papeis e tenancy |
| [ADR-0007](ADR-0007-local-development-docker-and-deploy.md) | Aceita | Docker Compose (web/api/db/proxy Caddy), same-origin, migrations explicitas, deploy VPS |
| [ADR-0008](ADR-0008-backend-module-documentation-standard.md) | Aceita | Padrao de documentacao dos modulos backend em `docs/modules/<module>.md` |
| [ADR-0009](ADR-0009-ai-runtime-claude-code.md) | Aceita | Runtime de IA assistida: Claude Code; skills do projeto em `.claude/skills/` |

## Decisoes Pendentes

Nenhuma decisao pendente no momento.

## Como Alterar Uma Decisao

Para substituir ou alterar materialmente uma decisao aceita:

1. Crie uma nova ADR.
2. Referencie a ADR anterior.
3. Explique o motivo da mudanca.
4. Descreva tradeoffs, riscos e impacto de migracao.
5. Marque a ADR antiga como substituida apenas depois que a nova decisao for
   aceita.
