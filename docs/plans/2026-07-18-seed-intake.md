# Skill `seed-intake` — Plano de Implementação

> **Para executores agênticos:** SUB-SKILL OBRIGATÓRIA: use
> `superpowers:subagent-driven-development` (recomendado) ou
> `superpowers:executing-plans` para implementar tarefa a tarefa. Os passos usam
> caixas (`- [ ]`) para acompanhamento.

**Goal:** Criar a skill de projeto `seed-intake` — a "recepção/triagem" do Seed
que orienta pelo estado do projeto, capta ideias de features num backlog único
persistido na `main` via git worktree, e roteia para `product-secretary` e
`superpowers:brainstorming`.

**Architecture:** Skill única (`SKILL.md`) sem estado próprio além de
`docs/specs/backlog.md`. O backlog é estado global do projeto: a skill lê e
escreve nele através de um git worktree dedicado preso à `main`
(`../seed-backlog`), para nunca sujar a branch de trabalho atual. A skill
coordena; o trabalho pesado fica em skills já existentes.

**Tech Stack:** Markdown (SKILL.md + backlog), convenção de skills do Claude Code
(frontmatter `name`/`description`), git worktree.

**Spec:** `docs/specs/2026-07-18-seed-intake-design.md`

---

## Estrutura de arquivos

- **Criar:** `.claude/skills/seed-intake/SKILL.md` — a skill (frontmatter +
  fluxo + procedimento de worktree + regras de delegação). Responsabilidade
  única: orientar, captar, triar e rotear ideias.
- **Criar (na `main`, via worktree):** `docs/specs/backlog.md` — backlog único
  de ideias. Responsabilidade única: guardar ideias ainda não amadurecidas.
  Como o backlog vive na `main`, ele é criado **dentro do worktree**
  (`../seed-backlog`), não na branch de feature.
- **Sem modificações** em skills existentes (`product-secretary`, etc.): a
  `seed-intake` apenas as invoca.

**Estratégia de branch:** implementar num **worktree isolado**
`.worktrees/seed-intake` (branch `feat/seed-intake` a partir da `main`), para
não tocar no `feat/app-shell` em andamento (que tem `CLAUDE.md` modificado). Ao
final, mesclar na `main`. **Exceção:** o `backlog.md` vai direto para a `main`
via um segundo worktree (`../seed-backlog`), porque é estado global lido/escrito
pela skill em tempo de execução.

---

### Task 0: Preparar worktree isolado de trabalho

**Files:** nenhum arquivo de código; git + mover 2 docs.

Contexto: você está em `feat/app-shell` com `CLAUDE.md` modificado e os 2 docs
do seed-intake como untracked. Para não tocar no app-shell, o trabalho vai num
worktree isolado. `.worktrees/` já está no `.gitignore`.

- [ ] **Step 1: Criar o worktree isolado com a branch nova**

Run:
```bash
git -C c:/Users/sergi/pessoal/seed worktree add -b feat/seed-intake .worktrees/seed-intake main
```
Expected: `Preparing worktree` e um checkout da `main` na nova branch
`feat/seed-intake` em `c:/Users/sergi/pessoal/seed/.worktrees/seed-intake`.

- [ ] **Step 2: Mover os 2 docs (soltos no repo principal) para o worktree**

Os docs foram criados no working tree do `feat/app-shell` (untracked). Mova-os
para o worktree do seed-intake e remova do principal:
```bash
mkdir -p c:/Users/sergi/pessoal/seed/.worktrees/seed-intake/docs/specs c:/Users/sergi/pessoal/seed/.worktrees/seed-intake/docs/plans
mv c:/Users/sergi/pessoal/seed/docs/specs/2026-07-18-seed-intake-design.md c:/Users/sergi/pessoal/seed/.worktrees/seed-intake/docs/specs/
mv c:/Users/sergi/pessoal/seed/docs/plans/2026-07-18-seed-intake.md c:/Users/sergi/pessoal/seed/.worktrees/seed-intake/docs/plans/
```
Expected: os 2 arquivos somem do `git status` do `feat/app-shell` e aparecem no
worktree. (Confirmar com `git -C c:/Users/sergi/pessoal/seed status --short` —
não devem mais aparecer os 2 docs; só o `CLAUDE.md` modificado do app-shell.)

- [ ] **Step 3: Commit do spec e do plano no worktree**

Run:
```bash
git -C c:/Users/sergi/pessoal/seed/.worktrees/seed-intake add docs/specs/2026-07-18-seed-intake-design.md docs/plans/2026-07-18-seed-intake.md
git -C c:/Users/sergi/pessoal/seed/.worktrees/seed-intake commit -m "docs(seed-intake): spec e plano de implementacao"
```
Expected: um commit com os dois arquivos na branch `feat/seed-intake`.

---

### Task 1: Preparar o worktree e criar o backlog na main

**Files:**
- Create: `../seed-backlog/docs/specs/backlog.md` (na `main`, via worktree)

- [ ] **Step 1: Criar o worktree preso à main**

Run:
```bash
git -C c:/Users/sergi/pessoal/seed worktree list
```
Se `../seed-backlog` não aparecer em `[main]`, crie:
```bash
git -C c:/Users/sergi/pessoal/seed worktree add ../seed-backlog main
```
Expected: `Preparing worktree` e um checkout da `main` em
`c:/Users/sergi/pessoal/seed-backlog`.

> Requer que a `main` não esteja checada em nenhum worktree. O repo principal
> segue em `feat/app-shell` e o trabalho está em `.worktrees/seed-intake`
> (`feat/seed-intake`), então a `main` está livre para este worktree de runtime.

- [ ] **Step 2: Escrever o backlog com cabeçalho e convenção de formato**

Conteúdo exato de `c:/Users/sergi/pessoal/seed-backlog/docs/specs/backlog.md`:

```markdown
# Backlog de ideias — Seed

Ponto único de captura de ideias de features do Seed, mantido pela skill
`seed-intake`. Cada entrada é uma ideia ainda não amadurecida — nada aqui está
aprovado nem planejado.

**Este arquivo vive na `main`.** A skill `seed-intake` lê e escreve nele através
de um git worktree preso à `main` (`../seed-backlog`), para não sujar a branch
de trabalho atual.

## Convenção de formato

Cada ideia é uma entrada iniciada por `###`, com slug em kebab-case, status,
data de captura (absoluta), relacionados (ADRs/specs/planos, ou `—`) e uma
descrição de 1 parágrafo. Status possíveis: `ideia`, `rascunho`,
`pronto-p-brainstorm`.

Modelo de uma entrada:

    ### slug-da-ideia — Título curto
    - **Status:** ideia
    - **Capturado em:** AAAA-MM-DD
    - **Relacionados:** —
    - **Descrição:** Um parágrafo explicando a ideia.

Quando uma ideia vira spec formal via brainstorming, marque-a como concluída
com link para o spec gerado, ou remova-a do backlog.

## Ideias

_(vazio — nenhuma ideia capturada ainda)_
```

- [ ] **Step 3: Commit na main (dentro do worktree)**

```bash
git -C c:/Users/sergi/pessoal/seed-backlog add docs/specs/backlog.md
git -C c:/Users/sergi/pessoal/seed-backlog commit -m "feat(seed-intake): backlog inicial de ideias"
```
Expected: um commit na `main`.

- [ ] **Step 4: Confirmar que o backlog está na main e a branch de trabalho está limpa**

Run:
```bash
git -C c:/Users/sergi/pessoal/seed log main --oneline -1
git -C c:/Users/sergi/pessoal/seed/.worktrees/seed-intake status --short
```
Expected: o topo da `main` é o commit do backlog; e o `status` do worktree
`feat/seed-intake` **não** mostra `docs/specs/backlog.md` (ele existe só na main).

---

### Task 2: Escrever a skill `seed-intake`

**Files:**
- Create (no worktree `feat/seed-intake`):
  `.worktrees/seed-intake/.claude/skills/seed-intake/SKILL.md`

Antes de escrever, considere invocar `superpowers:writing-skills` para conferir
as convenções de autoria. O conteúdo abaixo já segue o padrão das skills do Seed
(português claro; frontmatter `name`/`description`).

- [ ] **Step 1: Escrever `SKILL.md` com o conteúdo completo**

Conteúdo exato de `.claude/skills/seed-intake/SKILL.md`:

```markdown
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
```

- [ ] **Step 2: Verificar o frontmatter e a existência do arquivo**

Run:
```bash
head -4 c:/Users/sergi/pessoal/seed/.worktrees/seed-intake/.claude/skills/seed-intake/SKILL.md
```
Expected: mostra as 3 linhas do frontmatter delimitadas por `---`, com
`name: seed-intake` e a linha `description:`.

- [ ] **Step 3: Commit**

```bash
git -C c:/Users/sergi/pessoal/seed/.worktrees/seed-intake add .claude/skills/seed-intake/SKILL.md
git -C c:/Users/sergi/pessoal/seed/.worktrees/seed-intake commit -m "feat(seed-intake): skill de recepcao e triagem de ideias"
```

---

### Task 3: Verificação ponta-a-ponta (ensaio de captura)

Objetivo: exercitar o mecanismo do worktree de verdade e confirmar os critérios
de sucesso do spec — a ideia persiste na `main` e a branch de trabalho fica
limpa. Ao final, desfazer o ensaio.

**Files:** nenhum arquivo novo de produção; manipula o worktree.

- [ ] **Step 1: Confirmar que o worktree existe (criado na Task 1)**

Run:
```bash
git -C c:/Users/sergi/pessoal/seed worktree list
```
Expected: aparece `../seed-backlog` em `[main]`. (Se não aparecer, volte à
Task 1, Step 1.)

- [ ] **Step 2: Capturar uma ideia de teste no worktree**

Acrescente ao final de `c:/Users/sergi/pessoal/seed-backlog/docs/specs/backlog.md`
a entrada (substituindo a linha `_(vazio...)_` se ainda estiver lá):

```markdown
### teste-captura — Ideia de teste (remover)
- **Status:** ideia
- **Capturado em:** 2026-07-18
- **Relacionados:** —
- **Descrição:** Entrada de verificação do mecanismo de worktree. Deve ser removida.
```

Depois:
```bash
git -C c:/Users/sergi/pessoal/seed-backlog add docs/specs/backlog.md
git -C c:/Users/sergi/pessoal/seed-backlog commit -m "chore(backlog): captura teste-captura"
```
Expected: um commit na `main` dentro do worktree.

- [ ] **Step 3: Confirmar que a branch de trabalho NÃO foi suja**

Run:
```bash
git -C c:/Users/sergi/pessoal/seed/.worktrees/seed-intake status --short
```
Expected: **não** aparece nenhuma mudança em `docs/specs/backlog.md` no worktree
`feat/seed-intake` — a captura ficou só na `main` via worktree de runtime.

- [ ] **Step 4: Confirmar que a ideia está na main**

Run:
```bash
git -C c:/Users/sergi/pessoal/seed log main --oneline -1
```
Expected: o topo da `main` é o commit `chore(backlog): captura teste-captura`.

- [ ] **Step 5: Desfazer o ensaio**

Run:
```bash
git -C c:/Users/sergi/pessoal/seed-backlog reset --hard HEAD~1
```
Expected: a `main` volta ao estado sem a entrada de teste. (Confirmar com
`git -C c:/Users/sergi/pessoal/seed-backlog log --oneline -1`.)

> Este `reset --hard` é destrutivo, mas atua só sobre o commit de teste recém
> criado no worktree. Peça confirmação ao usuário antes de rodar.

---

## Critérios de sucesso (do spec)

- [ ] Ao ser chamada, a skill produz orientação curta e correta do estado do Seed.
- [ ] Um brain-dump de várias ideias termina com entradas bem-formadas no
      backlog (delegando à `product-secretary` quando bagunçado).
- [ ] Uma ideia marcada "pronta" resulta em invocação do `brainstorming`.
- [ ] A captura persiste na `main` sem sujar a branch de trabalho atual.
- [ ] A skill nunca escreve plano nem código por conta própria.
- [ ] Segue o padrão de idioma do Seed.

---

## Finalização

Após as tarefas e a verificação, integre a branch com
`superpowers:finishing-a-development-branch`: mesclar `feat/seed-intake` na
`main` (leva spec, plano e `SKILL.md`; o `backlog.md` já está na main). O
worktree `../seed-backlog` permanece — é a infraestrutura de runtime da skill.

**Follow-up (mapa do CLAUDE.md):** o `backlog.md` é um novo tipo de documento em
`docs/`. A regra do `CLAUDE.md` do projeto pede adicionar uma linha ao mapa
"Onde Encontrar o Quê". **Não fazer agora:** o `CLAUDE.md` está modificado e não
commitado no `feat/app-shell`; editar aqui causaria conflito. Fazer essa linha
na `main`, junto do merge, depois que o app-shell fechar.
```
