# ADR-0000: Fundacao do Produto

## Status

Aceita

## Contexto

O projeto precisa de uma fundacao duravel antes do inicio da implementacao. O
sistema deve se tornar um SaaS para gestao operacional, projetos de
implantacao, tarefas, documentacao viva e evolucao assistida por IA.

Futuras IAs e pessoas precisam de uma fonte unica de verdade para intencao do
produto, limites de escopo, principios, modulos, entidades, fluxos, riscos e
criterios de validacao.

## Decisao

Adotar `docs/foundation/marco-zero.md` como a fundacao formal Marco Zero do
produto.

Futuras decisoes arquiteturais, requisitos, planos de implementacao e
recomendacoes de IA devem respeitar essa fundacao, salvo se uma ADR aceita mais
recente a substituir.

## Consequencias

- `docs/foundation/marco-zero.md` passa a ser leitura obrigatoria para futuros
  trabalhos com IA.
- Direcao do produto, limites do MVP, papel da IA, regras de documentacao e
  principios de entrega ficam versionados no repositorio.
- Novas decisoes que conflitem com a fundacao devem ser registradas em uma nova
  ADR.
- A implementacao nao deve partir de ideias isoladas de funcionalidade; ela
  deve ser rastreavel ate a intencao do produto, limites de modulo e criterios
  de aceite.

## Alternativas Consideradas

### Manter O Marco Zero Apenas Como Prompt

Rejeitado. Um prompt e util para gerar raciocinio, mas nao e suficiente como
fonte duravel de verdade. O projeto precisa de um documento estavel que futuras
IAs possam consultar sem reconstruir contexto a partir de conversas anteriores.

### Colocar Toda A Fundacao Apenas No README

Rejeitado. O README deve permanecer como ponto de entrada. A fundacao precisa
de estrutura e detalhe suficientes para orientar produto, arquitetura,
documentacao e trabalho futuro com IA.

## Validacao

Esta decisao e valida se:

- futuras IAs conseguirem encontrar a fundacao do produto a partir de
  `CLAUDE.md`;
- a fundacao separar claramente MVP, evolucao futura e itens fora de escopo;
- novas decisoes puderem referenciar ou substituir a fundacao por meio de ADRs;
- o trabalho de implementacao puder ser rastreado ate intencao e criterios de
  aceite.

## Proximos Passos

- Definir a base backend em uma ADR futura.
- Definir banco de dados e propriedade dos dados em uma ADR posterior.
- Definir autenticacao e permissoes antes de implementar acesso de usuarios.
