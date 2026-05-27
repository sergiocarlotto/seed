# ADR-0003: Base Backend

## Status

Aceita

## Contexto

O produto precisa de uma fundacao backend que suporte uma arquitetura SaaS
robusta, separacao clara entre responsabilidades de frontend e backend,
aplicacao segura de regras de negocio e desenvolvimento assistido por IA de
forma previsivel.

A base frontend ja esta definida como TypeScript, React, Next.js, Tailwind CSS,
shadcn/ui, Zod e Playwright. O projeto deve permanecer portavel, executavel em
Linux com Docker e independente de recursos exclusivos de provedores de
hospedagem.

A principal preocupacao arquitetural e impedir que regras de negocio,
validacoes criticas, autorizacao, verificacoes de tenancy e responsabilidades
de auditoria sejam misturadas ao frontend ou espalhadas por codigo de UI por
humanos ou agentes de IA.

## Decisao

Usar uma aplicacao backend separada construida com C# e ASP.NET Core.

O backend sera organizado como monolito modular, com separacao clara entre
responsabilidades de API, application, domain e infrastructure.

Estrutura inicial da solucao:

```txt
apps/
  web/
  api/
    src/
      Seed.Api/
      Seed.Application/
      Seed.Domain/
      Seed.Infrastructure/
    tests/
      Seed.UnitTests/
      Seed.IntegrationTests/
```

O frontend consumira o backend por APIs HTTP documentadas com OpenAPI.

Regras de responsabilidade do backend:

- Regras de negocio pertencem ao backend.
- Validacoes criticas pertencem ao backend.
- Autorizacao pertence ao backend.
- Enforcement de multitenancy pertence ao backend.
- Comportamentos relevantes para auditoria pertencem ao backend.
- O frontend pode executar validacao apenas para experiencia de usuario.
- Nenhuma regra critica e valida se existir apenas no frontend.
- Endpoints HTTP devem permanecer finos e delegar casos de uso para a camada de
  application.
- Regras de dominio nao devem depender de ASP.NET Core, banco de dados ou
  codigo frontend.
- Preocupacoes de infrastructure, como persistencia, provedores externos e
  integracoes de runtime, pertencem fora da camada de domain.

Responsabilidades de teste:

- Testes unitarios devem cobrir regras de domain, validators, policies e casos
  de uso da application.
- Testes de integracao devem cobrir endpoints HTTP, persistencia,
  autenticacao, autorizacao e isolamento de tenant.
- Testes end-to-end de navegador continuam sob responsabilidade da suite de
  frontend e devem validar fluxos importantes de usuario com Playwright.

## Racional

C# e ASP.NET Core fornecem uma base backend forte para um produto SaaS que
precisa de manutenibilidade, seguranca, testabilidade e fronteiras
arquiteturais explicitas no longo prazo.

Essa escolha ajuda agentes de IA e humanos a trabalharem com seguranca porque o
projeto tem separacao fisica e conceitual entre codigo frontend e backend. Ela
tambem reduz o risco de colocar regras de negocio em componentes React, paginas
Next.js ou helpers de validacao client-side.

ASP.NET Core e open source, maduro, bem documentado, amplamente adotado e bem
adequado a deployments Linux e Docker. C# oferece tipagem forte, excelente
ferramental, estrutura previsivel de projeto e suporte maduro a testes.

## Consequencias

- O repositorio tera pelo menos duas aplicacoes: `apps/web` e `apps/api`.
- Frontend e backend evoluirao com contratos HTTP explicitos.
- OpenAPI se torna a fronteira de documentacao de contrato entre web e API.
- Desenvolvimento backend exige ferramental .NET alem do ferramental Node.js
  usado pelo frontend.
- Tipos compartilhados entre frontend e backend devem ser gerados a partir de
  contratos ou mapeados explicitamente, nao copiados informalmente.
- Agentes de IA devem respeitar a propriedade do backend sobre regras de
  negocio, validacoes criticas, autorizacao, tenancy e auditoria.
- Fluxos locais e de producao baseados em Docker devem suportar ambas as
  aplicacoes.

## Alternativas Consideradas

### Backend Fullstack Com Next.js

Rejeitado como base principal de backend.

Usar route handlers ou server actions do Next.js para o backend principal
reduziria a complexidade inicial de setup e manteria a aplicacao em uma unica
linguagem. Porem, isso tambem aumenta o risco de misturar regras de negocio,
logica de UI, handlers de servidor e validacao no mesmo codigo. Esse risco e
especialmente relevante para desenvolvimento assistido por IA.

Next.js permanece como framework frontend aceito.

### Backend TypeScript Com NestJS Ou Fastify

Considerado valido, mas nao selecionado.

Um backend TypeScript separado poderia oferecer fronteiras claras preservando
uma unica linguagem na stack. Porem, C# e ASP.NET Core oferecem convencoes
padrao mais fortes para estrutura backend enterprise, testes, autorizacao e
manutenibilidade SaaS de longo prazo.

### Java Com Spring Boot

Considerado valido, mas nao selecionado.

Java e Spring Boot sao maduros e robustos, mas sao mais pesados para as
necessidades iniciais do projeto. C# e ASP.NET Core oferecem nivel semelhante
de maturidade backend com uma experiencia de desenvolvimento mais direta para
este projeto.

### Python Com FastAPI

Rejeitado como base principal do backend SaaS.

Python e FastAPI sao escolhas fortes para servicos de IA, automacao e
processamento de dados. Eles podem ser uteis para servicos especificos de IA no
futuro, mas o backend principal do SaaS precisa de fronteiras estruturais mais
fortes para logica de dominio transacional, autorizacao, tenancy e auditoria.

### Go

Considerado valido, mas nao selecionado.

Go e operacionalmente simples e performatico, mas oferece menos convencoes de
alto nivel para uma aplicacao SaaS rica em dominio. Mais arquitetura precisaria
ser montada manualmente.

## Validacao

Esta decisao permanece valida se:

- o backend puder rodar em Docker no Linux;
- o frontend nao puder contornar autorizacao ou validacao critica do backend;
- regras de negocio forem implementadas e testadas em modulos backend;
- OpenAPI documentar com precisao o contrato entre frontend e backend;
- testes unitarios e de integracao puderem rodar independentemente do frontend;
- isolamento de tenant e comportamento relevante para auditoria forem aplicados
  server-side;
- futuras IAs conseguirem identificar onde regras, validacao, autorizacao e
  persistencia devem ficar.

## Proximos Passos

- Definir o modelo de banco de dados e propriedade dos dados na ADR-0005.
- Definir autenticacao e autorizacao em uma ADR posterior antes de implementar
  acesso de usuarios.
- Definir fluxos de desenvolvimento local e deploy baseados em Docker antes de
  hospedagem em producao.
- Definir padroes de documentacao de modulos backend antes de implementar o
  primeiro modulo de negocio.
