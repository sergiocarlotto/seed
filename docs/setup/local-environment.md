# Ambiente de Desenvolvimento Local (Windows) e Ponto de Retomada

Este documento registra o estado das ferramentas na maquina de desenvolvimento,
o passo pendente de instalacao do Docker e onde o trabalho parou, para facilitar
a retomada.

Ultima atualizacao: 2026-07-17.

## Status das ferramentas

| Ferramenta | Para que serve | Status |
| --- | --- | --- |
| Node.js v24.15.0 + npm 11.12.1 | Rodar o frontend (Next.js) | Instalado |
| .NET SDK 10.0.302 | Criar e compilar o backend (ASP.NET Core). O SDK e o kit de desenvolvimento; sem ele so da para executar programas .NET prontos, nao criar novos | Instalado nesta sessao |
| Docker Desktop | Empacotar e rodar tudo junto (backend, frontend, banco) em containers, conforme ADR-0007 | **PENDENTE** |
| WSL2 | Linux enxuto dentro do Windows que o Docker usa como motor | **PENDENTE** (nao instalado) |

## Passo pendente: instalar o Docker Desktop

O Docker depende do WSL2, e instalar o WSL2 exige reiniciar o computador. Por
isso foi criado um script que faz a instalacao de uma vez.

### Como executar

1. Rode o script `scripts/setup-docker-windows.cmd` (dois cliques nele).
   - Uma copia pronta para uso tambem esta em `c:\Users\sergi\pessoal\setup-docker.cmd`.
2. Confirme a janela de permissao de administrador (UAC) com **Sim**.
3. O script instala o WSL2 e o Docker Desktop via winget (leva alguns minutos).
4. **Reinicie o computador** (necessario para o WSL2).
5. Abra o **Docker Desktop**, aceite os termos e espere o status ficar
   **"Engine running"** (motor rodando).

### Como verificar que funcionou

Abra um terminal e rode:

```powershell
docker --version
docker compose version
```

Se ambos responderem com uma versao, o Docker esta pronto.

> O que o script faz por dentro: `wsl --install --no-distribution` (instala o
> WSL2 sem uma distribuicao Linux extra, ja que o Docker traz a sua propria) e
> `winget install Docker.DockerDesktop` (instala o Docker Desktop aceitando os
> termos automaticamente).

## Onde paramos e proximo passo

- Todas as decisoes de arquitetura (ADR-0000 a ADR-0009) estao **Aceitas**. A
  fundacao (Marco Zero) esta completa e nao ha decisoes pendentes.
- **Proximo passo**: montar o *scaffold* (a estrutura inicial vazia) do monorepo:
  - `apps/web`: frontend Next.js;
  - `apps/api`: backend ASP.NET Core (projetos `Seed.Api`, `Seed.Application`,
    `Seed.Domain`, `Seed.Infrastructure` mais os projetos de teste), conforme
    ADR-0003;
  - arquivos de Docker: `Dockerfile` de cada app, `docker-compose.yml` com
    overrides, `Caddyfile` e `.env.example`, conforme ADR-0007.
- Depois do scaffold: primeiro modulo `organizations` (escrever
  `docs/modules/organizations.md` a partir do template e implementar a base de
  tenancy: `Organization`, `User`, `OrganizationMembership` com ASP.NET Core
  Identity), conforme ADR-0006 e ADR-0008.

> Importante: o scaffold **nao depende do Docker** para ser criado e testado
> (bastam Node.js e o .NET SDK, ja instalados). O Docker e necessario para rodar
> o ambiente completo de forma padronizada e para o deploy. Ou seja, da para
> avancar no scaffold em paralelo enquanto o Docker e instalado.

## Sub-decisao ainda em aberto

- Estrategia de **email transacional** (envio automatico de e-mails), necessaria
  para os fluxos de convite e recuperacao de senha da ADR-0006. Nao bloqueia o
  scaffold, mas precisa ser definida antes de implementar esses fluxos.
