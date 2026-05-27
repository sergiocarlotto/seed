# ADR-0006: Autenticacao e Autorizacao

## Status

Pendente

## Contexto

O Seed precisa controlar acesso de usuarios, papeis, organizacoes e dados
operacionais antes de implementar fluxos reais de uso.

A ADR-0003 define que autenticacao, autorizacao, multitenancy e regras
criticas pertencem ao backend. A ADR-0005 deve definir a propriedade dos dados
e a raiz de tenancy antes desta decisao ser finalizada.

## Decisao Pendente

Definir a estrategia inicial de autenticacao e autorizacao para o MVP.

Esta ADR deve decidir:

- mecanismo de login;
- modelo inicial de usuarios e organizacoes;
- papeis iniciais;
- enforcement de autorizacao no backend;
- relacao entre usuario, organizacao e tenant atual;
- estrategia de sessoes, tokens ou cookies;
- limites do frontend em validacao de acesso;
- requisitos minimos de auditoria de acesso.

## Direcao Inicial

O backend deve continuar sendo a fonte de verdade para autorizacao. O frontend
pode esconder acoes por experiencia de usuario, mas nao pode ser a barreira de
seguranca.

O MVP deve evitar permissoes altamente granulares. Um modelo simples de papeis
por organizacao deve ser preferido ate haver necessidade concreta.

## Pontos Ainda Abertos

- Usar identidade nativa do ASP.NET Core ou provedor externo open source.
- Definir se a autenticacao inicial sera por email/senha, magic link ou outro
  fluxo simples.
- Definir papeis iniciais, como owner, admin, member e client.
- Definir como usuarios com acesso a mais de uma organizacao selecionam o
  contexto atual.
- Definir politica de convite, ativacao e desativacao de usuarios.
- Definir requisitos de recuperacao de senha e seguranca minima.

## Validacao

Esta decisao podera ser aceita se:

- nenhum acesso critico depender apenas do frontend;
- usuarios conseguirem acessar apenas organizacoes autorizadas;
- papeis iniciais forem suficientes para o MVP;
- a estrategia funcionar em Linux com Docker;
- a decisao nao criar dependencia obrigatoria de provedor especifico.
