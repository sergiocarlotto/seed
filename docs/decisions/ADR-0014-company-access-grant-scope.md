# ADR-0014: Escopo de Concessão de Acesso a Empresa

## Status

Aceita

## Contexto

A ADR-0012 estabeleceu dois eixos de autorização: o **funcional** (perfis) e o
de **empresa** (`UserCompanyAccess`). Até agora o segundo eixo só era escrito em
dois pontos — a auto-concessão de quem cria a empresa e o seed —, então nunca
houve a pergunta "quem pode conceder acesso a quem".

Com a permissão `companies.grant_access` a pergunta passa a existir, e com ela um
caminho de escalada: sem recorte, quem detém a permissão alcança os dados de
**todas** as empresas da organização simplesmente concedendo acesso a si mesmo.
Isso colapsaria o eixo de empresa dentro do eixo funcional — exatamente o
contrário do que a ADR-0012 decidiu manter independente.

A ADR-0012 adiou a **postura A** ("não conceder além de si") no eixo funcional
por custo: comparar conjuntos de permissões a cada concessão é caro e invasivo.
No eixo de empresa a comparação é um conjunto de ids.

## Decisão

Adotar a **postura A no eixo de empresa**, por meio de um único conceito:

**Escopo concedível do chamador**

- **owner** → todas as empresas não excluídas da organização;
- **não-owner** → as empresas do próprio `UserCompanyAccess`.

Regras derivadas:

1. Toda empresa citada num pedido de concessão ou revogação precisa estar no
   escopo concedível do chamador. Fora dele a resposta é **404**, e não 403:
   uma empresa da organização à qual o chamador não tem acesso já é hoje
   indistinguível de inexistente (comportamento do `CompanyService`), e um 403
   revelaria sua existência.
2. `PUT /users/{id}/companies` define o conjunto de empresas do usuário **dentro
   do escopo do chamador**. Concessões fora desse escopo são **preservadas**, não
   removidas por ausência no payload. Sem isso a regra se contradiria: a tela do
   chamador só lista o que ele pode conceder.
3. O **owner é isento** por ter a organização inteira como escopo. É o mesmo
   piso antilockout da ADR-0012, e é o que destrava uma **empresa órfã** — aquela
   cujo único usuário com acesso foi desativado, ou aquela cujas concessões foram
   todas revogadas.
   Para que isso seja verdade, o owner tem **bypass também na leitura** do eixo
   de empresa: ele enxerga todas as empresas da própria organização, com ou sem
   concessão explícita. Sem esse bypass a isenção seria vazia — o owner teria
   escopo total de escrita e nenhum caminho para descobrir o id da órfã, e a
   recuperação exigiria acesso ao banco. O filtro por organização nesse ramo é
   obrigatório e vem sempre da sessão: é o que separa "dono da organização" de
   "vê tudo no banco".
4. O **owner alvo** pode ter suas empresas alteradas, ao contrário de status e
   perfis, onde é somente-leitura. O motivo **não** é sujeição ao eixo de empresa
   — a regra 3 o isenta: é que alterar as concessões dele **não cria risco de
   lockout** (seu alcance vem do bypass, não das linhas de `UserCompanyAccess`) e
   as concessões continuam sendo registro útil, tanto para a tela espelho quanto
   para a auditoria. Bloquear a edição protegeria contra nada e criaria um caso
   especial a mais.

O eixo funcional **permanece em postura B**: `profiles.manage` e
`profiles.assign` continuam privilégios administrativos de fato, com perfis
`is_system` restritos ao owner.

## Consequências

- `companies.grant_access` não vale, na prática, "acesso a todos os dados da
  organização": o alcance de quem a detém é limitado pelo próprio acesso.
- Um administrador que não acessa nenhuma empresa não concede nenhuma. É o
  resultado pretendido; o owner é o caminho de destravamento.
- Nenhuma migração de dados: a chave nova entra pelo reconciliador de catálogo no
  boot e é concedida ao perfil de sistema pelo top-up do bootstrapper.
- O projeto passa a ter duas posturas convivendo — A no eixo de empresa, B no
  funcional. A assimetria é deliberada e vem do custo de implementação, não de
  uma diferença de risco; a evolução do eixo funcional para a postura A segue no
  backlog.
- **O owner deixa de estar sujeito ao eixo de empresa.** A ADR-0012 dizia que o
  bypass do owner era só funcional e que ele "continua sujeito ao eixo de
  empresa"; com o bypass de leitura desta ADR, isso passa a valer apenas para os
  demais usuários. A mudança é a consequência necessária da regra 3 e substitui
  aquela frase da ADR-0012 no que toca **exclusivamente ao owner**.
- Como o mesmo caminho de leitura serve a editar e excluir empresa, o owner
  também passa a editar e excluir qualquer empresa da organização sem concessão
  prévia. Isso **não amplia o poder efetivo dele**: o owner já podia se conceder
  acesso a qualquer empresa (regra 3 + regra 4) e então editá-la — o bypass
  elimina o passo intermediário, não uma barreira. Continua atrás do gate
  `companies.manage`, que o owner detém pelo bypass funcional da ADR-0012.
- Revogar todas as concessões de uma empresa deixa de ser destrutivo em termos
  práticos: a empresa some para os não-owners, mas o owner continua a alcançando.

## Alternativas Consideradas

### Sem recorte (qualquer empresa da organização)

Rejeitada. Implementação mais simples, mas transforma `companies.grant_access`
em acesso universal aos dados por autoconcessão, anulando o segundo eixo.

### Recorte sem isenção do owner

Rejeitada. Regra uniforme, sem caso especial, mas deixa uma empresa órfã
inalcançável pela aplicação — e o owner, que existe justamente para ser o piso
antilockout, precisaria do banco para destravá-la.

### Responder 403 para empresa fora do escopo

Rejeitada. Mensagem mais clara ao operador, mas revela a existência de empresas
que ele hoje não consegue distinguir de inexistentes, criando um vazamento que o
resto do módulo não tem.

## Validação

Esta decisão permanece válida se:

- nenhum não-owner conceder ou revogar empresa fora do próprio acesso;
- empresa fora do escopo continuar respondendo 404, nunca 403;
- concessões fora do escopo do chamador forem preservadas em vez de removidas;
- o owner mantiver escopo total e continuar capaz de destravar empresa órfã;
- o bypass de leitura do owner permanecer **limitado à própria organização** —
  é a única linha que separa esta decisão de um vazamento entre tenants, e ela
  tem de continuar coberta por teste;
- as regras permanecerem cobertas por teste de integração, **nas duas direções**
  (usuário→empresa e empresa→usuário).

## Decisões Relacionadas

- ADR-0012 (perfis configuráveis, dois eixos, postura B) — decisão que esta
  refina no eixo de empresa.
- ADR-0010 (modelo multiempresa, origem do `UserCompanyAccess`) — esta ADR a
  **substitui parcialmente**: a regra de "visibilidade por empresa sempre
  explícita, inclusive o admin" deixa de valer **para o owner**, que passa a ver,
  editar e excluir qualquer empresa da própria organização sem concessão. Para os
  demais usuários a regra da ADR-0010 permanece integral.
- ADR-0013 (padrão do `AuditEvent`) — contrato dos eventos de concessão.
- Design: `docs/specs/2026-07-21-user-provisioning-company-access-design.md`.
