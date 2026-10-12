# ADR-0015 — Resend como primeiro adapter de E-mail

- Status: Aceito para implementação; conta externa ainda não provisionada.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

O usuário estima menos de 500 E-mails mensais e prioriza baixo custo.
Precisamos de API, callbacks, supressões e comportamento claro em resultados incertos.

## Decisão

Priorizar Resend. Seu plano gratuito cobre o volume informado, condicionado ao
limite diário e de domínios. Usar HTTPS, chave por conta e Idempotency-Key estável
por Dispatch. A janela externa de deduplicação é limitada; não usar esse recurso
como garantia perpétua nem para autorizar fallback entre provedores.

O adapter é testado com fixtures e não está conectado ao consumer de produção.
O consumer atualmente habilitável é controlado, somente em Development.

## Alternativas

Amazon SES favorece baixo custo variável, mas requer configuração AWS, identidades
e saída do sandbox. Mantê-lo como candidato ao crescimento. SendGrid não oferece
vantagem econômica inicial frente à franquia suficiente. Brevo possui limite diário
gratuito diferente; também pode ser adapter posterior.

## Consequências e validação

O custo de envio não representa o custo de hospedagem do Aether. Vários tenants com
domínios próprios podem exceder a franquia ou precisar de contas separadas.
Não contratar plano nem cadastrar cartão automaticamente.
Produção exige conta, domínio validado, segredo, callbacks e governança conectados.
Revisitar em mudança de volume, picos ou número de domínios.

Fontes verificadas em 2026-10-12:
- [Resend](https://resend.com/pricing)
- [Idempotência](https://resend.com/docs/dashboard/emails/idempotency-keys)
- [SES](https://aws.amazon.com/ses/pricing/)
- [Sandbox SES](https://docs.aws.amazon.com/ses/latest/dg/request-production-access.html)
