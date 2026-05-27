# ADR-0001: Consolidacao do Repositorio

## Status

Aceita

## Contexto

O projeto inicialmente tinha um repositorio separado chamado `seed-skills` para
prompts e skills. Na pratica, isso criou custo de coordenacao desnecessario
durante o trabalho inicial de fundacao do produto, porque agentes precisavam
alternar entre dois repositorios, dois estados de git e duas possiveis fontes
de verdade.

A fundacao do produto Seed ja define documentacao, decisoes, prompts e
desenvolvimento assistido por IA como partes do mesmo sistema vivo. Manter
prompts e skills do projeto perto da fundacao e das ADRs torna o projeto mais
facil de inspecionar e evoluir por humanos e futuras IAs.

## Decisao

Usar este repositorio como a fonte versionada unica para:

- documentos de fundacao do produto;
- ADRs e registros de decisao;
- prompts do projeto em `prompts/`;
- skills do projeto em `skills/<skill-name>/`.

O repositorio separado `seed-skills` fica aposentado.

Skills locais do runtime Codex em `C:\Users\sergi\.codex\skills\` sao apenas
copias instaladas. Elas nao sao a fonte versionada de verdade.

## Racional

Isso mantem o conhecimento relacionado do projeto junto e reduz erros
operacionais. Tambem preserva rastreabilidade entre intencao do produto, papeis
de IA, prompts, skills, ADRs e trabalho futuro de implementacao.

O projeto ainda e pequeno, entao a consolidacao e mais simples e reversivel do
que manter um repositorio adicional.

## Consequencias

- Novas skills do projeto devem ser criadas em `skills/<skill-name>/`.
- Novos prompts do projeto devem ser criados em `prompts/`.
- Quando uma skill precisar ser usada pelo runtime local do Codex, sincronize-a
  de `skills/` para `C:\Users\sergi\.codex\skills\` com
  `tools/sync-codex-skills.ps1`.
- Futuras IAs nao devem recriar um repositorio `seed-skills` separado, salvo se
  uma nova ADR aceita substituir esta decisao.
- Mudancas de documentacao e skills agora podem ser revisadas juntas no mesmo
  historico git.

## Alternativas Consideradas

### Manter Um Repositorio `seed-skills` Separado

Rejeitado. Isso oferece separacao, mas o estagio atual do projeto se beneficia
mais de uma unica fonte de verdade do que de isolamento por repositorio.

### Manter Skills Apenas No Runtime Local Do Codex

Rejeitado. Pastas de runtime sao destinos de instalacao, nao artefatos duraveis
do projeto. Skills importantes para o projeto devem ser versionadas.

## Validacao

Esta decisao permanece valida se:

- futuras IAs conseguirem encontrar prompts e skills neste repositorio;
- o runtime local do Codex ainda puder ser atualizado a partir de `skills/`;
- decisoes de produto, prompts e skills permanecerem rastreaveis em conjunto;
- o repositorio permanecer compreensivel e nao acumular automacao pessoal nao
  relacionada.
