# ADR-0008: Padrao de Documentacao dos Modulos Backend

## Status

Pendente

## Contexto

O Marco Zero define modulos iniciais do MVP e exige documentacao viva com
objetivo, escopo, fora de escopo, criterios de aceite e validacao. A ADR-0003
define um backend em monolito modular com camadas de API, application, domain e
infrastructure.

Antes de implementar o primeiro modulo de negocio, o projeto precisa de um
padrao para documentar cada modulo de forma consistente e consultavel por
humanos e IAs.

## Decisao Pendente

Definir o formato minimo de documentacao dos modulos backend.

Esta ADR deve decidir:

- onde ficam os documentos de modulo;
- quais secoes sao obrigatorias;
- como relacionar modulo, ADR, requisitos, criterios de aceite e testes;
- como registrar fronteiras de dominio e dependencias;
- quando atualizar a documentacao;
- como evitar duplicacao excessiva entre docs e codigo.

## Direcao Inicial

Usar `docs/modules/<module-name>.md` para documentar cada modulo relevante.

Cada documento deve conter, no minimo:

- objetivo;
- escopo;
- fora de escopo;
- entidades envolvidas;
- casos de uso;
- regras de negocio;
- criterios de aceite;
- dependencias;
- eventos de auditoria;
- validacao esperada;
- decisoes relacionadas.

## Pontos Ainda Abertos

- Definir template final de modulo.
- Definir se criterios de aceite tambem terao copia em `docs/acceptance/`.
- Definir convencao de nomes entre portugues na documentacao e ingles no
  codigo.
- Definir quando uma mudanca de modulo exige nova ADR.

## Validacao

Esta decisao podera ser aceita se:

- futuras IAs conseguirem entender um modulo antes de alterar codigo;
- criterios de aceite ficarem verificaveis;
- a documentacao nao duplicar detalhes triviais de implementacao;
- mudancas relevantes de dominio continuarem rastreaveis.
