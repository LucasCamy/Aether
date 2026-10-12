# ADR-0007 — Contratos REST e idempotência

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Clientes repetem chamadas após timeout; Redis pode perder estado. Resposta 202 precisa
ter semântica durável e previsível.

## Decisão

Versionar /v1. Headers de negócio: Authorization, X-Tenant-ID e X-Correlation-ID;
X-Idempotency-Key para criação e cancelamento. Chave única no tenant, operação e
payload canônico entram no hash. PostgreSQL decide; Redis acelera.

POST retorna 202 somente após commit; replay retorna IDs/corpo original e sinaliza
Idempotency-Replayed. Hash diferente produz 409. GET consulta estado atual.
Bulk limitado tem aceitação atômica e entrega independente. Agendamento indica
elegibilidade; cancelamento não garante reversão externa. Erros usam Problem Details.

## Alternativas consideradas

Idempotência só Redis é vulnerável a expiração/failover. Responder 200 no replay com
estado atual mudaria o significado da resposta de criação.

## Consequências

Ingestão M1 oferece apenas E-mail imediato e consulta. Bulk/agendamento/cancelamento
são evolução, não endpoints fictícios. Retenção da chave cobre agendamento e replay.
Correlation ID não é deduplicador; validar formato e evitar log injection.

## Validação e revisão

Testar requisições concorrentes, hash canônico, payload diferente, resposta perdida
e recursos de outro tenant. Limites do chat são defaults a validar, não SLA.
