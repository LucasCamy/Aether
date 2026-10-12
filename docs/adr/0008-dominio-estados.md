# ADR-0008 — Estados, tentativas e resultados desconhecidos

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

Aceitação no provedor, entrega e leitura representam fatos distintos. Timeout e
reentrega não justificam automaticamente um novo envio.

## Decisão

Notifications é owner da máquina de estados. Execução admite Scheduled,
BlockedByPolicy, Pending, InFlight, RetryScheduled, OutcomeUnknown, ProviderAccepted,
Failed, Cancelled e Expired. Entrega e engajamento são dimensões separadas.

Persistir tentativa antes da chamada e finalizar seu resultado sem reescrita.
Resultado ambíguo entra em OutcomeUnknown; somente reconciliação ou decisão
auditada libera próximo passo. Alterações usam version/generation/fencing.
Policy holds não contam como tentativa. Estados terminais ignoram trabalho obsoleto.

## Alternativas consideradas

Uma única flag sent não representa callbacks, atrasos ou dúvida. Reabrir Dispatch
terminal por reentrega quebra invariantes. Timeout como falha segura duplica envios.

## Consequências

Cancelar InFlight é intenção, não prova de interrupção. ExpiresAt limita novas
tentativas, não desfaz envio aceito. Conteúdo/destino funcionalmente congelados;
correção exige nova solicitação. Privacidade pode eliminar conteúdo.

## Validação e revisão

Testes de transições e concorrência devem existir antes de provider real.
Callbacks fora de ordem preservam fatos e não aplicam simples last-write-wins.
