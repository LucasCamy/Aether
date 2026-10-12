# ADR-0013 — Observabilidade, readiness e recuperação

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Processo vivo não significa capacidade de aceitar ou enviar. Profundidade de fila
sozinha esconde idade e mensagens próximas da expiração.

## Decisão

Instrumentar OpenTelemetry para HTTP, DB, outbox, consumo e chamada externa.
Etapas assíncronas usam spans/links, sem span aberto durante agendamento.
Métricas: aceitação, espera elegível, latência externa, resultados, Unknown,
outbox oldest age, callbacks, DLQ, quotas e capacidade. Labels de baixa cardinalidade;
tenant/Dispatch/destino não entram indiscriminadamente em métricas.

Bootstrap oferece só /health/live. Readiness real nasce com dependências; indisponibilidade
do broker pode permitir aceitação enquanto orçamento de backlog comportar.
Broker: preservar outbox e drenar gradualmente. Redis: DB para cache/idempotência,
envio pausado ou orçamento degradado aprovado. Provedor: reduzir taxa e reconciliar dúvida.

## Alternativas consideradas

Health único verde esconderia ausência de serviços. Fail-open global no Redis viola
quota. Drenagem sem limites após recuperação pode causar nova queda.

## Consequências

Nenhum SLO numérico aceito sem volume e testes. Separar tempo bruto e espera por
política. Runbooks e alertas entram junto aos mecanismos, não após incidente.

## Validação e revisão

Smoke atual valida somente vida dos hosts. Exercitar falhas e recuperação em M2/M3.
Referência: https://opentelemetry.io/docs/specs/semconv/messaging/messaging-metrics/
