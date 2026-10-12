# ADR-0016 — Contratos de banco para tenancy e publicação

- Status: Aceito e implementado no incremento inicial.
- Data: 2026-10-12
- Projeto: Aether

## Contexto

RLS tenantizada não atende um publisher global com a mesma credencial da API.
O catálogo mínimo de tenant precisa de ownership explícito, sem consulta direta
da aplicação de Notifications às regras internas de Tenancy.

## Decisão

Catálogo reside em tenancy.tenants. Notifications mantém somente mapeamento
referencial excluído de migrations futuras e FKs; a admissão consulta o contrato
SQL tenancy.is_active com SECURITY INVOKER e RLS no contexto da transação.
Criação histórica do catálogo e movimentação estão nas migrations de bootstrap.
Evoluções do catálogo pertencem a Tenancy; não ampliar esse mapping para regras.

API/consumer usam aether_runtime, sem ownership ou BYPASSRLS. Publisher usa
aether_publisher sem SELECT direto na outbox. As funções claim/complete/release
têm SECURITY DEFINER, search_path fixo, PUBLIC revogado e dono NOLOGIN com
BYPASSRLS restrito a SELECT/UPDATE da outbox. Não conceder membership nesse dono.

## Alternativas

Credencial administrativa no Worker ampliaria exposição. Enumerar tenants em
configuração não escala. Um serviço Tenancy separado não é necessário neste estágio.

## Consequências e validação

Migrations/provisionamento exigem capacidade administrativa; não usar essas credenciais
na API. Os nomes de roles são fixos nesta versão. Funções são superfície privilegiada
e exigem revisão. Múltiplos consumidores futuros precisam namespacing da inbox.
Testes reais verificam isolamento, falta de contexto, role publisher e fencing.
