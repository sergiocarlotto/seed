# Regras de Trabalho para IAs

Este repositorio e a fonte de verdade para o produto Seed, prompts e skills do
projeto.

Antes de propor arquitetura, requisitos, implementacao, dependencias ou
alteracoes de codigo, toda IA deve ler:

1. `docs/foundation/marco-zero.md` — a fundacao do produto.
2. `docs/decisions/README.md` — o indice de decisoes, com o resumo de cada ADR.

Leia uma ADR completa (`docs/decisions/ADR-*.md`) apenas quando sua tarefa tocar
aquele assunto ou quando o resumo do indice nao for suficiente. Nao e preciso ler
todas as ADRs em toda tarefa; o indice ja traz o essencial de cada decisao. Isso
mantem o contexto enxuto sem perder rastreabilidade.

## Onde Encontrar o Quê

Ponto de partida: este `CLAUDE.md` e o indice de ADRs. O resto e consultado sob
demanda, conforme a tarefa.

- `docs/foundation/marco-zero.md` — fundacao do produto: visao, principios,
  escopo do MVP, modulos, entidades, fluxos e plano de evolucao.
- `docs/decisions/` — decisoes arquiteturais. Comece pelo `README.md` (indice);
  abra uma `ADR-*.md` so quando precisar do detalhe daquela decisao.
- `docs/modules/<module>.md` — documentacao de cada modulo backend (padrao
  ADR-0008). `_template.md` e o modelo a copiar para um modulo novo.
- `docs/specs/<data>-<assunto>.md` — designs aprovados (saida de brainstorming),
  antes de virar plano de implementacao.
- `docs/plans/<data>-<assunto>.md` — planos de implementacao passo a passo,
  escritos para execucao (inclusive por agentes).
- `docs/setup/local-environment.md` — como rodar o projeto localmente e o estado
  das ferramentas da maquina de desenvolvimento.
- `prompts/` — prompts versionados de IA. Atencao: `prompts/marco-zero.md` e o
  prompt que gerou a fundacao (entrada), nao a fundacao em si (que vive em
  `docs/foundation/marco-zero.md`).
- `.claude/skills/<skill>/` — skills do projeto (ex.: revisores especialistas),
  descobertas automaticamente pelo Claude Code neste repositorio.
- `apps/web/CLAUDE.md` — regras especificas do frontend (Next.js 16), carregadas
  so ao trabalhar em `apps/web`.

## Regras de Decisao

- Nao sobrescreva uma ADR aceita silenciosamente.
- Se uma nova recomendacao conflitar com uma ADR aceita, crie ou proponha uma
  nova ADR explicando a mudanca, racional, tradeoffs e impacto de migracao.
- Prefira decisoes pequenas e reversiveis.
- Mantenha o projeto executavel em Linux com Docker.
- Prefira tecnologias open source com comunidades ativas, continuidade
  previsivel, boa documentacao e bom suporte a desenvolvimento assistido por IA.
- Ao criar uma nova pasta ou tipo de documento em `docs/`, adicione a linha
  correspondente no mapa "Onde Encontrar o Quê" acima. O mapa deve refletir
  apenas o que ja existe; nao aponte para documentos ainda nao criados.

## Decisoes Obrigatorias Atuais

- Fundacao do produto: `docs/foundation/marco-zero.md`.
- Prompts versionados: `prompts/`.
- Skills versionadas do projeto: `.claude/skills/<skill-name>/`. O Claude Code
  descobre essas skills automaticamente ao trabalhar neste repositorio; nao ha
  passo de sincronizacao para runtime externo.
- Base frontend: TypeScript, React, Next.js, Tailwind CSS, shadcn/ui, Zod e
  Playwright.
- Padrao de idioma: codigo e contratos tecnicos em ingles; documentacao interna
  de produto em portugues; comentarios no codigo seguem a audiencia da
  explicacao, conforme definido na ADR-0004.
- Regra de portabilidade: o projeto nao deve depender de recursos exclusivos de
  hospedagem de um provedor. Ele deve permanecer implantavel em uma VPS Linux.
