# ADR-0010: Modelo Multiempresa (Organization -> Company -> Acesso por Usuario)

## Status

Aceita (parcialmente substituida pela ADR-0014 e pela ADR-0012)

A regra de **visibilidade por empresa sempre explicita** foi parcialmente
substituida pela ADR-0014: o **owner** da organizacao enxerga (e, pelo mesmo
caminho de leitura, edita e exclui) qualquer empresa da propria organizacao sem
`UserCompanyAccess`. Para os demais usuarios a regra continua valendo integral.
O papel de gestao `orgRole` (`Admin`/`Member`) foi substituido pela ADR-0012
(perfis configuraveis: `companies.access`, `companies.manage`). O restante —
modelo de tres niveis, isolamento duro por organizacao, `UserCompanyAccess` como
concessao explicita e 404 fora de escopo — permanece valido.

## Contexto

A ADR-0005 definiu `Organization` como a raiz de tenancy do produto. Ao detalhar
o primeiro modulo de negocio, o modelo de dominio foi esclarecido e mostrou-se
mais rico do que "uma organizacao = uma empresa":

- uma **organizacao** e a conta/cliente da plataforma e o muro de isolamento;
- dentro de uma organizacao existem **varias empresas** (multiempresa, ex.:
  varios CNPJs ou filiais do mesmo grupo);
- **usuarios** pertencem a uma organizacao e enxergam apenas as empresas as quais
  tem **acesso explicito**, nunca de outra organizacao.

A primeira implementacao tratava `Organization` como a propria empresa (com
auto-cadastro e CRUD de organizacoes). Isso nao reflete o dominio e precisa ser
corrigido antes de o modulo evoluir.

Design detalhado: `docs/specs/2026-07-18-organizations-multiempresa-design.md`.

## Decisao

Adotar um modelo de tres niveis, refinando (nao substituindo) a ADR-0005:

- **`Organization`**: a raiz de tenancy e o muro de isolamento (mantem ADR-0005).
  Provisionada por nos (por seed no MVP; painel super-admin no futuro).
- **`Company`** (empresa): varias por organizacao. Carrega `organization_id`. E o
  alvo do CRUD de empresa.
- **`User`**: pertence a uma organizacao, com papel de gestao `orgRole` in
  `{ Admin, Member }`.
- **`UserCompanyAccess`**: concessao explicita de acesso (usuario -> empresa).

Regras de acesso (aplicadas no backend):

- **Isolamento duro**: toda leitura/escrita filtra pelo `organization_id` do
  usuario autenticado; nada cruza organizacoes.
- **Visibilidade por empresa sempre explicita**: um usuario so ve/edita uma
  empresa se existir `UserCompanyAccess` correspondente — inclusive o admin. Nao
  ha "ver todas" automatico. **Excecao aberta pela ADR-0014**: o **owner**
  (`is_owner`) enxerga, edita e exclui todas as empresas da propria organizacao
  sem concessao explicita — e o piso antilockout que destrava a empresa orfa. O
  filtro por organizacao continua obrigatorio nesse ramo.
- **Gestao**: apenas `orgRole = Admin` cria, edita e exclui empresas. Ao criar
  uma empresa, o criador recebe automaticamente o acesso a ela.
- **Membro** so acessa as empresas concedidas; nao cria empresas.
- Acesso a empresa sem concessao ou de outra organizacao responde **404** (nao
  vaza existencia).

Autenticacao: sem auto-cadastro. Login por email+senha com cookie httpOnly
(ADR-0006). No MVP, organizacao/admin/empresa iniciais sao criados por **seed**
em ambiente de desenvolvimento.

## Consequencias

- O modelo fisico ganha `Company` e `UserCompanyAccess`; `User` (Identity) ganha
  `organization_id` e `org_role`.
- O CRUD de empresa opera sobre `Company`, sempre filtrado por acesso.
- Fica registrado como trabalho futuro: painel super-admin para provisionar
  organizacoes; **convite por email** (depende de email transacional); campos
  ricos da empresa (CNPJ, endereco).
  Ja entregues desde entao: a **gestao de usuarios** (criar com senha inicial
  definida pelo administrador, listar, ativar/desativar — modulo
  `access-control`), que nunca dependeu de email transacional, e a **UI de
  conceder/revogar acesso de empresas** a outros usuarios, nas duas direcoes
  (pela pessoa e pela empresa), sob a permissao `companies.grant_access` e o
  escopo concedivel da ADR-0014.
- A ADR-0005 permanece valida; esta ADR apenas detalha o nivel abaixo da
  organizacao e o escopo de acesso por usuario.

## Alternativas Consideradas

### Organization como a propria empresa (implementacao inicial)

Rejeitada. Nao suporta multiempresa nem o acesso por usuario dentro da conta.

### Admin ve todas as empresas automaticamente; member por concessao

Rejeitada. O dono da decisao definiu que a visibilidade e **sempre explicita por
empresa**, inclusive para administradores (que se auto-concedem ao criar).

Nota (ADR-0014): esta alternativa foi depois concedida ao **owner**, e apenas a
ele — nao aos administradores em geral. O racional e antilockout, nao
conveniencia: ao tornar o eixo de empresa revogavel, a ADR-0014 criou a
possibilidade de uma **empresa orfa** (sem nenhum usuario com acesso), que sem um
caminho de leitura so seria recuperavel pelo banco. Quem tem `companies.manage`
sem ser owner continua sujeito a concessao explicita.

### Auto-cadastro de organizacao (self-service)

Rejeitada para o MVP. Organizacoes sao provisionadas por nos; no MVP via seed,
futuramente via painel super-admin.

## Validacao

Esta decisao permanece valida se:

- nenhum dado cruza organizacoes;
- um usuario **nao-owner** so acessa empresas concedidas a ele, dentro da sua
  organizacao; o **owner** alcanca toda a propria organizacao (ADR-0014) e
  nenhuma empresa fora dela;
- apenas administradores criam/editam/excluem empresas;
- acesso sem concessao ou cross-tenant responde 404;
- o comportamento e coberto por testes de integracao.
