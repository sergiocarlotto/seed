# Design — Módulo `organizations` (multiempresa)

**Data:** 2026-07-18
**Status:** Aprovado (brainstorming) — pendente de plano de implementação
**Branch:** `feat/organizations-login-empresa`

## Contexto

O modelo de domínio foi esclarecido e é um multi-tenant de três níveis, mais
rico do que a primeira implementação (que tratava `Organization` como se fosse a
própria empresa). Este design corrige o modelo e substitui a abordagem anterior
de auto-cadastro/CRUD de organizações.

Refina a ADR-0005 (que define `Organization` como raiz de tenancy): a raiz
continua sendo `Organization`, mas passa a existir a entidade `Company` (empresa)
como sub-nível, com escopo de acesso por usuário.

## Modelo de domínio

Hierarquia de três níveis:

1. **Organization** — o tenant e o muro de isolamento. Nenhum dado cruza
   organizações. Provisionada por nós (semeada no MVP; super-admin no futuro).
2. **Company (Empresa)** — várias por organização (multiempresa, ex.: vários
   CNPJs/filiais). É o alvo do CRUD.
3. **User** — pertence a uma organização; enxerga apenas as empresas às quais
   recebeu acesso explícito.

### Entidades

| Entidade | Papel | Campos principais |
| --- | --- | --- |
| `Organization` | Tenant / isolamento | `id`, `name`, `status`, `createdAt`, `updatedAt` |
| `ApplicationUser` (Identity) | Usuário; pertence a uma org | `id`, `email`, `passwordHash`, `fullName`, `organizationId`, `orgRole` |
| `Company` | Empresa; várias por org | `id`, `organizationId`, `name`, `status`, `createdAt`, `updatedAt`, `deletedAt` |
| `UserCompanyAccess` | Concessão explícita usuário→empresa | `id`, `userId`, `companyId`, `organizationId`, `createdAt` |

Relações: `Organization` 1—N `Company`; `Organization` 1—N `User`;
`User` N—N `Company` via `UserCompanyAccess`.

`orgRole` (papel de gestão na organização): `Admin` | `Member`.

## Regras de acesso (enforcement no backend)

- **Isolamento duro:** toda leitura/escrita filtra pelo `organizationId` do
  usuário autenticado. Nada cruza organizações.
- **Visibilidade por empresa (sempre explícita):** o usuário só vê/edita uma
  empresa se existir `UserCompanyAccess` para (usuário, empresa) — **inclusive o
  admin**. Não há "ver todas" automático.
- **Gestão:** apenas `orgRole = Admin` pode criar empresas (e, no futuro,
  conceder/revogar acesso e gerir usuários). Ao criar uma empresa, o criador
  recebe automaticamente a concessão de acesso a ela.
- `Member` não cria empresas; apenas acessa as que lhe foram concedidas.
- Acesso a uma empresa de outra organização, ou sem concessão: responde **404**
  (não vaza existência).

## Autenticação

- **Removido:** auto-cadastro (`register`) que criava organização. Organizações
  são provisionadas por nós.
- **Mantido:** login (email+senha, cookie httpOnly/SameSite=Lax/Secure em prod —
  ADR-0006), logout, e `me`.
- `me` retorna: usuário + organização + lista das empresas às quais tem acesso.

## Escopo do MVP

- **Seed (apenas em Development):** 1 `Organization` "Demo", 1 `User` admin (email
  e senha fixos), 1 `Company` de exemplo com `UserCompanyAccess` para o admin —
  assim o login já mostra uma empresa e o CRUD é utilizável.
- **CRUD de `Company`** dentro da organização do usuário, restrito às empresas
  concedidas: listar (as minhas), criar (admin; auto-concede), ver, editar,
  excluir (soft delete).
- **Frontend:** sem tela de registro; login → lista das minhas empresas → criar
  /ver/editar/excluir. Construído com **shadcn/ui + Tailwind** (ADR-0002).
- **Testes de integração (Testcontainers):** isolamento entre organizações;
  usuário só vê empresas concedidas; CRUD; tentativa cross-tenant = 404.

## Fora de escopo (futuro — registrado, não construído agora)

- **Super-admin:** painel para provisionar organizações e seus admins.
- **Convite e gestão de usuários** na org (depende de e-mail transacional).
- **Conceder/revogar** acesso de empresas a outros usuários (UI e endpoints).
- **Campos ricos da empresa** (CNPJ, endereço, etc.).

## Design system

- **Agora:** usar o padrão já decidido na ADR-0002 — **shadcn/ui + Tailwind** —
  como base de componentes, garantindo consistência visual sem custo alto.
- **Futuro (registrado):** um **design system** formal (tokens de cor/tipografia/
  espaçamento, tema unificado, componentes próprios) para dar a mesma "cara" a
  todo o produto. Deve ganhar um **ADR próprio** quando for tratado.

## Impacto na implementação atual

O que já foi construído nesta branch (auth/cookie, infra EF/Identity, Docker,
testes) é reaproveitado. Muda o **modelo**:

- Separar `Organization` (tenant) de `Company` (empresa).
- Adicionar `UserCompanyAccess` (acesso explícito).
- `User` passa a ter `organizationId` + `orgRole`.
- Trocar o `register` self-service por **seed** de organização/admin/empresa.
- O CRUD passa a operar em `Company` (não em `Organization`).
- Ajustar `me` e o frontend (remover registro; lista de empresas).

Trabalho continua na branch `feat/organizations-login-empresa`; a `main` e o
backup permanecem intactos.

## Decisões a registrar como ADR

- **ADR-0010 (proposto):** Modelo multiempresa — `Organization` (tenant) →
  `Company` (empresa, várias por org) → acesso explícito por usuário
  (`UserCompanyAccess`). Refina a ADR-0005.
- **ADR-0011 (proposto):** Abordagem de UI/design system — shadcn/ui + Tailwind
  como base interina; design system formal adiado (ADR próprio no futuro).

## Critérios de aceite

- [ ] Seed cria organização Demo + admin + empresa de exemplo (concedida ao admin).
- [ ] Login por email+senha (cookie httpOnly); `me` sem sessão = 401.
- [ ] Usuário só vê/edita empresas concedidas a ele, dentro da sua organização.
- [ ] Acesso a empresa de outra organização ou sem concessão = 404.
- [ ] Admin cria empresa e passa a vê-la (auto-concessão); Member não cria.
- [ ] Exclusão de empresa é soft delete.
- [ ] Testes de integração verdes (isolamento, acesso explícito, CRUD, cross-tenant).
- [ ] Frontend (shadcn/ui): login e CRUD de empresa funcionam via Docker (same-origin).
- [ ] Evolução registrada em ADR-0010 e ADR-0011; ADR-0005 referenciada.
