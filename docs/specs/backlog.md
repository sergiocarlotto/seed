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
- **Status:** ✅ virou spec — `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md` (brainstorming + revisão de segurança concluídos; próximo passo: nova ADR + plano)
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

### auditoria-visualizador — Visualizador/gerenciador de auditoria
- **Status:** ideia
- **Capturado em:** 2026-07-19
- **Relacionados:** ADR-0005 (AuditEvent), spec access-control (2026-07-19)
- **Descrição:** UI para consultar, filtrar e exportar eventos de auditoria (ex.:
  "tudo o que um usuário fez num período"). Os eventos já são emitidos pelos
  módulos; falta a camada de leitura. Exige cuidado de **escala** (volume,
  retenção, indexação, paginação) — por isso adiado. Depende do contrato
  padronizado de `AuditEvent`.

### auditevent-padronizacao — Padronização e escala do AuditEvent (ADR)
- **Status:** ideia
- **Capturado em:** 2026-07-19
- **Relacionados:** ADR-0005, spec access-control (2026-07-19)
- **Descrição:** Formalizar por ADR o contrato comum de `AuditEvent` a todos os
  módulos: `actor_user_id`, `occurred_at`, `organization_id`, `action`
  (`<módulo>.<entidade>.<verbo>`), `target`/`entity`, e `metadata` no formato
  **antes/depois (`old`/`new`)** por mudança. Habilita relatórios transversais
  por usuário/período. Inclui a **estratégia de escala do armazenamento**:
  tabela `audit_events` dedicada, append-only, autossuficiente e indexada por
  `(org, occurred_at)` e `(actor, occurred_at)`; evolução para **particionamento
  nativo do PostgreSQL por tempo** (range mensal em `occurred_at`) + política de
  **retenção/arquivamento** de partições frias — transparente à aplicação. O spec
  de access-control já adota o contrato como padrão de trabalho; a ADR o ratifica
  e decide retenção/particionamento para o sistema. Snapshot/versionamento
  completo por versão (além do diff) é item separado, se necessário.

### acesso-postura-a — Anti-escalada "não conceder além de si" (postura A)
- **Status:** ideia
- **Capturado em:** 2026-07-19
- **Relacionados:** spec access-control (2026-07-19)
- **Descrição:** Evoluir o controle de acesso para a regra "um usuário só atribui
  perfis e define permissões contidas no seu próprio conjunto efetivo". Torna
  `profiles.manage`/`profiles.assign` genuinamente granulares e menos perigosas,
  substituindo a postura B do v1 (que trata essas permissões como privilégio
  administrativo e restringe só perfis `is_system`).

### i18n-troca-idioma — Internacionalização e troca de idioma da UI
- **Status:** ideia
- **Capturado em:** 2026-07-19
- **Relacionados:** ADR-0004 (padrão de idioma — hoje só código/docs), app-shell
  (spec 2026-07-18), ADR-0002 (Next.js)
- **Descrição:** Permitir que a UI troque de idioma facilmente (português,
  inglês, espanhol, etc.), de forma portável. O seletor de idioma vive no
  **app-shell**; a base técnica é o roteamento i18n nativo do Next.js (App
  Router) + uma lib de mensagens (ex.: `next-intl`). Escopo além de strings:
  formatação de **data, número e moeda** por locale. **Encaixe/insight:** é uma
  *feature futura*, mas o custo real não é traduzir — é **não hardcodar strings**.
  Se as telas nascerem usando chaves de tradução (`t("users.title")`) em vez de
  texto literal, adicionar idiomas depois é quase de graça; caso contrário, vira
  varredura de todas as telas. Estende a ADR-0004 para o idioma da UI em runtime
  (hoje ela só rege código e documentação). A decisão de disciplina é de agora,
  mesmo que a entrega seja futura.

### temas-cores-design-tokens — Temas e cores configuráveis (claro/escuro + temas de marca)
- **Status:** ideia
- **Capturado em:** 2026-07-19
- **Relacionados:** ADR-0011 (abordagem de UI/design system — modo escuro e
  tokens formais explicitamente adiados), ADR-0002 (Tailwind + shadcn/ui)
- **Descrição:** Definir as cores principais do sistema (claro/escuro) e permitir
  **temas nomeados** (ex.: um tema "azul escuro") aplicáveis a todas as telas de
  uma vez, com padrão bem definido de botões, barras, menus e cores de texto.
  **Encaixe/insight:** é literalmente o **design system formal adiado pela
  ADR-0011** (que já lista "tokens compartilhados formais" e "modo escuro
  toggle" como itens futuros de ADR própria). O shadcn/ui já usa **CSS variables
  como tokens semânticos** (`--primary`, `--background`, `--foreground`,
  `--border`, `--muted`…), então claro/escuro é quase nativo e um tema novo vira
  um **conjunto alternativo de valores** desses tokens. Condição para "trocar
  tudo de uma vez": as telas usarem **tokens semânticos** (`bg-primary`,
  `text-foreground`) e nunca cores literais (`bg-blue-600`) — disciplina que
  precisa valer desde já. Quando amadurecer, deve virar a **ADR do design system**
  prevista na ADR-0011.
