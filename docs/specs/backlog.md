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

### gestao-usuarios-perfis-permissoes — CRUD de usuários e perfis de permissão
- **Status:** pronto-p-brainstorm
- **Capturado em:** 2026-07-19
- **Relacionados:** ADR-0006 (auth/autorização, papéis fixos), ADR-0010 (multiempresa), docs/modules/organizations.md
- **Descrição:** Telas/CRUD para gestão de usuários e um cadastro de "perfis
  de usuário" ao qual o usuário é vinculado, carregando um conjunto de permissões.
  Detalhes definidos com o usuário:
  - **Perfis são por organização** (cada org define os seus).
  - **Gerir perfis é, ela mesma, uma permissão** (meta-permissão): um perfil com
    ela permite criar/editar perfis e atribuir permissões.
  - **Granularidade evolutiva:** começa por funcionalidade (item de menu) e deve
    poder evoluir para nível de ação — verbos básicos como `ver`,
    `criar/editar`, `excluir`.
  - **Regras de negócio cercadas por permissão**, indo além de RBAC simples:
    posse/escopo (ex.: editar só as tarefas que a pessoa criou, não as de outro
    projeto sem permissão) e nível de campo (ex.: um perfil pode editar o campo
    "executor" de uma tarefa, outro não). Isso encosta em ABAC/field-level.
  **Atenção:** conflita com a ADR-0006, que define papéis fixos (`owner`, `admin`,
  `member`) e deixa **explicitamente** fora do MVP permissões granulares por
  campo/recurso. Este desenho é mais amplo (RBAC configurável + posse +
  field-level) e exigiria uma **nova ADR** substituindo/estendendo a ADR-0006
  (racional, tradeoffs, migração dos papéis atuais, impacto no enforcement de
  tenant). Peso claro de **segurança + arquitetura**. `User` e o vínculo
  usuário↔organização já existem via `User`/`OrganizationMembership`; a novidade
  real é o modelo perfis+permissões, a granularidade e o enforcement backend.
