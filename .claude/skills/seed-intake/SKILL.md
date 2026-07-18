---
name: seed-intake
description: Recepção e triagem de ideias de features do Seed. Use por chamada explícita quando o usuário quiser falar de novas features do Seed, registrar ideias soltas do projeto, "jogar algo no backlog", despejar uma linha de raciocínio sobre o produto, ou se orientar sobre o estado atual do projeto antes de amadurecer uma ideia. Não auto-ativa no meio de outra tarefa.
---

# Recepção do Seed (seed-intake)

## Propósito

Ser a porta de entrada única para ideias de features do Seed: orientar pelo
estado atual do projeto, captar o que o usuário despejar, registrar as ideias
num backlog único e rotear para a skill certa. Esta skill **coordena** — ela
não escreve planos, não implementa código e não faz revisão de arquitetura ou
segurança.

## Quando usar

- O usuário chamou explicitamente (ex.: "vamos falar de features do Seed",
  "quero registrar umas ideias", "joga isso no backlog", "tenho umas ideias
  soltas do projeto").

## Quando NÃO usar

- No meio de outra tarefa, sem o usuário pedir (não auto-ativa).
- Para escrever plano de implementação → isso é `superpowers:writing-plans`,
  alcançado depois do brainstorming.
- Para implementar código, revisar arquitetura ou segurança → use
  `software-architect` / `security-engineer`.

## Fontes de contexto

Ao ativar, carregue e resuma:

- a memória `seed-project-status` (já vem na sessão);
- o `backlog.md` **lido do worktree da main** (ver "Backlog via worktree");
- os nomes dos arquivos em `docs/specs/` e `docs/plans/`;
- o índice de ADRs em `docs/decisions/`.

## Backlog via worktree

O `backlog.md` é estado global do projeto e vive na `main`. Leia e escreva nele
através de um git worktree dedicado preso à `main`, para nunca sujar a branch de
trabalho atual.

- **Local do worktree:** `../seed-backlog` (irmão do repositório).
- **Caminho do backlog no worktree:** `../seed-backlog/docs/specs/backlog.md`.

Procedimento (sempre nesta ordem):

1. **Se o usuário já está na `main` no repositório principal**, use o próprio
   repositório (sem worktree): edite `docs/specs/backlog.md` e commite na main.
   Detecte com `git branch --show-current`.
2. **Senão, garanta o worktree** (checar antes de criar, para não dar erro):
   - Verifique: `git worktree list` — procure uma linha com `../seed-backlog`
     em `[main]`.
   - Se não existir: `git worktree add ../seed-backlog main`.
3. **Ler:** leia `../seed-backlog/docs/specs/backlog.md`.
4. **Escrever:** edite esse arquivo no worktree, então:
   ```
   git -C ../seed-backlog add docs/specs/backlog.md
   git -C ../seed-backlog commit -m "chore(backlog): captura <slug>"
   ```
5. **Push (opcional):** se o usuário quiser sincronizar,
   `git -C ../seed-backlog push origin main`. Para dev solo, o commit local já
   garante que a ideia não se perde — pergunte antes de empurrar.

Nunca commite o backlog na branch de feature atual.

## Fluxo

1. **Orientar** — carregue o contexto e mostre um resumo de ~3 linhas: projeto =
   Seed, fase atual, specs/planos recentes, follow-ups abertos e quantas ideias
   há no backlog.
2. **Captar** — receba as ideias do usuário:
   - dump grande/bagunçado (compilado, notas do celular/ChatGPT, vários temas)
     → invoque `product-secretary` para estruturar;
   - ideia única e curta → organize direto.
3. **Triar** — para cada item, decida:
   - (a) feature nova → adicione uma entrada ao backlog;
   - (b) relacionada a item já existente → funda na entrada existente;
   - (c) pronta para amadurecer agora → marque `pronto-p-brainstorm` e ofereça
     o brainstorming.
   Se um item tiver peso claro de segurança ou arquitetura, **sinalize** isso ao
   usuário (não roteie automaticamente na v1).
4. **Encaminhar** — para itens "amadurecer agora", invoque
   `superpowers:brainstorming`. Para os demais, confirme o que foi gravado no
   backlog. Este é o estado terminal: não siga além do brainstorming.

## Formato de uma entrada no backlog

    ### slug-da-ideia — Título curto
    - **Status:** ideia | rascunho | pronto-p-brainstorm
    - **Capturado em:** AAAA-MM-DD
    - **Relacionados:** ADR-XXXX, spec/plano, ou —
    - **Descrição:** Um parágrafo.

## Idioma

Português claro; termos técnicos e nomes (comandos, arquivos, branches, APIs)
no original. Explique só termos em inglês menos comuns.
