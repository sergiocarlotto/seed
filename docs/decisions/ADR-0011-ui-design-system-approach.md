# ADR-0011: Abordagem de UI e Design System

## Status

Aceita

## Contexto

O produto precisa de consistencia visual desde o inicio, mas ainda nao tem um
design system proprio (tokens de cor/tipografia/espacamento, tema unificado,
componentes de marca). Criar um design system completo agora seria prematuro; ao
mesmo tempo, montar telas com estilos ad-hoc geraria retrabalho e inconsistencia.

A ADR-0002 ja define a base de frontend como TypeScript, React, Next.js, Tailwind
CSS e shadcn/ui.

## Decisao

Adotar, como base interina de componentes, **shadcn/ui + Tailwind CSS** (ADR-0002)
para todas as telas. Os componentes do shadcn/ui sao copiados para o repositorio
(`apps/web/src/components/ui/`), ficando versionados e customizaveis.

Nota de implementacao: a versao atual do shadcn/ui (preset padrao) integra com
Tailwind v4 e Next.js 16 e usa **Base UI** (`@base-ui/react`) como biblioteca de
primitivos acessiveis (e nao Radix, que era o padrao historico). Isso e um
detalhe do interino e nao muda a intencao desta decisao.

Um **design system formal** — tokens compartilhados, tema (incluindo modo
escuro), componentes de marca e diretrizes de uso — fica **adiado** e devera
ganhar o seu **proprio ADR** quando for tratado, evoluindo a partir da base do
shadcn/ui.

## Consequencias

- Novas telas usam os componentes de `apps/web/src/components/ui/`.
- A aparencia e consistente sem custo alto agora.
- Itens deixados para o design system futuro: tokens compartilhados formais,
  modo escuro automatico/toggle, componentes de marca e documentacao de uso.

## Alternativas Consideradas

### Construir um design system proprio agora

Rejeitada. Prematuro antes de o produto ter telas e uso reais que justifiquem os
tokens e componentes.

### Estilos ad-hoc com Tailwind, sem base de componentes

Rejeitada. Geraria inconsistencia e retrabalho.

## Validacao

Esta decisao permanece valida se:

- as telas usam uma base de componentes comum e consistente;
- a base permanece versionada e customizavel no repositorio;
- a evolucao para um design system formal e registrada em ADR proprio quando
  ocorrer.
