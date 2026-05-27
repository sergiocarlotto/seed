# ADR-0004: Padrao de Idioma e Documentacao

## Status

Aceita

## Contexto

O projeto deve manter um padrao tecnico internacional sem dificultar a
manutencao da intencao do produto, das decisoes e da documentacao viva pelo
dono atual do projeto.

Codigo, APIs, schemas de banco, testes e ferramentas interagem com ecossistemas
internacionais onde o ingles e o padrao pratico. Decisoes de produto,
requisitos, riscos, criterios de aceite e contexto de trabalho com IA precisam
permanecer claros e duraveis para as pessoas que conduzem o produto.

O projeto tambem usa desenvolvimento assistido por IA. Futuras IAs precisam de
regras explicitas para evitar mistura aleatoria de idiomas entre codigo,
documentacao, comentarios, prompts e textos visiveis ao usuario.

## Decisao

Usar ingles para artefatos tecnicos de implementacao e portugues para
documentacao interna de produto.

Ingles e obrigatorio para:

- identificadores no codigo, incluindo variaveis, funcoes, classes,
  componentes, hooks, services, entidades, comandos e testes;
- nomes de pastas e arquivos relacionados a codigo executavel;
- rotas de API, contratos de request e response, schemas OpenAPI, nomes de
  tabelas, nomes de colunas, migrations, eventos, filas e contratos de
  integracao;
- nomes de pacotes, scripts, variaveis de ambiente, branches e mensagens
  tecnicas de commit;
- documentacao tecnica publica destinada a desenvolvedores externos ou reuso
  mais amplo no ecossistema.

Portugues e obrigatorio para:

- documentos internos de fundacao do produto;
- ADRs e registros de decisao;
- requisitos, documentacao de modulos, criterios de aceite, riscos e analise de
  tradeoffs;
- prompts e skills do projeto usados principalmente pelo dono atual e por IAs
  internas;
- notas internas de produto e logs de evolucao.

Textos visiveis ao usuario final devem comecar em portugues porque o contexto
alvo inicial e de operacao em portugues. Internacionalizacao pode ser adicionada
depois, quando houver necessidade concreta de produto.

## Comentarios no Codigo

Comentarios no codigo devem ser raros e devem explicar intencao, decisoes nao
obvias, contexto de negocio ou restricoes operacionais. Eles nao devem repetir
o que o proprio codigo ja comunica.

Portugues e o idioma padrao para comentarios que explicam:

- regras de negocio;
- intencao de produto;
- decisoes de dominio;
- contexto operacional;
- restricoes temporarias ligadas a evolucao do Seed.

Ingles e o idioma padrao para comentarios que explicam:

- exemplos de API publica;
- restricoes de codigo gerado;
- comportamento de integracoes de terceiros;
- detalhes tecnicos especificos de bibliotecas;
- codigo destinado a reuso externo.

Comentarios bilingues devem ser usados apenas quando o mesmo comentario precisa
servir ao mesmo tempo uma audiencia interna de produto e uma audiencia tecnica
externa. Eles sao excecao porque explicacoes duplicadas podem divergir com o
tempo.

## Racional

Esse padrao mantem a implementacao compativel com convencoes internacionais de
desenvolvimento, sem sacrificar a clareza de produto no idioma em que o dono
atual do projeto raciocina com mais precisao.

Ele tambem da uma regra estavel para futuras IAs:

- codigo e contratos permanecem internacionalmente legiveis;
- pensamento de produto permanece claro em portugues;
- comentarios seguem a audiencia da explicacao;
- texto bilingue fica reservado para casos em que duas audiencias realmente
  precisam da mesma explicacao.

## Consequencias

- Novo codigo deve usar identificadores em ingles mesmo quando o conceito de
  negocio nasceu em portugues.
- Documentacao interna pode permanecer em portugues sem reduzir a portabilidade
  tecnica.
- Futuras IAs nao devem traduzir documentacao de produto existente para ingles,
  salvo se uma nova ADR aceita alterar essa regra.
- Futuras IAs nao devem adicionar comentarios bilingues por padrao.
- Glossarios podem ser introduzidos depois se termos de dominio precisarem de
  traducao estavel entre portugues e ingles.

## Alternativas Consideradas

### Tudo Em Ingles

Rejeitado por enquanto. Isso maximizaria consistencia internacional, mas
dificultaria preservar decisoes de produto e intencoes sutis para o dono atual.

### Tudo Em Portugues

Rejeitado. Isso reduziria compatibilidade com ferramentas, bibliotecas,
desenvolvedores externos, exemplos, contratos gerados e padroes comuns de
desenvolvimento assistido por IA.

### Documentacao Bilingue Em Todo Lugar

Rejeitado. Isso cria duplicacao e aumenta o risco de divergencia entre as duas
versoes. Conteudo bilingue deve existir apenas quando houver necessidade
concreta de audiencia.

## Validacao

Esta decisao permanece valida se:

- o codigo puder ser entendido por desenvolvedores acostumados a convencoes
  internacionais;
- a documentacao de produto continuar facil de manter pelo dono do projeto;
- futuras IAs conseguirem inferir idioma de nomes e documentos sem perguntar;
- comentarios explicarem intencao no idioma mais util para sua audiencia;
- texto bilingue continuar excepcional e deliberado.

## Proximos Passos

- Definir um glossario se termos recorrentes de dominio precisarem de traducao
  estavel entre portugues de produto e ingles de implementacao.
- Revisitar o idioma da interface apenas quando o produto tiver uma necessidade
  concreta de internacionalizacao.
