# Modulo: <module-name>

> Template de documentacao de modulo backend (ADR-0008). Copie este arquivo para
> `docs/modules/<module-name>.md`, use ingles kebab-case no nome do arquivo e
> preencha as secoes em portugues. Remova esta citacao ao criar o modulo.

## Objetivo

<O que este modulo resolve, em um paragrafo.>

## Escopo

- <O que pertence ao modulo.>

## Fora de Escopo

- <O que explicitamente nao pertence.>

## Entidades Envolvidas

- `<Entity>`: <proposito, propriedade por organizacao (`organization_id`) quando aplicavel — ADR-0005.>

## Casos de Uso

- <Acao suportada na camada de application.>

## Regras de Negocio

- <Invariante ou validacao critica do dominio.>

## Autorizacao e Tenancy

- Papeis que podem agir: <owner | admin | member> (ADR-0006).
- Filtro de tenant: <como a organizacao ativa restringe leituras e escritas — ADR-0005.>

## Criterios de Aceite

- [ ] <Condicao verificavel de conclusao, ligada a um teste.>

## Eventos de Auditoria

- `<action>`: <quando o AuditEvent e emitido pelo caso de uso — ADR-0005.>

## Dependencias

- Modulos: <outros modulos dos quais este depende.>
- ADRs: <ADRs que restringem este modulo.>

## Validacao Esperada

- Unit: <regras de dominio, validators, policies, casos de uso.>
- Integracao: <endpoints, persistencia, autorizacao, isolamento de tenant.>

## Decisoes Relacionadas

- <ADR-XXXX: motivo do vinculo.>
