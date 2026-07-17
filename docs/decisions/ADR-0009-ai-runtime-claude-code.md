# ADR-0009: Runtime de IA Assistida e Local das Skills

## Status

Aceita

## Contexto

O projeto foi iniciado usando o Codex (OpenAI) como runtime de desenvolvimento
assistido por IA. Por isso, decisoes anteriores assumiram esse runtime:

- as skills do projeto viviam em `skills/<skill-name>/` e eram sincronizadas para
  `C:\Users\sergi\.codex\skills\` com `tools/sync-codex-skills.ps1`
  (ver [ADR-0001](ADR-0001-repository-consolidation.md));
- cada skill tinha um arquivo `agents/openai.yaml` com metadados de interface do
  Codex;
- as regras de trabalho para IAs viviam em `AGENTS.md`;
- varias descricoes de skill diziam "Use quando Codex precisar".

O dono do projeto decidiu migrar o desenvolvimento assistido por IA do Codex para
o Claude Code. O Claude Code descobre skills de projeto diretamente em
`.claude/skills/<skill-name>/`, sem passo de sincronizacao, e carrega
automaticamente um `CLAUDE.md` na raiz do repositorio como regras de trabalho.

Manter a estrutura orientada ao Codex exigiria manutencao dupla e deixaria o
repositorio inconsistente com o runtime realmente usado.

## Decisao

Adotar o Claude Code como runtime de desenvolvimento assistido por IA do projeto
e alinhar a estrutura do repositorio a esse runtime:

- as skills versionadas do projeto passam a viver em
  `.claude/skills/<skill-name>/`, que e a fonte de verdade e o local do qual o
  Claude Code carrega diretamente;
- nao ha mais passo de sincronizacao para um runtime externo;
- os arquivos `agents/openai.yaml` sao removidos;
- o script `tools/sync-codex-skills.ps1` e removido;
- as regras de trabalho para IAs passam de `AGENTS.md` para `CLAUDE.md`, que o
  Claude Code carrega automaticamente;
- as descricoes de skill deixam de mencionar um runtime especifico e passam a ser
  neutras.

A decisao central da [ADR-0001](ADR-0001-repository-consolidation.md) permanece
valida: este repositorio continua sendo a fonte versionada unica para fundacao,
ADRs, prompts e skills. Esta ADR substitui apenas o local das skills e a
dependencia do runtime Codex definidos naquela decisao.

## Racional

- O Claude Code descobre skills de projeto automaticamente, eliminando o script
  de sync e a duplicacao entre fonte versionada e pasta de runtime.
- Uma unica fonte de verdade (`.claude/skills/`) reduz o risco de copias
  divergentes e de duplicatas aninhadas.
- `CLAUDE.md` na raiz e o mecanismo nativo do Claude Code para regras de projeto,
  garantindo que elas entrem no contexto sem passo manual.
- O formato `SKILL.md` (frontmatter com `name` e `description` mais corpo) e
  compativel entre os runtimes, entao o conteudo das skills foi preservado quase
  integralmente; a migracao foi de estrutura e nomenclatura, nao de reescrita.

## Consequencias

- Novas skills do projeto devem ser criadas em `.claude/skills/<skill-name>/`.
- Novos prompts do projeto continuam em `prompts/`.
- Nao existe mais o passo de sincronizacao para `~/.codex/skills`; qualquer copia
  antiga nesse diretorio e apenas um residuo de instalacao e nao e fonte de
  verdade.
- As regras de trabalho para IAs devem ser lidas em `CLAUDE.md`.
- Futuras IAs nao devem recriar `agents/openai.yaml` nem um script de sync para
  Codex, salvo se uma nova ADR aceita reintroduzir suporte a outro runtime.
- A portabilidade do produto em si (Linux, Docker, VPS) nao e afetada: esta
  decisao trata apenas do runtime de desenvolvimento assistido, nao do runtime de
  producao.

## Alternativas Consideradas

### Manter a estrutura do Codex e adicionar a camada do Claude ao lado

Rejeitado. Suportar os dois runtimes ao mesmo tempo dobra a manutencao das
skills e das regras e aumenta o risco de fontes divergentes, sem beneficio
concreto agora que o desenvolvimento migrou para o Claude Code.

### Manter as skills em `skills/` e criar um sync para `~/.claude/skills`

Rejeitado. Isso replicaria o modelo de sincronizacao do Codex sem necessidade. O
Claude Code descobre skills de projeto diretamente em `.claude/skills/`, entao um
script de sync so adicionaria um passo manual e uma segunda copia.

## Validacao

Esta decisao permanece valida se:

- o Claude Code encontrar e usar as skills do projeto a partir de
  `.claude/skills/` sem passo de sincronizacao;
- as regras de trabalho forem carregadas a partir de `CLAUDE.md`;
- o repositorio nao voltar a acumular arquivos ou scripts especificos do Codex;
- fundacao, ADRs, prompts e skills permanecerem rastreaveis em conjunto no mesmo
  historico git.
