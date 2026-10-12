# ADR-0014 — Entrega incremental e estratégia de validação

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Criar todas as integrações antes de validar aceitação e isolamento amplia risco.
A estrutura compilável não oferece garantias de negócio.

## Decisão

M0: hosts/módulos/ADRs. M1: E-mail imediato com identidade, RLS, idempotência,
Request/Dispatch/Outbox atômicos e GET. M2: transporte/consumo com adapter controlado.
M3: provider real, callback e governança. M4: bulk, agenda e demais canais.

Testes de domínio para regras; PostgreSQL real para transação/RLS; RabbitMQ real para
confirms/ACK; fixtures para providers. Não usar EF InMemory para provar SQL.
SDKs e infraestrutura entram quando a slice exige. Atualizar estado implementado
na documentação em cada entrega.

## Alternativas consideradas

Implementar seis providers em paralelo mascara falhas comuns. Simular 202 com memória
contradiz persistência durável. Cobrir apenas happy path não prova resiliência.

## Consequências

Banco de identidade, provider comercial, SLO e plataforma continuam pendentes,
mas têm pontos de decisão no plano. Infra dev não contém credenciais de produção.
API/Worker podem escalar separados mantendo o monólito modular.

## Validação e revisão

M0: Release e smoke. M1: concorrência/rollback/tenant. M2: crash windows.
M3: consentimento, timeout e callbacks. Detalhes em docs/implementation/plan.md.
