# ADR-0012: Perfis Configuraveis e Permissoes (substitui papeis fixos da ADR-0006)

## Status

Aceita

## Contexto

A ADR-0006 (Aceita) definiu autenticacao e autorizacao com **papeis fixos e
pequenos** por organizacao (`owner`, `admin`, `member`) e declarou
explicitamente que permissoes configuraveis e granulares por campo ou recurso
ficam **fora do MVP**. A ADR-0010 refinou o modelo para tres niveis
(`Organization` -> `Company` -> acesso por usuario via `UserCompanyAccess`) e, na
implementacao do modulo `organizations`, os papeis viraram `ApplicationUser.
orgRole` in `{ Admin, Member }` (um usuario pertence a uma organizacao).

Ao evoluir o produto surgiu a necessidade concreta de que cada organizacao possa
**definir seus proprios perfis de acesso**, atribuindo a cada perfil um conjunto
de permissoes, e vincular usuarios a um ou mais perfis. Papeis fixos nao
atendem: organizacoes diferentes precisam de recortes de acesso diferentes (ex.:
um perfil que so visualiza, outro que gerencia empresas, outro que administra
usuarios), e isso deve ser configuravel sem alterar codigo.

Esta ADR substitui **parcialmente** a ADR-0006 na parte de autorizacao por
papeis. Autenticacao, sessao em cookie httpOnly, convite/recuperacao e auditoria
da ADR-0006 permanecem validos. O isolamento por organizacao e o acesso por
empresa da ADR-0010 permanecem intactos.

Design detalhado:
`docs/specs/2026-07-19-access-control-perfis-permissoes-design.md`
(inclui revisao de seguranca).

## Decisao

Adotar um modelo de **perfis configuraveis com permissoes**, por organizacao, em
um novo modulo backend `AccessControl`, substituindo os papeis fixos.

### Modelo

- **Perfis substituem papeis.** `orgRole` (`Admin`/`Member`) e removido. Toda a
  autorizacao funcional passa a vir de perfis.
- **`Permission`**: catalogo de permissoes, **fixo no codigo** (cada modulo
  declara suas chaves estaveis `<modulo>.<capacidade>`) e **projetado** numa
  tabela reconciliada no boot (upsert idempotente). Chave que some do codigo vira
  `obsolete` (nao e deletada); a foreign key `ProfilePermission -> Permission` e
  a trava contra permissoes inexistentes.
- **`Profile`**: escopo organizacao, com `name`, `description`, `is_system`,
  `status` (`active`/`archived`). Toda org nasce com um perfil-semente
  "Administrador" (`is_system`, todas as permissoes).
- **`UserProfile`**: um usuario pode ter **varios perfis**; a permissao efetiva e
  a **uniao** das permissoes dos perfis `active` vinculados.
- **Owner**: `is_owner` (flag tecnico) substitui o papel `owner`. Tem bypass
  funcional completo, mas continua sujeito ao eixo de empresa. E **gerido fora da
  aplicacao** (banco no MVP; superadmin externo no futuro); a aplicacao nunca
  cria, remove nem edita owner, e o trata como somente-leitura na gestao de
  usuarios.

### Dois eixos de autorizacao

A decisao de acesso combina, no backend:

1. **Funcional** (o que pode fazer) — via perfis, escopo organizacao.
2. **Dados/empresa** (onde pode fazer) — via `UserCompanyAccess` (ADR-0010).

Sem a permissao funcional **ou** sem acesso a empresa, a acao e negada; acesso a
recurso de outra organizacao ou empresa sem concessao responde 404.

### Granularidade (v1 e evolucao)

- **v1**: permissao por **funcionalidade** (item de menu). O enforcement e
  aplicado no backend via authorization policies parametrizadas por chave de
  permissao; frontend apenas esconde por UX.
- **Evolucao (nao agora, sem quebrar o modelo)**: permissao por **acao**
  (`ver`/`criar-editar`/`excluir`), regras de **posse/field-level**, e perfil
  escopado por **empresa**.

### Postura de seguranca contra escalada de privilegio (postura B)

- Perfis `is_system` (o "Administrador") so sao **atribuiveis pelo owner**.
- `profiles.manage` e `profiles.assign` sao **privilegios administrativos de
  fato** no v1 (quem os detem e um administrador de confianca), nao capacidades
  granulares seguras. A evolucao para a postura A ("nao conceder alem de si")
  fica registrada como melhoria futura (backlog).
- Campos sensiveis (`is_system`, `is_owner`, `organization_id`, `status` de
  perfil) nunca sao aceitos do cliente (allow-list; anti mass-assignment).
- Owner e o piso de administracao garantido (somente-leitura na app), eliminando
  o cenario de "org trancada por dentro".

### Auditoria

Reusar o `AuditEvent` (ADR-0005), **emitindo** no v1 os eventos de mutacao
sensiveis (perfil criado/editado/arquivado com delta de permissoes; perfis
atribuidos/removidos de usuario; usuario ativado/desativado). O visualizador de
auditoria fica adiado. Adota-se um **contrato padronizado** de `AuditEvent`
(`actor_user_id`, `occurred_at`, `organization_id`, `action`, `target`,
`details`) para viabilizar relatorios transversais; a padronizacao formal para
todos os modulos sera objeto de ADR propria.

## Consequencias

- Novo modulo `AccessControl` com `Permission`, `Profile`, `ProfilePermission`,
  `UserProfile`; `ApplicationUser` ganha `is_owner` e **perde** `orgRole`.
- O reconciliador de catalogo roda no boot do `Seed.Api`, apos migrations.
- Migracao em duas fases: (1) cria estruturas, popula `Permission` e o perfil
  "Administrador" por org, marca `Admin` atual como `is_owner` e vincula ao
  "Administrador"; membros ficam sem perfil; (2) remove a coluna `orgRole`.
  Consequencia assumida: membros migrados sem perfil perdem acesso funcional
  (inclusive ver empresas) ate receberem um perfil.
- O modulo `organizations` passa a declarar `companies.access` e
  `companies.manage` no lugar do gate por `orgRole=Admin`.
- Autorizacao continua 100% no backend; frontend nunca e barreira.
- Requer nova documentacao de modulo (`docs/modules/access-control.md`, ADR-0008).

## Alternativas Consideradas

### Manter papeis fixos da ADR-0006

Rejeitada. Nao permite que cada organizacao defina seus proprios recortes de
acesso, que e o requisito concreto que motivou esta ADR.

### Permission como constantes em codigo, sem tabela (chaves string soltas)

Rejeitada para o v1. Simples, mas sem integridade referencial (permite conceder
permissao inexistente) e sem lugar consultavel para metadados de exibicao. A
tabela projetada do codigo, com FK, da a trava e alimenta a UI sem abrir mao do
codigo como fonte de verdade.

### Permissoes assadas nas claims do cookie no login

Rejeitada. Rapida, mas fica desatualizada ate novo login, conflitando com o
requisito da ADR-0006 de bloqueio de acesso imediato e com a troca de perfil
valer na hora.

### Postura A anti-escalada agora ("nao conceder alem de si")

Adiada. E o alvo correto de longo prazo, mas adiciona complexidade
(comparacao de conjuntos de permissao em toda concessao) sem necessidade
imediata; o v1 trata `profiles.manage`/`profiles.assign` como privilegio
administrativo (postura B) e restringe os perfis `is_system`.

## Validacao

Esta decisao permanece valida se:

- nenhum acesso critico depender apenas do frontend;
- os dois eixos (funcional + empresa) forem sempre aplicados no backend;
- revogacao de permissao/perfil bloquear no proximo request (sem cache entre
  requests);
- perfis `is_system` so forem atribuiveis pelo owner e o owner permanecer
  somente-leitura na app;
- campos sensiveis nunca forem aceitos do cliente;
- tentativa de escalada e acesso cross-tenant forem bloqueadas e cobertas por
  teste;
- o modelo rodar em Linux com Docker sem dependencia obrigatoria de provedor.

## Proximos Passos

- Escrever o plano de implementacao a partir do design de 2026-07-19.
- Ao aceitar esta ADR, marcar a ADR-0006 como "parcialmente substituida pela
  ADR-0012" no indice e atualizar o `README.md` das decisoes.
- Documentar o modulo `AccessControl` (`docs/modules/access-control.md`).
- Registrar a ADR de padronizacao do `AuditEvent` (backlog).
