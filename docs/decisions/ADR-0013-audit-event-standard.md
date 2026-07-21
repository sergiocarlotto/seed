# ADR-0013: Padrao do AuditEvent (taxonomia, contrato e retencao)

## Status

Aceita

## Contexto

A ADR-0005 criou o `AuditEvent` como registro de eventos de negocio, mas sem
definir **como** preencher os campos. O modulo `access_control` (ADR-0012) foi o
primeiro a emitir eventos de verdade e, ao faze-lo, precisou inventar convencoes
na marcha: nomes de acao, formato do `metadata` e um contrato `old`/`new` para
descrever o que mudou.

Hoje **apenas o `access_control` emite eventos**; o `organizations` ainda nao
emite os dele. Padronizar agora custa quase nada (nao ha historico a migrar) e
evita que cada modulo novo repita a invencao com um formato diferente — o que
inviabilizaria consultas transversais ("tudo que fulano alterou", "historico
desta entidade") justamente quando elas passarem a ser necessarias.

A entidade atual tem: `id`, `organization_id`, `actor_user_id`, `action`,
`entity_type`, `entity_id`, `occurred_at`, `metadata` (JSON serializado).

## Decisao

### 1. Taxonomia de `action`

Formato fixo de tres partes, em `snake_case`:

```
<modulo>.<entidade>.<verbo_no_passado>
```

- `modulo`: nome do modulo backend (`access_control`, `organizations`).
- `entidade`: entidade de dominio no singular (`profile`, `user`, `company`).
- `verbo`: o que aconteceu, **no passado** (`created`, `updated`, `archived`,
  `deleted`, `status_changed`, `permission_granted`, `permission_revoked`,
  `profile_assigned`, `profile_removed`).

Exemplos validos: `access_control.profile.created`,
`access_control.user.status_changed`, `organizations.company.deleted`.

A chave de acao e **estavel**: renomear uma acao invalida o historico, entao um
nome so muda criando um novo (o antigo permanece no historico).

### 2. Campos obrigatorios

- `organization_id`: sempre preenchido (o evento pertence a um tenant — ADR-0005).
- `actor_user_id`: usuario da sessao; `null` apenas para acoes do sistema
  (boot, migration, job).
- `entity_type`: nome da entidade em `PascalCase` (`Profile`, `User`, `Company`).
- `entity_id`: identificador do alvo, como string.
- `occurred_at`: UTC, vindo do `IClock` (nunca `DateTime.Now`).

### 3. Contrato do `metadata`

JSON de objeto plano. Tres formas, conforme a natureza da mudanca:

**a) Mudanca de valor de campo** — um evento **por campo alterado**:

```json
{ "field": "name", "old": "Financeiro", "new": "Financeiro Junior" }
```

**b) Vinculo concedido ou revogado** — identifica o alvo do vinculo e usa
`old`/`new` booleanos:

```json
{ "permission_key": "companies.manage", "profile_name": "Financeiro",
  "old": false, "new": true }
```

**c) Criacao** — os valores iniciais relevantes, sem `old`/`new`:

```json
{ "name": "Financeiro", "description": "Acesso ao modulo financeiro" }
```

Regras:

- **So se registra o que mudou.** Campo inalterado nao gera evento.
- `old`/`new` carregam o **valor legivel** (nome, status como texto), nao o
  objeto inteiro nem o enum numerico.
- Alem da chave tecnica de um vinculo, inclua um **rotulo humano** quando existir
  (`profile_name` junto de `profile_id`), para o historico continuar legivel
  depois que a entidade for renomeada ou removida.
- **Nunca** gravar segredo ou credencial (senha, hash, token, cookie de sessao),
  nem dado pessoal alem do necessario para identificar o alvo.

### 4. Emissao

O evento e gravado na **mesma unidade de trabalho** da mutacao: o `IAuditLog`
apenas adiciona a entidade ao contexto e o servico chamador persiste tudo junto.
Consequencia intencional: se a operacao falha, o evento **nao** e gravado — nao
existe evento de auditoria descrevendo algo que nao aconteceu.

Auditoria registra **mutacao de negocio bem-sucedida**. Nao substitui log de
aplicacao: tentativa negada (401/403), erro e diagnostico continuam no log
estruturado, nao no `AuditEvent`.

### 5. Retencao

No MVP os eventos sao **mantidos indefinidamente**: o volume e baixo e nao ha,
hoje, requisito legal ou contratual levantado que imponha prazo — nem para
guardar, nem para descartar.

O expurgo automatico fica adiado ate existir **volume real** que o justifique.
Quando isso ocorrer, a politica (prazo, expurgo ou particionamento por periodo,
e eventual exportacao antes do descarte) sera decidida em ADR propria, com o
dado de crescimento em maos. Ate la, `occurred_at` e `organization_id` ja
permitem recortar por periodo e por tenant sem mudanca de esquema.

## Consequencias

- Todo modulo novo que emitir auditoria segue esta taxonomia e este contrato; a
  secao "Eventos de Auditoria" da doc de modulo (ADR-0008) lista as acoes com os
  nomes ja no formato final.
- O `organizations`, ao passar a emitir seus eventos, usa
  `organizations.company.*` (e nao `company.*`, como sua doc de modulo indicava).
- Os eventos ja emitidos pelo `access_control` **ja estao** neste padrao: ele foi
  extraido do que aquele modulo praticou, entao nao ha reescrita de historico
  nem alteracao de codigo decorrente desta ADR.
- Consultas transversais por ator, entidade, periodo ou organizacao ficam
  possiveis sem interpretar formatos por modulo.
- `metadata` continua sendo JSON serializado em texto; se a consulta por conteudo
  do JSON virar requisito, migrar a coluna para `jsonb` sera uma decisao propria.

## Alternativas Consideradas

### Event sourcing / historico completo por entidade

Rejeitada. Resolveria "estado em qualquer ponto do tempo", mas e desproporcional
ao MVP e mudaria o modelo de persistencia inteiro (ADR-0005).

### Guardar o registro inteiro (snapshot) em `old`/`new`

Rejeitada. Infla o armazenamento, dificulta ler o que de fato mudou e aumenta o
risco de gravar campo sensivel por descuido.

### Deixar cada modulo definir seu formato

Rejeitada. E o estado que esta ADR corrige: sem contrato comum, consulta
transversal exige conhecer o formato de cada modulo.

### Fixar ja um prazo de retencao (ex.: 24 meses)

Rejeitada por ora. Sem volume real nem requisito levantado, o prazo seria
arbitrario — e descartar auditoria e irreversivel.

## Validacao

Esta decisao permanece valida se:

- as acoes seguem `<modulo>.<entidade>.<verbo_no_passado>` e sao estaveis;
- eventos de alteracao trazem `field`/`old`/`new` (ou o par de vinculo) e nunca
  segredo;
- o evento e persistido atomicamente com a mutacao que o originou;
- nenhum modulo novo introduz formato proprio de `metadata`;
- a politica de retencao e revisada em ADR propria quando houver volume.
