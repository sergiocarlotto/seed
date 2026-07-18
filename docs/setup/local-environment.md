# Ambiente de Desenvolvimento Local (Windows) e Ponto de Retomada

Este documento registra o estado das ferramentas na maquina de desenvolvimento,
como rodar o projeto e onde o trabalho parou, para facilitar a retomada.

Ultima atualizacao: 2026-07-18.

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
