# ADR-0008: Padrao de Documentacao dos Modulos Backend

## Status

Aceita

## Contexto

O Marco Zero define modulos iniciais do MVP e exige documentacao viva com
objetivo, escopo, fora de escopo, criterios de aceite e validacao. A ADR-0003
define um backend em monolito modular com camadas de API, application, domain e
infrastructure. A ADR-0004 define idioma (documentacao de produto em portugues,
codigo e contratos tecnicos em ingles). A ADR-0005 define propriedade dos dados,
tenancy por `organization_id` e auditoria. A ADR-0006 define papeis e
enforcement de acesso.

Antes de implementar o primeiro modulo de negocio, o projeto precisa de um
padrao consistente e consultavel por humanos e IAs para documentar cada modulo.

## Decisao

### Localizacao e nomeacao

Cada modulo relevante e documentado em `docs/modules/<module-name>.md`.

O nome do arquivo usa **ingles em kebab-case**, correspondendo ao nome do modulo
no codigo (ex.: `organizations.md`, `projects.md`, `tasks.md`). A prosa dentro
do documento e em **portugues** (ADR-0004); identificadores de codigo, entidades,
tipos, endpoints e nomes de modulo aparecem em ingles.

Um template versionado fica em `docs/modules/_template.md` e e a base para novos
modulos.

### Secoes obrigatorias

Cada documento de modulo deve conter, no minimo:

- **Objetivo**: o que o modulo resolve.
- **Escopo**: o que pertence ao modulo.
- **Fora de escopo**: o que explicitamente nao pertence.
- **Entidades envolvidas**: entidades de dominio e propriedade por organizacao.
- **Casos de uso**: acoes suportadas na camada de application.
- **Regras de negocio**: invariantes e validacoes criticas do dominio.
- **Autorizacao e tenancy**: papeis que podem agir e como o filtro de tenant se
  aplica (ADR-0005, ADR-0006).
- **Criterios de aceite**: condicoes verificaveis de conclusao.
- **Eventos de auditoria**: `AuditEvent` emitidos pelos casos de uso (ADR-0005).
- **Dependencias**: outros modulos, contratos e ADRs.
- **Validacao esperada**: testes minimos (unit e integracao) que provam o modulo.
- **Decisoes relacionadas**: ADRs que restringem ou originam o modulo.

### Relacao entre modulo, ADR, requisitos, criterios e testes

- Os **criterios de aceite** vivem no documento do modulo como **fonte unica**.
  Nao ha copia em `docs/acceptance/` no MVP, para evitar duplicacao; um indice
  agregado so sera criado se houver necessidade concreta, via nova decisao.
- Cada criterio de aceite deve ser rastreavel a um ou mais **testes** (unit ou
  integracao), conforme a ADR-0003.
- O documento referencia as ADRs que o restringem, em vez de repetir o conteudo
  delas.

### Fronteiras de dominio e dependencias

- O documento declara as fronteiras do modulo e suas dependencias explicitas.
- Relacionamentos entre entidades de organizacoes diferentes permanecem
  bloqueados (ADR-0005); o documento nao pode propor excecoes silenciosas.

### Quando atualizar

- Ao criar um modulo, antes de implementar o codigo dele.
- Ao mudar objetivo, escopo, entidades, regras de negocio, autorizacao ou
  criterios de aceite.
- Uma **nova ADR** e exigida quando a mudanca altera **fronteira de dominio,
  contrato entre modulos, modelo de dados compartilhado ou regra de seguranca e
  tenancy**. Mudancas internas de implementacao que nao afetam essas fronteiras
  nao exigem ADR, apenas atualizacao do documento do modulo.

### Evitar duplicacao com o codigo

O documento descreve intencao, fronteiras, regras e criterios, nao detalhes
triviais de implementacao. Assinaturas exatas, DTOs e nomes internos vivem no
codigo e no contrato OpenAPI (ADR-0003), nao devem ser copiados no documento.

## Consequencias

- O primeiro modulo (provavelmente `organizations`, base de tenancy) so comeca a
  ser implementado apos ter seu `docs/modules/organizations.md`.
- Existe um `docs/modules/_template.md` versionado para padronizar novos modulos.
- Revisores humanos e IAs conseguem entender um modulo antes de alterar codigo.
- Criterios de aceite ficam verificaveis e ligados a testes.

## Alternativas Consideradas

### Copiar criterios de aceite tambem em `docs/acceptance/`

Rejeitado no MVP. Duplicaria informacao e criaria risco de divergencia. Um
indice agregado de aceite pode ser criado depois, se houver necessidade real.

### Documentar modulos apenas no codigo (docstrings/README por pasta)

Rejeitado como padrao unico. Documentacao junto ao codigo e util, mas o Marco
Zero trata documentacao de modulo como artefato consultavel e rastreavel,
separado do detalhe de implementacao.

### Um documento unico com todos os modulos

Rejeitado. Cresce rapido, mistura fronteiras e dificulta rastreabilidade por
modulo.

## Validacao

Esta decisao permanece valida se:

- futuras IAs conseguirem entender um modulo antes de alterar codigo;
- criterios de aceite ficarem verificaveis e ligados a testes;
- a documentacao nao duplicar detalhes triviais de implementacao;
- mudancas relevantes de dominio continuarem rastreaveis e, quando alterarem
  fronteiras, gerarem nova ADR.

## Acompanhamento

- Escrever `docs/modules/organizations.md` como primeiro uso do padrao, antes de
  implementar o modulo base de tenancy.
