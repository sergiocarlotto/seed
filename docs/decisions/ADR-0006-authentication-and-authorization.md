# ADR-0006: Autenticacao e Autorizacao

## Status

Aceita

## Contexto

O Seed precisa controlar acesso de usuarios, papeis, organizacoes e dados
operacionais antes de implementar fluxos reais de uso.

A ADR-0003 define que autenticacao, autorizacao, multitenancy e regras criticas
pertencem ao backend em C# e ASP.NET Core. A ADR-0005 (Aceita) define
PostgreSQL com Entity Framework Core, `Organization` como raiz de tenancy e
isolamento logico por `organization_id`, alem do `AuditEvent` base. Esta ADR
fecha a estrategia inicial de autenticacao e autorizacao sobre essas bases.

## Decisao

### Provedor de identidade

Usar **ASP.NET Core Identity** como base de autenticacao, persistido no
PostgreSQL via EF Core. Nao adotar servidor de identidade externo (Keycloak ou
equivalente) no MVP.

Racional: Identity ja faz parte da stack backend aceita, e open source, roda em
Linux/Docker sem servico adicional e nao cria dependencia obrigatoria de
provedor. Um servidor de identidade dedicado adiciona operacao e complexidade
que o MVP com equipe interna nao justifica.

### Mecanismo de login

Login inicial por **email e senha**. O hashing de senha usa o mecanismo padrao
do ASP.NET Core Identity (PBKDF2). Magic link e login social ficam fora do MVP.

Convite, ativacao e recuperacao de senha usam email, mas o login em si nao
depende de entrega de email em tempo real.

### Estrategia de sessao

A sessao vive em **cookie `HttpOnly`, `Secure`, `SameSite`**, emitido pelo
backend. Nao usar JWT armazenado no browser (`localStorage`/`sessionStorage`)
como mecanismo de sessao, para eliminar roubo de token por XSS.

Web e API devem ser servidos sob o mesmo dominio (mesma origem) atraves de
reverse proxy, de forma que o cookie seja same-site. A configuracao concreta de
reverse proxy pertence a ADR-0007.

### Modelo de identidade e tenancy

- `Organization` e a raiz de tenancy (ADR-0005).
- `User` e global dentro da instancia e pode pertencer a mais de uma
  organizacao.
- O vinculo usuario/organizacao e explicito em `OrganizationMembership`, com no
  minimo: `user_id`, `organization_id`, `role`, `status`.
- O "tenant atual" (organizacao ativa) e resolvido no backend a partir da sessao
  autenticada e validado contra `OrganizationMembership` a cada request. O
  `organization_id` nunca e aceito do frontend como fonte de verdade.
- Um usuario com multiplas organizacoes seleciona a organizacao ativa de forma
  explicita; a troca de contexto e registrada em auditoria quando tiver impacto
  operacional.

### Papeis iniciais

Papeis por organizacao (escopo de `OrganizationMembership`), mantidos pequenos:

| Papel | Proposito |
| --- | --- |
| `owner` | Dono da organizacao. Controle total, incluindo gestao de usuarios, papeis e configuracao. |
| `admin` | Gestao operacional completa: projetos, tarefas, clientes, templates e usuarios, exceto transferir a propriedade da organizacao. |
| `member` | Usuario interno que executa trabalho: cria e atualiza projetos, tarefas e comentarios conforme atribuicao. |

O papel `client` (acesso externo do cliente com visibilidade limitada) e
**reservado, mas adiado**. Ele pertence ao marco "primeira versao com visao do
cliente" do Marco Zero e nao e implementado neste MVP. O enum de papeis pode
reservar o valor, sem regras de visibilidade externa ainda.

Permissoes altamente granulares por campo ou por recurso ficam fora do MVP.

### Enforcement

- A autorizacao e aplicada no backend, na camada de application, via
  authorization policies baseadas em papel e no tenant atual.
- Toda leitura e escrita de entidade de escopo organizacional passa por um
  filtro de tenant que conhece a organizacao ativa.
- Relacionamentos entre entidades de organizacoes diferentes sao bloqueados na
  camada de application/domain (reforcando a ADR-0005).
- O frontend pode esconder acoes por experiencia de usuario, mas nunca e a
  barreira de seguranca.
- Acesso negado deve ter comportamento previsivel e nao vazar existencia de
  recursos de outra organizacao.

### Convite, ativacao, recuperacao e desativacao

- Convite: enviado por email com **token assinado, curto e de uso unico**. A
  aceitacao cria/ativa o `User` e o `OrganizationMembership` e define a senha.
- Recuperacao de senha: fluxo por email com **token de uso unico e vida curta**.
- Desativacao: muda o `status` do `User` ou do `OrganizationMembership` para um
  estado que **bloqueia acesso imediatamente**, sem exclusao fisica (alinhado a
  ADR-0005, que prefere estados de ativacao a exclusao).
- Login e recuperacao devem ter **rate limiting** e politica minima de senha.

### Auditoria de acesso

Reusar o `AuditEvent` da ADR-0005 para eventos de identidade e acesso, comecando
pelo minimo:

- login bem-sucedido e login falho relevante;
- logout quando relevante;
- convite enviado, aceito, expirado e revogado;
- alteracao de papel;
- ativacao e desativacao de usuario;
- troca de organizacao ativa com impacto operacional;
- tentativa de acesso negado relevante;
- mudanca de configuracao de seguranca.

## Testes Obrigatorios

- acesso permitido, acesso negado e **tentativa de acesso cross-tenant**;
- usuario com multiplas organizacoes so enxerga dados da organizacao ativa;
- token de convite ou recuperacao expirado ou reutilizado e rejeitado;
- usuario desativado tem acesso bloqueado imediatamente;
- regra critica de autorizacao nao pode ser contornada pelo frontend;
- cookie de sessao e `HttpOnly` e `Secure` e nao e legivel por JavaScript.

## Consequencias

- `Seed.Infrastructure` inclui ASP.NET Core Identity sobre EF Core/PostgreSQL.
- O modelo de dados ganha `User`, `Organization`, `OrganizationMembership` e as
  tabelas de Identity, com `organization_id` nas entidades de escopo
  organizacional (ADR-0005).
- O backend expoe uma abstracao explicita de "tenant atual" antes de qualquer
  endpoint operacional.
- Web e API precisam ser servidos same-origin em desenvolvimento e producao
  (entrada para a ADR-0007).
- O frontend implementa telas de login, convite, ativacao e recuperacao, mas sem
  logica de autorizacao critica.
- Fluxos de convite e recuperacao dependem de envio de email; a estrategia de
  email transacional deve ser definida quando esses fluxos forem implementados.

## Alternativas Consideradas

### Keycloak ou servidor de identidade externo

Rejeitado para o MVP. Oferece OIDC, SSO e federacao prontos, mas adiciona um
servico dedicado para operar, versionar e proteger, sem necessidade concreta
para uma equipe interna no estagio atual. Pode ser reconsiderado por nova ADR se
surgir exigencia de SSO corporativo ou identidade federada.

### JWT armazenado no browser

Rejeitado como mecanismo de sessao. Util para APIs de terceiros e clientes
mobile futuros, mas armazenar token no browser aumenta o risco de roubo por XSS
e dificulta revogacao e logout. Pode ser adotado por nova ADR para integracoes
especificas, sem virar o mecanismo de sessao da aplicacao web.

### Magic link como login principal

Rejeitado para o MVP. Elimina senha, mas cria dependencia dura de entrega de
email rapida e confiavel no caminho critico de login diario.

### Implementar o papel `client` agora

Adiado. Antecipar acesso externo amplia a superficie de seguranca (visibilidade
limitada, regras de exposicao para fora da organizacao) antes de o fluxo interno
estar estavel. Pertence a um marco posterior do Marco Zero.

## Validacao

Esta decisao permanece valida se:

- nenhum acesso critico depender apenas do frontend;
- usuarios conseguirem acessar apenas organizacoes autorizadas;
- os papeis iniciais forem suficientes para o MVP;
- a estrategia rodar em Linux com Docker;
- a decisao nao criar dependencia obrigatoria de provedor especifico;
- o cookie de sessao permanecer `HttpOnly`/`Secure` e resistente a XSS;
- tentativa de acesso cross-tenant for bloqueada e coberta por teste.

## Proximos Passos

- Refletir o modelo `User`/`Organization`/`OrganizationMembership` no primeiro
  modelo fisico do backend (ADR-0005).
- Definir na ADR-0007 o reverse proxy e o same-origin entre web e API para
  viabilizar o cookie de sessao.
- Definir a estrategia de email transacional antes de implementar convite e
  recuperacao de senha.
