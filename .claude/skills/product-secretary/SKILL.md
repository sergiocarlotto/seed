---
name: product-secretary
description: Organiza conversas baguncadas de produto, arquitetura e funcionalidades em documentos estruturados de entrada do projeto. Use quando o usuario fornecer compilados Markdown, notas livres, resumos do ChatGPT no celular, ideias de funcionalidades futuras, perguntas de projeto nao resolvidas ou listas misturadas de intencoes, demandas, riscos, decisoes e pendencias que precisam de triagem antes de virar documentacao oficial do Seed.
---
# Secretario De Produto

## Proposito

Criar uma skill que funcione como um secretario de produto e arquitetura para
conversas desordenadas sobre o projeto.

A skill deve receber compilados em Markdown vindos de conversas com ChatGPT,
celular, anotacoes livres ou discussoes soltas, e transformar esse material em
estrutura reutilizavel sem assumir que algo foi aprovado.

## Nome Sugerido

`product-secretary`

Alternativas:

- `idea-secretary`
- `conversation-intake`
- `product-intake`

`product-secretary` e o melhor nome inicial porque comunica que a skill nao e
um arquiteto autonomo nem um gerador de backlog. Ela organiza, classifica,
levanta pendencias e prepara material para decisao humana.

## Quando Usar

Usar quando o usuario trouxer:

- um `.md` compilado de conversa;
- ideias soltas sobre funcionalidades;
- discussoes sobre arquitetura ainda nao decididas;
- listas misturadas de problemas, desejos, riscos e tarefas;
- material que precisa virar demandas candidatas, pendencias ou documentos
  formais.

## Entrada Esperada

A entrada principal deve ser um arquivo Markdown com uma conversa resumida ou
compilada.

Campos desejaveis, mas nao obrigatorios:

- origem da conversa;
- data;
- objetivo percebido;
- trechos importantes;
- duvidas abertas;
- decisoes sugeridas pela conversa;
- itens que parecem acao futura.

## Saidas Esperadas

A skill deve gerar ou atualizar documentos de triagem, preferencialmente dentro
de uma pasta de trabalho separada, antes de mexer em documentos oficiais.

Saidas recomendadas:

- resumo executivo;
- intencoes humanas identificadas;
- demandas candidatas;
- funcionalidades futuras;
- pendencias;
- riscos;
- perguntas abertas;
- conflitos com ADRs ou fundacao;
- sugestoes de migracao para documentos formais;
- itens que precisam de aprovacao humana.

## Autoridade Da Skill

A skill pode:

- organizar material bruto;
- classificar ideias;
- apontar conflitos;
- sugerir proximos documentos;
- preparar rascunhos de requisitos, ADRs ou criterios.

A skill nao pode:

- alterar ADR aceito como se fosse detalhe operacional;
- adicionar escopo ao MVP sem aprovacao;
- transformar conversa em decisao aprovada;
- criar tarefa de implementacao como obrigatoria sem revisao humana;
- escolher stack, hospedagem, banco ou arquitetura sem seguir o modelo de ADR.

## Modelo De Classificacao

Classificar cada item em uma das categorias abaixo.

| Categoria | Significado |
| --- | --- |
| `intencao` | Objetivo humano ou problema de negocio percebido |
| `demanda-candidata` | Algo que pode virar requisito, tarefa ou modulo |
| `feature-futura` | Ideia valida, mas fora do escopo inicial ou sem validacao |
| `pendencia` | Algo que precisa ser resolvido, confirmado ou investigado |
| `decisao-candidata` | Escolha que pode exigir ADR ou aprovacao formal |
| `risco` | Possivel problema de produto, arquitetura, entrega ou operacao |
| `pergunta-aberta` | Ponto que precisa de resposta humana ou pesquisa |
| `descartado-por-agora` | Ideia preservada, mas sem acao recomendada no momento |

## Status Dos Itens

Cada item deve ter um status simples.

| Status | Uso |
| --- | --- |
| `capturado` | Veio da conversa, ainda sem analise suficiente |
| `triado` | Foi classificado e resumido |
| `precisa-de-decisao` | Depende de escolha humana |
| `aprovado-para-documentar` | Pode virar documento formal |
| `migrado` | Ja foi levado para documento oficial |
| `bloqueado` | Nao pode avancar sem informacao externa |
| `rejeitado` | Foi recusado explicitamente |

## Relacao Com A Fundacao Do Projeto

A skill deve sempre comparar o material com:

- `docs/foundation/marco-zero.md`;
- `docs/decisions/README.md`;
- ADRs aceitos;
- regras em `CLAUDE.md`.

Se a conversa sugerir algo que conflita com a fundacao ou ADRs, a saida correta
nao e aplicar a mudanca. A saida correta e registrar o conflito e propor uma
decisao candidata ou um novo ADR.

## Pasta De Trabalho Recomendada

Usar uma pasta separada por compilado:

```txt
docs/intake/YYYY-MM-DD-slug/
```

Estrutura recomendada:

```txt
source.md
triage.md
candidate-demands.md
future-features.md
open-questions.md
migration-proposal.md
```

`source.md` preserva o compilado recebido.

`triage.md` e a leitura organizada.

`migration-proposal.md` lista somente o que pode ser promovido para documentos
formais depois de aprovacao humana.

## Fluxo De Trabalho Da Skill

1. Ler as regras do repositorio e os ADRs aceitos.
2. Ler o compilado recebido.
3. Extrair intencoes antes de listar funcionalidades.
4. Classificar cada ideia pelo modelo acima.
5. Separar MVP, futuro, fora de escopo, risco e duvida.
6. Identificar conflitos com fundacao ou ADRs.
7. Gerar documentos de triagem em `docs/intake/`.
8. Pedir aprovacao antes de migrar qualquer item para documentos oficiais.

## Criterios De Qualidade

Uma boa execucao da skill:

- reduz bagunca sem apagar contexto;
- nao inventa decisoes;
- preserva duvidas como duvidas;
- diferencia desejo, requisito, decisao e tarefa;
- produz documentos que outra IA consiga continuar;
- deixa claro o que precisa de aprovacao humana;
- mantem o projeto principal livre de ideias ainda imaturas.

## Recursos

Leia `references/conversation-compile-template.md` quando o usuario pedir um modelo para compilar conversas no ChatGPT do celular ou quando a entrada ainda nao estiver estruturada.

## Uso Como Fonte Versionada

Esta skill deve permanecer versionada neste repositorio em `.claude/skills/product-secretary`. O Claude Code a descobre automaticamente ao trabalhar neste repositorio, sem passo de sincronizacao: a pasta versionada e a fonte de verdade e o local do qual o Claude carrega diretamente.
