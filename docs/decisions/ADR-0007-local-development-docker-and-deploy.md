# ADR-0007: Desenvolvimento Local, Docker e Deploy

## Status

Aceita

## Contexto

O projeto deve permanecer executavel em Linux com Docker e implantavel em uma
VPS, sem depender de recursos exclusivos de provedor.

A ADR-0002 define a base frontend (Next.js em `apps/web`). A ADR-0003 define a
base backend (ASP.NET Core em `apps/api`, monolito modular). A ADR-0005 define
PostgreSQL com EF Core e migrations como parte do fluxo de evolucao. A ADR-0006
exige que web e API sejam servidos na **mesma origem** para viabilizar o cookie
de sessao `HttpOnly`/`Secure`/`SameSite`. Esta ADR conecta essas decisoes em um
fluxo operacional de desenvolvimento local, prod-like e deploy.

## Decisao

### Servicos

O ambiente e composto por quatro servicos em Docker Compose:

- `web`: aplicacao Next.js (`apps/web`).
- `api`: aplicacao ASP.NET Core (`apps/api`).
- `db`: PostgreSQL.
- `proxy`: reverse proxy **Caddy**, responsavel por TLS e roteamento same-origin.

### Estrategia de Compose

Usar um `docker-compose.yml` base com os servicos e overrides por ambiente:

- `docker-compose.override.yml`: desenvolvimento (aplicado automaticamente pelo
  Compose).
- `docker-compose.prod.yml`: configuracao prod-like, com imagens buildadas e o
  proxy terminando TLS.

Isso evita duplicar a definicao dos servicos e mantem as diferencas por ambiente
explicitas.

### Modos de desenvolvimento

Dois caminhos suportados:

- **Full Docker**: `web`, `api` e `db` sobem juntos com um comando documentado.
- **Hibrido**: apenas `db` roda em Docker; `web` (Node.js) e `api` (.NET SDK)
  rodam direto no host para iteracao mais rapida.

Ambos os modos devem ser documentados com comandos claros de start.

### Same-origin e TLS

Em producao e prod-like, o `proxy` Caddy termina TLS e roteia na mesma origem:

- `/api/*` para o servico `api`;
- todo o restante para o servico `web`.

Isso satisfaz o requisito de cookie same-site da ADR-0006. O Caddy foi escolhido
por prover HTTPS automatico (Let's Encrypt) com configuracao minima, sem lock-in
de provedor.

Em desenvolvimento local, a aplicacao roda em `http://localhost` e a flag
`Secure` do cookie e relaxada **apenas em ambiente de desenvolvimento**. Em
prod-like e producao, `Secure` e obrigatorio.

### Migrations

Migrations de banco **nao sao aplicadas no boot da API em producao**. Elas rodam
como um **passo explicito** no deploy (por exemplo, um comando ou container
one-shot executando `dotnet ef database update`) antes de a nova versao da API
receber trafego. Isso evita corrida entre multiplas instancias e mudancas de
schema acidentais.

Em desenvolvimento, migrations podem ser aplicadas via CLI do EF Core.

### Variaveis de ambiente e secrets

- Desenvolvimento usa um arquivo `.env` local, **fora do controle de versao**.
- Um `.env.example` versionado documenta as variaveis obrigatorias, sem valores
  sensiveis.
- Em producao, as variaveis sao **injetadas pelo host/ambiente**, nunca
  commitadas.
- Secrets (senha do banco, chaves de assinatura de token, credenciais de email)
  nunca entram no repositorio.

### Health checks e portas

- `db`: health check via `pg_isready`.
- `api`: expoe um endpoint `/health`.
- `web`: health check de disponibilidade do servidor Next.js.
- Portas padrao: `web` 3000, `api` 8080, `db` 5432, `proxy` 80/443.

### Jobs auxiliares

Nenhum job auxiliar e necessario no MVP alem do passo one-shot de migration.
Novos jobs (filas, tarefas agendadas) devem ser justificados por necessidade
concreta e, se relevantes para arquitetura, registrados em nova ADR.

## Consequencias

- O repositorio inclui `Dockerfile` para `web` e `api`, arquivos de Compose e um
  `Caddyfile`.
- O ambiente local exige Docker; o modo hibrido exige tambem Node.js e .NET SDK.
- O deploy em VPS Linux usa Docker Compose com o arquivo prod-like e o Caddy para
  TLS, sem dependencia de PaaS especifico.
- O pipeline de deploy passa a ter um passo explicito de migration antes de
  liberar trafego para a nova versao da API.
- A estrategia de email transacional (necessaria para convite/recuperacao da
  ADR-0006) continua pendente e devera definir suas variaveis de ambiente
  quando implementada.
- A configuracao de CI futura pode reutilizar as mesmas imagens e o mesmo passo
  de migration.

## Alternativas Consideradas

### Nginx como reverse proxy

Valido e ubiquo, com controle fino de configuracao. Rejeitado como padrao por
exigir configuracao separada de TLS (certbot e renovacao) e config mais verbosa,
sem beneficio relevante para o MVP frente ao TLS automatico do Caddy.

### Traefik como reverse proxy

Valido, com TLS automatico e descoberta por labels de container. Rejeitado por
introduzir mais conceitos do que o MVP precisa neste estagio.

### Aplicar migrations no boot da API

Rejeitado para producao. Simplifica o deploy, mas cria corrida entre instancias
e risco de alteracao de schema nao intencional. Aceitavel apenas em
desenvolvimento.

### Compose unico sem overrides

Rejeitado. Um unico arquivo tende a misturar configuracao de dev e prod e
dificulta manter diferencas seguras entre ambientes.

### Deploy em PaaS gerenciado (Vercel, Azure, AWS)

Rejeitado como dependencia obrigatoria, por violar o principio de portabilidade
do Marco Zero. Nada impede uso futuro opcional, desde que o caminho VPS+Docker
permaneca suportado.

## Validacao

Esta decisao permanece valida se:

- uma pessoa conseguir iniciar o ambiente local por comandos documentados, em
  full Docker ou hibrido;
- `web`, `api` e `db` funcionarem em Linux com Docker;
- web e API forem servidos same-origin, permitindo o cookie de sessao da
  ADR-0006;
- o caminho de deploy nao exigir provedor especifico;
- migrations forem aplicadas de forma previsivel e sem corrida em producao;
- o ambiente preservar separacao clara entre desenvolvimento e producao.

## Acompanhamento

- Definir a estrategia de email transacional antes de implementar convite e
  recuperacao de senha (ADR-0006).
- Criar os artefatos concretos (`Dockerfile`, arquivos de Compose, `Caddyfile`,
  `.env.example`) na primeira estruturacao de `apps/web` e `apps/api`.
- Definir o padrao de documentacao de modulos backend na ADR-0008 antes de
  implementar o primeiro modulo de negocio.
