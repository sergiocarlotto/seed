# Ambiente de Desenvolvimento Local (Windows) e Ponto de Retomada

Este documento registra o estado das ferramentas na maquina de desenvolvimento,
como rodar o projeto e onde o trabalho parou, para facilitar a retomada.

Ultima atualizacao: 2026-07-22.

## Status das ferramentas

| Ferramenta | Para que serve | Status |
| --- | --- | --- |
| Node.js v24.15.0 + npm 11.12.1 | Rodar o frontend (Next.js) | Instalado |
| .NET SDK 10.0.302 | Criar e compilar o backend (ASP.NET Core) | Instalado |
| Docker Desktop 29.6.1 + Compose v5.3.0 | Empacotar e rodar tudo junto (backend, frontend, banco) em containers, conforme ADR-0007 | Instalado |
| WSL2 (distro `docker-desktop`) | Linux enxuto dentro do Windows que o Docker usa como motor | Instalado |

O ambiente completo esta pronto. Para reinstalar o Docker em outra maquina
Windows, use `scripts/setup-docker-windows.cmd` (instala WSL2 + Docker Desktop;
exige reinicio).

## Gerar migrations do EF Core (contornando o Smart App Control)

Nesta maquina o Windows esta com o **Smart App Control (SAC) LIGADO**
(`HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy\VerifiedAndReputablePolicyState = 1`).
Isso quebra o `dotnet ef migrations add/remove` rodado direto no host: a
ferramenta carrega por reflexao o `Seed.Infrastructure.dll` recem-compilado (um
assembly sem assinatura) e o SAC bloqueia esse carregamento. (`dotnet build`,
`dotnet test` e `dotnet run` continuam funcionando — o bloqueio e especifico do
carregamento por reflexao que o EF faz.)

Nao vale desligar o SAC: ele **nao oferece exclusao por arquivo** e, uma vez
desligado, so volta a ligar **resetando/reinstalando o Windows**. A saida e
gerar as migrations dentro de um container Linux (.NET SDK), onde o SAC nao se
aplica. Use o wrapper:

```powershell
# Gerar uma migration nova (os arquivos aparecem no host, via bind mount):
scripts/ef.ps1 migrations add AddAccessControl -o Persistence/Migrations

# Listar / remover a ultima:
scripts/ef.ps1 migrations list
scripts/ef.ps1 migrations remove --force   # --force pula a checagem de banco
```

O wrapper monta o repo em `mcr.microsoft.com/dotnet/sdk:10.0`, instala o
`dotnet-ef`, restaura e roda o comando com `--project src/Seed.Infrastructure
--startup-project src/Seed.Api`. Um volume nomeado (`seed-nuget`) faz cache dos
pacotes entre execucoes.

Detalhes importantes:

- **Gerar migration nao toca no banco** (e offline). A APLICACAO acontece sozinha
  quando a API sobe (`Database.Migrate()` em `Seed.Api/Program.cs`); nao ha passo
  manual de `database update` no fluxo normal.
- `migrations remove` sem `--force` tenta conectar no Postgres (que nao existe em
  `localhost` dentro do container) e falha; use `--force`.
- A regeneracao do `SeedDbContextModelSnapshot.cs` pelo EF 10.0.10 pode introduzir
  diffs cosmeticos (ex.: `ToTable("X", (string)null)`); e esperado e inofensivo.
- Este e o caminho oficial para as migrations dos planos de controle de acesso
  (planos 2 e 3 em `docs/plans/`).

## Rodar os testes (contornando o Smart App Control)

O mesmo SAC bloqueia `dotnet test` no host: o `testhost` carrega o
`Seed.Infrastructure.dll` recem-compilado (sem assinatura) e o SAC bloqueia
(`System.IO.FileLoadException`, `0x800711C7`). Como o SAC avalia por hash, todo
rebuild volta a ser bloqueado. Use o wrapper que roda `dotnet test` dentro do
container Linux do .NET SDK:

```powershell
scripts/test.ps1                                              # suite completa
scripts/test.ps1 --filter FullyQualifiedName~NomeDosTestes    # subconjunto
```

O wrapper monta o repo e o **socket do Docker do host** no container, e define
`TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal` para o Testcontainers subir o
Postgres real no host e o codigo de teste (dentro do container) alcanca-lo.
`dotnet build` continua funcionando no host normalmente (o SAC so bloqueia o
carregamento por reflexao do testhost/EF).

## Como rodar o projeto

### Stack completa em Docker

Na raiz do repositorio:

```powershell
# 1. Crie o seu .env local a partir do exemplo (uma vez)
Copy-Item .env.example .env   # e ajuste a senha do banco

# 2. Suba tudo (db + api + web + proxy)
docker compose up -d --build

# 3. Acesse na mesma origem (via proxy Caddy):
#    http://localhost/           -> frontend (Next.js)
#    http://localhost/api/health -> backend (ASP.NET Core)

# 4. Pare tudo
docker compose down
```

O `.env` NAO e versionado (contem segredos). O modelo versionado e `.env.example`.

### Modo hibrido (iteracao rapida, sem rebuild de imagem)

Rode so o banco em Docker e as apps no host:

```powershell
docker compose up -d db                       # banco em Docker (porta 5432 exposta)
dotnet run --project apps/api/src/Seed.Api    # backend no host
npm --prefix apps/web run dev                 # frontend no host
```

### Rodar a partir de um worktree (`.worktrees/<branch>`)

Trabalhando num git worktree, tres detalhes custam tempo se nao estiverem
escritos:

- **O `.env` nao existe no worktree** (nao e versionado). Aponte o compose para o
  `.env` do repositorio principal, senao os containers sao **recriados com senha
  vazia** e a API morre com `28P01`:

  ```powershell
  docker compose --env-file C:\Users\sergi\pessoal\seed\.env up -d db api
  ```

- **Nao suba o servico `web` do compose**: ele disputa a porta 3000 com o
  `next dev` que o Playwright levanta. Suba so `db` e `api` (`docker compose
  ... up -d db api`) ou pare o `web` (`docker compose stop web`).

- **Container de API velho responde 405 a endpoint novo.** Se a imagem em
  execucao foi construida a partir da `main`, a rota nova simplesmente nao existe
  nela: o ASP.NET casa o path (`/users/{id}/...`) mas nao o verbo, e devolve
  **405 Method Not Allowed** — que parece bug de roteamento e nao container
  obsoleto. Rebuilde a API a partir do worktree antes de investigar qualquer
  outra coisa:

  ```powershell
  docker compose --env-file C:\Users\sergi\pessoal\seed\.env up -d --build api
  ```

### Prod-like

```powershell
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

Defina `SITE_ADDRESS=seu-dominio.com` no `.env` para o Caddy emitir HTTPS
automaticamente.

## Estado atual do codigo (scaffold concluido e verificado)

O *scaffold* (estrutura inicial) do monorepo esta montado e verificado rodando:

- `apps/api`: backend ASP.NET Core (.NET 10), solution modular `Seed.Api`,
  `Seed.Application`, `Seed.Domain`, `Seed.Infrastructure` mais os projetos de
  teste, com referencias em camadas (ADR-0003). Endpoint `/health` ativo.
  A suite passa via `scripts/test.ps1` (ver a secao de testes acima; nesta
  maquina o `dotnet test` direto no host e bloqueado pelo SAC).
- `apps/web`: frontend Next.js 16 + TypeScript + Tailwind (ADR-0002). Build ok,
  0 vulnerabilidades no npm. Saida `standalone` para container.
- Docker: `Dockerfile` de cada app, `docker-compose.yml` + overrides de dev e
  prod, `Caddyfile` (same-origin) e `.env.example` (ADR-0007). Stack sobe com
  todos os servicos `healthy` e o roteamento same-origin foi validado.

## Modulos implementados

Ambos estao na `main` (mergeados) e verificados.

### `organizations` — multiempresa (2026-07-18)

`Organization` (tenant) -> `Company` (varias por org) -> acesso explicito por
usuario (`UserCompanyAccess`); login por email+senha (cookie httpOnly), CRUD de
empresa restrito ao acesso e seed de dev (org Demo + admin + empresa). Frontend
com shadcn/ui: login e CRUD de empresa. Sem auto-cadastro (organizacoes sao
provisionadas por nos; super-admin no futuro).

Desde 2026-07-22 o modulo tambem **concede e revoga o acesso de usuarios as
empresas** (`companies.grant_access`), nas duas direcoes — pela tela do usuario
(`PUT /users/{id}/companies`) e pela tela da empresa
(`GET`/`PUT /companies/{id}/users`) —, com o **escopo concedivel** da ADR-0014:
o nao-owner so concede o que ja acessa; o owner alcanca a organizacao inteira.

Nota: os papeis fixos `Admin`/`Member` deste modulo **foram removidos** pela
ADR-0012 — ver abaixo.

Doc do modulo: `docs/modules/organizations.md`. Design/plano: ADR-0010, ADR-0011,
ADR-0014 (escopo de concessao).

### `access-control` — perfis e permissoes (2026-07-21)

Perfis configuraveis por organizacao no lugar dos papeis fixos (ADR-0012):
catalogo de permissoes declarado no codigo e reconciliado no boot, perfis por
org, atribuicao a usuarios, permissao efetiva por request e enforcement por
`[RequirePermission]`. Desde 2026-07-22 tambem **cria usuarios** na organizacao
(`POST /users`, com senha inicial definida pelo administrador; nao ha convite por
email). O dono da organizacao (`is_owner`) tem bypass funcional e, desde a
ADR-0014, tambem no **eixo de empresa** dentro da propria organizacao; segue
somente-leitura na aplicacao quanto a status e perfis (suas empresas sao
editaveis). Frontend: telas de Perfis e Usuarios.

Verificado: 1 unit + 79 testes de integracao no backend (Postgres real), 27 unit
no frontend, 13 e2e.

Doc do modulo: `docs/modules/access-control.md`. Decisoes: ADR-0012 (perfis),
ADR-0013 (padrao do `AuditEvent`), ADR-0014 (bypass do owner no eixo de empresa).

### Como experimentar

```powershell
docker compose up -d --build
# abra http://localhost/login e entre com o usuario semeado (Development):
#   email:  admin@demo.local
#   senha:  Admin123!
# empresas em /companies; perfis em /profiles; usuarios em /users
docker compose down
```

O usuario semeado e o **owner** da organizacao Demo: ve tudo, e nao pode ser
desativado nem ter perfis alterados pela aplicacao (por design — e o piso que
evita trancar a organizacao por fora).

**Atencao ao banco de dev:** o volume `pgdata` sobrevive entre branches, e o
Postgres so aplica `POSTGRES_PASSWORD` na **criacao** do volume. Se voce trocar a
senha no `.env`, a API sobe e morre com `28P01: password authentication failed`.
Corrija por dentro, sem apagar o volume (e sem perder os dados de dev):

```powershell
docker compose exec -T db psql -U seed -d seed -c "ALTER USER seed WITH PASSWORD 'a-senha-do-seu-env';"
docker compose up -d api
```

## Rodar os testes do frontend

Rodam no host (o SAC nao bloqueia):

```powershell
npm --prefix apps/web run test    # vitest
npm --prefix apps/web run lint
npm --prefix apps/web run build
```

O Playwright, ao contrario dos demais, **precisa ser invocado de dentro de
`apps/web`** — com `--prefix` o `playwright.config.ts` nao e resolvido a partir
do diretorio do projeto e o `baseURL` (`http://localhost:3000`) nao e aplicado,
fazendo as navegacoes relativas falharem:

```powershell
cd apps/web
npx playwright test          # exige a API de pe (docker compose up -d db api)
# npm run e2e (de dentro de apps/web) e equivalente; o que nao funciona e o --prefix
```

O Playwright sobe o `next dev` sozinho, mas **nao** sobe a API: deixe `db` e `api`
rodando antes. Nao suba o servico `web` do compose junto, para nao disputar a
porta 3000.

## Proximo passo

- Sub-decisao pendente: estrategia de email transacional (para convite e
  recuperacao de senha da ADR-0006).
- Proximo modulo: `clients` (cadastro dos clientes atendidos), seguindo o mesmo
  ritmo (doc -> modelo + migration -> endpoints + testes -> UI).

## Sub-decisao ainda em aberto

- Estrategia de **email transacional** (envio automatico de e-mails), necessaria
  para os fluxos de convite e recuperacao de senha da ADR-0006. Nao bloqueia o
  modulo `organizations`, mas precisa ser definida antes de implementar convite e
  recuperacao.
