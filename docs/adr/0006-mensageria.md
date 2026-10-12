# ADR-0006 — Topologia RabbitMQ, retry e QoS

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Os canais e prioridades têm latências distintas; requeue imediato causa loops e
tenants grandes podem consumir capacidade de outros.

## Decisão

Comandos usam comm.dispatch.v1 Direct com rota channel.priority.lane.
Eventos usam comm.events.v1 Topic. Retry e dead usam exchanges Direct próprias.
Um comando possui uma fila de execução; não duplicar bindings de trabalho.

Filas duráveis, mensagens persistentes, quorum e confirmações manuais.
Retries de negócio são registrados via outbox; filas de TTL fixo retornam por DLX.
Configurar explicitamente dead-lettering at-least-once e políticas compatíveis.
NACK sem requeue leva poison à DLQ, não decide nível de backoff.

## Alternativas consideradas

Prioridade só dentro de uma fila não reserva recursos. Kafka é apropriado a log/replay,
mas exigiria outro desenho de retries. Plugin de atraso não é requisito inicial.

## Consequências

Concorrência e prefetch são controles diferentes. Valores do chat são propostas:
começar conservador, medir e ajustar. Definir delivery-limit e distinguir retry
de envio de reentrega técnica. DLQ não faz replay automático.

## Validação e revisão

Testar rota sem binding, falha de destino DLX, expiração, limite de reentrega e
justiça entre prioridades. Filas/lane por tenant apenas quando justificadas.
Referências: https://www.rabbitmq.com/docs/dlx ; https://www.rabbitmq.com/docs/quorum-queues
