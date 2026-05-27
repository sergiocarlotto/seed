# ADR-0005: Banco de Dados e Propriedade dos Dados

## Status

Aceita

## Contexto

O Seed precisa de uma base de dados que sustente um SaaS operacional com
projetos de implantacao, tarefas, clientes, comentarios, decisoes,
auditoria e isolamento por organizacao.

O Marco Zero define forte preferencia por banco relacional para
rastreabilidade e consistencia. A ADR-0003 define C# e ASP.NET Core como base
backend, com regras de negocio, autorizacao, multitenancy e auditoria sob
responsabilidade do backend.

A decisao de dados precisa preservar:

- portabilidade para Linux e Docker;
- independencia de recursos exclusivos de provedor;
- consistencia transacional para entidades operacionais;
- rastreabilidade entre organizacao, cliente, projeto, tarefa, comentario,
  decisao e auditoria;
- simplicidade suficiente para o MVP;
- capacidade de evoluir sem trocar a fundacao de persistencia cedo demais.

## Decisao Proposta

Usar PostgreSQL como banco relacional principal do produto.

Usar Entity Framework Core como ORM e mecanismo padrao de migrations no
backend ASP.NET Core.

Adotar um unico banco compartilhado no MVP, com isolamento logico por
organizacao usando `organization_id` nas entidades operacionais aplicaveis.

Esta decisao define o modelo de dados dentro de uma instancia do Seed. Ela nao
impede multiplas instancias independentes do produto, cada uma com seu proprio
banco PostgreSQL. No MVP, dentro de cada instancia, multiplas organizacoes
compartilham o banco com isolamento logico por `organization_id`.

Usar colunas `uuid` como identificadores primarios das entidades de dominio.
IDs devem ser opacos para o usuario final e seguros para exposicao em URLs,
APIs e eventos. A convencao exata de geracao deve ser definida na
implementacao inicial, desde que continue portavel entre ambiente local,
Docker e producao.

Registrar timestamps em UTC. Entidades persistidas relevantes devem usar, no
minimo, `created_at` e `updated_at`. Quando a entidade permitir remocao logica,
usar tambem `deleted_at`. Campos de autoria, como `created_by_user_id`,
`updated_by_user_id` e `deleted_by_user_id`, devem ser usados quando forem
necessarios para auditoria operacional e houver usuario autenticado.

O backend deve ser o unico responsavel por aplicar regras de propriedade dos
dados, filtro de tenant, autorizacao e auditoria. O frontend nunca deve montar
ou confiar em filtros de tenant como mecanismo de seguranca.

Entidades operacionais devem ter propriedade explicita:

- entidades de escopo da organizacao devem carregar `organization_id`;
- entidades globais so devem existir quando forem realmente independentes de
  organizacao;
- relacionamentos entre entidades de organizacoes diferentes devem ser
  bloqueados pela camada de application/domain;
- leituras e escritas devem passar por politicas ou servicos que conhecem o
  tenant atual;
- eventos de auditoria devem registrar ator, organizacao, entidade, acao,
  data, origem e dados de mudanca quando aplicavel.

A documentacao viva do projeto Seed continua tendo o repositorio como fonte de
verdade e nao faz parte do schema inicial do banco. Isso inclui Marco Zero,
ADRs, documentacao de modulo, criterios de aceite, prompts, skills e historico
de evolucao.

O banco pode futuramente representar conversas, decisoes ou documentos
operacionais gerenciados pelo produto, caso esses modulos sejam implementados.
Essa modelagem pertence aos respectivos modulos de produto e nao deve substituir
a documentacao viva versionada do projeto.

## Racional Inicial

PostgreSQL e uma escolha madura, open source, amplamente adotada e adequada a
um SaaS transacional. Ele tem boa compatibilidade com Linux, Docker, backups,
migrations, indices, constraints relacionais e recursos futuros como JSONB,
busca textual e extensoes, sem exigir dependencia de um provedor especifico.

Entity Framework Core se alinha bem com ASP.NET Core, oferece migrations
padronizadas, bom suporte a testes de integracao e reduz atrito inicial para
desenvolvimento assistido por IA em C#.

Isolamento logico por `organization_id` e suficiente para o MVP e esta alinhado
ao Marco Zero. Isolamento fisico por banco ou schema separado fica fora do MVP
por aumentar custo operacional, complexidade de migrations e complexidade de
testes antes de haver exigencia concreta.

UUIDs reduzem acoplamento a sequencias globais, funcionam bem como IDs opacos
em APIs e mantem liberdade para futuras importacoes, sincronizacoes ou
separacoes de dados sem expor contadores internos. O custo de armazenamento e
indice e aceitavel para o MVP.

## Regras de Propriedade dos Dados

- `Organization` e a raiz de tenancy do produto.
- `User` deve ter relacao clara com organizacoes acessiveis e papeis definidos
  pela ADR de autenticacao e autorizacao.
- `Client`, `Project`, `ProjectTemplate`, `Stage`, `Task`, `Comment`,
  `Attachment` e `AuditEvent` devem pertencer a uma organizacao quando usados
  em fluxo operacional.
- `Conversation`, `Decision` e documentos operacionais do produto, se
  implementados no futuro, tambem devem ter propriedade clara por organizacao
  ou por entidade operacional vinculada.
- Dados de auditoria devem ser append-only por padrao.
- Eventos de auditoria com significado de produto devem ser emitidos pelos
  casos de uso da camada de application, como parte das regras de negocio.
- Eventos de auditoria devem ser persistidos na mesma transacao da mudanca
  auditada sempre que possivel.
- Soft delete deve ser aplicado por padrao a entidades operacionais cujo
  apagamento fisico prejudique rastreabilidade, historico, auditoria ou
  relacoes de negocio.
- Entidades de acesso e tenancy, como `Organization` e `User`, devem preferir
  estados de ativacao, desativacao ou suspensao em vez de exclusao logica como
  fluxo principal.
- Exclusao fisica deve ser reservada para dados tecnicos, dados temporarios ou
  casos em que exista politica explicita de retencao.
- Mudancas de negocio relevantes devem gerar `AuditEvent` explicito a partir
  da camada de application.
- `AuditEvent` deve comecar pequeno, com `id`, `organization_id`,
  `actor_user_id`, `action`, `entity_type`, `entity_id`, `occurred_at` e
  `metadata`.
- Campos tecnicos de rastreamento, origem da requisicao, estado antes/depois e
  dados de seguranca podem ser adicionados depois, quando houver necessidade
  real de observabilidade, seguranca ou compliance.
- Interceptors do EF Core podem ser usados para metadados tecnicos como
  timestamps, soft delete e validacoes defensivas de tenant, mas nao devem ser
  a unica fonte de eventos de auditoria com significado de negocio.

## Alternativas Consideradas

### SQL Server

Valido tecnicamente com ASP.NET Core, mas menos alinhado ao principio de
portabilidade simples e open source do projeto. Tambem pode aumentar custo e
dependencia operacional em ambientes Linux/VPS.

### SQLite

Util para prototipos locais e testes, mas insuficiente como banco principal de
um SaaS multiusuario com auditoria, concorrencia e operacao em producao.

### MySQL Ou MariaDB

Alternativas open source validas. PostgreSQL e preferido pela combinacao de
robustez relacional, recursos avancados, comunidade, documentacao e uso comum
em SaaS modernos.

### Banco Por Organizacao

Oferece isolamento forte, mas aumenta complexidade de provisioning,
migrations, backups, suporte e operacao. Deve ser reconsiderado apenas quando
existir exigencia real de isolamento fisico, compliance ou escala.

### Schema Por Organizacao

E um meio termo possivel, mas ainda aumenta complexidade de migrations e
operacao sem necessidade para o MVP.

### NoSQL Como Banco Principal

Rejeitado como base principal. O produto depende de relacoes, consistencia,
rastreabilidade e auditoria, que sao mais naturais em banco relacional neste
estagio.

## Consequencias Se Aceita

- O ambiente local e Docker devem incluir PostgreSQL.
- `Seed.Infrastructure` deve conter a implementacao de persistencia com EF
  Core.
- Migrations passam a fazer parte do fluxo normal de evolucao do backend.
- Testes de integracao devem validar persistencia, constraints, isolamento de
  tenant e auditoria usando banco real ou ambiente equivalente.
- Modelos de dominio nao devem depender diretamente de EF Core.
- Tipos, tabelas, colunas e migrations devem seguir a ADR-0004 e usar ingles.
- O primeiro modelo fisico deve incluir convencoes de `uuid`, timestamps UTC e
  `organization_id` nas entidades de escopo organizacional.
- O primeiro backend deve ter uma abstracao explicita para o tenant atual antes
  de expor endpoints operacionais.

## Validacao

Esta decisao permanece valida se:

- o banco escolhido rodar localmente e em Linux com Docker;
- o modelo suportar isolamento logico por organizacao;
- o backend conseguir impedir acesso cruzado entre tenants;
- as entidades principais do Marco Zero puderem ser representadas sem
  contorcoes;
- migrations forem previsiveis para humanos e IAs;
- auditoria basica puder ser validada por testes de integracao;
- a decisao nao introduzir dependencia obrigatoria de provedor de hospedagem.

## Proximos Passos

- Criar a estrutura inicial de `apps/api` com dependencias de persistencia.
- Definir a ADR de autenticacao e autorizacao antes de implementar usuarios e
  acesso real.
