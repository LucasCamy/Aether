# ADR-0012 — Callbacks autenticados e eventos fora de ordem

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Provedores enviam formatos diferentes, repetem eventos e podem emitir callback
antes de a plataforma persistir a correlação externa.

## Decisão

POST /v1/provider-callbacks/{provider}/{endpointId} recebe formato nativo.
Endpoint ID localiza autenticação, não é segredo. Preservar bytes para validação
de assinatura. Resolver tenant por conta/recurso autenticado e binding configurado.
Meta também possui desafio GET validado. Não exigir headers da API de negócio.

Persistir receipt autenticado e responder 200 sem esperar atualização do Dispatch.
Worker expande eventos, deduplica com chave nativa ou derivada por adapter,
correlaciona e atualiza estado com outbox. Eventos sem correlação ficam pendentes.
Timestamps de ocorrência e recebimento são separados.

## Alternativas consideradas

Exigir JSON universal inviabiliza Twilio form. Deduplicar só corpo inteiro falha
quando o provedor rearranja lotes. Responder sucesso antes de persistir perde eventos.

## Consequências

Receipt técnico pode conter múltiplos tenants e não é exposto à API pública.
Conta+configuração fazem parte da correlação. Não presumir assinatura universal Zenvia.
Evolução aditiva autenticada pode ir a classificação posterior, não loop de rejeição.

## Validação e revisão

Fixtures por produto/versão, assinatura inválida, repetição, callback adiantado e
eventos fora de ordem. Referência: https://www.twilio.com/docs/usage/webhooks/webhooks-security
