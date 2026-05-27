# Regras de Trabalho para IAs

Este repositorio e a fonte de verdade para o produto Seed, prompts e skills do
projeto.

Antes de propor arquitetura, requisitos, implementacao, dependencias ou
alteracoes de codigo, toda IA deve ler:

1. `docs/foundation/marco-zero.md`
2. `docs/decisions/README.md`
3. As ADRs aceitas referenciadas nesse indice

## Regras de Decisao

- Nao sobrescreva uma ADR aceita silenciosamente.
- Se uma nova recomendacao conflitar com uma ADR aceita, crie ou proponha uma
  nova ADR explicando a mudanca, racional, tradeoffs e impacto de migracao.
- Prefira decisoes pequenas e reversiveis.
- Mantenha o projeto executavel em Linux com Docker.
- Prefira tecnologias open source com comunidades ativas, continuidade
  previsivel, boa documentacao e bom suporte a desenvolvimento assistido por IA.

## Decisoes Obrigatorias Atuais

- Fundacao do produto: `docs/foundation/marco-zero.md`.
- Prompts versionados: `prompts/`.
- Skills versionadas do projeto: `skills/<skill-name>/`.
- Skills locais do runtime Codex em `C:\Users\sergi\.codex\skills\` sao apenas
  copias instaladas; a fonte versionada permanece neste repositorio.
- Base frontend: TypeScript, React, Next.js, Tailwind CSS, shadcn/ui, Zod e
  Playwright.
- Padrao de idioma: codigo e contratos tecnicos em ingles; documentacao interna
  de produto em portugues; comentarios no codigo seguem a audiencia da
  explicacao, conforme definido na ADR-0004.
- Regra de portabilidade: o projeto nao deve depender de recursos exclusivos de
  hospedagem de um provedor. Ele deve permanecer implantavel em uma VPS Linux.
