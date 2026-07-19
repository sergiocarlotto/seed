# Design — App Shell (estrutura básica de apresentação)

**Data:** 2026-07-18
**Status:** Aprovado (brainstorming) — pendente de plano de implementação
**Escopo:** `apps/web` (frontend)

## Contexto

O módulo `organizations` (multiempresa) já está implementado e verificado: login,
sessão (`/auth/me`), e CRUD de empresa com acesso explícito. Antes de construir
várias telas de negócio, é preciso definir a **estrutura básica de apresentação**
(o *app shell*): a moldura comum sobre a qual todas as telas autenticadas vão
nascer — layout, navegação, cabeçalho, seletor de empresa ativa, área de conteúdo
e os estados padrão (carregando, erro, vazio, sem acesso).

Existe uma referência visual de destino (estilo "Central One": app corporativo
denso com menu por módulos, busca global, notificações, mensagens, abas,
favoritos e seletor de módulos). Essa referência é o **norte de longo prazo**,
não o alvo desta iteração. Aqui construímos apenas o **esqueleto essencial**; os
recursos avançados da referência ficam como espaço reservado/futuro.

Restrições vigentes:

- ADR-0002 / ADR-0011: base de UI é **shadcn/ui + Tailwind CSS** (interina). O
  design system formal (tokens próprios, modo escuro, componentes de marca) fica
  adiado para ADR própria. **Nenhuma biblioteca de UI nova** nesta iteração.
- `apps/web/CLAUDE.md`: Next.js 16 tem breaking changes; seguir os guias em
  `node_modules/next/dist/docs/` ao implementar.
- Backend **não** muda nesta iteração: não existe conceito de "empresa ativa" na
  API; ele nasce no frontend, modelado para o backend assumir depois.

## Objetivo

Entregar um shell responsivo e consistente que:

1. Dá a toda tela autenticada a mesma moldura (menu lateral + barra superior +
   área de conteúdo).
2. Introduz a **empresa ativa global** como escopo do app.
3. Padroniza os quatro **estados de tela** (carregando, erro, vazio, sem acesso)
   como componentes reutilizáveis.
4. Fixa o **padrão de crescimento** da navegação (por módulo, definida como
   dados) para as próximas telas.

## Fora de escopo

- Sistema de abas, busca global funcional, painéis de notificações e mensagens,
  favoritos, seletor de módulos, criação rápida — todos da referência, tratados
  como "próximos".
- Modo escuro / tema formal (adiado pela ADR-0011).
- Telas de negócio novas. `/login` e `/companies` apenas **migram** para a nova
  estrutura; nenhuma tela nova é criada.
- Alterações de backend. A "empresa ativa" é 100% frontend por enquanto.
- Primitivos de adaptação de conteúdo mobile (ex.: "tabela→cartões"). Só a
  **convenção** é documentada; a construção fica com a primeira tela que precisar.

## Decisões (resumo)

| Tema | Decisão |
| --- | --- |
| Alcance | Esqueleto essencial; recursos avançados como espaço reservado |
| Empresa ativa | **Global** — dá escopo ao app |
| Local do seletor de empresa | **Barra superior** |
| Navegação | **Agrupada por módulo**, definida como **dados/config** |
| Grupos do menu | **Sempre visíveis** (não-colapsáveis) nesta versão |
| Papel/acesso | Menu **filtrado por permissão**; acesso direto por URL → estado "sem acesso" |
| Responsividade | **Totalmente responsivo** (desktop / tablet / mobile) |
| Arquitetura | **Layout de servidor + ilhas cliente** (Abordagem A) |
| Adaptação de conteúdo mobile | Responsabilidade **por-tela**; shell só documenta a convenção |

## Arquitetura

### Abordagem escolhida — Layout de servidor + ilhas cliente

O `(app)/layout.tsx` é um **componente de servidor** que busca a sessão uma única
vez e monta o casco; as partes interativas são **componentes cliente** isolados
("ilhas"). A empresa ativa vive num **cookie** (para o servidor ler o escopo) e
num **contexto cliente** (para a UI reagir à troca).

Alternativas descartadas:

- **Tudo cliente:** mais simples de escrever (igual ao código atual), mas cada
  tela rebusca o `me`, o casco não tem SSR e migrar para "empresa ativa" no
  backend vira retrabalho.
- **Híbrido enxuto (sem cookie):** empresa ativa só no cliente/`localStorage`; o
  servidor não enxerga o escopo — anula o principal ganho quando as telas futuras
  forem server components escopadas por empresa.

### Áreas de rota (*route groups*)

```
src/app/
  layout.tsx            # raiz (html/body, fontes) — já existe
  page.tsx              # decide login vs (app) por sessão — já existe
  (auth)/
    layout.tsx          # casca mínima, centralizada (sem shell)
    login/page.tsx      # movido de app/login
  (app)/
    layout.tsx          # servidor: sessão + empresa ativa + monta o shell
    companies/...        # movido de app/companies (só conteúdo)
```

- **`(auth)`** — telas fora do shell (hoje: `/login`).
- **`(app)`** — tudo dentro do shell (autenticado).

### Fluxo do `(app)/layout.tsx` (servidor)

1. Lê o cookie de sessão. Sem sessão válida → `redirect("/login")`.
2. Busca o `me` (usuário, `orgRole`, empresas acessíveis).
3. Resolve a **empresa ativa** a partir do cookie `active-company` (ver seção
   própria).
4. Envolve as páginas filhas nos providers cliente (`SessionProvider`,
   `ActiveCompanyProvider`) e renderiza o casco (sidebar + topbar + conteúdo).

Resultado: `me` e empresa ativa disponíveis para qualquer tela — servidor ou
cliente — sem cada uma rebuscar.

## Componentes do shell

### Menu lateral (navegação)

**Navegação como dados.** A estrutura é uma configuração, não JSX escrito à mão:

```ts
type NavItem = {
  label: string;      // "Empresas"
  href: string;       // "/companies"
  icon: IconName;
  roles?: OrgRole[];  // opcional: quem vê. Ausente = todos os papéis
};

type NavModule = {
  label: string;      // "Administração"
  icon: IconName;
  items: NavItem[];
};
```

Estado inicial real (só o que existe hoje):

- **Administração** → **Empresas** (`/companies`).
- *Usuários* fica preparado como próximo item, **não** entra agora.
- Módulos do mock (Projetos, Relatórios, Comunicação, Configurações) **não**
  entram — adicioná-los depois é só acrescentar itens na config.

**Filtro por papel.** Antes de renderizar, remove itens cujo `roles` não inclui o
papel do usuário; um módulo sem itens visíveis desaparece. Hoje todo usuário
autenticado vê suas empresas, então "Empresas" fica visível a `Admin` e `Member`;
o gate por papel já fica pronto para as próximas telas.

**Estrutura visual:**

- Topo: logo + nome da plataforma + botão de recolher.
- Corpo: módulos (sempre visíveis, não-colapsáveis) com seus itens; o item da
  rota atual recebe destaque ("ativo").
- Rodapé: área reservada para sessão/suporte (Ajuda, Sair). **Sair** funcional;
  os demais são espaço reservado.
- **Recolhido:** faixa estreita só com ícones; rótulos em *tooltip*. Preferência
  recolhido/expandido persistida no navegador.

### Barra superior (topbar)

Somente estrutura — sem busca/notificações/mensagens funcionais. Da esquerda para
a direita:

- **Botão de menu** — recolhe/expande a sidebar (desktop) ou abre a gaveta
  (mobile).
- **Título da rotina + breadcrumb** — cada página do `(app)` declara seu
  título/caminho via um **contexto de "cabeçalho de página"** que a página
  preenche e a topbar lê. A topbar é única; cada tela diz o que mostrar.
- **Busca (desabilitada)** — campo visual "Pesquisar no sistema" **desabilitado**,
  ancorando o layout; sem função. Some no mobile.
- **Seletor de empresa ativa** — mostra a empresa ativa e permite trocar
  (dropdown com as empresas acessíveis). Ver seção própria.
- **Área reservada de ações globais** — criação rápida, notificações, mensagens,
  ajuda: **não** entram agora (nem como ícone); registradas como "próximos".
- **Menu do usuário** — avatar + nome, com dropdown. Funcionais: identificação
  (nome/e-mail/papel) e **Sair**. Itens do mock (Minha conta, Preferências,
  Alterar senha) aparecem **acinzentados/desabilitados**, sinalizando que virão;
  "trocar empresa" já é atendido pelo seletor dedicado.

### Área de conteúdo e estados padrão

A área de conteúdo é a região à direita do menu e abaixo da topbar. O shell provê
só a moldura (espaçamento, largura máxima, rolagem própria); o miolo é da página.

Quatro **estados padrão** entregues como **componentes reutilizáveis** (em
`components/states/`, coerentes com shadcn/ui — sem biblioteca nova), para que
toda tela futura os use igual:

- **`Loading`** — *skeletons* (blocos que imitam o formato do conteúdo) para
  layouts previsíveis, ou indicador simples para casos genéricos.
- **`ErrorState`** — mensagem amigável + causa quando útil + botão **"Tentar
  novamente"**. Reaproveita `errorMessage()` de `lib/api.ts`.
- **`EmptyState`** — título curto, texto de apoio e ação principal opcional
  (ex.: "Nova empresa" para Admin).
- **`NoAccess`** — quando o usuário chega por URL direta a uma tela/recurso que o
  papel não permite, ou a recurso de outra empresa/sem concessão. Mensagem clara
  + caminho de volta. É o par visual do `403`/`404` que o backend já devolve.

**Encadeamento padrão** que as telas seguem: `carregando → (erro | vazio |
conteúdo)`, com `sem acesso` quando a resposta indica falta de permissão/recurso.

O **`NoAccess`** é renderizado **dentro** da área de conteúdo (menu e topbar
visíveis), não como página cheia — o usuário continua orientado e pode navegar.

## Empresa ativa (comportamento)

Nasce no frontend, modelada para o backend assumir depois sem retrabalho.

**Onde vive:** cookie `active-company` (só o `id`) + contexto cliente
`ActiveCompanyProvider` (expõe a empresa ativa e a função de trocar). Cookie para
o servidor montar no escopo certo; contexto para a UI reagir à troca.

**Resolução no carregamento** (layout de servidor, a cada navegação):

1. Lê `active-company` do cookie.
2. Confere se o `id` está entre as empresas do `me`.
3. **Válido** → é a empresa ativa. **Inválido/ausente** → *fallback* para a
   primeira empresa (ordenada por nome) e corrige o cookie.

Regra dura: **a empresa ativa é sempre uma empresa que o usuário realmente
acessa** — nunca um valor solto vindo do navegador.

**Troca de empresa** (seletor da topbar): grava o novo `id` no cookie e chama
`router.refresh()`, re-renderizando os server components no novo escopo. A
navegação **permanece na mesma rota** (só recarrega os dados). Efeito: telas
futuras reaparecem filtradas pela nova empresa, sem piscar.

**Usuário sem nenhuma empresa** (Member sem concessões, ou antes do primeiro
acesso):

- O **seletor** mostra estado vazio ("Nenhuma empresa disponível"), sem opções.
- Telas que exigem empresa mostram `EmptyState`/`NoAccess` explicando que ele
  precisa receber acesso (ação do Admin — fora do escopo desta iteração).
- O shell **não quebra**: menu e topbar seguem funcionando.

## Responsividade

*Breakpoints* padrão do Tailwind (assumidos pelo shadcn/ui).

- **Desktop (`lg`+):** menu fixo ao lado do conteúdo, alternando
  expandido/recolhido; preferência persistida. Topbar completa.
- **Tablet (`md`):** menu recolhido por padrão (só ícones), expansível; busca
  central pode encolher; breadcrumb pode abreviar para só o título.
- **Mobile (< `md`):** menu vira **gaveta sobreposta** (*drawer*) com fundo
  escurecido, fecha ao escolher item ou tocar fora. Topbar enxuta (menu + título
  curto + seletor compacto + menu do usuário). Busca desabilitada **some**.
  Conteúdo em largura total.

**Regras gerais:**

- Sem rolagem horizontal na página; conteúdo largo rola dentro do próprio
  container.
- Alvos de toque confortáveis no mobile.
- Só tema claro (ADR-0011), mantendo os *tokens* do shadcn/ui — habilitar modo
  escuro no futuro será ligar o tema, sem refazer telas.

### Adaptação de conteúdo mobile — fronteira de responsabilidade

O shell garante uma moldura responsiva sólida (gaveta, topbar enxuta, área que
rola), mas **não resolve o conteúdo**. Tabelas densas, formulários longos e
futuros quadros precisam de tratamento próprio no celular — responsabilidade de
**cada tela**, não do shell.

**Convenção registrada** (a ser seguida quando cada tela existir):

- No mobile, **tabelas viram lista de cartões** (cada linha vira um cartão
  empilhado).
- Formulários passam a **uma coluna**.
- Ações secundárias vão para um menu "⋯".

Os **primitivos genéricos** dessa adaptação (ex.: um "TabelaResponsiva") **não**
são construídos agora (YAGNI): sem conteúdo real, a forma seria chutada. Serão
construídos **junto da primeira tela** que precisar, com um caso concreto na mão,
e cada tela ganha sua atenção mobile no próprio design/spec.

## Migrações desta iteração

- `/login` migra de `app/login` para `(auth)/login` (comportamento inalterado).
- `/companies` (e `companies/new`, `companies/[id]`) migra para `(app)`, perdendo
  o cabeçalho e o botão "Sair" próprios — que passam a ser do shell. O conteúdo
  (lista, criar, editar, excluir) permanece igual, agora usando os estados padrão
  (`Loading`/`ErrorState`/`EmptyState`) do shell no lugar dos textos inline
  atuais ("Carregando...", "Nenhuma empresa ainda.").
- `page.tsx` raiz continua decidindo `/login` vs `(app)` pela presença da sessão.

## Componentes e organização de arquivos (indicativo)

```
src/
  app/
    (auth)/layout.tsx, login/page.tsx
    (app)/layout.tsx, companies/...
  components/
    shell/            # AppSidebar, AppTopbar, CompanySwitcher, UserMenu, MobileDrawer
    states/           # Loading, ErrorState, EmptyState, NoAccess
    ui/               # shadcn/ui (existente)
  lib/
    nav.ts            # config da navegação (NavModule[]) + filtro por papel
    active-company.ts # leitura/escrita do cookie + resolução/fallback
    session.ts        # contexto de sessão (me) no cliente
    page-header.ts    # contexto do cabeçalho de página (título/breadcrumb)
```

## Impacto em ADRs

- **Compatível com a ADR-0011:** usa exclusivamente shadcn/ui + Tailwind; adia
  modo escuro e componentes de marca. Não requer nova ADR.
- Se, ao implementar, surgir necessidade de decisão estrutural durável (ex.:
  padrão oficial de *route groups* ou de navegação-como-dados), avaliar registrar
  em ADR própria — conforme regra do `CLAUDE.md` do projeto.

## Critérios de aceite

- [ ] Área autenticada envolvida por `(app)/layout.tsx` (servidor) que busca o
      `me` uma vez e monta sidebar + topbar + conteúdo.
- [ ] `/login` em `(auth)` (sem shell); `/companies` em `(app)` (com shell), sem
      cabeçalho/Sair próprios.
- [ ] Menu lateral gerado a partir de config de dados, filtrado por papel, com
      item ativo destacado e recolher (ícones + tooltip) persistido.
- [ ] Barra superior com título/breadcrumb dinâmicos, busca desabilitada,
      seletor de empresa ativa e menu do usuário (itens futuros acinzentados).
- [ ] Empresa ativa resolvida via cookie com fallback validado contra o `me`;
      troca via seletor recarrega o escopo na mesma rota; caso "sem empresas"
      tratado sem quebrar o shell.
- [ ] Componentes `Loading`, `ErrorState`, `EmptyState`, `NoAccess`
      reutilizáveis; `/companies` migrada para usá-los.
- [ ] Responsivo: gaveta no mobile, recolhido no tablet, fixo no desktop; sem
      rolagem horizontal.
- [ ] Convenção de adaptação de conteúdo mobile documentada (tabelas→cartões,
      formulários em uma coluna), sem construir primitivos.

## Decisões relacionadas

- ADR-0011 (UI/shadcn interino), ADR-0002 (base frontend), ADR-0010
  (multiempresa), ADR-0006 (auth/cookie).
- Módulo: `docs/modules/organizations.md`.
- Design do módulo: `docs/specs/2026-07-18-organizations-multiempresa-design.md`.
