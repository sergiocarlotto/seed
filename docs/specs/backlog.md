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
- **Status:** ✅ **entregue** — ADR-0012 + ADR-0013, backend e frontend mergeados na `main` em 2026-07-21 (branch `feat/access-control`, 76 commits). O escopo v1 é RBAC configurável; posse/escopo e field-level continuam fora (ver `acesso-postura-a` e o "Fora de escopo (v1)" da spec).
- **Capturado em:** 2026-07-19
- **Relacionados:** ADR-0012, ADR-0013, docs/modules/access-control.md,
  `docs/specs/2026-07-19-access-control-perfis-permissoes-design.md`,
  `docs/specs/2026-07-20-access-control-frontend-perfis-usuarios-design.md`
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

### auditevent-escala — Escala do armazenamento do AuditEvent
- **Status:** ideia (o **contrato** já foi decidido na ADR-0013; resta a escala)
- **Capturado em:** 2026-07-19 · **Atualizado em:** 2026-07-21
- **Relacionados:** ADR-0013 (padrão do AuditEvent), ADR-0005,
  docs/modules/access-control.md
- **Descrição:** A ADR-0013 fechou a parte de **contrato**: taxonomia
  `<módulo>.<entidade>.<verbo>`, campos obrigatórios, `metadata` com `old`/`new`,
  emissão atômica com a mutação e retenção **indefinida no MVP**. Ficou de fora,
  de propósito, a **estratégia de escala do armazenamento**: índices por
  `(org, occurred_at)` e `(actor, occurred_at)`, **particionamento nativo do
  PostgreSQL por tempo** (range mensal em `occurred_at`), política de
  expurgo/arquivamento de partições frias e eventual migração de `metadata` para
  `jsonb` (se consultar por conteúdo virar requisito). A ADR-0013 adiou isso até
  existir volume real que justifique — retomar quando houver esse dado.

<!-- Entrada original, preservada para histórico do que a ADR-0013 resolveu: -->
### ~~auditevent-padronizacao~~ — Padronização do AuditEvent (ADR)
- **Status:** ✅ resolvida pela **ADR-0013** (2026-07-21)
- **Capturado em:** 2026-07-19
- **Relacionados:** ADR-0013, ADR-0005, spec access-control (2026-07-19)
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

### ui-polimento-listas-mobile — Polimento de UI das listas e telas de erro
- **Status:** ideia
- **Capturado em:** 2026-07-21
- **Relacionados:** apps/web/CLAUDE.md (convenção mobile), ADR-0011 (UI),
  `docs/specs/2026-07-20-access-control-frontend-perfis-usuarios-design.md`
- **Descrição:** Follow-ups menores identificados na validação de ponta a ponta do
  controle de acesso (2026-07-21), agrupados porque rendem um polimento conjunto:
  (a) **tabelas → cards no mobile** — a convenção do `apps/web/CLAUDE.md` ainda
  não foi aplicada em nenhuma lista; confirmado visualmente que `/users` e
  `/profiles` vazam horizontalmente no viewport de 390px, cortando colunas e as
  ações; é o item de maior impacto real; (b) **título do topbar nas telas RSC de
  erro** — 403/404 não chamam `useSetPageHeader`, então o topbar fica sem título;
  (c) **guard `isServerApiError`** no lugar do cast `as {status?}`;
  (d) trocar `Button render={<Link/>}` por `<Link className={buttonVariants(…)}>`
  — o Base UI emite warning de acessibilidade em 13 usos (pré-existente, também
  em `companies`), e a "correção" sugerida pela lib (`nativeButton={false}`)
  pioraria a semântica, pondo `role="button"` num link de navegação.

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

### agendamento-servicos-profissionais — Módulo de agendamento de serviços (profissionais + tarefas agendadas)
- **Status:** ideia
- **Capturado em:** 2026-07-19
- **Relacionados:** marco-zero (entidade **Tarefa**, "Tarefas Operacionais"
  avulsas), módulo de controle de acesso (`AccessControl` / ADR-0012, vínculo
  profissional↔usuário), ADR-0010 (multiempresa)
- **Descrição:** Módulo dedicado ao **agendamento de serviços**: cadastrar
  **profissionais** e atribuir a eles tarefas/serviços com **horário agendado**,
  reutilizando a mesma estrutura de Tarefa que atende projetos — mas para
  serviços avulsos com hora marcada. **Encaixe/insight:** a Tarefa da fundação
  já é reutilizável para trabalho avulso (o marco-zero prevê "tarefas vinculadas
  a projetos ou independentes"), então reaproveitar a estrutura não conflita com
  nada. A novidade real são três pontos ainda não modelados:
  (1) **agendamento ≠ prazo** — a Tarefa hoje tem `prazo` (deadline); serviço
  agendado precisa de um **slot de tempo** (início/fim/duração) num calendário.
  Encaixe natural: Tarefa ganha um **agendamento opcional (appointment)**, sem
  trocar o modelo, para projeto e serviço avulso compartilharem a entidade.
  (2) **"Profissional" é conceito novo** e a decisão-chave é se ele é sempre um
  `User` (faz login) ou um **recurso agendável** que pode não ter conta (prestador
  externo só agendado) — escolha que encosta direto no módulo de controle de
  acesso em construção e muda o modelo de dados.
  (3) **Calendário traz regras próprias**: disponibilidade do profissional,
  **conflito de horários** e visão de agenda — o que justifica um módulo
  dedicado, não só um campo extra na Tarefa.
  **Catálogo de serviços e combos:** cadastrar **serviços** pré-definidos com
  **duração e preço padrão** (ex.: 30 min, R$ 50) para **agendar rápido** — o
  serviço do catálogo funciona como **template** (padrão "modelo → instância",
  igual aos Templates de Projeto da fundação): ao agendar, gera uma execução
  concreta já preenchida. Além disso, **combos** = pacotes comerciais que agrupam
  serviços com **desconto** (ex.: X + Y valem R$ 50 cada avulso = R$ 100; no combo,
  R$ 80). **Insight-chave (regra de modelagem):** o combo é só a camada
  **comercial/precificação** — a **execução permanece granular por serviço**.
  Isso separa **três níveis** que não devem se misturar:
  **(a) Serviço (catálogo/tipo)** = template com duração + preço padrão;
  **(b) Combo (pacote)** = agrupa serviços e aplica desconto no total;
  **(c) Execução (agendamento)** = a instância rastreável (tarefa + horário +
  profissional + status). O **desconto vive no combo, nunca na execução**, para
  medir cada serviço isoladamente (quantos X, quanto tempo) independentemente de
  estar num combo. Isto traz **precificação** para o escopo — que o marco-zero
  deixa fora do MVP ("gestão financeira do projeto" fora de escopo) —, então a
  ADR do módulo deve tratar a fronteira "preço de serviço" vs. "financeiro do
  projeto".
  **Atenção:** peso claro de **arquitetura** (e agora **precificação/financeiro**).
  As decisões "Tarefa única com agendamento opcional vs. entidades separadas com
  base comum", "Profissional = User vs. recurso à parte" e "catálogo/combo/execução
  como três entidades" tocam a entidade central Tarefa e o modelo multiempresa
  (ADR-0010); ao amadurecer, pedem o `software-architect` e provavelmente uma
  **nova ADR**.
  **Nota de pesquisa — UI de calendário (não implementar do zero):** o calendário
  visual/scheduler deve **reusar uma biblioteca open source permissiva** e
  editável, nunca ser construído do zero (regra de portabilidade: OSS permissiva,
  sem lock-in, roda em VPS). Achado-chave da pesquisa (2026): a **visão por
  recurso/profissional** (profissional = linha/coluna na timeline) é justamente o
  **recurso pago** das libs grandes — FullCalendar Scheduler (comercial, ~US$480/ano),
  Schedule-X resource scheduler (premium), MUI X Event Timeline (Premium, beta).
  Opções **genuinamente livres com visão de recurso**: **react-big-schedule** (MIT)
  e **DayPilot Lite for React** (open source, colunas por pessoa). MUI X Event
  Calendar Community (MIT) serve se não for exigida timeline horizontal de recurso.
  **Evitar Cal.com** como base de código (é app completo e **AGPL** — copyleft de
  rede, conflita com portabilidade/SaaS). Escolha final = decisão de arquitetura
  (`software-architect` + ADR de dependência) na maturação.
