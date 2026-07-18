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

Ao ativar, carregue e resuma (todas lidas do worktree da `main` — ver "Backlog
via worktree" para localizá-lo, chamado ali de `WT_MAIN`):

- a memória `seed-project-status` (já vem na sessão);
- o `backlog.md` em `<WT_MAIN>/docs/specs/backlog.md`;
- os nomes dos arquivos em `<WT_MAIN>/docs/specs/` e `<WT_MAIN>/docs/plans/`;
- o índice de ADRs em `<WT_MAIN>/docs/decisions/`.

Ler tudo do mesmo `WT_MAIN` garante uma visão consistente do estado global,
independentemente da branch de feature em que o usuário esteja.

## Backlog via worktree

O `backlog.md` é estado global do projeto e vive na `main`. Sempre trabalhe nele
no worktree que está com a `main` — nunca na branch de feature atual. Use
**caminhos absolutos** (a saída do git é absoluta e `..` depende do diretório
atual, que a partir de um worktree de feature resolveria errado).

Procedimento (sempre nesta ordem):

1. **Descubra o repositório principal** (`MAIN_REPO`): o pai do git-common-dir.
   ```
   git rev-parse --git-common-dir     # ex.: .../seed/.git  → MAIN_REPO = .../seed
   ```

2. **Ache o worktree que está com a `main`** (`WT_MAIN`):
   ```
   git worktree list
   ```
   Pegue o caminho (absoluto) da linha marcada `[main]`.
   - Se a `main` estiver no próprio `MAIN_REPO` (usuário está na main), então
     `WT_MAIN = MAIN_REPO` — edite/commite ali mesmo.
   - Se estiver no worktree dedicado, `WT_MAIN = <MAIN_REPO>/../seed-backlog`.

3. **Se NENHUMA linha estiver `[main]`**, crie o worktree dedicado, ancorado no
   `MAIN_REPO` (não no diretório atual):
   ```
   git -C "<MAIN_REPO>" worktree add ../seed-backlog main
   ```
   Agora `WT_MAIN = <MAIN_REPO>/../seed-backlog`. (A `main` só pode estar em um
   worktree; por isso primeiro procuramos um existente e só criamos se não houver.)

4. **Ler:** `<WT_MAIN>/docs/specs/backlog.md`.

5. **Escrever + commit:** edite esse arquivo e commite no próprio `WT_MAIN`:
   ```
   git -C "<WT_MAIN>" add docs/specs/backlog.md
   git -C "<WT_MAIN>" commit -m "chore(backlog): captura <slug>"
   ```

6. **Push (opcional):** para dev solo o commit local já garante que a ideia não
   se perde — pergunte antes de empurrar: `git -C "<WT_MAIN>" push origin main`.

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
