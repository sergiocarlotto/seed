# ADR-0007: Desenvolvimento Local, Docker e Deploy

## Status

Pendente

## Contexto

O projeto deve permanecer executavel em Linux com Docker e implantavel em uma
VPS, sem depender de recursos exclusivos de provedor.

A ADR-0002 define a base frontend. A ADR-0003 define a base backend. A ADR-0005
deve definir o banco de dados. Esta ADR deve conectar essas decisoes em um
fluxo operacional de desenvolvimento local e deploy.

## Decisao Pendente

Definir como `apps/web`, `apps/api` e servicos de infraestrutura rodam juntos
localmente, em CI futura e em producao.

Esta ADR deve decidir:

- estrutura de Dockerfiles;
- uso de Docker Compose para desenvolvimento local;
- variaveis de ambiente obrigatorias;
- estrategia de secrets;
- health checks;
- portas padrao;
- build e start de frontend e backend;
- execucao de migrations;
- estrategia inicial de deploy em VPS Linux;
- limites para evitar dependencia de Vercel, Azure, AWS ou outro provedor.

## Direcao Inicial

O desenvolvimento local deve ter um caminho simples para iniciar web, API e
banco com Docker Compose. O projeto tambem deve permitir rodar web e API
diretamente com Node.js e .NET SDK quando isso acelerar desenvolvimento local.

## Pontos Ainda Abertos

- Definir se o compose local sera unico ou separado entre dev e prod-like.
- Definir estrategia de reverse proxy em producao.
- Definir como migrations serao aplicadas com seguranca.
- Definir nome e formato dos arquivos de ambiente.
- Definir se jobs auxiliares serao necessarios no MVP.

## Validacao

Esta decisao podera ser aceita se:

- uma pessoa conseguir iniciar o ambiente local por comandos documentados;
- web, API e banco funcionarem em Linux com Docker;
- o caminho de deploy nao exigir provedor especifico;
- o ambiente preservar separacao clara entre desenvolvimento e producao.
