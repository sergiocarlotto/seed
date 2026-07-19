# Ambiente de Desenvolvimento Local (Windows) e Ponto de Retomada

Este documento registra o estado das ferramentas na maquina de desenvolvimento,
como rodar o projeto e onde o trabalho parou, para facilitar a retomada.

Ultima atualizacao: 2026-07-19.

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
  `dotnet test` passa (inclui teste de integracao que sobe a API).
- `apps/web`: frontend Next.js 16 + TypeScript + Tailwind (ADR-0002). Build ok,
  0 vulnerabilidades no npm. Saida `standalone` para container.
- Docker: `Dockerfile` de cada app, `docker-compose.yml` + overrides de dev e
  prod, `Caddyfile` (same-origin) e `.env.example` (ADR-0007). Stack sobe com
  todos os servicos `healthy` e o roteamento same-origin foi validado.

## Modulo `organizations` — multiempresa (branch `feat/organizations-login-empresa`)

O primeiro modulo de negocio esta implementado no modelo multiempresa e
verificado (2026-07-18): `Organization` (tenant) -> `Company` (varias por org) ->
acesso explicito por usuario (`UserCompanyAccess`); login por email+senha (cookie
httpOnly), CRUD de empresa restrito ao acesso, papeis `Admin`/`Member`, seed de
dev (org Demo + admin + empresa), e testes de integracao (10/10 verdes com
Postgres real). Frontend com shadcn/ui: login e CRUD de empresa. Sem auto-cadastro
(organizacoes sao provisionadas por nos; super-admin no futuro).

Design/plano/decisoes: `docs/specs/2026-07-18-organizations-multiempresa-design.md`,
`docs/plans/2026-07-18-organizations-multiempresa-rework.md`, ADR-0010 e ADR-0011.

### Como experimentar

```powershell
docker compose up -d --build
# abra http://localhost/login e entre com o usuario semeado (Development):
#   email:  admin@demo.local
#   senha:  Admin123!
# gerencie as empresas em http://localhost/companies (Admin cria/edita/exclui)
docker compose down
```

## Proximo passo

- Revisar e mesclar a branch `feat/organizations-login-empresa` na `main`.
- Sub-decisao pendente: estrategia de email transacional (para convite e
  recuperacao de senha da ADR-0006).
- Proximo modulo: `clients` (cadastro dos clientes atendidos), seguindo o mesmo
  ritmo (doc -> modelo + migration -> endpoints + testes -> UI).

## Sub-decisao ainda em aberto

- Estrategia de **email transacional** (envio automatico de e-mails), necessaria
  para os fluxos de convite e recuperacao de senha da ADR-0006. Nao bloqueia o
  modulo `organizations`, mas precisa ser definida antes de implementar convite e
  recuperacao.
