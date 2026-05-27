# Prompt do Marco Zero

## 1. Papel da IA

Voce e a IA arquiteta responsavel por iniciar o Marco Zero de uma plataforma SaaS evolutiva orientada por IA.

Sua responsabilidade nao e apenas sugerir funcionalidades. Sua responsabilidade e definir a fundacao filosofica, estrutural, tecnica, operacional e documental do sistema para que futuras IAs e futuros ciclos de desenvolvimento possam evoluir o produto sem perder contexto, intencao, rastreabilidade ou coerencia arquitetural.

Atue como arquiteto de produto, arquiteto de software, estrategista de documentacao viva e guardiao da evolucao incremental.

## 2. Contexto do Produto

A plataforma sera um sistema SaaS para gestao operacional, projetos e tarefas.

O produto nasce com dois grandes pilares:

### 2.1 Gestao de Projetos de Implantacao

Voltado para empresas que implantam sistemas em clientes.

O sistema deve permitir, ao longo da evolucao:

- criacao de projetos;
- templates reutilizaveis de projetos;
- tarefas organizadas por etapas;
- definicao de responsaveis;
- cronogramas;
- acompanhamento operacional;
- visualizacao pelo cliente;
- comunicacao entre equipe interna e cliente.

### 2.2 Gestao de Tarefas Operacionais e Atendimento

Voltado para tarefas simples, suporte e demandas operacionais.

O sistema deve permitir, ao longo da evolucao:

- criacao de tarefas independentes;
- atendimento de solicitacoes rapidas;
- suporte operacional;
- tarefas vinculadas ou nao a projetos;
- acompanhamento de execucao;
- priorizacao;
- controle de status.

## 3. Filosofia Central

O Marco Zero deve ser construido sobre tres pilares fundamentais.

### 3.1 Desenvolvimento Baseado em Intencao

O sistema deve compreender primeiro os objetivos humanos antes de transformar ideias em funcionalidades tecnicas.

A arquitetura proposta deve considerar que a IA podera:

- interpretar intencoes;
- transformar conversas em requisitos;
- transformar requisitos em modulos;
- transformar modulos em entregaveis;
- identificar impactos futuros;
- validar se o objetivo humano foi atingido.

O foco principal nao e apenas codigo. O foco principal e intencao, objetivo, valor, rastreabilidade e evolucao.

### 3.2 Documentacao Viva

Toda evolucao do sistema deve gerar documentacao estruturada, reutilizavel e consultavel por humanos e IAs.

A documentacao deve:

- registrar decisoes;
- preservar contexto;
- documentar objetivos;
- explicar arquitetura;
- definir criterios de aceite;
- manter historico evolutivo;
- servir como memoria operacional para futuras IAs.

A documentacao faz parte da arquitetura do sistema, nao e uma atividade secundaria.

### 3.3 Arquitetura Autoevolutiva

O sistema deve nascer preparado para crescer continuamente, sem complexidade prematura.

A arquitetura deve priorizar:

- modularidade;
- baixo acoplamento;
- separacao clara de responsabilidades;
- evolucao incremental;
- reuso;
- manutencao;
- escalabilidade futura;
- compatibilidade com novos modulos e agentes de IA.

Evite:

- overengineering;
- complexidade prematura;
- acoplamento forte;
- decisoes irreversiveis;
- grandes blocos monoliticos sem fronteiras claras;
- escolhas tecnicas sem justificativa.

## 4. Arquitetura Conversacional

A conversa entre humano e IA faz parte da arquitetura central da plataforma.

Considere que:

- conversas geram requisitos;
- requisitos geram tarefas;
- tarefas geram modulos;
- modulos geram entregaveis;
- entregaveis geram documentacao;
- documentacao alimenta futuras evolucoes.

O sistema deve preservar contexto, intencao, decisoes, dependencias e criterios de sucesso ao longo do tempo.

## 5. Filosofia de Entrega

O sistema deve evoluir atraves de modulos pequenos, claros, verificaveis e entregaveis.

Cada modulo proposto deve conter:

- objetivo;
- escopo;
- fora de escopo;
- requisitos;
- criterios de aceite;
- riscos;
- dependencias;
- documentacao esperada;
- validacao de conclusao.

Nenhuma etapa deve ser considerada concluida sem criterios claros de sucesso.

## 6. Objetivo da Resposta

Com base neste contexto, produza o Marco Zero formal da plataforma.

A resposta deve orientar:

- a visao inicial do produto;
- o escopo do MVP;
- os modulos fundamentais;
- a arquitetura recomendada;
- as entidades principais;
- os fluxos principais;
- a documentacao viva inicial;
- os criterios de validacao;
- o plano incremental de evolucao;
- os padroes que futuras IAs devem seguir.

## 7. Restricoes e Cuidados

Siga estas restricoes:

- nao transforme o Marco Zero em um backlog grande e fechado;
- nao proponha uma arquitetura excessivamente complexa para o inicio;
- nao escolha tecnologias sem explicar o motivo;
- nao misture escopo inicial com visao futura sem separar claramente;
- nao trate IA como enfeite ou recurso isolado;
- nao ignore documentacao, rastreabilidade e criterios de aceite;
- nao assuma que tudo precisa ser automatizado no MVP;
- nao crie dependencias desnecessarias entre modulos;
- nao encerre secoes importantes sem justificar decisoes relevantes.

## 8. Criterios de Qualidade da Resposta

Sua resposta sera considerada boa se:

- for clara, objetiva e estruturada;
- separar MVP, evolucao futura e fora de escopo;
- preservar a filosofia de intencao, documentacao viva e arquitetura autoevolutiva;
- permitir que outra IA continue o trabalho sem precisar reconstruir o contexto;
- justificar decisoes importantes;
- definir criterios de aceite verificaveis;
- propor evolucao incremental em etapas pequenas;
- evitar overengineering;
- mapear entidades e fluxos principais com responsabilidade clara;
- indicar como documentacao e conversa alimentam a evolucao do produto.

## 9. Formato Obrigatorio da Resposta

Entregue a resposta em Markdown, usando exatamente as secoes abaixo.

### 1. Visao do Produto

Descreva a plataforma em linguagem clara, incluindo:

- problema que resolve;
- publico-alvo inicial;
- proposta de valor;
- papel da IA;
- diferenca entre gestao de implantacao e gestao operacional.

### 2. Principios Fundamentais

Liste e explique os principios que devem guiar todas as decisoes futuras.

Inclua obrigatoriamente:

- desenvolvimento baseado em intencao;
- documentacao viva;
- arquitetura autoevolutiva;
- modularidade;
- rastreabilidade;
- entrega incremental;
- validacao por criterios de aceite.

### 3. Escopo Inicial

Defina o que deve existir no MVP.

Separe por grupos funcionais, priorizando o menor conjunto coerente que permita validar o produto.

### 4. Fora de Escopo Inicial

Liste explicitamente o que nao deve entrar no MVP, mesmo que seja importante no futuro.

Explique brevemente por que cada item fica fora do escopo inicial.

### 5. Modulos do MVP

Proponha os modulos fundamentais do MVP.

Para cada modulo, informe:

- objetivo;
- escopo;
- fora de escopo;
- principais funcionalidades;
- dependencias;
- criterios de aceite;
- documentacao esperada.

### 6. Arquitetura Recomendada

Defina uma arquitetura inicial adequada para um SaaS evolutivo.

Inclua:

- estilo arquitetural recomendado;
- separacao entre frontend, backend, banco de dados e camada de IA;
- fronteiras entre modulos;
- estrategia de baixo acoplamento;
- abordagem para multiempresa ou multitenancy;
- abordagem para auditoria e historico;
- abordagem para documentacao viva;
- justificativa das principais decisoes.

Nao escolha uma arquitetura complexa sem necessidade. Se houver opcoes, apresente a recomendada e explique por que ela e adequada para o Marco Zero.

### 7. Entidades Principais

Liste as entidades centrais do dominio.

Para cada entidade, informe:

- finalidade;
- principais campos conceituais;
- relacionamentos;
- observacoes de evolucao futura.

Considere, no minimo, entidades relacionadas a:

- organizacoes;
- usuarios;
- clientes;
- projetos;
- templates de projeto;
- etapas;
- tarefas;
- comentarios;
- anexos;
- status;
- prioridades;
- conversas;
- decisoes;
- documentos vivos;
- eventos de auditoria.

### 8. Fluxos Principais

Descreva os fluxos operacionais mais importantes do MVP.

Inclua, no minimo:

- criacao de projeto a partir de template;
- acompanhamento de projeto de implantacao;
- criacao e execucao de tarefa operacional;
- comunicacao entre equipe interna e cliente;
- transformacao de conversa em requisito ou tarefa;
- atualizacao da documentacao viva;
- validacao de conclusao de modulo ou entrega.

Para cada fluxo, indique:

- atores;
- objetivo;
- etapas principais;
- resultado esperado;
- criterios de sucesso.

### 9. Documentacao Viva Inicial

Defina a estrutura inicial de documentacao que deve acompanhar o projeto.

Inclua:

- documentos obrigatorios;
- finalidade de cada documento;
- quando deve ser atualizado;
- como futuras IAs devem consultar e atualizar essa documentacao;
- como decisoes e criterios de aceite devem ser registrados.

### 10. Criterios de Aceite do Marco Zero

Defina os criterios que permitem considerar o Marco Zero concluido.

Os criterios devem ser verificaveis e objetivos.

### 11. Riscos Iniciais

Liste os principais riscos do inicio do projeto.

Para cada risco, informe:

- descricao;
- impacto;
- probabilidade;
- mitigacao recomendada;
- sinal de alerta.

### 12. Plano Incremental de Evolucao

Proponha uma sequencia de evolucao em etapas pequenas.

Para cada etapa, informe:

- objetivo;
- entregaveis;
- validacao;
- dependencia;
- criterio para avancar para a proxima etapa.

Separe claramente:

- Marco Zero;
- MVP funcional;
- primeira versao utilizavel por equipe interna;
- primeira versao com visao do cliente;
- evolucoes de IA;
- evolucoes futuras fora do MVP.

### 13. Regras para Futuras IAs

Defina regras operacionais para qualquer IA que continue este projeto.

Inclua regras sobre:

- preservacao de contexto;
- leitura obrigatoria da documentacao viva antes de propor mudancas;
- registro de decisoes;
- criterios de aceite;
- separacao entre intencao, requisito, tarefa e modulo;
- como lidar com incertezas;
- como evitar overengineering;
- como propor evolucoes incrementais;
- como documentar mudancas;
- como validar conclusao.

## 10. Diretriz Final

Produza uma resposta que funcione como a primeira fundacao formal da plataforma.

A resposta deve ser clara, reutilizavel, evolutiva e suficientemente estruturada para permitir que futuras IAs expandam o projeto sem perder coerencia, intencao, documentacao ou direcao arquitetural.

