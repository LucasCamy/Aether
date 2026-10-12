# ADR-0005 — Outbox, inbox e entrega pelo menos uma vez

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Não há transação local atômica envolvendo PostgreSQL e RabbitMQ. Um crash também pode
ocorrer após envio externo e antes do registro do resultado.

## Decisão

Request, Dispatch, idempotência e Outbox compartilham commit de aceitação.
Publisher faz polling com leases e SKIP LOCKED, sem manter transação durante rede.
Marca PublishedAt somente depois de confirm e sem retorno mandatory unroutable.
NOTIFY pode despertar polling; não é transporte durável. CDC fica para necessidade futura.

Consumidor usa geração, lease/fencing e inbox com estado incompleto/concluído.
ACK somente após decisão durável; retry usa estado+outbox no mesmo commit.
Deduplicar mensagem não é suficiente para uma chamada externa exatamente uma vez.

## Alternativas consideradas

Publicação dentro da requisição mantém dual-write. Transação distribuída aumenta
acoplamento. Marcar inbox como concluída antes de enviar perde trabalho.

## Consequências

Falha entre confirm e PublishedAt gera duplicata. Falha após chamada externa pode
gerar OutcomeUnknown, exigindo reconciliação. Mesmo fencing não desfaz efeito externo
de um Worker que perdeu lease.

## Validação e revisão

Exercitar crash antes/depois de publish, confirm, resultado e ACK.
Verificar leases expirados e duplicatas. Métrica obrigatória: idade da outbox.
Referência: https://www.rabbitmq.com/docs/confirms
