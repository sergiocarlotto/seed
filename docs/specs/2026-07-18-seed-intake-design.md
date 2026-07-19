# Design — Skill `seed-intake`

- **Data:** 2026-07-18
- **Status:** Aprovado no brainstorming, aguardando plano de implementação
- **Tipo:** Skill de projeto (Seed)
- **Local da skill:** `.claude/skills/seed-intake/SKILL.md`

## Problema

Recorrentemente o usuário fala sobre novas features do Seed em linha de
raciocínio (brain-dump). Falta um ponto único que:

1. entenda em que projeto estamos (o Seed) e resuma o estado atual de forma
   clara antes de qualquer coisa;
2. receba essas ideias soltas, organize e **estacione** as que ainda não vão
   virar trabalho agora;
3. quando uma ideia amadurece, **encaminhe** para o fluxo de brainstorming já
   existente.

Hoje esse trabalho está espalhado: a skill `product-secretary` sabe estruturar
notas bagunçadas, e o superpowers tem `brainstorming` / `writing-plans` para
transformar ideia em spec e plano — mas não há uma "porta de entrada" que una
o contexto do Seed com o roteamento para essas skills.

## Objetivo

Criar `seed-intake`: uma skill enxuta de **triagem/roteamento** (a "recepção"
do Seed) que orienta pelo estado do projeto, capta ideias de features num
**backlog único** e roteia para as skills certas. Ela **coordena**, não faz o
trabalho pesado — reaproveita o que já existe.

### Não-objetivos (v1)

- Não escreve planos de implementação (isso é `superpowers:writing-plans`,
  alcançado **depois** do brainstorming).
- Não implementa código.
- Não faz revisão de arquitetura/segurança — isso continua em
  `software-architect` / `security-engineer`. No máximo **sinaliza** que uma
  ideia tem peso de segurança/arquitetura. Roteamento automático para essas
  skills fica como evolução futura.
- Não substitui a `product-secretary`; **delega** a ela.

## Decisões (do brainstorming)

| Tema | Decisão |
|------|---------|
| Papel | Orquestrador/recepção enxuto: entende o Seed → organiza → pode chamar o brainstorming |
| Destino das ideias | **Backlog único** em `docs/specs/backlog.md` |
| Persistência | **Worktree dedicado preso à `main`** (nunca se perde, nunca suja a branch atual) |
| Notas bagunçadas | **Delega** à `product-secretary` para estruturar |
| Acionamento | **Chamada explícita** (não auto-ativa) |
| Arquitetura | Abordagem A — roteador enxuto, mínima manutenção |

## Arquitetura

Skill única (`SKILL.md`) sem estado próprio além do `backlog.md`. Depende de:

- **Contexto de entrada:** memória `seed-project-status` (já vem na sessão),
  o `backlog.md` **lido do worktree da `main`** (ver "Captura vs. branch atual"),
  nomes de arquivos em `docs/specs/` e `docs/plans/`, índice de ADRs em
  `docs/decisions/`.
- **Skills que ela invoca:** `product-secretary` (estruturar dump bagunçado),
  `superpowers:brainstorming` (amadurecer ideia pronta).

### Captura vs. branch atual

O `backlog.md` é **estado global do projeto**, não trabalho da branch atual. Por
isso a leitura e a escrita passam por um **git worktree dedicado, preso à
`main`** — uma segunda pasta de trabalho ligada ao mesmo repositório.

- **Local do worktree:** pasta irmã, padrão `../seed-backlog` (ou seja,
  `C:/Users/sergi/pessoal/seed-backlog`), definido no setup.
- **Escrita:** a skill grava e commita o `backlog.md` **nesse worktree, na
  `main`** (mensagem `chore(backlog): captura <slug>`). O repositório principal
  (na `feat/...` em que você trabalha) não é tocado e o arquivo não aparece no
  seu `git status`.
- **Leitura:** o passo "orientar" também lê o backlog **do worktree**, garantindo
  fonte única na `main` independentemente da branch atual.
- **Push:** opcional; a skill pode empurrar a `main` para `origin` após capturar
  (decisão de implementação — para dev solo, o commit local já garante "não se
  perde").
- **Pegadinha — main já em uso:** `git worktree add` não permite prender a `main`
  se ela já estiver checada em outra pasta. Se você já estiver **na `main`** no
  repositório principal, a skill captura direto ali (sem worktree). Se a `main`
  estiver livre (caso comum, você trabalha em `feat/...`), usa o worktree.
- **Merge:** como o `backlog.md` só é editado na `main` e é append-only, branches
  de feature não mexem nele — não há conflito quando elas viram merge na `main`.

### Fluxo

```
1. ORIENTAR   carrega contexto → resumo de ~3 linhas
                (projeto = Seed, fase atual, specs/planos recentes,
                 follow-ups abertos, nº de ideias no backlog)
2. CAPTAR     usuário despeja ideias
                ├─ dump grande/bagunçado → invoca product-secretary
                └─ ideia curta          → organiza direto
3. TRIAR      para cada item:
                (a) feature nova            → adiciona ao backlog
                (b) relacionada a item já existente → funde
                (c) pronta p/ amadurecer    → marca e oferece brainstorming
4. ENCAMINHAR itens "amadurecer agora" → invoca superpowers:brainstorming
                demais                  → confirma o que foi gravado no backlog
```

O passo 4 é o **estado terminal**: para amadurecer, a skill entrega o controle
ao `brainstorming` (que por sua vez leva a `writing-plans`). `seed-intake` não
segue além disso.

## Formato do backlog (`docs/specs/backlog.md`)

Arquivo único, uma entrada por ideia. Campos:

- `slug` — identificador kebab-case (ex.: `notificacoes-email`)
- **Título** — frase curta
- **Descrição** — 1 parágrafo
- **Capturado em** — data (absoluta)
- **Status** — `ideia` | `rascunho` | `pronto-p-brainstorm`
- **Relacionados** — links para ADRs/specs/planos pertinentes (se houver)

Modelo de entrada:

```markdown
### notificacoes-email — Notificações por e-mail
- **Status:** ideia
- **Capturado em:** 2026-07-18
- **Relacionados:** ADR-0006, follow-up (4) email transacional
- **Descrição:** Enviar e-mails transacionais (convite, recuperação de senha)
  a partir dos eventos de organização. Depende de provedor de e-mail definido.
```

Quando uma ideia vai ao brainstorming e vira spec formal em `docs/specs/`, a
entrada é marcada como concluída (com link para o spec gerado) ou removida do
backlog. A skill decide isso na hora do encaminhamento.

### Quando delegar à `product-secretary`

- **Delega:** compilados em Markdown, notas do celular/ChatGPT, listas
  misturadas de intenções/riscos/decisões, múltiplos temas de uma vez.
- **Faz direto:** ideia única e curta que já cabe numa entrada de backlog.

## Gatilho (descrição da skill)

A skill ativa por chamada explícita, com `description` escrita para reconhecer
frases como: "vamos falar de features do Seed", "quero registrar umas ideias",
"joga isso no backlog", "tenho umas ideias soltas do projeto". Nunca auto-ativa
no meio de outra tarefa.

## Componentes a criar

1. `.claude/skills/seed-intake/SKILL.md` — a skill (frontmatter + fluxo +
   procedimento de leitura/escrita via worktree da `main`).
2. `docs/specs/backlog.md` — o backlog inicial na `main` (cabeçalho + convenção
   de formato + vazio de ideias).
3. Setup do worktree — a skill verifica se `../seed-backlog` existe e, se não,
   guia a criação (`git worktree add ../seed-backlog main`). Feito uma vez.

## Critérios de sucesso

- Ao ser chamada, produz orientação curta e correta do estado do Seed.
- Um brain-dump de várias ideias termina com entradas bem-formadas no backlog
  (delegando à `product-secretary` quando bagunçado).
- Uma ideia marcada "pronta" resulta em invocação do `brainstorming`.
- A skill nunca escreve plano nem código por conta própria.
- Segue o padrão de idioma do Seed (português claro; termos técnicos no
  original).

## Riscos / questões em aberto

- **Contexto desatualizado:** "entender o projeto" depende de
  `seed-project-status` e dos docs estarem atualizados. Aceito na abordagem A;
  se virar problema, reavaliar o "painel de estado" (abordagem B).
- **Fronteira com `product-secretary`:** manter claro que `seed-intake` roteia
  e a `product-secretary` estrutura, para não duplicarem responsabilidade.
- **Setup do worktree:** exige um passo inicial (`git worktree add`). A skill
  deve **checar se o worktree já existe antes de criar** (para não dar erro em
  usos seguintes) e tratar o caso da `main` já estar checada no repo principal.
