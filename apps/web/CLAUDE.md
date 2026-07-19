# apps/web — Next.js 16

Esta versao do Next.js (16.x) tem breaking changes: APIs, convencoes e estrutura
de arquivos podem diferir de versoes anteriores e de dados de treino. Antes de
escrever codigo aqui, leia o guia relevante em `node_modules/next/dist/docs/` e
respeite os avisos de deprecacao.

## Convenção de adaptação de conteúdo mobile (app shell)

O shell garante a moldura responsiva (gaveta, topbar enxuta, área que rola), mas
**não resolve o conteúdo**. Cada tela é responsável pela própria adaptação mobile,
seguindo esta convenção:

- No mobile (< `md`), **tabelas viram lista de cartões** (cada linha vira um cartão
  empilhado).
- Formulários passam a **uma coluna**.
- Ações secundárias vão para um menu "⋯".

Os primitivos genéricos (ex.: uma `TabelaResponsiva`) **não** existem ainda (YAGNI):
serão construídos junto da primeira tela que precisar, com um caso concreto. Cada
tela ganha sua atenção mobile no próprio design/spec.
