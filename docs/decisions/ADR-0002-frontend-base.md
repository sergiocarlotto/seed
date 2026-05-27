# ADR-0002: Base Frontend

## Status

Aceita

## Contexto

O projeto deve ser um sistema 100% web que possa rodar em servidores Linux no
futuro, incluindo uma VPS. A base tecnologica deve ser open source, estavel,
popular no mercado, previsivel, bem documentada e adequada para muitos anos de
evolucao.

O projeto tambem deve ser eficiente para desenvolvimento assistido por IA.
Tecnologias com bons exemplos publicos, convencoes claras e estrutura
previsivel sao preferidas porque agentes de IA conseguem raciocinar sobre elas,
modifica-las e testa-las com mais confiabilidade.

## Decisao

Usar a seguinte base frontend:

```txt
Linguagem: TypeScript
Biblioteca de UI: React
Framework web: Next.js
Estilizacao: Tailwind CSS
Base de componentes: shadcn/ui
Validacao e contratos: Zod
Testes de navegador: Playwright
```

## Racional

Esta stack oferece um bom equilibrio entre:

- bases open source;
- comunidades grandes e ativas;
- continuidade de mercado no longo prazo;
- ampla disponibilidade de profissionais e documentacao;
- boa compatibilidade com Linux e Docker;
- alta produtividade para desenvolvedores humanos;
- alta produtividade para desenvolvimento assistido por IA.

React e TypeScript sao as escolhas fundacionais mais importantes. Next.js e
aceito como framework web inicial, mas o projeto deve evitar dependencia de
comportamentos exclusivos de provedor de hospedagem.

## Restricoes

- A aplicacao deve permanecer implantavel em uma VPS Linux.
- Compatibilidade com Docker deve ser preservada.
- O projeto nao deve exigir Vercel ou qualquer outro provedor especifico de
  hospedagem.
- Novos recursos do Next.js nao devem ser adotados por impulso. Decisoes
  materiais de framework devem ser registradas em ADRs quando afetarem
  arquitetura, deploy ou manutenibilidade de longo prazo.
- A UI deve favorecer componentes reutilizaveis em vez de estilos pontuais
  espalhados por paginas.

## Alternativas Consideradas

### CSS Modules Em Vez De Tailwind CSS

CSS Modules sao simples e estaveis, mas Tailwind e mais eficiente para trabalho
de interface assistido por IA porque a intencao de estilo fica visivel
diretamente na estrutura do componente. CSS Modules ainda podem ser usados
localmente se um componente precisar de CSS customizado que fique mais claro
fora de classes utilitarias.

### Vue E Nuxt

Vue e Nuxt sao alternativas open source validas, com boa estabilidade e boa
experiencia de desenvolvimento. Eles nao foram selecionados porque React tem
mercado maior, ecossistema maior e superficie de treinamento de IA mais forte.

### Angular

Angular e estavel e forte para sistemas enterprise grandes. Ele nao foi
selecionado porque e mais pesado e opinativo do que o necessario no inicio
deste projeto.

### Svelte E SvelteKit

Svelte e tecnicamente forte e rapido, mas tem mercado e ecossistema menores do
que React. Ele nao foi selecionado porque contratacao, continuidade e suporte
por IA no longo prazo sao prioridades maiores do que maxima elegancia de
framework.

## Consequencias

- O trabalho frontend inicial deve ser criado com Next.js e TypeScript.
- Tailwind CSS deve ser configurado desde o inicio.
- shadcn/ui pode ser introduzido como a fundacao de componentes reutilizaveis.
- Zod deve ser usado para validacao e contratos de dados quando formularios ou
  fronteiras de API aparecerem.
- Playwright deve ser usado para validar fluxos importantes de usuario em um
  navegador real.

## Validacao

Esta decisao permanece valida se:

- a aplicacao puder rodar localmente sem uma plataforma proprietaria;
- a aplicacao puder ser conteinerizada para deploy em Linux;
- agentes de IA conseguirem inspecionar, editar e testar trabalho de UI de
  forma previsivel;
- a stack permanecer ativamente mantida e amplamente adotada.

## Proximos Passos

- Definir a base backend em uma ADR separada.
- Definir o modelo de deploy em uma ADR separada antes de hospedagem em
  producao.
- Definir regras de componentes de UI depois que as primeiras telas reais forem
  desenhadas.
