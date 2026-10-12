# Registros de decisão — Aether

Data-base: 2026-10-12. Estes registros consolidam as Partes 1–3 e definem a base
para implementação. **Aceito não significa implementado**: progresso no
[plano](../implementation/plan.md) e no [README](../../README.md).

| ADR | Decisão | Status |
|---|---|---|
| [0001](0001-monolito-modular.md) | Monólito modular e fronteiras de módulos | Aceito |
| [0002](0002-plataforma-dotnet.md) | .NET 10 e política de dependências | Aceito |
| [0003](0003-isolamento-tenant.md) | Isolamento multi-tenant e identidade | Aceito |
| [0004](0004-persistencia-identidade.md) | PostgreSQL, UUIDv7 e preparação para particionamento | Aceito |
| [0005](0005-outbox-inbox.md) | Outbox, inbox e entrega pelo menos uma vez | Aceito |
| [0006](0006-mensageria.md) | Topologia RabbitMQ, retry e QoS | Aceito |
| [0007](0007-api-idempotencia.md) | Contratos REST e idempotência | Aceito |
| [0008](0008-dominio-estados.md) | Estados, tentativas e resultados desconhecidos | Aceito |
| [0009](0009-provedores.md) | Adapters, circuitos e fallback | Aceito |
| [0010](0010-governanca.md) | Governança de envio e limitação distribuída | Aceito |
| [0011](0011-seguranca-privacidade.md) | Segredos, PII e ciclo de vida | Aceito |
| [0012](0012-callbacks.md) | Callbacks autenticados e eventos fora de ordem | Aceito |
| [0013](0013-operacao.md) | Observabilidade, readiness e recuperação | Aceito |
| [0014](0014-entrega-incremental.md) | Entrega incremental e estratégia de validação | Aceito |
| [0015](0015-provedor-email-inicial.md) | Resend como primeiro adapter de E-mail | Aceito |
| [0016](0016-catalogo-tenant-e-publisher.md) | Catálogo tenancy e publisher com privilégios separados | Aceito |

## Processo

Novo ADR descreve contexto, decisão, alternativas, consequências e validação.
Não reescrever silenciosamente uma decisão aceita: novo registro marca a anterior
como substituída e explica a mudança. Ajustes editoriais são permitidos.

Valores de retry, retenção, prefetch e capacidade do chat são propostas iniciais.
Fornecedor de identidade, providers comerciais, versões da infraestrutura e SLOs
ficam no registro de pendências, sem bloquear a fundação.
