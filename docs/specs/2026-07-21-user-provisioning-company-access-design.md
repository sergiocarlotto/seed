# Design — Provisionamento de Usuários e Concessão de Acesso a Empresas

- **Data:** 2026-07-21
- **Status:** Aprovado (brainstorming) — pré-plano
- **Módulos alvo:** `access-control` (criar usuário) e `organizations`
  (concessão de acesso a empresa)
- **Relacionados:** ADR-0012 (perfis e permissões), ADR-0013 (padrão do
  `AuditEvent`), ADR-0010 (multiempresa), ADR-0006 (auth — convite por e-mail
  ainda pendente), `docs/modules/access-control.md`,
  `docs/modules/organizations.md`

## Contexto e problema

A ADR-0012 está concluída: perfis configuráveis, catálogo de permissões,
atribuição de perfis, permissão efetiva por request e enforcement por
`[RequirePermission]`, com as telas `/profiles` e `/users` entregues.

Falta o que torna esse controle **operável**. A organização tem hoje um único
usuário — o owner semeado — que é justamente o único que a aplicação se recusa a
editar. A tela de Usuários existe sem um caso de uso executável de ponta a ponta.
Duas capacidades estão ausentes:

1. **Criar usuário.** Não existe `POST /users`; o controller só tem `GET`,
   `GET/{id}`, `PATCH /{id}/status` e `PUT /{id}/profiles`.
2. **Conceder/revogar acesso a empresa.** `UserCompanyAccess` só é escrito em
   dois lugares: a auto-concessão de quem cria a empresa (`CompanyService`) e o
   `DataSeeder`. Nenhum endpoint concede acesso a **outro** usuário, então o
   segundo eixo de autorização da ADR-0012 existe apenas pela metade.

**Fora de escopo:** e-mail transacional, convite por e-mail e recuperação de
senha seguem pendentes (sub-decisão aberta desde a ADR-0006); nenhum fornecedor
é escolhido aqui. Postura A no eixo funcional e visualizador de auditoria
continuam no backlog.

## Decisões travadas no brainstorming

1. **Senha inicial definida pelo administrador, sem troca obrigatória no
   primeiro login.** Não há flag `must_change_password`, tela de troca nem gate
   de navegação neste incremento. Consequência assumida na seção
   "Riscos aceitos".
2. **`users.manage` governa criar usuário.** Sem chave nova. O que sustenta essa
   escolha é a decisão 4: como o usuário nasce sem perfis, criar produz uma
   identidade de permissão efetiva **vazia** — não é caminho de escalada por si
   só. O poder real continua atrás de `profiles.assign`.
3. **Permissão nova `companies.grant_access`** governa conceder e revogar acesso
   a empresa, declarada pelo módulo `organizations` (dono de `UserCompanyAccess`).
   É a mesma simetria que o projeto já adotou para perfis: `profiles.manage` cria
   o perfil, `profiles.assign` o atribui a pessoas; `companies.manage` cria a
   empresa, `companies.grant_access` decide quem entra nela. Conceder acesso a
   empresa é concessão de acesso a **dados** (o segundo eixo da ADR-0012), não
   manutenção de cadastro.
4. **Usuário nasce `Active`, sem perfis e sem empresas.** A configuração vem
   depois, por endpoints com seus próprios gates.
5. **A concessão aparece nas duas telas** — detalhe do usuário e detalhe da
   empresa — sobre um **único serviço** no backend.
6. **Recorte do concedente (postura A no eixo de empresa):** um caller não-owner
   só concede ou revoga empresas às quais ele próprio tem acesso; o owner é
   isento. Vira **ADR-0014** (ver "Decisão a registrar").

## Backend — criar usuário

### Endpoint

`POST /users`, gate `[RequirePermission(AccessControlPermissions.UsersManage)]`.

A descrição da chave `users.manage` passa a **"Criar, listar, ativar e desativar
usuários"** no catálogo. A chave em si **não muda** — renomear chave invalida
histórico (ADR-0012); só o texto de exibição é atualizado, e o reconciliador de
catálogo já propaga alteração de `display_name`/`description` no boot.

### Contrato

```
CreateUserRequest(string FullName, string Email, string Password)
```

Allow-list estrita. `OrganizationId`, `IsOwner`, `Status` e `EmailConfirmed`
**não existem no DTO** — são fixados no servidor, então não há campo a ignorar:

- `OrganizationId` = organização do caller (via `CallerAsync`, nunca do request);
- `IsOwner` = `false` (owner é gerido fora da aplicação — ADR-0012);
- `Status` = `Active`;
- `EmailConfirmed` = `true`, coerente com o `DataSeeder`. Sem e-mail transacional
  não há fluxo de confirmação, e a aplicação não exige conta confirmada para
  login; marcar `false` bloquearia o usuário sem que exista caminho de saída.

Resposta: `UserDto` (mesma forma já usada em `GET /users/{id}`), com `201` e
`Location` para `/users/{id}`.

### Regras

- Criação via `UserManager<ApplicationUser>.CreateAsync`, que aplica a política de
  senha vigente (`RequiredLength = 8` mais os defaults do Identity: dígito,
  maiúscula, minúscula e não-alfanumérico) e a unicidade de e-mail. **Não se
  reimplementa validação de senha**; o Identity é a autoridade.
- `UserName` = `Email`. `FullName` e `Email` são normalizados com `Trim()`.
- Falhas do `IdentityResult` viram `400` com mensagem legível.
- E-mail já em uso responde `400` com **mensagem neutra**, sem revelar
  organização, status nem se a conta é de outro tenant.

### Atomicidade da auditoria

`UserManager.CreateAsync` chama `SaveChanges` por conta própria. Duas armadilhas
decorrem disso, e a solução cobre as duas:

- registrar o evento **antes** deixaria um `AuditEvent` pendurado no change
  tracker caso a criação falhasse na validação — ele seria persistido pelo
  próximo `SaveChanges` do request, produzindo "evento de algo que não
  aconteceu", exatamente o que a ADR-0013 proíbe;
- registrar **depois**, em um `SaveChanges` separado, permite que o usuário
  exista sem o evento se a segunda escrita falhar.

O serviço abre uma **transação explícita** (`db.Database.BeginTransactionAsync`)
cobrindo `CreateAsync` + `audit.Record` + `SaveChangesAsync` + `Commit`. Assim o
usuário e seu evento nascem juntos ou não nascem.

### Auditoria

`access_control.user.created` — forma (c) da ADR-0013 (criação, sem `old`/`new`):

```json
{ "full_name": "Maria Silva", "email": "maria@exemplo.com" }
```

`entity_type` = `"User"`, `entity_id` = id do usuário criado, `actor_user_id` =
caller. **Nunca** senha, hash ou qualquer derivado da credencial.

## Backend — concessão de acesso a empresa

### Permissão nova

Em `CompaniesPermissions` (módulo `organizations`):

```
companies.grant_access — "Conceder acesso a empresas"
  "Conceder e revogar o acesso de usuários às empresas."
```

O catálogo é reconciliado no boot, então a chave nova entra sem migração de
dados. Ela **não** é adicionada a nenhum perfil automaticamente — exceto ao
perfil `is_system` "Administrador", que por construção recebe todas as permissões
ativas.

### Endpoints

Um único serviço, `CompanyAccessService` (módulo `organizations`), atende as duas
telas — as regras vivem em um lugar só:

| Endpoint | Gate | Uso |
| --- | --- | --- |
| `PUT /users/{id}/companies` | `companies.grant_access` | tela do usuário: define o conjunto de empresas daquele usuário |
| `GET /companies/{id}/users` | `companies.grant_access` | tela da empresa: usuários da org e quem tem acesso |
| `PUT /companies/{id}/users` | `companies.grant_access` | tela da empresa: define o conjunto de usuários daquela empresa |

Ambos os `PUT` recebem o **conjunto completo** e o serviço calcula o delta —
simétrico ao `PUT /users/{id}/profiles` já existente.

```
SetUserCompaniesRequest(IReadOnlyList<Guid>? CompanyIds)
SetCompanyUsersRequest(IReadOnlyList<Guid>? UserIds)
```

`GET /companies/{id}/users` devolve apenas `id`, `fullName`, `email` e
`hasAccess` — o mínimo para escolher. Quem pode conceder acesso precisa saber a
quem, mas não recebe o restante do cadastro.

### Regras

- **Tenancy:** usuário alvo, empresa alvo e todo id do conjunto pedido precisam
  pertencer à organização do caller. Fora dela → **404** (não vaza existência).
- **Empresa soft-deleted** (`DeletedAt != null`) não pode ser concedida → 404.
- **Recorte do concedente:** para caller **não-owner**, cada empresa
  **adicionada ou removida** precisa estar no `UserCompanyAccess` do próprio
  caller; senão **403**. A verificação incide sobre o **delta**, não sobre o
  conjunto inteiro — assim um caller que não acessa a empresa X consegue salvar
  alterações em um usuário que já tem X, sem ser forçado a removê-la. É a mesma
  mecânica de delta que a postura B já usa em `UserService.SetProfilesAsync`.
- **Owner caller é isento do recorte:** concede qualquer empresa da organização.
  É o piso antilockout já aceito na ADR-0012 e o que destrava uma empresa órfã,
  cujo único usuário com acesso foi desativado.
- **Owner alvo é editável neste eixo** — diferença deliberada em relação a status
  e perfis, onde o owner é somente-leitura. O owner está sujeito ao eixo de
  empresa (ADR-0012), então precisa poder receber acesso; e como é isento do
  recorte, sempre consegue se reconceder. Não há lockout a proteger aqui.
- **Concorrência:** violação da PK composta (`23505`) e remoção concorrente
  (`DbUpdateConcurrencyException`) são traduzidas para **409**, como já é feito
  em `SetProfilesAsync`.
- A auto-concessão de quem cria empresa (`CompanyService.CreateAsync`) permanece
  como está.

### Auditoria

`organizations.user.company_access_granted` e
`organizations.user.company_access_revoked` — forma (b) da ADR-0013 (vínculo):

```json
{ "company_id": "…", "company_name": "Empresa Demo", "old": false, "new": true }
```

`entity_type` = `"User"`, `entity_id` = id do usuário alvo. Um evento por empresa
tocada, nos dois sentidos.

**Nota de nomenclatura.** O nome levantado inicialmente foi
`organizations.company_access.granted`. Adotou-se
`organizations.user.company_access_granted` porque a ADR-0013 pede que
`entity_type` case com a entidade da `action`, e o alvo aqui é o **usuário**. É
também o precedente já praticado: `access_control.user.profile_assigned` audita
um vínculo tendo o usuário como entidade-alvo. O `company_name` acompanha o id
pela regra do rótulo humano (ADR-0013, seção 3).

## Frontend

Autorização continua 100% no backend; o frontend é espelho de UX.

- **`/users/new`** — formulário com nome, e-mail, senha e confirmação, validado
  com Zod (a confirmação é conferida só no cliente; a força da senha é decidida
  pelo backend, que devolve a mensagem do Identity). Botão "Novo usuário" na
  lista `/users`, visível com `users.manage`. Ao salvar, redireciona para
  `/users/{id}`, onde perfis e empresas já estão à mão.
- **`/users/{id}`** — o card "Empresas acessíveis", hoje somente-leitura, vira
  checklist editável sob `companies.grant_access`, no mesmo padrão do
  `UserProfilesForm`. O texto atual, que promete uma gestão "no módulo de
  Empresas" inexistente, sai.
- **`/companies/{id}`** — nova seção "Usuários com acesso", com o mesmo padrão de
  checklist.
- Toda lista nova nasce **responsiva** (tabela vira cartões abaixo de `md`),
  conforme `apps/web/CLAUDE.md`. A dívida `ui-polimento-listas-mobile` não
  aumenta.
- A senha nunca é ecoada de volta pela API nem persistida em estado do cliente
  além do submit.

## Testes

TDD: o teste vem antes da implementação.

### Integração (Postgres real)

Criação de usuário:

- cria com sucesso e devolve `201` com o usuário;
- `403` sem `users.manage`; `401` sem sessão;
- e-mail duplicado → `400` com mensagem neutra;
- senha fraca ou curta → `400`;
- o usuário criado nasce `Active`, **sem perfis e sem empresas**, e sua permissão
  efetiva é **vazia**;
- o usuário criado consegue **logar** e recebe `/auth/me` sem permissões nem
  empresas;
- `organizationId` é sempre o do caller, mesmo que o JSON traga campos extras
  (anti mass-assignment);
- `access_control.user.created` é emitido, com `actor_user_id` correto e sem
  qualquer resquício de credencial no `metadata`.

Concessão de acesso:

- concede e revoga, nos dois endpoints, com efeito real na listagem de empresas
  do usuário alvo;
- `403` sem `companies.grant_access`;
- não-owner **não** concede empresa à qual não tem acesso (`403`), e **não** a
  revoga;
- não-owner consegue salvar um alvo que já possui empresa fora do recorte, desde
  que não a toque;
- owner concede qualquer empresa da organização;
- usuário ou empresa de outra organização → `404`; empresa soft-deleted → `404`;
- owner alvo pode ter empresas alteradas;
- os dois eixos permanecem independentes: ter `UserCompanyAccess` sem
  `companies.access` não lista a empresa;
- eventos `company_access_granted` / `_revoked` emitidos com `old`/`new` corretos.

### Frontend

- Unit: helper de diff do conjunto de empresas (mesmo espírito do
  `user-profiles.ts`).
- E2E: criar usuário e conceder acesso a uma empresa, sobre a stack real.

## Riscos aceitos

- **Senha inicial conhecida pelo administrador.** Sem troca obrigatória, quem
  cria a conta retém indefinidamente uma credencial válida de outra pessoa e pode
  autenticar-se como ela. Todo `actor_user_id` daquele usuário passa a ser
  contestável, o que enfraquece o valor da auditoria recém-padronizada pela
  ADR-0013. Mitigação de v1 é **detecção, não prevenção**: `user.created` registra
  quem criou a conta. A eliminação do risco fica amarrada ao convite por e-mail —
  entra no backlog como dependência do e-mail transacional, e não como item
  isolado.
- **Oráculo de existência de e-mail entre organizações.** `RequireUniqueEmail`
  torna o e-mail único globalmente, consequência do login por e-mail sem seleção
  de tenant. Um admin da organização A que tenta criar um e-mail já existente na
  organização B descobre que ele existe em algum lugar. Mitigado pela mensagem
  neutra; eliminar exigiria e-mail único **por organização**, o que tornaria o
  login ambíguo e é decisão de outra ordem.
- **Lacuna conhecida da postura B permanece.** Quem tem `profiles.manage` ainda
  pode criar um perfil não-`is_system` com todas as permissões e atribuí-lo. A
  criação de usuários não abre esse caminho, mas o torna mais discreto: em vez de
  elevar a si mesmo, o ator pode elevar uma conta-fantoche cuja senha conhece. A
  postura A no eixo funcional continua no backlog, agora com um motivo a mais.

## Decisão a registrar (ADR-0014)

O recorte do concedente é a **primeira adoção de postura A** no projeto, ainda
que restrita ao eixo de empresa. A ADR-0012 adiou a postura A por ser caro
comparar conjuntos de permissões a cada concessão; no eixo de empresa a
comparação é um conjunto de ids, então sai barato. Por ser uma postura de
segurança que futuras IAs precisam encontrar pelo índice de decisões — e não
enterrada numa spec — vira **ADR-0014**, referenciando a ADR-0012 e registrando:
a regra, a isenção do owner, o motivo de o eixo funcional continuar em postura B,
e o impacto (nenhuma migração de dados; a chave nova entra pelo reconciliador).

## Documentação a atualizar

- `docs/modules/access-control.md`: criar usuário entra no escopo; sai de "Fora de
  Escopo" a linha sobre cadastro de usuários (o **convite por e-mail** continua
  fora); novo evento `access_control.user.created`; descrição de `users.manage`.
- `docs/modules/organizations.md`: permissão `companies.grant_access`, os três
  endpoints, os dois eventos novos e a remoção de "UI para conceder/revogar
  acesso" de "Fora de Escopo".
- `docs/decisions/README.md`: linha da ADR-0014.
- `docs/specs/backlog.md`: convite por e-mail passa a carregar também a dívida da
  troca obrigatória de senha.

## Critérios de aceite

- [ ] `POST /users` cria usuário na organização do caller, com `users.manage`.
- [ ] Usuário criado nasce `Active`, sem perfis, sem empresas, com permissão
      efetiva vazia, e consegue autenticar.
- [ ] Campos sensíveis nunca vêm do cliente; `organizationId` sempre do caller.
- [ ] `companies.grant_access` existe no catálogo e governa os três endpoints.
- [ ] Não-owner só concede/revoga empresas dentro do próprio acesso; owner é
      isento.
- [ ] Cross-tenant responde 404 em todos os caminhos novos.
- [ ] Eventos `user.created`, `company_access_granted` e `company_access_revoked`
      emitidos na mesma unidade de trabalho, sem credenciais no `metadata`.
- [ ] Telas: criar usuário, editar empresas no detalhe do usuário e gerir
      usuários no detalhe da empresa — todas responsivas.
- [ ] Revisão pela skill `security-engineer` concluída.
- [ ] Backend, unit do frontend e e2e verdes.
