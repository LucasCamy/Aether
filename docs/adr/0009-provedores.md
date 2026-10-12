# ADR-0009 — Adapters, circuitos e fallback

- Status: Aceito como direção arquitetural; implementação conforme milestones.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

SDKs e semânticas variam por produto. Provedor secundário pode compartilhar falha
com o primário ou não possuir remetente/template compatível.

## Decisão

Strategy decide elegibilidade, canal e seleção; Adapter isola SDK/HTTP e callbacks.
Plugins são pacotes aprovados em deployment, nunca código de tenant.
Declarar capacidades, schema, autenticação, idempotência e reconciliação.

Circuito por conta/configuração/canal/endpoint; erro de destinatário não abre circuito.
429 controla quota. Half-open tem sondagens limitadas. Fallback no mesmo canal cria
tentativa; entre canais libera Dispatch bloqueado. Exigir conteúdo, permissões,
remetentes e templates compatíveis. Unknown não aciona fallback automático.

## Alternativas consideradas

Adapter genérico sem capacidades perde recursos. Trocar provedor após todo timeout
gera duplicidade. Load de assembly não oferece sandbox de segurança.

## Consequências

E-mail aceito com deferral aguarda o provedor. WhatsApp revalida janela/template;
SMS estima codificação/segmentos; Telegram vincula bot+chat. Push/Webhooks possuem
capacidades e controles de destino próprios. Fornecedor comercial segue pendente.

## Validação e revisão

Validar fixtures e timeout ambíguo antes de habilitar externo. Health check não envia
mensagens ao cliente. Referência: https://learn.microsoft.com/en-us/azure/architecture/patterns/circuit-breaker
