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

### ui-erros-api-mensagens — Mensagens de erro da API na UI e sessão expirada
- **Status:** ideia
- **Capturado em:** 2026-07-22
- **Relacionados:** `apps/web/src/lib/api.ts`, ADR-0006 (sessão em cookie),
  `docs/specs/2026-07-21-user-provisioning-company-access-design.md`,
  [`ui-polimento-listas-mobile`](#ui-polimento-listas-mobile--polimento-de-ui-das-listas-e-telas-de-erro)
- **Descrição:** Levantado na revisão de coerência da entrega de provisionamento
  de usuários (2026-07-22). Dois problemas na borda entre a API e a UI:
  (a) **status sem corpo viram texto em inglês** — 404 e 403 são devolvidos com
  `NotFound()`/`Forbid()` sem payload, então `api.ts` cai no `res.statusText` e a
  tela mostra "Not Found" ou "Forbidden" em vermelho numa interface toda em
  português; acontece, por exemplo, ao salvar empresas de um usuário excluído em
  outra aba. Os 400 e 409 já estão certos, porque devolvem `{ error }`. Duas
  saídas possíveis: devolver `{ error }` também nesses status — as mensagens do
  backend já são deliberadamente neutras, então não vazam existência — ou mapear
  status → mensagem dentro de `errorMessage()`. (b) **401 não redireciona** — se a
  sessão expira durante um PUT, o usuário vê "Unauthorized" e fica preso na tela,
  sem caminho para `/login`; pede tratamento global no `api.ts`, não caso a caso.
  Nenhum dos dois afeta autorização (o backend barra de qualquer forma); é
  qualidade de uso.

### ui-rotulo-acao-lista-usuarios — Ação "Ver" na lista de usuários deveria ser "Editar"
- **Status:** ideia
- **Capturado em:** 2026-07-22
- **Relacionados:** `apps/web/src/components/UsersList.tsx:94`,
  `apps/web/src/components/ProfilesList.tsx:104-109` (precedente),
  `apps/web/src/app/(app)/companies/page.tsx:131-143` (precedente),
  ADR-0011 (UI)
- **Descrição:** A coluna "Ações" de `/users` rotula o botão como **"Ver"**, mas o
  destino é literalmente de acesso **e edição**: no detalhe do usuário se altera o
  status (switch), o conjunto de perfis e — desde a entrega de provisionamento de
  2026-07-22 — o conjunto de empresas. O rótulo descreve menos do que a ação faz.
  O projeto já tem o padrão certo em duas telas: `ProfilesList` usa "Ver" para o
  perfil `is_system` (esse sim somente-leitura) e "Editar" para os demais, e a
  lista de empresas passou a usar "Editar"/"Acessos" conforme a permissão do
  operador. **Nuance a considerar na correção:** "Ver" continua sendo o rótulo
  correto quando o alvo é o **owner**, que é somente-leitura na aplicação (não
  pode ser desativado nem ter perfis alterados — ADR-0012), e também para um
  operador sem nenhuma das permissões de edição. Ou seja, a correção provável não
  é trocar a palavra, e sim torná-la condicional, como já se faz nas outras duas
  listas.

### usuarios-crud-editar-excluir — Completar o CRUD de usuário: editar dados e excluir
- **Status:** ideia
- **Capturado em:** 2026-07-22
- **Relacionados:** `docs/modules/access-control.md`, ADR-0012 (owner somente-leitura),
  ADR-0013 (auditoria sem FK), ADR-0005 (soft delete como padrão),
  `apps/api/src/Seed.Api/Controllers/UsersController.cs`
- **Descrição:** O cadastro de usuário só cobre parte do CRUD. Hoje existem
  `GET /users`, `GET /users/{id}`, `POST /users`, `PATCH /users/{id}/status`,
  `PUT /users/{id}/profiles` e `PUT /users/{id}/companies`. **Desativar já está
  implementado** (`UserStatus.Inactive`: permissão efetiva vazia, login recusado,
  bloqueio imediato) — a ação existe como switch no detalhe do usuário, e o que
  eventualmente falta ali é visibilidade, já que ela não aparece na lista. O que
  **não existe** é: (a) **editar os dados básicos** — não há endpoint para alterar
  `fullName` nem `email`, então um nome digitado errado no cadastro não tem
  conserto pela aplicação; (b) **excluir**. Questões de design a resolver antes de
  implementar a exclusão, todas com resposta não óbvia: **soft ou hard?** O
  projeto usa soft delete (`DeletedAt`) em `Company`, `Profile` e `Organization`,
  mas `ApplicationUser` é do Identity e **não tem `DeletedAt`** — só `Status`;
  seria coluna nova, estado novo, ou "desativado" já basta e o que falta é apenas
  nomenclatura na UI? **E-mail preso:** o e-mail é único globalmente, então um
  usuário apenas desativado retém o endereço para sempre — quem sai e volta, ou um
  endereço a ser reaproveitado, não têm caminho. Isso é um argumento concreto a
  favor de exclusão real. **Auditoria:** `AuditEvents` é append-only e sem FK
  (ADR-0013), então excluir não quebra integridade, mas o histórico perde o rótulo
  humano do ator — vale conferir se os eventos de usuário gravam nome junto do id,
  como a ADR-0013 seção 3 exige para vínculos. **Cascata:** `UserProfile` e
  `UserCompanyAccess` têm FK com `Cascade`, então a exclusão leva os vínculos
  junto, silenciosamente. **Owner:** não pode ser excluído nem arquivado, pelo
  mesmo piso antilockout que já o protege de desativação (ADR-0012). **LGPD:**
  direito ao esquecimento pode transformar a exclusão real em requisito, não
  preferência — se for o caso, a decisão muda de natureza e pede ADR.

### acesso-postura-a — Anti-escalada "não conceder além de si" (postura A)
- **Status:** ideia
- **Capturado em:** 2026-07-19
- **Relacionados:** spec access-control (2026-07-19)
- **Descrição:** Evoluir o controle de acesso para a regra "um usuário só atribui
  perfis e define permissões contidas no seu próprio conjunto efetivo". Torna
  `profiles.manage`/`profiles.assign` genuinamente granulares e menos perigosas,
  substituindo a postura B do v1 (que trata essas permissões como privilégio
  administrativo e restringe só perfis `is_system`).

### email-transacional-convite-usuario — E-mail transacional: convite, recuperação de senha e troca obrigatória no primeiro login
- **Status:** ideia
- **Capturado em:** 2026-07-22
- **Relacionados:** ADR-0006 (sub-decisão de e-mail aberta desde o início),
  ADR-0013 (auditoria), docs/modules/access-control.md,
  `docs/specs/2026-07-21-user-provisioning-company-access-design.md`
- **Descrição:** Escolher fornecedor e implementar e-mail transacional, que
  destrava três coisas hoje ausentes: **convite de usuário por e-mail**,
  **recuperação de senha** e a **troca obrigatória de senha no primeiro login**.
  Este último item entrou aqui em 2026-07-22, com o provisionamento de usuários
  (`POST /users`): como o administrador define a senha inicial e **não existe**
  flag `must_change_password`, tela de troca nem gate de navegação, quem cria a
  conta retém indefinidamente uma credencial válida de outra pessoa e pode
  autenticar-se como ela. Consequência direta: todo `actor_user_id` daquele
  usuário passa a ser contestável, o que **enfraquece a não-repúdio** da trilha
  de auditoria recém-padronizada pela ADR-0013. A mitigação de v1 é **detecção,
  não prevenção** — o evento `access_control.user.created` registra quem criou a
  conta. O risco foi aceito explicitamente no design de 2026-07-21 (seção "Riscos
  aceitos"), amarrado ao convite por e-mail em vez de virar item isolado: com
  convite, a senha nunca chega a existir do lado do administrador, e a troca
  obrigatória deixa de ser remendo para virar consequência do fluxo.

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
