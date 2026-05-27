# Marco Zero

## 1. Visao do Produto

A plataforma e um SaaS para gestao operacional, projetos e tarefas. O foco
inicial e atender empresas que implantam sistemas em clientes e precisam
organizar projetos de implantacao, tarefas por etapa, responsaveis,
cronogramas, comunicacao e acompanhamento operacional.

O produto tambem cobre tarefas operacionais independentes, suporte e demandas
rapidas que podem ou nao estar vinculadas a projetos.

O problema central e reduzir perda de contexto, desalinhamento operacional,
falta de rastreabilidade e dificuldade de transformar conversas em execucao
organizada.

O publico-alvo inicial sao equipes internas de empresas que executam
implantacoes, atendimento operacional ou suporte recorrente para clientes.

A proposta de valor e oferecer uma base unica para transformar intencoes,
conversas, tarefas, projetos, decisoes e documentacao em um fluxo operacional
consultavel, rastreavel e evolutivo.

A IA nao deve ser tratada como enfeite. Ela deve ajudar a interpretar intencoes,
estruturar requisitos, sugerir tarefas, identificar impactos, preservar
contexto e apoiar a evolucao da documentacao viva. No MVP, a IA pode comecar
como assistente de estruturacao e analise, sem automatizar decisoes criticas.

A diferenca entre gestao de implantacao e gestao operacional deve permanecer
clara:

- Implantacao: projetos com etapas, templates, cronograma, cliente e entregas.
- Operacional: tarefas avulsas, suporte, demandas rapidas, priorizacao e
  acompanhamento de execucao.

## 2. Principios Fundamentais

- Desenvolvimento baseado em intencao: entender o objetivo humano antes de
  transformar uma ideia em requisito, tarefa, modulo ou codigo.
- Documentacao viva: decisoes, objetivos, criterios de aceite, arquitetura e
  historico evolutivo fazem parte do sistema.
- Arquitetura autoevolutiva: permitir crescimento continuo sem complexidade
  prematura.
- Modularidade: separar responsabilidades por dominio e evitar acoplamento
  desnecessario.
- Rastreabilidade: manter ligacao entre conversa, intencao, requisito, tarefa,
  decisao, entrega e documentacao.
- Entrega incremental: evoluir em partes pequenas, verificaveis e reversiveis.
- Validacao por criterios de aceite: nenhuma entrega deve ser considerada
  concluida sem criterio objetivo de sucesso.
- Portabilidade: o sistema deve poder rodar em Linux, preferencialmente com
  Docker, sem dependencia obrigatoria de provedor especifico.
- Open source e continuidade: priorizar tecnologias abertas, populares,
  estaveis, bem documentadas e com comunidade ativa.
- AI-efficient by design: escolher estruturas e ferramentas que facilitem a
  colaboracao precisa entre humanos e IAs.

## 3. Escopo Inicial

O MVP deve validar o menor conjunto coerente para operar projetos de
implantacao e tarefas operacionais com rastreabilidade basica.

Grupos funcionais iniciais:

- Organizacoes e usuarios: permitir contexto de empresa, usuarios internos e
  papeis basicos.
- Clientes: cadastrar clientes atendidos pela organizacao.
- Projetos de implantacao: criar projetos, associar cliente, status,
  responsaveis e etapas.
- Templates simples de projeto: permitir criar projetos a partir de estrutura
  reaproveitavel.
- Tarefas: criar tarefas vinculadas a projetos ou independentes.
- Comentarios e comunicacao: registrar comunicacoes operacionais em projetos e
  tarefas.
- Status e prioridade: acompanhar execucao com estados claros.
- Documentacao viva inicial: registrar decisoes, requisitos e criterios de
  aceite relacionados a evolucao do produto.
- Auditoria basica: registrar eventos relevantes de criacao, alteracao e
  conclusao.

## 4. Fora de Escopo Inicial

- Automacoes complexas de IA: ficam fora ate existirem fluxos humanos claros e
  dados reais suficientes para validar valor.
- Marketplace, billing e cobranca recorrente: importantes para SaaS, mas nao
  necessarios para validar o fluxo operacional inicial.
- Mobile nativo: o produto nasce 100% web.
- Integracoes externas amplas: devem ser adicionadas apenas quando houver caso
  de uso concreto.
- BI avancado: relatorios simples podem existir, mas analises complexas ficam
  para depois.
- Workflow engine generica: o MVP deve evitar criar uma plataforma generica de
  automacao antes de validar o dominio.
- Multi-idioma: deve ser considerado no design futuro, mas nao e requisito do
  primeiro MVP.
- Permissoes altamente granulares: usar modelo simples no inicio e evoluir com
  necessidade real.

## 5. Modulos do MVP

### Organizacoes e Usuarios

Objetivo: estruturar o contexto basico de uso da plataforma.

Escopo: organizacao, usuarios internos, papeis iniciais e vinculacao com
clientes, projetos e tarefas.

Fora de escopo: permissao granular por campo, SSO corporativo e billing.

Funcionalidades: cadastro de organizacao, usuarios, papeis basicos e acesso a
dados da organizacao.

Dependencias: autenticacao e modelo de dados base.

Criterios de aceite: usuarios conseguem acessar apenas dados da organizacao
correta e executar acoes compatíveis com seu papel.

Documentacao esperada: regra de tenancy, papeis iniciais e limites de acesso.

### Clientes

Objetivo: representar os clientes atendidos.

Escopo: cadastro, dados principais e associacao com projetos.

Fora de escopo: CRM completo, funil comercial e contratos.

Funcionalidades: criar, editar, listar e consultar clientes.

Dependencias: organizacoes e usuarios.

Criterios de aceite: projetos podem ser associados a clientes e filtrados por
cliente.

Documentacao esperada: definicao da entidade cliente e relacionamento com
projetos.

### Projetos de Implantacao

Objetivo: organizar implantacoes em etapas, responsaveis e acompanhamento.

Escopo: projeto, cliente, status, responsaveis, etapas e tarefas vinculadas.

Fora de escopo: gestao financeira do projeto, alocacao avancada e Gantt
complexo.

Funcionalidades: criar projeto, definir responsaveis, acompanhar etapas,
visualizar status e registrar comentarios.

Dependencias: clientes, usuarios, tarefas e status.

Criterios de aceite: uma equipe consegue criar e acompanhar uma implantacao
simples do inicio ao fim.

Documentacao esperada: fluxo de projeto e criterios de conclusao.

### Templates de Projeto

Objetivo: reaproveitar estruturas comuns de implantacao.

Escopo: template com etapas e tarefas padrao.

Fora de escopo: versionamento avancado de templates e regras condicionais
complexas.

Funcionalidades: criar template simples e gerar projeto a partir dele.

Dependencias: projetos, etapas e tarefas.

Criterios de aceite: um projeto novo pode ser criado a partir de um template
sem recriar manualmente todas as etapas.

Documentacao esperada: estrutura conceitual de template e regra de aplicacao.

### Tarefas Operacionais

Objetivo: controlar demandas simples, suporte e execucao diaria.

Escopo: tarefas independentes ou vinculadas a projeto, responsavel, prioridade,
status, comentarios e anexos.

Fora de escopo: automacao complexa de SLA, filas inteligentes e roteamento por
IA.

Funcionalidades: criar, atribuir, priorizar, comentar, anexar e concluir
tarefas.

Dependencias: usuarios, clientes opcionais, projetos opcionais e status.

Criterios de aceite: demandas operacionais podem ser registradas, executadas e
concluidas com historico minimo.

Documentacao esperada: estados de tarefa, prioridade e criterio de conclusao.

### Conversas, Decisoes e Documentacao Viva

Objetivo: preservar contexto e transformar conversa em estrutura reutilizavel.

Escopo: registro de conversas relevantes, decisoes, requisitos, criterios de
aceite e documentos vivos.

Fora de escopo: agente autonomo que altera produto sem aprovacao humana.

Funcionalidades: registrar decisoes, ligar decisoes a entidades, documentar
criterios de aceite e manter historico.

Dependencias: projetos, tarefas, usuarios e auditoria.

Criterios de aceite: uma decisao importante pode ser encontrada e rastreada ate
o contexto que a originou.

Documentacao esperada: indice de documentos vivos e regras para futuras IAs.

## 6. Arquitetura Recomendada

O estilo inicial recomendado e um monolito modular web, com fronteiras claras
por dominio. Isso evita complexidade prematura e mantem o sistema simples para
desenvolver, testar, operar e evoluir.

Separacao inicial:

- Frontend: interface web em TypeScript, React e Next.js.
- Backend: definido em ADR separado, preservando contratos claros e dominio
  modular.
- Banco de dados: definido em ADR separado, com forte preferencia por banco
  relacional para rastreabilidade e consistencia.
- Camada de IA: inicialmente assistiva, separada por contratos e com aprovacao
  humana para mudancas relevantes.
- Documentacao viva: armazenada no repositorio para arquitetura do produto e,
  futuramente, tambem representada como entidades consultaveis no sistema.

Fronteiras iniciais de modulo:

- Organizacoes e acesso.
- Clientes.
- Projetos.
- Templates.
- Tarefas.
- Comunicacao e comentarios.
- Documentacao viva e decisoes.
- Auditoria.

Multitenancy deve iniciar por isolamento logico por organizacao. Toda entidade
operacional deve ter vinculo claro com a organizacao quando aplicavel. Isolamento
fisico por banco ou schema separado fica fora do MVP, salvo exigencia futura.

Auditoria deve registrar eventos importantes: criacao, alteracao, mudanca de
status, conclusao, comentario relevante, decisao e atualizacao de documento.

O baixo acoplamento deve ser obtido por limites de dominio, contratos explicitos
e evolucao incremental. Microservicos nao sao recomendados no inicio.

## 7. Entidades Principais

- Organizacao: empresa usuaria da plataforma. Campos conceituais: nome, status,
  plano futuro, configuracoes. Relaciona usuarios, clientes, projetos e tarefas.
- Usuario: pessoa que acessa o sistema. Campos: nome, email, papel, status.
  Relaciona tarefas, comentarios, decisoes e eventos.
- Cliente: cliente atendido pela organizacao. Campos: nome, documento opcional,
  contatos, status. Relaciona projetos e tarefas.
- Projeto: implantacao ou iniciativa estruturada. Campos: nome, cliente,
  responsaveis, status, datas, progresso. Relaciona etapas, tarefas,
  comentarios, documentos e auditoria.
- Template de Projeto: modelo reutilizavel. Campos: nome, descricao, etapas
  padrao, tarefas padrao, status. Gera projetos.
- Etapa: fase dentro de projeto ou template. Campos: nome, ordem, status,
  criterio de conclusao. Relaciona tarefas.
- Tarefa: unidade operacional de trabalho. Campos: titulo, descricao,
  responsavel, prioridade, status, prazo, vinculo opcional com projeto/cliente.
- Comentario: comunicacao contextual. Campos: autor, texto, data, visibilidade,
  entidade vinculada.
- Anexo: arquivo ou referencia externa. Campos: nome, tipo, origem, entidade
  vinculada, autor.
- Status: estado controlado de projeto, etapa ou tarefa. Campos: nome, ordem,
  categoria e regra de conclusao.
- Prioridade: classificacao de urgencia/importancia. Campos: nome, nivel,
  descricao.
- Conversa: registro de interacao humana ou humano-IA relevante. Campos:
  participantes, contexto, resumo, origem, entidades vinculadas.
- Decisao: escolha registrada. Campos: titulo, contexto, decisao, alternativas,
  consequencias, status, data e responsavel.
- Documento Vivo: artefato consultavel que preserva contexto. Campos: titulo,
  tipo, versao, conteudo, entidades vinculadas.
- Evento de Auditoria: fato relevante ocorrido no sistema. Campos: ator, acao,
  entidade, antes/depois quando aplicavel, data e origem.

## 8. Fluxos Principais

### Criacao de projeto a partir de template

Atores: usuario interno responsavel por implantacao.

Objetivo: criar rapidamente um projeto com estrutura padrao.

Etapas: selecionar cliente, escolher template, revisar etapas e tarefas, definir
responsaveis iniciais, criar projeto.

Resultado esperado: projeto criado com etapas e tarefas iniciais.

Criterios de sucesso: o projeto fica visivel, associado ao cliente e pronto para
execucao sem recriacao manual da estrutura.

### Acompanhamento de projeto de implantacao

Atores: equipe interna, responsavel do projeto e cliente em visao futura.

Objetivo: acompanhar progresso, pendencias e comunicacao.

Etapas: visualizar projeto, revisar etapas, atualizar tarefas, registrar
comentarios, mudar status e validar conclusao.

Resultado esperado: situacao da implantacao clara e rastreavel.

Criterios de sucesso: qualquer usuario autorizado entende o estado atual e o
proximo passo.

### Criacao e execucao de tarefa operacional

Atores: usuario interno, responsavel pela tarefa e solicitante.

Objetivo: registrar demanda simples e acompanhar execucao.

Etapas: criar tarefa, definir prioridade, atribuir responsavel, comentar,
executar e concluir.

Resultado esperado: demanda registrada com historico e status.

Criterios de sucesso: a tarefa tem responsavel, status, prioridade e criterio
claro de conclusao.

### Comunicacao entre equipe interna e cliente

Atores: equipe interna e cliente.

Objetivo: centralizar comunicacao contextual.

Etapas: registrar comentario, definir visibilidade, vincular a projeto ou
tarefa, notificar partes relevantes em evolucao futura.

Resultado esperado: comunicacao preservada no contexto correto.

Criterios de sucesso: comunicacoes importantes nao ficam perdidas fora do
sistema.

### Transformacao de conversa em requisito ou tarefa

Atores: humano solicitante, IA assistiva e responsavel humano.

Objetivo: transformar intencao em estrutura operacional.

Etapas: capturar conversa, resumir intencao, propor requisito ou tarefa,
submeter a revisao humana, registrar aprovacao e criar item operacional.

Resultado esperado: conversa vira requisito ou tarefa rastreavel.

Criterios de sucesso: nenhuma acao critica e criada sem confirmacao humana.

### Atualizacao da documentacao viva

Atores: humano responsavel e IA assistiva.

Objetivo: manter contexto consultavel e atualizado.

Etapas: identificar mudanca relevante, propor atualizacao, revisar, aprovar e
registrar documento ou decisao.

Resultado esperado: documentacao acompanha a evolucao real.

Criterios de sucesso: futuras IAs conseguem entender o motivo da mudanca.

### Validacao de conclusao de modulo ou entrega

Atores: responsavel pela entrega, revisor e IA assistiva.

Objetivo: confirmar que uma entrega atingiu os criterios definidos.

Etapas: consultar criterios de aceite, executar validacoes, registrar evidencias
e atualizar status.

Resultado esperado: conclusao rastreavel e verificavel.

Criterios de sucesso: entrega so e concluida quando criterios objetivos foram
atendidos ou excecoes foram registradas.

## 9. Documentacao Viva Inicial

Documentos obrigatorios:

- `AGENTS.md`: regras de trabalho para IAs no repositorio.
- `docs/foundation/marco-zero.md`: fundacao formal do produto.
- `docs/decisions/README.md`: indice de decisoes arquiteturais.
- `docs/decisions/ADR-*.md`: decisoes arquiteturais versionadas.
- `docs/modules/`: futura definicao de modulos, escopo e criterios.
- `docs/acceptance/`: futuros criterios de aceite por entrega relevante.
- `docs/evolution-log.md`: futuro historico resumido de evolucao.

Quando atualizar:

- Toda decisao arquitetural relevante deve virar ADR.
- Toda mudanca de direcao de produto deve atualizar a fundacao ou criar decisao
  especifica.
- Todo modulo relevante deve ter objetivo, escopo, fora de escopo, criterios de
  aceite e validacao.
- Toda IA deve consultar `AGENTS.md`, este documento e o indice de ADRs antes de
  propor mudancas materiais.

Decisoes e criterios de aceite devem ser registrados de forma objetiva,
consultavel e vinculada ao contexto que motivou a mudanca.

## 10. Criterios de Aceite do Marco Zero

O Marco Zero e considerado concluido quando:

- a visao do produto esta registrada;
- os principios fundamentais estao definidos;
- o escopo inicial e o fora de escopo inicial estao separados;
- os modulos do MVP estao descritos com objetivo e criterio de aceite;
- as entidades principais estao mapeadas;
- os fluxos principais estao descritos;
- a documentacao viva inicial esta definida;
- as regras para futuras IAs estao registradas;
- as decisoes tecnicas iniciais estao registradas em ADRs;
- existe um proximo passo pequeno e verificavel.

## 11. Riscos Iniciais

### Escopo amplo demais

Impacto: atrasar validacao e criar produto pesado antes de uso real.

Probabilidade: alta.

Mitigacao: separar MVP, futuro e fora de escopo em toda decisao.

Sinal de alerta: surgimento de muitos modulos antes dos fluxos basicos
funcionarem.

### IA sem responsabilidade clara

Impacto: automacoes prematuras, baixa confianca e decisoes sem revisao humana.

Probabilidade: media.

Mitigacao: IA inicialmente assistiva, com aprovacao humana para mudancas
criticas.

Sinal de alerta: IA criando tarefas, decisoes ou documentos sem validacao.

### Documentacao virar arquivo morto

Impacto: perda de contexto e repeticao de decisoes.

Probabilidade: media.

Mitigacao: tratar documentos como parte da arquitetura e atualizar em cada
mudanca relevante.

Sinal de alerta: codigo ou planos divergindo de ADRs e fundacao.

### Complexidade arquitetural prematura

Impacto: aumento de custo, lentidao de entrega e dificuldade de manutencao.

Probabilidade: media.

Mitigacao: monolito modular inicial, contratos claros e decisoes reversiveis.

Sinal de alerta: proposta de microservicos, filas ou automacoes genericas antes
de necessidade real.

### Falta de criterio de sucesso

Impacto: entregas subjetivas e discussoes recorrentes sobre conclusao.

Probabilidade: alta.

Mitigacao: exigir criterios de aceite verificaveis por modulo e entrega.

Sinal de alerta: tarefas descritas apenas como ideias ou telas, sem validacao.

## 12. Plano Incremental de Evolucao

### Marco Zero

Objetivo: registrar fundacao, principios, escopo e decisoes iniciais.

Entregaveis: este documento, indice de ADRs, ADR de frontend e proximos ADRs
tecnicos.

Validacao: futuras IAs conseguem continuar o projeto lendo a documentacao.

Dependencia: nenhuma.

Criterio para avancar: criterios de aceite do Marco Zero atendidos.

### MVP funcional

Objetivo: validar gestao basica de projetos de implantacao e tarefas
operacionais.

Entregaveis: autenticacao, organizacoes, clientes, projetos, templates simples,
tarefas, comentarios, status e auditoria basica.

Validacao: equipe interna consegue operar um projeto simples e tarefas avulsas.

Dependencia: decisoes de backend, banco, autenticacao e deploy.

Criterio para avancar: uso real ou simulado cobre os fluxos principais.

### Primeira versao utilizavel por equipe interna

Objetivo: tornar a plataforma util para operacao interna diaria.

Entregaveis: filtros, listas, detalhes, atualizacoes de status, comentarios,
historico e criterios de conclusao.

Validacao: equipe consegue acompanhar trabalho sem depender de planilhas
paralelas para o fluxo principal.

Dependencia: MVP funcional.

Criterio para avancar: principais dores operacionais aparecem e podem ser
priorizadas com base em uso.

### Primeira versao com visao do cliente

Objetivo: permitir acompanhamento controlado pelo cliente.

Entregaveis: acesso do cliente, visibilidade limitada, comentarios e status de
projeto.

Validacao: cliente entende andamento e pendencias sem acesso indevido.

Dependencia: permissao, visibilidade e auditoria mais maduras.

Criterio para avancar: fluxo interno esta estavel.

### Evolucoes de IA

Objetivo: transformar conversa em requisitos, tarefas e documentacao com apoio
assistivo.

Entregaveis: captura de conversa, resumo, sugestao de tarefa/requisito,
aprovacao humana e registro de decisao.

Validacao: IA reduz trabalho manual sem criar itens incorretos sem revisao.

Dependencia: entidades de conversa, decisao e documento vivo.

Criterio para avancar: humanos confiam nas sugestoes e ha rastreabilidade.

### Evolucoes futuras fora do MVP

Objetivo: expandir SaaS com maturidade.

Entregaveis potenciais: billing, integracoes, BI, automacoes avancadas,
permissoes granulares, workflow engine, SSO e mobile.

Validacao: cada evolucao deve ter justificativa de uso real e ADR quando
impactar arquitetura.

Dependencia: aprendizado do MVP.

Criterio para avancar: demanda recorrente e impacto claro.

## 13. Regras para Futuras IAs

- Leia `AGENTS.md`, este documento e `docs/decisions/README.md` antes de propor
  mudancas materiais.
- Preserve a separacao entre intencao, requisito, tarefa, modulo e codigo.
- Nao transforme visao futura em escopo do MVP sem decisao explicita.
- Registre decisoes arquiteturais em ADRs.
- Proponha evolucoes pequenas, verificaveis e reversiveis.
- Quando houver incerteza, registre a suposicao e o impacto antes de decidir.
- Evite overengineering e plataformas genericas antes de necessidade real.
- Toda entrega relevante deve ter criterio de aceite verificavel.
- Documente mudancas que alterem produto, arquitetura, dados, seguranca,
  permissao, deploy ou papel da IA.
- IA pode recomendar e estruturar; decisoes criticas de produto, arquitetura,
  acesso, custos e automacao permanecem humanas.

